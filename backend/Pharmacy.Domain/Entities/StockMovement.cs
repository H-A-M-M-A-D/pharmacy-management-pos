using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Represents a stock movement transaction.
/// IMMUTABLE and permanent record of every inventory change.
/// This is the source of truth for stock levels.
/// </summary>
public class StockMovement : Entity
{
    private int _quantity;
    private StockMovementType _movementType;

    /// <summary>
    /// Type of stock movement.
    /// </summary>
    public required StockMovementType MovementType
    {
        get => _movementType;
        set
        {
            if (_quantity != 0)
            {
                ValidateQuantityForMovementType(value, _quantity);
            }
            _movementType = value;
        }
    }

    /// <summary>
    /// Foreign key to branch where movement occurred.
    /// </summary>
    public Guid BranchId { get; set; }

    /// <summary>
    /// Navigation property to branch.
    /// </summary>
    public Branch? Branch { get; set; }

    /// <summary>
    /// Foreign key to product.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Navigation property to product.
    /// </summary>
    public Product? Product { get; set; }

    /// <summary>
    /// Foreign key to product batch.
    /// </summary>
    public Guid ProductBatchId { get; set; }

    /// <summary>
    /// Navigation property to product batch.
    /// </summary>
    public ProductBatch? ProductBatch { get; set; }

    /// <summary>
    /// Quantity moved (positive or negative based on convention).
    /// Positive for stock in (purchases, returns).
    /// Negative for stock out (sales, adjustments).
    /// </summary>
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (_movementType != 0)
            {
                ValidateQuantityForMovementType(_movementType, value);
            }
            _quantity = value;
        }
    }

    public void Validate()
    {
        ValidateQuantityForMovementType(MovementType, Quantity);
    }

    public static void ValidateQuantityForMovementType(StockMovementType movementType, int quantity)
    {
        if (!Enum.IsDefined(movementType))
        {
            throw new InvalidOperationException($"Unknown stock movement type: {movementType}.");
        }

        if (quantity == 0)
        {
            throw new InvalidOperationException("Stock movement quantity cannot be zero.");
        }

        var isPositiveMovement = movementType is StockMovementType.OpeningStock or StockMovementType.Purchase or StockMovementType.SaleReturn or StockMovementType.TransferIn or StockMovementType.AdjustmentIncrease;
        var isNegativeMovement = movementType is StockMovementType.Sale or StockMovementType.PurchaseReturn or StockMovementType.TransferOut or StockMovementType.AdjustmentDecrease or StockMovementType.Expired or StockMovementType.Damaged;

        if (isPositiveMovement && quantity <= 0)
        {
            throw new InvalidOperationException($"{movementType} requires a positive quantity.");
        }

        if (isNegativeMovement && quantity >= 0)
        {
            throw new InvalidOperationException($"{movementType} requires a negative quantity.");
        }
    }

    /// <summary>
    /// Type of document that triggered this movement.
    /// E.g., "Purchase", "Sale", "SaleReturn", "PurchaseReturn", "Adjustment"
    /// </summary>
    public string? ReferenceType { get; set; }

    /// <summary>
    /// ID of the reference document (e.g., Sale ID, Purchase ID).
    /// </summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>
    /// Optional notes/remarks for this movement.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Foreign key to user who performed this movement.
    /// </summary>
    public Guid? PerformedByUserId { get; set; }

    /// <summary>
    /// Navigation property to the user who performed the movement.
    /// </summary>
    public User? PerformedByUser { get; set; }
}

/// <summary>
/// Types of stock movements in the pharmacy system.
/// </summary>
public enum StockMovementType
{
    /// <summary>
    /// Opening/initial stock.
    /// </summary>
    OpeningStock = 1,

    /// <summary>
    /// Purchase from supplier.
    /// </summary>
    Purchase = 2,

    /// <summary>
    /// Sale to customer.
    /// </summary>
    Sale = 3,

    /// <summary>
    /// Return from customer.
    /// </summary>
    SaleReturn = 4,

    /// <summary>
    /// Return to supplier.
    /// </summary>
    PurchaseReturn = 5,

    /// <summary>
    /// Transfer into this branch from another branch.
    /// </summary>
    TransferIn = 6,

    /// <summary>
    /// Transfer out to another branch.
    /// </summary>
    TransferOut = 7,

    /// <summary>
    /// Stock increase adjustment.
    /// </summary>
    AdjustmentIncrease = 8,

    /// <summary>
    /// Stock decrease adjustment.
    /// </summary>
    AdjustmentDecrease = 9,

    /// <summary>
    /// Expired product disposal.
    /// </summary>
    Expired = 10,

    /// <summary>
    /// Damaged product.
    /// </summary>
    Damaged = 11
}
