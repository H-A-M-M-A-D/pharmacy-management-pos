using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Pricing;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Pricing;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class PricingManagementTests
{
    [Fact]
    public async Task Creating_a_price_level_requires_pricing_manage_permission()
    {
        var f = new Fixture(PermissionCatalog.PricingView);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            f.Service.CreatePriceLevelAsync(f.Actor.Id, new("Wholesale", "WS", 2, false, true)));
    }

    [Fact]
    public async Task Creating_a_duplicate_price_level_code_is_rejected()
    {
        var f = new Fixture(PermissionCatalog.PricingManage);
        await f.Service.CreatePriceLevelAsync(f.Actor.Id, new("Wholesale", "WS", 2, false, true));
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            f.Service.CreatePriceLevelAsync(f.Actor.Id, new("Wholesale Two", "ws", 3, false, true)));
    }

    [Fact]
    public async Task Marking_a_new_level_default_clears_the_previous_default()
    {
        var f = new Fixture(PermissionCatalog.PricingManage, PermissionCatalog.PricingView);
        var retail = await f.Service.CreatePriceLevelAsync(f.Actor.Id, new("Retail", "RT", 1, true, true));
        var wholesale = await f.Service.CreatePriceLevelAsync(f.Actor.Id, new("Wholesale", "WS", 2, true, true));
        var levels = await f.Service.ListPriceLevelsAsync(f.Actor.Id, false);
        Assert.False(levels.Single(x => x.Id == retail.Id).IsDefault);
        Assert.True(levels.Single(x => x.Id == wholesale.Id).IsDefault);
    }

    [Fact]
    public async Task Setting_a_product_price_for_a_level_upserts_rather_than_duplicates()
    {
        var f = new Fixture(PermissionCatalog.PricingManage, PermissionCatalog.PricingView);
        var level = await f.Service.CreatePriceLevelAsync(f.Actor.Id, new("Wholesale", "WS", 1, false, true));
        var first = await f.Service.SetProductPriceAsync(f.Actor.Id, new(f.Product.Id, level.Id, 90));
        var second = await f.Service.SetProductPriceAsync(f.Actor.Id, new(f.Product.Id, level.Id, 85));
        Assert.Equal(first.Id, second.Id);
        var all = await f.Service.ListProductPricesAsync(f.Actor.Id, f.Product.Id, level.Id);
        Assert.Single(all);
        Assert.Equal(85, all.Single().SellingPrice);
    }

    [Fact]
    public async Task Negative_selling_price_is_rejected()
    {
        var f = new Fixture(PermissionCatalog.PricingManage);
        var level = await f.Service.CreatePriceLevelAsync(f.Actor.Id, new("Wholesale", "WS", 1, false, true));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.SetProductPriceAsync(f.Actor.Id, new(f.Product.Id, level.Id, -1)));
    }

    [Fact]
    public async Task Price_break_minimum_quantity_must_be_positive()
    {
        var f = new Fixture(PermissionCatalog.PricingManage);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.SetProductPriceBreakAsync(f.Actor.Id, new(f.Product.Id, null, 0, 50)));
    }

    [Fact]
    public async Task Removing_a_product_price_break_is_reflected_in_the_list()
    {
        var f = new Fixture(PermissionCatalog.PricingManage, PermissionCatalog.PricingView);
        var brk = await f.Service.SetProductPriceBreakAsync(f.Actor.Id, new(f.Product.Id, null, 10, 80));
        await f.Service.RemoveProductPriceBreakAsync(f.Actor.Id, brk.Id);
        var all = await f.Service.ListProductPriceBreaksAsync(f.Actor.Id, f.Product.Id);
        Assert.Empty(all);
    }

    private sealed class Fixture : IPricingRepository
    {
        public readonly Product Product = new()
        {
            SKU = "SKU-1", NormalizedSku = "SKU-1", Name = "Panadol", Unit = "Tablet", PackSize = 1,
            PurchasePrice = 8, RetailPrice = 12, IsActive = true
        };
        public readonly User Actor;
        public readonly List<PriceLevel> Levels = [];
        public readonly List<ProductPriceLevel> ProductPrices = [];
        public readonly List<ProductPriceBreak> ProductBreaks = [];
        public readonly List<AuditLog> Audits = [];
        public PricingService Service { get; }

        public Fixture(params string[] permissions)
        {
            var role = new Role { Name = RoleCatalog.Manager };
            foreach (var permission in permissions)
                role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "actor", NormalizedUsername = "ACTOR", FullName = "Actor", PasswordHash = "hash", RoleId = role.Id, Role = role, IsActive = true };
            Service = new(this, new NullResolver(), TimeProvider.System);
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(Product.Id == productId ? Product : null);
        public Task<PriceLevel?> GetPriceLevelAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Levels.FirstOrDefault(x => x.Id == id));
        public Task<bool> PriceLevelCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken = default) => Task.FromResult(Levels.Any(x => x.Code == code && x.Id != excludingId));
        public Task<IReadOnlyList<PriceLevelDto>> ListPriceLevelsAsync(bool activeOnly, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PriceLevelDto>>(Levels.Where(x => !activeOnly || x.IsActive)
                .Select(x => new PriceLevelDto(x.Id, x.Name, x.Code, x.Priority, x.IsDefault, x.IsActive, x.BranchId, null)).ToList());
        public Task AddPriceLevelAsync(PriceLevel level, CancellationToken cancellationToken = default) { Levels.Add(level); return Task.CompletedTask; }
        public Task ClearDefaultPriceLevelAsync(Guid? excludingId, CancellationToken cancellationToken = default)
        {
            foreach (var level in Levels.Where(x => x.IsDefault && x.Id != excludingId)) level.IsDefault = false;
            return Task.CompletedTask;
        }

        public Task<ProductPriceLevel?> GetProductPriceLevelAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(ProductPrices.FirstOrDefault(x => x.Id == id));
        public Task<ProductPriceLevel?> FindProductPriceLevelAsync(Guid productId, Guid priceLevelId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductPrices.FirstOrDefault(x => x.ProductId == productId && x.PriceLevelId == priceLevelId));
        public Task<IReadOnlyList<ProductPriceLevelDto>> ListProductPriceLevelsAsync(Guid? productId, Guid? priceLevelId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductPriceLevelDto>>(ProductPrices
                .Where(x => (!productId.HasValue || x.ProductId == productId) && (!priceLevelId.HasValue || x.PriceLevelId == priceLevelId))
                .Select(x => new ProductPriceLevelDto(x.Id, x.ProductId, Product.Name, Product.SKU, x.PriceLevelId, Levels.First(l => l.Id == x.PriceLevelId).Name, x.SellingPrice, x.IsActive)).ToList());
        public Task AddProductPriceLevelAsync(ProductPriceLevel entry, CancellationToken cancellationToken = default) { ProductPrices.Add(entry); return Task.CompletedTask; }
        public Task RemoveProductPriceLevelAsync(ProductPriceLevel entry, CancellationToken cancellationToken = default) { ProductPrices.Remove(entry); return Task.CompletedTask; }

        public Task<ProductPriceBreak?> GetProductPriceBreakAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(ProductBreaks.FirstOrDefault(x => x.Id == id));
        public Task<ProductPriceBreak?> FindProductPriceBreakAsync(Guid productId, Guid? priceLevelId, int minimumQuantity, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductBreaks.FirstOrDefault(x => x.ProductId == productId && x.PriceLevelId == priceLevelId && x.MinimumQuantity == minimumQuantity));
        public Task<IReadOnlyList<ProductPriceBreakDto>> ListProductPriceBreaksAsync(Guid? productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductPriceBreakDto>>(ProductBreaks.Where(x => !productId.HasValue || x.ProductId == productId)
                .Select(x => new ProductPriceBreakDto(x.Id, x.ProductId, Product.Name, Product.SKU, x.PriceLevelId, null, x.MinimumQuantity, x.SellingPrice, x.IsActive)).ToList());
        public Task AddProductPriceBreakAsync(ProductPriceBreak entry, CancellationToken cancellationToken = default) { ProductBreaks.Add(entry); return Task.CompletedTask; }
        public Task RemoveProductPriceBreakAsync(ProductPriceBreak entry, CancellationToken cancellationToken = default) { ProductBreaks.Remove(entry); return Task.CompletedTask; }

        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        private sealed class NullResolver : IPriceResolutionService
        {
            public Task<PriceResolutionResult> ResolveAsync(Guid? customerId, Guid productId, int quantity, Guid? explicitPriceLevelId = null, CancellationToken cancellationToken = default) =>
                Task.FromResult(new PriceResolutionResult(null, PriceSource.Default, null, null));
        }
    }
}
