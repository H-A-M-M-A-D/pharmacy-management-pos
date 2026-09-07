using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class ExpenseCategory : Entity, ISoftDeletable
{
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }
}
