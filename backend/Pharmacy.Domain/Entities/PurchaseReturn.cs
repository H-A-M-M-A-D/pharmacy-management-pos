using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class PurchaseReturn : Entity
{
    public required string ReturnNumber { get; set; }
    public Guid OriginalGoodsReceiptId { get; set; }
    public GoodsReceipt? OriginalGoodsReceipt { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid ProcessedByUserId { get; set; }
    public User? ProcessedByUser { get; set; }
    public DateTime ReturnDateUtc { get; set; }
    public PurchaseReturnReason Reason { get; set; }
    public string? Notes { get; set; }
    public decimal GrossReturnAmount { get; set; }
    public decimal DiscountAdjustment { get; set; }
    public decimal TaxAdjustment { get; set; }
    public decimal NetSupplierCredit { get; set; }
    public PurchaseReturnStatus Status { get; set; } = PurchaseReturnStatus.Posted;
    public DateTime PostedAtUtc { get; set; }
    public ICollection<PurchaseReturnItem> Items { get; set; } = new List<PurchaseReturnItem>();
}

public enum PurchaseReturnStatus
{
    Posted = 1
}

public enum PurchaseReturnReason
{
    Damaged = 1,
    Expired = 2,
    SupplierRecall = 3,
    WrongItem = 4,
    ExcessSupply = 5,
    Other = 6
}
