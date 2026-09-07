using Microsoft.EntityFrameworkCore;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;
using Moq;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Administration;
using Pharmacy.Application.Common;

namespace Pharmacy.Tests;

public sealed class SystemAdministrationTests
{
    [Fact]
    public async Task Audit_log_is_append_only()
    {
        await using var context = Context();
        var audit = new AuditLog { UserId=Guid.NewGuid(), Action="Created", EntityType="Test", EntityId=Guid.NewGuid(), NewValues="{}" };
        context.AuditLogs.Add(audit);
        await context.SaveChangesAsync();
        audit.Action = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Audit_values_are_scrubbed_centrally()
    {
        await using var context = Context();
        var audit = new AuditLog { UserId=Guid.NewGuid(), Action="Created", EntityType="Test", EntityId=Guid.NewGuid(), NewValues="{\"password\":\"plain\",\"nested\":{\"accessToken\":\"token\"}}" };
        context.AuditLogs.Add(audit);
        await context.SaveChangesAsync();
        Assert.DoesNotContain("plain", audit.NewValues);
        Assert.DoesNotContain("\"token\"}", audit.NewValues);
        Assert.Contains("[REDACTED]", audit.NewValues);
    }

    [Fact]
    public async Task Soft_deleted_reference_items_are_filtered_by_default()
    {
        await using var context = Context();
        context.ProductCategories.Add(new ProductCategory { Name="Deleted", NormalizedName="DELETED", IsDeleted=true, DeletedAtUtc=DateTime.UtcNow });
        await context.SaveChangesAsync();
        Assert.Empty(await context.ProductCategories.ToListAsync());
        Assert.Single(await context.ProductCategories.IgnoreQueryFilters().ToListAsync());
    }

    private static PharmacyDbContext Context()
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new PharmacyDbContext(options);
    }

    [Fact]
    public async Task Last_active_branch_cannot_be_deactivated()
    {
        var repository = new Mock<IAdministrationRepository>();
        var actor = Actor(PermissionCatalog.BranchesManage);
        var branch = new Branch { Code="HQ", NormalizedCode="HQ", Name="Head Office", IsActive=true };
        repository.Setup(x=>x.GetActorAsync(actor.Id,It.IsAny<CancellationToken>())).ReturnsAsync(actor);
        repository.Setup(x=>x.GetBranchAsync(branch.Id,It.IsAny<CancellationToken>())).ReturnsAsync(branch);
        repository.Setup(x=>x.ActiveBranchCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var service = new AdministrationService(repository.Object, TimeProvider.System);
        await Assert.ThrowsAsync<ResourceConflictException>(()=>service.SetBranchActiveAsync(actor.Id,branch.Id,false));
    }

    [Fact]
    public async Task Transaction_entities_are_rejected_by_recycle_bin_allow_list()
    {
        var repository = new Mock<IAdministrationRepository>();
        var actor = Actor(PermissionCatalog.RecycleBinRestore);
        repository.Setup(x=>x.GetActorAsync(actor.Id,It.IsAny<CancellationToken>())).ReturnsAsync(actor);
        var service = new AdministrationService(repository.Object, TimeProvider.System);
        await Assert.ThrowsAsync<RequestValidationException>(()=>service.SoftDeleteAsync(actor.Id,"Sale",Guid.NewGuid()));
        repository.Verify(x=>x.GetRecyclableAsync(It.IsAny<string>(),It.IsAny<Guid>(),It.IsAny<bool>(),It.IsAny<CancellationToken>()),Times.Never);
    }

    [Fact]
    public async Task Sign_out_everywhere_increments_token_version_and_audits()
    {
        var repository = new Mock<IAdministrationRepository>();
        var actor = Actor(PermissionCatalog.ProfileView); actor.TokenVersion=3;
        repository.Setup(x=>x.GetActorAsync(actor.Id,It.IsAny<CancellationToken>())).ReturnsAsync(actor);
        var service = new AdministrationService(repository.Object, TimeProvider.System);
        await service.SignOutEverywhereAsync(actor.Id);
        Assert.Equal(4,actor.TokenVersion);
        repository.Verify(x=>x.AddAuditAsync(It.Is<AuditLog>(a=>a.Action=="SessionsRevoked"),It.IsAny<CancellationToken>()),Times.Once);
        repository.Verify(x=>x.SaveAsync(It.IsAny<CancellationToken>()),Times.Once);
    }

    private static User Actor(string permission)
    {
        var p=new Permission { Code=permission, Description=permission, Category="test" };
        var role=new Role { Name="Owner", RolePermissions=[new RolePermission { Permission=p }] };
        return new User { Username="owner", NormalizedUsername="OWNER", FullName="Owner", PasswordHash="hash", IsActive=true, Role=role };
    }
}
