using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class SaleItem : Entity
{
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int RequestedQuantity { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }
    public PriceSource PriceSource { get; set; } = PriceSource.Default;
    public decimal? ResolvedUnitPrice { get; set; }
    public bool IsManualPriceOverride { get; set; }
    public string? PriceOverrideReason { get; set; }
    public bool IsDiscountOverride { get; set; }
    public string? DiscountOverrideReason { get; set; }
    public bool IsBelowCost { get; set; }
    public string? BelowCostOverrideReason { get; set; }
    public ICollection<SaleItemBatchAllocation> Allocations { get; set; } = new List<SaleItemBatchAllocation>();
}

public enum PriceSource
{
    Default = 1,
    PriceLevel = 2,
    QuantityBreak = 3,
    ManualOverride = 4,

    /// <summary>Price carried over unchanged from a source SalesQuotation/SalesOrder line - not a
    /// fresh resolution and not a human override, so it does not require sales.price_override.</summary>
    DocumentSnapshot = 5,
    PricingRule = 6,
    Promotion = 7
}
