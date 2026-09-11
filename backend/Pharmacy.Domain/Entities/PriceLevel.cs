using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A commercial pricing tier (e.g. Retail, Wholesale, Trade). Global by default; a Branch-scoped
/// row overrides the global set of levels for that branch only when BranchId is set.
/// </summary>
public class PriceLevel : Entity
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public int Priority { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public ICollection<ProductPriceLevel> ProductPrices { get; set; } = new List<ProductPriceLevel>();
    public ICollection<ProductPriceBreak> ProductPriceBreaks { get; set; } = new List<ProductPriceBreak>();
}

/// <summary>Flat per-product selling price for a given price level (no quantity dependency).</summary>
public class ProductPriceLevel : Entity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid PriceLevelId { get; set; }
    public PriceLevel? PriceLevel { get; set; }
    public decimal SellingPrice { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// A quantity-break selling price for a product. PriceLevelId is nullable: null means the break
/// applies generically regardless of the customer's assigned price level; a set value scopes the
/// break to that level only.
/// </summary>
public class ProductPriceBreak : Entity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid? PriceLevelId { get; set; }
    public PriceLevel? PriceLevel { get; set; }
    public int MinimumQuantity { get; set; }
    public decimal SellingPrice { get; set; }
    public bool IsActive { get; set; } = true;
}
