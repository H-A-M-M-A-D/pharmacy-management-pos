using Moq;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Auth;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Auth;
using Pharmacy.Application.Services.Users;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Services.Auth;

namespace Pharmacy.Tests;

public sealed class IdentityManagementTests
{
    private const string ValidPassword = "StrongPass1!";
    private readonly DateTimeOffset _now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Correct_password_succeeds_and_resets_failures()
    {
        var fixture = CreateFixture();
        fixture.User.FailedLoginAttempts = 3;
        var result = await fixture.Auth.LoginAsync(new LoginRequest(fixture.User.Username, ValidPassword));

        Assert.NotNull(result);
        Assert.Equal(0, fixture.User.FailedLoginAttempts);
        Assert.Null(fixture.User.LockoutEndUtc);
        Assert.Equal(_now.UtcDateTime, fixture.User.LastLoginAtUtc);
        Assert.Contains(fixture.Repository.Audits, audit => audit.Action == "LoginSucceeded");
    }

    [Fact]
    public async Task Wrong_password_increments_failures_and_locks_at_threshold()
    {
        var fixture = CreateFixture();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            Assert.Null(await fixture.Auth.LoginAsync(new LoginRequest(fixture.User.Username, "WrongPass1!")));
        }

        Assert.Equal(5, fixture.User.FailedLoginAttempts);
        Assert.Equal(_now.AddMinutes(15).UtcDateTime, fixture.User.LockoutEndUtc);
        Assert.Equal(5, fixture.Repository.Audits.Count(audit => audit.Action == "LoginFailed"));
    }

    [Fact]
    public async Task Locked_and_inactive_users_cannot_log_in()
    {
        var fixture = CreateFixture();
        fixture.User.LockoutEndUtc = _now.AddMinutes(1).UtcDateTime;
        Assert.Null(await fixture.Auth.LoginAsync(new LoginRequest(fixture.User.Username, ValidPassword)));

        fixture.User.LockoutEndUtc = null;
        fixture.User.IsActive = false;
        Assert.Null(await fixture.Auth.LoginAsync(new LoginRequest(fixture.User.Username, ValidPassword)));
    }

    [Fact]
    public async Task Password_change_requires_current_password_and_clears_first_login_flag()
    {
        var fixture = CreateFixture();
        fixture.User.MustChangePassword = true;
        await Assert.ThrowsAsync<RequestValidationException>(() => fixture.Auth.ChangePasswordAsync(
            fixture.User.Id, new ChangePasswordRequest("WrongPass1!", "AnotherPass2@")));

        var result = await fixture.Auth.ChangePasswordAsync(
            fixture.User.Id, new ChangePasswordRequest(ValidPassword, "AnotherPass2@"));

        Assert.False(fixture.User.MustChangePassword);
        Assert.Equal(1, fixture.User.TokenVersion);
        Assert.True(fixture.Hasher.Verify("AnotherPass2@", fixture.User.PasswordHash));
        Assert.Equal("test-token", result.AccessToken);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoDigitsHere!")]
    [InlineData("NoSpecial123")]
    public void Weak_password_is_rejected(string password) =>
        Assert.Throws<RequestValidationException>(() => IdentityValidation.ValidatePassword(password));

    [Fact]
    public async Task Create_user_requires_permission_and_sets_temporary_password_state()
    {
        var fixture = CreateFixture(includeManagementPermissions: false);
        var request = CreateUserRequest(fixture.Branch.Id, fixture.CashierRole.Id);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            fixture.Users.CreateAsync(fixture.User.Id, request));

        AddPermission(fixture.User.Role!, PermissionCatalog.UsersCreate);
        var created = await fixture.Users.CreateAsync(fixture.User.Id, request);
        var entity = Assert.Single(fixture.Repository.Users, user => user.Id == created.Id);
        Assert.True(entity.MustChangePassword);
        Assert.True(fixture.Hasher.Verify(request.TemporaryPassword, entity.PasswordHash));
        Assert.DoesNotContain(request.TemporaryPassword, fixture.Repository.Audits.Select(audit => audit.NewValues));
    }

    [Fact]
    public async Task Duplicate_username_and_invalid_role_are_rejected()
    {
        var fixture = CreateFixture();
        await Assert.ThrowsAsync<ResourceConflictException>(() => fixture.Users.CreateAsync(
            fixture.User.Id,
            CreateUserRequest(fixture.Branch.Id, fixture.CashierRole.Id) with { Username = fixture.User.Username.ToUpperInvariant() }));

        await Assert.ThrowsAsync<RequestValidationException>(() => fixture.Users.CreateAsync(
            fixture.User.Id,
            CreateUserRequest(fixture.Branch.Id, Guid.NewGuid())));
    }

    [Fact]
    public async Task Activate_deactivate_and_reset_password_update_security_state()
    {
        var fixture = CreateFixture();
        var target = fixture.AddUser("target", fixture.CashierRole, isActive: true);
        await fixture.Users.DeactivateAsync(fixture.User.Id, target.Id);
        Assert.False(target.IsActive);
        Assert.Equal(1, target.TokenVersion);

        await fixture.Users.ActivateAsync(fixture.User.Id, target.Id);
        Assert.True(target.IsActive);
        await fixture.Users.ResetPasswordAsync(
            fixture.User.Id, target.Id, new ResetPasswordRequest("Temporary2@"));
        Assert.True(target.MustChangePassword);
        Assert.True(fixture.Hasher.Verify("Temporary2@", target.PasswordHash));
        Assert.Contains(fixture.Repository.Audits, audit => audit.Action == "PasswordReset");
    }

    [Fact]
    public async Task Last_active_owner_cannot_be_deactivated_or_lose_owner_role()
    {
        var fixture = CreateFixture(ownerActor: true);
        await Assert.ThrowsAsync<ResourceConflictException>(() =>
            fixture.Users.DeactivateAsync(fixture.User.Id, fixture.User.Id));
        await Assert.ThrowsAsync<ResourceConflictException>(() => fixture.Users.UpdateAsync(
            fixture.User.Id,
            fixture.User.Id,
            new UpdateUserRequest("Owner User", null, null, fixture.Branch.Id, fixture.CashierRole.Id)));
    }

    [Fact]
    public async Task Manager_cannot_modify_owner_without_owner_management_permission()
    {
        var fixture = CreateFixture();
        var owner = fixture.AddUser("owner", fixture.OwnerRole, isActive: true);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            fixture.Users.DeactivateAsync(fixture.User.Id, owner.Id));
    }

    [Fact]
    public async Task Admin_reset_never_audits_password_or_hash()
    {
        var fixture = CreateFixture();
        var target = fixture.AddUser("target", fixture.CashierRole, isActive: true);
        await fixture.Users.ResetPasswordAsync(
            fixture.User.Id, target.Id, new ResetPasswordRequest("Temporary2@"));

        var audit = Assert.Single(fixture.Repository.Audits, item => item.Action == "PasswordReset");
        Assert.DoesNotContain("Temporary2@", audit.NewValues);
        Assert.DoesNotContain("PBKDF2", audit.NewValues);
    }

    private Fixture CreateFixture(bool includeManagementPermissions = true, bool ownerActor = false)
    {
        var repository = new FakeUserAccountRepository();
        var branch = new Branch { Code = "HQ", Name = "Head Office", IsActive = true };
        var ownerRole = new Role { Name = RoleCatalog.Owner, IsActive = true };
        var managerRole = new Role { Name = RoleCatalog.Manager, IsActive = true };
        var cashierRole = new Role { Name = RoleCatalog.Cashier, IsActive = true };
        repository.Branches.Add(branch);
        repository.Roles.AddRange([ownerRole, managerRole, cashierRole]);
        var actorRole = ownerActor ? ownerRole : managerRole;
        AddPermission(actorRole, PermissionCatalog.ProfileView);
        AddPermission(actorRole, PermissionCatalog.ProfileUpdate);
        AddPermission(actorRole, PermissionCatalog.ProfileChangePassword);
        if (includeManagementPermissions)
        {
            foreach (var permission in new[]
            {
                PermissionCatalog.UsersCreate, PermissionCatalog.UsersUpdate,
                PermissionCatalog.UsersActivate, PermissionCatalog.UsersDeactivate,
                PermissionCatalog.UsersResetPassword
            })
            {
                AddPermission(actorRole, permission);
            }
        }

        if (ownerActor)
        {
            AddPermission(actorRole, PermissionCatalog.UsersManageOwner);
        }

        var hasher = new Pbkdf2PasswordHasher();
        var user = new User
        {
            Username = ownerActor ? "owner" : "manager",
            NormalizedUsername = ownerActor ? "OWNER" : "MANAGER",
            FullName = ownerActor ? "Owner User" : "Manager User",
            PasswordHash = hasher.Hash(ValidPassword),
            BranchId = branch.Id,
            Branch = branch,
            RoleId = actorRole.Id,
            Role = actorRole,
            IsActive = true
        };
        repository.Users.Add(user);
        var time = new FixedTimeProvider(_now);
        var token = new Mock<ITokenService>();
        token.Setup(service => service.CreateToken(It.IsAny<CurrentUserDto>(), It.IsAny<int>()))
            .Returns(new AccessTokenResult("test-token", _now.AddHours(1).UtcDateTime));
        var auth = new AuthService(repository, hasher, token.Object, new AuthenticationSecurityOptions(), time, Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance);
        var users = new UserManagementService(repository, hasher, time);
        return new Fixture(repository, hasher, auth, users, user, branch, ownerRole, cashierRole);
    }

    private static CreateUserRequest CreateUserRequest(Guid branchId, Guid roleId) =>
        new("New User", "new.user", null, null, branchId, roleId, "Temporary1!", true);

    private static void AddPermission(Role role, string code)
    {
        if (role.RolePermissions.Any(item => item.Permission?.Code == code))
        {
            return;
        }

        var permission = new Permission { Code = code, Description = code, Category = code.Split('.')[0] };
        role.RolePermissions.Add(new RolePermission
        {
            RoleId = role.Id,
            PermissionId = permission.Id,
            Role = role,
            Permission = permission
        });
    }

    private sealed record Fixture(
        FakeUserAccountRepository Repository,
        Pbkdf2PasswordHasher Hasher,
        AuthService Auth,
        UserManagementService Users,
        User User,
        Branch Branch,
        Role OwnerRole,
        Role CashierRole)
    {
        public User AddUser(string username, Role role, bool isActive)
        {
            var user = new User
            {
                Username = username,
                NormalizedUsername = username.ToUpperInvariant(),
                FullName = $"{username} user",
                PasswordHash = Hasher.Hash(ValidPassword),
                BranchId = Branch.Id,
                Branch = Branch,
                RoleId = role.Id,
                Role = role,
                IsActive = isActive
            };
            Repository.Users.Add(user);
            return user;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeUserAccountRepository : IUserAccountRepository
    {
        public List<User> Users { get; } = [];
        public List<Role> Roles { get; } = [];
        public List<Branch> Branches { get; } = [];
        public List<AuditLog> Audits { get; } = [];

        public Task<User?> GetByNormalizedUsernameAsync(string value, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.SingleOrDefault(user => user.NormalizedUsername == value));
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.SingleOrDefault(user => user.Id == id));
        public Task<bool> AnyUsersAsync(CancellationToken cancellationToken = default) => Task.FromResult(Users.Count != 0);
        public Task<bool> UsernameExistsAsync(string value, Guid? excludingUserId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Any(user => user.NormalizedUsername == value && user.Id != excludingUserId));
        public Task<bool> EmailExistsAsync(string value, Guid? excludingUserId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Any(user => user.NormalizedEmail == value && user.Id != excludingUserId));
        public Task<Role?> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Roles.SingleOrDefault(role => role.Id == id));
        public Task<Role?> GetRoleByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(Roles.SingleOrDefault(role => role.Name == name));
        public Task<Branch?> GetBranchByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Branches.SingleOrDefault(branch => branch.Id == id));
        public Task<Branch?> GetBranchByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(Branches.SingleOrDefault(branch => branch.Code == code));
        public Task<int> CountActiveUsersInRoleAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Count(user => user.IsActive && user.Role?.Name == name));
        public Task<PagedResult<UserListItemDto>> ListUsersAsync(UserListQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<UserListItemDto>([], query.Page, query.PageSize, Users.Count));
        public Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RoleDto>>(Roles.Select(role => new RoleDto(role.Id, role.Name, role.Description)).ToList());
        public Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PermissionDto>>([]);
        public Task<IReadOnlyList<BranchDto>> ListBranchesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BranchDto>>(Branches.Select(branch => new BranchDto(branch.Id, branch.Code, branch.Name)).ToList());
        public Task AddRoleAsync(Role role, CancellationToken cancellationToken = default) { Roles.Add(role); return Task.CompletedTask; }
        public Task AddBranchAsync(Branch branch, CancellationToken cancellationToken = default) { Branches.Add(branch); return Task.CompletedTask; }
        public Task AddUserAsync(User user, CancellationToken cancellationToken = default) { Users.Add(user); return Task.CompletedTask; }
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
