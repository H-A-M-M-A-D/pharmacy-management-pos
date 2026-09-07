using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class SalesReturn : Entity
{
    public required string ReturnNumber { get; set; }
    public Guid OriginalSaleId { get; set; }
    public Sale? OriginalSale { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid ProcessedByUserId { get; set; }
    public User? ProcessedByUser { get; set; }
    public DateTime ReturnDateUtc { get; set; }
    public SalesReturnReason Reason { get; set; }
    public string? Notes { get; set; }
    public decimal GrossReturnAmount { get; set; }
    public decimal DiscountReturnAmount { get; set; }
    public decimal TaxReturnAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal CustomerCreditReductionAmount { get; set; }
    public decimal CashRefundAmount { get; set; }
    public SalesReturnStatus Status { get; set; } = SalesReturnStatus.Posted;
    public DateTime? PostedAtUtc { get; set; }
    public ICollection<SalesReturnItem> Items { get; set; } = new List<SalesReturnItem>();
    public ICollection<SalesRefundPayment> RefundPayments { get; set; } = new List<SalesRefundPayment>();
}

public enum SalesReturnStatus
{
    Posted = 1
}

public enum SalesReturnReason
{
    CustomerReturn = 1,
    WrongItem = 2,
    Damaged = 3,
    QualityIssue = 4,
    Other = 5
}
