using System.Diagnostics;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.DTOs.Administration;
using Pharmacy.Application.Services.Administration;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class AdministrationRepository(
    PharmacyDbContext context,
    IConfiguration configuration,
    ILogger<AdministrationRepository> logger) : IAdministrationRepository
{
    public Task<User?> GetActorAsync(Guid id,CancellationToken ct)=>context.Users.Include(x=>x.Role).ThenInclude(x=>x!.RolePermissions).ThenInclude(x=>x.Permission).FirstOrDefaultAsync(x=>x.Id==id,ct);
    public async Task<PagedAuditDto> ListAuditAsync(AuditQuery q,CancellationToken ct){var rows=context.AuditLogs.AsNoTracking().AsQueryable();if(q.FromUtc.HasValue)rows=rows.Where(x=>x.CreatedAt>=q.FromUtc);if(q.ToUtc.HasValue)rows=rows.Where(x=>x.CreatedAt<=q.ToUtc);if(q.UserId.HasValue)rows=rows.Where(x=>x.UserId==q.UserId);if(q.BranchId.HasValue)rows=rows.Where(x=>x.User!.BranchId==q.BranchId);if(!string.IsNullOrWhiteSpace(q.Action))rows=rows.Where(x=>x.Action==q.Action);if(!string.IsNullOrWhiteSpace(q.EntityType))rows=rows.Where(x=>x.EntityType==q.EntityType);if(q.EntityId.HasValue)rows=rows.Where(x=>x.EntityId==q.EntityId);if(!string.IsNullOrWhiteSpace(q.Search)){var p=$"%{q.Search.Trim()}%";rows=rows.Where(x=>EF.Functions.ILike(x.Action,p)||EF.Functions.ILike(x.EntityType,p)||(x.NewValues!=null&&EF.Functions.ILike(x.NewValues,p)));}var count=await rows.CountAsync(ct);var items=await rows.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Skip((q.Page-1)*q.PageSize).Take(q.PageSize).Select(x=>new AuditItemDto(x.Id,x.CreatedAt,x.UserId,x.User!.Username,x.User.BranchId,x.User.Branch!.Name,x.Action,x.EntityType,x.EntityId)).ToListAsync(ct);return new(items,q.Page,q.PageSize,count);}
    public Task<AuditDetailsDto?> GetAuditAsync(Guid id,CancellationToken ct)=>context.AuditLogs.AsNoTracking().Where(x=>x.Id==id).Select(x=>new AuditDetailsDto(x.Id,x.CreatedAt,x.UserId,x.User!.Username,x.User.BranchId,x.User.Branch!.Name,x.Action,x.EntityType,x.EntityId,x.OldValues,x.NewValues,x.IPAddress,x.UserAgent)).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<RecycleBinItemDto>> ListDeletedAsync(CancellationToken ct){var a=await context.ProductCategories.IgnoreQueryFilters().Where(x=>x.IsDeleted).Select(x=>new RecycleBinItemDto("ProductCategory",x.Id,x.Name,x.DeletedAtUtc!.Value,x.DeletedByUserId)).ToListAsync(ct);a.AddRange(await context.Manufacturers.IgnoreQueryFilters().Where(x=>x.IsDeleted).Select(x=>new RecycleBinItemDto("Manufacturer",x.Id,x.Name,x.DeletedAtUtc!.Value,x.DeletedByUserId)).ToListAsync(ct));a.AddRange(await context.ExpenseCategories.IgnoreQueryFilters().Where(x=>x.IsDeleted).Select(x=>new RecycleBinItemDto("ExpenseCategory",x.Id,x.Name,x.DeletedAtUtc!.Value,x.DeletedByUserId)).ToListAsync(ct));return a.OrderByDescending(x=>x.DeletedAtUtc).ThenBy(x=>x.EntityType).ThenBy(x=>x.Name).ToList();}
    public async Task<ISoftDeletable?> GetRecyclableAsync(string type,Guid id,bool include,CancellationToken ct)=>type.ToUpperInvariant() switch{"PRODUCTCATEGORY"=>await (include?context.ProductCategories.IgnoreQueryFilters():context.ProductCategories).FirstOrDefaultAsync(x=>x.Id==id,ct),"MANUFACTURER"=>await (include?context.Manufacturers.IgnoreQueryFilters():context.Manufacturers).FirstOrDefaultAsync(x=>x.Id==id,ct),"EXPENSECATEGORY"=>await (include?context.ExpenseCategories.IgnoreQueryFilters():context.ExpenseCategories).FirstOrDefaultAsync(x=>x.Id==id,ct),_=>null};
    public Task<bool> HasReferencesAsync(string type,Guid id,CancellationToken ct)=>type.ToUpperInvariant() switch{"PRODUCTCATEGORY"=>context.Products.AnyAsync(x=>x.CategoryId==id,ct),"MANUFACTURER"=>context.Products.AnyAsync(x=>x.ManufacturerId==id,ct),"EXPENSECATEGORY"=>context.Expenses.AnyAsync(x=>x.ExpenseCategoryId==id,ct),_=>Task.FromResult(true)};
    public async Task<IReadOnlyList<BranchAdminDto>> ListBranchesAsync(CancellationToken ct)=>await context.Branches.AsNoTracking().OrderBy(x=>x.Name).Select(x=>new BranchAdminDto(x.Id,x.Code,x.Name,x.Address,x.City,x.PhoneNumber,x.Email,x.IsHeadOffice,x.IsActive,x.CreatedAt,x.UpdatedAt)).ToListAsync(ct);
    public Task<Branch?> GetBranchAsync(Guid id,CancellationToken ct)=>context.Branches.FirstOrDefaultAsync(x=>x.Id==id,ct);
    public Task<bool> BranchCodeExistsAsync(string code,Guid? except,CancellationToken ct)=>context.Branches.AnyAsync(x=>x.NormalizedCode==code&&(!except.HasValue||x.Id!=except),ct);
    public Task<int> ActiveBranchCountAsync(CancellationToken ct)=>context.Branches.CountAsync(x=>x.IsActive,ct);
    public Task<bool> HasActiveUsersAsync(Guid id,CancellationToken ct)=>context.Users.AnyAsync(x=>x.BranchId==id&&x.IsActive,ct);
    public async Task AddBranchAsync(Branch b,CancellationToken ct)=>await context.Branches.AddAsync(b,ct);
    public async Task<IReadOnlyList<SystemSetting>> GetSettingsAsync(CancellationToken ct)=>await context.SystemSettings.OrderBy(x=>x.Key).ToListAsync(ct);
    public async Task AddSettingAsync(SystemSetting s,CancellationToken ct)=>await context.SystemSettings.AddAsync(s,ct);
    public Task<User?> GetUserAsync(Guid id,CancellationToken ct)=>context.Users.FirstOrDefaultAsync(x=>x.Id==id,ct);
    public async Task AddAuditAsync(AuditLog a,CancellationToken ct)=>await context.AuditLogs.AddAsync(a,ct);
    public async Task<IReadOnlyList<BackupRecordDto>> ListBackupsAsync(CancellationToken ct)=>await context.BackupRecords.AsNoTracking().OrderByDescending(x=>x.CreatedAt).Select(x=>new BackupRecordDto(x.Id,x.FileName,x.SizeBytes,x.Status,x.CreatedAt,x.CompletedAtUtc,x.ErrorMessage)).ToListAsync(ct);
    public async Task<BackupRecord> CreateBackupAsync(Guid actor,CancellationToken ct)
    {
        var cs=new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
        var pgDump=FindPostgresTool("pg_dump.exe")??throw new InvalidOperationException("pg_dump was not found. Configure Backup:PostgreSqlToolsPath or install PostgreSQL client tools.");
        var pgRestore=FindPostgresTool("pg_restore.exe")??throw new InvalidOperationException("pg_restore was not found. Archive validation cannot run.");
        var root=GetBackupRoot();
        Directory.CreateDirectory(root);
        EnsureDiskSpace(root);
        var file=$"pharmacy-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.backup";
        var finalPath=SafePath(root,file);
        var partialPath=SafePath(root,file+".partial");
        var record=new BackupRecord{FileName=file,Status="InProgress",RequestedByUserId=actor};
        context.BackupRecords.Add(record);
        await context.SaveChangesAsync(ct);
        try
        {
            var dump=CreateToolStartInfo(pgDump,cs);
            dump.ArgumentList.Add("--format=custom");dump.ArgumentList.Add("--no-password");dump.ArgumentList.Add("--file");dump.ArgumentList.Add(partialPath);dump.ArgumentList.Add(cs.Database!);
            await RunToolAsync(dump,"pg_dump",ct);
            if(!File.Exists(partialPath)||new FileInfo(partialPath).Length==0)throw new InvalidOperationException("Backup archive validation failed because the output was empty.");
            var validate=CreateToolStartInfo(pgRestore,cs);validate.ArgumentList.Add("--list");validate.ArgumentList.Add(partialPath);
            await RunToolAsync(validate,"pg_restore archive validation",ct);
            File.Move(partialPath,finalPath);
            record.Status="Completed";record.SizeBytes=new FileInfo(finalPath).Length;record.CompletedAtUtc=DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
            await ApplyRetention(root,ct);
            return record;
        }
        catch(Exception exception) when(exception is not OperationCanceledException)
        {
            if(File.Exists(partialPath))File.Delete(partialPath);
            var detail=exception.Message;
            record.Status="Failed";record.ErrorMessage=detail.Length>500?detail[..500]:detail;record.CompletedAtUtc=DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken.None);
            logger.LogError(exception,"Database backup failed. BackupRecordId: {BackupRecordId}",record.Id);
            throw new InvalidOperationException("Backup creation or validation failed. See the backup record and server log for details.");
        }
        finally
        {
            cs.Password=string.Empty;
        }
    }
    public async Task<SystemInformationDto> GetSystemInformationAsync(CancellationToken ct){var ok=await context.Database.CanConnectAsync(ct);var version=ok?(await context.Database.SqlQueryRaw<string>("SELECT version() AS \"Value\"").FirstAsync(ct)):"Unavailable";var migration=(await context.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault()??"None";var root=GetBackupRoot();var writable=false;try{Directory.CreateDirectory(root);var probe=SafePath(root,$".write-{Guid.NewGuid():N}.tmp");await File.WriteAllTextAsync(probe,string.Empty,ct);File.Delete(probe);writable=true;}catch(Exception exception) when(exception is IOException or UnauthorizedAccessException){logger.LogWarning("Backup directory write probe failed: {ErrorType}",exception.GetType().Name);}var apiVersion=Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??"0.1.0-rc.1";var last=await context.BackupRecords.Where(x=>x.Status=="Completed").MaxAsync(x=>(DateTime?)x.CompletedAtUtc,ct);return new(ok?"healthy":"unhealthy","PostgreSQL",version,migration,ok,"Asia/Karachi","PKR",apiVersion,FindPostgresTool("pg_dump.exe") is not null&&FindPostgresTool("pg_restore.exe") is not null,writable,last);}
    public Task SaveAsync(CancellationToken ct)=>context.SaveChangesAsync(ct);
    private string GetBackupRoot()=>Path.GetFullPath(configuration["Backup:Directory"]??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PharmacyPOS","Backups"));
    private string? FindPostgresTool(string name)
    {
        var configured=configuration["Backup:PostgreSqlToolsPath"];
        if(!string.IsNullOrWhiteSpace(configured)){var candidate=Path.Combine(configured,name);if(File.Exists(candidate))return candidate;}
        var path=(Environment.GetEnvironmentVariable("PATH")??string.Empty).Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries).Select(x=>Path.Combine(x.Trim(),name)).FirstOrDefault(File.Exists);if(path is not null)return path;
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PostgreSQL");
        if(!Directory.Exists(root))return null;
        return Directory.EnumerateDirectories(root).OrderByDescending(x=>x).Select(x=>Path.Combine(x,"bin",name)).FirstOrDefault(File.Exists);
    }
    private static string SafePath(string root,string name){var canonicalRoot=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;var path=Path.GetFullPath(Path.Combine(canonicalRoot,name));if(!path.StartsWith(canonicalRoot,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Backup path escaped the configured directory.");return path;}
    private void EnsureDiskSpace(string root){var drive=new DriveInfo(Path.GetPathRoot(root)!);var minimum=long.TryParse(configuration["Backup:MinimumFreeSpaceBytes"],out var value)?value:104857600;if(drive.AvailableFreeSpace<minimum)throw new InvalidOperationException("Insufficient free disk space for backup.");}
    private ProcessStartInfo CreateToolStartInfo(string executable,NpgsqlConnectionStringBuilder cs){var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};start.ArgumentList.Add("--host");start.ArgumentList.Add(cs.Host!);start.ArgumentList.Add("--port");start.ArgumentList.Add(cs.Port.ToString());start.ArgumentList.Add("--username");start.ArgumentList.Add(cs.Username!);start.Environment["PGPASSWORD"]=cs.Password;return start;}
    private static async Task RunToolAsync(ProcessStartInfo start,string operation,CancellationToken ct){using var process=Process.Start(start)??throw new InvalidOperationException($"Could not start {operation}.");var stderrTask=process.StandardError.ReadToEndAsync(ct);var stdoutTask=process.StandardOutput.ReadToEndAsync(ct);await process.WaitForExitAsync(ct);await stdoutTask;var stderr=await stderrTask;if(process.ExitCode!=0){var detail=stderr.Length>500?stderr[..500]:stderr;throw new InvalidOperationException($"{operation} failed with exit code {process.ExitCode}.{(string.IsNullOrWhiteSpace(detail)?"":" "+detail.Trim())}");}}
    private async Task ApplyRetention(string root,CancellationToken ct){var configured=int.TryParse(configuration["Backup:RetentionCount"],out var value)?value:10;var retain=Math.Clamp(configured,1,100);var completed=context.BackupRecords.Where(x=>x.Status=="Completed").OrderByDescending(x=>x.CompletedAtUtc).Skip(retain).ToList();if(completed.Count==0)return;foreach(var item in completed){var path=SafePath(root,item.FileName);if(File.Exists(path))File.Delete(path);item.Status="Purged";}await context.SaveChangesAsync(ct);}
}
