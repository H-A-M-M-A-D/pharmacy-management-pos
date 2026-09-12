using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Reports;

public sealed partial class ReportingService
{
    private static string? NewAnalyticsDimension(string report) => report switch
    {
        "sales/products" or "profitability/products" or "profitability/discount-impact" => "product", "sales/categories" => "category",
        "sales/cashiers" => "cashier", "sales/by-customer" or "profitability/by-customer" => "customer",
        "sales/retail-vs-wholesale" => "type", "sales/by-price-level" => "price-level",
        "sales/trend" or "sales/daily-summary" => "day", "sales/monthly" => "month", "sales/hourly" => "hour",
        "sales/manufacturers" or "profitability/manufacturers" => "manufacturer",
        "sales/branches" or "profitability/branches" => "branch", "sales/godowns" or "profitability/godowns" => "godown",
        "profitability/categories" => "category", "profitability/invoices" => "invoice",
        "sales/invoice-metrics" => "total", "sales/return-analysis" => "invoice",
        _ => null
    };

    private async Task EnsureGodownScope(User actor, ReportQuery query, CancellationToken ct)
    {
        foreach (var id in new[] { query.GodownId, query.SourceGodownId, query.DestinationGodownId }.Where(x => x.HasValue).Select(x => x!.Value).Distinct())
        {
            var godown = await repository.GetReportGodownAsync(id, ct) ?? throw new RequestValidationException("Godown was not found.");
            if (actor.Role?.Name is not (RoleCatalog.Owner or RoleCatalog.Manager) &&
                (godown.BranchId != actor.BranchId || !await repository.HasGodownAccessAsync(actor.Id, id, ct)))
                throw new ForbiddenOperationException("The current user cannot access this godown.");
            if (query.GodownId == id && query.BranchId.HasValue && godown.BranchId != query.BranchId)
                throw new RequestValidationException("Godown does not belong to the selected branch.");
        }
    }

    private async Task<object> ManagementReportAsync(Guid actorId, User actor, string report, ReportQuery q, string? option, CancellationToken ct)
    {
        if (report == "management/filters") return await repository.FilterOptionsAsync(actor, ct);
        if (report is "management/daily" or "management/monthly")
        {
            var local = q.FromUtc.AddHours(5);
            var start = report == "management/daily"
                ? new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Utc).AddHours(-5)
                : new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-5);
            q = q with { FromUtc = start, ToUtc = report == "management/daily" ? start.AddDays(1) : start.AddMonths(1) };
            option = report == "management/monthly" ? "month" : null;
        }
        if (report == "management/godowns")
        {
            if (!Has(actor, PermissionCatalog.ReportsInventory)) throw new ForbiddenOperationException("Inventory report permission is required.");
            return ProtectCostFields(await repository.GodownManagementAsync(q, ct), actor);
        }
        if (report == "management/branches")
        {
            if (!Has(actor, PermissionCatalog.ReportsSales) || !Has(actor, PermissionCatalog.ReportsPurchases) ||
                !Has(actor, PermissionCatalog.ReportsInventory) || !Has(actor, PermissionCatalog.ReportsFinancial) ||
                !Has(actor, PermissionCatalog.ReportsProfitability) || !Has(actor, PermissionCatalog.AccountsJournalView))
                throw new ForbiddenOperationException("Branch MIS requires sales, purchasing, inventory, financial and profitability report permissions.");
            ValidateFinancialDimensions(q);
            return await repository.BranchManagementAsync(q, ct);
        }
        if (report is "management/financial" or "management/working-capital" or "management/budget")
        {
            if (!Has(actor, PermissionCatalog.ReportsFinancial) || !Has(actor, PermissionCatalog.AccountsJournalView) || !Has(actor, PermissionCatalog.ReportsProfitability))
                throw new ForbiddenOperationException("Financial and profitability permissions are required.");
            ValidateFinancialDimensions(q);
            if (report == "management/budget")
            {
                if (budgets is null) throw new InvalidOperationException("Budget service is unavailable.");
                var date = ReportCalculations.KarachiDay(DateOnly.FromDateTime(q.FromUtc.AddHours(5)));
                var local = date.FromUtc.AddHours(5);
                var actual = await budgets.GetBudgetVsActualAsync(actorId, new(local.Year, option == "year" ? null : local.Month, q.BranchId), ct);
                var groups = actual.Rows.Where(x => x.AccountType is AccountType.Income or AccountType.CostOfSales or AccountType.Expense)
                    .GroupBy(x => x.AccountType).Select(g => new
                    {
                        Group = g.Key == AccountType.Income ? "Revenue" : g.Key == AccountType.CostOfSales ? "COGS" : "Expenses",
                        Budget = g.Sum(x => x.BudgetAmount), Actual = g.Sum(x => x.ActualAmount), Variance = g.Sum(x => x.ActualAmount - x.BudgetAmount),
                        VariancePercent = g.Sum(x => x.BudgetAmount) == 0 ? (decimal?)null : g.Sum(x => x.ActualAmount - x.BudgetAmount) / Math.Abs(g.Sum(x => x.BudgetAmount)) * 100
                    }).ToList();
                decimal Sum(AccountType type, bool budget) => actual.Rows.Where(x => x.AccountType == type).Sum(x => budget ? x.BudgetAmount : x.ActualAmount);
                var profitBudget = Sum(AccountType.Income, true) - Sum(AccountType.CostOfSales, true) - Sum(AccountType.Expense, true);
                var profitActual = Sum(AccountType.Income, false) - Sum(AccountType.CostOfSales, false) - Sum(AccountType.Expense, false);
                return new { actual.FiscalYear, actual.PeriodNumber, actual.BranchId, actual.Rows, Groups = groups,
                    Profit = new { Budget = profitBudget, Actual = profitActual, Variance = profitActual - profitBudget,
                        VariancePercent = profitBudget == 0 ? (decimal?)null : (profitActual - profitBudget) / Math.Abs(profitBudget) * 100 } };
            }
            if (accounting is null) throw new InvalidOperationException("Accounting service is unavailable.");
            var position = await repository.FinancialPositionAsync(q, ct);
            if (report == "management/working-capital")
            {
                var stock = (await repository.InventoryExposureAsync(q, false, ct)).Sum(x => x.CostValue);
                var mappedCurrentAssets = position.Cash + position.Bank + position.Receivables + stock + position.SupplierAdvances;
                var mappedCurrentLiabilities = position.Payables + position.CustomerAdvances;
                return new { position, InventoryValue = stock, MappedCurrentAssets = mappedCurrentAssets, MappedCurrentLiabilities = mappedCurrentLiabilities,
                    NetWorkingCapital = mappedCurrentAssets - mappedCurrentLiabilities, CurrentRatio = (decimal?)null,
                    Method = "Mapped cash, bank, AR, inventory, supplier advances, AP and customer advances. COA has no current/non-current classification; these are identified components, not a complete current ratio." };
            }
            return new { Position = position,
                ProfitAndLoss = await accounting.GetProfitAndLossAsync(actorId, q.FromUtc, q.ToUtc.AddTicks(-1), q.BranchId, ct),
                BalanceSheet = await accounting.GetBalanceSheetAsync(actorId, q.ToUtc.AddTicks(-1), q.BranchId, ct),
                CashFlow = await accounting.GetCashFlowStatementAsync(actorId, q.FromUtc, q.ToUtc.AddTicks(-1), q.BranchId, ct) };
        }
        if (report is not ("management/overview" or "management/comparison" or "management/daily" or "management/monthly"))
            throw new RequestValidationException("Unknown management report.");
        var scoped = repository.WithFilters(q);
        var previous = ReportPeriods.Previous(q, option);
        var hasSales = Has(actor, PermissionCatalog.ReportsSales);
        var hasPurchases = Has(actor, PermissionCatalog.ReportsPurchases) && CanViewCost(actor) && q.CustomerId is null && q.UserId is null && q.SaleType is null && q.PriceLevelId is null;
        var hasInventory = Has(actor, PermissionCatalog.ReportsInventory) && q.CustomerId is null && q.UserId is null && q.SaleType is null && q.PriceLevelId is null;
        var hasProfit = Has(actor, PermissionCatalog.ReportsProfitability);
        var clock = report == "management/overview" ? timeProvider.GetUtcNow().UtcDateTime : q.FromUtc;
        var businessNow = clock.AddHours(5);
        var today = new DateTime(businessNow.Year, businessNow.Month, businessNow.Day, 0, 0, 0, DateTimeKind.Utc).AddHours(-5);
        var month = new DateTime(businessNow.Year, businessNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-5);
        var year = new DateTime(businessNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-5);
        async Task<decimal> CalendarSale(DateTime start, DateTime end) =>
            (await scoped.SalesAnalyticsAsync(q with { FromUtc = start, ToUtc = end, Page = 1, PageSize = 1 }, "total", ct)).Items.SingleOrDefault()?.NetSales ?? 0;
        async Task<decimal> CalendarPurchase(DateTime start, DateTime end) =>
            (await scoped.PurchaseAnalyticsAsync(q with { FromUtc = start, ToUtc = end, Page = 1, PageSize = 1 }, "total", ct)).Items.SingleOrDefault()?.NetPurchaseValue ?? 0;
        object? calendarSales = hasSales ? new { SalesToday = await CalendarSale(today, today.AddDays(1)),
            SalesThisMonth = await CalendarSale(month, month.AddMonths(1)), SalesThisYear = await CalendarSale(year, year.AddYears(1)) } : null;
        object? calendarPurchases = hasPurchases ? new { PurchasesToday = await CalendarPurchase(today, today.AddDays(1)),
            PurchasesThisMonth = await CalendarPurchase(month, month.AddMonths(1)) } : null;
        var hasFinance = Has(actor, PermissionCatalog.ReportsFinancial) && Has(actor, PermissionCatalog.AccountsJournalView) && CanViewCost(actor) &&
            q.GodownId is null && q.ProductId is null && q.CategoryId is null && q.ManufacturerId is null && q.CustomerId is null && q.SupplierId is null && q.UserId is null && q.SaleType is null && q.PriceLevelId is null;
        var sales = hasSales || hasProfit ? (await scoped.SalesAnalyticsAsync(q with { Page = 1, PageSize = 1 }, "total", ct)).Items.SingleOrDefault() : null;
        var priorSales = hasSales || hasProfit ? (await scoped.SalesAnalyticsAsync(previous with { Page = 1, PageSize = 1 }, "total", ct)).Items.SingleOrDefault() : null;
        object? salesPayload = hasSales ? sales : null;
        if (hasSales && q.ProductId is null && q.BatchId is null && q.CategoryId is null && q.ManufacturerId is null && q.SupplierId is null)
        {
            var documentTotals = await scoped.SalesSummaryAsync(q.BranchId, q.FromUtc, q.ToUtc, ct);
            var payload = System.Text.Json.JsonSerializer.SerializeToNode(sales, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) as System.Text.Json.Nodes.JsonObject ?? new();
            payload["paymentsAtSale"] = documentTotals.PaymentsAtSale;
            payload["creditCreated"] = documentTotals.CreditCreated;
            salesPayload = payload;
        }
        var purchases = hasPurchases ? (await scoped.PurchaseAnalyticsAsync(q with { Page = 1, PageSize = 1 }, "total", ct)).Items.SingleOrDefault() : null;
        var priorPurchases = hasPurchases ? (await scoped.PurchaseAnalyticsAsync(previous with { Page = 1, PageSize = 1 }, "total", ct)).Items.SingleOrDefault() : null;
        var aging = hasInventory ? await scoped.InventoryExposureAsync(q, false, ct) : [];
        var expiry = hasInventory ? await scoped.InventoryExposureAsync(q, true, ct) : [];
        var trend = hasSales ? (await scoped.SalesAnalyticsAsync(q with { Page = 1, PageSize = 200 }, q.ToUtc - q.FromUtc > TimeSpan.FromDays(200) ? "month" : "day", ct)).Items : [];
        ProfitAndLossDto? pnl = null; ProfitAndLossDto? priorPnl = null;
        object? finance = null;
        if (hasFinance && accounting is not null)
        {
            pnl = await accounting.GetProfitAndLossAsync(actorId, q.FromUtc, q.ToUtc.AddTicks(-1), q.BranchId, ct);
            priorPnl = await accounting.GetProfitAndLossAsync(actorId, previous.FromUtc, previous.ToUtc.AddTicks(-1), q.BranchId, ct);
            var position = await scoped.FinancialPositionAsync(q, ct);
            var ar = Has(actor, PermissionCatalog.AccountsAgingReceivablesView) ? await accounting.GetArAgingSummaryAsync(actorId, q.ToUtc.AddTicks(-1), q.BranchId, q.CustomerId, ct) : null;
            var ap = Has(actor, PermissionCatalog.AccountsAgingPayablesView) ? await accounting.GetApAgingSummaryAsync(actorId, q.ToUtc.AddTicks(-1), q.BranchId, q.SupplierId, ct) : null;
            var bookQuery = new CashBankBookQuery(q.BranchId, q.FromUtc, q.ToUtc.AddTicks(-1), 1, 1);
            var cash = await accounting.GetCashBookAsync(actorId, bookQuery, ct);
            var bank = await accounting.GetBankBookAsync(actorId, bookQuery, ct);
            finance = new { Position = position, ReceivablesAging = ar, PayablesAging = ap,
                OverdueReceivables = ar is null ? (decimal?)null : ar.Total - ar.Current,
                OverduePayables = ap is null ? (decimal?)null : ap.Total - ap.Current,
                CashReceipts = cash.TotalReceipts, CashPayments = cash.TotalPayments, BankReceipts = bank.TotalReceipts, BankPayments = bank.TotalPayments,
                Expenses = pnl.TotalOperatingExpenses + pnl.TotalOtherExpenses, pnl.NetProfit };
        }
        var comparisons = new List<ComparisonMetricDto>();
        if (hasSales)
        {
            comparisons.Add(ReportPeriods.Compare("Sales", sales?.NetSales ?? 0, priorSales?.NetSales ?? 0));
            comparisons.Add(ReportPeriods.Compare("Returns", sales?.Returns ?? 0, priorSales?.Returns ?? 0));
            comparisons.Add(ReportPeriods.Compare("Transactions", sales?.InvoiceCount ?? 0, priorSales?.InvoiceCount ?? 0));
            comparisons.Add(ReportPeriods.Compare("Average basket", sales?.AverageInvoice ?? 0, priorSales?.AverageInvoice ?? 0));
        }
        if (hasProfit) comparisons.Add(ReportPeriods.Compare("Gross profit", sales?.GrossProfit ?? 0, priorSales?.GrossProfit ?? 0));
        if (hasPurchases) comparisons.Add(ReportPeriods.Compare("Purchases", purchases?.NetPurchaseValue ?? 0, priorPurchases?.NetPurchaseValue ?? 0));
        if (pnl is not null && priorPnl is not null)
        {
            comparisons.Add(ReportPeriods.Compare("Expenses", pnl.TotalOperatingExpenses + pnl.TotalOtherExpenses, priorPnl.TotalOperatingExpenses + priorPnl.TotalOtherExpenses));
            if (hasProfit) comparisons.Add(ReportPeriods.Compare("Net profit", pnl.NetProfit, priorPnl.NetProfit));
        }
        return ProtectCostFields(new ManagementSnapshotDto(q.FromUtc, q.ToUtc, q.ToUtc.AddTicks(-1), salesPayload,
            hasProfit ? new { GrossProfit = sales?.GrossProfit ?? 0, GrossMarginPercent = sales?.GrossMarginPercent ?? 0,
                CostOfGoodsSold = sales?.CostOfGoodsSold ?? 0, NetProfit = pnl?.NetProfit,
                GlGrossProfit = pnl?.GrossProfit, GrossProfitReconciliationDifference = pnl is null ? (decimal?)null : pnl.GrossProfit - (sales?.GrossProfit ?? 0),
                Scope = pnl is null ? "Operational allocation profit; financial profit unavailable for selected dimensions/permissions." : "Operational gross profit and GL net profit; adjustments may produce a reconciliation difference." } : null,
            purchases, hasInventory ? await scoped.InventoryAlertsAsync(q, ct) : null,
            finance, comparisons, trend, aging, expiry,
            hasSales ? (await scoped.SalesAnalyticsAsync(q with { Page = 1, PageSize = 10 }, "product", ct)).Items : null,
            hasSales ? (await scoped.SalesAnalyticsAsync(q with { Page = 1, PageSize = 10 }, "customer", ct)).Items : null,
            hasPurchases ? (await scoped.PurchaseAnalyticsAsync(q with { Page = 1, PageSize = 10 }, "supplier", ct)).Items : null,
            hasSales ? (await scoped.SalesAnalyticsAsync(q with { Page = 1, PageSize = 200 }, "branch", ct)).Items : null,
            hasFinance ? await scoped.CashPositionAsync(q.BranchId, q.FromUtc, q.ToUtc, ct) : null, calendarSales, calendarPurchases), actor);
    }
    private static void ValidateFinancialDimensions(ReportQuery q)
    {
        if (q.GodownId.HasValue || q.ProductId.HasValue || q.CategoryId.HasValue || q.ManufacturerId.HasValue ||
            q.CustomerId.HasValue || q.SupplierId.HasValue || q.UserId.HasValue || q.SaleType.HasValue || q.PriceLevelId.HasValue)
            throw new RequestValidationException("Financial statements support branch and date filters only.");
    }
}
