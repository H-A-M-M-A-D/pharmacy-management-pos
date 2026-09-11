using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A confirmed customer order awaiting fulfillment. Confirming an order never posts to the journal
/// or moves stock by itself - only fulfilling it (which creates a real Sale) does. Fulfillment may
/// happen across several partial Sales; SalesOrderItem tracks OrderedQuantity vs FulfilledQuantity.
/// </summary>
public class SalesOrder : Entity
{
    public required string OrderNumber { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid? GodownId { get; set; }
    public Godown? Godown { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? PriceLevelId { get; set; }
    public PriceLevel? PriceLevel { get; set; }
    public Guid? QuotationId { get; set; }
    public SalesQuotation? Quotation { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly? ExpectedDeliveryDate { get; set; }
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;
    public string? Notes { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal NetTotal { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public User? ConfirmedByUser { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();
}

public enum SalesOrderStatus
{
    Draft = 1,
    Confirmed = 2,
    PartiallyFulfilled = 3,
    Fulfilled = 4,
    Cancelled = 5
}

public class SalesOrderItem : Entity
{
    public Guid SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int OrderedQuantity { get; set; }
    public int FulfilledQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
}
