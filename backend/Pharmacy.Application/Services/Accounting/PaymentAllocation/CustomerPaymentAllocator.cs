using Pharmacy.Application.DTOs.Customers;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.PaymentAllocation;

/// <summary>
/// Default FIFO (oldest due date, then oldest document date) allocation of a customer payment
/// against that customer's open credit-sale documents. Shared by <see cref="Customers.CustomerService"/>'s
/// direct payment recording and by the Cash/Bank Receipt voucher path, so the algorithm lives in
/// exactly one place. Takes its data access as delegates (rather than a repository interface) so
/// any caller whose repository exposes matching methods can reuse it without a shared base type.
/// Never over-allocates: each document receives at most its own remaining outstanding, and the sum
/// of allocations never exceeds the payment amount.
/// </summary>
public static class CustomerPaymentAllocator
{
    public static async Task<decimal> AllocateFifoAsync(
        Func<Guid, Guid?, CancellationToken, Task<IReadOnlyList<OpenReceivableDto>>> getOpenReceivables,
        Func<CustomerPaymentAllocation, CancellationToken, Task> addAllocation,
        Guid customerId, Guid branchId, Guid customerPaymentId,
        decimal paymentAmount, Guid actorId, DateTime allocatedAtUtc, CancellationToken cancellationToken = default)
    {
        if (paymentAmount <= 0) return 0;
        var openDocuments = await getOpenReceivables(customerId, null, cancellationToken);
        var remaining = paymentAmount;
        decimal totalAllocated = 0;
        foreach (var document in openDocuments)
        {
            if (remaining <= 0) break;
            var toAllocate = decimal.Round(Math.Min(remaining, document.Outstanding), 2);
            if (toAllocate <= 0) continue;
            await addAllocation(new CustomerPaymentAllocation
            {
                CustomerPaymentId = customerPaymentId,
                SaleId = document.SaleId,
                CustomerId = customerId,
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
