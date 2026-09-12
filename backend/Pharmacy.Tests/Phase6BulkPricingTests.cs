using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Phase6;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Services;

namespace Pharmacy.Tests;

public sealed class Phase6BulkPricingTests
{
    [Fact]
    public async Task Preview_does_not_change_prices_and_apply_records_history_and_actor()
    {
        await using var f = await Fixture.Create();
        var preview = await f.Service.PreviewBulkPricingAsync(f.Actor.Id, f.Request());
        Assert.Equal(100, f.Product.RetailPrice);
        Assert.Equal(110, Assert.Single(preview.Rows).NewPrice);
        Assert.Empty(f.Db.PricingPriceHistories);
        Assert.Equal(1, await f.Service.ApplyBulkPricingAsync(f.Actor.Id, new(preview.PreviewId, true)));
        Assert.Equal(110, f.Product.RetailPrice);
        var history = Assert.Single(f.Db.PricingPriceHistories);
        Assert.Equal(100, history.OldPrice);
        Assert.Equal(110, history.NewPrice);
        Assert.Equal(f.Actor.Id, history.ActorId);
        Assert.Equal("Supplier increase", history.Reason);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ApplyBulkPricingAsync(f.Actor.Id, new(preview.PreviewId, true)));
    }

    [Fact]
    public async Task Apply_requires_explicit_confirmation()
    {
        await using var f = await Fixture.Create();
        var preview = await f.Service.PreviewBulkPricingAsync(f.Actor.Id, f.Request());
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ApplyBulkPricingAsync(f.Actor.Id, new(preview.PreviewId, false)));
        Assert.Equal(100, f.Product.RetailPrice);
    }

    [Fact]
    public async Task Unauthorized_actor_cannot_preview_or_apply()
    {
        await using var f = await Fixture.Create(false);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.PreviewBulkPricingAsync(f.Actor.Id, f.Request()));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.ApplyBulkPricingAsync(f.Actor.Id, new(Guid.NewGuid(), true)));
    }

    [Fact]
    public async Task Below_cost_preview_is_rejected()
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.PreviewBulkPricingAsync(f.Actor.Id, f.Request() with { Action = BulkPricingAction.DecreasePercent, Value = 75 }));
        Assert.Equal(100, f.Product.RetailPrice);
    }

    [Fact]
    public async Task Changed_cost_rejects_entire_preview_without_history()
    {
        await using var f = await Fixture.Create();
        var preview = await f.Service.PreviewBulkPricingAsync(f.Actor.Id, f.Request());
        f.Product.PurchasePrice = 90;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ApplyBulkPricingAsync(f.Actor.Id, new(preview.PreviewId, true)));
        Assert.Equal(100, f.Product.RetailPrice);
        Assert.Empty(f.Db.PricingPriceHistories);
    }

    [Theory]
    [InlineData(BulkPricingAction.SetMargin, 20, 100)]
    [InlineData(BulkPricingAction.SetMarkup, 25, 100)]
    [InlineData(BulkPricingAction.Round, 0, 100)]
    public async Task Margin_markup_and_rounding_are_deterministic(BulkPricingAction action, decimal value, decimal expected)
    {
        await using var f = await Fixture.Create();
        var preview = await f.Service.PreviewBulkPricingAsync(f.Actor.Id, f.Request() with { Action = action, Value = value });
        Assert.Equal(expected, Assert.Single(preview.Rows).NewPrice);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required PharmacyDbContext Db { get; init; }
        public required User Actor { get; init; }
        public required Product Product { get; init; }
        public Phase6Service Service => new(Db, TimeProvider.System);
        public BulkPricingRequest Request() => new(Product.CategoryId, null, null, [Product.Id], BulkPricingAction.IncreasePercent, 10, 1, "Supplier increase");
        public static async Task<Fixture> Create(bool permitted = true)
        {
            var db = new PharmacyDbContext(new DbContextOptionsBuilder<PharmacyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var role = new Role { Name = "Manager" };
            if (permitted) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = PermissionCatalog.PricingManage, Description = "Pricing", Category = "Pricing" } });
            var actor = new User { Username = "manager", NormalizedUsername = "MANAGER", FullName = "Manager", PasswordHash = "hash", Role = role, IsActive = true };
            var product = new Product { SKU = "P", NormalizedSku = "P", Name = "Tablet", Unit = "unit", PackSize = 1, PurchasePrice = 80, RetailPrice = 100, Category = new ProductCategory { Name = "Category", NormalizedName = "CATEGORY" } };
            db.AddRange(actor, product);
            await db.SaveChangesAsync();
            return new() { Db = db, Actor = actor, Product = product };
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
