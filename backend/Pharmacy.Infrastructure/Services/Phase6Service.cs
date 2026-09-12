using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Phase6;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Phase6;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Services;

public sealed partial class Phase6Service(PharmacyDbContext db, TimeProvider clock) : IPhase6Service
{
    public async Task<IReadOnlyList<PricingRuleDto>> ListPricingRulesAsync(Guid actorId, bool activeOnly, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PricingView, ct);
        var query = db.PricingRules.AsNoTracking().Where(x => x.BranchId == null || x.BranchId == actor.BranchId);
        if (activeOnly) query = query.Where(x => x.IsActive);
        return await query.OrderByDescending(x => x.Priority).ThenBy(x => x.Name).Select(MapRule).ToListAsync(ct);
    }

    public async Task<PricingRuleDto> SavePricingRuleAsync(Guid actorId, Guid? id, PricingRuleRequest request, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PricingManage, ct);
        if (request.BranchId.HasValue) EnsureBranch(actor, request.BranchId.Value);
        ValidateRule(request);
        await ValidateRuleReferences(request, actor, ct);
        var oldValues = id.HasValue ? await db.PricingRules.AsNoTracking().Where(x => x.Id == id).Select(MapRule).SingleOrDefaultAsync(ct) : null;
        PricingRule rule;
        if (id.HasValue)
        {
            rule = await db.PricingRules.SingleOrDefaultAsync(x => x.Id == id.Value, ct)
                ?? throw new ResourceNotFoundException("Pricing rule was not found.");
            if (rule.BranchId.HasValue) EnsureBranch(actor, rule.BranchId.Value);
        }
        else
        {
            rule = new PricingRule { Name = request.Name.Trim() };
            db.PricingRules.Add(rule);
        }
        rule.Name = request.Name.Trim(); rule.Kind = request.Kind; rule.Priority = request.Priority;
        rule.BranchId = request.BranchId; rule.CustomerId = request.CustomerId; rule.ProductId = request.ProductId;
        rule.CategoryId = request.CategoryId; rule.ManufacturerId = request.ManufacturerId; rule.CustomerType = request.CustomerType;
        rule.PriceLevelId = request.PriceLevelId; rule.SaleType = request.SaleType; rule.MinimumQuantity = request.MinimumQuantity;
        rule.StartsAtUtc = request.StartsAtUtc?.ToUniversalTime(); rule.EndsAtUtc = request.EndsAtUtc?.ToUniversalTime();
        rule.AdjustmentType = request.AdjustmentType; rule.AdjustmentValue = Money(request.AdjustmentValue); rule.IsActive = request.IsActive;
        db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = id.HasValue ? "PricingRuleUpdated" : "PricingRuleCreated", EntityType = "PricingRule", EntityId = rule.Id, OldValues = oldValues == null ? null : JsonSerializer.Serialize(oldValues), NewValues = JsonSerializer.Serialize(request) });
        await db.SaveChangesAsync(ct);
        return await db.PricingRules.AsNoTracking().Where(x => x.Id == rule.Id).Select(MapRule).SingleAsync(ct);
    }

    public async Task<PricingSuggestionDto> SuggestPriceAsync(Guid actorId, PricingSuggestionRequest request, CancellationToken ct = default)
    {
        await Require(actorId, PermissionCatalog.PricingSuggest, ct);
        if (request.TargetMarginPercent is <= 0 or >= 100 || request.TargetMarkupPercent is < 0 || request.RoundingIncrement <= 0)
            throw new RequestValidationException("Pricing targets and rounding increment are invalid.");
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ProductId, ct)
            ?? throw new ResourceNotFoundException("Product was not found.");
        if (request.TargetMarginPercent.HasValue && request.TargetMarkupPercent.HasValue)
            throw new RequestValidationException("Provide either a target margin or a target markup, not both.");
        var suggested = request.TargetMarginPercent.HasValue
            ? product.PurchasePrice / (1m - request.TargetMarginPercent.Value / 100m)
            : request.TargetMarkupPercent.HasValue ? product.PurchasePrice * (1m + request.TargetMarkupPercent.Value / 100m) : product.RetailPrice;
        suggested = decimal.Round(Math.Ceiling(suggested / request.RoundingIncrement) * request.RoundingIncrement, 2, MidpointRounding.AwayFromZero);
        var margin = suggested <= 0 ? null : (decimal?)decimal.Round((suggested - product.PurchasePrice) / suggested * 100m, 2);
        var markup = product.PurchasePrice <= 0 ? null : (decimal?)decimal.Round((suggested - product.PurchasePrice) / product.PurchasePrice * 100m, 2);
        return new(product.Id, product.PurchasePrice, product.RetailPrice, suggested, margin, markup);
    }

    public async Task<IReadOnlyList<ReorderSuggestionDto>> ListReorderSuggestionsAsync(Guid actorId, Guid? branchId, Guid? godownId, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryReorderView, ct);
        if (branchId.HasValue) EnsureBranch(actor, branchId.Value);
        var branch = branchId ?? actor.BranchId;
        var thresholds = await ThresholdsCore(ct);
        var unrestricted = actor.Role!.Name is RoleCatalog.Owner or RoleCatalog.Manager;
        var inventory = await db.Inventory.AsNoTracking().Include(x => x.Product).Include(x => x.Branch).Include(x => x.Godown).Include(x => x.ProductBatch)
            .Where(x => x.BranchId == branch)
            .Where(x => unrestricted || db.UserGodowns.Any(g => g.UserId == actorId && g.GodownId == x.GodownId))
            .ToListAsync(ct);
        var since = clock.GetUtcNow().UtcDateTime.AddDays(-30);
        var velocity = await db.SaleItemBatchAllocations.AsNoTracking()
            .Where(x => x.SaleItem!.Sale!.Status == SaleStatus.Posted && x.SaleItem.Sale.PostedAtUtc >= since && x.SaleItem.Sale.BranchId == branch)
            .GroupBy(x => new { x.SaleItem!.ProductId, x.SaleItem.Sale!.GodownId }).Select(x => new { x.Key, Quantity = x.Sum(y => y.Quantity) }).ToListAsync(ct);
        var pendingRows = await db.PurchaseOrderItems.AsNoTracking().Where(x => x.PurchaseOrder!.Status == PurchaseOrderStatus.Draft || x.PurchaseOrder!.Status == PurchaseOrderStatus.Submitted || x.PurchaseOrder!.Status == PurchaseOrderStatus.PartiallyReceived)
            .Where(x => x.PurchaseOrder!.BranchId == branch).GroupBy(x => new { x.ProductId, x.PurchaseOrder!.GodownId })
            .Select(x => new { x.Key, Quantity = x.Sum(y => y.OrderedQuantity - y.ReceivedQuantity) }).ToListAsync(ct);
        var pending = pendingRows.Where(x => x.Key.GodownId == null).ToDictionary(x => x.Key.ProductId, x => x.Quantity);
        var lastPurchases = await db.ProductBatches.AsNoTracking().Include(x => x.Supplier).Where(x => x.BranchId == branch && x.Supplier != null && x.Supplier.IsActive)
            .GroupBy(x => x.ProductId).Select(x => x.OrderByDescending(y => y.CreatedAt).Select(y => new { ProductId = x.Key, y.SupplierId, Supplier = y.Supplier!.Name, y.PurchasePrice }).First()).ToDictionaryAsync(x => x.ProductId, ct);
        return inventory.GroupBy(x => new { x.BranchId, x.ProductId, x.GodownId }).OrderBy(x => x.Key.ProductId).ThenBy(x => x.Key.GodownId).Select(group =>
        {
            var first = group.First(); var stock = group.Sum(x => x.QuantityInStock); var reorder = group.Max(x => x.ReorderLevel > 0 ? x.ReorderLevel : x.Product!.ReorderLevel);
            var daily = (velocity.SingleOrDefault(x => x.Key.ProductId == group.Key.ProductId && x.Key.GodownId == group.Key.GodownId)?.Quantity ?? 0) / 30m;
            var target = Math.Max(reorder * 2, (int)Math.Ceiling(daily * thresholds.DefaultReorderCoverDays));
            var need = Math.Max(0, target - stock);
            var scopedPo = group.Key.GodownId.HasValue ? pendingRows.SingleOrDefault(x => x.Key.ProductId == group.Key.ProductId && x.Key.GodownId == group.Key.GodownId)?.Quantity ?? 0 : 0;
            var unscopedPo = Math.Min(Math.Max(0, need - scopedPo), pending.GetValueOrDefault(group.Key.ProductId));
            var po = scopedPo + unscopedPo;
            pending[group.Key.ProductId] = pending.GetValueOrDefault(group.Key.ProductId) - unscopedPo;
            var suggested = Math.Max(0, need - po); var days = daily > 0 ? stock / daily : (decimal?)null;
            var last = lastPurchases.GetValueOrDefault(group.Key.ProductId);
            return new ReorderSuggestionDto(group.Key.ProductId, first.Product!.Name, first.Product.SKU, first.BranchId, first.Branch!.Name, first.GodownId, first.Godown?.Name,
                stock, stock, reorder, po, decimal.Round(daily, 2), days.HasValue ? decimal.Round(days.Value, 1) : null, suggested,
                last?.SupplierId, last?.Supplier, last?.PurchasePrice, stock <= 0 ? "OutOfStock" : stock <= reorder ? "Reorder" : "Healthy");
        }).Where(x => x.SuggestedOrderQuantity > 0 && (!godownId.HasValue || x.GodownId == godownId)).OrderByDescending(x => x.Risk == "OutOfStock").ThenBy(x => x.Product).ToList();
    }

    public async Task<IReadOnlyList<BusinessAlertDto>> ListAlertsAsync(Guid actorId, bool includeResolved, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AlertsView, ct);
        var unrestricted = actor.Role!.Name is RoleCatalog.Owner or RoleCatalog.Manager;
        var ar = actor.Role.RolePermissions.Any(x => x.Permission!.Code is PermissionCatalog.AccountsAgingReceivablesView or PermissionCatalog.ReportsFinancial);
        var ap = actor.Role.RolePermissions.Any(x => x.Permission!.Code is PermissionCatalog.AccountsAgingPayablesView or PermissionCatalog.ReportsFinancial);
        return await db.BusinessAlerts.AsNoTracking().Where(x => (!x.ResolvedAtUtc.HasValue || includeResolved) && (x.BranchId == null || x.BranchId == actor.BranchId))
            .Where(x => (x.Category != BusinessAlertCategory.Receivables || ar) && (x.Category != BusinessAlertCategory.Payables || ap))
            .Where(x => unrestricted || x.GodownId == null || db.UserGodowns.Any(g => g.UserId == actorId && g.GodownId == x.GodownId))
            .OrderByDescending(x => x.Severity).ThenByDescending(x => x.CreatedAt).Select(MapAlert).ToListAsync(ct);
    }

    public async Task<int> RefreshAlertsAsync(Guid actorId, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AlertsManage, ct);
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(606060)", ct);
        var count = await RefreshAlertsCoreAsync(actor, ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return count;
    }

    private async Task<int> RefreshAlertsCoreAsync(User actor, CancellationToken ct, AutomationTriggerType? trigger = null, Dictionary<string, decimal>? conditions = null, Guid? automationRuleId = null)
    {
        var now = clock.GetUtcNow().UtcDateTime; var created = 0;
        var thresholds = await ThresholdsCore(ct);
        var unrestricted = actor.Role!.Name is RoleCatalog.Owner or RoleCatalog.Manager;
        conditions ??= new();
        var ruleScope = $":rule:{automationRuleId:N}:{actor.BranchId:N}:";
        var suffix = automationRuleId.HasValue ? ruleScope + now.ToString("yyyyMMdd") : "";
        var sources = trigger switch {
            AutomationTriggerType.StockBelowReorder => new[] { "LowStock" }, AutomationTriggerType.NearExpiry => new[] { "Expiry" },
            AutomationTriggerType.CustomerOverdue => new[] { "CustomerOverdue", "CreditNear", "CreditExceeded", "CustomerOutstanding" },
            AutomationTriggerType.SupplierDue => new[] { "SupplierDue", "SupplierOverdue", "SupplierOutstanding" },
            AutomationTriggerType.MarginBelowThreshold => new[] { "BelowCost", "LowMargin" },
            _ => new[] { "LowStock", "Expiry", "SlowStock", "CustomerOverdue", "CreditNear", "CreditExceeded", "CustomerOutstanding", "SupplierDue", "SupplierOverdue", "SupplierOutstanding", "BelowCost", "LowMargin" }
        };
        var stale = await db.BusinessAlerts.Where(x => (x.BranchId == actor.BranchId || x.BranchId == null && x.Category == BusinessAlertCategory.Pricing) && sources.Contains(x.SourceType)
            && (unrestricted || x.GodownId == null || db.UserGodowns.Any(g => g.UserId == actor.Id && g.GodownId == x.GodownId))).ToListAsync(ct);
        foreach (var alert in stale.Where(x => automationRuleId.HasValue ? x.SourceKey.Contains(ruleScope) : !x.SourceKey.Contains(":rule:"))) alert.ResolvedAtUtc = now;
        var lowStock = await db.Inventory.AsNoTracking().Where(x => x.BranchId == actor.BranchId && x.Product!.IsActive && (unrestricted || db.UserGodowns.Any(g => g.UserId == actor.Id && g.GodownId == x.GodownId)))
            .GroupBy(x => new { x.BranchId, x.GodownId, x.ProductId, x.Product!.Name }).Select(g => new {
                g.Key, Quantity = g.Sum(x => x.QuantityInStock), Reorder = g.Max(x => x.ReorderLevel > 0 ? x.ReorderLevel : x.Product!.ReorderLevel)
            }).Where(x => x.Quantity <= x.Reorder).ToListAsync(ct);
        foreach (var row in lowStock.Where(x => (!trigger.HasValue || trigger == AutomationTriggerType.StockBelowReorder) && (!conditions.ContainsKey("minimumOrderQuantity") || Math.Max(0, x.Reorder * 2 - x.Quantity) >= conditions["minimumOrderQuantity"]))) created += await UpsertAlert("LowStock", $"{row.Key.BranchId}:{row.Key.GodownId}:{row.Key.ProductId}{suffix}", BusinessAlertCategory.Inventory, row.Quantity <= 0 ? BusinessAlertSeverity.Critical : BusinessAlertSeverity.Warning,
            row.Quantity <= 0 ? "Out of stock" : "Low stock", $"{row.Key.Name} has {row.Quantity} units against reorder level {row.Reorder}.", row.Key.BranchId, row.Key.GodownId, ct);
        var warningDays = conditions.GetValueOrDefault("warningDays", thresholds.ExpiryWarningDays);
        var expiry = await db.Inventory.AsNoTracking().Include(x => x.ProductBatch).Include(x => x.Product).Where(x => x.BranchId == actor.BranchId && x.QuantityInStock > 0 && x.ProductBatch!.ExpiryDate <= DateOnly.FromDateTime(now.AddDays((double)warningDays)) && (unrestricted || db.UserGodowns.Any(g => g.UserId == actor.Id && g.GodownId == x.GodownId))).ToListAsync(ct);
        foreach (var row in expiry.Where(x => !trigger.HasValue || trigger == AutomationTriggerType.NearExpiry))
        {
            var days = row.ProductBatch!.ExpiryDate.DayNumber - DateOnly.FromDateTime(now).DayNumber;
            created += await UpsertAlert("Expiry", $"{row.BranchId}:{row.GodownId}:{row.ProductBatchId}{suffix}", BusinessAlertCategory.Expiry, days < 0 ? BusinessAlertSeverity.Critical : BusinessAlertSeverity.Warning,
                days < 0 ? "Expired stock" : "Stock nearing expiry", $"{row.Product!.Name} batch {row.ProductBatch.BatchNumber} has {row.QuantityInStock} units and expires in {days} days.", row.BranchId, row.GodownId, ct);
        }
        var minMargin = conditions.GetValueOrDefault("minimumMarginPercent", thresholds.MinimumMarginPercent);
        var margin = await db.Products.AsNoTracking().Where(x => x.IsActive && (x.RetailPrice <= 0 || (x.RetailPrice - x.PurchasePrice) / x.RetailPrice * 100 < minMargin)).ToListAsync(ct);
        foreach (var product in margin.Where(x => !trigger.HasValue || trigger == AutomationTriggerType.MarginBelowThreshold)) created += await UpsertAlert(product.RetailPrice < product.PurchasePrice ? "BelowCost" : "LowMargin", product.Id + suffix, BusinessAlertCategory.Pricing, BusinessAlertSeverity.Warning,
            product.RetailPrice < product.PurchasePrice ? "Retail price below cost" : "Retail margin below threshold", $"{product.Name} retails at {product.RetailPrice:0.00}, cost {product.PurchasePrice:0.00}, minimum margin {minMargin:0.##}%.", automationRuleId.HasValue ? actor.BranchId : null, null, ct);
        created += await RefreshExtendedAlerts(actor, thresholds, ct, trigger, conditions, suffix);
        await db.SaveChangesAsync(ct); return created;
    }

    public async Task DismissAlertAsync(Guid actorId, Guid id, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AlertsManage, ct);
        var alert = await db.BusinessAlerts.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException("Alert was not found.");
        if (alert.BranchId.HasValue) EnsureBranch(actor, alert.BranchId.Value);
        if (alert.GodownId.HasValue && actor.Role!.Name is not (RoleCatalog.Owner or RoleCatalog.Manager) && !await db.UserGodowns.AnyAsync(x => x.UserId == actorId && x.GodownId == alert.GodownId, ct))
            throw new ForbiddenOperationException("The current user is not permitted to manage this godown's alerts.");
        alert.DismissedAtUtc = clock.GetUtcNow().UtcDateTime; await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AutomationRuleDto>> ListAutomationRulesAsync(Guid actorId, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AutomationView, ct);
        return await db.AutomationRules.AsNoTracking().Where(x => x.BranchId == null || x.BranchId == actor.BranchId).OrderBy(x => x.Name).Select(MapAutomation).ToListAsync(ct);
    }

    public async Task<AutomationRuleDto> SaveAutomationRuleAsync(Guid actorId, Guid? id, AutomationRuleRequest request, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AutomationManage, ct);
        if (request.BranchId.HasValue) EnsureBranch(actor, request.BranchId.Value);
        if (!Enum.IsDefined(request.TriggerType) || !Enum.IsDefined(request.ActionType) || request.ActionType == AutomationActionType.CreateDraftPurchaseOrder && request.TriggerType != AutomationTriggerType.StockBelowReorder)
            throw new RequestValidationException("Unsupported automation trigger/action combination.");
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ConditionsJson)) throw new RequestValidationException("Automation name and conditions are required.");
        ReadAutomationConditions(request.TriggerType, request.ConditionsJson);
        var rule = id.HasValue ? await db.AutomationRules.SingleOrDefaultAsync(x => x.Id == id.Value, ct) ?? throw new ResourceNotFoundException("Automation rule was not found.") : new AutomationRule { Name = request.Name.Trim(), ConditionsJson = "{}", CreatedByUserId = actorId };
        if (rule.BranchId.HasValue) EnsureBranch(actor, rule.BranchId.Value);
        rule.Name = request.Name.Trim(); rule.TriggerType = request.TriggerType; rule.ConditionsJson = request.ConditionsJson; rule.ActionType = request.ActionType; rule.BranchId = request.BranchId; rule.IsActive = request.IsActive;
        if (!id.HasValue) db.AutomationRules.Add(rule);
        db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = id.HasValue ? "AutomationRuleUpdated" : "AutomationRuleCreated", EntityType = "AutomationRule", EntityId = rule.Id, NewValues = JsonSerializer.Serialize(request) });
        await db.SaveChangesAsync(ct); return await db.AutomationRules.AsNoTracking().Where(x => x.Id == rule.Id).Select(MapAutomation).SingleAsync(ct);
    }

    public Task<AutomationRunResult> RunAutomationAsync(Guid actorId, CancellationToken ct = default) => RunSafeAutomationAsync(actorId, ct);


    private async Task<int> UpsertAlert(string type, string key, BusinessAlertCategory category, BusinessAlertSeverity severity, string title, string description, Guid? branch, Guid? godown, CancellationToken ct)
    {
        var alert = db.BusinessAlerts.Local.SingleOrDefault(x => x.SourceType == type && x.SourceKey == key)
            ?? await db.BusinessAlerts.SingleOrDefaultAsync(x => x.SourceType == type && x.SourceKey == key, ct);
        if (alert is null) { db.BusinessAlerts.Add(new BusinessAlert { Category = category, Severity = severity, Title = title, Description = description, SourceType = type, SourceKey = key, BranchId = branch, GodownId = godown }); return 1; }
        alert.Severity = severity; alert.Title = title; alert.Description = description; alert.ResolvedAtUtc = null; return 0;
    }

    private async Task<User> Require(Guid actorId, string permission, CancellationToken ct) => await db.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission).SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive, ct) is { } actor && actor.Role!.RolePermissions.Any(x => x.Permission!.Code == permission) ? actor : throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
    private static void EnsureBranch(User actor, Guid branchId) { if (actor.BranchId != branchId) throw new ForbiddenOperationException("The current user is not permitted to access this branch."); }
    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static void ValidateRule(PricingRuleRequest request) { if (!Enum.IsDefined(request.Kind) || !Enum.IsDefined(request.AdjustmentType) || request.CustomerType.HasValue && !Enum.IsDefined(request.CustomerType.Value) || request.SaleType.HasValue && !Enum.IsDefined(request.SaleType.Value) || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 160 || request.Priority < 0 || request.AdjustmentValue < 0 || request.MinimumQuantity is <= 0 || request.EndsAtUtc <= request.StartsAtUtc || request.AdjustmentType == PricingAdjustmentType.PercentageDiscount && request.AdjustmentValue > 100) throw new RequestValidationException("Pricing rule values or date range are invalid."); }
    private static System.Linq.Expressions.Expression<Func<PricingRule, PricingRuleDto>> MapRule => x => new(x.Id, x.Name, x.Kind, x.Priority, x.BranchId, x.CustomerId, x.ProductId, x.CategoryId, x.ManufacturerId, x.CustomerType, x.PriceLevelId, x.SaleType, x.MinimumQuantity, x.StartsAtUtc, x.EndsAtUtc, x.AdjustmentType, x.AdjustmentValue, x.IsActive);
    private static System.Linq.Expressions.Expression<Func<BusinessAlert, BusinessAlertDto>> MapAlert => x => new(x.Id, x.Category, x.Severity, x.Title, x.Description, x.SourceType, x.SourceKey, x.BranchId, x.GodownId, x.CreatedAt, x.DismissedAtUtc.HasValue, x.ResolvedAtUtc.HasValue);
    private static System.Linq.Expressions.Expression<Func<AutomationRule, AutomationRuleDto>> MapAutomation => x => new(x.Id, x.Name, x.TriggerType, x.ConditionsJson, x.ActionType, x.BranchId, x.IsActive, x.LastRunAtUtc);
}
