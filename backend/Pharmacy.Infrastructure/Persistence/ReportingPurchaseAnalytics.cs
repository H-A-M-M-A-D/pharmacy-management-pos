using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class ReportingRepository
{
    public async Task<PagedReport<PurchaseAnalyticsDto>> PurchaseAnalyticsAsync(ReportQuery q, string dimension, CancellationToken ct)
    {
        dimension = dimension.Replace("by-", "", StringComparison.Ordinal);
        var start = BusinessDate(q.FromUtc); var end = BusinessDate(q.ToUtc);
        var receipts = db.GoodsReceiptItems.AsNoTracking().Where(x => x.GoodsReceipt!.Status == GoodsReceiptStatus.Posted &&
            x.GoodsReceipt.ReceiptDate >= start && x.GoodsReceipt.ReceiptDate < end &&
            (!q.BranchId.HasValue || x.GoodsReceipt.BranchId == q.BranchId) && (q.BranchIds == null || q.BranchIds.Contains(x.GoodsReceipt.BranchId)) && (!q.GodownId.HasValue || x.GoodsReceipt.GodownId == q.GodownId) &&
            (!q.GodownUserId.HasValue || db.UserGodowns.Any(g => g.UserId == q.GodownUserId && g.GodownId == x.GoodsReceipt.GodownId)) &&
            (!q.SupplierId.HasValue || x.GoodsReceipt.SupplierId == q.SupplierId) && (!q.ProductId.HasValue || x.ProductId == q.ProductId) &&
            (!q.CategoryId.HasValue || x.Product!.CategoryId == q.CategoryId) && (!q.ManufacturerId.HasValue || x.Product!.ManufacturerId == q.ManufacturerId));
        var returns = PurchaseReturnItems(q);
        var facts = receipts.Select(x => new
        {
            Key = dimension == "supplier" ? x.GoodsReceipt!.SupplierId.ToString() : dimension == "product" ? x.ProductId.ToString() :
                dimension == "category" ? x.Product!.CategoryId.ToString() : dimension == "manufacturer" ? x.Product!.ManufacturerId.ToString() :
                dimension == "branch" ? x.GoodsReceipt!.BranchId.ToString() : dimension == "godown" ? x.GoodsReceipt!.GodownId.ToString() :
                dimension == "trend" ? x.GoodsReceipt!.ReceiptDate.Year.ToString() + "-" + x.GoodsReceipt.ReceiptDate.Month.ToString() + "-" + x.GoodsReceipt.ReceiptDate.Day.ToString() : "total",
            Name = dimension == "supplier" ? x.GoodsReceipt!.Supplier!.Name : dimension == "product" ? x.Product!.Name :
                dimension == "category" ? x.Product!.Category!.Name : dimension == "manufacturer" ? x.Product!.Manufacturer!.Name :
                dimension == "branch" ? x.GoodsReceipt!.Branch!.Name : dimension == "godown" ? (x.GoodsReceipt!.Godown != null ? x.GoodsReceipt.Godown.Name : "Unscoped") :
                dimension == "trend" ? x.GoodsReceipt!.ReceiptDate.Year.ToString() + "-" + x.GoodsReceipt.ReceiptDate.Month.ToString() + "-" + x.GoodsReceipt.ReceiptDate.Day.ToString() : "Total",
            Id = x.GoodsReceiptId, Qty = x.PurchasedQuantity + x.BonusQuantity, Value = x.NetLineAmount, Returned = 0m, Received = true
        }).Concat(returns.Select(x => new
        {
            Key = dimension == "supplier" ? x.PurchaseReturn!.SupplierId.ToString() : dimension == "product" ? x.ProductId.ToString() :
                dimension == "category" ? x.Product!.CategoryId.ToString() : dimension == "manufacturer" ? x.Product!.ManufacturerId.ToString() :
                dimension == "branch" ? x.PurchaseReturn!.BranchId.ToString() : dimension == "godown" ? x.OriginalGoodsReceiptItem!.GoodsReceipt!.GodownId.ToString() :
                dimension == "trend" ? x.PurchaseReturn!.PostedAtUtc.AddHours(5).Year.ToString() + "-" + (x.PurchaseReturn.PostedAtUtc.AddHours(5).Month < 10 ? "0" + x.PurchaseReturn.PostedAtUtc.AddHours(5).Month.ToString() : x.PurchaseReturn.PostedAtUtc.AddHours(5).Month.ToString()) + "-" + (x.PurchaseReturn.PostedAtUtc.AddHours(5).Day < 10 ? "0" + x.PurchaseReturn.PostedAtUtc.AddHours(5).Day.ToString() : x.PurchaseReturn.PostedAtUtc.AddHours(5).Day.ToString()) : "total",
            Name = dimension == "supplier" ? x.PurchaseReturn!.Supplier!.Name : dimension == "product" ? x.Product!.Name :
                dimension == "category" ? x.Product!.Category!.Name : dimension == "manufacturer" ? x.Product!.Manufacturer!.Name :
                dimension == "branch" ? x.PurchaseReturn!.Branch!.Name : dimension == "godown" ? (x.OriginalGoodsReceiptItem!.GoodsReceipt!.Godown != null ? x.OriginalGoodsReceiptItem.GoodsReceipt.Godown.Name : "Unscoped") :
                dimension == "trend" ? x.PurchaseReturn!.PostedAtUtc.AddHours(5).Year.ToString() + "-" + (x.PurchaseReturn.PostedAtUtc.AddHours(5).Month < 10 ? "0" + x.PurchaseReturn.PostedAtUtc.AddHours(5).Month.ToString() : x.PurchaseReturn.PostedAtUtc.AddHours(5).Month.ToString()) + "-" + (x.PurchaseReturn.PostedAtUtc.AddHours(5).Day < 10 ? "0" + x.PurchaseReturn.PostedAtUtc.AddHours(5).Day.ToString() : x.PurchaseReturn.PostedAtUtc.AddHours(5).Day.ToString()) : "Total",
            Id = x.PurchaseReturnId, Qty = -(x.PaidReturnQuantity + x.BonusReturnQuantity), Value = 0m, Returned = x.NetSupplierCredit, Received = false
        }));
        var grouped = facts.GroupBy(x => new { x.Key, x.Name }).Select(g => new
        { g.Key.Key, g.Key.Name, Count = g.Where(x => x.Received).Select(x => x.Id).Distinct().Count(), Qty = g.Sum(x => x.Qty), Value = g.Sum(x => x.Value), Returned = g.Sum(x => x.Returned) });
        var total = await grouped.CountAsync(ct);
        var ordered = dimension == "trend" ? grouped.OrderBy(x => x.Key) : grouped.OrderByDescending(x => x.Value - x.Returned).ThenBy(x => x.Key);
        var rows = await ordered.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        return new(rows.Select(x => new PurchaseAnalyticsDto(x.Key ?? "unscoped", x.Name, x.Count, x.Qty, x.Value, x.Returned, x.Value - x.Returned)).ToList(), total, q.Page, q.PageSize);
    }

    public async Task<FinancialPositionDto> FinancialPositionAsync(ReportQuery q, CancellationToken ct)
    {
        // Reuse the Phase 4 mapped balance query; reporting does not recompute GL balances.
        var accounts = new AccountingRepository(db);
        var values = new Dictionary<AccountMappingKey, decimal>();
        foreach (var key in new[] { AccountMappingKey.Cash, AccountMappingKey.Bank, AccountMappingKey.AccountsReceivable,
            AccountMappingKey.AccountsPayable, AccountMappingKey.CustomerAdvances, AccountMappingKey.SupplierAdvances })
            values[key] = await accounts.GetMappedAccountBalanceAsync(key, q.ToUtc.AddTicks(-1), q.BranchId, ct);
        decimal Balance(AccountMappingKey key) => values[key];
        return new(Balance(AccountMappingKey.Cash), Balance(AccountMappingKey.Bank), Balance(AccountMappingKey.AccountsReceivable),
            -Balance(AccountMappingKey.AccountsPayable), -Balance(AccountMappingKey.CustomerAdvances), Balance(AccountMappingKey.SupplierAdvances));
    }

    public async Task<PagedReport<ContributionDto>> AbcAsync(ReportQuery q, string basis, CancellationToken ct)
    {
        if (basis is not ("sales" or "profit" or "inventory")) throw new Pharmacy.Application.Common.RequestValidationException("ABC basis must be sales, profit, or inventory.");
        var sales = SalesFacts(q, "product").GroupBy(x => new { x.Key, x.Name }).Select(g => new
            { Key = g.Key.Key, Name = g.Key.Name, Value = basis == "profit" ? g.Sum(x => x.Net - x.Cost) : g.Sum(x => x.Net) });
        var inventory = BatchPositions(q).GroupBy(x => new { x.ProductId, x.Product }).Select(g => new
            { Key = g.Key.ProductId.ToString(), Name = g.Key.Product, Value = g.Sum(x => x.Quantity * x.Cost) });
        var contributions = (basis == "inventory" ? inventory : sales).Where(x => x.Value > 0);
        var totalValue = await contributions.SumAsync(x => (decimal?)x.Value, ct) ?? 0;
        var total = await contributions.CountAsync(ct);
        var ranked = contributions.Select(x => new
        {
            x.Key, x.Name, x.Value,
            Cumulative = contributions.Where(y => y.Value > x.Value || y.Value == x.Value && string.Compare(y.Key, x.Key) <= 0).Sum(y => (decimal?)y.Value) ?? 0
        });
        var rows = await ranked.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        return new(rows.Select(x =>
        {
            var before = totalValue == 0 ? 0 : (x.Cumulative - x.Value) / totalValue * 100;
            return new ContributionDto(Guid.Parse(x.Key), x.Name, x.Value, totalValue == 0 ? 0 : decimal.Round(x.Value / totalValue * 100, 2),
                totalValue == 0 ? 0 : decimal.Round(x.Cumulative / totalValue * 100, 2), before < q.AbcA ? "A" : before < q.AbcB ? "B" : "C");
        }).ToList(), total, q.Page, q.PageSize);
    }
}
