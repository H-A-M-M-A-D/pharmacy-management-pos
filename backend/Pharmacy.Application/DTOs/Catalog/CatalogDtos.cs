namespace Pharmacy.Application.DTOs.Catalog;

public sealed record CatalogLookupDto(Guid Id, string Name, bool IsActive);

public sealed record ProductRequest(
    string Name, string SKU, string? Barcode, string? GenericName, string? BrandName,
    Guid CategoryId, Guid? ManufacturerId, string Unit, int PackSize,
    decimal PurchasePrice, decimal RetailPrice, decimal? TradePrice,
    decimal MaximumDiscountPercent, int ReorderLevel, bool IsActive = true);

public sealed record ProductUpdateRequest(
    string Name, string? Barcode, string? GenericName, string? BrandName,
    Guid CategoryId, Guid? ManufacturerId, string Unit, int PackSize,
    decimal PurchasePrice, decimal RetailPrice, decimal? TradePrice,
    decimal MaximumDiscountPercent, int ReorderLevel);

public sealed record ProductListQuery(
    int Page = 1, int PageSize = 25, string? Search = null,
    Guid? CategoryId = null, Guid? ManufacturerId = null, bool? IsActive = null,
    string SortBy = "name", bool Descending = false);

public sealed record ProductListItemDto(
    Guid Id, string Name, string SKU, string? Barcode, string? GenericName,
    string? BrandName, CatalogLookupDto Category, CatalogLookupDto? Manufacturer,
    string Unit, decimal RetailPrice, bool IsActive);

public sealed record ProductDetailsDto(
    Guid Id, string Name, string SKU, string? Barcode, string? GenericName,
    string? BrandName, CatalogLookupDto Category, CatalogLookupDto? Manufacturer,
    string Unit, int PackSize, decimal PurchasePrice, decimal RetailPrice,
    decimal? TradePrice, decimal MaximumDiscountPercent, int ReorderLevel,
    bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record CategoryRequest(string Name, string? Description, bool IsActive = true);
public sealed record CategoryDto(
    Guid Id, string Name, string? Description, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record ManufacturerRequest(
    string Name, string? ShortName, string? PhoneNumber, string? Email,
    string? Website, string? Country, string? Address, bool IsActive = true);
public sealed record ManufacturerDto(
    Guid Id, string Name, string? ShortName, string? PhoneNumber, string? Email,
    string? Website, string? Country, string? Address, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record ProductMasterOptionsDto(
    IReadOnlyList<CatalogLookupDto> Categories,
    IReadOnlyList<CatalogLookupDto> Manufacturers,
    IReadOnlyList<string> Units);
