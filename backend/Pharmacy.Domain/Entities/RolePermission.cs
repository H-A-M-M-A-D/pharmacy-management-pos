using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Junction entity linking roles to permissions (many-to-many relationship).
/// </summary>
public class RolePermission : Entity
{
    /// <summary>
    /// Foreign key to Role.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Foreign key to Permission.
    /// </summary>
    public Guid PermissionId { get; set; }

    /// <summary>
    /// Navigation property to the role.
    /// </summary>
    public Role? Role { get; set; }

    /// <summary>
    /// Navigation property to the permission.
    /// </summary>
    public Permission? Permission { get; set; }
}
