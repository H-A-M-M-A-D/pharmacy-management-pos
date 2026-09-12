using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class ReportingRepository
{
    public Task<Godown?> GetReportGodownAsync(Guid id, CancellationToken ct) => db.Godowns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> HasGodownAccessAsync(Guid userId, Guid godownId, CancellationToken ct) =>
        db.UserGodowns.AnyAsync(x => x.UserId == userId && x.GodownId == godownId, ct);

    private sealed class SalesFact
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public Guid InvoiceId { get; set; }
        public int SoldQuantity { get; set; }
        public int ReturnedQuantity { get; set; }
        public decimal Gross { get; set; }
        public decimal Discount { get; set; }
        public decimal Net { get; set; }
        public decimal Returned { get; set; }
        public decimal Cost { get; set; }
    }

    private IQueryable<SalesFact> SalesFacts(ReportQuery q, string dimension)
    {
        var allocations = db.SaleItemBatchAllocations.AsNoTracking().Where(x =>
            (!q.PriceSourceFilter.HasValue || x.SaleItem!.PriceSource == q.PriceSourceFilter) &&
            x.SaleItem!.Sale!.Status == SaleStatus.Posted && x.SaleItem.Sale.PostedAtUtc >= q.FromUtc && x.SaleItem.Sale.PostedAtUtc < q.ToUtc &&
            (!q.GodownUserId.HasValue || x.SaleItem.Sale.GodownId.HasValue && db.UserGodowns.Any(g => g.UserId == q.GodownUserId && g.GodownId == x.SaleItem.Sale.GodownId)) &&
            (!q.BranchId.HasValue || x.SaleItem.Sale.BranchId == q.BranchId) && (q.BranchIds == null || q.BranchIds.Contains(x.SaleItem.Sale.BranchId)) &&
            (!q.GodownId.HasValue || x.SaleItem.Sale.GodownId == q.GodownId) &&
            (!q.ProductId.HasValue || x.SaleItem.ProductId == q.ProductId) &&
            (!q.SupplierId.HasValue || x.ProductBatch!.SupplierId == q.SupplierId) &&
            (!q.CategoryId.HasValue || x.SaleItem.Product!.CategoryId == q.CategoryId) &&
            (!q.ManufacturerId.HasValue || x.SaleItem.Product!.ManufacturerId == q.ManufacturerId) &&
            (!q.CustomerId.HasValue || x.SaleItem.Sale.CustomerId == q.CustomerId) &&
            (!q.UserId.HasValue || x.SaleItem.Sale.CashierUserId == q.UserId) &&
            (!q.PriceLevelId.HasValue || x.SaleItem.Sale.PriceLevelId == q.PriceLevelId) &&
            (!q.SaleType.HasValue || x.SaleItem.Sale.SaleType == q.SaleType));
        var returns = db.SalesReturnAllocations.AsNoTracking().Where(x =>
            (!q.PriceSourceFilter.HasValue || x.OriginalSaleItemBatchAllocation!.SaleItem!.PriceSource == q.PriceSourceFilter) &&
            x.SalesReturnItem!.SalesReturn!.Status == SalesReturnStatus.Posted &&
            (!q.GodownUserId.HasValue || x.SalesReturnItem.SalesReturn.OriginalSale!.GodownId.HasValue && db.UserGodowns.Any(g => g.UserId == q.GodownUserId && g.GodownId == x.SalesReturnItem.SalesReturn.OriginalSale.GodownId)) &&
            x.SalesReturnItem.SalesReturn.PostedAtUtc >= q.FromUtc && x.SalesReturnItem.SalesReturn.PostedAtUtc < q.ToUtc &&
            (!q.BranchId.HasValue || x.SalesReturnItem.SalesReturn.BranchId == q.BranchId) && (q.BranchIds == null || q.BranchIds.Contains(x.SalesReturnItem.SalesReturn.BranchId)) &&
            (!q.GodownId.HasValue || x.SalesReturnItem.SalesReturn.OriginalSale!.GodownId == q.GodownId) &&
            (!q.ProductId.HasValue || x.SalesReturnItem.ProductId == q.ProductId) &&
            (!q.SupplierId.HasValue || x.ProductBatch!.SupplierId == q.SupplierId) &&
            (!q.CategoryId.HasValue || x.SalesReturnItem.Product!.CategoryId == q.CategoryId) &&
            (!q.ManufacturerId.HasValue || x.SalesReturnItem.Product!.ManufacturerId == q.ManufacturerId) &&
            (!q.CustomerId.HasValue || x.SalesReturnItem.SalesReturn.OriginalSale!.CustomerId == q.CustomerId) &&
            (!q.UserId.HasValue || x.SalesReturnItem.SalesReturn.OriginalSale!.CashierUserId == q.UserId) &&
            (!q.PriceLevelId.HasValue || x.SalesReturnItem.SalesReturn.OriginalSale!.PriceLevelId == q.PriceLevelId) &&
            (!q.SaleType.HasValue || x.SalesReturnItem.SalesReturn.OriginalSale!.SaleType == q.SaleType));
        var sold = allocations.Select(x => new SalesFact
        {
            Key = dimension == "product" ? x.SaleItem!.Product!.Id.ToString() :
                dimension == "category" ? x.SaleItem!.Product!.CategoryId.ToString() :
                dimension == "manufacturer" ? (x.SaleItem!.Product!.ManufacturerId.ToString() ?? "unknown") :
                dimension == "customer" ? (x.SaleItem!.Sale!.CustomerId.HasValue ? x.SaleItem!.Sale!.CustomerId.Value.ToString() : "walk-in") :
                dimension == "cashier" ? x.SaleItem!.Sale!.CashierUserId.ToString() :
                dimension == "branch" ? x.SaleItem!.Sale!.BranchId.ToString() :
                dimension == "godown" ? (x.SaleItem!.Sale!.GodownId.HasValue ? x.SaleItem!.Sale!.GodownId.Value.ToString() : "unscoped") :
                dimension == "type" ? x.SaleItem!.Sale!.SaleType.ToString() :
                dimension == "price-level" ? (x.SaleItem!.Sale!.PriceLevelId.HasValue ? x.SaleItem!.Sale!.PriceLevelId.Value.ToString() : "default") :
                dimension == "invoice" ? x.SaleItem!.Sale!.Id.ToString() :
                dimension == "hour" ? (x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Hour < 10 ? "0" + x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Hour.ToString() : x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Hour.ToString()) :
                dimension == "month" ? x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Year.ToString() + "-" + (x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Month < 10 ? "0" + x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Month.ToString() : x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Month.ToString()) :
                dimension == "day" ? x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Year.ToString() + "-" + (x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Month < 10 ? "0" + x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Month.ToString() : x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Month.ToString()) + "-" + (x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Day < 10 ? "0" + x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Day.ToString() : x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Day.ToString()) : "total",
            Name = dimension == "product" ? x.SaleItem!.Product!.Name :
                dimension == "category" ? x.SaleItem!.Product!.Category!.Name :
                dimension == "manufacturer" ? (x.SaleItem!.Product!.Manufacturer != null ? x.SaleItem!.Product!.Manufacturer.Name : "Unknown") :
                dimension == "customer" ? (x.SaleItem!.Sale!.Customer != null ? x.SaleItem!.Sale!.Customer.Name : "Walk-in") :
                dimension == "cashier" ? x.SaleItem!.Sale!.CashierUser!.FullName :
                dimension == "branch" ? x.SaleItem!.Sale!.Branch!.Name :
                dimension == "godown" ? (x.SaleItem!.Sale!.Godown != null ? x.SaleItem!.Sale!.Godown.Name : "Unscoped") :
                dimension == "type" ? x.SaleItem!.Sale!.SaleType.ToString() :
                dimension == "price-level" ? (x.SaleItem!.Sale!.PriceLevel != null ? x.SaleItem!.Sale!.PriceLevel.Name : "Default") :
                dimension == "invoice" ? x.SaleItem!.Sale!.InvoiceNumber! :
                dimension == "hour" ? (x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Hour < 10 ? "0" + x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Hour.ToString() : x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Hour.ToString()) :
                dimension == "month" ? x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Year.ToString() + "-" + (x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Month < 10 ? "0" + x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Month.ToString() : x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Month.ToString()) :
                dimension == "day" ? x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Year.ToString() + "-" + (x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Month < 10 ? "0" + x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Month.ToString() : x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Month.ToString()) + "-" + (x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Day < 10 ? "0" + x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Day.ToString() : x.SaleItem!.Sale!.PostedAtUtc!.Value.AddHours(5).Day.ToString()) : "Total",
            InvoiceId = x.SaleItem!.SaleId, SoldQuantity = x.Quantity, ReturnedQuantity = 0,
            Gross = x.GrossAmount, Discount = x.DiscountAmount, Net = x.NetAmount, Returned = 0,
            Cost = x.Quantity * x.UnitCostPriceSnapshot
        });
        var refunded = returns.Select(x => new SalesFact
        {
            Key = dimension == "product" ? x.SalesReturnItem!.Product!.Id.ToString() :
                dimension == "category" ? x.SalesReturnItem!.Product!.CategoryId.ToString() :
                dimension == "manufacturer" ? (x.SalesReturnItem!.Product!.ManufacturerId.ToString() ?? "unknown") :
                dimension == "customer" ? (x.SalesReturnItem!.SalesReturn!.OriginalSale!.CustomerId.HasValue ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.CustomerId.Value.ToString() : "walk-in") :
                dimension == "cashier" ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.CashierUserId.ToString() :
                dimension == "branch" ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.BranchId.ToString() :
                dimension == "godown" ? (x.SalesReturnItem!.SalesReturn!.OriginalSale!.GodownId.HasValue ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.GodownId.Value.ToString() : "unscoped") :
                dimension == "type" ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.SaleType.ToString() :
                dimension == "price-level" ? (x.SalesReturnItem!.SalesReturn!.OriginalSale!.PriceLevelId.HasValue ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.PriceLevelId.Value.ToString() : "default") :
                dimension == "invoice" ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.Id.ToString() :
                dimension == "hour" ? (x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Hour < 10 ? "0" + x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Hour.ToString() : x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Hour.ToString()) :
                dimension == "month" ? x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Year.ToString() + "-" + (x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Month < 10 ? "0" + x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Month.ToString() : x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Month.ToString()) :
                dimension == "day" ? x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Year.ToString() + "-" + (x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Month < 10 ? "0" + x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Month.ToString() : x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Month.ToString()) + "-" + (x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Day < 10 ? "0" + x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Day.ToString() : x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Day.ToString()) : "total",
            Name = dimension == "product" ? x.SalesReturnItem!.Product!.Name :
                dimension == "category" ? x.SalesReturnItem!.Product!.Category!.Name :
                dimension == "manufacturer" ? (x.SalesReturnItem!.Product!.Manufacturer != null ? x.SalesReturnItem!.Product!.Manufacturer.Name : "Unknown") :
                dimension == "customer" ? (x.SalesReturnItem!.SalesReturn!.OriginalSale!.Customer != null ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.Customer.Name : "Walk-in") :
                dimension == "cashier" ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.CashierUser!.FullName :
                dimension == "branch" ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.Branch!.Name :
                dimension == "godown" ? (x.SalesReturnItem!.SalesReturn!.OriginalSale!.Godown != null ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.Godown.Name : "Unscoped") :
                dimension == "type" ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.SaleType.ToString() :
                dimension == "price-level" ? (x.SalesReturnItem!.SalesReturn!.OriginalSale!.PriceLevel != null ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.PriceLevel.Name : "Default") :
                dimension == "invoice" ? x.SalesReturnItem!.SalesReturn!.OriginalSale!.InvoiceNumber! :
                dimension == "hour" ? (x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Hour < 10 ? "0" + x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Hour.ToString() : x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Hour.ToString()) :
                dimension == "month" ? x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Year.ToString() + "-" + (x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Month < 10 ? "0" + x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Month.ToString() : x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Month.ToString()) :
                dimension == "day" ? x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Year.ToString() + "-" + (x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Month < 10 ? "0" + x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Month.ToString() : x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Month.ToString()) + "-" + (x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Day < 10 ? "0" + x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Day.ToString() : x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value.AddHours(5).Day.ToString()) : "Total",
            InvoiceId = x.SalesReturnItem!.SalesReturn!.OriginalSaleId, SoldQuantity = 0, ReturnedQuantity = x.Quantity,
            Gross = 0, Discount = 0, Net = -x.RefundAmount, Returned = x.RefundAmount,
            Cost = -x.Quantity * x.UnitCostPriceSnapshot
        });
        return sold.Concat(refunded);
    }

    public async Task<PagedReport<AnalyticsRowDto>> SalesAnalyticsAsync(ReportQuery q, string dimension, CancellationToken ct)
    {
        var grouped = SalesFacts(q, dimension).GroupBy(x => new { x.Key, x.Name }).Select(g => new
        {
            g.Key.Key, g.Key.Name,
            Count = g.Where(x => x.SoldQuantity > 0).Select(x => x.InvoiceId).Distinct().Count(),
            Sold = g.Sum(x => x.SoldQuantity), Qty = g.Sum(x => x.SoldQuantity - x.ReturnedQuantity),
            Gross = g.Sum(x => x.Gross), Discount = g.Sum(x => x.Discount), Net = g.Sum(x => x.Net),
            Returned = g.Sum(x => x.Returned), Cost = g.Sum(x => x.Cost)
        });
        if (!string.IsNullOrWhiteSpace(q.Search)) grouped = grouped.Where(x => x.Name.Contains(q.Search));
        var total = await grouped.CountAsync(ct);
        var ordered = dimension is "day" or "month" or "hour" ? grouped.OrderBy(x => x.Key) : grouped.OrderByDescending(x => x.Net).ThenBy(x => x.Key);
        var rows = await ordered.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync(ct);
        return new(rows.Select(x => new AnalyticsRowDto(x.Key, x.Name, x.Count, x.Qty, x.Gross, x.Discount, x.Net, x.Returned,
            x.Cost, x.Net - x.Cost, x.Net == 0 ? 0 : decimal.Round((x.Net - x.Cost) / x.Net * 100, 2),
            x.Count == 0 ? 0 : decimal.Round((x.Net + x.Returned) / x.Count, 2),
            x.Count == 0 ? 0 : decimal.Round((decimal)x.Sold / x.Count, 2), x.Sold, x.Sold - x.Qty)).ToList(), total, q.Page, q.PageSize);
    }
}
