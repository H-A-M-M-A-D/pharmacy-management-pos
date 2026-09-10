using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class GoodsReceipt : Entity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>Destination godown stock was received into. Nullable for legacy/unscoped branches.</summary>
    public Guid? GodownId { get; set; }
    public Godown? Godown { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public required string GrnNumber { get; set; }
    public string? SupplierInvoiceNumber { get; set; }
    public string? NormalizedSupplierInvoiceNumber { get; set; }
    public DateOnly ReceiptDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Posted;
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal NetTotal { get; set; }
    public string? Notes { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public User? ReceivedByUser { get; set; }
    public ICollection<GoodsReceiptItem> Items { get; set; } = new List<GoodsReceiptItem>();
}

public enum GoodsReceiptStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3
}
