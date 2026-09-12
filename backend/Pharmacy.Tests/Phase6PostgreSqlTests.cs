using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Phase6;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Services;

namespace Pharmacy.Tests;

[Collection("Management PostgreSQL reporting")]
public sealed class Phase6PostgreSqlTests
{
    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task All_Phase6_reports_execute_through_existing_report_engine_with_real_postgresql()
    {
        await using var f = await Phase6WorkflowFixture.Create(true);
        var preview = await f.Service.PreviewBulkPricingAsync(f.Actor.Id, new(null, null, null, [f.Product.Id], BulkPricingAction.IncreasePercent, 10, 1, "Report test"));
        await f.Service.ApplyBulkPricingAsync(f.Actor.Id, new(preview.PreviewId, true));
        await f.Service.SaveAutomationRuleAsync(f.Actor.Id, null, new("Expiry report", AutomationTriggerType.NearExpiry, "{}", AutomationActionType.CreateAlert, f.Actor.BranchId));
        await f.Service.RunAutomationAsync(f.Actor.Id);
        var engine = new Pharmacy.Application.Services.Reports.ReportingService(new ReportingRepository(f.Db), TimeProvider.System, phase6: f.Service);
        var query = new Pharmacy.Application.DTOs.Reports.ReportQuery(f.Actor.BranchId, DateTime.UtcNow.AddDays(-365), DateTime.UtcNow.AddMinutes(1), ProductId: f.Product.Id);
        foreach (var name in new[] { "price-history", "promotion-performance", "margin-exceptions", "low-margin", "reorder", "stockout-risk", "slow-dead-stock", "expiry-summary", "automation-log", "alert-summary" })
        {
            var result = await engine.ExecuteAsync(f.Actor.Id, "phase6/" + name, query, null, default);
            var json = System.Text.Json.JsonSerializer.SerializeToElement(result);
            Assert.True(json.TryGetProperty("items", out _), name);
        }
    }
    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Bulk_pricing_concurrent_previews_allow_one_winner_and_one_history_record()
    {
        await using var f = await Phase6WorkflowFixture.Create(true);
        var request = new BulkPricingRequest(null, null, null, [f.Product.Id], BulkPricingAction.IncreasePercent, 10, 1, "Concurrent test");
        var first = await f.Service.PreviewBulkPricingAsync(f.Actor.Id, request);
        var second = await f.Service.PreviewBulkPricingAsync(f.Actor.Id, request);
        async Task<bool> Apply(Guid preview) {
            await using var db = new PharmacyDbContext(f.Options);
            try { await new Phase6Service(db, TimeProvider.System).ApplyBulkPricingAsync(f.Actor.Id, new(preview, true)); return true; }
            catch (RequestValidationException) { return false; }
        }
        var results = await Task.WhenAll(Apply(first.PreviewId), Apply(second.PreviewId));
        Assert.Equal(1, results.Count(x => x));
        Assert.Equal(110, await f.Db.Products.AsNoTracking().Where(x => x.Id == f.Product.Id).Select(x => x.RetailPrice).SingleAsync());
        Assert.Equal(1, await f.Db.PricingPriceHistories.CountAsync(x => x.ProductId == f.Product.Id));
    }
    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Concurrent_automation_runs_execute_daily_rule_once()
    {
        await using var f = await Phase6WorkflowFixture.Create(true);
        await f.Service.SaveAutomationRuleAsync(f.Actor.Id, null, new("Reorder", AutomationTriggerType.StockBelowReorder, "{}", AutomationActionType.CreateDraftPurchaseOrder, f.Actor.BranchId));
        async Task<AutomationRunResult> Run() {
            await using var db = new PharmacyDbContext(f.Options);
            return await new Phase6Service(db, TimeProvider.System).RunAutomationAsync(f.Actor.Id);
        }
        var runs = await Task.WhenAll(Run(), Run());
        Assert.Equal(1, runs.Sum(x => x.ExecutedRules));
        Assert.Equal(1, runs.Sum(x => x.DuplicateRulesSuppressed));
        Assert.Equal(1, await f.Db.PurchaseOrders.CountAsync(x => x.BranchId == f.Actor.BranchId));
    }
    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Concurrent_identical_reorder_requests_create_one_draft()
    {
        await using var f = await Phase6WorkflowFixture.Create(true);
        var row = Assert.Single(await f.Service.ListReorderSuggestionsAsync(f.Actor.Id, null, null));
        var request = new ReorderDraftRequest([new(row.ProductId, row.BranchId, row.GodownId, f.Supplier.Id, row.SuggestedOrderQuantity, 20)], true);
        async Task<ReorderDraftResult> Create() {
            await using var db = new PharmacyDbContext(f.Options);
            return await new Phase6Service(db, TimeProvider.System).CreateReorderDraftsAsync(f.Actor.Id, request);
        }
        var runs = await Task.WhenAll(Create(), Create());
        Assert.Equal(runs[0].PurchaseOrderIds, runs[1].PurchaseOrderIds);
        Assert.Equal(1, runs.Count(x => x.DuplicateSuppressed));
        Assert.Equal(PurchaseOrderStatus.Draft, await f.Db.PurchaseOrders.Where(x => x.BranchId == f.Actor.BranchId).Select(x => x.Status).SingleAsync());
    }
    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Rule_conflicts_resolve_specificity_before_priority_and_preserve_level_base()
    {
        await using var f = await Phase6WorkflowFixture.Create(true);
        f.Db.PricingRules.AddRange(new PricingRule { Name = "Broad", BranchId = f.Actor.BranchId, CategoryId = f.Product.CategoryId, Priority = 999, AdjustmentType = PricingAdjustmentType.FixedPrice, AdjustmentValue = 85 },
            new PricingRule { Name = "Specific", BranchId = f.Actor.BranchId, ProductId = f.Product.Id, CustomerId = f.Customer.Id, AdjustmentType = PricingAdjustmentType.FixedPrice, AdjustmentValue = 95 });
        await f.Db.SaveChangesAsync();
        var result = await new PriceResolutionService(f.Db).ResolveForContextAsync(f.Customer.Id, f.Product.Id, 1, branchId: f.Actor.BranchId, saleType: SaleType.Retail);
        Assert.Equal(95, result.Price);
        Assert.Equal(PriceSource.PricingRule, result.Source);
    }
    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Concurrent_alert_refreshes_deduplicate_source_and_window()
    {
        await using var f = await Phase6WorkflowFixture.Create(true);
        async Task<int> Refresh() {
            await using var db = new PharmacyDbContext(f.Options);
            return await new Phase6Service(db, TimeProvider.System).RefreshAlertsAsync(f.Actor.Id);
        }
        await Task.WhenAll(Refresh(), Refresh());
        var rows = await f.Db.BusinessAlerts.AsNoTracking().Where(x => x.BranchId == f.Actor.BranchId).ToListAsync();
        Assert.Equal(rows.Count, rows.Select(x => (x.SourceType, x.SourceKey)).Distinct().Count());
        Assert.Single(rows, x => x.SourceType == "LowStock");
        Assert.Single(rows, x => x.SourceType == "SlowStock");
    }
    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Overlapping_promotions_use_priority_and_stable_id_tiebreak()
    {
        await using var f = await Phase6WorkflowFixture.Create(true);
        var now = DateTime.UtcNow;
        var low = new PricingRule { Name = "Low", BranchId = f.Actor.BranchId, ProductId = f.Product.Id, Kind = PricingRuleKind.Promotion, Priority = 1, CreatedAt = now, StartsAtUtc = now.AddDays(-1), EndsAtUtc = now.AddDays(1), AdjustmentType = PricingAdjustmentType.FixedPrice, AdjustmentValue = 90 };
        var high = new PricingRule { Name = "High", BranchId = f.Actor.BranchId, ProductId = f.Product.Id, Kind = PricingRuleKind.Promotion, Priority = 2, CreatedAt = now, StartsAtUtc = now.AddDays(-1), EndsAtUtc = now.AddDays(1), AdjustmentType = PricingAdjustmentType.FixedPrice, AdjustmentValue = 95 };
        f.Db.AddRange(low, high); await f.Db.SaveChangesAsync();
        for (var i = 0; i < 3; i++) {
            await using var db = new PharmacyDbContext(f.Options);
            var result = await new PriceResolutionService(db).ResolveForContextAsync(null, f.Product.Id, 1, branchId: f.Actor.BranchId, atUtc: now);
            Assert.Equal(high.Id, result.MatchedPricingRuleId);
            Assert.Equal(PriceSource.Promotion, result.Source);
        }
    }
    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Reorder_calculation_is_consistent_and_pending_draft_reduces_need()
    {
        await using var f = await Phase6WorkflowFixture.Create(true);
        var first = Assert.Single(await f.Service.ListReorderSuggestionsAsync(f.Actor.Id, null, null));
        await using var other = new PharmacyDbContext(f.Options);
        var second = Assert.Single(await new Phase6Service(other, TimeProvider.System).ListReorderSuggestionsAsync(f.Actor.Id, null, null));
        Assert.Equal(first, second);
        await f.Service.CreateReorderDraftsAsync(f.Actor.Id, new([new(first.ProductId, first.BranchId, first.GodownId, f.Supplier.Id, first.SuggestedOrderQuantity, first.SuggestedOrderQuantity)], true));
        Assert.Empty(await new Phase6Service(other, TimeProvider.System).ListReorderSuggestionsAsync(f.Actor.Id, null, null));
    }
}
