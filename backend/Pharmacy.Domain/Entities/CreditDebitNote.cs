using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A formal, numbered, stock-neutral financial adjustment reducing a customer's receivable balance —
/// for billing corrections, goodwill credits, or price disputes that don't involve returned goods
/// (a real physical return still goes through <see cref="SalesReturn"/>). Posts through the same
/// AR-adjustment-suspense pattern as <c>CustomerService.AdjustBalanceAsync</c>, just with a document
/// identity, mandatory reason, and optional single-invoice allocation. When <see cref="AppliedToSaleId"/>
/// is set the credit settles that specific sale (via a real <see cref="CustomerPayment"/>/
/// <see cref="CustomerPaymentAllocation"/> pair so AR aging stays automatically correct); otherwise it
/// is FIFO-allocated across the customer's open receivables using the same allocator every other
/// customer credit uses.
/// </summary>
public class CreditNote : Entity
{
    public required string CreditNoteNumber { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public decimal Amount { get; set; }
    public required string Reason { get; set; }
    public string? Notes { get; set; }
    public DateTime IssueDateUtc { get; set; }
    public Guid? AppliedToSaleId { get; set; }
    public Sale? AppliedToSale { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime PostedAtUtc { get; set; }
}

/// <summary>
/// The supplier-side mirror of <see cref="CreditNote"/>: a formal, numbered, stock-neutral reduction
/// of a payable balance (a real physical return still goes through <see cref="PurchaseReturn"/>).
/// </summary>
public class DebitNote : Entity
{
    public required string DebitNoteNumber { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public decimal Amount { get; set; }
    public required string Reason { get; set; }
    public string? Notes { get; set; }
    public DateTime IssueDateUtc { get; set; }
    public Guid? AppliedToGoodsReceiptId { get; set; }
    public GoodsReceipt? AppliedToGoodsReceipt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime PostedAtUtc { get; set; }
}
