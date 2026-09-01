using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Represents a pharmaceutical manufacturer/company.
/// </summary>
public class Manufacturer : Entity
{
    /// <summary>
    /// Manufacturer name.
    /// </summary>
    public required string Name { get; set; }

    public string NormalizedName { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    /// <summary>
    /// Manufacturer country of origin.
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Contact email.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Contact phone number.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Physical address.
    /// </summary>
    public string? Address { get; set; }

    public string? Website { get; set; }

    /// <summary>
    /// Whether the manufacturer is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property for products manufactured by this company.
    /// </summary>
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
