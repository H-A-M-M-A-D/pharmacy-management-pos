using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Represents a user role (e.g., Owner, Manager, Pharmacist, Cashier).
/// Roles are collections of permissions.
/// </summary>
public class Role : Entity
{
    /// <summary>
    /// Unique role name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Role description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this role is built-in and cannot be deleted.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// Whether this role is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property for permissions assigned to this role.
    /// </summary>
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    /// <summary>
    /// Navigation property for users assigned to this role.
    /// </summary>
    public ICollection<User> Users { get; set; } = new List<User>();
}
