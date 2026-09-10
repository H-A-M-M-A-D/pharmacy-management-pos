using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Godowns;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Godowns;

public sealed class GodownService(IGodownRepository repository, TimeProvider timeProvider) : IGodownService
{
    public async Task<PagedResult<GodownListItemDto>> ListGodownsAsync(Guid actorId, GodownListQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.GodownsView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        var branchId = CanSelectBranch(actor) ? query.BranchId : actor.BranchId;
        return await repository.ListGodownsAsync(query with { BranchId = branchId }, cancellationToken);
    }

    public async Task<GodownDetailsDto> GetGodownAsync(Guid actorId, Guid godownId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.GodownsView, cancellationToken);
        var dto = await repository.GetGodownDetailsAsync(godownId, cancellationToken) ?? throw new ResourceNotFoundException("Godown was not found.");
        EnsureBranchAccess(actor, dto.BranchId);
        return dto;
    }

    public async Task<GodownDetailsDto> CreateGodownAsync(Guid actorId, GodownRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.GodownsCreate, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        Validate(request.Code, request.Name);
        var branch = await repository.GetBranchAsync(request.BranchId, cancellationToken);
        if (branch is not { IsActive: true }) throw new RequestValidationException("Branch is invalid or inactive.");
        var normalized = Normalize(request.Code);
        if (await repository.NormalizedCodeExistsAsync(request.BranchId, normalized, null, cancellationToken))
            throw new ResourceConflictException("A godown with this code already exists in the selected branch.");

        Godown? godown = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var makeDefault = request.IsDefault || await repository.ActiveGodownCountAsync(request.BranchId, ct) == 0;
            if (makeDefault) await repository.ClearDefaultForBranchAsync(request.BranchId, null, ct);
            godown = new Godown
            {
                BranchId = request.BranchId,
                Code = request.Code.Trim(),
                NormalizedCode = normalized,
                Name = request.Name.Trim(),
                Description = Clean(request.Description),
                IsDefault = makeDefault,
                IsActive = request.IsActive
            };
            await repository.AddGodownAsync(godown, ct);
            await Audit(actorId, "GodownCreated", godown.Id, null, Values(godown), ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetGodownDetailsAsync(godown!.Id, cancellationToken) ?? throw new ResourceNotFoundException("Godown was not found.");
    }

    public async Task<GodownDetailsDto> UpdateGodownAsync(Guid actorId, Guid godownId, GodownUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.GodownsUpdate, cancellationToken);
        Validate(request.Code, request.Name);
        var godown = await RequiredGodown(godownId, cancellationToken);
        EnsureBranchAccess(actor, godown.BranchId);
        var normalized = Normalize(request.Code);
        if (await repository.NormalizedCodeExistsAsync(godown.BranchId, normalized, godownId, cancellationToken))
            throw new ResourceConflictException("A godown with this code already exists in the selected branch.");
        var old = Values(godown);
        godown.Code = request.Code.Trim();
        godown.NormalizedCode = normalized;
        godown.Name = request.Name.Trim();
        godown.Description = Clean(request.Description);
        godown.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await Audit(actorId, "GodownUpdated", godown.Id, old, Values(godown), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await repository.GetGodownDetailsAsync(godown.Id, cancellationToken) ?? throw new ResourceNotFoundException("Godown was not found.");
    }

    public async Task SetGodownActiveAsync(Guid actorId, Guid godownId, bool active, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, active ? PermissionCatalog.GodownsActivate : PermissionCatalog.GodownsDeactivate, cancellationToken);
        var godown = await RequiredGodown(godownId, cancellationToken);
        EnsureBranchAccess(actor, godown.BranchId);
        if (godown.IsActive == active) return;
        if (!active)
        {
            if (godown.IsDefault) throw new RequestValidationException("Set another godown as default before deactivating this one.");
            if (await repository.ActiveGodownCountAsync(godown.BranchId, cancellationToken) <= 1)
                throw new RequestValidationException("A branch must retain at least one active godown.");
        }
        var old = new { godown.IsActive };
        godown.IsActive = active;
        godown.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await Audit(actorId, active ? "GodownActivated" : "GodownDeactivated", godown.Id, old, new { godown.IsActive }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<GodownDetailsDto> SetDefaultGodownAsync(Guid actorId, Guid godownId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.GodownsSetDefault, cancellationToken);
        var godown = await RequiredGodown(godownId, cancellationToken);
        EnsureBranchAccess(actor, godown.BranchId);
        if (!godown.IsActive) throw new RequestValidationException("An inactive godown cannot be made the default.");
        if (!godown.IsDefault)
        {
            await repository.ExecuteInTransactionAsync(async ct =>
            {
                await repository.ClearDefaultForBranchAsync(godown.BranchId, godown.Id, ct);
                godown.IsDefault = true;
                godown.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
                await Audit(actorId, "GodownSetDefault", godown.Id, null, new { godown.BranchId, godown.Id }, ct);
                await repository.SaveChangesAsync(ct);
            }, IsolationLevel.Serializable, cancellationToken);
        }
        return await repository.GetGodownDetailsAsync(godown.Id, cancellationToken) ?? throw new ResourceNotFoundException("Godown was not found.");
    }

    public async Task<IReadOnlyList<GodownLookupDto>> LookupGodownsAsync(Guid actorId, Guid? branchId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.GodownsView, cancellationToken);
        var scoped = CanSelectBranch(actor) ? branchId : actor.BranchId;
        return await repository.LookupGodownsAsync(scoped, activeOnly, cancellationToken);
    }

    public async Task<IReadOnlyList<GodownLookupDto>> GetMyGodownsAsync(Guid actorId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await repository.GetActorAsync(actorId, cancellationToken);
        if (actor is not { IsActive: true }) throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        var targetBranchId = CanSelectBranch(actor) ? (branchId ?? actor.BranchId) : actor.BranchId;
        return await repository.GetAllowedGodownsForUserAsync(actorId, targetBranchId, CanSelectBranch(actor), cancellationToken);
    }

    public async Task<IReadOnlyList<UserGodownDto>> ListAssignedUsersAsync(Guid actorId, Guid godownId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.GodownsManage, cancellationToken);
        var godown = await RequiredGodown(godownId, cancellationToken);
        EnsureBranchAccess(actor, godown.BranchId);
        return await repository.ListUserGodownsAsync(godownId, cancellationToken);
    }

    public async Task AssignUserAsync(Guid actorId, AssignUserGodownRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.GodownsManage, cancellationToken);
        var godown = await RequiredGodown(request.GodownId, cancellationToken);
        EnsureBranchAccess(actor, godown.BranchId);
        var user = await repository.GetUserAsync(request.UserId, cancellationToken) ?? throw new ResourceNotFoundException("User was not found.");
        if (user.BranchId != godown.BranchId)
            throw new RequestValidationException("A user can only be granted access to a godown within their own branch.");
        var existing = await repository.GetUserGodownAsync(request.UserId, request.GodownId, cancellationToken);
        if (existing is not null) return;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            if (request.IsDefault) await repository.ClearUserDefaultAsync(request.UserId, null, ct);
            var mapping = new UserGodown { UserId = request.UserId, GodownId = request.GodownId, IsDefault = request.IsDefault };
            await repository.AddUserGodownAsync(mapping, ct);
            await Audit(actorId, "UserGodownAssigned", request.GodownId, null, new { request.UserId, request.GodownId, request.IsDefault }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task UnassignUserAsync(Guid actorId, Guid userId, Guid godownId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.GodownsManage, cancellationToken);
        var godown = await RequiredGodown(godownId, cancellationToken);
        EnsureBranchAccess(actor, godown.BranchId);
        var mapping = await repository.GetUserGodownAsync(userId, godownId, cancellationToken);
        if (mapping is null) return;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            await repository.RemoveUserGodownAsync(mapping, ct);
            await Audit(actorId, "UserGodownUnassigned", godownId, null, new { userId, godownId }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task SetUserDefaultGodownAsync(Guid actorId, Guid userId, Guid godownId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.GodownsManage, cancellationToken);
        var godown = await RequiredGodown(godownId, cancellationToken);
        EnsureBranchAccess(actor, godown.BranchId);
        var mapping = await repository.GetUserGodownAsync(userId, godownId, cancellationToken)
            ?? throw new RequestValidationException("The user is not assigned to this godown.");
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            await repository.ClearUserDefaultAsync(userId, godownId, ct);
            mapping.IsDefault = true;
            mapping.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            await Audit(actorId, "UserGodownSetDefault", godownId, null, new { userId, godownId }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
    }

    private async Task<User> Require(Guid actorId, string permission, CancellationToken cancellationToken)
    {
        var actor = await repository.GetActorAsync(actorId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private async Task<Godown> RequiredGodown(Guid id, CancellationToken ct) =>
        await repository.GetGodownAsync(id, ct) ?? throw new ResourceNotFoundException("Godown was not found.");

    private static bool CanSelectBranch(User actor) =>
        actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.UsersView) == true;

    private static void EnsureBranchAccess(User actor, Guid branchId)
    {
        if (!CanSelectBranch(actor) && actor.BranchId != branchId)
            throw new ForbiddenOperationException("The current user is not permitted to manage this branch.");
    }

    private static void Validate(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length is < 1 or > 50) throw new RequestValidationException("Godown code is required and must be at most 50 characters.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2) throw new RequestValidationException("Godown name is required.");
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static object Values(Godown x) => new { x.BranchId, x.Code, x.Name, x.Description, x.IsDefault, x.IsActive };
    private Task Audit(Guid actor, string action, Guid id, object? old, object? current, CancellationToken ct) => repository.AddAuditAsync(new AuditLog
    { UserId = actor, Action = action, EntityType = "Godown", EntityId = id, OldValues = old is null ? null : JsonSerializer.Serialize(old), NewValues = current is null ? null : JsonSerializer.Serialize(current) }, ct);
    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
    }
}
