using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// An optional cost/department dimension for manual journals, expenses, and other income lines
/// (<see cref="JournalEntryLine.CostCenterId"/>). Deliberately not retrofitted onto automatic
/// Sales/Purchase/etc. postings — those call sites never set it, so this is purely additive and
/// carries zero risk to existing, already-tested posting paths.
/// </summary>
public class CostCenter : Entity
{
    public required string Code { get; set; }
    public required string NormalizedCode { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}
