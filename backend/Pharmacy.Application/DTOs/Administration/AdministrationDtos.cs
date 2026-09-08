namespace Pharmacy.Application.DTOs.Administration;

public sealed record AuditQuery(DateTime? FromUtc = null, DateTime? ToUtc = null, Guid? UserId = null,
    string? Action = null, string? EntityType = null, Guid? EntityId = null, Guid? BranchId = null,
    string? Search = null, int Page = 1, int PageSize = 50);
public sealed record AuditItemDto(Guid Id, DateTime CreatedAt, Guid UserId, string Username, Guid BranchId,
    string BranchName, string Action, string EntityType, Guid EntityId);
public sealed record AuditDetailsDto(Guid Id, DateTime CreatedAt, Guid UserId, string Username, Guid BranchId,
    string BranchName, string Action, string EntityType, Guid EntityId, string? OldValues, string? NewValues,
    string? IPAddress, string? UserAgent);
public sealed record PagedAuditDto(IReadOnlyList<AuditItemDto> Items, int Page, int PageSize, int TotalCount);

public sealed record RecycleBinItemDto(string EntityType, Guid Id, string Name, DateTime DeletedAtUtc,
    Guid? DeletedByUserId);
public sealed record BranchAdminDto(Guid Id, string Code, string Name, string? Address, string? City,
    string? PhoneNumber, string? Email, bool IsHeadOffice, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record SaveBranchRequest(string Code, string Name, string? Address, string? City,
    string? PhoneNumber, string? Email, bool IsHeadOffice);
public sealed record SystemSettingsDto(string BusinessName, string? ReceiptHeader, string? ReceiptAddress,
    string? ReceiptPhone, string? ReceiptEmail, string? ReceiptFooter, bool ShowCustomerPhone,
    string TimeZone, string Currency, int Version);
public sealed record UpdateSystemSettingsRequest(string BusinessName, string? ReceiptHeader, string? ReceiptAddress,
    string? ReceiptPhone, string? ReceiptEmail, string? ReceiptFooter, bool ShowCustomerPhone, int ExpectedVersion);
public sealed record BackupRecordDto(Guid Id, string FileName, long SizeBytes, string Status,
    DateTime CreatedAt, DateTime? CompletedAtUtc, string? ErrorMessage);
public sealed record SystemInformationDto(string Status, string DatabaseProvider, string DatabaseVersion,
    string LatestMigration, bool CanConnect, string TimeZone, string Currency, string ApiVersion,
    bool BackupToolAvailable, bool BackupDirectoryWritable, DateTime? LastSuccessfulBackupUtc);
