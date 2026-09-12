using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class ReportingRepository
{
    private IQueryable<Sale> FilterSales(IQueryable<Sale> sales)
    {
        if (filters is not { } q) return sales;
        return sales.Where(x => (!q.GodownId.HasValue || x.GodownId == q.GodownId) &&
            (!q.GodownUserId.HasValue || x.GodownId.HasValue && db.UserGodowns.Any(g => g.UserId == q.GodownUserId && g.GodownId == x.GodownId)) &&
            (!q.CustomerId.HasValue || x.CustomerId == q.CustomerId) && (!q.UserId.HasValue || x.CashierUserId == q.UserId) &&
            (!q.SaleType.HasValue || x.SaleType == q.SaleType) && (!q.PriceLevelId.HasValue || x.PriceLevelId == q.PriceLevelId) &&
            (!q.ProductId.HasValue || x.Items.Any(i => i.ProductId == q.ProductId)) &&
            (!q.CategoryId.HasValue || x.Items.Any(i => i.Product!.CategoryId == q.CategoryId)) &&
            (!q.ManufacturerId.HasValue || x.Items.Any(i => i.Product!.ManufacturerId == q.ManufacturerId)));
    }
    private IQueryable<SaleItemBatchAllocation> FilterAllocations(IQueryable<SaleItemBatchAllocation> allocations)
    {
        if (filters is not { } q) return allocations;
        var saleIds = FilterSales(db.Sales).Select(x => x.Id);
        return allocations.Where(x => saleIds.Contains(x.SaleItem!.SaleId) &&
            (!q.ProductId.HasValue || x.SaleItem.ProductId == q.ProductId) &&
            (!q.CategoryId.HasValue || x.SaleItem.Product!.CategoryId == q.CategoryId) &&
            (!q.ManufacturerId.HasValue || x.SaleItem.Product!.ManufacturerId == q.ManufacturerId));
    }
    private IQueryable<SalesReturn> FilterReturns(IQueryable<SalesReturn> returns)
    {
        if (filters is not { } q) return returns;
        var saleIds = FilterSales(db.Sales).Select(x => x.Id);
        return returns.Where(x => saleIds.Contains(x.OriginalSaleId) &&
            (!q.ProductId.HasValue || x.Items.Any(i => i.ProductId == q.ProductId)));
    }
    private IQueryable<SalesReturnAllocation> FilterReturnAllocations(IQueryable<SalesReturnAllocation> allocations)
    {
        if (filters is not { } q) return allocations;
        var returnIds = FilterReturns(db.SalesReturns).Select(x => x.Id);
        return allocations.Where(x => returnIds.Contains(x.SalesReturnItem!.SalesReturnId) &&
            (!q.ProductId.HasValue || x.SalesReturnItem.ProductId == q.ProductId) &&
            (!q.CategoryId.HasValue || x.SalesReturnItem.Product!.CategoryId == q.CategoryId) &&
            (!q.ManufacturerId.HasValue || x.SalesReturnItem.Product!.ManufacturerId == q.ManufacturerId));
    }
}
