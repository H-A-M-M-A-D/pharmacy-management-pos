using Pharmacy.Application.DTOs.Pricing;

namespace Pharmacy.Application.Services.Pricing;

public interface IPricingService
{
    Task<IReadOnlyList<PriceLevelDto>> ListPriceLevelsAsync(Guid actorId, bool activeOnly, CancellationToken cancellationToken = default);
    Task<PriceLevelDto> CreatePriceLevelAsync(Guid actorId, PriceLevelRequest request, CancellationToken cancellationToken = default);
    Task<PriceLevelDto> UpdatePriceLevelAsync(Guid actorId, Guid id, PriceLevelRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductPriceLevelDto>> ListProductPricesAsync(Guid actorId, Guid? productId, Guid? priceLevelId, CancellationToken cancellationToken = default);
    Task<ProductPriceLevelDto> SetProductPriceAsync(Guid actorId, ProductPriceLevelRequest request, CancellationToken cancellationToken = default);
    Task RemoveProductPriceAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductPriceBreakDto>> ListProductPriceBreaksAsync(Guid actorId, Guid? productId, CancellationToken cancellationToken = default);
    Task<ProductPriceBreakDto> SetProductPriceBreakAsync(Guid actorId, ProductPriceBreakRequest request, CancellationToken cancellationToken = default);
    Task RemoveProductPriceBreakAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);

    Task<ResolvedPriceDto> PreviewResolvedPriceAsync(Guid actorId, Guid? customerId, Guid productId, int quantity, CancellationToken cancellationToken = default);
}
