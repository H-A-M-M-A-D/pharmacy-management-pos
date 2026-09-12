using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A controlled bad-debt write-off that clears (all or part of) a customer's receivable balance.
/// Reuses the exact same settlement machinery a real payment uses (a <see cref="CustomerPayment"/>
/// tagged <see cref="CustomerPaymentMethod.WriteOff"/> plus <see cref="CustomerPaymentAllocation"/>
/// rows) so AR aging, the customer ledger, and everything downstream treat it identically to a real
/// payment except for which GL account absorbs the other side (Bad Debt Expense instead of Cash/Bank).
/// </summary>
public class CustomerWriteOff : Entity
{
    public required string WriteOffNumber { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public decimal Amount { get; set; }
    public required string Reason { get; set; }
    public string? Notes { get; set; }
    public DateTime WriteOffDateUtc { get; set; }
    public Guid? AppliedToSaleId { get; set; }
    public Sale? AppliedToSale { get; set; }
    public Guid CustomerPaymentId { get; set; }
    public CustomerPayment? CustomerPayment { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime PostedAtUtc { get; set; }
}

/// <summary>
/// The supplier-side mirror of <see cref="CustomerWriteOff"/>: clears a payable balance the business
/// will never actually pay (a settled dispute, a supplier waiving a small remainder). Reuses the same
/// <see cref="SupplierLedgerEntry"/>/<see cref="SupplierPaymentAllocation"/> settlement machinery a
/// real supplier payment uses, crediting Payables Write-Off Income instead of Cash/Bank.
/// </summary>
public class SupplierWriteOff : Entity
{
    public required string WriteOffNumber { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public decimal Amount { get; set; }
    public required string Reason { get; set; }
    public string? Notes { get; set; }
    public DateTime WriteOffDateUtc { get; set; }
    public Guid? AppliedToGoodsReceiptId { get; set; }
    public GoodsReceipt? AppliedToGoodsReceipt { get; set; }
    public Guid SupplierLedgerEntryId { get; set; }
    public SupplierLedgerEntry? SupplierLedgerEntry { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime PostedAtUtc { get; set; }
}
