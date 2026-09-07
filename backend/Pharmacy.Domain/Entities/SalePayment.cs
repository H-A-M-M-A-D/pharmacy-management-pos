using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class SalePayment : Entity
{
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }
    public SalePaymentMethod Method { get; set; }
    public decimal AmountApplied { get; set; }
    public decimal? TenderedAmount { get; set; }
    public string? ReferenceNumber { get; set; }
    public Guid? FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
}

public enum SalePaymentMethod
{
    Cash = 1,
    Card = 2,
    BankTransfer = 3,
    Easypaisa = 4,
    JazzCash = 5,
    Other = 6
}
