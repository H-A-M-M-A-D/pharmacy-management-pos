using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class ReportingRepository
{
    private IQueryable<StockMovement> InventoryMovements(ReportQuery q) => db.StockMovements.AsNoTracking().Where(x =>
        x.CreatedAt >= q.FromUtc && x.CreatedAt < q.ToUtc && (!q.BranchId.HasValue || x.BranchId == q.BranchId) &&
        (!q.GodownId.HasValue || x.GodownId == q.GodownId) && (!q.ProductId.HasValue || x.ProductId == q.ProductId) &&
        (!q.BatchId.HasValue || x.ProductBatchId == q.BatchId) &&
        (!q.CategoryId.HasValue || x.Product!.CategoryId == q.CategoryId) && (!q.ManufacturerId.HasValue || x.Product!.ManufacturerId == q.ManufacturerId) &&
        (!q.UserId.HasValue || x.PerformedByUserId == q.UserId) &&
        (!q.GodownUserId.HasValue || db.UserGodowns.Any(g => g.UserId == q.GodownUserId && g.GodownId == x.GodownId)));

    public async Task<object> InventoryDetailAsync(ReportQuery q, string report, CancellationToken ct)
    {
        if (report == "batch-position")
        {
            var day = BusinessDate(q.ToUtc.AddTicks(-1));
            var positions = BatchPositions(q);
            if (q.Status == "expired") positions = positions.Where(x => x.Expiry < day);
            if (q.Status == "near") positions = positions.Where(x => x.Expiry >= day && x.Expiry <= day.AddDays(30));
            return await ReportPageAsync(positions.Where(x => x.Quantity > 0).OrderBy(x => x.Expiry).ThenBy(x => x.BatchId)
                .Select(x => new { x.BatchId, x.BatchNumber, x.ProductId, x.Product, x.Sku, x.BranchId, x.Branch, x.GodownId, x.Godown, x.Expiry, x.ReceivedAt,
                    x.Quantity, UnitCost = x.Cost, StockValue = x.Quantity * x.Cost, RetailValue = x.Quantity * x.Retail }), q, ct);
        }
        if (report == "in-transit")
        {
            var transfers = db.StockTransferItems.AsNoTracking().Where(x => x.StockTransfer!.DispatchedAtUtc < q.ToUtc &&
                (x.StockTransfer.Status == StockTransferStatus.Dispatched || x.StockTransfer.Status == StockTransferStatus.PartiallyReceived) &&
                x.QuantityDispatched > x.QuantityReceived &&
                (!q.BranchId.HasValue || x.StockTransfer.SourceBranchId == q.BranchId || x.StockTransfer.DestinationBranchId == q.BranchId) &&
                (!q.GodownId.HasValue || x.StockTransfer.SourceGodownId == q.GodownId || x.StockTransfer.DestinationGodownId == q.GodownId) &&
                (!q.GodownUserId.HasValue || db.UserGodowns.Any(g => g.UserId == q.GodownUserId &&
                    (g.GodownId == x.StockTransfer.SourceGodownId || g.GodownId == x.StockTransfer.DestinationGodownId))) &&
                (!q.ProductId.HasValue || x.ProductId == q.ProductId));
            return await ReportPageAsync(transfers.OrderBy(x => x.StockTransfer!.DispatchedAtUtc).ThenBy(x => x.Id).Select(x => new
            { x.StockTransfer!.TransferNumber, x.ProductId, Product = x.Product!.Name, x.BatchNumber,
                SourceGodown = x.StockTransfer.SourceGodown!.Name, DestinationGodown = x.StockTransfer.DestinationGodown!.Name,
                InTransitQuantity = x.QuantityDispatched - x.QuantityReceived, InTransitValue = (x.QuantityDispatched - x.QuantityReceived) * x.UnitCostSnapshot,
                x.StockTransfer.DispatchedAtUtc, Method = "Currently unresolved transfers dispatched before cutoff; later receipt state is not reconstructed." }), q, ct);
        }
        var movements = InventoryMovements(q);
        if (report == "stock-adjustments")
        {
            movements = movements.Where(x => x.MovementType == StockMovementType.AdjustmentIncrease || x.MovementType == StockMovementType.AdjustmentDecrease ||
                x.MovementType == StockMovementType.Damaged || x.MovementType == StockMovementType.Expired);
            var inventoryAccount = await new AccountingRepository(db).GetAccountMappingLookupAsync(ct);
            var inventoryId = inventoryAccount.GetValueOrDefault(AccountMappingKey.Inventory);
            return await ReportPageAsync(movements.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new
            { x.Id, x.CreatedAt, x.ProductId, Product = x.Product!.Name, Branch = x.Branch!.Name, Godown = x.Godown != null ? x.Godown.Name : "Unscoped",
                MovementType = x.MovementType.ToString(), x.Quantity, x.Notes,
                AdjustmentValue = db.JournalEntryLines.Where(l => l.JournalEntry!.SourceId == x.Id && l.ChartOfAccountId == inventoryId)
                    .Sum(l => (decimal?)(l.Debit - l.Credit)) }), q, ct);
        }
        if (report == "movement-summary")
            return await ReportPageAsync(movements.GroupBy(x => new { x.ProductId, Product = x.Product!.Name, x.BranchId, Branch = x.Branch!.Name, x.GodownId, x.MovementType })
                .Select(g => new { g.Key.ProductId, g.Key.Product, g.Key.BranchId, g.Key.Branch, g.Key.GodownId, MovementType = g.Key.MovementType.ToString(),
                    Quantity = g.Sum(x => x.Quantity), MovementCount = g.Count() }).OrderBy(x => x.ProductId).ThenBy(x => x.BranchId).ThenBy(x => x.GodownId).ThenBy(x => x.MovementType), q, ct);
        if (report == "turnover")
        {
            var startValue = await BatchPositions(q with { ToUtc = q.FromUtc }).SumAsync(x => (decimal?)(x.Quantity * x.Cost), ct) ?? 0;
            var endValue = await BatchPositions(q).SumAsync(x => (decimal?)(x.Quantity * x.Cost), ct) ?? 0;
            var sales = (await SalesAnalyticsAsync(q with { Page = 1, PageSize = 1 }, "total", ct)).Items.SingleOrDefault();
            var average = (startValue + endValue) / 2;
            return new { OpeningInventoryValue = startValue, ClosingInventoryValue = endValue, AverageInventoryValue = average,
                CostOfGoodsSold = sales?.CostOfGoodsSold ?? 0, InventoryTurnover = average <= 0 ? (decimal?)null : (sales?.CostOfGoodsSold ?? 0) / average,
                Method = "Period net historical allocation COGS / mean opening and closing movement-derived stock at batch cost. Not annualized; batch repricing affects valuation." };
        }
        throw new Pharmacy.Application.Common.RequestValidationException("Unknown inventory detail report.");
    }
}
