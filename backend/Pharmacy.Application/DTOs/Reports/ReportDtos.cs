namespace Pharmacy.Application.DTOs.Reports;

public sealed record ReportQuery(Guid? BranchId, DateTime FromUtc, DateTime ToUtc, int Page = 1, int PageSize = 50, string? Search = null);
public sealed record PagedReport<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record SalesSummaryDto(decimal GrossSales, decimal Discounts, decimal NetSales, decimal PaymentsAtSale,
    decimal CreditCreated, decimal SalesReturns, decimal NetSalesAfterReturns, int InvoiceCount, int ItemsSold);
public sealed record DailySaleDto(Guid Id, string InvoiceNumber, DateTime PostedAtUtc, string Customer, string Cashier,
    decimal Gross, decimal Discount, decimal Net, decimal Paid, decimal Credit, decimal ReturnAmount,
    decimal NetAfterReturn, string PaymentSummary);
public sealed record ProductSalesDto(Guid ProductId, string Product, string Sku, string Category, int QuantitySold,
    int QuantityReturned, int NetQuantity, decimal GrossSales, decimal Discount, decimal NetSales, decimal Cost,
    decimal GrossProfit, decimal GrossMarginPercent);
public sealed record NamedSalesDto(string Name, int InvoiceCount, decimal GrossSales, decimal Discount,
    decimal NetSales, decimal Returns, decimal NetAfterReturns);
public sealed record PaymentMethodSalesDto(string Method, decimal Amount);
public sealed record DiscountRowDto(string InvoiceNumber, string Product, string Cashier, decimal Gross,
    decimal DiscountPercent, decimal DiscountAmount, decimal Net);
public sealed record CreditSaleDto(string InvoiceNumber, string Customer, DateTime PostedAtUtc, decimal NetSale,
    decimal PaidAtSale, decimal CreditCreated, decimal? CustomerCurrentBalance);

public sealed record PurchaseSummaryDto(decimal GrossPurchases, decimal Discounts, decimal Tax, decimal NetPurchases,
    decimal PurchaseReturnCredit, decimal NetAfterReturns, int PurchaseCount, int PaidQuantity, int BonusQuantity);
public sealed record SupplierPurchaseDto(Guid SupplierId, string Supplier, int PurchaseCount, int PaidQuantity,
    int BonusQuantity, decimal NetPurchases, decimal ReturnCredit, decimal NetPurchaseValue, decimal CurrentOutstanding);
public sealed record ProductPurchaseDto(Guid ProductId, string Product, string Sku, int PaidReceived, int BonusReceived,
    int PaidReturned, int BonusReturned, int NetPhysicalQuantity, decimal PurchaseCost);
public sealed record PurchaseReturnRowDto(string ReturnNumber, string Supplier, DateTime PostedAtUtc,
    int PaidQuantity, int BonusQuantity, decimal SupplierCredit);

public sealed record StockRowDto(Guid ProductId, string Product, string Sku, string Category, string Manufacturer,
    string Branch, int CurrentQuantity, int ReorderLevel, string Status, int ActiveBatches, DateOnly? NearestExpiry,
    decimal InventoryValue);
public sealed record BatchStockRowDto(string Product, string Sku, string Batch, DateOnly Expiry, int Quantity,
    decimal PurchaseCost, decimal StockValue, string Supplier, string Branch);
public sealed record StockMovementRowDto(DateTime OccurredAtUtc, string Branch, string Product, string Batch,
    string MovementType, int Quantity, string User, string? ReferenceType, string? Notes);
public sealed record InventorySummaryDto(decimal Value, int LowStockCount, int OutOfStockCount, int NearExpiryCount);

public sealed record ExpenseReportDto(string Number, DateTime OccurredAtUtc, string Category, string Account,
    string Branch, string User, decimal Amount, string Description);
public sealed record OtherIncomeReportDto(string Number, DateTime OccurredAtUtc, string Account, string Branch,
    string User, decimal Amount, string Description);
public sealed record OutstandingDto(Guid PartyId, string Code, string Name, string? Phone, decimal? CreditLimit,
    decimal Balance, decimal? AvailableCredit, DateOnly? LastPaymentDate, DateOnly? LastTransactionDate, string Status);
public sealed record LedgerReportDto(DateTime OccurredAtUtc, string EntryType, string Reference, string Description,
    decimal Amount, string Account);
public sealed record CashPositionReportDto(decimal Opening, decimal ExternalInflows, decimal ExternalOutflows,
    decimal Closing, decimal SalesPayments, decimal CustomerPayments, decimal SupplierPayments, decimal Expenses,
    decimal Refunds, decimal OtherIncome, decimal TransferIn, decimal TransferOut);
public sealed record DashboardDto(decimal NetSales, decimal GrossProfit, decimal InventoryValue, int LowStockCount,
    int NearExpiryCount, decimal CustomerOutstanding, decimal SupplierOutstanding, decimal Expenses, decimal CashPosition,
    IReadOnlyList<TrendPointDto> SalesTrend, IReadOnlyList<ProductSalesDto> TopProducts,
    IReadOnlyList<PaymentMethodSalesDto> PaymentMethods);
public sealed record TrendPointDto(DateOnly Date, decimal NetSales, decimal GrossProfit);
