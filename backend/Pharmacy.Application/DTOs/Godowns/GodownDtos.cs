namespace Pharmacy.Application.DTOs.Godowns;

public sealed record GodownRequest(Guid BranchId, string Code, string Name, string? Description, bool IsDefault, bool IsActive);

public sealed record GodownUpdateRequest(string Code, string Name, string? Description);

public sealed record GodownListQuery(int Page = 1, int PageSize = 25, Guid? BranchId = null, string? Search = null, bool? IsActive = null);

public sealed record GodownListItemDto(
    Guid Id, Guid BranchId, string BranchName, string Code, string Name, string? Description,
    bool IsDefault, bool IsActive, DateTime CreatedAt);

public sealed record GodownDetailsDto(
    Guid Id, Guid BranchId, string BranchName, string Code, string Name, string? Description,
    bool IsDefault, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record GodownLookupDto(Guid Id, Guid BranchId, string Code, string Name, bool IsDefault, bool IsActive);

public sealed record UserGodownDto(Guid UserId, string UserFullName, Guid GodownId, string GodownName, bool IsDefault);

public sealed record AssignUserGodownRequest(Guid UserId, Guid GodownId, bool IsDefault = false);
