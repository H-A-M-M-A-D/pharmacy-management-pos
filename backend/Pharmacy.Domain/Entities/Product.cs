using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Represents a pharmaceutical product/medicine.
/// IMPORTANT: This entity does NOT store current stock directly.
/// Stock is determined by StockMovement history and ProductBatch inventory.
/// The same product can have multiple batches with different prices and expiry dates.
/// </summary>
public class Product : Entity
{
    /// <summary>
    /// Stock Keeping Unit - unique product code.
    /// Must be unique within the system.
    /// </summary>
    public required string SKU { get; set; }

    /// <summary>
    /// Product barcode (optional, but must be unique when present).
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// Commercial/brand product name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Generic/INN (International Nonproprietary Name) name.
    /// </summary>
    public string? GenericName { get; set; }

    /// <summary>
    /// Brand name of the manufacturer.
    /// </summary>
    public string? BrandName { get; set; }

    /// <summary>
    /// Foreign key to product category.
    /// </summary>
    public Guid CategoryId { get; set; }

    /// <summary>
    /// Navigation property to the product category.
    /// </summary>
    public ProductCategory? Category { get; set; }

    /// <summary>
    /// Foreign key to manufacturer.
    /// </summary>
    public Guid? ManufacturerId { get; set; }

    /// <summary>
    /// Navigation property to the manufacturer.
    /// </summary>
    public Manufacturer? Manufacturer { get; set; }

    /// <summary>
    /// Unit of measurement (e.g., "tablet", "ml", "strip", "box").
    /// </summary>
    public required string Unit { get; set; }

    /// <summary>
    /// Pack size/quantity per pack (e.g., 10 tablets per strip, 30ml per bottle).
    /// </summary>
    public int PackSize { get; set; }

    /// <summary>
    /// Standard purchase price per unit (decimal to avoid floating point errors).
    /// This may be overridden per batch.
    /// </summary>
    public decimal PurchasePrice { get; set; }

    /// <summary>
    /// Standard retail selling price per unit.
    /// This may be overridden per batch.
    /// </summary>
    public decimal RetailPrice { get; set; }

    /// <summary>
    /// Trade price (wholesale price for retailers).
    /// This may be overridden per batch.
    /// </summary>
    public decimal? TradePrice { get; set; }

    /// <summary>
    /// Minimum stock level (reorder point).
    /// Alerts are triggered when stock falls below this level.
    /// </summary>
    public int ReorderLevel { get; set; }

    /// <summary>
    /// Maximum discount percentage allowed for this product.
    /// </summary>
    public decimal MaximumDiscountPercent { get; set; }

    /// <summary>
    /// Whether the product is currently active/available for sale.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property for all batches of this product.
    /// </summary>
    public ICollection<ProductBatch> ProductBatches { get; set; } = new List<ProductBatch>();

    /// <summary>
    /// Navigation property for inventory records.
    /// </summary>
    public ICollection<Inventory> Inventory { get; set; } = new List<Inventory>();

    /// <summary>
    /// Navigation property for stock movements.
    /// </summary>
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
