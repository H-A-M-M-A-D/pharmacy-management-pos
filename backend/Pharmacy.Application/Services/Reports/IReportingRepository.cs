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
    Task<PagedReport<StockMovementRowDto>> StockMovementsAsync(Guid? branchId, ReportQuery query, string? movementType, Guid? godownId, CancellationToken ct);
    Task<InventorySummaryDto> InventorySummaryAsync(Guid? branchId, DateTime nowUtc, CancellationToken ct);
    Task<PagedReport<ExpenseReportDto>> ExpensesAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<PagedReport<OtherIncomeReportDto>> OtherIncomeAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<IReadOnlyList<OutstandingDto>> CustomerOutstandingAsync(Guid? branchId, CancellationToken ct);
    Task<IReadOnlyList<OutstandingDto>> SupplierOutstandingAsync(Guid? branchId, CancellationToken ct);
    Task<PagedReport<LedgerReportDto>> AccountLedgerAsync(Guid? branchId, Guid? accountId, ReportQuery query, CancellationToken ct);
    Task<CashPositionReportDto> CashPositionAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<IReadOnlyList<TrendPointDto>> TrendAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<IReadOnlyList<GodownStockRowDto>> GodownStockAsync(Guid? branchId, Guid? godownId, CancellationToken ct);
    Task<IReadOnlyList<InTransitStockRowDto>> InTransitStockAsync(Guid? branchId, CancellationToken ct);
    Task<StockTransferSummaryDto> TransferSummaryAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<PagedReport<DailyTransferDto>> DailyTransfersAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<PagedReport<TransferDetailRowDto>> TransferDetailAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<PagedReport<TransferDiscrepancyRowDto>> TransferDiscrepancyAsync(Guid? branchId, ReportQuery query, string? resolutionFilter, CancellationToken ct);
    Task<PagedReport<StockCountVarianceRowDto>> StockCountVarianceAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<IReadOnlyList<NamedSalesDto>> SalesByCustomerAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<IReadOnlyList<NamedSalesDto>> SalesByTypeAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<IReadOnlyList<NamedSalesDto>> SalesByPriceLevelAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<PagedReport<PriceOverrideRowDto>> PriceOverridesAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<PagedReport<BelowCostSaleRowDto>> BelowCostSalesAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
    Task<IReadOnlyList<CustomerProfitDto>> GrossProfitByCustomerAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<IReadOnlyList<CreditUtilizationRowDto>> CreditLimitUtilizationAsync(Guid? branchId, CancellationToken ct);
    Task<QuotationSummaryDto> QuotationSummaryAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<SalesOrderSummaryDto> SalesOrderSummaryAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct);
    Task<PagedReport<OpenSalesOrderRowDto>> OpenSalesOrdersAsync(Guid? branchId, ReportQuery query, CancellationToken ct);
}
