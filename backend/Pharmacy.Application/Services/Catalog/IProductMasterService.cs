using Pharmacy.Application.DTOs.Catalog;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.Catalog;

public interface IProductMasterService
{
    Task<PagedResult<ProductListItemDto>> ListProductsAsync(ProductListQuery query, CancellationToken cancellationToken = default);
    Task<ProductDetailsDto> GetProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDetailsDto> CreateProductAsync(Guid actorId, ProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductDetailsDto> UpdateProductAsync(Guid actorId, Guid id, ProductUpdateRequest request, CancellationToken cancellationToken = default);
    Task SetProductActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(string? search, bool? active, CancellationToken cancellationToken = default);
    Task<CategoryDto> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateCategoryAsync(Guid actorId, CategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateCategoryAsync(Guid actorId, Guid id, CategoryRequest request, CancellationToken cancellationToken = default);
    Task SetCategoryActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManufacturerDto>> ListManufacturersAsync(string? search, bool? active, CancellationToken cancellationToken = default);
    Task<ManufacturerDto> GetManufacturerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ManufacturerDto> CreateManufacturerAsync(Guid actorId, ManufacturerRequest request, CancellationToken cancellationToken = default);
    Task<ManufacturerDto> UpdateManufacturerAsync(Guid actorId, Guid id, ManufacturerRequest request, CancellationToken cancellationToken = default);
    Task SetManufacturerActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default);
    Task<ProductMasterOptionsDto> GetOptionsAsync(CancellationToken cancellationToken = default);
}
