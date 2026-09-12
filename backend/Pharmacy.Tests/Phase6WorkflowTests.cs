using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Phase6;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class Phase6WorkflowTests
{
    [Fact]
    public async Task Near_credit_limit_and_supplier_due_alerts_use_current_aging_data()
    {
        await using var f = await Phase6WorkflowFixture.Create();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        f.Db.AddRange(new CustomerLedgerEntry { Customer = f.Customer, BranchId = f.Actor.BranchId, EntryType = CustomerLedgerEntryType.OpeningBalance, Amount = 95000, EntryDate = today },
            new GoodsReceipt { BranchId = f.Actor.BranchId, Supplier = f.Supplier, GrnNumber = Guid.NewGuid().ToString("N"), ReceiptDate = today, DueDate = today.AddDays(2), NetTotal = 150000, Status = GoodsReceiptStatus.Posted });
        await f.Db.SaveChangesAsync();
        await f.Service.RefreshAlertsAsync(f.Actor.Id);
        Assert.Contains(f.Db.BusinessAlerts, x => x.SourceType == "CreditNear");
        Assert.Contains(f.Db.BusinessAlerts, x => x.SourceType == "SupplierDue");
        Assert.Contains(f.Db.BusinessAlerts, x => x.SourceType == "SupplierOutstanding");
    }
    [Fact]
    public async Task Purchase_cost_suggestion_requires_confirmation_and_preserves_history_when_accepted()
    {
        await using var f = await Phase6WorkflowFixture.Create();
        var suggestion = new PurchaseCostSuggestionDto(Guid.NewGuid(), f.Product.Id, f.Product.Name, f.Actor.BranchId, Guid.NewGuid(), 80, 90, 100, 10, 112.5m, f.Product.UpdatedAt);
        f.Db.AuditLogs.Add(new AuditLog { UserId = f.Actor.Id, Action = "PurchaseCostSuggestion", EntityType = "Product", EntityId = suggestion.Id, NewValues = System.Text.Json.JsonSerializer.Serialize(suggestion) });
        await f.Db.SaveChangesAsync();
        Assert.Equal(113, Assert.Single(await f.Service.ListPurchaseCostSuggestionsAsync(f.Actor.Id)).SuggestedPrice);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.AcceptPurchaseCostSuggestionAsync(f.Actor.Id, suggestion.Id, false));
        Assert.Equal(100, f.Product.RetailPrice);
        await f.Service.AcceptPurchaseCostSuggestionAsync(f.Actor.Id, suggestion.Id, true);
        Assert.Equal(113, f.Product.RetailPrice);
        Assert.Single(f.Db.PricingPriceHistories);
        Assert.Empty(await f.Service.ListPurchaseCostSuggestionsAsync(f.Actor.Id));
    }
    [Fact]
    public async Task Automation_alerts_are_rule_window_scoped_and_review_flags_are_idempotent()
    {
        await using var f = await Phase6WorkflowFixture.Create();
        await f.Service.SaveAutomationRuleAsync(f.Actor.Id, null, new("Review expiry", AutomationTriggerType.NearExpiry, "{\"warningDays\":30}", AutomationActionType.FlagForReview, f.Actor.BranchId));
        var first = await f.Service.RunAutomationAsync(f.Actor.Id);
        Assert.Equal(1, first.AlertsCreated);
        Assert.Equal(1, first.ReviewFlagsCreated);
        Assert.All(f.Db.BusinessAlerts, x => Assert.Equal(f.Actor.BranchId, x.BranchId));
        Assert.DoesNotContain(f.Db.BusinessAlerts, x => x.SourceType == "LowStock");
        var repeated = await f.Service.RunAutomationAsync(f.Actor.Id);
        Assert.Equal(1, repeated.DuplicateRulesSuppressed);
        Assert.Equal(2, await f.Db.BusinessAlerts.CountAsync());
    }
    [Fact]
    public async Task Automation_rejects_unrecognized_conditions_and_respects_quantity_condition()
    {
        await using var f = await Phase6WorkflowFixture.Create();
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.SaveAutomationRuleAsync(f.Actor.Id, null, new("Bad", AutomationTriggerType.NearExpiry, "{\"script\":1}", AutomationActionType.CreateAlert, f.Actor.BranchId)));
        await f.Service.SaveAutomationRuleAsync(f.Actor.Id, null, new("Large reorder only", AutomationTriggerType.StockBelowReorder, "{\"minimumOrderQuantity\":100}", AutomationActionType.CreateDraftPurchaseOrder, f.Actor.BranchId));
        var result = await f.Service.RunAutomationAsync(f.Actor.Id);
        Assert.Empty(result.DraftPurchaseOrderIds);
        Assert.Empty(f.Db.PurchaseOrders);
    }
    [Fact]
    public async Task Reorder_drafts_preserve_suggested_and_final_quantity_and_repeated_request_returns_same_draft()
    {
        await using var f = await Phase6WorkflowFixture.Create();
        var row = Assert.Single(await f.Service.ListReorderSuggestionsAsync(f.Actor.Id, null, null));
        var request = new ReorderDraftRequest([new(row.ProductId, row.BranchId, row.GodownId, f.Supplier.Id, row.SuggestedOrderQuantity, 25)], true);
        var first = await f.Service.CreateReorderDraftsAsync(f.Actor.Id, request);
        var repeat = await f.Service.CreateReorderDraftsAsync(f.Actor.Id, request);
        Assert.True(repeat.DuplicateSuppressed);
        Assert.Equal(first.PurchaseOrderIds, repeat.PurchaseOrderIds);
        var po = Assert.Single(f.Db.PurchaseOrders);
        Assert.Equal(PurchaseOrderStatus.Draft, po.Status);
        Assert.Empty(f.Db.GoodsReceipts);
        var line = Assert.Single(po.Items);
        Assert.Equal(25, line.OrderedQuantity);
        Assert.Contains("Suggested quantity: 19", line.Notes);
        po.Status = PurchaseOrderStatus.Cancelled;
        await f.Db.SaveChangesAsync();
        var nextCycle = new Pharmacy.Infrastructure.Services.Phase6Service(f.Db, new NextDayClock());
        var future = await nextCycle.CreateReorderDraftsAsync(f.Actor.Id, request);
        Assert.False(future.DuplicateSuppressed);
        Assert.NotEqual(first.PurchaseOrderIds.Single(), future.PurchaseOrderIds.Single());
    }
    [Fact]
    public async Task Reorder_draft_rejects_stale_suggestion_and_cross_branch_selection()
    {
        await using var f = await Phase6WorkflowFixture.Create();
        var row = Assert.Single(await f.Service.ListReorderSuggestionsAsync(f.Actor.Id, null, null));
        var line = new ReorderDraftLine(row.ProductId, row.BranchId, row.GodownId, f.Supplier.Id, 99, 20);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateReorderDraftsAsync(f.Actor.Id, new([line], true)));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.CreateReorderDraftsAsync(f.Actor.Id, new([line with { BranchId = Guid.NewGuid() }], true)));
        Assert.Empty(f.Db.PurchaseOrders);
    }
    [Fact]
    public async Task Slow_stock_uses_movements_and_minimum_value_without_classifying_product()
    {
        await using var f = await Phase6WorkflowFixture.Create();
        var row = Assert.Single(await f.Service.ListSlowStockAsync(f.Actor.Id, 0));
        Assert.Equal("Dead stock", row.Status);
        Assert.Null(row.LastSaleDate);
        Assert.Equal(200, row.DaysSinceLastSale);
        Assert.Equal(80, row.StockValue);
        Assert.Empty(await f.Service.ListSlowStockAsync(f.Actor.Id, 81));
    }
    [Fact]
    public async Task Aging_alerts_use_existing_opening_balances_and_deduplicate_daily_window()
    {
        await using var f = await Phase6WorkflowFixture.Create();
        f.Db.AddRange(new CustomerLedgerEntry { Customer = f.Customer, BranchId = f.Actor.BranchId, EntryType = CustomerLedgerEntryType.OpeningBalance, Amount = 120000, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-45)) },
            new SupplierLedgerEntry { Supplier = f.Supplier, BranchId = f.Actor.BranchId, EntryType = SupplierLedgerEntryType.OpeningBalance, Amount = 150000, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)) });
        await f.Db.SaveChangesAsync();
        await f.Service.RefreshAlertsAsync(f.Actor.Id);
        Assert.Contains(f.Db.BusinessAlerts, x => x.SourceType == "CustomerOverdue");
        Assert.Contains(f.Db.BusinessAlerts, x => x.SourceType == "CreditExceeded");
        Assert.Contains(f.Db.BusinessAlerts, x => x.SourceType == "SupplierOverdue");
        var count = await f.Db.BusinessAlerts.CountAsync();
        Assert.Equal(0, await f.Service.RefreshAlertsAsync(f.Actor.Id));
        Assert.Equal(count, await f.Db.BusinessAlerts.CountAsync());
    }
    [Fact]
    public async Task Thresholds_validate_and_reuse_settings_storage()
    {
        await using var f = await Phase6WorkflowFixture.Create();
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.SaveThresholdsAsync(f.Actor.Id, new(SlowMovingDays: 180, DeadStockDays: 90)));
        var settings = new Phase6Thresholds(30, 30, 120, 15, 45, 5);
        Assert.Equal(settings, await f.Service.SaveThresholdsAsync(f.Actor.Id, settings));
        Assert.Equal(settings, await f.Service.GetThresholdsAsync(f.Actor.Id));
        Assert.Single(f.Db.SystemSettings);
    }
    [Fact]
    public async Task Expiry_discount_is_suggestion_only_and_cost_floor_is_enforced()
    {
        await using var f = await Phase6WorkflowFixture.Create();
        var row = Assert.Single(await f.Service.ListExpiryDiscountSuggestionsAsync(f.Actor.Id, 50));
        Assert.Equal(80, row.FinalPrice);
        Assert.Equal(0, row.ProjectedMarginPercent);
        Assert.True(row.BelowCostGuardApplied);
        Assert.Equal(100, f.Product.RetailPrice);
        Assert.Empty(f.Db.PricingPriceHistories);
    }
    [Fact]
    public async Task Safe_automation_creates_drafts_once_and_never_receives_or_posts()
    {
        await using var f = await Phase6WorkflowFixture.Create();
        await f.Service.SaveAutomationRuleAsync(f.Actor.Id, null, new("Reorder drafts", AutomationTriggerType.StockBelowReorder, "{}", AutomationActionType.CreateDraftPurchaseOrder, f.Actor.BranchId));
        var first = await f.Service.RunAutomationAsync(f.Actor.Id);
        var repeat = await f.Service.RunAutomationAsync(f.Actor.Id);
        Assert.Single(first.DraftPurchaseOrderIds);
        Assert.Equal(1, repeat.DuplicateRulesSuppressed);
        Assert.Equal(0, repeat.ExecutedRules);
        Assert.Single(f.Db.PurchaseOrders);
        Assert.Equal(PurchaseOrderStatus.Draft, f.Db.PurchaseOrders.Single().Status);
        Assert.Empty(f.Db.GoodsReceipts);
        Assert.Empty(f.Db.JournalEntries);
    }
    [Fact]
    public async Task Unsafe_automation_combination_is_rejected()
    {
        await using var f = await Phase6WorkflowFixture.Create();
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.SaveAutomationRuleAsync(f.Actor.Id, null, new("Unsafe", AutomationTriggerType.SupplierDue, "{}", AutomationActionType.CreateDraftPurchaseOrder, f.Actor.BranchId)));
    }
    private sealed class NextDayClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddDays(1);
    }
}
