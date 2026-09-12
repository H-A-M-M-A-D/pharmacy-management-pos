using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class ReportingRepository
{
    public async Task<object> BranchManagementAsync(ReportQuery q, CancellationToken ct)
    {
        var branches = db.Branches.AsNoTracking().Where(x => !q.BranchId.HasValue || x.Id == q.BranchId);
        var total = await branches.CountAsync(ct);
        var page = await branches.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var ids = page.Select(x => x.Id).ToList();
        var scoped = q with { BranchIds = ids, Page = 1, PageSize = 200 };
        var sales = (await SalesAnalyticsAsync(scoped, "branch", ct)).Items.ToDictionary(x => x.Key);
        var purchases = (await PurchaseAnalyticsAsync(scoped, "branch", ct)).Items.ToDictionary(x => x.Key);
        var inventory = await BatchPositions(scoped).GroupBy(x => x.BranchId).Select(g => new { Id = g.Key, Value = g.Sum(x => x.Quantity * x.Cost) }).ToDictionaryAsync(x => x.Id, ct);
        var accounts = new AccountingRepository(db);
        var activity = await accounts.GetBranchAccountActivityAsync(q.FromUtc, q.ToUtc.AddTicks(-1), ids, ct);
        var balances = await accounts.GetBranchAccountActivityAsync(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), q.ToUtc.AddTicks(-1), ids, ct);
        var mappings = await accounts.GetAccountMappingLookupAsync(ct);
        var result = page.Select(branch =>
        {
            var key = branch.Id.ToString();
            sales.TryGetValue(key, out var sale); purchases.TryGetValue(key, out var purchase);
            var rows = activity.GetValueOrDefault(branch.Id) ?? [];
            var position = balances.GetValueOrDefault(branch.Id) ?? [];
            var pnl = FinancialStatements.ProfitAndLoss(q.FromUtc, q.ToUtc.AddTicks(-1), rows, mappings);
            decimal Balance(AccountMappingKey mapping) => position.Where(x => x.ChartOfAccountId == mappings.GetValueOrDefault(mapping)).Sum(x => x.Debit - x.Credit);
            return new { BranchId = branch.Id, Branch = branch.Name, Sales = sale?.NetSales ?? 0, Purchases = purchase?.NetPurchaseValue ?? 0,
                pnl.GrossProfit, Expenses = pnl.TotalOperatingExpenses + pnl.TotalOtherExpenses, pnl.NetProfit,
                InventoryValue = inventory.GetValueOrDefault(branch.Id)?.Value ?? 0, Receivables = Balance(AccountMappingKey.AccountsReceivable),
                Payables = -Balance(AccountMappingKey.AccountsPayable), Cash = Balance(AccountMappingKey.Cash), Bank = Balance(AccountMappingKey.Bank),
                Transactions = sale?.InvoiceCount ?? 0, AverageInvoice = sale?.AverageInvoice ?? 0,
                OperationalGrossProfit = sale?.GrossProfit ?? 0, GrossProfitReconciliationDifference = pnl.GrossProfit - (sale?.GrossProfit ?? 0) };
        }).ToList();
        return new PagedReport<object>(result.Cast<object>().ToList(), total, q.Page, q.PageSize);
    }

    public async Task<object> GodownManagementAsync(ReportQuery q, CancellationToken ct)
    {
        var day = BusinessDate(q.ToUtc.AddTicks(-1));
        var positions = BatchPositions(q);
        var movements = InventoryMovements(q);
        var godowns = db.Godowns.AsNoTracking().Where(g => (!q.BranchId.HasValue || g.BranchId == q.BranchId) &&
            (!q.GodownId.HasValue || g.Id == q.GodownId) && (!q.GodownUserId.HasValue || g.UserGodowns.Any(u => u.UserId == q.GodownUserId)));
        return await ReportPageAsync(godowns.OrderBy(x => x.Name).ThenBy(x => x.Id).Select(g => new
        {
            GodownId = g.Id, Godown = g.Name, g.BranchId, Branch = g.Branch!.Name,
            Quantity = positions.Where(x => x.GodownId == g.Id).Sum(x => (int?)x.Quantity) ?? 0,
            StockValue = positions.Where(x => x.GodownId == g.Id).Sum(x => (decimal?)(x.Quantity * x.Cost)) ?? 0,
            NearExpiryValue = positions.Where(x => x.GodownId == g.Id && x.Expiry >= day && x.Expiry <= day.AddDays(30)).Sum(x => (decimal?)(x.Quantity * x.Cost)) ?? 0,
            ExpiredValue = positions.Where(x => x.GodownId == g.Id && x.Expiry < day).Sum(x => (decimal?)(x.Quantity * x.Cost)) ?? 0,
            LowStockItems = positions.Where(x => x.GodownId == g.Id).GroupBy(x => new { x.ProductId, x.ReorderLevel })
                .Count(p => p.Sum(x => x.Quantity) > 0 && p.Sum(x => x.Quantity) <= p.Key.ReorderLevel),
            MovementCount = movements.Count(x => x.GodownId == g.Id),
            TransfersIn = movements.Where(x => x.GodownId == g.Id && x.MovementType == StockMovementType.TransferIn).Sum(x => (int?)x.Quantity) ?? 0,
            TransfersOut = -(movements.Where(x => x.GodownId == g.Id && x.MovementType == StockMovementType.TransferOut).Sum(x => (int?)x.Quantity) ?? 0),
            AdjustmentQuantity = movements.Where(x => x.GodownId == g.Id && (x.MovementType == StockMovementType.AdjustmentIncrease || x.MovementType == StockMovementType.AdjustmentDecrease)).Sum(x => (int?)x.Quantity) ?? 0,
            InTransitQuantity = db.StockTransferItems.Where(x => (x.StockTransfer!.SourceGodownId == g.Id || x.StockTransfer.DestinationGodownId == g.Id) &&
                (x.StockTransfer.Status == StockTransferStatus.Dispatched || x.StockTransfer.Status == StockTransferStatus.PartiallyReceived) && x.StockTransfer.DispatchedAtUtc < q.ToUtc)
                .Sum(x => (int?)(x.QuantityDispatched - x.QuantityReceived)) ?? 0,
            CountVarianceQuantity = db.StockCountItems.Where(x => x.StockCountSession!.GodownId == g.Id && x.StockCountSession.Status == StockCountStatus.Completed &&
                x.StockCountSession.CountDate >= BusinessDate(q.FromUtc) && x.StockCountSession.CountDate < BusinessDate(q.ToUtc))
                .Sum(x => (int?)(x.CountedQuantity - x.SystemQuantity)) ?? 0
        }), q, ct);
    }
}
