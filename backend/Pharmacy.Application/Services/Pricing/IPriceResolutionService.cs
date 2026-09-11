using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Pricing;

/// <summary>
/// Resolves the effective selling price for a product/customer/quantity combination. This is a
/// pure read used internally by Sales, Quotation, and SalesOrder services - it performs no
/// permission checks of its own (the calling action is already permission-gated), mirroring the
/// role IGodownAccessService plays for godown access checks. When no PriceLevel or quantity break
/// applies, Source is Default and Price is null: the caller must fall back to its own default
/// pricing (the product/batch retail price), preserving pre-Phase-3 behaviour exactly.
/// </summary>
public interface IPriceResolutionService
{
    /// <summary>
    /// Resolves a price. When explicitPriceLevelId is supplied it is used directly (e.g. staff
    /// picking a specific tier for one document); otherwise the customer's assigned price level is
    /// looked up.
    /// </summary>
    Task<PriceResolutionResult> ResolveAsync(Guid? customerId, Guid productId, int quantity, Guid? explicitPriceLevelId = null, CancellationToken cancellationToken = default);
}

public sealed record PriceResolutionResult(decimal? Price, PriceSource Source, Guid? PriceLevelId, Guid? MatchedPriceBreakId);
