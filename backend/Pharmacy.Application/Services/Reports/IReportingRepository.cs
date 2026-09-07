using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Reports;

public interface IReportingRepository
{
    Task<User?> GetActorAsync(Guid id, CancellationToken ct);
    Task<SalesSummaryDto> SalesSummaryAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<PagedReport<DailySaleDto>> DailySalesAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<IReadOnlyList<ProductSalesDto>> ProductSalesAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<IReadOnlyList<NamedSalesDto>> SalesByCategoryAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<IReadOnlyList<NamedSalesDto>> SalesByCashierAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<IReadOnlyList<PaymentMethodSalesDto>> SalesByPaymentAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<PagedReport<DiscountRowDto>> DiscountsAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<PagedReport<CreditSaleDto>> CreditSalesAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<PurchaseSummaryDto> PurchaseSummaryAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<IReadOnlyList<SupplierPurchaseDto>> PurchasesBySupplierAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<IReadOnlyList<ProductPurchaseDto>> PurchasesByProductAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<PagedReport<PurchaseReturnRowDto>> PurchaseReturnsAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<IReadOnlyList<StockRowDto>> CurrentStockAsync(Guid? branchId, string? status, CancellationToken ct);
    Task<PagedReport<BatchStockRowDto>> BatchStockAsync(Guid? branchId, ReportQuery query, int? expiryDays, bool expired, CancellationToken ct);
    Task<PagedReport<StockMovementRowDto>> StockMovementsAsync(Guid? branchId, ReportQuery query, string? movementType, CancellationToken ct);
    Task<InventorySummaryDto> InventorySummaryAsync(Guid? branchId, DateTime nowUtc, CancellationToken ct);
    Task<PagedReport<ExpenseReportDto>> ExpensesAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<PagedReport<OtherIncomeReportDto>> OtherIncomeAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<IReadOnlyList<OutstandingDto>> CustomerOutstandingAsync(Guid? branchId, CancellationToken ct);
    Task<IReadOnlyList<OutstandingDto>> SupplierOutstandingAsync(Guid? branchId, CancellationToken ct);
    Task<PagedReport<LedgerReportDto>> AccountLedgerAsync(Guid? branchId, Guid? accountId, ReportQuery query, CancellationToken ct);
    Task<CashPositionReportDto> CashPositionAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<IReadOnlyList<TrendPointDto>> TrendAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
}
