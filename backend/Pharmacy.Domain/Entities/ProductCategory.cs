using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Represents a product category for organizing medicines.
/// </summary>
public class ProductCategory : Entity, ISoftDeletable
{
    /// <summary>
    /// Category name (e.g., "Antibiotics", "Pain Relief", "Vitamins").
    /// </summary>
    public required string Name { get; set; }

    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>
    /// Category description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether the category is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }

    /// <summary>
    /// Navigation property for products in this category.
    /// </summary>
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
