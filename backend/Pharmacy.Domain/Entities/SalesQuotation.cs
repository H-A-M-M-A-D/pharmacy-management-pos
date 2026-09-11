using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A non-binding price quote for a customer. Never moves stock and never posts to the journal;
/// it only becomes financially real once converted into a SalesOrder or directly into a Sale.
/// </summary>
public class SalesQuotation : Entity
{
    public required string QuotationNumber { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid? GodownId { get; set; }
    public Godown? Godown { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? PriceLevelId { get; set; }
    public PriceLevel? PriceLevel { get; set; }
    public DateOnly QuotationDate { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public SalesQuotationStatus Status { get; set; } = SalesQuotationStatus.Draft;
    public string? Notes { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal NetTotal { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public Guid? ConvertedToSalesOrderId { get; set; }
    public SalesOrder? ConvertedToSalesOrder { get; set; }
    public Guid? ConvertedToSaleId { get; set; }
    public Sale? ConvertedToSale { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public ICollection<SalesQuotationItem> Items { get; set; } = new List<SalesQuotationItem>();
}

public enum SalesQuotationStatus
{
    Draft = 1,
    Sent = 2,
    Accepted = 3,
    Rejected = 4,
    Expired = 5,
    Converted = 6,
    Cancelled = 7
}

/// <summary>A quotation line. UnitPrice/amounts are snapshots taken at creation time and never
/// silently recalculated - only an explicit edit of a Draft quotation changes them.</summary>
public class SalesQuotationItem : Entity
{
    public Guid SalesQuotationId { get; set; }
    public SalesQuotation? SalesQuotation { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
}
