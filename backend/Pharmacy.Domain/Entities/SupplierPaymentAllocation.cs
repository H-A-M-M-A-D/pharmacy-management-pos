using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Links a supplier payment (the <see cref="SupplierLedgerEntry"/> row of type
/// <see cref="SupplierLedgerEntryType.Payment"/>) to a specific open goods-receipt document
/// (<see cref="GoodsReceipt"/>) it settles, in whole or in part. There is no dedicated
/// "SupplierPayment" header entity in this system (unlike Customers) — the ledger entry itself
/// is the stable payment record, so it is what allocations reference.
/// </summary>
public class SupplierPaymentAllocation : Entity
{
    public Guid SupplierLedgerEntryId { get; set; }
    public SupplierLedgerEntry? SupplierLedgerEntry { get; set; }
    public Guid GoodsReceiptId { get; set; }
    public GoodsReceipt? GoodsReceipt { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public decimal AllocatedAmount { get; set; }
    public DateTime AllocatedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
