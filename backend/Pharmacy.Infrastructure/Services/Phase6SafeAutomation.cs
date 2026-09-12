using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Phase6;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Services;

public sealed partial class Phase6Service
{
    private async Task<AutomationRunResult> RunSafeAutomationAsync(Guid actorId, CancellationToken ct)
    {
        var actor = await Require(actorId, PermissionCatalog.AutomationRun, ct);
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(606060)", ct);
        var rules = await db.AutomationRules.Where(x => x.IsActive && (x.BranchId == null || x.BranchId == actor.BranchId)).OrderBy(x => x.Id).ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var alerts = 0; var flags = 0; var executed = 0; var duplicate = 0;
        var ids = new List<Guid>();
        foreach (var rule in rules)
        {
            var conditions = ReadAutomationConditions(rule.TriggerType, rule.ConditionsJson);
            if (rule.TriggerType == AutomationTriggerType.CustomerOverdue) await Require(actorId, PermissionCatalog.AccountsAgingReceivablesView, ct);
            if (rule.TriggerType == AutomationTriggerType.SupplierDue) await Require(actorId, PermissionCatalog.AccountsAgingPayablesView, ct);
            if (!Enum.IsDefined(rule.ActionType) || !Enum.IsDefined(rule.TriggerType) || rule.ActionType == AutomationActionType.CreateDraftPurchaseOrder && rule.TriggerType != AutomationTriggerType.StockBelowReorder)
                throw new RequestValidationException("Unsafe automation action or trigger is configured.");
            var key = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{rule.Id}:{actor.BranchId}:{now:yyyy-MM-dd}:{rule.ConditionsJson}:{rule.ActionType}:{rule.TriggerType}")).AsSpan(0, 16));
            if (await db.AuditLogs.AnyAsync(x => x.Action == "AutomationExecuted" && x.EntityId == key, ct)) { duplicate++; continue; }
            if (rule.ActionType == AutomationActionType.CreateDraftPurchaseOrder)
            {
                await Require(actorId, PermissionCatalog.PurchaseOrdersCreate, ct);
                var rows = await ListReorderSuggestionsAsync(actorId, actor.BranchId, null, ct);
                var lines = rows.Where(x => x.PreferredSupplierId.HasValue && x.SuggestedOrderQuantity >= conditions.GetValueOrDefault("minimumOrderQuantity", 1)).Select(x => new ReorderDraftLine(x.ProductId, x.BranchId, x.GodownId, x.PreferredSupplierId!.Value, x.SuggestedOrderQuantity, x.SuggestedOrderQuantity)).ToList();
                if (lines.Count > 0) {
                    var result = await CreateReorderDraftsAsync(actorId, new(lines, true), ct);
                    ids.AddRange(result.PurchaseOrderIds);
                }
                foreach (var row in rows.Where(x => !x.PreferredSupplierId.HasValue))
                    flags += await UpsertAlert("ReorderSupplierReview", $"{rule.Id}:{actor.BranchId}:{row.GodownId}:{row.ProductId}:{now:yyyy-MM-dd}", BusinessAlertCategory.Purchasing, BusinessAlertSeverity.Warning,
                        "Supplier selection required", $"{row.Product}: no defensible supplier suggestion; select a supplier before creating a draft PO.", actor.BranchId, row.GodownId, ct);
            }
            else
            {
                // Reuse the alert source-of-truth; actions remain read-side and never post financial documents.
                alerts += await RefreshAlertsCoreAsync(actor, ct, rule.TriggerType, conditions, rule.Id);
                if (rule.ActionType == AutomationActionType.FlagForReview)
                {
                    var sources = rule.TriggerType switch {
                        AutomationTriggerType.StockBelowReorder => new[] { "LowStock" }, AutomationTriggerType.NearExpiry => new[] { "Expiry" },
                        AutomationTriggerType.CustomerOverdue => new[] { "CustomerOverdue" }, AutomationTriggerType.SupplierDue => new[] { "SupplierDue", "SupplierOverdue" },
                        _ => new[] { "BelowCost", "LowMargin" }
                    };
                    var ruleScope = $":rule:{rule.Id:N}:{actor.BranchId:N}:";
                    var candidates = await db.BusinessAlerts.AsNoTracking().Where(x => !x.ResolvedAtUtc.HasValue && x.BranchId == actor.BranchId && sources.Contains(x.SourceType) && x.SourceKey.Contains(ruleScope)).ToListAsync(ct);
                    foreach (var row in candidates) flags += await UpsertAlert("AutomationReview", $"{rule.Id}:{actor.BranchId}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(row.SourceKey)))}:{now:yyyy-MM-dd}", row.Category, row.Severity, ("Review: " + row.Title)[..Math.Min(200, 8 + row.Title.Length)], row.Description, row.BranchId, row.GodownId, ct);
                }
            }
            rule.LastRunAtUtc = now; executed++;
            db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "AutomationExecuted", EntityType = "AutomationRule", EntityId = key,
                NewValues = JsonSerializer.Serialize(new { RuleId = rule.Id, actor.BranchId, rule.TriggerType, rule.ActionType, Window = now.ToString("yyyy-MM-dd"), AlertsCreated = alerts, ReviewFlagsCreated = flags, DraftPurchaseOrderIds = ids }) });
            await db.SaveChangesAsync(ct);
        }
        if (transaction != null) await transaction.CommitAsync(ct);
        return new(alerts, flags, ids.Distinct().ToList(), executed, duplicate);
    }

    private static Dictionary<string, decimal> ReadAutomationConditions(AutomationTriggerType trigger, string json)
    {
        try {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new RequestValidationException("Automation conditions must be an object.");
            var allowed = trigger switch {
                AutomationTriggerType.StockBelowReorder => new[] { "minimumOrderQuantity" },
                AutomationTriggerType.NearExpiry => new[] { "warningDays" },
                AutomationTriggerType.CustomerOverdue => new[] { "highOutstandingAmount", "nearCreditLimitPercent" },
                AutomationTriggerType.SupplierDue => new[] { "highOutstandingAmount", "dueWithinDays" },
                AutomationTriggerType.MarginBelowThreshold => new[] { "minimumMarginPercent" },
                _ => Array.Empty<string>()
            };
            var result = new Dictionary<string, decimal>();
            foreach (var property in document.RootElement.EnumerateObject()) {
                if (!allowed.Contains(property.Name) || !property.Value.TryGetDecimal(out var value) || value < 0 || value > 100000000
                    || property.Name == "minimumOrderQuantity" && (value < 1 || value != Math.Truncate(value))
                    || property.Name is "warningDays" or "dueWithinDays" && (value > 730 || value != Math.Truncate(value))
                    || property.Name == "warningDays" && value < 1
                    || property.Name == "nearCreditLimitPercent" && value is <= 0 or > 100
                    || property.Name == "minimumMarginPercent" && value >= 100
                    || property.Name == "highOutstandingAmount" && value <= 0 || !result.TryAdd(property.Name, value))
                    throw new RequestValidationException("Unsupported or invalid automation condition.");
            }
            return result;
        } catch (JsonException) { throw new RequestValidationException("Automation conditions must be valid JSON."); }
        catch (InvalidOperationException) { throw new RequestValidationException("Automation conditions must contain numeric values."); }
    }
}
