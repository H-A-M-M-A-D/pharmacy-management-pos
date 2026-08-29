using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Represents a pharmacy branch/store location.
/// </summary>
public class Branch : Entity
{
    /// <summary>
    /// Unique branch code.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Branch name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Branch address.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// City/District.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Contact phone number.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Contact email.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Indicates if this is the main/head office branch.
    /// </summary>
    public bool IsHeadOffice { get; set; }

    /// <summary>
    /// Whether the branch is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Navigation property for branch managers/staff.
    /// </summary>
    public ICollection<User> Users { get; set; } = new List<User>();

    /// <summary>
    /// Navigation property for inventory in this branch.
    /// </summary>
    public ICollection<Inventory> Inventory { get; set; } = new List<Inventory>();

    /// <summary>
    /// Navigation property for product batches stored in this branch.
    /// </summary>
    public ICollection<ProductBatch> ProductBatches { get; set; } = new List<ProductBatch>();

    /// <summary>
    /// Navigation property for stock movements in this branch.
    /// </summary>
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
