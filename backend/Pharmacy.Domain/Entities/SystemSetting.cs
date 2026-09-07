using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

public sealed class SystemSetting : Entity
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public int Version { get; set; } = 1;
    public Guid UpdatedByUserId { get; set; }
}
