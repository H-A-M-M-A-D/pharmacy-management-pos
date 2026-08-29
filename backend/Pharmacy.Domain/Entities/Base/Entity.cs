namespace Pharmacy.Domain.Entities.Base;

/// <summary>
/// Base class for all domain entities.
/// </summary>
public abstract class Entity : IEntity
{
    /// <summary>
    /// Unique identifier using GUID/UUID.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Entity creation timestamp (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Entity last update timestamp (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
