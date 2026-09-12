using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Phase6;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class ReportingRepository
{
    public async Task<IReadOnlyList<SlowStockDto>> SlowStockAsync(ReportQuery q, decimal minimumStockValue, CancellationToken ct)
    {
        var positions = BatchPositions(q).GroupBy(x => new { x.ProductId, x.Product, x.BranchId, x.Branch, x.GodownId, x.Godown })
            .Select(g => new { g.Key, Qty = g.Sum(x => x.Quantity), Value = g.Sum(x => x.Quantity * x.Cost), Received = g.Min(x => x.ReceivedAt) });
        var rows = await positions.Where(x => x.Qty > 0 && x.Value >= minimumStockValue).ToListAsync(ct);
        var sales = await db.StockMovements.AsNoTracking().Where(x => x.MovementType == StockMovementType.Sale && x.CreatedAt < q.ToUtc
            && (!q.BranchId.HasValue || x.BranchId == q.BranchId) && (!q.GodownId.HasValue || x.GodownId == q.GodownId)
            && (!q.GodownUserId.HasValue || db.UserGodowns.Any(g => g.UserId == q.GodownUserId && g.GodownId == x.GodownId)))
            .GroupBy(x => new { x.ProductId, x.BranchId, x.GodownId }).Select(g => new { g.Key, LastSale = g.Max(x => x.CreatedAt) }).ToListAsync(ct);
        return rows.Select(x => {
            var last = sales.SingleOrDefault(s => s.Key.ProductId == x.Key.ProductId && s.Key.BranchId == x.Key.BranchId && s.Key.GodownId == x.Key.GodownId)?.LastSale;
            var days = Math.Max(0, (q.ToUtc - (last ?? x.Received)).Days);
            return new SlowStockDto(x.Key.ProductId, x.Key.Product, x.Key.BranchId, x.Key.Branch, x.Key.GodownId, x.Key.Godown, x.Qty, x.Value, last, days,
                days >= q.DeadStockDays ? "Dead stock" : "Slow moving");
        }).Where(x => x.DaysSinceLastSale >= q.SlowMovingDays).OrderByDescending(x => x.DaysSinceLastSale).ThenBy(x => x.ProductId).ToList();
    }
}
