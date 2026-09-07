using Pharmacy.Application.DTOs.Administration;

namespace Pharmacy.Application.Services.Administration;

public interface IAdministrationService
{
    Task<PagedAuditDto> ListAuditAsync(Guid actorId, AuditQuery query, CancellationToken ct = default);
    Task<AuditDetailsDto> GetAuditAsync(Guid actorId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RecycleBinItemDto>> ListDeletedAsync(Guid actorId, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid actorId, string entityType, Guid id, CancellationToken ct = default);
    Task RestoreAsync(Guid actorId, string entityType, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BranchAdminDto>> ListBranchesAsync(Guid actorId, CancellationToken ct = default);
    Task<BranchAdminDto> CreateBranchAsync(Guid actorId, SaveBranchRequest request, CancellationToken ct = default);
    Task<BranchAdminDto> UpdateBranchAsync(Guid actorId, Guid id, SaveBranchRequest request, CancellationToken ct = default);
    Task SetBranchActiveAsync(Guid actorId, Guid id, bool active, CancellationToken ct = default);
    Task<SystemSettingsDto> GetSettingsAsync(Guid actorId, CancellationToken ct = default);
    Task<SystemSettingsDto> UpdateSettingsAsync(Guid actorId, UpdateSystemSettingsRequest request, CancellationToken ct = default);
    Task SignOutEverywhereAsync(Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<BackupRecordDto>> ListBackupsAsync(Guid actorId, CancellationToken ct = default);
    Task<BackupRecordDto> CreateBackupAsync(Guid actorId, CancellationToken ct = default);
    Task<SystemInformationDto> GetSystemInformationAsync(Guid actorId, CancellationToken ct = default);
}
