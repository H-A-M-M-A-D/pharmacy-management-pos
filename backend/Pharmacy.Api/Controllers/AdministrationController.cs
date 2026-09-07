using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Administration;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Administration;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdministrationController(IAdministrationService service) : ControllerBase
{
    [HttpGet("audit")][HasPermission(PermissionCatalog.AuditView)]
    public Task<PagedAuditDto> Audit([FromQuery] AuditQuery query,CancellationToken ct)=>service.ListAuditAsync(UserId,query,ct);
    [HttpGet("audit/{id:guid}")][HasPermission(PermissionCatalog.AuditView)]
    public Task<AuditDetailsDto> AuditDetails(Guid id,CancellationToken ct)=>service.GetAuditAsync(UserId,id,ct);
    [HttpGet("recycle-bin")][HasPermission(PermissionCatalog.RecycleBinView)]
    public Task<IReadOnlyList<RecycleBinItemDto>> RecycleBin(CancellationToken ct)=>service.ListDeletedAsync(UserId,ct);
    [HttpPost("recycle-bin/{entityType}/{id:guid}/delete")][HasPermission(PermissionCatalog.RecycleBinRestore)]
    public async Task<IActionResult> Delete(string entityType,Guid id,CancellationToken ct){await service.SoftDeleteAsync(UserId,entityType,id,ct);return NoContent();}
    [HttpPost("recycle-bin/{entityType}/{id:guid}/restore")][HasPermission(PermissionCatalog.RecycleBinRestore)]
    public async Task<IActionResult> Restore(string entityType,Guid id,CancellationToken ct){await service.RestoreAsync(UserId,entityType,id,ct);return NoContent();}
    [HttpGet("branches")][HasPermission(PermissionCatalog.BranchesView)]
    public Task<IReadOnlyList<BranchAdminDto>> Branches(CancellationToken ct)=>service.ListBranchesAsync(UserId,ct);
    [HttpPost("branches")][HasPermission(PermissionCatalog.BranchesManage)]
    public async Task<ActionResult<BranchAdminDto>> CreateBranch(SaveBranchRequest request,CancellationToken ct){var b=await service.CreateBranchAsync(UserId,request,ct);return CreatedAtAction(nameof(Branches),b);}
    [HttpPut("branches/{id:guid}")][HasPermission(PermissionCatalog.BranchesManage)]
    public Task<BranchAdminDto> UpdateBranch(Guid id,SaveBranchRequest request,CancellationToken ct)=>service.UpdateBranchAsync(UserId,id,request,ct);
    [HttpPost("branches/{id:guid}/activate")][HasPermission(PermissionCatalog.BranchesManage)]
    public async Task<IActionResult> ActivateBranch(Guid id,CancellationToken ct){await service.SetBranchActiveAsync(UserId,id,true,ct);return NoContent();}
    [HttpPost("branches/{id:guid}/deactivate")][HasPermission(PermissionCatalog.BranchesManage)]
    public async Task<IActionResult> DeactivateBranch(Guid id,CancellationToken ct){await service.SetBranchActiveAsync(UserId,id,false,ct);return NoContent();}
    [HttpGet("settings")][HasPermission(PermissionCatalog.SystemView)]
    public Task<SystemSettingsDto> Settings(CancellationToken ct)=>service.GetSettingsAsync(UserId,ct);
    [HttpPut("settings")][HasPermission(PermissionCatalog.SystemSettingsManage)]
    public Task<SystemSettingsDto> UpdateSettings(UpdateSystemSettingsRequest request,CancellationToken ct)=>service.UpdateSettingsAsync(UserId,request,ct);
    [HttpGet("backups")][HasPermission(PermissionCatalog.SystemBackup)]
    public Task<IReadOnlyList<BackupRecordDto>> Backups(CancellationToken ct)=>service.ListBackupsAsync(UserId,ct);
    [HttpPost("backups")][HasPermission(PermissionCatalog.SystemBackup)]
    public Task<BackupRecordDto> Backup(CancellationToken ct)=>service.CreateBackupAsync(UserId,ct);
    [HttpGet("system-info")][HasPermission(PermissionCatalog.SystemView)]
    public Task<SystemInformationDto> SystemInfo(CancellationToken ct)=>service.GetSystemInformationAsync(UserId,ct);
    [HttpPost("sessions/revoke")]
    public async Task<IActionResult> RevokeSessions(CancellationToken ct){await service.SignOutEverywhereAsync(UserId,ct);return NoContent();}
    private Guid UserId=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:throw new UnauthorizedAccessException();
}
