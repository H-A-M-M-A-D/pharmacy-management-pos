using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A business-friendly typed accounting voucher (Cash/Bank Receipt or Payment, Contra, Journal).
/// Every voucher posts through the existing <see cref="JournalEntry"/>/<see cref="JournalEntryLine"/>
/// engine — this entity never carries its own balances, it is a numbered, party-aware wrapper around
/// one journal entry. Draft vouchers have no journal entry yet and can be cancelled freely; once
/// posted, the voucher (and its journal entry) is immutable and corrections must be reversals.
/// </summary>
public class Voucher : Entity
{
    public required string VoucherNumber { get; set; }
    public VoucherType Type { get; set; }
    public DateTime VoucherDateUtc { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string? Reference { get; set; }
    public required string Description { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid? ChartOfAccountId { get; set; }
    public ChartOfAccount? ChartOfAccount { get; set; }
    public Guid? ContraToChartOfAccountId { get; set; }
    public ChartOfAccount? ContraToChartOfAccount { get; set; }
    public decimal? Amount { get; set; }
    /// <summary>Optional till/bank attribution for Cash/Bank Receipt/Payment/Contra vouchers. When set,
    /// posting also writes a <see cref="FinancialLedgerEntry"/> against this account so the voucher's
    /// cash movement shows up in that account's Cash/Bank Book and is eligible for bank reconciliation.
    /// Left null, a voucher still posts to the GL Cash/Bank control account exactly as before —
    /// entirely backward compatible with vouchers created before this field existed.</summary>
    public Guid? FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
    public VoucherStatus Status { get; set; } = VoucherStatus.Draft;
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public Guid? PostedByUserId { get; set; }
    public User? PostedByUser { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public Guid? JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public Guid? ReversalOfVoucherId { get; set; }
    public Voucher? ReversalOfVoucher { get; set; }
    public ICollection<VoucherLine> Lines { get; set; } = new List<VoucherLine>();
}

/// <summary>
/// A journal-style debit/credit line captured on the voucher for print/detail rendering. Mirrors
/// <see cref="JournalEntryLine"/> exactly and is written at posting time from the same computed
/// lines that get sent to the journal entry, never derived from it after the fact.
/// </summary>
public class VoucherLine : Entity
{
    public Guid VoucherId { get; set; }
    public Voucher? Voucher { get; set; }
    public Guid ChartOfAccountId { get; set; }
    public ChartOfAccount? ChartOfAccount { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string? Description { get; set; }
}

public enum VoucherType
{
    CashReceipt = 1,
    CashPayment = 2,
    BankReceipt = 3,
    BankPayment = 4,
    Contra = 5,
    Journal = 6
}

public enum VoucherStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3
}
