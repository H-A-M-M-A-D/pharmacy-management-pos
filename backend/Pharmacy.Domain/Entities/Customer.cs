using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class Customer : Entity
{
    public required string CustomerCode { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AlternatePhone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? BusinessName { get; set; }
    public string? NTN { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal CreditLimit { get; set; }
    public int? CreditDays { get; set; }
    public bool IsActive { get; set; } = true;
    public CustomerType CustomerType { get; set; } = CustomerType.Retail;
    public bool CreditAllowed { get; set; } = true;
    public Guid? PriceLevelId { get; set; }
    public PriceLevel? PriceLevel { get; set; }
    public string? ContactPerson { get; set; }
    public string? ShippingAddress { get; set; }
    public string? Notes { get; set; }
    public ICollection<CustomerLedgerEntry> LedgerEntries { get; set; } = new List<CustomerLedgerEntry>();
    public ICollection<CustomerPayment> Payments { get; set; } = new List<CustomerPayment>();
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}

public enum CustomerType
{
    Retail = 1,
    Wholesale = 2,
    Institutional = 3
}
