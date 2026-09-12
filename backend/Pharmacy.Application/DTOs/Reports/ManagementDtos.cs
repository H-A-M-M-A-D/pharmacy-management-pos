namespace Pharmacy.Application.DTOs.Reports;

public sealed record ComparisonMetricDto(string Metric, decimal Current, decimal Previous, decimal AbsoluteChange, decimal? PercentageChange);
public sealed record AnalyticsRowDto(string Key, string Name, int InvoiceCount, int Quantity, decimal GrossSales,
    decimal Discounts, decimal NetSales, decimal Returns, decimal CostOfGoodsSold, decimal GrossProfit,
    decimal GrossMarginPercent, decimal AverageInvoice, decimal BasketSize, int QuantitySold = 0, int QuantityReturned = 0)
{
    public decimal GrossProfitBeforeDiscount => GrossProfit + Discounts;
    public decimal DiscountImpactOnProfit => Discounts;
}
public sealed record ProductPerformanceDto(Guid ProductId, string Product, string Sku, int QuantitySold, decimal SalesAmount,
    decimal GrossProfit, decimal MarginPercent, int PurchaseQuantity, decimal PurchaseValue, int CurrentStock,
    decimal StockValue, int? DaysSinceLastSale, int? DaysSinceLastPurchase, int ReturnQuantity, decimal AverageSellingRate, string MovementClass);
public sealed record ContributionDto(Guid ProductId, string Product, decimal Contribution, decimal ContributionPercent,
    decimal CumulativePercent, string Classification);
public sealed record ExposureDto(string Bucket, int Products, int Batches, int Quantity, decimal CostValue,
    decimal RetailValue, decimal PotentialLoss, decimal InventoryValuePercent);
public sealed record PurchaseAnalyticsDto(string Key, string Name, int ReceiptCount, int Quantity, decimal PurchaseValue,
    decimal ReturnValue, decimal NetPurchaseValue);
public sealed record FinancialPositionDto(decimal Cash, decimal Bank, decimal Receivables, decimal Payables,
    decimal CustomerAdvances, decimal SupplierAdvances);
public sealed record StockPositionDto(Guid ProductId, string Product, string Sku, Guid BranchId, string Branch,
    Guid? GodownId, string Godown, int Quantity, decimal StockValue, int ReorderLevel, string Status)
{
    public int ReorderQuantity => Math.Max(0, ReorderLevel - Quantity);
}
public sealed record ManagementSnapshotDto(DateTime FromUtc, DateTime ToUtc, DateTime StockAsOfUtc,
    object? Sales, object? Profitability, object? Purchases, object? Inventory, object? Finance,
    IReadOnlyList<ComparisonMetricDto> Comparison, IReadOnlyList<AnalyticsRowDto> Trend,
    IReadOnlyList<ExposureDto> InventoryAging, IReadOnlyList<ExposureDto> ExpiryExposure,
    IReadOnlyList<AnalyticsRowDto>? TopProducts = null, IReadOnlyList<AnalyticsRowDto>? TopCustomers = null,
    IReadOnlyList<PurchaseAnalyticsDto>? TopSuppliers = null, IReadOnlyList<AnalyticsRowDto>? BranchPerformance = null,
    object? CashActivity = null, object? CalendarSales = null, object? CalendarPurchases = null);
