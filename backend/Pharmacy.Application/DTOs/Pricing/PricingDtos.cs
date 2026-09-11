namespace Pharmacy.Application.DTOs.Pricing;

public sealed record PriceLevelRequest(string Name, string Code, int Priority, bool IsDefault, bool IsActive, Guid? BranchId = null);

public sealed record PriceLevelDto(Guid Id, string Name, string Code, int Priority, bool IsDefault, bool IsActive, Guid? BranchId, string? BranchName);

public sealed record ProductPriceLevelRequest(Guid ProductId, Guid PriceLevelId, decimal SellingPrice, bool IsActive = true);

public sealed record ProductPriceLevelDto(Guid Id, Guid ProductId, string ProductName, string SKU, Guid PriceLevelId, string PriceLevelName, decimal SellingPrice, bool IsActive);

public sealed record ProductPriceBreakRequest(Guid ProductId, Guid? PriceLevelId, int MinimumQuantity, decimal SellingPrice, bool IsActive = true);

public sealed record ProductPriceBreakDto(Guid Id, Guid ProductId, string ProductName, string SKU, Guid? PriceLevelId, string? PriceLevelName, int MinimumQuantity, decimal SellingPrice, bool IsActive);

public sealed record ResolvedPriceDto(decimal? Price, string Source, Guid? PriceLevelId, decimal? FallbackPrice);
