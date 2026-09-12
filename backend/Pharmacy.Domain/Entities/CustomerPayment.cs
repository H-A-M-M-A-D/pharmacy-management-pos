using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class CustomerPayment : Entity
{
    public required string ReceiptNumber { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public decimal Amount { get; set; }
    public CustomerPaymentMethod PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime PaymentDateUtc { get; set; }
    public string? Notes { get; set; }
    public Guid ReceivedByUserId { get; set; }
    public User? ReceivedByUser { get; set; }
    public Guid? FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
}

public enum CustomerPaymentMethod
{
    Cash = 1,
    Card = 2,
    BankTransfer = 3,
    Easypaisa = 4,
    JazzCash = 5,
    Cheque = 6,
    Other = 7,
    WriteOff = 8,
    AppliedAdvance = 9,
    CreditNote = 10
}
