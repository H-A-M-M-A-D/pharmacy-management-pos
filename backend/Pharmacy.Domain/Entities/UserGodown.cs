using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Maps a user to a godown they are authorized to transact against.
/// A user with no rows here (and no branch-selection privilege) has no authorized godown.
/// </summary>
public class UserGodown : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid GodownId { get; set; }
    public Godown? Godown { get; set; }

    /// <summary>
    /// Whether this is the user's default godown when more than one is assigned.
    /// </summary>
    public bool IsDefault { get; set; }
}
