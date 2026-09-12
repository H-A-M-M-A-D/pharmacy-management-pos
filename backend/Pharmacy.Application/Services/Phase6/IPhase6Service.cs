using Pharmacy.Application.DTOs.Phase6;

namespace Pharmacy.Application.Services.Phase6;

public interface IPhase6Service
{
    Task<Phase6PricingOptions> PricingOptionsAsync(Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<Phase6OptionDto>> ReorderSuppliersAsync(Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseCostSuggestionDto>> ListPurchaseCostSuggestionsAsync(Guid actorId, CancellationToken ct = default);
    Task<int> AcceptPurchaseCostSuggestionAsync(Guid actorId, Guid id, bool confirmed, CancellationToken ct = default);
    Task<IReadOnlyList<ExpiryDiscountSuggestionDto>> ListExpiryDiscountSuggestionsAsync(Guid actorId, decimal discountPercent, CancellationToken ct = default);
    Task<Pharmacy.Application.DTOs.Pricing.ResolvedPriceDto> ResolveSalePriceAsync(Guid actorId, Guid? customerId, Guid productId, int quantity, Pharmacy.Domain.Entities.SaleType saleType, CancellationToken ct = default);
    Task<BulkPricingPreview> PreviewBulkPricingAsync(Guid actorId, BulkPricingRequest request, CancellationToken ct = default);
    Task<int> ApplyBulkPricingAsync(Guid actorId, BulkPricingApplyRequest request, CancellationToken ct = default);
    Task<ReorderDraftResult> CreateReorderDraftsAsync(Guid actorId, ReorderDraftRequest request, CancellationToken ct = default);
    Task<Phase6Thresholds> GetThresholdsAsync(Guid actorId, CancellationToken ct = default);
    Task<Phase6Thresholds> SaveThresholdsAsync(Guid actorId, Phase6Thresholds request, CancellationToken ct = default);
    Task<IReadOnlyList<SlowStockDto>> ListSlowStockAsync(Guid actorId, decimal minimumStockValue, CancellationToken ct = default);
    Task<IReadOnlyList<PricingRuleDto>> ListPricingRulesAsync(Guid actorId, bool activeOnly, CancellationToken ct = default);
    Task<PricingRuleDto> SavePricingRuleAsync(Guid actorId, Guid? id, PricingRuleRequest request, CancellationToken ct = default);
    Task<PricingSuggestionDto> SuggestPriceAsync(Guid actorId, PricingSuggestionRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ReorderSuggestionDto>> ListReorderSuggestionsAsync(Guid actorId, Guid? branchId, Guid? godownId, CancellationToken ct = default);
    Task<IReadOnlyList<BusinessAlertDto>> ListAlertsAsync(Guid actorId, bool includeResolved, CancellationToken ct = default);
    Task<int> RefreshAlertsAsync(Guid actorId, CancellationToken ct = default);
    Task DismissAlertAsync(Guid actorId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AutomationRuleDto>> ListAutomationRulesAsync(Guid actorId, CancellationToken ct = default);
    Task<AutomationRuleDto> SaveAutomationRuleAsync(Guid actorId, Guid? id, AutomationRuleRequest request, CancellationToken ct = default);
    Task<AutomationRunResult> RunAutomationAsync(Guid actorId, CancellationToken ct = default);
}
