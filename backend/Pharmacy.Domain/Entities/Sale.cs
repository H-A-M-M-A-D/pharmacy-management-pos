using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class Sale : Entity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
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
    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    public ICollection<SalePayment> Payments { get; set; } = new List<SalePayment>();
}

public enum SaleStatus
{
    Held = 1,
    Posted = 2,
    Cancelled = 3
}
