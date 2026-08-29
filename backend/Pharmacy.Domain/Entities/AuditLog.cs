using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Audit log for tracking sensitive operations and changes.
/// Records who did what, when, and what changed.
/// </summary>
public class AuditLog : Entity
{
    /// <summary>
    /// Foreign key to the user who performed the action.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Navigation property to the user.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Description of the action performed (e.g., "Create", "Update", "Delete").
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// Entity type that was affected (e.g., "Product", "StockMovement", "User").
    /// </summary>
    public required string EntityType { get; set; }

    /// <summary>
    /// ID of the entity that was affected.
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Previous values before the change (JSON format).
    /// Only applicable for Update actions.
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// New values after the change (JSON format).
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Client IP address if available.
    /// </summary>
    public string? IPAddress { get; set; }

    /// <summary>
    /// User agent/browser information if available.
    /// </summary>
    public string? UserAgent { get; set; }
}
