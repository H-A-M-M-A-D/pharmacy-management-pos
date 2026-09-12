using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Money received from a customer before any invoice exists for it. Posts Dr Cash/Bank, Cr the
/// "Customer Advances" liability mapping (<see cref="AccountMappingKey.CustomerAdvances"/>) — never
/// directly to Sales Revenue or the Accounts Receivable control account, so it can't be mistaken for
/// either until it is explicitly applied to a real invoice via <see cref="CustomerAdvanceApplication"/>.
/// </summary>
public class CustomerAdvance : Entity
{
    public required string AdvanceNumber { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountApplied { get; set; }
    public Guid FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
    public DateTime ReceivedDateUtc { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime PostedAtUtc { get; set; }
    public ICollection<CustomerAdvanceApplication> Applications { get; set; } = [];
}

/// <summary>
/// Applies part (or all) of a <see cref="CustomerAdvance"/> to a specific open sale. Reuses the
/// standard payment-settlement machinery (a <see cref="CustomerPayment"/> tagged
/// <see cref="CustomerPaymentMethod.AppliedAdvance"/> plus a <see cref="CustomerPaymentAllocation"/>)
/// so AR aging and the customer ledger see it exactly like a real payment; the journal entry instead
/// moves the amount out of the Customer Advances liability (Dr Customer Advances, Cr Accounts
/// Receivable) since the cash already moved when the advance was originally received.
/// </summary>
public class CustomerAdvanceApplication : Entity
{
    public Guid CustomerAdvanceId { get; set; }
    public CustomerAdvance? CustomerAdvance { get; set; }
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }
    public decimal AppliedAmount { get; set; }
    public DateTime AppliedAtUtc { get; set; }
    public Guid CustomerPaymentId { get; set; }
    public CustomerPayment? CustomerPayment { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}

/// <summary>
/// The supplier-side mirror of <see cref="CustomerAdvance"/>: a payment made to a supplier before any
/// bill/goods-receipt exists for it. Posts Dr the "Supplier Advances" asset mapping
/// (<see cref="AccountMappingKey.SupplierAdvances"/>), Cr Cash/Bank.
/// </summary>
public class SupplierAdvance : Entity
{
    public required string AdvanceNumber { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountApplied { get; set; }
    public Guid FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
    public DateTime PaidDateUtc { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime PostedAtUtc { get; set; }
    public ICollection<SupplierAdvanceApplication> Applications { get; set; } = [];
}

/// <summary>
/// Applies part (or all) of a <see cref="SupplierAdvance"/> to a specific open goods receipt. Reuses
/// the standard settlement machinery (a <see cref="SupplierLedgerEntry"/> plus a
/// <see cref="SupplierPaymentAllocation"/>) so AP aging and the supplier ledger see it exactly like a
/// real payment; the journal entry moves the amount out of the Supplier Advances asset (Dr Accounts
/// Payable, Cr Supplier Advances).
/// </summary>
public class SupplierAdvanceApplication : Entity
{
    public Guid SupplierAdvanceId { get; set; }
    public SupplierAdvance? SupplierAdvance { get; set; }
    public Guid GoodsReceiptId { get; set; }
    public GoodsReceipt? GoodsReceipt { get; set; }
    public decimal AppliedAmount { get; set; }
    public DateTime AppliedAtUtc { get; set; }
    public Guid SupplierLedgerEntryId { get; set; }
    public SupplierLedgerEntry? SupplierLedgerEntry { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
