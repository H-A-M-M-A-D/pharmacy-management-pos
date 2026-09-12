using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class ReportingRepository
{
    public async Task<object> InventoryAlertsAsync(ReportQuery q, CancellationToken ct)
    {
        var positions = BatchPositions(q);
        var day = BusinessDate(q.ToUtc.AddTicks(-1));
        var grouped = positions.GroupBy(x => new { x.ProductId, x.ReorderLevel }).Select(g => new
        {
            g.Key.ProductId, g.Key.ReorderLevel, Qty = g.Sum(x => x.Quantity), Value = g.Sum(x => x.Quantity * x.Cost),
            LastSale = db.SaleItems.Where(i => i.ProductId == g.Key.ProductId && i.Sale!.Status == SaleStatus.Posted &&
                i.Sale.PostedAtUtc < q.ToUtc && (!q.BranchId.HasValue || i.Sale.BranchId == q.BranchId) &&
                (!q.GodownId.HasValue || i.Sale.GodownId == q.GodownId) &&
                (!q.GodownUserId.HasValue || db.UserGodowns.Any(u => u.UserId == q.GodownUserId && u.GodownId == i.Sale.GodownId))).Max(i => i.Sale!.PostedAtUtc)
        });
        return new
        {
            InventoryValue = await positions.SumAsync(x => (decimal?)(x.Quantity * x.Cost), ct) ?? 0,
            CurrentStock = await positions.SumAsync(x => (int?)x.Quantity, ct) ?? 0,
            LowStockItems = await grouped.CountAsync(x => x.Qty > 0 && x.Qty <= x.ReorderLevel, ct),
            OutOfStockItems = await grouped.CountAsync(x => x.Qty <= 0, ct),
            NearExpiryValue = await positions.Where(x => x.Quantity > 0 && x.Expiry >= day && x.Expiry <= day.AddDays(30)).SumAsync(x => (decimal?)(x.Quantity * x.Cost), ct) ?? 0,
            ExpiredStockValue = await positions.Where(x => x.Quantity > 0 && x.Expiry < day).SumAsync(x => (decimal?)(x.Quantity * x.Cost), ct) ?? 0,
            SlowMovingInventoryValue = await grouped.Where(x => x.Qty > 0 && x.LastSale < q.ToUtc.AddDays(-q.SlowMovingDays) && x.LastSale >= q.ToUtc.AddDays(-q.DeadStockDays)).SumAsync(x => (decimal?)x.Value, ct) ?? 0,
            DeadStockValue = await grouped.Where(x => x.Qty > 0 && (x.LastSale == null || x.LastSale < q.ToUtc.AddDays(-q.DeadStockDays))).SumAsync(x => (decimal?)x.Value, ct) ?? 0
        };
    }
    private sealed class BatchPosition
    {
        public Guid BatchId { get; set; }
        public string BatchNumber { get; set; } = "";
        public string Supplier { get; set; } = "";
        public Guid ProductId { get; set; }
        public string Product { get; set; } = "";
        public string Sku { get; set; } = "";
        public Guid BranchId { get; set; }
        public string Branch { get; set; } = "";
        public Guid? GodownId { get; set; }
        public string Godown { get; set; } = "";
        public int ReorderLevel { get; set; }
        public DateOnly Expiry { get; set; }
        public DateTime ReceivedAt { get; set; }
        public int Quantity { get; set; }
        public decimal Cost { get; set; }
        public decimal Retail { get; set; }
    }

    // Quantity always derives from immutable movements; batch prices value the physical batch.
    private IQueryable<BatchPosition> BatchPositions(ReportQuery q) => db.ProductBatches.AsNoTracking()
        .Where(x => (!q.BranchId.HasValue || x.BranchId == q.BranchId) && (q.BranchIds == null || q.BranchIds.Contains(x.BranchId)) &&
            (!q.GodownUserId.HasValue || x.GodownId.HasValue && db.UserGodowns.Any(g => g.UserId == q.GodownUserId && g.GodownId == x.GodownId)) &&
            (!q.GodownId.HasValue || x.GodownId == q.GodownId) &&
            (!q.ProductId.HasValue || x.ProductId == q.ProductId) &&
            (!q.BatchId.HasValue || x.Id == q.BatchId) &&
            (!q.CategoryId.HasValue || x.Product!.CategoryId == q.CategoryId) &&
            (!q.ManufacturerId.HasValue || x.Product!.ManufacturerId == q.ManufacturerId) &&
            (!q.SupplierId.HasValue || x.SupplierId == q.SupplierId))
        .Select(x => new BatchPosition
        {
            BatchId = x.Id, BatchNumber = x.BatchNumber,
            Supplier = x.Supplier != null ? x.Supplier.Name : "",
            ProductId = x.ProductId, Product = x.Product!.Name, Sku = x.Product.SKU,
            BranchId = x.BranchId, Branch = x.Branch!.Name, GodownId = x.GodownId,
            Godown = x.Godown != null ? x.Godown.Name : "Legacy/unscoped",
            ReorderLevel = x.Product.ReorderLevel, Expiry = x.ExpiryDate,
            ReceivedAt = x.StockMovements.Where(m => m.CreatedAt < q.ToUtc &&
                (m.MovementType == StockMovementType.Purchase || m.MovementType == StockMovementType.OpeningStock))
                .Min(m => (DateTime?)m.CreatedAt) ?? x.CreatedAt,
            Quantity = x.StockMovements.Where(m => m.CreatedAt < q.ToUtc).Sum(m => (int?)m.Quantity) ?? 0,
            Cost = x.PurchasePrice, Retail = x.RetailPrice
        });

    public async Task<PagedReport<StockPositionDto>> StockPositionAsync(ReportQuery q, CancellationToken ct)
    {
        var grouped = BatchPositions(q).GroupBy(x => new { x.ProductId, x.Product, x.Sku, x.BranchId, x.Branch, x.GodownId, x.Godown, x.ReorderLevel })
            .Select(g => new { g.Key, Qty = g.Sum(x => x.Quantity), Value = g.Sum(x => x.Quantity * x.Cost) });
        if (q.Status == "low") grouped = grouped.Where(x => x.Qty > 0 && x.Qty <= x.Key.ReorderLevel);
        if (q.Status == "out") grouped = grouped.Where(x => x.Qty <= 0);
        if (q.Status == "reorder") grouped = grouped.Where(x => x.Qty <= x.Key.ReorderLevel);
        if (!string.IsNullOrWhiteSpace(q.Search)) grouped = grouped.Where(x => x.Key.Product.Contains(q.Search) || x.Key.Sku.Contains(q.Search));
        var total = await grouped.CountAsync(ct);
        var rows = await grouped.OrderBy(x => x.Key.Product).ThenBy(x => x.Key.ProductId).ThenBy(x => x.Key.BranchId).ThenBy(x => x.Key.GodownId)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        return new(rows.Select(x => new StockPositionDto(x.Key.ProductId, x.Key.Product, x.Key.Sku, x.Key.BranchId, x.Key.Branch,
            x.Key.GodownId, x.Key.Godown, x.Qty, x.Value, x.Key.ReorderLevel,
            x.Qty <= 0 ? "Out of stock" : x.Qty <= x.Key.ReorderLevel ? "Low stock" : "Available")).ToList(), total, q.Page, q.PageSize);
    }

    public async Task<IReadOnlyList<ExposureDto>> InventoryExposureAsync(ReportQuery q, bool expiry, CancellationToken ct)
    {
        var day = BusinessDate(q.ToUtc.AddTicks(-1));
        var positions = BatchPositions(q).Where(x => x.Quantity > 0);
        var rows = expiry
            ? await positions.GroupBy(x => x.Expiry < day ? "Expired" : x.Expiry <= day.AddDays(30) ? "0–30" :
                x.Expiry <= day.AddDays(60) ? "31–60" : x.Expiry <= day.AddDays(90) ? "61–90" :
                x.Expiry <= day.AddDays(180) ? "91–180" : "181+")
                .Select(g => new ExposureDto(g.Key, g.Select(x => x.ProductId).Distinct().Count(), g.Count(), g.Sum(x => x.Quantity),
                    g.Sum(x => x.Quantity * x.Cost), g.Sum(x => x.Quantity * x.Retail), g.Key == "181+" ? 0 : g.Sum(x => x.Quantity * x.Cost), 0)).ToListAsync(ct)
            : await positions.GroupBy(x => x.ReceivedAt >= q.ToUtc.AddDays(-30) ? "0–30" : x.ReceivedAt >= q.ToUtc.AddDays(-60) ? "31–60" :
                x.ReceivedAt >= q.ToUtc.AddDays(-90) ? "61–90" : x.ReceivedAt >= q.ToUtc.AddDays(-180) ? "91–180" :
                x.ReceivedAt >= q.ToUtc.AddDays(-365) ? "181–365" : "365+")
                .Select(g => new ExposureDto(g.Key, g.Select(x => x.ProductId).Distinct().Count(), g.Count(), g.Sum(x => x.Quantity),
                    g.Sum(x => x.Quantity * x.Cost), g.Sum(x => x.Quantity * x.Retail), 0, 0)).ToListAsync(ct);
        var total = rows.Sum(x => x.CostValue);
        var buckets = expiry ? new[] { "Expired", "0–30", "31–60", "61–90", "91–180", "181+" } : ["0–30", "31–60", "61–90", "91–180", "181–365", "365+"];
        return buckets.Select(bucket => (rows.SingleOrDefault(x => x.Bucket == bucket) ?? new(bucket, 0, 0, 0, 0, 0, 0, 0))
            with { InventoryValuePercent = total == 0 ? 0 : decimal.Round((rows.SingleOrDefault(x => x.Bucket == bucket)?.CostValue ?? 0) / total * 100, 2) }).ToList();
    }

    public async Task<PagedReport<ProductPerformanceDto>> ProductPerformanceAsync(ReportQuery q, CancellationToken ct)
    {
        var sales = SalesFacts(q, "product");
        var batches = BatchPositions(q);
        var start = BusinessDate(q.FromUtc); var end = BusinessDate(q.ToUtc);
        var products = db.Products.AsNoTracking().Where(x => x.IsActive &&
            (batches.Any(b => b.ProductId == x.Id) || sales.Any(s => s.Key == x.Id.ToString())) &&
            (!q.ProductId.HasValue || x.Id == q.ProductId) && (!q.CategoryId.HasValue || x.CategoryId == q.CategoryId) &&
            (!q.ManufacturerId.HasValue || x.ManufacturerId == q.ManufacturerId) &&
            (string.IsNullOrWhiteSpace(q.Search) || x.Name.Contains(q.Search) || x.SKU.Contains(q.Search)));
        var query = products.Select(p => new
        {
            p.Id, p.Name, p.SKU,
            Sold = sales.Where(x => x.Key == p.Id.ToString()).Sum(x => (int?)x.SoldQuantity) ?? 0,
            Returned = sales.Where(x => x.Key == p.Id.ToString()).Sum(x => (int?)x.ReturnedQuantity) ?? 0,
            Net = sales.Where(x => x.Key == p.Id.ToString()).Sum(x => (decimal?)x.Net) ?? 0,
            Cost = sales.Where(x => x.Key == p.Id.ToString()).Sum(x => (decimal?)x.Cost) ?? 0,
            Stock = batches.Where(x => x.ProductId == p.Id).Sum(x => (int?)x.Quantity) ?? 0,
            StockValue = batches.Where(x => x.ProductId == p.Id).Sum(x => (decimal?)(x.Quantity * x.Cost)) ?? 0,
            PurchaseQty = db.GoodsReceiptItems.Where(x => x.ProductId == p.Id && x.GoodsReceipt!.Status == GoodsReceiptStatus.Posted &&
                x.GoodsReceipt.ReceiptDate >= start && x.GoodsReceipt.ReceiptDate < end &&
                (!q.BranchId.HasValue || x.GoodsReceipt.BranchId == q.BranchId) && (!q.GodownId.HasValue || x.GoodsReceipt.GodownId == q.GodownId) && (!q.SupplierId.HasValue || x.GoodsReceipt.SupplierId == q.SupplierId) && (!q.GodownUserId.HasValue || db.UserGodowns.Any(u => u.UserId == q.GodownUserId && u.GodownId == x.GoodsReceipt.GodownId)))
                .Sum(x => (int?)(x.PurchasedQuantity + x.BonusQuantity)) ?? 0,
            PurchaseValue = db.GoodsReceiptItems.Where(x => x.ProductId == p.Id && x.GoodsReceipt!.Status == GoodsReceiptStatus.Posted &&
                x.GoodsReceipt.ReceiptDate >= start && x.GoodsReceipt.ReceiptDate < end &&
                (!q.BranchId.HasValue || x.GoodsReceipt.BranchId == q.BranchId) && (!q.GodownId.HasValue || x.GoodsReceipt.GodownId == q.GodownId) && (!q.SupplierId.HasValue || x.GoodsReceipt.SupplierId == q.SupplierId) && (!q.GodownUserId.HasValue || db.UserGodowns.Any(u => u.UserId == q.GodownUserId && u.GodownId == x.GoodsReceipt.GodownId)))
                .Sum(x => (decimal?)x.NetLineAmount) ?? 0,
            LastSale = db.SaleItems.Where(x => x.ProductId == p.Id && x.Sale!.Status == SaleStatus.Posted && x.Sale.PostedAtUtc < q.ToUtc &&
                (!q.BranchId.HasValue || x.Sale.BranchId == q.BranchId) && (!q.GodownId.HasValue || x.Sale.GodownId == q.GodownId) && (!q.GodownUserId.HasValue || db.UserGodowns.Any(u => u.UserId == q.GodownUserId && u.GodownId == x.Sale.GodownId)))
                .Max(x => x.Sale!.PostedAtUtc),
            LastPurchase = db.GoodsReceiptItems.Where(x => x.ProductId == p.Id && x.GoodsReceipt!.Status == GoodsReceiptStatus.Posted &&
                x.GoodsReceipt.ReceiptDate < end && (!q.BranchId.HasValue || x.GoodsReceipt.BranchId == q.BranchId) &&
                (!q.GodownId.HasValue || x.GoodsReceipt.GodownId == q.GodownId) && (!q.SupplierId.HasValue || x.GoodsReceipt.SupplierId == q.SupplierId) && (!q.GodownUserId.HasValue || db.UserGodowns.Any(u => u.UserId == q.GodownUserId && u.GodownId == x.GoodsReceipt.GodownId))).Max(x => (DateOnly?)x.GoodsReceipt!.ReceiptDate)
        });
        if (q.Status == "fast") query = query.Where(x => x.Sold >= q.FastMovingQuantity);
        if (q.Status == "slow") query = query.Where(x => x.Sold < q.FastMovingQuantity && x.Stock > 0 && x.LastSale <= q.ToUtc.AddDays(-q.SlowMovingDays) && x.LastSale > q.ToUtc.AddDays(-q.DeadStockDays));
        if (q.Status == "dead") query = query.Where(x => x.Sold < q.FastMovingQuantity && x.Stock > 0 && (x.LastSale == null || x.LastSale <= q.ToUtc.AddDays(-q.DeadStockDays)));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Net).ThenBy(x => x.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        return new(rows.Select(x =>
        {
            int? sinceSale = x.LastSale.HasValue ? Math.Max(0, (q.ToUtc - x.LastSale.Value).Days) : null;
            int? sincePurchase = x.LastPurchase.HasValue ? Math.Max(0, dayNumber(end) - dayNumber(x.LastPurchase.Value)) : null;
            var classification = x.Sold >= q.FastMovingQuantity ? "Fast Moving" : x.Stock > 0 && (!sinceSale.HasValue || sinceSale >= q.DeadStockDays) ? "Dead Stock" :
                x.Stock > 0 && sinceSale >= q.SlowMovingDays ? "Slow Moving" : "Medium Moving";
            return new ProductPerformanceDto(x.Id, x.Name, x.SKU, x.Sold, x.Net, x.Net - x.Cost,
                x.Net == 0 ? 0 : decimal.Round((x.Net - x.Cost) / x.Net * 100, 2), x.PurchaseQty, x.PurchaseValue, x.Stock, x.StockValue,
                sinceSale, sincePurchase, x.Returned, x.Sold - x.Returned == 0 ? 0 : decimal.Round(x.Net / (x.Sold - x.Returned), 2), classification);
        }).ToList(), total, q.Page, q.PageSize);
    }

    private static int dayNumber(DateOnly day) => day.DayNumber;
}
