using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class PurchaseReturnItem : Entity
{
    public Guid PurchaseReturnId { get; set; }
    public PurchaseReturn? PurchaseReturn { get; set; }
    public Guid OriginalGoodsReceiptItemId { get; set; }
    public GoodsReceiptItem? OriginalGoodsReceiptItem { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }
    public required string BatchNumber { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public int PaidReturnQuantity { get; set; }
    public int BonusReturnQuantity { get; set; }
    public decimal PurchasePriceSnapshot { get; set; }
    public decimal GrossReturnAmount { get; set; }
    public decimal DiscountAdjustment { get; set; }
    public decimal TaxAdjustment { get; set; }
    public decimal NetSupplierCredit { get; set; }
}
