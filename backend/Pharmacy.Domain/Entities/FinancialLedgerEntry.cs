using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class FinancialLedgerEntry : Entity
{
    public Guid FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public FinancialLedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public required string ReferenceType { get; set; }
    public Guid ReferenceId { get; set; }
    public string? ReferenceNumber { get; set; }
    public required string Description { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public enum FinancialLedgerEntryType
{
    OpeningBalance = 1,
    SalePayment = 2,
    CustomerPayment = 3,
    SupplierPayment = 4,
    SalesRefund = 5,
    Expense = 6,
    OtherIncome = 7,
    TransferOut = 8,
    TransferIn = 9,
    AdjustmentDebit = 10,
    AdjustmentCredit = 11
}
