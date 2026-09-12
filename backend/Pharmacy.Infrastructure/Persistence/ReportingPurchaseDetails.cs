using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class ReportingRepository
{
    private IQueryable<PurchaseReturnItem> PurchaseReturnItems(ReportQuery q) => db.PurchaseReturnItems.AsNoTracking().Where(x =>
        x.PurchaseReturn!.Status == PurchaseReturnStatus.Posted && x.PurchaseReturn.PostedAtUtc >= q.FromUtc && x.PurchaseReturn.PostedAtUtc < q.ToUtc &&
        (!q.BranchId.HasValue || x.PurchaseReturn.BranchId == q.BranchId) && (q.BranchIds == null || q.BranchIds.Contains(x.PurchaseReturn.BranchId)) &&
        (!q.GodownId.HasValue || x.OriginalGoodsReceiptItem!.GoodsReceipt!.GodownId == q.GodownId) &&
        (!q.GodownUserId.HasValue || db.UserGodowns.Any(g => g.UserId == q.GodownUserId && g.GodownId == x.OriginalGoodsReceiptItem!.GoodsReceipt!.GodownId)) &&
        (!q.SupplierId.HasValue || x.PurchaseReturn.SupplierId == q.SupplierId) && (!q.ProductId.HasValue || x.ProductId == q.ProductId) &&
        (!q.CategoryId.HasValue || x.Product!.CategoryId == q.CategoryId) && (!q.ManufacturerId.HasValue || x.Product!.ManufacturerId == q.ManufacturerId));
    private async Task<PagedReport<T>> ReportPageAsync<T>(IQueryable<T> ordered, ReportQuery q, CancellationToken ct)
    {
        var count = await ordered.CountAsync(ct);
        return new(await ordered.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct), count, q.Page, q.PageSize);
    }

    private IQueryable<GoodsReceiptItem> PurchaseItems(ReportQuery q)
    {
        var from = BusinessDate(q.FromUtc); var to = BusinessDate(q.ToUtc);
        return db.GoodsReceiptItems.AsNoTracking().Where(x => x.GoodsReceipt!.Status == GoodsReceiptStatus.Posted &&
            x.GoodsReceipt.ReceiptDate >= from && x.GoodsReceipt.ReceiptDate < to &&
            (!q.BranchId.HasValue || x.GoodsReceipt.BranchId == q.BranchId) && (!q.GodownId.HasValue || x.GoodsReceipt.GodownId == q.GodownId) &&
            (!q.GodownUserId.HasValue || db.UserGodowns.Any(g => g.UserId == q.GodownUserId && g.GodownId == x.GoodsReceipt.GodownId)) &&
            (!q.SupplierId.HasValue || x.GoodsReceipt.SupplierId == q.SupplierId) && (!q.ProductId.HasValue || x.ProductId == q.ProductId) &&
            (!q.CategoryId.HasValue || x.Product!.CategoryId == q.CategoryId) && (!q.ManufacturerId.HasValue || x.Product!.ManufacturerId == q.ManufacturerId));
    }

    public async Task<object> PurchasingDetailAsync(ReportQuery q, string report, CancellationToken ct)
    {
        var purchases = PurchaseItems(q);
        if (report is "price-history" or "price-comparison" or "last-rate" or "price-variance")
        {
            var history = purchases.Select(x => new
            {
                x.Id, x.ProductId, Product = x.Product!.Name, x.GoodsReceipt!.SupplierId, Supplier = x.GoodsReceipt.Supplier!.Name,
                x.GoodsReceiptId, ReceiptNumber = x.GoodsReceipt.GrnNumber, x.GoodsReceipt.ReceiptDate,
                Quantity = x.PurchasedQuantity + x.BonusQuantity, PurchasePrice = x.PurchasePrice,
                EffectiveUnitCost = x.PurchasedQuantity + x.BonusQuantity == 0 ? 0 : x.NetLineAmount / (x.PurchasedQuantity + x.BonusQuantity),
                PreviousPrice = db.GoodsReceiptItems.Where(p => p.ProductId == x.ProductId && p.GoodsReceipt!.SupplierId == x.GoodsReceipt.SupplierId &&
                    p.GoodsReceipt.Status == GoodsReceiptStatus.Posted && p.GoodsReceipt.ReceiptDate < x.GoodsReceipt.ReceiptDate &&
                    (!q.BranchId.HasValue || p.GoodsReceipt.BranchId == q.BranchId) && (!q.GodownId.HasValue || p.GoodsReceipt.GodownId == q.GodownId) &&
                    (!q.GodownUserId.HasValue || db.UserGodowns.Any(g => g.UserId == q.GodownUserId && g.GodownId == p.GoodsReceipt.GodownId)))
                    .OrderByDescending(p => p.GoodsReceipt!.ReceiptDate).ThenByDescending(p => p.Id).Select(p => (decimal?)p.PurchasePrice).FirstOrDefault(),
                IsLatest = !purchases.Any(p => p.ProductId == x.ProductId && p.GoodsReceipt!.SupplierId == x.GoodsReceipt.SupplierId &&
                    (p.GoodsReceipt.ReceiptDate > x.GoodsReceipt.ReceiptDate || p.GoodsReceipt.ReceiptDate == x.GoodsReceipt.ReceiptDate && p.Id.CompareTo(x.Id) > 0))
            });
            if (report is "last-rate" or "price-comparison") history = history.Where(x => x.IsLatest);
            return await ReportPageAsync(history.OrderBy(x => x.Product).ThenBy(x => x.Supplier).ThenByDescending(x => x.ReceiptDate).ThenBy(x => x.Id).Select(x => new
            { x.ProductId, x.Product, x.SupplierId, x.Supplier, x.GoodsReceiptId, x.ReceiptNumber, x.ReceiptDate, x.Quantity, x.PurchasePrice,
                x.EffectiveUnitCost, x.PreviousPrice, PriceVariance = x.PreviousPrice.HasValue ? (decimal?)(x.PurchasePrice - x.PreviousPrice.Value) : null,
                PriceVariancePercent = x.PreviousPrice.HasValue && x.PreviousPrice != 0 ? (decimal?)((x.PurchasePrice - x.PreviousPrice.Value) / x.PreviousPrice.Value * 100) : null }), q, ct);
        }
        if (report is "pending-po" or "po-vs-grn")
        {
            if (q.GodownId.HasValue) throw new Pharmacy.Application.Common.RequestValidationException("Purchase orders have no godown assignment; select a branch instead.");
            var start = BusinessDate(q.FromUtc); var end = BusinessDate(q.ToUtc);
            var orders = db.PurchaseOrders.AsNoTracking().Where(x => x.OrderDate >= start && x.OrderDate < end &&
                (!q.BranchId.HasValue || x.BranchId == q.BranchId) && (!q.SupplierId.HasValue || x.SupplierId == q.SupplierId));
            if (report == "pending-po") orders = orders.Where(x => x.Items.Any(i => i.ReceivedQuantity < i.OrderedQuantity) && x.Status != PurchaseOrderStatus.Cancelled);
            return await ReportPageAsync(orders.OrderBy(x => x.ExpectedDate ?? x.OrderDate).ThenBy(x => x.Id).Select(x => new
            { x.Id, x.OrderNumber, x.OrderDate, x.ExpectedDate, Supplier = x.Supplier!.Name, Branch = x.Branch!.Name, Status = x.Status.ToString(),
                OrderedQuantity = x.Items.Where(i => !q.ProductId.HasValue || i.ProductId == q.ProductId).Sum(i => i.OrderedQuantity),
                ReceivedQuantity = x.Items.Where(i => !q.ProductId.HasValue || i.ProductId == q.ProductId).Sum(i => i.ReceivedQuantity),
                ReceiptCount = x.GoodsReceipts.Count(r => r.Status == GoodsReceiptStatus.Posted),
                DeliveryCompletionPercent = x.Items.Sum(i => i.OrderedQuantity) == 0 ? 0 : (decimal)x.Items.Sum(i => i.ReceivedQuantity) / x.Items.Sum(i => i.OrderedQuantity) * 100 }), q, ct);
        }
        if (report == "supplier-performance")
        {
            var grouped = purchases.GroupBy(x => new { x.GoodsReceipt!.SupplierId, x.GoodsReceipt.Supplier!.Name }).Select(g => new
            {
                SupplierId = g.Key.SupplierId, Supplier = g.Key.Name, PurchaseValue = g.Sum(x => x.NetLineAmount),
                ReceiptCount = g.Select(x => x.GoodsReceiptId).Distinct().Count(),
                ReturnValue = db.PurchaseReturns.Where(r => r.SupplierId == g.Key.SupplierId && r.Status == PurchaseReturnStatus.Posted &&
                    r.PostedAtUtc >= q.FromUtc && r.PostedAtUtc < q.ToUtc && (!q.BranchId.HasValue || r.BranchId == q.BranchId)).Sum(r => (decimal?)r.NetSupplierCredit) ?? 0,
                Outstanding = db.SupplierLedgerEntries.Where(l => l.SupplierId == g.Key.SupplierId && (!q.BranchId.HasValue || l.BranchId == q.BranchId) &&
                    l.EntryDate < BusinessDate(q.ToUtc)).Sum(l => (decimal?)l.Amount) ?? 0,
                Ordered = db.PurchaseOrderItems.Where(i => i.PurchaseOrder!.SupplierId == g.Key.SupplierId &&
                    i.PurchaseOrder.OrderDate >= BusinessDate(q.FromUtc) && i.PurchaseOrder.OrderDate < BusinessDate(q.ToUtc) && i.PurchaseOrder.Status != PurchaseOrderStatus.Cancelled &&
                    (!q.BranchId.HasValue || i.PurchaseOrder.BranchId == q.BranchId)).Sum(i => (int?)i.OrderedQuantity) ?? 0,
                Received = db.PurchaseOrderItems.Where(i => i.PurchaseOrder!.SupplierId == g.Key.SupplierId &&
                    i.PurchaseOrder.OrderDate >= BusinessDate(q.FromUtc) && i.PurchaseOrder.OrderDate < BusinessDate(q.ToUtc) && i.PurchaseOrder.Status != PurchaseOrderStatus.Cancelled &&
                    (!q.BranchId.HasValue || i.PurchaseOrder.BranchId == q.BranchId)).Sum(i => (int?)i.ReceivedQuantity) ?? 0
            });
            return await ReportPageAsync(grouped.OrderByDescending(x => x.PurchaseValue).ThenBy(x => x.SupplierId).Select(x => new
            { x.SupplierId, x.Supplier, x.PurchaseValue, x.ReceiptCount, x.ReturnValue, x.Outstanding,
                BalanceScope = "Supplier balance within the branch; product/godown filters apply to receipt analytics. Delivery completion is current state of branch POs placed in the period.",
                DeliveryCompletionPercent = x.Ordered == 0 ? (decimal?)null : (decimal)x.Received / x.Ordered * 100,
                AveragePaymentDays = (decimal?)null, PaymentDaysMethod = "Deferred: supplier settlement history can be changed by reversals; use allocation-aware payment history." }), q, ct);
        }
        throw new Pharmacy.Application.Common.RequestValidationException("Unknown purchase detail report.");
    }
}
