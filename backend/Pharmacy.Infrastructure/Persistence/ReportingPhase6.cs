using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Phase6;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class ReportingRepository
{
    public async Task<object> Phase6ReportAsync(ReportQuery q, string report, CancellationToken ct)
    {
        var json = await db.SystemSettings.AsNoTracking().Where(x => x.Key == "phase6.thresholds").Select(x => x.Value).SingleOrDefaultAsync(ct);
        var settings = json == null ? new Phase6Thresholds() : JsonSerializer.Deserialize<Phase6Thresholds>(json)!;
        if (report == "stockout-risk")
        {
            var positions = await BatchPositions(q).GroupBy(x => new { x.ProductId, x.Product, x.BranchId, x.Branch, x.GodownId, x.Godown })
                .Select(g => new { g.Key, Quantity = g.Sum(x => x.Quantity) }).ToListAsync(ct);
            var daily = await db.StockMovements.AsNoTracking().Where(x => x.MovementType == StockMovementType.Sale && x.CreatedAt >= q.ToUtc.AddDays(-30) && x.CreatedAt < q.ToUtc
                && (!q.BranchId.HasValue || x.BranchId == q.BranchId) && (!q.GodownId.HasValue || x.GodownId == q.GodownId))
                .GroupBy(x => new { x.ProductId, x.BranchId, x.GodownId }).Select(g => new { g.Key, Daily = -g.Sum(x => x.Quantity) / 30m }).ToListAsync(ct);
            var rows = positions.Select(x => {
                var velocity = daily.SingleOrDefault(s => s.Key.ProductId == x.Key.ProductId && s.Key.BranchId == x.Key.BranchId && s.Key.GodownId == x.Key.GodownId)?.Daily ?? 0;
                var days = velocity > 0 ? x.Quantity / velocity : (decimal?)null;
                return new { x.Key.ProductId, x.Key.Product, x.Key.BranchId, x.Key.Branch, x.Key.GodownId, x.Key.Godown, CurrentQuantity = x.Quantity, AverageDailySales = velocity, DaysOfStock = days, Risk = x.Quantity <= 0 ? "Out of stock" : "Stockout within seven days" };
            }).Where(x => x.CurrentQuantity <= 0 || x.DaysOfStock <= 7).OrderBy(x => x.CurrentQuantity).ThenBy(x => x.ProductId).ToList();
            return new PagedReport<object>(rows.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Cast<object>().ToList(), rows.Count, q.Page, q.PageSize);
        }
        if (report == "price-history")
            return await ReportPageAsync(db.PricingPriceHistories.AsNoTracking().Where(x => x.CreatedAt >= q.FromUtc && x.CreatedAt < q.ToUtc
                && (!q.ProductId.HasValue || x.ProductId == q.ProductId) && (!q.PriceLevelId.HasValue || x.PriceLevelId == q.PriceLevelId)
                && (!q.CategoryId.HasValue || x.Product!.CategoryId == q.CategoryId) && (!q.ManufacturerId.HasValue || x.Product!.ManufacturerId == q.ManufacturerId)
                && (!q.UserId.HasValue || x.ActorId == q.UserId)
                && (!q.BranchId.HasValue || x.PriceLevelId == null || x.PriceLevel!.BranchId == null || x.PriceLevel.BranchId == q.BranchId))
                .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new { x.Id, Product = x.Product!.Name, x.ProductId, x.PriceLevelId, x.OldPrice, x.NewPrice, x.Reason, x.ActorId, Actor = x.Actor!.FullName, x.CreatedAt }), q, ct);
        // Phase 5 allocation analytics retain the promotional sale's price-source snapshot on returns.
        if (report == "promotion-performance") return await SalesAnalyticsAsync(q with { PriceSourceFilter = PriceSource.Promotion }, "product", ct);
        if (report is "margin-exceptions" or "low-margin")
        {
            var retail = db.Products.AsNoTracking().Where(x => x.IsActive && !q.PriceLevelId.HasValue && (!q.ProductId.HasValue || x.Id == q.ProductId)
                && (!q.CategoryId.HasValue || x.CategoryId == q.CategoryId) && (!q.ManufacturerId.HasValue || x.ManufacturerId == q.ManufacturerId))
                .Select(x => new { ProductId = x.Id, Product = x.Name, Source = "Retail", PriceLevelId = (Guid?)null, MinimumQuantity = (int?)null, Cost = x.PurchasePrice, SellingPrice = x.RetailPrice });
            var levels = db.ProductPriceLevels.AsNoTracking().Where(x => x.IsActive && x.Product!.IsActive && x.PriceLevel!.IsActive && (!q.PriceLevelId.HasValue || x.PriceLevelId == q.PriceLevelId)
                && (x.PriceLevel.BranchId == null || !q.BranchId.HasValue || x.PriceLevel.BranchId == q.BranchId) && (!q.ProductId.HasValue || x.ProductId == q.ProductId)
                && (!q.CategoryId.HasValue || x.Product.CategoryId == q.CategoryId) && (!q.ManufacturerId.HasValue || x.Product.ManufacturerId == q.ManufacturerId))
                .Select(x => new { x.ProductId, Product = x.Product!.Name, Source = "Price Level", PriceLevelId = (Guid?)x.PriceLevelId, MinimumQuantity = (int?)null, Cost = x.Product.PurchasePrice, x.SellingPrice });
            var breaks = db.ProductPriceBreaks.AsNoTracking().Where(x => x.IsActive && x.Product!.IsActive && (x.PriceLevelId == null || x.PriceLevel!.IsActive)
                && (!q.PriceLevelId.HasValue || x.PriceLevelId == null || x.PriceLevelId == q.PriceLevelId)
                && (x.PriceLevelId == null || x.PriceLevel!.BranchId == null || !q.BranchId.HasValue || x.PriceLevel.BranchId == q.BranchId) && (!q.ProductId.HasValue || x.ProductId == q.ProductId)
                && (!q.CategoryId.HasValue || x.Product.CategoryId == q.CategoryId) && (!q.ManufacturerId.HasValue || x.Product.ManufacturerId == q.ManufacturerId))
                .Select(x => new { x.ProductId, Product = x.Product!.Name, Source = "Quantity Break", x.PriceLevelId, MinimumQuantity = (int?)x.MinimumQuantity, Cost = x.Product.PurchasePrice, x.SellingPrice });
            var prices = retail.Concat(levels).Concat(breaks).Where(x => report == "margin-exceptions" ? x.SellingPrice < x.Cost : x.SellingPrice <= 0 || (x.SellingPrice - x.Cost) / x.SellingPrice * 100 < settings.MinimumMarginPercent);
            return await ReportPageAsync(prices.OrderBy(x => x.ProductId).ThenBy(x => x.PriceLevelId).ThenBy(x => x.MinimumQuantity).Select(x => new { x.ProductId, x.Product, x.Source, x.PriceLevelId, x.MinimumQuantity, x.Cost, x.SellingPrice, MarginPercent = x.SellingPrice <= 0 ? 0 : (x.SellingPrice - x.Cost) / x.SellingPrice * 100 }), q, ct);
        }
        if (report == "slow-dead-stock")
        {
            var rows = await SlowStockAsync(q with { SlowMovingDays = settings.SlowMovingDays, DeadStockDays = settings.DeadStockDays }, 0, ct);
            return new PagedReport<SlowStockDto>(rows.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToList(), rows.Count, q.Page, q.PageSize);
        }
        if (report == "expiry-summary")
        {
            var today = BusinessDate(q.ToUtc.AddTicks(-1));
            return await ReportPageAsync(BatchPositions(q).Where(x => x.Quantity > 0 && x.Expiry <= today.AddDays(settings.ExpiryWarningDays))
                .OrderBy(x => x.Expiry).ThenBy(x => x.BatchId).Select(x => new { x.BatchId, x.BatchNumber, x.ProductId, x.Product, x.BranchId, x.Branch, x.GodownId, x.Godown, x.Quantity, StockValue = x.Quantity * x.Cost, ExpiryDate = x.Expiry, Status = x.Expiry < today ? "Expired" : "Near expiry" }), q, ct);
        }
        if (report == "automation-log")
        {
            var logs = await db.AuditLogs.AsNoTracking().Where(x => x.Action == "AutomationExecuted" && x.CreatedAt >= q.FromUtc && x.CreatedAt < q.ToUtc && (!q.UserId.HasValue || x.UserId == q.UserId))
                .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
            var scoped = logs.Where(x => {
                using var values = JsonDocument.Parse(x.NewValues!);
                return !q.BranchId.HasValue || values.RootElement.TryGetProperty("BranchId", out var branch) && branch.GetGuid() == q.BranchId;
            }).Select(x => new { x.Id, x.CreatedAt, x.EntityId, x.UserId, Execution = JsonSerializer.Deserialize<JsonElement>(x.NewValues!) }).ToList();
            return new PagedReport<object>(scoped.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Cast<object>().ToList(), scoped.Count, q.Page, q.PageSize);
        }
        if (report == "alert-summary")
            return await ReportPageAsync(db.BusinessAlerts.AsNoTracking().Where(x => x.CreatedAt >= q.FromUtc && x.CreatedAt < q.ToUtc
                && (x.Category != BusinessAlertCategory.Receivables || q.CanViewReceivables) && (x.Category != BusinessAlertCategory.Payables || q.CanViewPayables)
                && (x.BranchId == null || !q.BranchId.HasValue || x.BranchId == q.BranchId) && (!q.GodownId.HasValue || x.GodownId == q.GodownId)
                && (!q.GodownUserId.HasValue || x.GodownId == null || db.UserGodowns.Any(g => g.UserId == q.GodownUserId && g.GodownId == x.GodownId)))
                .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new { x.Id, x.Category, x.Severity, x.Title, x.Description, x.BranchId, x.GodownId, x.SourceType, x.SourceKey, x.CreatedAt, x.DismissedAtUtc, x.ResolvedAtUtc }), q, ct);
        throw new RequestValidationException("Unknown Phase 6 report.");
    }
}
