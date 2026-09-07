using Pharmacy.Application.DTOs.Administration;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Administration;

public interface IAdministrationRepository
{
    Task<User?> GetActorAsync(Guid id, CancellationToken ct);
    Task<PagedAuditDto> ListAuditAsync(AuditQuery query, CancellationToken ct);
    Task<AuditDetailsDto?> GetAuditAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<RecycleBinItemDto>> ListDeletedAsync(CancellationToken ct);
    Task<ISoftDeletable?> GetRecyclableAsync(string entityType, Guid id, bool includeDeleted, CancellationToken ct);
    Task<bool> HasReferencesAsync(string entityType, Guid id, CancellationToken ct);
    Task<IReadOnlyList<BranchAdminDto>> ListBranchesAsync(CancellationToken ct);
    Task<Branch?> GetBranchAsync(Guid id, CancellationToken ct);
    Task<bool> BranchCodeExistsAsync(string normalizedCode, Guid? exceptId, CancellationToken ct);
    Task<int> ActiveBranchCountAsync(CancellationToken ct);
    Task<bool> HasActiveUsersAsync(Guid branchId, CancellationToken ct);
    Task AddBranchAsync(Branch branch, CancellationToken ct);
    Task<IReadOnlyList<SystemSetting>> GetSettingsAsync(CancellationToken ct);
    Task AddSettingAsync(SystemSetting setting, CancellationToken ct);
    Task<User?> GetUserAsync(Guid id, CancellationToken ct);
    Task AddAuditAsync(AuditLog audit, CancellationToken ct);
    Task<IReadOnlyList<BackupRecordDto>> ListBackupsAsync(CancellationToken ct);
    Task<BackupRecord> CreateBackupAsync(Guid actorId, CancellationToken ct);
    Task<SystemInformationDto> GetSystemInformationAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
