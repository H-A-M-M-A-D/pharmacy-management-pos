using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Godowns;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Godowns;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class GodownManagementTests
{
    [Fact]
    public async Task Create_first_godown_for_a_branch_automatically_becomes_default()
    {
        var f = new Fixture(PermissionCatalog.GodownsCreate, PermissionCatalog.GodownsView);
        var created = await f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, "MAIN", "Main Godown", null, false, true));
        Assert.True(created.IsDefault);
    }

    [Fact]
    public async Task Create_rejects_duplicate_code_within_the_same_branch_but_allows_it_in_another_branch()
    {
        var f = new Fixture(PermissionCatalog.GodownsCreate, PermissionCatalog.GodownsView);
        await f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, "MAIN", "Main Godown", null, true, true));
        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, " main ", "Duplicate", null, false, true)));

        var otherBranch = new Branch { Code = "B2", Name = "Second Branch", IsActive = true };
        f.Branches[otherBranch.Id] = otherBranch;
        var otherGodown = await f.Service.CreateGodownAsync(f.Actor.Id, new(otherBranch.Id, "MAIN", "Main Godown", null, false, true));
        Assert.Equal("MAIN", otherGodown.Code);
    }

    [Fact]
    public async Task Setting_a_new_default_clears_the_previous_default_for_the_branch()
    {
        var f = new Fixture(PermissionCatalog.GodownsCreate, PermissionCatalog.GodownsView, PermissionCatalog.GodownsSetDefault);
        var first = await f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, "MAIN", "Main Godown", null, true, true));
        var second = await f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, "RETAIL", "Retail Counter", null, false, true));
        Assert.True(first.IsDefault);
        Assert.False(second.IsDefault);

        var updated = await f.Service.SetDefaultGodownAsync(f.Actor.Id, second.Id);
        Assert.True(updated.IsDefault);
        var previousDefault = await f.Service.GetGodownAsync(f.Actor.Id, first.Id);
        Assert.False(previousDefault.IsDefault);
    }

    [Fact]
    public async Task Cannot_deactivate_the_only_active_godown_or_the_current_default()
    {
        var f = new Fixture(PermissionCatalog.GodownsCreate, PermissionCatalog.GodownsView, PermissionCatalog.GodownsDeactivate, PermissionCatalog.GodownsSetDefault);
        var only = await f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, "MAIN", "Main Godown", null, true, true));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.SetGodownActiveAsync(f.Actor.Id, only.Id, false));

        var second = await f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, "RETAIL", "Retail Counter", null, false, true));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.SetGodownActiveAsync(f.Actor.Id, only.Id, false));

        await f.Service.SetDefaultGodownAsync(f.Actor.Id, second.Id);
        await f.Service.SetGodownActiveAsync(f.Actor.Id, only.Id, false);
        var deactivated = await f.Service.GetGodownAsync(f.Actor.Id, only.Id);
        Assert.False(deactivated.IsActive);
    }

    [Fact]
    public async Task Inactive_godown_cannot_be_made_the_default()
    {
        var f = new Fixture(PermissionCatalog.GodownsCreate, PermissionCatalog.GodownsView, PermissionCatalog.GodownsSetDefault, PermissionCatalog.GodownsDeactivate);
        var main = await f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, "MAIN", "Main Godown", null, true, true));
        var retail = await f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, "RETAIL", "Retail Counter", null, false, false));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.SetDefaultGodownAsync(f.Actor.Id, retail.Id));
    }

    [Fact]
    public async Task Missing_permission_is_forbidden()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, "MAIN", "Main Godown", null, true, true)));
    }

    [Fact]
    public async Task User_assigned_to_a_godown_is_reported_as_allowed_for_that_branch()
    {
        var f = new Fixture(PermissionCatalog.GodownsCreate, PermissionCatalog.GodownsView, PermissionCatalog.GodownsManage);
        var main = await f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, "MAIN", "Main Godown", null, true, true));
        var retail = await f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, "RETAIL", "Retail Counter", null, false, true));
        var cashier = f.AddUser(f.Branch.Id);

        await f.Service.AssignUserAsync(f.Actor.Id, new(cashier.Id, retail.Id, true));
        var allowed = await f.Service.GetMyGodownsAsync(cashier.Id, null);

        Assert.Single(allowed);
        Assert.Equal(retail.Id, allowed.Single().Id);
    }

    [Fact]
    public async Task Assigning_a_user_to_a_godown_in_another_branch_is_rejected()
    {
        var f = new Fixture(PermissionCatalog.GodownsCreate, PermissionCatalog.GodownsView, PermissionCatalog.GodownsManage);
        var main = await f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, "MAIN", "Main Godown", null, true, true));
        var otherBranch = new Branch { Code = "B2", Name = "Second Branch", IsActive = true };
        f.Branches[otherBranch.Id] = otherBranch;
        var otherBranchUser = f.AddUser(otherBranch.Id);

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.AssignUserAsync(f.Actor.Id, new(otherBranchUser.Id, main.Id, true)));
    }

    [Fact]
    public async Task User_with_no_godown_assignment_has_no_allowed_godowns()
    {
        var f = new Fixture(PermissionCatalog.GodownsCreate, PermissionCatalog.GodownsView);
        await f.Service.CreateGodownAsync(f.Actor.Id, new(f.Branch.Id, "MAIN", "Main Godown", null, true, true));
        var cashier = f.AddUser(f.Branch.Id);

        var allowed = await f.Service.GetMyGodownsAsync(cashier.Id, null);
        Assert.Empty(allowed);
    }

    private sealed class Fixture : IGodownRepository
    {
        public readonly Branch Branch = new() { Code = "MAIN", Name = "Main Branch", IsActive = true };
        public readonly Dictionary<Guid, Branch> Branches = [];
        public readonly User Actor;
        public readonly List<User> Users = [];
        public readonly List<Godown> Godowns = [];
        public readonly List<UserGodown> UserGodowns = [];
        public readonly List<AuditLog> Audits = [];
        public IGodownService Service { get; }

        public Fixture(params string[] permissions)
        {
            Branches[Branch.Id] = Branch;
            var role = new Role { Name = RoleCatalog.Manager };
            foreach (var permission in permissions) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "actor", NormalizedUsername = "ACTOR", FullName = "Actor", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role, IsActive = true };
            Users.Add(Actor);
            Service = new GodownService(this, TimeProvider.System);
        }

        public User AddUser(Guid branchId)
        {
            var role = new Role { Name = RoleCatalog.Cashier };
            var user = new User { Username = $"user{Users.Count}", NormalizedUsername = $"USER{Users.Count}", FullName = "Cashier", PasswordHash = "hash", BranchId = branchId, RoleId = role.Id, Role = role, IsActive = true };
            Users.Add(user);
            return user;
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult(Users.FirstOrDefault(x => x.Id == actorId));
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult(Branches.GetValueOrDefault(branchId));
        public Task<Godown?> GetGodownAsync(Guid godownId, CancellationToken cancellationToken = default) => Task.FromResult(Godowns.FirstOrDefault(x => x.Id == godownId));
        public Task<bool> NormalizedCodeExistsAsync(Guid branchId, string normalizedCode, Guid? excludingId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Godowns.Any(x => x.BranchId == branchId && x.NormalizedCode == normalizedCode && (!excludingId.HasValue || x.Id != excludingId)));
        public Task<Godown?> GetDefaultGodownAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult(Godowns.FirstOrDefault(x => x.BranchId == branchId && x.IsDefault && x.IsActive));
        public Task<int> ActiveGodownCountAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult(Godowns.Count(x => x.BranchId == branchId && x.IsActive));
        public Task<bool> GodownHasHistoryAsync(Guid godownId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddGodownAsync(Godown godown, CancellationToken cancellationToken = default) { Godowns.Add(godown); return Task.CompletedTask; }
        public Task ClearDefaultForBranchAsync(Guid branchId, Guid? excludingId = null, CancellationToken cancellationToken = default)
        {
            foreach (var g in Godowns.Where(x => x.BranchId == branchId && x.IsDefault && (!excludingId.HasValue || x.Id != excludingId))) g.IsDefault = false;
            return Task.CompletedTask;
        }
        public Task<PagedResult<GodownListItemDto>> ListGodownsAsync(GodownListQuery query, CancellationToken cancellationToken = default)
        {
            var items = Godowns.Where(x => !query.BranchId.HasValue || x.BranchId == query.BranchId)
                .Select(x => new GodownListItemDto(x.Id, x.BranchId, Branches[x.BranchId].Name, x.Code, x.Name, x.Description, x.IsDefault, x.IsActive, x.CreatedAt)).ToList();
            return Task.FromResult(new PagedResult<GodownListItemDto>(items, query.Page, query.PageSize, items.Count));
        }
        public Task<GodownDetailsDto?> GetGodownDetailsAsync(Guid godownId, CancellationToken cancellationToken = default)
        {
            var g = Godowns.FirstOrDefault(x => x.Id == godownId);
            return Task.FromResult(g is null ? null : new GodownDetailsDto(g.Id, g.BranchId, Branches[g.BranchId].Name, g.Code, g.Name, g.Description, g.IsDefault, g.IsActive, g.CreatedAt, g.UpdatedAt));
        }
        public Task<IReadOnlyList<GodownLookupDto>> LookupGodownsAsync(Guid? branchId, bool activeOnly, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GodownLookupDto>>(Godowns.Where(x => (!branchId.HasValue || x.BranchId == branchId) && (!activeOnly || x.IsActive))
                .Select(x => new GodownLookupDto(x.Id, x.BranchId, x.Code, x.Name, x.IsDefault, x.IsActive)).ToList());
        public Task<IReadOnlyList<GodownLookupDto>> GetAllowedGodownsForUserAsync(Guid userId, Guid branchId, bool canSelectBranch, CancellationToken cancellationToken = default)
        {
            var active = Godowns.Where(x => x.BranchId == branchId && x.IsActive);
            if (canSelectBranch) return Task.FromResult<IReadOnlyList<GodownLookupDto>>(active.Select(x => new GodownLookupDto(x.Id, x.BranchId, x.Code, x.Name, x.IsDefault, x.IsActive)).ToList());
            var allowedIds = UserGodowns.Where(x => x.UserId == userId).Select(x => x.GodownId).ToHashSet();
            return Task.FromResult<IReadOnlyList<GodownLookupDto>>(active.Where(x => allowedIds.Contains(x.Id)).Select(x => new GodownLookupDto(x.Id, x.BranchId, x.Code, x.Name, x.IsDefault, x.IsActive)).ToList());
        }
        public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Users.FirstOrDefault(x => x.Id == userId));
        public Task<UserGodown?> GetUserGodownAsync(Guid userId, Guid godownId, CancellationToken cancellationToken = default) => Task.FromResult(UserGodowns.FirstOrDefault(x => x.UserId == userId && x.GodownId == godownId));
        public Task<IReadOnlyList<UserGodownDto>> ListUserGodownsAsync(Guid godownId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserGodownDto>>(UserGodowns.Where(x => x.GodownId == godownId)
                .Select(x => new UserGodownDto(x.UserId, Users.First(u => u.Id == x.UserId).FullName, x.GodownId, Godowns.First(g => g.Id == x.GodownId).Name, x.IsDefault)).ToList());
        public Task AddUserGodownAsync(UserGodown userGodown, CancellationToken cancellationToken = default) { UserGodowns.Add(userGodown); return Task.CompletedTask; }
        public Task RemoveUserGodownAsync(UserGodown userGodown, CancellationToken cancellationToken = default) { UserGodowns.Remove(userGodown); return Task.CompletedTask; }
        public Task ClearUserDefaultAsync(Guid userId, Guid? excludingGodownId = null, CancellationToken cancellationToken = default)
        {
            foreach (var m in UserGodowns.Where(x => x.UserId == userId && x.IsDefault && (!excludingGodownId.HasValue || x.GodownId != excludingGodownId))) m.IsDefault = false;
            return Task.CompletedTask;
        }
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => await operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
