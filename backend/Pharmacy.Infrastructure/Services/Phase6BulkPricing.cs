using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Phase6;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Services;

public sealed partial class Phase6Service
{
    public async Task<BulkPricingPreview> PreviewBulkPricingAsync(Guid actorId, BulkPricingRequest request, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PricingManage, ct);
        if (!Enum.IsDefined(request.Action) || request.Value < 0 || request.RoundingIncrement <= 0 || request.RoundingIncrement > 1000
            || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500
            || request.Action == BulkPricingAction.SetMargin && request.Value >= 100
            || request.Action == BulkPricingAction.DecreasePercent && request.Value > 100)
            throw new RequestValidationException("Bulk pricing action, value, rounding, or reason is invalid.");
        if (request.PriceLevelId.HasValue)
        {
            var level = await db.PriceLevels.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.PriceLevelId && x.IsActive, ct)
                ?? throw new ResourceNotFoundException("Active price level was not found.");
            if (level.BranchId.HasValue) EnsureBranch(actor, level.BranchId.Value);
        }
        var products = await db.Products.AsNoTracking().Where(x => x.IsActive
            && (!request.CategoryId.HasValue || x.CategoryId == request.CategoryId)
            && (!request.ManufacturerId.HasValue || x.ManufacturerId == request.ManufacturerId)
            && (request.ProductIds == null || request.ProductIds.Contains(x.Id))).OrderBy(x => x.Id).ToListAsync(ct);
        var prices = request.PriceLevelId.HasValue
            ? await db.ProductPriceLevels.AsNoTracking().Where(x => x.PriceLevelId == request.PriceLevelId && x.IsActive).ToDictionaryAsync(x => x.ProductId, ct)
            : new Dictionary<Guid, ProductPriceLevel>();
        var rows = new List<BulkPriceRow>();
        foreach (var product in products)
        {
            var levelPrice = prices.GetValueOrDefault(product.Id);
            if (request.PriceLevelId.HasValue && levelPrice == null) continue;
            var old = levelPrice?.SellingPrice ?? product.RetailPrice;
            var raw = request.Action switch
            {
                BulkPricingAction.IncreasePercent => old * (1 + request.Value / 100),
                BulkPricingAction.DecreasePercent => old * (1 - request.Value / 100),
                BulkPricingAction.SetMargin => product.PurchasePrice / (1 - request.Value / 100),
                BulkPricingAction.SetMarkup => product.PurchasePrice * (1 + request.Value / 100),
                _ => old
            };
            var next = Money(Math.Ceiling(raw / request.RoundingIncrement) * request.RoundingIncrement);
            if (next < product.PurchasePrice) throw new RequestValidationException($"The proposed price for {product.Name} is below cost.");
            rows.Add(new(product.Id, product.Name, request.PriceLevelId, product.PurchasePrice, old, next, product.UpdatedAt, levelPrice?.UpdatedAt));
        }
        if (rows.Count == 0) throw new RequestValidationException("No active prices match the selected filters.");
        var preview = new BulkPricingPreview(Guid.NewGuid(), clock.GetUtcNow().UtcDateTime.AddMinutes(15), request.Reason.Trim(), rows);
        db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "BulkPricingPreview", EntityType = "BulkPricing", EntityId = preview.PreviewId, NewValues = JsonSerializer.Serialize(preview) });
        await db.SaveChangesAsync(ct);
        return preview;
    }

    public async Task<int> ApplyBulkPricingAsync(Guid actorId, BulkPricingApplyRequest request, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PricingManage, ct);
        if (!request.Confirmed) throw new RequestValidationException("Explicit confirmation of the preview is required.");
        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(ct) : null;
        // Serialize consumption of a preview across processes; individual prices use compare-and-swap below.
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(request.PreviewId.ToByteArray(), 0)})", ct);
        var audit = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(x => x.EntityId == request.PreviewId && x.Action == "BulkPricingPreview" && x.UserId == actorId, ct)
            ?? throw new ResourceNotFoundException("Your bulk pricing preview was not found.");
        if (await db.AuditLogs.AnyAsync(x => x.EntityId == request.PreviewId && x.Action == "BulkPricingApplied", ct))
            throw new RequestValidationException("This preview has already been applied.");
        var preview = JsonSerializer.Deserialize<BulkPricingPreview>(audit.NewValues!)!;
        if (preview.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime) throw new RequestValidationException("The preview has expired. Preview again.");
        foreach (var levelId in preview.Rows.Where(x => x.PriceLevelId.HasValue).Select(x => x.PriceLevelId!.Value).Distinct())
        {
            var level = await db.PriceLevels.AsNoTracking().SingleOrDefaultAsync(x => x.Id == levelId && x.IsActive, ct) ?? throw new RequestValidationException("The preview's price level is no longer active.");
            if (level.BranchId.HasValue) EnsureBranch(actor, level.BranchId.Value);
        }
        // Validate every row before mutation, including on the in-memory test provider.
        foreach (var row in preview.Rows)
        {
            var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == row.ProductId && x.IsActive, ct);
            var price = row.PriceLevelId.HasValue ? await db.ProductPriceLevels.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == row.ProductId && x.PriceLevelId == row.PriceLevelId && x.IsActive, ct) : null;
            if (product == null || product.UpdatedAt != row.ProductVersion || product.PurchasePrice != row.Cost
                || (row.PriceLevelId.HasValue ? price == null || price.SellingPrice != row.OldPrice || price.UpdatedAt != row.PriceVersion : product.RetailPrice != row.OldPrice))
                throw new RequestValidationException("Prices or costs changed since preview. Preview again.");
        }
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var row in preview.Rows)
        {
            if (db.Database.IsRelational())
            {
                // Lock and recheck the cost/version even when editing a separate price-level row.
                var changed = await db.Products.Where(x => x.Id == row.ProductId && x.UpdatedAt == row.ProductVersion && x.PurchasePrice == row.Cost && x.IsActive
                    && (row.PriceLevelId.HasValue || x.RetailPrice == row.OldPrice))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, now).SetProperty(x => x.RetailPrice, x => row.PriceLevelId.HasValue ? x.RetailPrice : row.NewPrice), ct);
                if (changed != 1) throw new RequestValidationException("Concurrent product update. Preview again.");
                if (row.PriceLevelId.HasValue && await db.ProductPriceLevels.Where(x => x.ProductId == row.ProductId && x.PriceLevelId == row.PriceLevelId && x.IsActive && x.SellingPrice == row.OldPrice && x.UpdatedAt == row.PriceVersion)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.SellingPrice, row.NewPrice).SetProperty(x => x.UpdatedAt, now), ct) != 1)
                    throw new RequestValidationException("Concurrent price update. Preview again.");
            }
            else
            {
                if (row.PriceLevelId.HasValue) (await db.ProductPriceLevels.SingleAsync(x => x.ProductId == row.ProductId && x.PriceLevelId == row.PriceLevelId, ct)).SellingPrice = row.NewPrice;
                else (await db.Products.SingleAsync(x => x.Id == row.ProductId, ct)).RetailPrice = row.NewPrice;
            }
            db.PricingPriceHistories.Add(new PricingPriceHistory { ProductId = row.ProductId, PriceLevelId = row.PriceLevelId, OldPrice = row.OldPrice, NewPrice = row.NewPrice, ActorId = actorId, Reason = preview.Reason });
            db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "BulkPriceChanged", EntityType = "Product", EntityId = row.ProductId, OldValues = JsonSerializer.Serialize(new { Price = row.OldPrice, row.PriceLevelId }), NewValues = JsonSerializer.Serialize(new { Price = row.NewPrice, preview.Reason, preview.PreviewId, row.PriceLevelId }) });
        }
        db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "BulkPricingApplied", EntityType = "BulkPricing", EntityId = preview.PreviewId, NewValues = JsonSerializer.Serialize(new { Count = preview.Rows.Count }) });
        await db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return preview.Rows.Count;
    }
}
