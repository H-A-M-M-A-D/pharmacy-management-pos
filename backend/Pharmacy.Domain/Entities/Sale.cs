using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class Sale : Entity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>Source godown stock was deducted from. Nullable for legacy/unscoped branches.</summary>
    public Guid? GodownId { get; set; }
    public Godown? Godown { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? HoldNumber { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Held;
    public DateTime? PostedAtUtc { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public Guid CashierUserId { get; set; }
    public User? CashierUser { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal NetTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal ChangeGiven { get; set; }
    public string? Notes { get; set; }
    public SaleType SaleType { get; set; } = SaleType.Retail;
    public Guid? PriceLevelId { get; set; }
    public PriceLevel? PriceLevel { get; set; }
    public Guid? QuotationId { get; set; }
    public SalesQuotation? Quotation { get; set; }
    public Guid? SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }
    public string? CustomerPoNumber { get; set; }
    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    public ICollection<SalePayment> Payments { get; set; } = new List<SalePayment>();
}

public enum SaleStatus
{
    Held = 1,
    Posted = 2,
    Cancelled = 3
}

public enum SaleType
{
    Retail = 1,
    Wholesale = 2
}
