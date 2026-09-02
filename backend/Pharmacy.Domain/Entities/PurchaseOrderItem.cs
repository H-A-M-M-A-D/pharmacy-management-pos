using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class PurchaseOrderItem : Entity
{
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int OrderedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public decimal? ExpectedPurchasePrice { get; set; }
    public string? Notes { get; set; }
    public ICollection<GoodsReceiptItem> GoodsReceiptItems { get; set; } = new List<GoodsReceiptItem>();
}
