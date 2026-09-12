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
        => await ResolveCoreAsync(customerId, productId, quantity, explicitPriceLevelId, null, null, null, cancellationToken);

    public async Task<PriceResolutionResult> ResolveForContextAsync(Guid? customerId, Guid productId, int quantity,
        Guid? explicitPriceLevelId = null, Guid? branchId = null, SaleType? saleType = null,
        DateTime? atUtc = null, CancellationToken cancellationToken = default) =>
        await ResolveCoreAsync(customerId, productId, quantity, explicitPriceLevelId, branchId, saleType, atUtc, cancellationToken);

    private async Task<PriceResolutionResult> ResolveCoreAsync(Guid? customerId, Guid productId, int quantity,
        Guid? explicitPriceLevelId, Guid? branchId, SaleType? saleType, DateTime? atUtc, CancellationToken cancellationToken)
    {
        var product = await context.Products.AsNoTracking().Where(x => x.Id == productId)
            .Select(x => new { x.RetailPrice, x.PurchasePrice, x.CategoryId, x.ManufacturerId }).SingleAsync(cancellationToken);
        var now = atUtc ?? DateTime.UtcNow;
        CustomerType? customerType = null;
        Guid? priceLevelId = explicitPriceLevelId;
        if (customerId.HasValue)
        {
            var customer = await context.Customers.AsNoTracking()
                .Where(x => x.Id == customerId.Value)
                .Select(x => new { x.PriceLevelId, x.CustomerType })
                .FirstOrDefaultAsync(cancellationToken);
            priceLevelId ??= customer?.PriceLevelId;
            customerType = customer?.CustomerType;
        }
        if (priceLevelId.HasValue)
        {
            var levelActive = await context.PriceLevels.AsNoTracking()
                .AnyAsync(x => x.Id == priceLevelId.Value && x.IsActive && (x.BranchId == null || x.BranchId == branchId || branchId == null), cancellationToken);
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
        decimal? levelPrice = null;
        if (priceLevelId.HasValue)
        {
            levelPrice = await context.ProductPriceLevels.AsNoTracking()
                .Where(x => x.ProductId == productId && x.PriceLevelId == priceLevelId.Value && x.IsActive)
                .Select(x => (decimal?)x.SellingPrice)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var rules = await context.PricingRules.AsNoTracking()
            .Where(x => x.IsActive && (x.BranchId == null || x.BranchId == branchId)
                && (x.CustomerId == null || x.CustomerId == customerId)
                && (x.ProductId == null || x.ProductId == productId)
                && (x.CategoryId == null || x.CategoryId == product.CategoryId)
                && (x.ManufacturerId == null || x.ManufacturerId == product.ManufacturerId)
                && (x.CustomerType == null || x.CustomerType == customerType)
                && (x.PriceLevelId == null || x.PriceLevelId == priceLevelId)
                && (x.SaleType == null || x.SaleType == saleType)
                && (x.MinimumQuantity == null || x.MinimumQuantity <= quantity)
                && (x.StartsAtUtc == null || x.StartsAtUtc <= now)
                && (x.EndsAtUtc == null || x.EndsAtUtc > now))
            .ToListAsync(cancellationToken);
        var rule = rules.OrderByDescending(x => Specificity(x, customerId, productId, priceLevelId, branchId))
            .ThenByDescending(x => x.Priority).ThenByDescending(x => x.CreatedAt).ThenBy(x => x.Id).FirstOrDefault();
        if (rule is null) return bestBreak != null ? new(bestBreak.SellingPrice, PriceSource.QuantityBreak, priceLevelId, bestBreak.Id)
            : levelPrice.HasValue ? new(levelPrice, PriceSource.PriceLevel, priceLevelId, null) : new(null, PriceSource.Default, priceLevelId, null);
        var basePrice = bestBreak?.SellingPrice ?? levelPrice ?? product.RetailPrice;
        var adjusted = rule.AdjustmentType switch
        {
            PricingAdjustmentType.FixedPrice => rule.AdjustmentValue,
            PricingAdjustmentType.PercentageDiscount => basePrice * (1m - rule.AdjustmentValue / 100m),
            PricingAdjustmentType.FixedDiscount => basePrice - rule.AdjustmentValue,
            _ => basePrice
        };
        adjusted = decimal.Round(Math.Max(product.PurchasePrice, Math.Max(0m, adjusted)), 2, MidpointRounding.AwayFromZero);
        return new PriceResolutionResult(adjusted,
            rule.Kind == PricingRuleKind.Promotion ? PriceSource.Promotion : PriceSource.PricingRule,
            priceLevelId, null, rule.Id);
    }

    private static int Specificity(PricingRule rule, Guid? customerId, Guid productId, Guid? priceLevelId, Guid? branchId) =>
        (rule.CustomerId == customerId && customerId.HasValue ? 64 : 0)
        + (rule.ProductId == productId && rule.ProductId.HasValue ? 32 : 0)
        + (rule.PriceLevelId == priceLevelId && rule.PriceLevelId.HasValue ? 16 : 0)
        + (rule.CategoryId.HasValue ? 8 : 0) + (rule.ManufacturerId.HasValue ? 4 : 0)
        + (rule.MinimumQuantity.HasValue ? 2 : 0) + (rule.BranchId == branchId && branchId.HasValue ? 1 : 0);
}
