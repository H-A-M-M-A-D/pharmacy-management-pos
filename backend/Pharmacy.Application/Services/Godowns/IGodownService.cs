using Pharmacy.Application.DTOs.Godowns;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.Godowns;

public interface IGodownService
{
    Task<PagedResult<GodownListItemDto>> ListGodownsAsync(Guid actorId, GodownListQuery query, CancellationToken cancellationToken = default);
    Task<GodownDetailsDto> GetGodownAsync(Guid actorId, Guid godownId, CancellationToken cancellationToken = default);
    Task<GodownDetailsDto> CreateGodownAsync(Guid actorId, GodownRequest request, CancellationToken cancellationToken = default);
    Task<GodownDetailsDto> UpdateGodownAsync(Guid actorId, Guid godownId, GodownUpdateRequest request, CancellationToken cancellationToken = default);
    Task SetGodownActiveAsync(Guid actorId, Guid godownId, bool active, CancellationToken cancellationToken = default);
    Task<GodownDetailsDto> SetDefaultGodownAsync(Guid actorId, Guid godownId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GodownLookupDto>> LookupGodownsAsync(Guid actorId, Guid? branchId, bool activeOnly, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GodownLookupDto>> GetMyGodownsAsync(Guid actorId, Guid? branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserGodownDto>> ListAssignedUsersAsync(Guid actorId, Guid godownId, CancellationToken cancellationToken = default);
    Task AssignUserAsync(Guid actorId, AssignUserGodownRequest request, CancellationToken cancellationToken = default);
    Task UnassignUserAsync(Guid actorId, Guid userId, Guid godownId, CancellationToken cancellationToken = default);
    Task SetUserDefaultGodownAsync(Guid actorId, Guid userId, Guid godownId, CancellationToken cancellationToken = default);
}
