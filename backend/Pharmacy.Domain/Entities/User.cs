using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Represents a system user (employee).
/// </summary>
public class User : Entity
{
    /// <summary>
    /// Unique username/login identifier.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// User's email address.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// User's full name.
    /// </summary>
    public required string FullName { get; set; }

    /// <summary>
    /// Hashed password (never store plaintext).
    /// </summary>
    public required string PasswordHash { get; set; }

    /// <summary>
    /// Contact phone number.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Foreign key to the branch where this user works.
    /// </summary>
    public Guid BranchId { get; set; }

    /// <summary>
    /// Navigation property to the user's assigned branch.
    /// </summary>
    public Branch? Branch { get; set; }

    /// <summary>
    /// Foreign key to the user's assigned role.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Navigation property to the user's role.
    /// </summary>
    public Role? Role { get; set; }

    /// <summary>
    /// Whether the user account is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last login timestamp.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Navigation property for audit log entries created by this user.
    /// </summary>
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    /// <summary>
    /// Navigation property for stock movements performed by this user.
    /// </summary>
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
