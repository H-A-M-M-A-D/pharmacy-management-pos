using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Accounting.Budgets;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pharmacy.Application.Services.Reports;

public sealed partial class ReportingService(IReportingRepository repository, TimeProvider timeProvider,
    IAccountingService? accounting = null, IBudgetService? budgets = null) : IReportingService
{
    public async Task<object> ExecuteAsync(Guid actorId, string report, ReportQuery query, string? option, CancellationToken ct)
    {
        var permission = report.Split('/')[0] switch
        {
            "sales" => PermissionCatalog.ReportsSales,
            "purchases" => PermissionCatalog.ReportsPurchases,
            "inventory" => PermissionCatalog.ReportsInventory,
            "transfers" => PermissionCatalog.ReportsInventory,
            "financial" => PermissionCatalog.ReportsFinancial,
            "profitability" => PermissionCatalog.ReportsProfitability,
            "quotations" => PermissionCatalog.QuotationsView,
            "sales_orders" => PermissionCatalog.SalesOrdersView,
            "management" => PermissionCatalog.ReportsView,
            _ => throw new RequestValidationException("Unknown report.")
        };
        var actor = await Require(actorId, permission, ct);
        if (report.StartsWith("purchases/", StringComparison.Ordinal) && !CanViewCost(actor))
            throw new ForbiddenOperationException("Purchase reports require cost permission.");
        if (report == "sales/below-cost" && !Has(actor, PermissionCatalog.ReportsProfitability))
            throw new ForbiddenOperationException("Below-cost reporting requires profitability permission.");
        if (report == "financial/customer-performance" && !Has(actor, PermissionCatalog.ReportsSales))
            throw new ForbiddenOperationException("Customer performance requires sales report permission.");
        Validate(query);
        if (report == "inventory/godown-stock" && Guid.TryParse(option, out var selectedGodown)) query = query with { GodownId = selectedGodown };
        var branch = Scope(actor, query.BranchId);
        query = query with { BranchId = branch, GodownUserId = actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager ? null : actor.Id };
        await EnsureGodownScope(actor, query, ct);
        var scopedRepository = repository.WithFilters(query);
        if (report is "purchases/suppliers" or "purchases/products")
            return ProtectCostFields(await scopedRepository.PurchaseAnalyticsAsync(query, report == "purchases/suppliers" ? "supplier" : "product", ct), actor);
        if (report is "financial/customer-outstanding" or "financial/supplier-outstanding" or "financial/credit-limit-utilization")
            return ProtectCostFields(await scopedRepository.PartyDetailAsync(query, report == "financial/supplier-outstanding", report.Split('/')[1], ct), actor);
        if (report is "inventory/current" or "inventory/godown-stock" or "inventory/godown-valuation")
            return ProtectCostFields(await scopedRepository.StockPositionAsync(query with { Status = query.Status ?? (report == "inventory/current" ? option : null) }, ct), actor);
        if (report == "sales/summary")
        {
            var row = (await scopedRepository.SalesAnalyticsAsync(query with { Page = 1, PageSize = 1 }, "total", ct)).Items.SingleOrDefault();
            if (query.ProductId.HasValue || query.CategoryId.HasValue || query.ManufacturerId.HasValue)
                return new { GrossSales = row?.GrossSales ?? 0, Discounts = row?.Discounts ?? 0, NetSales = (row?.NetSales ?? 0) + (row?.Returns ?? 0),
                    SalesReturns = row?.Returns ?? 0, NetSalesAfterReturns = row?.NetSales ?? 0, InvoiceCount = row?.InvoiceCount ?? 0, ItemsSold = row?.QuantitySold ?? 0,
                    Method = "Selected allocation lines. Invoice payments/credit cannot be attributed to individual products and are omitted." };
            var header = await scopedRepository.SalesSummaryAsync(branch, query.FromUtc, query.ToUtc, ct);
            return header with { GrossSales = row?.GrossSales ?? 0, Discounts = row?.Discounts ?? 0,
                NetSales = (row?.NetSales ?? 0) + (row?.Returns ?? 0), SalesReturns = row?.Returns ?? 0,
                NetSalesAfterReturns = row?.NetSales ?? 0, InvoiceCount = row?.InvoiceCount ?? 0, ItemsSold = row?.QuantitySold ?? 0 };
        }
        if (report.StartsWith("management/", StringComparison.Ordinal))
            return await ManagementReportAsync(actorId, actor, report, query, option, ct);
        if (report is "purchases/price-history" or "purchases/price-comparison" or "purchases/last-rate" or "purchases/price-variance" or
            "purchases/pending-po" or "purchases/po-vs-grn" or "purchases/supplier-performance")
            return ProtectCostFields(await scopedRepository.PurchasingDetailAsync(query, report.Split('/')[1], ct), actor);
        if (report == "sales/staff-performance") return await scopedRepository.StaffPerformanceAsync(query, ct);
        if (report is "inventory/batch-position" or "inventory/stock-adjustments" or "inventory/movement-summary" or "inventory/turnover" or "inventory/in-transit")
        {
            if (report == "inventory/turnover" && !CanViewCost(actor)) throw new ForbiddenOperationException("Inventory turnover requires cost permission.");
            return ProtectCostFields(await scopedRepository.InventoryDetailAsync(query, report.Split('/')[1], ct), actor);
        }
        if (report is "financial/customer-aging" or "financial/supplier-aging")
        {
            if (accounting is null) throw new InvalidOperationException("Accounting service is unavailable.");
            return report == "financial/customer-aging"
                ? await accounting.GetArAgingSummaryAsync(actorId, query.ToUtc.AddTicks(-1), branch, query.CustomerId, ct)
                : await accounting.GetApAgingSummaryAsync(actorId, query.ToUtc.AddTicks(-1), branch, query.SupplierId, ct);
        }
        if (report is "financial/customer-payments" or "financial/supplier-payments" or "financial/customer-statement" or "financial/supplier-statement" or
            "financial/customer-performance" or "financial/supplier-performance")
            return ProtectCostFields(await scopedRepository.PartyDetailAsync(query, report.Contains("supplier", StringComparison.Ordinal), report.Split('/')[1], ct), actor);
        if (NewAnalyticsDimension(report) is { } dimension)
        {
            var rows = await scopedRepository.SalesAnalyticsAsync(query, dimension, ct);
            return ProtectCostFields(rows, actor);
        }
        if (report is "inventory/performance" or "inventory/fast-moving" or "inventory/slow-moving" or "inventory/dead-stock")
            return ProtectCostFields(await scopedRepository.ProductPerformanceAsync(query with { Status = report switch
                { "inventory/fast-moving" => "fast", "inventory/slow-moving" => "slow", "inventory/dead-stock" => "dead", _ => query.Status } }, ct), actor);
        if (report is "inventory/aging" or "inventory/expiry-risk")
            return ProtectCostFields(await scopedRepository.InventoryExposureAsync(query, report == "inventory/expiry-risk", ct), actor);
        if (report == "inventory/position")
            return ProtectCostFields(await scopedRepository.StockPositionAsync(query, ct), actor);
        if (report == "inventory/abc")
        {
            if (option is "profit" && !Has(actor, PermissionCatalog.ReportsProfitability) || option is "inventory" && !CanViewCost(actor))
                throw new ForbiddenOperationException("Cost/profit permission is required for this ABC basis.");
            return await scopedRepository.AbcAsync(query, option ?? "sales", ct);
        }
        if (report.StartsWith("purchases/by-", StringComparison.Ordinal) || report == "purchases/trend")
            return ProtectCostFields(await scopedRepository.PurchaseAnalyticsAsync(query, report[10..], ct), actor);
        object result = report switch
        {
            "sales/summary" => await scopedRepository.SalesSummaryAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/daily" => await scopedRepository.DailySalesAsync(branch, query, ct),
            "sales/products" or "profitability/products" => await scopedRepository.ProductSalesAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/categories" => await scopedRepository.SalesByCategoryAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/cashiers" => await scopedRepository.SalesByCashierAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/payments" => await scopedRepository.SalesByPaymentAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/discounts" => await scopedRepository.DiscountsAsync(branch, query, ct),
            "sales/credit" => await scopedRepository.CreditSalesAsync(branch, query, ct),
            "purchases/summary" => await scopedRepository.PurchaseSummaryAsync(branch, query.FromUtc, query.ToUtc, ct),
            "purchases/suppliers" => await scopedRepository.PurchasesBySupplierAsync(branch, query.FromUtc, query.ToUtc, ct),
            "purchases/products" => await scopedRepository.PurchasesByProductAsync(branch, query.FromUtc, query.ToUtc, ct),
            "purchases/returns" => await scopedRepository.PurchaseReturnsAsync(branch, query, ct),
            "inventory/current" => await scopedRepository.CurrentStockAsync(branch, option, ct),
            "inventory/batches" or "inventory/expiry" => await scopedRepository.BatchStockAsync(branch, query, ParseDays(option), option == "expired", ct),
            "inventory/movements" or "inventory/godown-movements" => await scopedRepository.StockMovementsAsync(branch, query, option, query.GodownId, ct),
            "inventory/valuation" => await scopedRepository.InventorySummaryAsync(branch, query.ToUtc.AddTicks(-1), ct),
            "inventory/godown-stock" => await scopedRepository.GodownStockAsync(branch, Guid.TryParse(option, out var godownId) ? godownId : null, ct),
            "inventory/godown-valuation" => await scopedRepository.GodownStockAsync(branch, query.GodownId, ct),
            "inventory/in-transit" => await scopedRepository.InTransitStockAsync(branch, ct),
            "inventory/stock-count-variance" => await scopedRepository.StockCountVarianceAsync(branch, query, ct),
            "transfers/summary" => await scopedRepository.TransferSummaryAsync(branch, query, ct),
            "transfers/daily" => await scopedRepository.DailyTransfersAsync(branch, query, ct),
            "transfers/detail" => await scopedRepository.TransferDetailAsync(branch, query, ct),
            "transfers/discrepancy" => await scopedRepository.TransferDiscrepancyAsync(branch, query, option, ct),
            "financial/expenses" => await scopedRepository.ExpensesAsync(branch, query, ct),
            "financial/other-income" => await scopedRepository.OtherIncomeAsync(branch, query, ct),
            "financial/customer-outstanding" => await scopedRepository.CustomerOutstandingAsync(branch, ct),
            "financial/supplier-outstanding" => await scopedRepository.SupplierOutstandingAsync(branch, ct),
            "financial/account-ledger" => await scopedRepository.AccountLedgerAsync(branch, Guid.TryParse(option, out var id) ? id : null, query, ct),
            "financial/cash-position" => await scopedRepository.CashPositionAsync(branch, query.FromUtc, query.ToUtc, ct),
            "financial/credit-limit-utilization" => await scopedRepository.CreditLimitUtilizationAsync(branch, ct),
            "profitability/summary" => await ProfitabilitySummary(branch, query, ct),
            "profitability/by-customer" => await scopedRepository.GrossProfitByCustomerAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/by-customer" => await scopedRepository.SalesByCustomerAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/retail-vs-wholesale" => await scopedRepository.SalesByTypeAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/by-price-level" => await scopedRepository.SalesByPriceLevelAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/price-overrides" => await scopedRepository.PriceOverridesAsync(branch, query, ct),
            "sales/below-cost" => await scopedRepository.BelowCostSalesAsync(branch, query, ct),
            "sales/daily-wholesale" => await scopedRepository.DailySalesAsync(branch, query with { SaleType = SaleType.Wholesale }, ct),
            "quotations/summary" => await scopedRepository.QuotationSummaryAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales_orders/summary" => await scopedRepository.SalesOrderSummaryAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales_orders/open" => await scopedRepository.OpenSalesOrdersAsync(branch, query, ct),
            _ => throw new RequestValidationException("Unknown report.")
        };
        return ProtectCostFields(result, actor);
    }

    public async Task<object> DashboardAsync(Guid actorId, ReportQuery query, CancellationToken ct)
    {
        var actor = await Require(actorId, PermissionCatalog.ReportsView, ct);
        Validate(query);
        var branch = Scope(actor, query.BranchId);
        query = query with { BranchId = branch, GodownUserId = actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager ? null : actor.Id };
        await EnsureGodownScope(actor, query, ct);
        var snapshot = await ManagementReportAsync(actorId, actor, "management/overview", query, null, ct);
        var element = JsonSerializer.SerializeToNode(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var inventory = element["inventory"];
        var nearExpiry = element["expiryExposure"]?.AsArray().FirstOrDefault(x => x?["bucket"]?.GetValue<string>() == "0–30");
        return new JsonObject
        {
            ["netSales"] = element["sales"]?["netSales"]?.DeepClone(),
            ["grossProfit"] = element["profitability"]?["grossProfit"]?.DeepClone(),
            ["inventoryValue"] = inventory?["inventoryValue"]?.DeepClone(),
            ["lowStockCount"] = inventory?["lowStockItems"]?.DeepClone(),
            ["outOfStockCount"] = inventory?["outOfStockItems"]?.DeepClone(),
            ["nearExpiryCount"] = nearExpiry?["batches"]?.DeepClone(),
            ["salesTrend"] = element["trend"]?.DeepClone(),
            ["expiryExposure"] = element["expiryExposure"]?.DeepClone(),
            ["topProducts"] = Has(actor, PermissionCatalog.ReportsSales) ? JsonSerializer.SerializeToNode(
                ProtectCostFields((await repository.SalesAnalyticsAsync(query with { Page = 1, PageSize = 5 }, "product", ct)).Items, actor)) : null,
            ["expenses"] = element["finance"]?["expenses"]?.DeepClone(),
            ["cashPosition"] = element["finance"]?["position"]?["cash"]?.DeepClone(),
            ["customerOutstanding"] = element["finance"]?["position"]?["receivables"]?.DeepClone(),
            ["supplierOutstanding"] = element["finance"]?["position"]?["payables"]?.DeepClone()
        };
    }

    private async Task<object> ProfitabilitySummary(Guid? branch, ReportQuery query, CancellationToken ct)
    {
        var row = (await repository.SalesAnalyticsAsync(query with { BranchId = branch, Page = 1, PageSize = 1 }, "total", ct)).Items.SingleOrDefault();
        var sales = row?.NetSales ?? 0; var cost = row?.CostOfGoodsSold ?? 0; var profit = ReportCalculations.GrossProfit(sales, cost);
        return new { NetSales = sales, CostOfGoodsSold = cost, GrossProfit = profit, GrossMarginPercent = ReportCalculations.MarginPercent(sales, profit) };
    }
    private async Task<User> Require(Guid id, string permission, CancellationToken ct)
    {
        var actor = await repository.GetActorAsync(id, ct);
        if (actor is null || !actor.IsActive || actor.Role is not { IsActive: true } || actor.Role.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to view this report.");
        return actor;
    }
    private static object ProtectCostFields(object result, User actor)
    {
        var node = JsonSerializer.SerializeToNode(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        RemoveRestrictedFields(node, CanViewCost(actor), Has(actor, PermissionCatalog.ReportsProfitability));
        if (!Has(actor, PermissionCatalog.ReportsFinancial)) RemoveFinancialFields(node);
        return node!;
    }
    private static bool CanViewCost(User actor) => Has(actor, PermissionCatalog.ReportsProfitability) || Has(actor, PermissionCatalog.SalesCostView);
    private static readonly HashSet<string> CostFields = new(StringComparer.OrdinalIgnoreCase)
    { "cost", "costOfGoodsSold", "unitCost", "unitCostSnapshot", "unitCostPriceSnapshot", "purchaseCost", "purchasePrice", "inventoryValue", "stockValue", "value",
      "dispatchedValue", "receivedValue", "unresolvedValue", "varianceValue", "costValue", "potentialLoss", "purchaseValue", "inventoryValuePercent", "slowMovingInventoryValue", "deadStockValue",
      "inTransitValue", "adjustmentValue", "openingInventoryValue", "closingInventoryValue", "averageInventoryValue", "inventoryTurnover", "effectiveUnitCost", "previousPrice", "priceVariance", "priceVariancePercent", "nearExpiryValue", "expiredValue", "expiredStockValue" };
    private static readonly HashSet<string> ProfitFields = new(StringComparer.OrdinalIgnoreCase)
    { "grossProfit", "grossMarginPercent", "marginPercent", "netProfit", "netMarginPercent", "lossPerUnit", "totalLoss", "grossProfitBeforeDiscount", "discountImpactOnProfit" };
    private static readonly HashSet<string> FinancialFields = new(StringComparer.OrdinalIgnoreCase)
    { "outstanding", "currentOutstanding", "customerCurrentBalance", "cashVariance", "receivables", "payables", "cashBalance", "bankBalance" };
    private static void RemoveFinancialFields(JsonNode? node)
    {
        if (node is JsonObject obj)
            foreach (var key in obj.Select(x => x.Key).ToList())
            {
                if (FinancialFields.Contains(key)) obj.Remove(key);
                else RemoveFinancialFields(obj[key]);
            }
        else if (node is JsonArray array) foreach (var item in array) RemoveFinancialFields(item);
    }
    private static void RemoveRestrictedFields(JsonNode? node, bool cost, bool profit)
    {
        if (node is JsonObject obj)
            foreach (var key in obj.Select(x => x.Key).ToList())
            {
                if (!cost && CostFields.Contains(key) || !profit && ProfitFields.Contains(key)) obj.Remove(key);
                else RemoveRestrictedFields(obj[key], cost, profit);
            }
        else if (node is JsonArray array) foreach (var item in array) RemoveRestrictedFields(item, cost, profit);
    }
    private static bool Has(User actor, string permission) => actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) == true;
    private static Guid? Scope(User actor, Guid? requested)
    {
        var canSelect = actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager;
        if (!canSelect && requested.HasValue && requested != actor.BranchId) throw new ForbiddenOperationException("The current user cannot access this branch.");
        return canSelect ? requested : actor.BranchId;
    }
    private static void Validate(ReportQuery q)
    {
        if (q.FromUtc.Kind != DateTimeKind.Utc || q.ToUtc.Kind != DateTimeKind.Utc || q.ToUtc <= q.FromUtc)
            throw new RequestValidationException("A valid explicit UTC date range is required.");
        if (q.Page < 1 || q.PageSize is < 1 or > 200 || (long)(q.Page - 1) * q.PageSize > int.MaxValue) throw new RequestValidationException("Invalid pagination.");
        if (q.ToUtc - q.FromUtc > TimeSpan.FromDays(366)) throw new RequestValidationException("Interactive report ranges cannot exceed 366 days.");
        if (q.FastMovingQuantity < 1 || q.SlowMovingDays < 1 || q.DeadStockDays <= q.SlowMovingDays ||
            q.AbcA <= 0 || q.AbcB <= q.AbcA || q.AbcB >= 100)
            throw new RequestValidationException("Invalid movement or ABC thresholds.");
    }
    private static int? ParseDays(string? value) => int.TryParse(value, out var days) && days is > 0 and <= 365 ? days : null;
}
