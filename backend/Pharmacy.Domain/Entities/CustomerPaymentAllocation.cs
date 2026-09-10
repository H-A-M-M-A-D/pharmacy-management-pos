using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Links a <see cref="CustomerPayment"/> to a specific open credit-sale document (<see cref="Sale"/>)
/// it settles, in whole or in part. Without this, customer payments only reduce the customer's
/// running ledger balance with no traceable link back to the invoice(s) they paid off.
/// </summary>
public class CustomerPaymentAllocation : Entity
{
    public Guid CustomerPaymentId { get; set; }
    public CustomerPayment? CustomerPayment { get; set; }
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public decimal AllocatedAmount { get; set; }
    public DateTime AllocatedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
