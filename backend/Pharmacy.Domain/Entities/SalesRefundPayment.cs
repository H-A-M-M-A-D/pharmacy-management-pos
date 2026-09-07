using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class SalesRefundPayment : Entity
{
    public Guid SalesReturnId { get; set; }
    public SalesReturn? SalesReturn { get; set; }
    public SalePaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid? FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
}
