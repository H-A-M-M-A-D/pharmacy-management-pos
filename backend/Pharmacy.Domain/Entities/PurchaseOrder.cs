using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class PurchaseOrder : Entity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid? GodownId { get; set; }
    public Godown? Godown { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public required string OrderNumber { get; set; }
    public string? SupplierReference { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
    public ICollection<GoodsReceipt> GoodsReceipts { get; set; } = new List<GoodsReceipt>();
}

public enum PurchaseOrderStatus
{
    Draft = 1,
    Submitted = 2,
    PartiallyReceived = 3,
    Completed = 4,
    Cancelled = 5
}
