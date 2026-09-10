using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Represents a product batch.
/// Each product can have multiple batches with different:
/// - Manufacturing and expiry dates
/// - Purchase and retail prices
/// - Suppliers
/// - Available quantities per branch
///
/// Critical for:
/// - FEFO (First Expire, First Out) logic
/// - Batch/lot tracking
/// - Expiry management
/// - Price variations per batch
/// </summary>
public class ProductBatch : Entity
{
    /// <summary>
    /// Foreign key to the product.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Navigation property to the product.
    /// </summary>
    public Product? Product { get; set; }

    /// <summary>
    /// Foreign key to the supplier who supplied this batch.
    /// </summary>
    public Guid? SupplierId { get; set; }

    /// <summary>
    /// Navigation property to the supplier.
    /// </summary>
    public Supplier? Supplier { get; set; }

    /// <summary>
    /// Foreign key to the branch where this batch is stored.
    /// </summary>
    public Guid BranchId { get; set; }

    /// <summary>
    /// Navigation property to the branch.
    /// </summary>
    public Branch? Branch { get; set; }

    /// <summary>
    /// Foreign key to the godown (stock location) within the branch where this batch physically sits.
    /// Nullable for records created before multi-godown support (legacy/unscoped stock).
    /// </summary>
    public Guid? GodownId { get; set; }

    /// <summary>
    /// Navigation property to the godown.
    /// </summary>
    public Godown? Godown { get; set; }

    /// <summary>
    /// Unique batch/lot number from supplier.
    /// Note: Batch numbers may repeat across branches and products, so
    /// (BranchId + GodownId + ProductId + BatchNumber) is unique.
    /// </summary>
    public required string BatchNumber { get; set; }

    /// <summary>
    /// Manufacturing date (may not always be available).
    /// </summary>
    public DateOnly? ManufacturingDate { get; set; }

    /// <summary>
    /// Expiry date (CRITICAL for pharmacy - never sell after this date).
    /// </summary>
    public required DateOnly ExpiryDate { get; set; }

    /// <summary>
    /// Purchase price per unit for this batch (may differ from product default).
    /// </summary>
    public decimal PurchasePrice { get; set; }

    /// <summary>
    /// Retail price per unit for this batch (may differ from product default).
    /// </summary>
    public decimal RetailPrice { get; set; }

    /// <summary>
    /// Total quantity received from supplier for this batch.
    /// </summary>
    public int QuantityReceived { get; set; }

    /// <summary>
    /// Quantity currently available for sale.
    /// This is updated through StockMovement records.
    /// </summary>
    public int QuantityAvailable { get; set; }

    /// <summary>
    /// Whether this batch is marked for disposal/expired.
    /// </summary>
    public bool IsDisposed { get; set; }

    /// <summary>
    /// Navigation property for inventory records linked to this batch.
    /// </summary>
    public ICollection<Inventory> Inventory { get; set; } = new List<Inventory>();

    /// <summary>
    /// Navigation property for stock movements of this batch.
    /// </summary>
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
