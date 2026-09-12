using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public enum PricingRuleKind
{
    Rule = 1,
    Promotion = 2
}

public enum PricingAdjustmentType
{
    FixedPrice = 1,
    PercentageDiscount = 2,
    FixedDiscount = 3
}

public class PricingRule : Entity
{
    public required string Name { get; set; }
    public PricingRuleKind Kind { get; set; } = PricingRuleKind.Rule;
    public int Priority { get; set; }
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid? CategoryId { get; set; }
    public ProductCategory? Category { get; set; }
    public Guid? ManufacturerId { get; set; }
    public Manufacturer? Manufacturer { get; set; }
    public CustomerType? CustomerType { get; set; }
    public Guid? PriceLevelId { get; set; }
    public PriceLevel? PriceLevel { get; set; }
    public SaleType? SaleType { get; set; }
    public int? MinimumQuantity { get; set; }
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public PricingAdjustmentType AdjustmentType { get; set; }
    public decimal AdjustmentValue { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PricingPriceHistory : Entity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid? PriceLevelId { get; set; }
    public PriceLevel? PriceLevel { get; set; }
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public required string Reason { get; set; }
    public Guid ActorId { get; set; }
    public User? Actor { get; set; }
}

public enum BusinessAlertCategory
{
    Inventory = 1,
    Expiry = 2,
    Pricing = 3,
    Receivables = 4,
    Payables = 5,
    Purchasing = 6,
    System = 7
}

public enum BusinessAlertSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}

public class BusinessAlert : Entity
{
    public BusinessAlertCategory Category { get; set; }
    public BusinessAlertSeverity Severity { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string SourceType { get; set; }
    public required string SourceKey { get; set; }
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid? GodownId { get; set; }
    public Godown? Godown { get; set; }
    public DateTime? DismissedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

public enum AutomationTriggerType
{
    StockBelowReorder = 1,
    NearExpiry = 2,
    CustomerOverdue = 3,
    SupplierDue = 4,
    MarginBelowThreshold = 5
}

public enum AutomationActionType
{
    CreateAlert = 1,
    CreateDraftPurchaseOrder = 2,
    FlagForReview = 3
}

public class AutomationRule : Entity
{
    public required string Name { get; set; }
    public AutomationTriggerType TriggerType { get; set; }
    public required string ConditionsJson { get; set; }
    public AutomationActionType ActionType { get; set; }
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
}