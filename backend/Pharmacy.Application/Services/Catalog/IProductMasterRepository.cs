using Pharmacy.Application.DTOs.Catalog;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Catalog;

public interface IProductMasterRepository
{
    Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductCategory?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Manufacturer?> GetManufacturerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SkuExistsAsync(string normalizedSku, CancellationToken cancellationToken = default);
    Task<bool> BarcodeExistsAsync(string normalizedBarcode, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task<bool> CategoryNameExistsAsync(string normalizedName, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task<bool> ManufacturerNameExistsAsync(string normalizedName, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task<PagedResult<ProductListItemDto>> ListProductsAsync(ProductListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductCategory>> ListCategoriesAsync(string? search, bool? active, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Manufacturer>> ListManufacturersAsync(string? search, bool? active, CancellationToken cancellationToken = default);
    Task AddProductAsync(Product product, CancellationToken cancellationToken = default);
    Task AddCategoryAsync(ProductCategory category, CancellationToken cancellationToken = default);
    Task AddManufacturerAsync(Manufacturer manufacturer, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
