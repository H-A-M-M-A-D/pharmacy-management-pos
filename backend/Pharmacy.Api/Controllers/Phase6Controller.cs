using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Phase6;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Phase6;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/phase6")]
public sealed class Phase6Controller(IPhase6Service service) : ControllerBase
{
    [HttpGet("pricing-options"), HasPermission(PermissionCatalog.PricingManage)]
    public Task<Phase6PricingOptions> PricingOptions(CancellationToken ct) => service.PricingOptionsAsync(UserId(), ct);
    [HttpGet("reorder/suppliers"), HasPermission(PermissionCatalog.PurchaseOrdersCreate)]
    public Task<IReadOnlyList<Phase6OptionDto>> ReorderSuppliers(CancellationToken ct) => service.ReorderSuppliersAsync(UserId(), ct);
    [HttpGet("purchase-cost-suggestions"), HasPermission(PermissionCatalog.PricingSuggest)]
    public Task<IReadOnlyList<PurchaseCostSuggestionDto>> PurchaseCostSuggestions(CancellationToken ct) => service.ListPurchaseCostSuggestionsAsync(UserId(), ct);
    [HttpPost("purchase-cost-suggestions/{id:guid}/accept"), HasPermission(PermissionCatalog.PricingManage)]
    public Task<int> AcceptPurchaseCostSuggestion(Guid id, AcceptSuggestionRequest request, CancellationToken ct) => service.AcceptPurchaseCostSuggestionAsync(UserId(), id, request.Confirmed, ct);
    [HttpGet("expiry-discount-suggestions"), HasPermission(PermissionCatalog.PricingSuggest)]
    public Task<IReadOnlyList<ExpiryDiscountSuggestionDto>> ExpiryDiscountSuggestions([FromQuery] decimal discountPercent = 10, CancellationToken ct = default) => service.ListExpiryDiscountSuggestionsAsync(UserId(), discountPercent, ct);
    [HttpGet("sale-price"), HasPermission(PermissionCatalog.SalesCreate)]
    public Task<Pharmacy.Application.DTOs.Pricing.ResolvedPriceDto> SalePrice([FromQuery] Guid? customerId, [FromQuery] Guid productId, [FromQuery] int quantity, [FromQuery] Pharmacy.Domain.Entities.SaleType saleType, CancellationToken ct) => service.ResolveSalePriceAsync(UserId(), customerId, productId, quantity, saleType, ct);
    [HttpGet("thresholds"), HasPermission(PermissionCatalog.AlertsView)]
    public Task<Phase6Thresholds> Thresholds(CancellationToken ct) => service.GetThresholdsAsync(UserId(), ct);
    [HttpPut("thresholds"), HasPermission(PermissionCatalog.SystemSettingsManage)]
    public Task<Phase6Thresholds> SaveThresholds(Phase6Thresholds request, CancellationToken ct) => service.SaveThresholdsAsync(UserId(), request, ct);
    [HttpGet("slow-stock"), HasPermission(PermissionCatalog.AlertsView)]
    public Task<IReadOnlyList<SlowStockDto>> SlowStock([FromQuery] decimal minimumStockValue = 0, CancellationToken ct = default) => service.ListSlowStockAsync(UserId(), minimumStockValue, ct);
    [HttpPost("bulk-pricing/preview"), HasPermission(PermissionCatalog.PricingManage)]
    public Task<BulkPricingPreview> PreviewBulkPricing(BulkPricingRequest request, CancellationToken ct) => service.PreviewBulkPricingAsync(UserId(), request, ct);

    [HttpPost("bulk-pricing/apply"), HasPermission(PermissionCatalog.PricingManage)]
    public Task<int> ApplyBulkPricing(BulkPricingApplyRequest request, CancellationToken ct) => service.ApplyBulkPricingAsync(UserId(), request, ct);

    [HttpPost("reorder/drafts"), HasPermission(PermissionCatalog.PurchaseOrdersCreate)]
    public Task<ReorderDraftResult> CreateReorderDrafts(ReorderDraftRequest request, CancellationToken ct) => service.CreateReorderDraftsAsync(UserId(), request, ct);
    [HttpGet("pricing-rules"), HasPermission(PermissionCatalog.PricingView)]
    public Task<IReadOnlyList<PricingRuleDto>> PricingRules([FromQuery] bool activeOnly = true, CancellationToken ct = default) => service.ListPricingRulesAsync(UserId(), activeOnly, ct);

    [HttpPost("pricing-rules"), HasPermission(PermissionCatalog.PricingManage)]
    public Task<PricingRuleDto> SavePricingRule(PricingRuleRequest request, CancellationToken ct) => service.SavePricingRuleAsync(UserId(), null, request, ct);

    [HttpPut("pricing-rules/{id:guid}"), HasPermission(PermissionCatalog.PricingManage)]
    public Task<PricingRuleDto> UpdatePricingRule(Guid id, PricingRuleRequest request, CancellationToken ct) => service.SavePricingRuleAsync(UserId(), id, request, ct);

    [HttpPost("pricing-suggestions"), HasPermission(PermissionCatalog.PricingSuggest)]
    public Task<PricingSuggestionDto> PricingSuggestion(PricingSuggestionRequest request, CancellationToken ct) => service.SuggestPriceAsync(UserId(), request, ct);

    [HttpGet("reorder"), HasPermission(PermissionCatalog.InventoryReorderView)]
    public Task<IReadOnlyList<ReorderSuggestionDto>> Reorder([FromQuery] Guid? branchId, [FromQuery] Guid? godownId, CancellationToken ct) => service.ListReorderSuggestionsAsync(UserId(), branchId, godownId, ct);

    [HttpGet("alerts"), HasPermission(PermissionCatalog.AlertsView)]
    public Task<IReadOnlyList<BusinessAlertDto>> Alerts([FromQuery] bool includeResolved = false, CancellationToken ct = default) => service.ListAlertsAsync(UserId(), includeResolved, ct);

    [HttpPost("alerts/refresh"), HasPermission(PermissionCatalog.AlertsManage)]
    public Task<int> RefreshAlerts(CancellationToken ct) => service.RefreshAlertsAsync(UserId(), ct);

    [HttpPost("alerts/{id:guid}/dismiss"), HasPermission(PermissionCatalog.AlertsManage)]
    public async Task<IActionResult> DismissAlert(Guid id, CancellationToken ct) { await service.DismissAlertAsync(UserId(), id, ct); return NoContent(); }

    [HttpGet("automation"), HasPermission(PermissionCatalog.AutomationView)]
    public Task<IReadOnlyList<AutomationRuleDto>> Automation(CancellationToken ct) => service.ListAutomationRulesAsync(UserId(), ct);

    [HttpPost("automation"), HasPermission(PermissionCatalog.AutomationManage)]
    public Task<AutomationRuleDto> SaveAutomation(AutomationRuleRequest request, CancellationToken ct) => service.SaveAutomationRuleAsync(UserId(), null, request, ct);

    [HttpPut("automation/{id:guid}"), HasPermission(PermissionCatalog.AutomationManage)]
    public Task<AutomationRuleDto> UpdateAutomation(Guid id, AutomationRuleRequest request, CancellationToken ct) => service.SaveAutomationRuleAsync(UserId(), id, request, ct);

    [HttpPost("automation/run"), HasPermission(PermissionCatalog.AutomationRun)]
    public Task<AutomationRunResult> RunAutomation(CancellationToken ct) => service.RunAutomationAsync(UserId(), ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
