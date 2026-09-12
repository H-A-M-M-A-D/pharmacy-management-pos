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
    public async Task<ReorderDraftResult> CreateReorderDraftsAsync(Guid actorId, ReorderDraftRequest request, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchaseOrdersCreate, ct);
        await Require(actorId, PermissionCatalog.InventoryReorderView, ct);
        if (!request.Confirmed || request.Lines.Count == 0 || request.Lines.Count > 500 || request.Lines.Any(x => x.FinalQuantity <= 0 || x.SuggestedQuantity <= 0)
            || request.Lines.GroupBy(x => new { x.ProductId, x.BranchId, x.GodownId }).Any(x => x.Count() > 1))
            throw new RequestValidationException("Confirm a nonempty selection with positive quantities and no duplicate locations.");
        foreach (var line in request.Lines) EnsureBranch(actor, line.BranchId);
        var ordered = request.Lines.OrderBy(x => x.BranchId).ThenBy(x => x.GodownId).ThenBy(x => x.SupplierId).ThenBy(x => x.ProductId).ToArray();
        // Suppress repeat submissions within the execution window, while allowing a future replenishment cycle.
        var fingerprint = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Window = clock.GetUtcNow().ToString("yyyy-MM-dd"), Lines = ordered }))).AsSpan(0, 16));
        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(ct) : null;
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(actor.BranchId.ToByteArray(), 0)})", ct);
        var previous = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Action == "ReorderDraftsCreated" && x.EntityId == fingerprint, ct);
        if (previous != null) return JsonSerializer.Deserialize<ReorderDraftResult>(previous.NewValues!)! with { DuplicateSuppressed = true };
        var suggestions = await ListReorderSuggestionsAsync(actorId, actor.BranchId, null, ct);
        foreach (var line in ordered)
        {
            var suggestion = suggestions.SingleOrDefault(x => x.ProductId == line.ProductId && x.BranchId == line.BranchId && x.GodownId == line.GodownId);
            if (suggestion == null || suggestion.SuggestedOrderQuantity != line.SuggestedQuantity)
                throw new RequestValidationException("Reorder suggestions changed. Refresh and review the quantities.");
            if (!await db.Suppliers.AnyAsync(x => x.Id == line.SupplierId && x.IsActive, ct)) throw new RequestValidationException("Select an active supplier.");
            if (line.GodownId.HasValue && !await db.Godowns.AnyAsync(x => x.Id == line.GodownId && x.BranchId == actor.BranchId && x.IsActive, ct))
                throw new RequestValidationException("Select an active godown in your branch.");
        }
        var ids = new List<Guid>();
        foreach (var group in ordered.GroupBy(x => new { x.BranchId, x.GodownId, x.SupplierId }))
        {
            var po = new PurchaseOrder { BranchId = group.Key.BranchId, SupplierId = group.Key.SupplierId,
                GodownId = group.Key.GodownId,
                OrderNumber = $"REORDER-{Guid.NewGuid():N}", OrderDate = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime),
                Status = PurchaseOrderStatus.Draft, CreatedByUserId = actorId,
                Notes = $"Reorder draft. Intended godown: {group.Key.GodownId}. Review supplier, prices, and quantities before submission." };
            foreach (var line in group)
            {
                var suggestion = suggestions.Single(x => x.ProductId == line.ProductId && x.GodownId == line.GodownId);
                po.Items.Add(new PurchaseOrderItem { ProductId = line.ProductId, OrderedQuantity = line.FinalQuantity,
                    SuggestedOrderQuantity = line.SuggestedQuantity,
                    ExpectedPurchasePrice = suggestion.PreferredSupplierId == line.SupplierId ? suggestion.LastPurchaseRate : null,
                    Notes = $"Suggested quantity: {line.SuggestedQuantity}; reviewed quantity: {line.FinalQuantity}; godown: {line.GodownId}" });
            }
            db.PurchaseOrders.Add(po); ids.Add(po.Id);
        }
        var result = new ReorderDraftResult(ids, false);
        db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "ReorderDraftsCreated", EntityType = "Reorder", EntityId = fingerprint, OldValues = JsonSerializer.Serialize(ordered), NewValues = JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return result;
    }
}
