using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public class ExpenseCategory : Entity
{
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
