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
    /// <summary>Set once this entry has been matched into a <see cref="BankReconciliation"/>. This is
    /// the one pair of fields this otherwise-immutable row may still change after insert — see
    /// <see cref="Pharmacy.Infrastructure.Data.PharmacyDbContext"/>'s finance-document validator,
    /// which allows a Modified state only when exactly these two properties changed.</summary>
    public Guid? BankReconciliationId { get; set; }
    public BankReconciliation? BankReconciliation { get; set; }
    public DateTime? ReconciledAtUtc { get; set; }
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
