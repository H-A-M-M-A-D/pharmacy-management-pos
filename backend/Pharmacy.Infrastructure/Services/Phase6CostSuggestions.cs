using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Phase6;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Services;

public sealed partial class Phase6Service
{
    public async Task<IReadOnlyList<PurchaseCostSuggestionDto>> ListPurchaseCostSuggestionsAsync(Guid actorId, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PricingSuggest, ct);
        await RequireCost(actorId, ct);
        var rows = await db.AuditLogs.AsNoTracking().Where(x => x.Action == "PurchaseCostSuggestion").OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var accepted = await db.AuditLogs.AsNoTracking().Where(x => x.Action == "BulkPricingApplied").Select(x => x.EntityId).ToListAsync(ct);
        var settings = await ThresholdsCore(ct);
        return rows.Select(x => JsonSerializer.Deserialize<PurchaseCostSuggestionDto>(x.NewValues!)!).Where(x => x.BranchId == actor.BranchId && !accepted.Contains(x.Id))
            .Select(x => x with { SuggestedPrice = Money(Math.Ceiling(Math.Max(x.SuggestedPrice, Math.Max(x.NewCost, x.OldCost)) / settings.RoundingIncrement) * settings.RoundingIncrement) }).ToList();
    }
    public async Task<int> AcceptPurchaseCostSuggestionAsync(Guid actorId, Guid id, bool confirmed, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PricingManage, ct);
        await RequireCost(actorId, ct);
        if (!confirmed) throw new RequestValidationException("Explicit confirmation is required.");
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(id.ToByteArray(), 0)})", ct);
        var audit = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Action == "PurchaseCostSuggestion" && x.EntityId == id, ct) ?? throw new ResourceNotFoundException("Suggestion was not found.");
        var suggestion = JsonSerializer.Deserialize<PurchaseCostSuggestionDto>(audit.NewValues!)!;
        EnsureBranch(actor, suggestion.BranchId);
        var product = await db.Products.AsNoTracking().SingleAsync(x => x.Id == suggestion.ProductId, ct);
        if (product.UpdatedAt != suggestion.ProductVersion || product.RetailPrice != suggestion.CurrentSellingPrice || product.PurchasePrice != suggestion.OldCost)
            throw new RequestValidationException("Product prices or costs changed. Request a fresh pricing suggestion.");
        var settings = await ThresholdsCore(ct);
        var next = Money(Math.Ceiling(Math.Max(suggestion.SuggestedPrice, Math.Max(suggestion.NewCost, product.PurchasePrice)) / settings.RoundingIncrement) * settings.RoundingIncrement);
        var preview = new BulkPricingPreview(id, clock.GetUtcNow().UtcDateTime.AddMinutes(15), $"Accepted purchase-cost suggestion for GRN {suggestion.GoodsReceiptId}",
            [new(product.Id, product.Name, null, product.PurchasePrice, product.RetailPrice, next, product.UpdatedAt, null)]);
        // Preview is derived entirely from the persisted GRN suggestion, never from client-supplied prices.
        var stored = await db.AuditLogs.SingleOrDefaultAsync(x => x.Action == "BulkPricingPreview" && x.EntityId == id, ct);
        if (stored == null) { db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "BulkPricingPreview", EntityType = "BulkPricing", EntityId = id, NewValues = JsonSerializer.Serialize(preview) }); await db.SaveChangesAsync(ct); }
        else if (stored.UserId != actorId) throw new RequestValidationException("Another manager is reviewing this suggestion.");
        var count = await ApplyBulkPricingAsync(actorId, new(id, true), ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return count;
    }
    public async Task<IReadOnlyList<ExpiryDiscountSuggestionDto>> ListExpiryDiscountSuggestionsAsync(Guid actorId, decimal discountPercent, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PricingSuggest, ct);
        await RequireCost(actorId, ct);
        if (discountPercent is < 0 or > 100) throw new RequestValidationException("Discount must be between 0 and 100 percent.");
        var settings = await ThresholdsCore(ct);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var unrestricted = actor.Role!.Name is RoleCatalog.Owner or RoleCatalog.Manager;
        var rows = await db.Inventory.AsNoTracking().Include(x => x.Product).Include(x => x.ProductBatch).Where(x => x.BranchId == actor.BranchId && x.QuantityInStock > 0
            && (unrestricted || db.UserGodowns.Any(g => g.UserId == actorId && g.GodownId == x.GodownId))
            && x.ProductBatch!.ExpiryDate >= today && x.ProductBatch.ExpiryDate <= today.AddDays(settings.ExpiryWarningDays)).OrderBy(x => x.ProductBatch!.ExpiryDate).ThenBy(x => x.Id).ToListAsync(ct);
        return rows.Select(x => {
            var batch = x.ProductBatch!;
            var raw = batch.RetailPrice * (1 - discountPercent / 100);
            var price = Money(Math.Ceiling(Math.Max(raw, batch.PurchasePrice) / settings.RoundingIncrement) * settings.RoundingIncrement);
            return new ExpiryDiscountSuggestionDto(batch.Id, x.ProductId, x.Product!.Name, batch.BatchNumber, x.BranchId, x.GodownId, batch.ExpiryDate, x.QuantityInStock,
                batch.RetailPrice <= 0 ? 0 : Money((batch.RetailPrice - price) / batch.RetailPrice * 100), batch.RetailPrice, price,
                price <= 0 ? 0 : Money((price - batch.PurchasePrice) / price * 100), raw < batch.PurchasePrice);
        }).ToList();
    }
    private async Task RequireCost(Guid actorId, CancellationToken ct)
    {
        var actor = await db.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission).SingleAsync(x => x.Id == actorId, ct);
        if (!actor.Role!.RolePermissions.Any(x => x.Permission!.Code is PermissionCatalog.SalesCostView or PermissionCatalog.ReportsProfitability))
            throw new ForbiddenOperationException("Cost visibility permission is required for this suggestion.");
    }
}
