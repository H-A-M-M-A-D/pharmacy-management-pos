using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Reports;

public sealed class ReportingService(IReportingRepository repository, TimeProvider timeProvider) : IReportingService
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
            _ => throw new RequestValidationException("Unknown report.")
        };
        var actor = await Require(actorId, permission, ct);
        Validate(query);
        var branch = Scope(actor, query.BranchId);
        object result = report switch
        {
            "sales/summary" => await repository.SalesSummaryAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/daily" => await repository.DailySalesAsync(branch, query, ct),
            "sales/products" or "profitability/products" => await repository.ProductSalesAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/categories" => await repository.SalesByCategoryAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/cashiers" => await repository.SalesByCashierAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/payments" => await repository.SalesByPaymentAsync(branch, query.FromUtc, query.ToUtc, ct),
            "sales/discounts" => await repository.DiscountsAsync(branch, query, ct),
            "sales/credit" => await repository.CreditSalesAsync(branch, query, ct),
            "purchases/summary" => await repository.PurchaseSummaryAsync(branch, query.FromUtc, query.ToUtc, ct),
            "purchases/suppliers" => await repository.PurchasesBySupplierAsync(branch, query.FromUtc, query.ToUtc, ct),
            "purchases/products" => await repository.PurchasesByProductAsync(branch, query.FromUtc, query.ToUtc, ct),
            "purchases/returns" => await repository.PurchaseReturnsAsync(branch, query, ct),
            "inventory/current" => await repository.CurrentStockAsync(branch, option, ct),
            "inventory/batches" or "inventory/expiry" => await repository.BatchStockAsync(branch, query, ParseDays(option), option == "expired", ct),
            "inventory/movements" or "inventory/godown-movements" => await repository.StockMovementsAsync(branch, query, option, query.GodownId, ct),
            "inventory/valuation" => await repository.InventorySummaryAsync(branch, timeProvider.GetUtcNow().UtcDateTime, ct),
            "inventory/godown-stock" => await repository.GodownStockAsync(branch, Guid.TryParse(option, out var godownId) ? godownId : null, ct),
            "inventory/godown-valuation" => await repository.GodownStockAsync(branch, query.GodownId, ct),
            "inventory/in-transit" => await repository.InTransitStockAsync(branch, ct),
            "inventory/stock-count-variance" => await repository.StockCountVarianceAsync(branch, query, ct),
            "transfers/summary" => await repository.TransferSummaryAsync(branch, query, ct),
            "transfers/daily" => await repository.DailyTransfersAsync(branch, query, ct),
            "transfers/detail" => await repository.TransferDetailAsync(branch, query, ct),
            "transfers/discrepancy" => await repository.TransferDiscrepancyAsync(branch, query, option, ct),
            "financial/expenses" => await repository.ExpensesAsync(branch, query, ct),
            "financial/other-income" => await repository.OtherIncomeAsync(branch, query, ct),
            "financial/customer-outstanding" => await repository.CustomerOutstandingAsync(branch, ct),
            "financial/supplier-outstanding" => await repository.SupplierOutstandingAsync(branch, ct),
            "financial/account-ledger" => await repository.AccountLedgerAsync(branch, Guid.TryParse(option, out var id) ? id : null, query, ct),
            "financial/cash-position" => await repository.CashPositionAsync(branch, query.FromUtc, query.ToUtc, ct),
            "profitability/summary" => await ProfitabilitySummary(branch, query, ct),
            _ => throw new RequestValidationException("Unknown report.")
        };
        return ProtectCostFields(result, actor);
    }

    public async Task<DashboardDto> DashboardAsync(Guid actorId, ReportQuery query, CancellationToken ct)
    {
        var actor = await Require(actorId, PermissionCatalog.ReportsView, ct);
        Validate(query);
        var branch = Scope(actor, query.BranchId);
        var sales = await repository.SalesSummaryAsync(branch, query.FromUtc, query.ToUtc, ct);
        var products = await repository.ProductSalesAsync(branch, query.FromUtc, query.ToUtc, ct);
        var inventory = await repository.InventorySummaryAsync(branch, timeProvider.GetUtcNow().UtcDateTime, ct);
        var customers = await repository.CustomerOutstandingAsync(branch, ct);
        var suppliers = await repository.SupplierOutstandingAsync(branch, ct);
        var expenses = await repository.ExpensesAsync(branch, query with { PageSize = 100 }, ct);
        var cash = await repository.CashPositionAsync(branch, query.FromUtc, query.ToUtc, ct);
        var canViewProfit = Has(actor, PermissionCatalog.ReportsProfitability);
        var visibleProducts = canViewProfit ? products : products.Select(HideCost).ToList();
        return new(sales.NetSalesAfterReturns, canViewProfit ? products.Sum(x => x.GrossProfit) : 0, canViewProfit ? inventory.Value : 0, inventory.LowStockCount,
            inventory.NearExpiryCount, customers.Sum(x => x.Balance), suppliers.Sum(x => x.Balance),
            expenses.Items.Sum(x => x.Amount), cash.Closing, await repository.TrendAsync(branch, query.FromUtc, query.ToUtc, ct),
            visibleProducts.OrderByDescending(x => x.NetQuantity).Take(5).ToList(),
            await repository.SalesByPaymentAsync(branch, query.FromUtc, query.ToUtc, ct));
    }

    private async Task<object> ProfitabilitySummary(Guid? branch, ReportQuery query, CancellationToken ct)
    {
        var rows = await repository.ProductSalesAsync(branch, query.FromUtc, query.ToUtc, ct);
        var sales = rows.Sum(x => x.NetSales); var cost = rows.Sum(x => x.Cost); var profit = ReportCalculations.GrossProfit(sales, cost);
        return new { NetSales = sales, CostOfGoodsSold = cost, GrossProfit = profit, GrossMarginPercent = ReportCalculations.MarginPercent(sales, profit) };
    }
    private async Task<User> Require(Guid id, string permission, CancellationToken ct)
    {
        var actor = await repository.GetActorAsync(id, ct);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to view this report.");
        return actor;
    }
    private static object ProtectCostFields(object result, User actor)
    {
        if (Has(actor, PermissionCatalog.ReportsProfitability)) return result;
        return result switch
        {
            IReadOnlyList<ProductSalesDto> rows => rows.Select(HideCost).ToList(),
            IReadOnlyList<StockRowDto> rows => rows.Select(x => x with { InventoryValue = 0 }).ToList(),
            IReadOnlyList<GodownStockRowDto> rows => rows.Select(x => x with { StockValue = 0 }).ToList(),
            InventorySummaryDto summary => summary with { Value = 0 },
            StockTransferSummaryDto transferSummary => transferSummary with { DispatchedValue = 0, ReceivedValue = 0 },
            _ => result
        };
    }
    private static ProductSalesDto HideCost(ProductSalesDto x) => x with { Cost = 0, GrossProfit = 0, GrossMarginPercent = 0 };
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
        if (q.Page < 1 || q.PageSize is < 1 or > 200) throw new RequestValidationException("Invalid pagination.");
        if (q.ToUtc - q.FromUtc > TimeSpan.FromDays(366)) throw new RequestValidationException("Interactive report ranges cannot exceed 366 days.");
    }
    private static int? ParseDays(string? value) => int.TryParse(value, out var days) && days is > 0 and <= 365 ? days : null;
}
