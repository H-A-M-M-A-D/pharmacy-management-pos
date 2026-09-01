using Moq;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Catalog;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Auth;
using Pharmacy.Application.Services.Catalog;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class ProductMasterTests
{
    [Fact]
    public async Task Create_product_normalizes_identifiers_and_audits()
    {
        var f = Fixture(PermissionCatalog.ProductsCreate);
        var result = await f.Service.CreateProductAsync(f.Actor.Id, Request(sku: " med-01 ", barcode: " 00123 "));
        Assert.Equal("MED-01", f.Product!.NormalizedSku);
        Assert.Equal("00123", f.Product.NormalizedBarcode);
        Assert.Equal("med-01", result.SKU);
        Assert.Contains(f.Audits, x => x.Action == "ProductCreated" && x.NewValues!.Contains("med-01"));
    }

    [Theory]
    [InlineData(-1, 10, 0, 0)]
    [InlineData(1, -1, 0, 0)]
    [InlineData(1, 10, 101, 0)]
    [InlineData(1, 10, 0, -1)]
    public async Task Invalid_product_numbers_are_rejected(decimal purchase, decimal retail, decimal discount, int reorder)
    {
        var f = Fixture(PermissionCatalog.ProductsCreate);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateProductAsync(f.Actor.Id,
            Request(purchase: purchase, retail: retail, discount: discount, reorder: reorder)));
    }

    [Fact]
    public async Task Duplicate_sku_returns_conflict()
    {
        var f = Fixture(PermissionCatalog.ProductsCreate); f.Repository.Setup(x => x.SkuExistsAsync("SKU-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.CreateProductAsync(f.Actor.Id, Request()));
    }

    [Fact]
    public async Task Duplicate_barcode_returns_conflict_but_null_is_allowed()
    {
        var f = Fixture(PermissionCatalog.ProductsCreate); f.Repository.Setup(x => x.BarcodeExistsAsync("123", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.CreateProductAsync(f.Actor.Id, Request(barcode: "123")));
        await f.Service.CreateProductAsync(f.Actor.Id, Request(barcode: null));
    }

    [Fact]
    public async Task Invalid_or_inactive_references_are_rejected()
    {
        var f = Fixture(PermissionCatalog.ProductsCreate); f.Category.IsActive = false;
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateProductAsync(f.Actor.Id, Request()));
    }

    [Fact]
    public async Task Update_preserves_sku_and_records_changed_values()
    {
        var f = Fixture(PermissionCatalog.ProductsUpdate); f.ExistingProduct();
        var result = await f.Service.UpdateProductAsync(f.Actor.Id, f.Product!.Id, Update(name: "Updated"));
        Assert.Equal("SKU-1", result.SKU); Assert.Equal("Updated", result.Name);
        Assert.Contains(f.Audits, x => x.Action == "ProductUpdated" && x.OldValues is not null && x.NewValues is not null);
    }

    [Theory]
    [InlineData(true, "products.activate", "ProductActivated")]
    [InlineData(false, "products.deactivate", "ProductDeactivated")]
    public async Task Product_status_actions_require_permission_and_audit(bool active, string permission, string action)
    {
        var f = Fixture(permission); f.ExistingProduct(); f.Product!.IsActive = !active;
        await f.Service.SetProductActiveAsync(f.Actor.Id, f.Product.Id, active);
        Assert.Equal(active, f.Product.IsActive); Assert.Contains(f.Audits, x => x.Action == action);
    }

    [Fact]
    public async Task Missing_permission_is_forbidden()
    {
        var f = Fixture();
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.CreateProductAsync(f.Actor.Id, Request()));
    }

    [Fact]
    public async Task Category_and_manufacturer_names_are_normalized_and_unique()
    {
        var f = Fixture(PermissionCatalog.CategoriesManage, PermissionCatalog.ManufacturersManage);
        await f.Service.CreateCategoryAsync(f.Actor.Id, new(" Tablets ", null));
        await f.Service.CreateManufacturerAsync(f.Actor.Id, new(" Acme Pharma ", "Acme", null, null, null, null, null));
        Assert.Equal("TABLETS", f.AddedCategory!.NormalizedName); Assert.Equal("ACME PHARMA", f.AddedManufacturer!.NormalizedName);
        f.Repository.Setup(x => x.CategoryNameExistsAsync("TABLETS", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.CreateCategoryAsync(f.Actor.Id, new("tablets", null)));
    }

    [Fact]
    public async Task List_validation_rejects_invalid_paging_and_sort()
    {
        var f = Fixture();
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ListProductsAsync(new(Page: 0)));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ListProductsAsync(new(SortBy: "stock")));
    }

    private static ProductRequest Request(string sku = "SKU-1", string? barcode = null, decimal purchase = 10, decimal retail = 12, decimal discount = 5, int reorder = 2) =>
        new("Panadol Extra", sku, barcode, "Paracetamol", "Panadol", CategoryId, ManufacturerId, "Box", 10, purchase, retail, 11, discount, reorder);
    private static ProductUpdateRequest Update(string name = "Panadol Extra") =>
        new(name, null, "Paracetamol", "Panadol", CategoryId, ManufacturerId, "Box", 10, 10, 12, 11, 5, 2);
    private static readonly Guid CategoryId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ManufacturerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static TestFixture Fixture(params string[] permissions) => new(permissions);

    private sealed class TestFixture
    {
        public TestFixture(IEnumerable<string> permissions)
        {
            var role = new Role { Name = "Manager" };
            foreach (var code in permissions) role.RolePermissions.Add(new RolePermission { Role = role, Permission = new Permission { Code = code, Description = code, Category = "test" } });
            Actor = new User { Username = "actor", NormalizedUsername = "ACTOR", FullName = "Actor", PasswordHash = "hash", BranchId = Guid.NewGuid(), RoleId = role.Id, Role = role };
            Category = new ProductCategory { Id = CategoryId, Name = "Tablets", NormalizedName = "TABLETS", IsActive = true };
            Manufacturer = new Manufacturer { Id = ManufacturerId, Name = "Acme", NormalizedName = "ACME", IsActive = true };
            Users.Setup(x => x.GetByIdAsync(Actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Actor);
            Repository.Setup(x => x.GetCategoryAsync(CategoryId, It.IsAny<CancellationToken>())).ReturnsAsync(Category);
            Repository.Setup(x => x.GetManufacturerAsync(ManufacturerId, It.IsAny<CancellationToken>())).ReturnsAsync(Manufacturer);
            Repository.Setup(x => x.AddProductAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>())).Callback<Product, CancellationToken>((x, _) => Product = x).Returns(Task.CompletedTask);
            Repository.Setup(x => x.AddCategoryAsync(It.IsAny<ProductCategory>(), It.IsAny<CancellationToken>())).Callback<ProductCategory, CancellationToken>((x, _) => AddedCategory = x).Returns(Task.CompletedTask);
            Repository.Setup(x => x.AddManufacturerAsync(It.IsAny<Manufacturer>(), It.IsAny<CancellationToken>())).Callback<Manufacturer, CancellationToken>((x, _) => AddedManufacturer = x).Returns(Task.CompletedTask);
            Repository.Setup(x => x.AddAuditAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>())).Callback<AuditLog, CancellationToken>((x, _) => Audits.Add(x)).Returns(Task.CompletedTask);
            Repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Service = new(Repository.Object, Users.Object, TimeProvider.System);
        }
        public readonly Mock<IProductMasterRepository> Repository = new(); public readonly Mock<IUserAccountRepository> Users = new();
        public readonly User Actor; public readonly ProductCategory Category; public readonly Manufacturer Manufacturer; public readonly ProductMasterService Service;
        public Product? Product; public ProductCategory? AddedCategory; public Manufacturer? AddedManufacturer; public List<AuditLog> Audits { get; } = [];
        public void ExistingProduct()
        {
            Product = new Product { Name = "Panadol", SKU = "SKU-1", NormalizedSku = "SKU-1", CategoryId = Category.Id, Category = Category, ManufacturerId = Manufacturer.Id, Manufacturer = Manufacturer, Unit = "Box", PackSize = 10, PurchasePrice = 10, RetailPrice = 12, MaximumDiscountPercent = 5, ReorderLevel = 2, IsActive = true };
            Repository.Setup(x => x.GetProductAsync(Product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Product);
        }
    }
}
