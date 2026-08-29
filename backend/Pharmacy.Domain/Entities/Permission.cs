using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Represents a permission that controls what users can do.
/// Permissions are granular and not role-dependent.
/// Examples: "products.view", "sales.create", "inventory.adjust"
/// </summary>
public class Permission : Entity
{
    /// <summary>
    /// Unique permission code (e.g., "products.view", "sales.create").
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Human-readable permission description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Permission category for grouping (e.g., "products", "sales", "inventory", "users", "reports").
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// Navigation property for roles that have this permission.
    /// </summary>
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
