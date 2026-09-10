using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Godowns;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Godowns;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/godowns")]
public sealed class GodownsController(IGodownService godowns) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.GodownsView)]
    public Task<PagedResult<GodownListItemDto>> List([FromQuery] GodownListQuery query, CancellationToken ct) =>
        godowns.ListGodownsAsync(UserId(), query, ct);

    [HttpGet("lookup"), HasPermission(PermissionCatalog.GodownsView)]
    public Task<IReadOnlyList<GodownLookupDto>> Lookup([FromQuery] Guid? branchId, [FromQuery] bool activeOnly = true, CancellationToken ct = default) =>
        godowns.LookupGodownsAsync(UserId(), branchId, activeOnly, ct);

    /// <summary>
    /// Godowns the current user is authorized to transact against. Used by transaction screens
    /// (POS, GRN, Direct Purchase, Purchase Return) to populate/auto-select the source or
    /// destination godown selector without trusting a client-supplied value alone.
    /// </summary>
    [HttpGet("mine")]
    public Task<IReadOnlyList<GodownLookupDto>> Mine([FromQuery] Guid? branchId, CancellationToken ct) =>
        godowns.GetMyGodownsAsync(UserId(), branchId, ct);

    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.GodownsView)]
    public Task<GodownDetailsDto> Get(Guid id, CancellationToken ct) => godowns.GetGodownAsync(UserId(), id, ct);

    [HttpPost, HasPermission(PermissionCatalog.GodownsCreate)]
    public async Task<ActionResult<GodownDetailsDto>> Create(GodownRequest request, CancellationToken ct)
    {
        var result = await godowns.CreateGodownAsync(UserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}"), HasPermission(PermissionCatalog.GodownsUpdate)]
    public Task<GodownDetailsDto> Update(Guid id, GodownUpdateRequest request, CancellationToken ct) =>
        godowns.UpdateGodownAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/activate"), HasPermission(PermissionCatalog.GodownsActivate)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await godowns.SetGodownActiveAsync(UserId(), id, true, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate"), HasPermission(PermissionCatalog.GodownsDeactivate)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await godowns.SetGodownActiveAsync(UserId(), id, false, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/set-default"), HasPermission(PermissionCatalog.GodownsSetDefault)]
    public Task<GodownDetailsDto> SetDefault(Guid id, CancellationToken ct) => godowns.SetDefaultGodownAsync(UserId(), id, ct);

    [HttpGet("{id:guid}/users"), HasPermission(PermissionCatalog.GodownsManage)]
    public Task<IReadOnlyList<UserGodownDto>> Users(Guid id, CancellationToken ct) => godowns.ListAssignedUsersAsync(UserId(), id, ct);

    [HttpPost("{id:guid}/users"), HasPermission(PermissionCatalog.GodownsManage)]
    public async Task<IActionResult> AssignUser(Guid id, AssignUserGodownRequest request, CancellationToken ct)
    {
        await godowns.AssignUserAsync(UserId(), request with { GodownId = id }, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/users/{userId:guid}"), HasPermission(PermissionCatalog.GodownsManage)]
    public async Task<IActionResult> UnassignUser(Guid id, Guid userId, CancellationToken ct)
    {
        await godowns.UnassignUserAsync(UserId(), userId, id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/users/{userId:guid}/default"), HasPermission(PermissionCatalog.GodownsManage)]
    public async Task<IActionResult> SetUserDefault(Guid id, Guid userId, CancellationToken ct)
    {
        await godowns.SetUserDefaultGodownAsync(UserId(), userId, id, ct);
        return NoContent();
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
