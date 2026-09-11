using Pharmacy.Application.DTOs.Pricing;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Pricing;

public interface IPricingRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<PriceLevel?> GetPriceLevelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> PriceLevelCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PriceLevelDto>> ListPriceLevelsAsync(bool activeOnly, CancellationToken cancellationToken = default);
    Task AddPriceLevelAsync(PriceLevel level, CancellationToken cancellationToken = default);
    Task ClearDefaultPriceLevelAsync(Guid? excludingId, CancellationToken cancellationToken = default);

    Task<ProductPriceLevel?> GetProductPriceLevelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductPriceLevel?> FindProductPriceLevelAsync(Guid productId, Guid priceLevelId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductPriceLevelDto>> ListProductPriceLevelsAsync(Guid? productId, Guid? priceLevelId, CancellationToken cancellationToken = default);
    Task AddProductPriceLevelAsync(ProductPriceLevel entry, CancellationToken cancellationToken = default);
    Task RemoveProductPriceLevelAsync(ProductPriceLevel entry, CancellationToken cancellationToken = default);

    Task<ProductPriceBreak?> GetProductPriceBreakAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductPriceBreak?> FindProductPriceBreakAsync(Guid productId, Guid? priceLevelId, int minimumQuantity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductPriceBreakDto>> ListProductPriceBreaksAsync(Guid? productId, CancellationToken cancellationToken = default);
    Task AddProductPriceBreakAsync(ProductPriceBreak entry, CancellationToken cancellationToken = default);
    Task RemoveProductPriceBreakAsync(ProductPriceBreak entry, CancellationToken cancellationToken = default);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
