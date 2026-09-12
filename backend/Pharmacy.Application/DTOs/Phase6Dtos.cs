using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Phase6;

public enum BulkPricingAction { IncreasePercent = 1, DecreasePercent = 2, SetMargin = 3, SetMarkup = 4, Round = 5 }
public sealed record BulkPricingRequest(Guid? CategoryId, Guid? ManufacturerId, Guid? PriceLevelId,
    IReadOnlyList<Guid>? ProductIds, BulkPricingAction Action, decimal Value, decimal RoundingIncrement, string Reason);
public sealed record BulkPriceRow(Guid ProductId, string Product, Guid? PriceLevelId, decimal Cost, decimal OldPrice, decimal NewPrice, DateTime ProductVersion, DateTime? PriceVersion);
public sealed record BulkPricingPreview(Guid PreviewId, DateTime ExpiresAtUtc, string Reason, IReadOnlyList<BulkPriceRow> Rows);
public sealed record BulkPricingApplyRequest(Guid PreviewId, bool Confirmed);
public sealed record Phase6OptionDto(Guid Id, string Name);
public sealed record Phase6PricingOptions(IReadOnlyList<Phase6OptionDto> Categories, IReadOnlyList<Phase6OptionDto> Manufacturers,
    IReadOnlyList<Phase6OptionDto> Products, IReadOnlyList<Phase6OptionDto> PriceLevels, IReadOnlyList<Phase6OptionDto> Customers,
    IReadOnlyList<Phase6OptionDto> Branches, Phase6Thresholds Thresholds);
public sealed record ReorderDraftLine(Guid ProductId, Guid BranchId, Guid? GodownId, Guid SupplierId, int SuggestedQuantity, int FinalQuantity);
public sealed record ReorderDraftRequest(IReadOnlyList<ReorderDraftLine> Lines, bool Confirmed);
public sealed record ReorderDraftResult(IReadOnlyList<Guid> PurchaseOrderIds, bool DuplicateSuppressed);
public sealed record Phase6Thresholds(int ExpiryWarningDays = 90, int SlowMovingDays = 90, int DeadStockDays = 180,
    decimal MinimumMarginPercent = 10, int DefaultReorderCoverDays = 30, decimal RoundingIncrement = 1);
public sealed record SlowStockDto(Guid ProductId, string Product, Guid BranchId, string Branch, Guid? GodownId, string Godown,
    int CurrentQuantity, decimal StockValue, DateTime? LastSaleDate, int DaysSinceLastSale, string Status);
public sealed record AutomationRunResult(int AlertsCreated, int ReviewFlagsCreated, IReadOnlyList<Guid> DraftPurchaseOrderIds, int ExecutedRules, int DuplicateRulesSuppressed);
public sealed record PurchaseCostSuggestionDto(Guid Id, Guid ProductId, string Product, Guid BranchId, Guid GoodsReceiptId,
    decimal OldCost, decimal NewCost, decimal CurrentSellingPrice, decimal CurrentMarginPercent, decimal SuggestedPrice, DateTime ProductVersion);
public sealed record AcceptSuggestionRequest(bool Confirmed);
public sealed record ExpiryDiscountSuggestionDto(Guid BatchId, Guid ProductId, string Product, string Batch, Guid BranchId, Guid? GodownId,
    DateOnly ExpiryDate, int Quantity, decimal DiscountPercent, decimal CurrentPrice, decimal FinalPrice, decimal ProjectedMarginPercent, bool BelowCostGuardApplied);

public sealed record PricingRuleRequest(
    string Name, PricingRuleKind Kind, int Priority, Guid? BranchId, Guid? CustomerId, Guid? ProductId, Guid? CategoryId,
    Guid? ManufacturerId, CustomerType? CustomerType, Guid? PriceLevelId, SaleType? SaleType,
    int? MinimumQuantity, DateTime? StartsAtUtc, DateTime? EndsAtUtc,
    PricingAdjustmentType AdjustmentType, decimal AdjustmentValue, bool IsActive = true);

public sealed record PricingRuleDto(
    Guid Id, string Name, PricingRuleKind Kind, int Priority, Guid? BranchId, Guid? CustomerId, Guid? ProductId,
    Guid? CategoryId, Guid? ManufacturerId, CustomerType? CustomerType, Guid? PriceLevelId,
    SaleType? SaleType, int? MinimumQuantity, DateTime? StartsAtUtc, DateTime? EndsAtUtc,
    PricingAdjustmentType AdjustmentType, decimal AdjustmentValue, bool IsActive);

public sealed record PricingSuggestionRequest(Guid ProductId, decimal? TargetMarginPercent, decimal? TargetMarkupPercent, int RoundingIncrement = 1);
public sealed record PricingSuggestionDto(Guid ProductId, decimal Cost, decimal? CurrentPrice, decimal SuggestedPrice, decimal? MarginPercent, decimal? MarkupPercent);

public sealed record ReorderSuggestionDto(
    Guid ProductId, string Product, string SKU, Guid BranchId, string Branch, Guid? GodownId, string? Godown,
    int CurrentStock, int AvailableStock, int ReorderLevel, int PendingPurchaseOrderQuantity,
    decimal AverageDailySales, decimal? DaysOfStock, int SuggestedOrderQuantity, Guid? PreferredSupplierId,
    string? PreferredSupplier, decimal? LastPurchaseRate, string Risk);

public sealed record BusinessAlertDto(
    Guid Id, BusinessAlertCategory Category, BusinessAlertSeverity Severity, string Title,
    string Description, string SourceType, string SourceKey, Guid? BranchId, Guid? GodownId,
    DateTime CreatedAt, bool Dismissed, bool Resolved);

public sealed record AutomationRuleRequest(string Name, AutomationTriggerType TriggerType, string ConditionsJson, AutomationActionType ActionType, Guid? BranchId, bool IsActive = true);
public sealed record AutomationRuleDto(Guid Id, string Name, AutomationTriggerType TriggerType, string ConditionsJson, AutomationActionType ActionType, Guid? BranchId, bool IsActive, DateTime? LastRunAtUtc);
