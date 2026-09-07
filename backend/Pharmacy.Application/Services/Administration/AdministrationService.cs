using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Administration;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Administration;

public sealed class AdministrationService(IAdministrationRepository repository, TimeProvider clock) : IAdministrationService
{
    private static readonly HashSet<string> Recyclable = new(StringComparer.OrdinalIgnoreCase)
        { "ProductCategory", "Manufacturer", "ExpenseCategory" };

    public async Task<PagedAuditDto> ListAuditAsync(Guid actorId, AuditQuery query, CancellationToken ct = default)
    {
        await RequireAsync(actorId, PermissionCatalog.AuditView, ct);
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || query.FromUtc > query.ToUtc)
            throw new RequestValidationException("Invalid audit query range or pagination.");
        return await repository.ListAuditAsync(query, ct);
    }

    public async Task<AuditDetailsDto> GetAuditAsync(Guid actorId, Guid id, CancellationToken ct = default)
    {
        await RequireAsync(actorId, PermissionCatalog.AuditView, ct);
        return await repository.GetAuditAsync(id, ct) ?? throw new ResourceNotFoundException("Audit event was not found.");
    }

    public async Task<IReadOnlyList<RecycleBinItemDto>> ListDeletedAsync(Guid actorId, CancellationToken ct = default)
    {
        await RequireAsync(actorId, PermissionCatalog.RecycleBinView, ct);
        return await repository.ListDeletedAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid actorId, string entityType, Guid id, CancellationToken ct = default)
    {
        await RequireAsync(actorId, PermissionCatalog.RecycleBinRestore, ct);
        EnsureRecyclable(entityType);
        var entity = await repository.GetRecyclableAsync(entityType, id, false, ct)
            ?? throw new ResourceNotFoundException("Reference item was not found.");
        if (await repository.HasReferencesAsync(entityType, id, ct))
            throw new ResourceConflictException("Referenced items cannot be moved to the recycle bin; deactivate them instead.");
        entity.IsDeleted = true;
        entity.DeletedAtUtc = clock.GetUtcNow().UtcDateTime;
        entity.DeletedByUserId = actorId;
        await AuditAsync(actorId, "ReferenceSoftDeleted", entityType, id, new { entityType, id }, ct);
        await repository.SaveAsync(ct);
    }

    public async Task RestoreAsync(Guid actorId, string entityType, Guid id, CancellationToken ct = default)
    {
        await RequireAsync(actorId, PermissionCatalog.RecycleBinRestore, ct);
        EnsureRecyclable(entityType);
        var entity = await repository.GetRecyclableAsync(entityType, id, true, ct)
            ?? throw new ResourceNotFoundException("Deleted reference item was not found.");
        if (!entity.IsDeleted) throw new ResourceConflictException("Reference item is not deleted.");
        entity.IsDeleted = false;
        entity.DeletedAtUtc = null;
        entity.DeletedByUserId = null;
        await AuditAsync(actorId, "ReferenceRestored", entityType, id, new { entityType, id }, ct);
        await repository.SaveAsync(ct);
    }

    public async Task<IReadOnlyList<BranchAdminDto>> ListBranchesAsync(Guid actorId, CancellationToken ct = default)
    { await RequireAsync(actorId, PermissionCatalog.BranchesView, ct); return await repository.ListBranchesAsync(ct); }

    public async Task<BranchAdminDto> CreateBranchAsync(Guid actorId, SaveBranchRequest request, CancellationToken ct = default)
    {
        await RequireAsync(actorId, PermissionCatalog.BranchesManage, ct);
        ValidateBranch(request);
        var normalized = NormalizeCode(request.Code);
        if (await repository.BranchCodeExistsAsync(normalized, null, ct)) throw new ResourceConflictException("Branch code is already in use.");
        var branch = new Branch { Code = request.Code.Trim(), NormalizedCode = normalized, Name = request.Name.Trim(), Address = Clean(request.Address), City = Clean(request.City), PhoneNumber = Clean(request.PhoneNumber), Email = Clean(request.Email), IsHeadOffice = request.IsHeadOffice };
        await repository.AddBranchAsync(branch, ct);
        await AuditAsync(actorId, "BranchCreated", "Branch", branch.Id, new { branch.Code, branch.Name }, ct);
        await repository.SaveAsync(ct);
        return Map(branch);
    }

    public async Task<BranchAdminDto> UpdateBranchAsync(Guid actorId, Guid id, SaveBranchRequest request, CancellationToken ct = default)
    {
        await RequireAsync(actorId, PermissionCatalog.BranchesManage, ct); ValidateBranch(request);
        var branch = await repository.GetBranchAsync(id, ct) ?? throw new ResourceNotFoundException("Branch was not found.");
        var normalized = NormalizeCode(request.Code);
        if (await repository.BranchCodeExistsAsync(normalized, id, ct)) throw new ResourceConflictException("Branch code is already in use.");
        var old = new { branch.Code, branch.Name, branch.Address, branch.City, branch.PhoneNumber, branch.Email, branch.IsHeadOffice };
        branch.Code=request.Code.Trim(); branch.NormalizedCode=normalized; branch.Name=request.Name.Trim(); branch.Address=Clean(request.Address); branch.City=Clean(request.City); branch.PhoneNumber=Clean(request.PhoneNumber); branch.Email=Clean(request.Email); branch.IsHeadOffice=request.IsHeadOffice; branch.UpdatedAt=clock.GetUtcNow().UtcDateTime;
        await AuditAsync(actorId,"BranchUpdated","Branch",id,new { Old=old, New=new { branch.Code,branch.Name,branch.Address,branch.City,branch.PhoneNumber,branch.Email,branch.IsHeadOffice }},ct);
        await repository.SaveAsync(ct); return Map(branch);
    }

    public async Task SetBranchActiveAsync(Guid actorId, Guid id, bool active, CancellationToken ct = default)
    {
        await RequireAsync(actorId, PermissionCatalog.BranchesManage, ct);
        var branch=await repository.GetBranchAsync(id,ct) ?? throw new ResourceNotFoundException("Branch was not found.");
        if (!active && branch.IsActive) {
            if (await repository.ActiveBranchCountAsync(ct)<=1) throw new ResourceConflictException("The last active branch cannot be deactivated.");
            if (await repository.HasActiveUsersAsync(id,ct)) throw new ResourceConflictException("A branch with active assigned users cannot be deactivated.");
        }
        branch.IsActive=active; branch.UpdatedAt=clock.GetUtcNow().UtcDateTime;
        await AuditAsync(actorId,active?"BranchActivated":"BranchDeactivated","Branch",id,new { branch.IsActive },ct); await repository.SaveAsync(ct);
    }

    public async Task<SystemSettingsDto> GetSettingsAsync(Guid actorId, CancellationToken ct = default)
    { await RequireAsync(actorId,PermissionCatalog.SystemView,ct); return MapSettings(await repository.GetSettingsAsync(ct)); }

    public async Task<SystemSettingsDto> UpdateSettingsAsync(Guid actorId, UpdateSystemSettingsRequest request, CancellationToken ct = default)
    {
        await RequireAsync(actorId,PermissionCatalog.SystemSettingsManage,ct);
        if (string.IsNullOrWhiteSpace(request.BusinessName) || request.BusinessName.Trim().Length>200) throw new RequestValidationException("Business name is required and cannot exceed 200 characters.");
        var existing=(await repository.GetSettingsAsync(ct)).ToDictionary(x=>x.Key);
        var currentVersion=existing.Count==0?0:existing.Values.Max(x=>x.Version);
        if(request.ExpectedVersion!=currentVersion) throw new ResourceConflictException("Settings changed since they were loaded.");
        var values=new Dictionary<string,string>{["business.name"]=request.BusinessName.Trim(),["receipt.header"]=Clean(request.ReceiptHeader)??"",["receipt.address"]=Clean(request.ReceiptAddress)??"",["receipt.phone"]=Clean(request.ReceiptPhone)??"",["receipt.email"]=Clean(request.ReceiptEmail)??"",["receipt.footer"]=Clean(request.ReceiptFooter)??"",["receipt.show_customer_phone"]=request.ShowCustomerPhone.ToString()};
        foreach(var pair in values){if(!existing.TryGetValue(pair.Key,out var setting)){setting=new SystemSetting{Key=pair.Key,Value=pair.Value,UpdatedByUserId=actorId,Version=currentVersion+1};await repository.AddSettingAsync(setting,ct);}else{setting.Value=pair.Value;setting.Version=currentVersion+1;setting.UpdatedByUserId=actorId;setting.UpdatedAt=clock.GetUtcNow().UtcDateTime;}}
        await AuditAsync(actorId,"SystemSettingsUpdated","SystemSettings",Guid.Empty,new { ChangedKeys=values.Keys },ct); await repository.SaveAsync(ct); return MapSettings(await repository.GetSettingsAsync(ct));
    }

    public async Task SignOutEverywhereAsync(Guid actorId, CancellationToken ct = default)
    { var user=await RequireAsync(actorId,PermissionCatalog.ProfileView,ct); user.TokenVersion++; user.UpdatedAt=clock.GetUtcNow().UtcDateTime; await AuditAsync(actorId,"SessionsRevoked","User",actorId,new { user.TokenVersion },ct); await repository.SaveAsync(ct); }
    public async Task<IReadOnlyList<BackupRecordDto>> ListBackupsAsync(Guid actorId,CancellationToken ct=default){await RequireAsync(actorId,PermissionCatalog.SystemBackup,ct);return await repository.ListBackupsAsync(ct);}
    public async Task<BackupRecordDto> CreateBackupAsync(Guid actorId,CancellationToken ct=default){await RequireAsync(actorId,PermissionCatalog.SystemBackup,ct);var b=await repository.CreateBackupAsync(actorId,ct);await AuditAsync(actorId,"DatabaseBackupCreated","BackupRecord",b.Id,new { b.FileName,b.SizeBytes,b.Status },ct);await repository.SaveAsync(ct);return new(b.Id,b.FileName,b.SizeBytes,b.Status,b.CreatedAt,b.CompletedAtUtc,b.ErrorMessage);}
    public async Task<SystemInformationDto> GetSystemInformationAsync(Guid actorId,CancellationToken ct=default){await RequireAsync(actorId,PermissionCatalog.SystemView,ct);return await repository.GetSystemInformationAsync(ct);}

    private async Task<User> RequireAsync(Guid id,string permission,CancellationToken ct){var u=await repository.GetActorAsync(id,ct)??throw new ForbiddenOperationException("Access denied.");if(!u.IsActive||u.Role?.RolePermissions.Any(x=>x.Permission?.Code==permission)!=true)throw new ForbiddenOperationException("Access denied.");return u;}
    private Task AuditAsync(Guid actor,string action,string type,Guid id,object values,CancellationToken ct)=>repository.AddAuditAsync(new AuditLog{UserId=actor,Action=action,EntityType=type,EntityId=id,NewValues=JsonSerializer.Serialize(values)},ct);
    private static void EnsureRecyclable(string value){if(!Recyclable.Contains(value))throw new RequestValidationException("Entity type is not eligible for the recycle bin.");}
    private static string NormalizeCode(string code)=>code.Trim().ToUpperInvariant();
    private static string? Clean(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static void ValidateBranch(SaveBranchRequest r){if(string.IsNullOrWhiteSpace(r.Code)||r.Code.Trim().Length>50||string.IsNullOrWhiteSpace(r.Name)||r.Name.Trim().Length>200)throw new RequestValidationException("Branch code and name are required.");}
    private static BranchAdminDto Map(Branch b)=>new(b.Id,b.Code,b.Name,b.Address,b.City,b.PhoneNumber,b.Email,b.IsHeadOffice,b.IsActive,b.CreatedAt,b.UpdatedAt);
    private static SystemSettingsDto MapSettings(IReadOnlyList<SystemSetting> rows){var d=rows.ToDictionary(x=>x.Key,x=>x.Value);string? Get(string k)=>d.TryGetValue(k,out var v)&&v.Length>0?v:null;return new(Get("business.name")??"Pharmacy",Get("receipt.header"),Get("receipt.address"),Get("receipt.phone"),Get("receipt.email"),Get("receipt.footer"),bool.TryParse(Get("receipt.show_customer_phone"),out var show)&&show,"Asia/Karachi","PKR",rows.Count==0?0:rows.Max(x=>x.Version));}
}
