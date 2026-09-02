using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Represents a supplier of pharmaceutical products.
/// </summary>
public class Supplier : Entity
{
    /// <summary>
    /// Supplier name/company name.
    /// </summary>
    public required string Name { get; set; }

    public string NormalizedName { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    /// <summary>
    /// Supplier contact person name.
    /// </summary>
    public string? ContactPerson { get; set; }

    /// <summary>
    /// Contact email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Contact phone number.
    /// </summary>
    public string? PhoneNumber { get; set; }

    public string? AlternatePhone { get; set; }

    public string? WhatsApp { get; set; }

    /// <summary>
    /// Physical address.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// City/District.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Tax/Registration number (if applicable).
    /// </summary>
    public string? TaxNumber { get; set; }

    public string? STRN { get; set; }

    /// <summary>
    /// Payment terms description.
    /// </summary>
    public string? PaymentTerms { get; set; }

    public decimal OpeningBalance { get; set; }

    public decimal? CreditLimit { get; set; }

    public int? PaymentTermsDays { get; set; }

    /// <summary>
    /// Whether the supplier is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property for product batches supplied by this supplier.
    /// </summary>
    public ICollection<ProductBatch> ProductBatches { get; set; } = new List<ProductBatch>();

    public ICollection<SupplierLedgerEntry> LedgerEntries { get; set; } = new List<SupplierLedgerEntry>();
}
