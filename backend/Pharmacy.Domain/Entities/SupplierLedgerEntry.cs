using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class SupplierLedgerEntry : Entity
{
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public required SupplierLedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public DateOnly EntryDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public void Validate()
    {
        ValidateAmountForEntryType(EntryType, Amount);
    }

    public static void ValidateAmountForEntryType(SupplierLedgerEntryType entryType, decimal amount)
    {
        if (!Enum.IsDefined(entryType)) throw new InvalidOperationException($"Unknown supplier ledger entry type: {entryType}.");
        if (amount == 0) throw new InvalidOperationException("Supplier ledger amount cannot be zero.");
        if (entryType == SupplierLedgerEntryType.OpeningBalance) return;
        var positive = entryType is SupplierLedgerEntryType.AdjustmentDebit or SupplierLedgerEntryType.Purchase;
        var negative = entryType is SupplierLedgerEntryType.Payment or SupplierLedgerEntryType.AdjustmentCredit or SupplierLedgerEntryType.PurchaseReturn;
        if (positive && amount <= 0) throw new InvalidOperationException($"{entryType} requires a positive amount.");
        if (negative && amount >= 0) throw new InvalidOperationException($"{entryType} requires a negative amount.");
    }
}

public enum SupplierLedgerEntryType
{
    OpeningBalance = 1,
    Payment = 2,
    AdjustmentDebit = 3,
    AdjustmentCredit = 4,
    Purchase = 5,
    PurchaseReturn = 6
}
