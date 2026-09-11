using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Services.Pricing;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

/// <summary>
/// Resolution order: an active quantity break at the customer's price level, then a generic
/// (level-less) quantity break, then a flat price-level price, then Default (caller falls back to
/// the product/batch retail price). A break is preferred over a flat level price because it is the
/// more specific match once a quantity is known.
/// </summary>
public sealed class PriceResolutionService(PharmacyDbContext context) : IPriceResolutionService
{
    public async Task<PriceResolutionResult> ResolveAsync(Guid? customerId, Guid productId, int quantity, Guid? explicitPriceLevelId = null, CancellationToken cancellationToken = default)
    {
        Guid? priceLevelId = explicitPriceLevelId;
        if (!priceLevelId.HasValue && customerId.HasValue)
        {
            priceLevelId = await context.Customers.AsNoTracking()
                .Where(x => x.Id == customerId.Value)
                .Select(x => x.PriceLevelId)
                .FirstOrDefaultAsync(cancellationToken);
        }
        if (priceLevelId.HasValue)
        {
            var levelActive = await context.PriceLevels.AsNoTracking()
                .AnyAsync(x => x.Id == priceLevelId.Value && x.IsActive, cancellationToken);
            if (!levelActive) priceLevelId = null;
        }

        var candidateBreaks = await context.ProductPriceBreaks.AsNoTracking()
            .Where(x => x.ProductId == productId && x.IsActive && x.MinimumQuantity <= quantity
                && (x.PriceLevelId == priceLevelId || x.PriceLevelId == null))
            .Select(x => new { x.Id, x.PriceLevelId, x.MinimumQuantity, x.SellingPrice })
            .ToListAsync(cancellationToken);
        var bestBreak = candidateBreaks
            .OrderBy(x => x.PriceLevelId == priceLevelId ? 0 : 1)
            .ThenByDescending(x => x.MinimumQuantity)
            .FirstOrDefault();
        if (bestBreak is not null)
            return new PriceResolutionResult(bestBreak.SellingPrice, PriceSource.QuantityBreak, priceLevelId, bestBreak.Id);

        if (priceLevelId.HasValue)
        {
            var levelPrice = await context.ProductPriceLevels.AsNoTracking()
                .Where(x => x.ProductId == productId && x.PriceLevelId == priceLevelId.Value && x.IsActive)
                .Select(x => (decimal?)x.SellingPrice)
                .FirstOrDefaultAsync(cancellationToken);
            if (levelPrice.HasValue)
                return new PriceResolutionResult(levelPrice, PriceSource.PriceLevel, priceLevelId, null);
        }

        return new PriceResolutionResult(null, PriceSource.Default, priceLevelId, null);
    }
}
