using Pharmacy.Application.DTOs.Suppliers;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.PaymentAllocation;

/// <summary>
/// Default FIFO (oldest due date, then oldest document date) allocation of a supplier payment
/// against that supplier's open goods-receipt documents. Mirrors <see cref="CustomerPaymentAllocator"/>
/// exactly, including the delegate-based data access; see its remarks for the shared rationale.
/// </summary>
public static class SupplierPaymentAllocator
{
    public static async Task<decimal> AllocateFifoAsync(
        Func<Guid, Guid?, CancellationToken, Task<IReadOnlyList<OpenPayableDto>>> getOpenPayables,
        Func<SupplierPaymentAllocation, CancellationToken, Task> addAllocation,
        Guid supplierId, Guid branchId, Guid supplierLedgerEntryId,
        decimal paymentAmount, Guid actorId, DateTime allocatedAtUtc, CancellationToken cancellationToken = default)
    {
        if (paymentAmount <= 0) return 0;
        var openDocuments = await getOpenPayables(supplierId, null, cancellationToken);
        var remaining = paymentAmount;
        decimal totalAllocated = 0;
        foreach (var document in openDocuments)
        {
            if (remaining <= 0) break;
            var toAllocate = decimal.Round(Math.Min(remaining, document.Outstanding), 2);
            if (toAllocate <= 0) continue;
            await addAllocation(new SupplierPaymentAllocation
            {
                SupplierLedgerEntryId = supplierLedgerEntryId,
                GoodsReceiptId = document.GoodsReceiptId,
                SupplierId = supplierId,
                BranchId = branchId,
                AllocatedAmount = toAllocate,
                AllocatedAtUtc = allocatedAtUtc,
                CreatedByUserId = actorId
            }, cancellationToken);
            remaining -= toAllocate;
            totalAllocated += toAllocate;
        }
        return totalAllocated;
    }
}
