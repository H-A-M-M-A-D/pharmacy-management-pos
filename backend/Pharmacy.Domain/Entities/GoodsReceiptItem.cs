using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class GoodsReceiptItem : Entity
{
    public Guid GoodsReceiptId { get; set; }
    public GoodsReceipt? GoodsReceipt { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid? PurchaseOrderItemId { get; set; }
    public PurchaseOrderItem? PurchaseOrderItem { get; set; }
    public Guid? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }
    public required string BatchNumber { get; set; }
    public DateOnly? ManufacturingDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public int PurchasedQuantity { get; set; }
    public int BonusQuantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal RetailPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetLineAmount { get; set; }
}
