using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Catalog;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.Catalog;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class ProductMasterRepository(PharmacyDbContext context) : IProductMasterRepository
{
    public Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Products.Include(x => x.Category).Include(x => x.Manufacturer).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<ProductCategory?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default) => context.ProductCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Manufacturer?> GetManufacturerAsync(Guid id, CancellationToken cancellationToken = default) => context.Manufacturers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<bool> SkuExistsAsync(string normalizedSku, CancellationToken cancellationToken = default) => context.Products.AnyAsync(x => x.NormalizedSku == normalizedSku, cancellationToken);
    public Task<bool> BarcodeExistsAsync(string normalizedBarcode, Guid? excludingId = null, CancellationToken cancellationToken = default) => context.Products.AnyAsync(x => x.NormalizedBarcode == normalizedBarcode && (!excludingId.HasValue || x.Id != excludingId), cancellationToken);
    public Task<bool> CategoryNameExistsAsync(string normalizedName, Guid? excludingId = null, CancellationToken cancellationToken = default) => context.ProductCategories.AnyAsync(x => x.NormalizedName == normalizedName && (!excludingId.HasValue || x.Id != excludingId), cancellationToken);
    public Task<bool> ManufacturerNameExistsAsync(string normalizedName, Guid? excludingId = null, CancellationToken cancellationToken = default) => context.Manufacturers.AnyAsync(x => x.NormalizedName == normalizedName && (!excludingId.HasValue || x.Id != excludingId), cancellationToken);

    public async Task<PagedResult<ProductListItemDto>> ListProductsAsync(ProductListQuery query, CancellationToken cancellationToken = default)
    {
        var products = context.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            products = products.Where(x => EF.Functions.ILike(x.Name, pattern) || EF.Functions.ILike(x.SKU, pattern) ||
                (x.Barcode != null && EF.Functions.ILike(x.Barcode, pattern)) || (x.GenericName != null && EF.Functions.ILike(x.GenericName, pattern)) ||
                (x.BrandName != null && EF.Functions.ILike(x.BrandName, pattern)));
        }
        if (query.CategoryId.HasValue) products = products.Where(x => x.CategoryId == query.CategoryId);
        if (query.ManufacturerId.HasValue) products = products.Where(x => x.ManufacturerId == query.ManufacturerId);
        if (query.IsActive.HasValue) products = products.Where(x => x.IsActive == query.IsActive);
        var total = await products.CountAsync(cancellationToken);
        var sorted = (query.SortBy.ToLowerInvariant(), query.Descending) switch
        {
            ("sku", false) => products.OrderBy(x => x.SKU).ThenBy(x => x.Id),
            ("sku", true) => products.OrderByDescending(x => x.SKU).ThenBy(x => x.Id),
            ("createdat", false) => products.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            ("createdat", true) => products.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
            ("updatedat", false) => products.OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id),
            ("updatedat", true) => products.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id),
            ("retailprice", false) => products.OrderBy(x => x.RetailPrice).ThenBy(x => x.Name).ThenBy(x => x.Id),
            ("retailprice", true) => products.OrderByDescending(x => x.RetailPrice).ThenBy(x => x.Name).ThenBy(x => x.Id),
            (_, true) => products.OrderByDescending(x => x.Name).ThenBy(x => x.SKU).ThenBy(x => x.Id),
            _ => products.OrderBy(x => x.Name).ThenBy(x => x.SKU).ThenBy(x => x.Id)
        };
        var items = await sorted.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ProductListItemDto(x.Id, x.Name, x.SKU, x.Barcode, x.GenericName, x.BrandName,
                new CatalogLookupDto(x.CategoryId, x.Category!.Name, x.Category.IsActive),
                x.Manufacturer == null ? null : new CatalogLookupDto(x.Manufacturer.Id, x.Manufacturer.Name, x.Manufacturer.IsActive),
                x.Unit, x.RetailPrice, x.IsActive)).ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<ProductCategory>> ListCategoriesAsync(string? search, bool? active, CancellationToken cancellationToken = default)
    {
        var query = context.ProductCategories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => EF.Functions.ILike(x.Name, $"%{search.Trim()}%"));
        if (active.HasValue) query = query.Where(x => x.IsActive == active);
        return await query.OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<Manufacturer>> ListManufacturersAsync(string? search, bool? active, CancellationToken cancellationToken = default)
    {
        var query = context.Manufacturers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => EF.Functions.ILike(x.Name, $"%{search.Trim()}%") || x.ShortName != null && EF.Functions.ILike(x.ShortName, $"%{search.Trim()}%"));
        if (active.HasValue) query = query.Where(x => x.IsActive == active);
        return await query.OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    }
    public async Task AddProductAsync(Product product, CancellationToken cancellationToken = default) => await context.Products.AddAsync(product, cancellationToken);
    public async Task AddCategoryAsync(ProductCategory category, CancellationToken cancellationToken = default) => await context.ProductCategories.AddAsync(category, cancellationToken);
    public async Task AddManufacturerAsync(Manufacturer manufacturer, CancellationToken cancellationToken = default) => await context.Manufacturers.AddAsync(manufacturer, cancellationToken);
    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw pg.ConstraintName switch
            {
                "IX_Products_NormalizedSku" => new ResourceConflictException("A product with this SKU already exists."),
                "IX_Products_NormalizedBarcode" => new ResourceConflictException("This barcode is already assigned to another product."),
                "IX_ProductCategories_NormalizedName" => new ResourceConflictException("A category with this name already exists."),
                "IX_Manufacturers_NormalizedName" => new ResourceConflictException("A manufacturer with this name already exists."),
                _ => new ResourceConflictException("A catalog record with the same unique value already exists.")
            };
        }
    }
}
