using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Represents a physical stock-keeping location (warehouse/store room/counter) within a branch.
/// A branch can have multiple godowns; exactly one active godown per branch should be marked default.
/// </summary>
public class Godown : Entity
{
    /// <summary>
    /// Foreign key to the branch this godown belongs to.
    /// </summary>
    public Guid BranchId { get; set; }

    /// <summary>
    /// Navigation property to the branch.
    /// </summary>
    public Branch? Branch { get; set; }

    /// <summary>
    /// Godown code, unique within the branch.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>Normalized code used for case-insensitive uniqueness checks within the branch.</summary>
    public string NormalizedCode { get; set; } = string.Empty;

    /// <summary>
    /// Godown name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Optional free-form description/notes.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this is the branch's default godown (used when a transaction does not specify one explicitly).
    /// Exactly one active default is expected per branch.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Whether the godown is currently active/selectable for new transactions.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public ICollection<ProductBatch> ProductBatches { get; set; } = new List<ProductBatch>();
    public ICollection<Inventory> Inventory { get; set; } = new List<Inventory>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<UserGodown> UserGodowns { get; set; } = new List<UserGodown>();
}
