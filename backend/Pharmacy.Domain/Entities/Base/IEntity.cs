namespace Pharmacy.Domain.Entities.Base;

/// <summary>
/// Base interface for all domain entities.
/// All entities use GUID/UUID as primary key.
/// </summary>
public interface IEntity
{
    Guid Id { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
