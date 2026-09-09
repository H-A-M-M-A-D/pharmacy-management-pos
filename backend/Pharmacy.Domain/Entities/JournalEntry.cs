using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// The double-entry accounting source of truth. Every posted entry's lines must balance
/// (total debit = total credit); posted entries are permanent and cannot be edited or deleted —
/// corrections are new reversing/adjusting entries, never in-place edits.
/// </summary>
public class JournalEntry : Entity
{
    public required string EntryNumber { get; set; }
    public DateTime EntryDateUtc { get; set; }
    public JournalSourceType SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public string? Reference { get; set; }
    public required string Description { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid PostedByUserId { get; set; }
    public User? PostedByUser { get; set; }
    public DateTime PostedAtUtc { get; set; }
    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Posted;
    public ICollection<JournalEntryLine> Lines { get; set; } = [];
}

public class JournalEntryLine : Entity
{
    public Guid JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public Guid ChartOfAccountId { get; set; }
    public ChartOfAccount? ChartOfAccount { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string? Description { get; set; }
}

public enum JournalEntryStatus
{
    Posted = 1
}

public enum JournalSourceType
{
    Sale = 1,
    SalesReturn = 2,
    Purchase = 3,
    PurchaseReturn = 4,
    CustomerPayment = 5,
    SupplierPayment = 6,
    Expense = 7,
    OtherIncome = 8,
    CashTransfer = 9,
    StockWriteOff = 10,
    StockAdjustment = 11,
    OpeningBalance = 12,
    ManualVoucher = 13
}
