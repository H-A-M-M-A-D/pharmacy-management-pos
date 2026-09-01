using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Catalog;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Auth;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Catalog;

public sealed class ProductMasterService(
    IProductMasterRepository repository,
    IUserAccountRepository users,
    TimeProvider timeProvider) : IProductMasterService
{
    public Task<PagedResult<ProductListItemDto>> ListProductsAsync(ProductListQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
            throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
        if (!new[] { "name", "sku", "createdat", "updatedat", "retailprice" }.Contains(query.SortBy.ToLowerInvariant()))
            throw new RequestValidationException("The requested product sort is not supported.");
        return repository.ListProductsAsync(query, cancellationToken);
    }

    public async Task<ProductDetailsDto> GetProductAsync(Guid id, CancellationToken cancellationToken = default) =>
        Map(await RequiredProduct(id, cancellationToken));

    public async Task<ProductDetailsDto> CreateProductAsync(Guid actorId, ProductRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.ProductsCreate, cancellationToken);
        CatalogValidation.Sku(request.SKU);
        CatalogValidation.Product(request.Name, request.Unit, request.PackSize, request.PurchasePrice,
            request.RetailPrice, request.TradePrice, request.MaximumDiscountPercent, request.ReorderLevel);
        var sku = CatalogValidation.Normalize(request.SKU);
        var barcode = CatalogValidation.NormalizeOptional(request.Barcode);
        if (await repository.SkuExistsAsync(sku, cancellationToken))
            throw new ResourceConflictException("A product with this SKU already exists.");
        if (barcode is not null && await repository.BarcodeExistsAsync(barcode, null, cancellationToken))
            throw new ResourceConflictException("This barcode is already assigned to another product.");
        var (category, manufacturer) = await References(request.CategoryId, request.ManufacturerId, true, cancellationToken);
        var product = new Product
        {
            Name = request.Name.Trim(), SKU = request.SKU.Trim(), NormalizedSku = sku,
            Barcode = Clean(request.Barcode), NormalizedBarcode = barcode,
            GenericName = Clean(request.GenericName), BrandName = Clean(request.BrandName),
            CategoryId = category.Id, Category = category, ManufacturerId = manufacturer?.Id, Manufacturer = manufacturer,
            Unit = request.Unit.Trim(), PackSize = request.PackSize, PurchasePrice = request.PurchasePrice,
            RetailPrice = request.RetailPrice, TradePrice = request.TradePrice,
            MaximumDiscountPercent = request.MaximumDiscountPercent, ReorderLevel = request.ReorderLevel,
            IsActive = request.IsActive
        };
        await repository.AddProductAsync(product, cancellationToken);
        await Audit(actorId, "ProductCreated", "Product", product.Id, null, ProductValues(product), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(product);
    }

    public async Task<ProductDetailsDto> UpdateProductAsync(Guid actorId, Guid id, ProductUpdateRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.ProductsUpdate, cancellationToken);
        CatalogValidation.Product(request.Name, request.Unit, request.PackSize, request.PurchasePrice,
            request.RetailPrice, request.TradePrice, request.MaximumDiscountPercent, request.ReorderLevel);
        var product = await RequiredProduct(id, cancellationToken);
        var barcode = CatalogValidation.NormalizeOptional(request.Barcode);
        if (barcode is not null && await repository.BarcodeExistsAsync(barcode, id, cancellationToken))
            throw new ResourceConflictException("This barcode is already assigned to another product.");
        var (category, manufacturer) = await References(request.CategoryId, request.ManufacturerId, true, cancellationToken);
        var old = ProductValues(product);
        product.Name = request.Name.Trim(); product.Barcode = Clean(request.Barcode); product.NormalizedBarcode = barcode;
        product.GenericName = Clean(request.GenericName); product.BrandName = Clean(request.BrandName);
        product.CategoryId = category.Id; product.Category = category;
        product.ManufacturerId = manufacturer?.Id; product.Manufacturer = manufacturer;
        product.Unit = request.Unit.Trim(); product.PackSize = request.PackSize;
        product.PurchasePrice = request.PurchasePrice; product.RetailPrice = request.RetailPrice;
        product.TradePrice = request.TradePrice; product.MaximumDiscountPercent = request.MaximumDiscountPercent;
        product.ReorderLevel = request.ReorderLevel; product.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await Audit(actorId, "ProductUpdated", "Product", id, old, ProductValues(product), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(product);
    }

    public async Task SetProductActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default)
    {
        await Require(actorId, active ? PermissionCatalog.ProductsActivate : PermissionCatalog.ProductsDeactivate, cancellationToken);
        var product = await RequiredProduct(id, cancellationToken);
        if (product.IsActive == active) return;
        var old = new { product.IsActive }; product.IsActive = active; product.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await Audit(actorId, active ? "ProductActivated" : "ProductDeactivated", "Product", id, old, new { product.IsActive }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(string? search, bool? active, CancellationToken cancellationToken = default) =>
        (await repository.ListCategoriesAsync(search, active, cancellationToken)).Select(Map).ToList();
    public async Task<CategoryDto> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default) => Map(await RequiredCategory(id, cancellationToken));
    public async Task<CategoryDto> CreateCategoryAsync(Guid actorId, CategoryRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.CategoriesManage, cancellationToken); CatalogValidation.Name(request.Name, "Category name");
        var normalized = CatalogValidation.Normalize(request.Name);
        if (await repository.CategoryNameExistsAsync(normalized, null, cancellationToken)) throw new ResourceConflictException("A category with this name already exists.");
        var entity = new ProductCategory { Name = request.Name.Trim(), NormalizedName = normalized, Description = Clean(request.Description), IsActive = request.IsActive };
        await repository.AddCategoryAsync(entity, cancellationToken); await Audit(actorId, "CategoryCreated", "ProductCategory", entity.Id, null, CategoryValues(entity), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken); return Map(entity);
    }
    public async Task<CategoryDto> UpdateCategoryAsync(Guid actorId, Guid id, CategoryRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.CategoriesManage, cancellationToken); CatalogValidation.Name(request.Name, "Category name");
        var entity = await RequiredCategory(id, cancellationToken); var normalized = CatalogValidation.Normalize(request.Name);
        if (await repository.CategoryNameExistsAsync(normalized, id, cancellationToken)) throw new ResourceConflictException("A category with this name already exists.");
        var old = CategoryValues(entity); entity.Name = request.Name.Trim(); entity.NormalizedName = normalized; entity.Description = Clean(request.Description); entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await Audit(actorId, "CategoryUpdated", "ProductCategory", id, old, CategoryValues(entity), cancellationToken); await repository.SaveChangesAsync(cancellationToken); return Map(entity);
    }
    public async Task SetCategoryActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.CategoriesManage, cancellationToken); var entity = await RequiredCategory(id, cancellationToken);
        if (entity.IsActive == active) return; entity.IsActive = active; entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await Audit(actorId, active ? "CategoryActivated" : "CategoryDeactivated", "ProductCategory", id, null, new { entity.IsActive }, cancellationToken); await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ManufacturerDto>> ListManufacturersAsync(string? search, bool? active, CancellationToken cancellationToken = default) =>
        (await repository.ListManufacturersAsync(search, active, cancellationToken)).Select(Map).ToList();
    public async Task<ManufacturerDto> GetManufacturerAsync(Guid id, CancellationToken cancellationToken = default) => Map(await RequiredManufacturer(id, cancellationToken));
    public async Task<ManufacturerDto> CreateManufacturerAsync(Guid actorId, ManufacturerRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.ManufacturersManage, cancellationToken); CatalogValidation.Name(request.Name, "Manufacturer name");
        var normalized = CatalogValidation.Normalize(request.Name);
        if (await repository.ManufacturerNameExistsAsync(normalized, null, cancellationToken)) throw new ResourceConflictException("A manufacturer with this name already exists.");
        var entity = NewManufacturer(request, normalized); await repository.AddManufacturerAsync(entity, cancellationToken);
        await Audit(actorId, "ManufacturerCreated", "Manufacturer", entity.Id, null, ManufacturerValues(entity), cancellationToken); await repository.SaveChangesAsync(cancellationToken); return Map(entity);
    }
    public async Task<ManufacturerDto> UpdateManufacturerAsync(Guid actorId, Guid id, ManufacturerRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.ManufacturersManage, cancellationToken); CatalogValidation.Name(request.Name, "Manufacturer name");
        var entity = await RequiredManufacturer(id, cancellationToken); var normalized = CatalogValidation.Normalize(request.Name);
        if (await repository.ManufacturerNameExistsAsync(normalized, id, cancellationToken)) throw new ResourceConflictException("A manufacturer with this name already exists.");
        var old = ManufacturerValues(entity); var active = entity.IsActive; Apply(entity, request, normalized); entity.IsActive = active; entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await Audit(actorId, "ManufacturerUpdated", "Manufacturer", id, old, ManufacturerValues(entity), cancellationToken); await repository.SaveChangesAsync(cancellationToken); return Map(entity);
    }
    public async Task SetManufacturerActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.ManufacturersManage, cancellationToken); var entity = await RequiredManufacturer(id, cancellationToken);
        if (entity.IsActive == active) return; entity.IsActive = active; entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await Audit(actorId, active ? "ManufacturerActivated" : "ManufacturerDeactivated", "Manufacturer", id, null, new { entity.IsActive }, cancellationToken); await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductMasterOptionsDto> GetOptionsAsync(CancellationToken cancellationToken = default) => new(
        (await repository.ListCategoriesAsync(null, true, cancellationToken)).Select(x => new CatalogLookupDto(x.Id, x.Name, x.IsActive)).ToList(),
        (await repository.ListManufacturersAsync(null, true, cancellationToken)).Select(x => new CatalogLookupDto(x.Id, x.Name, x.IsActive)).ToList(),
        ["Box", "Pack", "Strip", "Tablet", "Capsule", "Bottle", "Tube", "Sachet", "Piece", "Vial", "Ampoule"]);

    private async Task Require(Guid actorId, string permission, CancellationToken cancellationToken)
    {
        var actor = await users.GetByIdAsync(actorId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
    }
    private async Task<Product> RequiredProduct(Guid id, CancellationToken ct) => await repository.GetProductAsync(id, ct) ?? throw new ResourceNotFoundException("Product was not found.");
    private async Task<ProductCategory> RequiredCategory(Guid id, CancellationToken ct) => await repository.GetCategoryAsync(id, ct) ?? throw new ResourceNotFoundException("Category was not found.");
    private async Task<Manufacturer> RequiredManufacturer(Guid id, CancellationToken ct) => await repository.GetManufacturerAsync(id, ct) ?? throw new ResourceNotFoundException("Manufacturer was not found.");
    private async Task<(ProductCategory, Manufacturer?)> References(Guid categoryId, Guid? manufacturerId, bool requireActive, CancellationToken ct)
    {
        var category = await repository.GetCategoryAsync(categoryId, ct);
        var manufacturer = manufacturerId.HasValue ? await repository.GetManufacturerAsync(manufacturerId.Value, ct) : null;
        if (category is null || (requireActive && !category.IsActive) || (manufacturerId.HasValue && (manufacturer is null || requireActive && !manufacturer.IsActive)))
            throw new RequestValidationException("Category or manufacturer is invalid or inactive.");
        return (category, manufacturer);
    }
    private Task Audit(Guid actor, string action, string type, Guid id, object? old, object? current, CancellationToken ct) => repository.AddAuditAsync(new AuditLog
    { UserId = actor, Action = action, EntityType = type, EntityId = id, OldValues = old is null ? null : JsonSerializer.Serialize(old), NewValues = current is null ? null : JsonSerializer.Serialize(current) }, ct);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static object ProductValues(Product x) => new { x.Name, x.SKU, x.Barcode, x.GenericName, x.BrandName, x.CategoryId, x.ManufacturerId, x.Unit, x.PackSize, x.PurchasePrice, x.RetailPrice, x.TradePrice, x.MaximumDiscountPercent, x.ReorderLevel, x.IsActive };
    private static object CategoryValues(ProductCategory x) => new { x.Name, x.Description, x.IsActive };
    private static object ManufacturerValues(Manufacturer x) => new { x.Name, x.ShortName, x.PhoneNumber, x.Email, x.Website, x.Country, x.Address, x.IsActive };
    private static ProductDetailsDto Map(Product x) => new(x.Id, x.Name, x.SKU, x.Barcode, x.GenericName, x.BrandName, new(x.CategoryId, x.Category!.Name, x.Category.IsActive), x.Manufacturer is null ? null : new(x.Manufacturer.Id, x.Manufacturer.Name, x.Manufacturer.IsActive), x.Unit, x.PackSize, x.PurchasePrice, x.RetailPrice, x.TradePrice, x.MaximumDiscountPercent, x.ReorderLevel, x.IsActive, x.CreatedAt, x.UpdatedAt);
    private static CategoryDto Map(ProductCategory x) => new(x.Id, x.Name, x.Description, x.IsActive, x.CreatedAt, x.UpdatedAt);
    private static ManufacturerDto Map(Manufacturer x) => new(x.Id, x.Name, x.ShortName, x.PhoneNumber, x.Email, x.Website, x.Country, x.Address, x.IsActive, x.CreatedAt, x.UpdatedAt);
    private static Manufacturer NewManufacturer(ManufacturerRequest r, string normalized) { var x = new Manufacturer { Name = r.Name.Trim(), NormalizedName = normalized }; Apply(x, r, normalized); return x; }
    private static void Apply(Manufacturer x, ManufacturerRequest r, string normalized) { x.Name = r.Name.Trim(); x.NormalizedName = normalized; x.ShortName = Clean(r.ShortName); x.PhoneNumber = Clean(r.PhoneNumber); x.Email = Clean(r.Email); x.Website = Clean(r.Website); x.Country = Clean(r.Country); x.Address = Clean(r.Address); x.IsActive = r.IsActive; }
}
