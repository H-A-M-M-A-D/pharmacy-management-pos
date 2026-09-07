using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class CustomerLedgerEntry : Entity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public required CustomerLedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public DateOnly EntryDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public void Validate() => ValidateAmountForEntryType(EntryType, Amount);

    public static void ValidateAmountForEntryType(CustomerLedgerEntryType entryType, decimal amount)
    {
        if (!Enum.IsDefined(entryType)) throw new InvalidOperationException($"Unknown customer ledger entry type: {entryType}.");
        if (amount == 0) throw new InvalidOperationException("Customer ledger amount cannot be zero.");
        if (entryType == CustomerLedgerEntryType.OpeningBalance) return;
        var positive = entryType is CustomerLedgerEntryType.CreditSale or CustomerLedgerEntryType.AdjustmentDebit;
        var negative = entryType is CustomerLedgerEntryType.Payment or CustomerLedgerEntryType.SalesReturn or CustomerLedgerEntryType.AdjustmentCredit;
        if (positive && amount <= 0) throw new InvalidOperationException($"{entryType} requires a positive amount.");
        if (negative && amount >= 0) throw new InvalidOperationException($"{entryType} requires a negative amount.");
    }
}

public enum CustomerLedgerEntryType
{
    OpeningBalance = 1,
    CreditSale = 2,
    Payment = 3,
    SalesReturn = 4,
    AdjustmentDebit = 5,
    AdjustmentCredit = 6
}
