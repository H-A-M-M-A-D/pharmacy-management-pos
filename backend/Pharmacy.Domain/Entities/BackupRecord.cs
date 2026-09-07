using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public sealed class BackupRecord : Entity
{
    public required string FileName { get; set; }
    public long SizeBytes { get; set; }
    public required string Status { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
