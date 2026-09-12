using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A short-lived marker recording that a specific actor already submitted an operation with this
/// exact content. Used only to reject an accidental double-click or client retry within a short
/// window; it is not a permanent audit record and carries no business meaning of its own.
/// </summary>
public sealed class SubmissionFingerprint : Entity
{
    public required string Operation { get; set; }
    public Guid FingerprintHash { get; set; }
    public Guid ActorId { get; set; }
}
