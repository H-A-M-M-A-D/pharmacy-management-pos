using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Application.Services.Reports;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class ReportingRepository(PharmacyDbContext db) : IReportingRepository
{
    public Task<User?> GetActorAsync(Guid id, CancellationToken ct) => db.Users.AsNoTracking()
        .Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
        .SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<SalesSummaryDto> SalesSummaryAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var sales = Sales(branchId, from, to);
        var totals = await sales.GroupBy(_ => 1).Select(g => new { Gross = g.Sum(x => x.Subtotal), Discount = g.Sum(x => x.DiscountTotal), Net = g.Sum(x => x.NetTotal), Paid = g.Sum(x => x.AmountPaid), Credit = g.Sum(x => x.CreditAmount), Count = g.Count(), Items = g.Sum(x => x.Items.Sum(i => i.RequestedQuantity)) }).SingleOrDefaultAsync(ct);
        var returns = Returns(branchId, from, to);
        var returned = await returns.SumAsync(x => (decimal?)x.RefundAmount, ct) ?? 0m;
        return new(totals?.Gross ?? 0, totals?.Discount ?? 0, totals?.Net ?? 0, totals?.Paid ?? 0,
            totals?.Credit ?? 0, returned, (totals?.Net ?? 0) - returned, totals?.Count ?? 0, totals?.Items ?? 0);
    }

    public async Task<PagedReport<DailySaleDto>> DailySalesAsync(Guid? branchId, ReportQuery q, CancellationToken ct)
    {
        var query = Sales(branchId, q.FromUtc, q.ToUtc, q.SaleType);
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(x => x.InvoiceNumber!.Contains(q.Search) || (x.CustomerName ?? "").Contains(q.Search));
        var total = await query.CountAsync(ct);
        var raw = await query.OrderByDescending(x => x.PostedAtUtc).ThenBy(x => x.InvoiceNumber).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new { x.Id, Invoice = x.InvoiceNumber!, At = x.PostedAtUtc!.Value, Customer = x.Customer != null ? x.Customer.Name : x.CustomerName ?? "Walk-in", Cashier = x.CashierUser!.FullName, Gross = x.Subtotal, Discount = x.DiscountTotal, Net = x.NetTotal, Paid = x.AmountPaid, Credit = x.CreditAmount, Returned = x.Id == Guid.Empty ? 0 : db.SalesReturns.Where(r => r.OriginalSaleId == x.Id && r.Status == SalesReturnStatus.Posted).Sum(r => (decimal?)r.RefundAmount) ?? 0 }).ToListAsync(ct);
        var ids = raw.Select(x => x.Id).ToList();
        var payments = await db.SalePayments.AsNoTracking().Where(x => ids.Contains(x.SaleId)).GroupBy(x => new { x.SaleId, x.Method }).Select(g => new { g.Key.SaleId, g.Key.Method, Amount = g.Sum(x => x.AmountApplied) }).ToListAsync(ct);
        var rows = raw.Select(x => new DailySaleDto(x.Id, x.Invoice, x.At, x.Customer, x.Cashier, x.Gross, x.Discount, x.Net, x.Paid, x.Credit, x.Returned, x.Net - x.Returned,
            string.Join("; ", payments.Where(p => p.SaleId == x.Id).OrderBy(p => p.Method).Select(p => $"{p.Method}: {p.Amount:0.00}")))).ToList();
        return new(rows, total, q.Page, q.PageSize);
    }

    public async Task<IReadOnlyList<ProductSalesDto>> ProductSalesAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var sold = Allocations(branchId, from, to).GroupBy(x => new { x.SaleItem!.ProductId, x.SaleItem.Product!.Name, x.SaleItem.Product.SKU, Category = x.SaleItem.Product.Category!.Name })
            .Select(g => new { g.Key.ProductId, Product = g.Key.Name, Sku = g.Key.SKU, g.Key.Category, Qty = g.Sum(x => x.Quantity), Gross = g.Sum(x => x.GrossAmount), Discount = g.Sum(x => x.DiscountAmount), Net = g.Sum(x => x.NetAmount), Cost = g.Sum(x => x.UnitCostPriceSnapshot * x.Quantity) });
        var soldRows = await sold.ToListAsync(ct);
        var returned = await ReturnAllocations(branchId, from, to).GroupBy(x => x.SalesReturnItem!.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity), Gross = g.Sum(x => x.GrossReturnAmount), Discount = g.Sum(x => x.DiscountReturnAmount), Net = g.Sum(x => x.RefundAmount), Cost = g.Sum(x => x.UnitCostPriceSnapshot * x.Quantity) }).ToListAsync(ct);
        return soldRows.Select(x => { var r = returned.FirstOrDefault(y => y.ProductId == x.ProductId); var net = x.Net - (r?.Net ?? 0); var cost = x.Cost - (r?.Cost ?? 0); var profit = net - cost; return new ProductSalesDto(x.ProductId, x.Product, x.Sku, x.Category, x.Qty, r?.Qty ?? 0, x.Qty - (r?.Qty ?? 0), x.Gross - (r?.Gross ?? 0), x.Discount - (r?.Discount ?? 0), net, cost, profit, net == 0 ? 0 : decimal.Round(profit / net * 100, 2)); }).OrderByDescending(x => x.NetSales).ToList();
    }

    public async Task<IReadOnlyList<NamedSalesDto>> SalesByCategoryAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var sold = await Allocations(branchId, from, to).GroupBy(x => x.SaleItem!.Product!.Category!.Name).Select(g => new { Name = g.Key, Gross = g.Sum(x => x.GrossAmount), Discount = g.Sum(x => x.DiscountAmount), Net = g.Sum(x => x.NetAmount) }).ToListAsync(ct);
        var returned = await ReturnAllocations(branchId, from, to).GroupBy(x => x.SalesReturnItem!.Product!.Category!.Name).Select(g => new { Name = g.Key, Value = g.Sum(x => x.RefundAmount) }).ToListAsync(ct);
        return sold.Select(x => { var returns = returned.FirstOrDefault(r => r.Name == x.Name)?.Value ?? 0; return new NamedSalesDto(x.Name, 0, x.Gross, x.Discount, x.Net, returns, x.Net - returns); }).OrderByDescending(x => x.NetAfterReturns).ToList();
    }

    public async Task<IReadOnlyList<NamedSalesDto>> SalesByCashierAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await Sales(branchId, from, to).GroupBy(x => new { x.CashierUserId, x.CashierUser!.FullName }).Select(g => new { g.Key.CashierUserId, Name = g.Key.FullName, Count = g.Count(), Gross = g.Sum(x => x.Subtotal), Discount = g.Sum(x => x.DiscountTotal), Net = g.Sum(x => x.NetTotal) }).ToListAsync(ct);
        var returned = await Returns(branchId, from, to).GroupBy(x => x.OriginalSale!.CashierUserId).Select(g => new { Id = g.Key, Value = g.Sum(x => x.RefundAmount) }).ToListAsync(ct);
        return rows.Select(x => { var value = returned.FirstOrDefault(r => r.Id == x.CashierUserId)?.Value ?? 0; return new NamedSalesDto(x.Name, x.Count, x.Gross, x.Discount, x.Net, value, x.Net - value); }).ToList();
    }

    public async Task<IReadOnlyList<PaymentMethodSalesDto>> SalesByPaymentAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await db.SalePayments.AsNoTracking().Where(x => x.Sale!.Status == SaleStatus.Posted && x.Sale.PostedAtUtc >= from && x.Sale.PostedAtUtc < to && (!branchId.HasValue || x.Sale.BranchId == branchId)).GroupBy(x => x.Method).Select(g => new { Method = g.Key, Amount = g.Sum(x => x.AmountApplied) }).OrderByDescending(x => x.Amount).ToListAsync(ct);
        return rows.Select(x => new PaymentMethodSalesDto(x.Method.ToString(), x.Amount)).ToList();
    }

    public async Task<PagedReport<DiscountRowDto>> DiscountsAsync(Guid? branchId, ReportQuery q, CancellationToken ct)
    {
        var query = db.SaleItems.AsNoTracking().Where(x => x.Sale!.Status == SaleStatus.Posted && x.Sale.PostedAtUtc >= q.FromUtc && x.Sale.PostedAtUtc < q.ToUtc && x.DiscountAmount > 0 && (!branchId.HasValue || x.Sale.BranchId == branchId));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Sale!.PostedAtUtc).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new DiscountRowDto(x.Sale!.InvoiceNumber!, x.Product!.Name, x.Sale.CashierUser!.FullName, x.GrossAmount, x.DiscountPercent, x.DiscountAmount, x.NetAmount, x.IsDiscountOverride, x.DiscountOverrideReason)).ToListAsync(ct);
        return new(items, total, q.Page, q.PageSize);
    }

    public async Task<IReadOnlyList<NamedSalesDto>> SalesByCustomerAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await Sales(branchId, from, to).Where(x => x.CustomerId != null)
            .GroupBy(x => new { x.CustomerId, Name = x.Customer!.Name })
            .Select(g => new { g.Key.CustomerId, g.Key.Name, Count = g.Count(), Gross = g.Sum(x => x.Subtotal), Discount = g.Sum(x => x.DiscountTotal), Net = g.Sum(x => x.NetTotal) })
            .ToListAsync(ct);
        var returned = await Returns(branchId, from, to).Where(x => x.OriginalSale!.CustomerId != null)
            .GroupBy(x => x.OriginalSale!.CustomerId).Select(g => new { Id = g.Key, Value = g.Sum(x => x.RefundAmount) }).ToListAsync(ct);
        return rows.Select(x => { var value = returned.FirstOrDefault(r => r.Id == x.CustomerId)?.Value ?? 0; return new NamedSalesDto(x.Name, x.Count, x.Gross, x.Discount, x.Net, value, x.Net - value); })
            .OrderByDescending(x => x.NetSales).ToList();
    }

    public async Task<IReadOnlyList<NamedSalesDto>> SalesByTypeAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await Sales(branchId, from, to).GroupBy(x => x.SaleType)
            .Select(g => new { Type = g.Key, Count = g.Count(), Gross = g.Sum(x => x.Subtotal), Discount = g.Sum(x => x.DiscountTotal), Net = g.Sum(x => x.NetTotal) })
            .ToListAsync(ct);
        return rows.Select(x => new NamedSalesDto(x.Type.ToString(), x.Count, x.Gross, x.Discount, x.Net, 0, x.Net)).OrderByDescending(x => x.NetSales).ToList();
    }

    public async Task<IReadOnlyList<NamedSalesDto>> SalesByPriceLevelAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await Sales(branchId, from, to)
            .GroupBy(x => new { x.PriceLevelId, Name = x.PriceLevel != null ? x.PriceLevel.Name : "Default" })
            .Select(g => new { g.Key.Name, Count = g.Count(), Gross = g.Sum(x => x.Subtotal), Discount = g.Sum(x => x.DiscountTotal), Net = g.Sum(x => x.NetTotal) })
            .ToListAsync(ct);
        return rows.Select(x => new NamedSalesDto(x.Name, x.Count, x.Gross, x.Discount, x.Net, 0, x.Net)).OrderByDescending(x => x.NetSales).ToList();
    }

    public async Task<PagedReport<PriceOverrideRowDto>> PriceOverridesAsync(Guid? branchId, ReportQuery q, CancellationToken ct)
    {
        var query = db.SaleItems.AsNoTracking().Where(x => x.Sale!.Status == SaleStatus.Posted && x.Sale.PostedAtUtc >= q.FromUtc && x.Sale.PostedAtUtc < q.ToUtc
            && x.IsManualPriceOverride && (!branchId.HasValue || x.Sale.BranchId == branchId));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Sale!.PostedAtUtc).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new PriceOverrideRowDto(x.Sale!.InvoiceNumber!, x.Sale.PostedAtUtc!.Value, x.Product!.Name, x.Sale.CashierUser!.FullName,
                x.ResolvedUnitPrice ?? 0, x.Product.RetailPrice, x.PriceOverrideReason)).ToListAsync(ct);
        return new(items, total, q.Page, q.PageSize);
    }

    public async Task<PagedReport<BelowCostSaleRowDto>> BelowCostSalesAsync(Guid? branchId, ReportQuery q, CancellationToken ct)
    {
        var query = db.SaleItemBatchAllocations.AsNoTracking().Where(x => x.SaleItem!.Sale!.Status == SaleStatus.Posted && x.SaleItem.Sale.PostedAtUtc >= q.FromUtc && x.SaleItem.Sale.PostedAtUtc < q.ToUtc
            && x.SaleItem.IsBelowCost && (!branchId.HasValue || x.SaleItem.Sale.BranchId == branchId));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.SaleItem!.Sale!.PostedAtUtc).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new { x.SaleItem!.Sale!.InvoiceNumber, PostedAtUtc = x.SaleItem.Sale.PostedAtUtc!.Value, Product = x.SaleItem.Product!.Name,
                Cashier = x.SaleItem.Sale.CashierUser!.FullName, x.Quantity, SellingPrice = x.UnitSalePriceSnapshot, UnitCost = x.UnitCostPriceSnapshot })
            .ToListAsync(ct);
        return new(items.Select(x => new BelowCostSaleRowDto(x.InvoiceNumber!, x.PostedAtUtc, x.Product, x.Cashier, x.Quantity, x.SellingPrice, x.UnitCost,
            x.UnitCost - x.SellingPrice, (x.UnitCost - x.SellingPrice) * x.Quantity)).ToList(), total, q.Page, q.PageSize);
    }

    public async Task<IReadOnlyList<CustomerProfitDto>> GrossProfitByCustomerAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await db.SaleItemBatchAllocations.AsNoTracking()
            .Where(x => x.SaleItem!.Sale!.Status == SaleStatus.Posted && x.SaleItem.Sale.PostedAtUtc >= from && x.SaleItem.Sale.PostedAtUtc < to
                && x.SaleItem.Sale.CustomerId != null && (!branchId.HasValue || x.SaleItem.Sale.BranchId == branchId))
            .GroupBy(x => new { x.SaleItem!.Sale!.CustomerId, Name = x.SaleItem.Sale.Customer!.Name })
            .Select(g => new { g.Key.CustomerId, g.Key.Name, Net = g.Sum(x => x.NetAmount), Cost = g.Sum(x => x.UnitCostPriceSnapshot * x.Quantity) })
            .ToListAsync(ct);
        return rows.Select(x => { var profit = x.Net - x.Cost; return new CustomerProfitDto(x.CustomerId!.Value, x.Name, x.Net, x.Cost, profit, x.Net == 0 ? 0 : decimal.Round(profit / x.Net * 100, 2)); })
            .OrderByDescending(x => x.GrossProfit).ToList();
    }

    public async Task<IReadOnlyList<CreditUtilizationRowDto>> CreditLimitUtilizationAsync(Guid? branchId, CancellationToken ct)
    {
        var balances = await db.CustomerLedgerEntries.AsNoTracking().Where(x => !branchId.HasValue || x.BranchId == branchId)
            .GroupBy(x => x.CustomerId).Select(g => new { Id = g.Key, Balance = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var customers = await db.Customers.AsNoTracking().Where(x => x.CreditLimit > 0)
            .Select(x => new { x.Id, x.CustomerCode, x.Name, x.CreditLimit }).ToListAsync(ct);
        return customers.Select(c =>
        {
            var outstanding = balances.FirstOrDefault(b => b.Id == c.Id)?.Balance ?? 0;
            outstanding = outstanding > 0 ? outstanding : 0;
            return new CreditUtilizationRowDto(c.Id, c.CustomerCode, c.Name, c.CreditLimit, outstanding, c.CreditLimit - outstanding,
                c.CreditLimit == 0 ? 0 : decimal.Round(outstanding / c.CreditLimit * 100, 2));
        }).OrderByDescending(x => x.UtilizationPercent).ToList();
    }

    public async Task<QuotationSummaryDto> QuotationSummaryAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var quotationDateFrom = BusinessDate(from);
        var quotationDateTo = BusinessDate(to);
        var rows = await db.SalesQuotations.AsNoTracking()
            .Where(x => x.QuotationDate >= quotationDateFrom && x.QuotationDate < quotationDateTo && (!branchId.HasValue || x.BranchId == branchId))
            .Select(x => new { x.Status, x.NetTotal }).ToListAsync(ct);
        int Count(SalesQuotationStatus s) => rows.Count(x => x.Status == s);
        var converted = Count(SalesQuotationStatus.Converted);
        var total = rows.Count;
        var responded = total - Count(SalesQuotationStatus.Draft) - Count(SalesQuotationStatus.Sent);
        return new(total, Count(SalesQuotationStatus.Draft), Count(SalesQuotationStatus.Sent), Count(SalesQuotationStatus.Accepted),
            Count(SalesQuotationStatus.Rejected), Count(SalesQuotationStatus.Expired), converted, Count(SalesQuotationStatus.Cancelled),
            rows.Sum(x => x.NetTotal), rows.Where(x => x.Status == SalesQuotationStatus.Converted).Sum(x => x.NetTotal),
            responded == 0 ? 0 : decimal.Round((decimal)converted / responded * 100, 2));
    }

    public async Task<SalesOrderSummaryDto> SalesOrderSummaryAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var orderDateFrom = BusinessDate(from);
        var orderDateTo = BusinessDate(to);
        var rows = await db.SalesOrders.AsNoTracking()
            .Where(x => x.OrderDate >= orderDateFrom && x.OrderDate < orderDateTo && (!branchId.HasValue || x.BranchId == branchId))
            .Select(x => new { x.Status, x.NetTotal, Ordered = x.Items.Sum(i => i.OrderedQuantity), Fulfilled = x.Items.Sum(i => i.FulfilledQuantity) })
            .ToListAsync(ct);
        int Count(SalesOrderStatus s) => rows.Count(x => x.Status == s);
        var totalOrdered = rows.Sum(x => x.Ordered);
        var totalFulfilled = rows.Sum(x => x.Fulfilled);
        return new(rows.Count, Count(SalesOrderStatus.Draft), Count(SalesOrderStatus.Confirmed), Count(SalesOrderStatus.PartiallyFulfilled),
            Count(SalesOrderStatus.Fulfilled), Count(SalesOrderStatus.Cancelled), rows.Sum(x => x.NetTotal), totalOrdered, totalFulfilled,
            totalOrdered == 0 ? 0 : decimal.Round((decimal)totalFulfilled / totalOrdered * 100, 2));
    }

    public async Task<PagedReport<OpenSalesOrderRowDto>> OpenSalesOrdersAsync(Guid? branchId, ReportQuery q, CancellationToken ct)
    {
        var query = db.SalesOrders.AsNoTracking().Where(x => (x.Status == SalesOrderStatus.Confirmed || x.Status == SalesOrderStatus.PartiallyFulfilled)
            && (!branchId.HasValue || x.BranchId == branchId));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.ExpectedDeliveryDate ?? x.OrderDate).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new { x.OrderNumber, Customer = x.Customer!.Name, x.OrderDate, x.ExpectedDeliveryDate, x.Status, x.NetTotal,
                Ordered = x.Items.Sum(i => i.OrderedQuantity), Fulfilled = x.Items.Sum(i => i.FulfilledQuantity) })
            .ToListAsync(ct);
        return new(items.Select(x => new OpenSalesOrderRowDto(x.OrderNumber, x.Customer, x.OrderDate, x.ExpectedDeliveryDate, x.Status.ToString(),
            x.NetTotal, x.Ordered, x.Fulfilled, x.Ordered - x.Fulfilled)).ToList(), total, q.Page, q.PageSize);
    }

    public async Task<PagedReport<CreditSaleDto>> CreditSalesAsync(Guid? branchId, ReportQuery q, CancellationToken ct)
    {
        var query = Sales(branchId, q.FromUtc, q.ToUtc).Where(x => x.CreditAmount > 0);
        var total = await query.CountAsync(ct); var items = await query.OrderByDescending(x => x.PostedAtUtc).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(x => new CreditSaleDto(x.InvoiceNumber!, x.Customer!.Name, x.PostedAtUtc!.Value, x.NetTotal, x.AmountPaid, x.CreditAmount, x.CustomerId.HasValue ? db.CustomerLedgerEntries.Where(e => e.CustomerId == x.CustomerId && (!branchId.HasValue || e.BranchId == branchId)).Sum(e => (decimal?)e.Amount) : null)).ToListAsync(ct); return new(items, total, q.Page, q.PageSize);
    }

    public async Task<PurchaseSummaryDto> PurchaseSummaryAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var receipts = Receipts(branchId, from, to); var x = await receipts.GroupBy(_ => 1).Select(g => new { Gross = g.Sum(r => r.Subtotal), Discount = g.Sum(r => r.DiscountTotal), Tax = g.Sum(r => r.TaxTotal), Net = g.Sum(r => r.NetTotal), Count = g.Count(), Paid = g.Sum(r => r.Items.Sum(i => i.PurchasedQuantity)), Bonus = g.Sum(r => r.Items.Sum(i => i.BonusQuantity)) }).SingleOrDefaultAsync(ct);
        var returns = await PurchaseReturns(branchId, from, to).SumAsync(r => (decimal?)r.NetSupplierCredit, ct) ?? 0; return new(x?.Gross ?? 0, x?.Discount ?? 0, x?.Tax ?? 0, x?.Net ?? 0, returns, (x?.Net ?? 0) - returns, x?.Count ?? 0, x?.Paid ?? 0, x?.Bonus ?? 0);
    }

    public async Task<IReadOnlyList<SupplierPurchaseDto>> PurchasesBySupplierAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var purchases = await Receipts(branchId, from, to).GroupBy(x => new { x.SupplierId, x.Supplier!.Name }).Select(g => new { g.Key.SupplierId, Supplier = g.Key.Name, Count = g.Count(), Paid = g.Sum(x => x.Items.Sum(i => i.PurchasedQuantity)), Bonus = g.Sum(x => x.Items.Sum(i => i.BonusQuantity)), Net = g.Sum(x => x.NetTotal) }).ToListAsync(ct);
        var returns = await PurchaseReturns(branchId, from, to).GroupBy(x => x.SupplierId).Select(g => new { Id = g.Key, Value = g.Sum(x => x.NetSupplierCredit) }).ToListAsync(ct);
        var balances = await db.SupplierLedgerEntries.AsNoTracking().Where(x => !branchId.HasValue || x.BranchId == branchId).GroupBy(x => x.SupplierId).Select(g => new { Id = g.Key, Value = g.Sum(x => x.Amount) }).ToListAsync(ct);
        return purchases.Select(x => { var ret = returns.FirstOrDefault(r => r.Id == x.SupplierId)?.Value ?? 0; return new SupplierPurchaseDto(x.SupplierId, x.Supplier, x.Count, x.Paid, x.Bonus, x.Net, ret, x.Net - ret, balances.FirstOrDefault(b => b.Id == x.SupplierId)?.Value ?? 0); }).ToList();
    }

    public async Task<IReadOnlyList<ProductPurchaseDto>> PurchasesByProductAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct)
    {
        var start = BusinessDate(from); var end = BusinessDate(to);
        var received = await db.GoodsReceiptItems.AsNoTracking().Where(x => x.GoodsReceipt!.Status == GoodsReceiptStatus.Posted && x.GoodsReceipt.ReceiptDate >= start && x.GoodsReceipt.ReceiptDate < end && (!branchId.HasValue || x.GoodsReceipt.BranchId == branchId)).GroupBy(x => new { x.ProductId, x.Product!.Name, x.Product.SKU }).Select(g => new { g.Key.ProductId, Product = g.Key.Name, Sku = g.Key.SKU, Paid = g.Sum(x => x.PurchasedQuantity), Bonus = g.Sum(x => x.BonusQuantity), Cost = g.Sum(x => x.NetLineAmount) }).ToListAsync(ct);
        var returned = await db.PurchaseReturnItems.AsNoTracking().Where(x => x.PurchaseReturn!.PostedAtUtc >= from && x.PurchaseReturn.PostedAtUtc < to && (!branchId.HasValue || x.PurchaseReturn.BranchId == branchId)).GroupBy(x => x.ProductId).Select(g => new { Id = g.Key, Paid = g.Sum(x => x.PaidReturnQuantity), Bonus = g.Sum(x => x.BonusReturnQuantity), Credit = g.Sum(x => x.NetSupplierCredit) }).ToListAsync(ct);
        return received.Select(x => { var r = returned.FirstOrDefault(y => y.Id == x.ProductId); return new ProductPurchaseDto(x.ProductId, x.Product, x.Sku, x.Paid, x.Bonus, r?.Paid ?? 0, r?.Bonus ?? 0, x.Paid + x.Bonus - (r?.Paid ?? 0) - (r?.Bonus ?? 0), x.Cost - (r?.Credit ?? 0)); }).ToList();
    }

    public async Task<PagedReport<PurchaseReturnRowDto>> PurchaseReturnsAsync(Guid? branchId, ReportQuery q, CancellationToken ct)
    {
        var query = PurchaseReturns(branchId, q.FromUtc, q.ToUtc); var total = await query.CountAsync(ct); var rows = await query.OrderByDescending(x => x.PostedAtUtc).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(x => new PurchaseReturnRowDto(x.ReturnNumber, x.Supplier!.Name, x.PostedAtUtc, x.Items.Sum(i => i.PaidReturnQuantity), x.Items.Sum(i => i.BonusReturnQuantity), x.NetSupplierCredit)).ToListAsync(ct); return new(rows, total, q.Page, q.PageSize);
    }

    public async Task<IReadOnlyList<StockRowDto>> CurrentStockAsync(Guid? branchId, string? status, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking().Where(x => x.IsActive).Select(x => new { Product = x, Qty = x.ProductBatches.Where(b => !branchId.HasValue || b.BranchId == branchId).Sum(b => (int?)b.QuantityAvailable) ?? 0, Batches = x.ProductBatches.Count(b => b.QuantityAvailable > 0 && (!branchId.HasValue || b.BranchId == branchId)), Expiry = x.ProductBatches.Where(b => b.QuantityAvailable > 0 && (!branchId.HasValue || b.BranchId == branchId)).Min(b => (DateOnly?)b.ExpiryDate), Value = x.ProductBatches.Where(b => !branchId.HasValue || b.BranchId == branchId).Sum(b => (decimal?)(b.QuantityAvailable * b.PurchasePrice)) ?? 0 });
        if (status == "low") query = query.Where(x => x.Qty > 0 && x.Qty <= x.Product.ReorderLevel); else if (status == "out") query = query.Where(x => x.Qty <= 0);
        return await query.OrderBy(x => x.Product.Name).Select(x => new StockRowDto(x.Product.Id, x.Product.Name, x.Product.SKU, x.Product.Category!.Name, x.Product.Manufacturer != null ? x.Product.Manufacturer.Name : "", branchId.HasValue ? db.Branches.Where(b => b.Id == branchId).Select(b => b.Name).First() : "All branches", x.Qty, x.Product.ReorderLevel, x.Qty <= 0 ? "Out of stock" : x.Qty <= x.Product.ReorderLevel ? "Low stock" : "In stock", x.Batches, x.Expiry, x.Value)).ToListAsync(ct);
    }

    public async Task<PagedReport<BatchStockRowDto>> BatchStockAsync(Guid? branchId, ReportQuery q, int? expiryDays, bool expired, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Pakistan Standard Time")); var query = db.ProductBatches.AsNoTracking().Where(x => x.QuantityAvailable > 0 && (!branchId.HasValue || x.BranchId == branchId));
        if (expired) query = query.Where(x => x.ExpiryDate < today); else if (expiryDays.HasValue) query = query.Where(x => x.ExpiryDate >= today && x.ExpiryDate <= today.AddDays(expiryDays.Value));
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(x => x.Product!.Name.Contains(q.Search) || x.BatchNumber.Contains(q.Search)); var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.ExpiryDate).ThenBy(x => x.BatchNumber).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(x => new BatchStockRowDto(x.Product!.Name, x.Product.SKU, x.BatchNumber, x.ExpiryDate, x.QuantityAvailable, x.PurchasePrice, x.QuantityAvailable * x.PurchasePrice, x.Supplier != null ? x.Supplier.Name : "", x.Branch!.Name)).ToListAsync(ct); return new(rows, total, q.Page, q.PageSize);
    }

    public async Task<PagedReport<StockMovementRowDto>> StockMovementsAsync(Guid? branchId, ReportQuery q, string? movementType, Guid? godownId, CancellationToken ct)
    {
        var query = db.StockMovements.AsNoTracking().Where(x => x.CreatedAt >= q.FromUtc && x.CreatedAt < q.ToUtc && (!branchId.HasValue || x.BranchId == branchId) && (!godownId.HasValue || x.GodownId == godownId)); if (Enum.TryParse<StockMovementType>(movementType, true, out var type)) query = query.Where(x => x.MovementType == type); var total = await query.CountAsync(ct);
        var raw = await query.OrderByDescending(x => x.CreatedAt).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(x => new { x.CreatedAt, Branch = x.Branch!.Name, Product = x.Product!.Name, Batch = x.ProductBatch!.BatchNumber, x.MovementType, x.Quantity, User = x.PerformedByUser != null ? x.PerformedByUser.FullName : "System", x.ReferenceType, x.Notes, Godown = x.Godown == null ? null : x.Godown.Name }).ToListAsync(ct); var rows = raw.Select(x => new StockMovementRowDto(x.CreatedAt, x.Branch, x.Product, x.Batch, x.MovementType.ToString(), x.Quantity, x.User, x.ReferenceType, x.Notes, x.Godown)).ToList(); return new(rows, total, q.Page, q.PageSize);
    }

    public async Task<IReadOnlyList<GodownStockRowDto>> GodownStockAsync(Guid? branchId, Guid? godownId, CancellationToken ct)
    {
        var query = db.ProductBatches.AsNoTracking().Where(x => x.GodownId != null && x.QuantityAvailable > 0 &&
            (!branchId.HasValue || x.BranchId == branchId) && (!godownId.HasValue || x.GodownId == godownId));
        var rows = await query
            .GroupBy(x => new { x.GodownId, GodownName = x.Godown!.Name, x.BranchId, BranchName = x.Branch!.Name, x.ProductId, ProductName = x.Product!.Name, x.Product.SKU, Category = x.Product.Category!.Name, x.Product.ReorderLevel })
            .Select(g => new { g.Key.GodownId, g.Key.GodownName, g.Key.BranchId, g.Key.BranchName, g.Key.ProductId, g.Key.ProductName, g.Key.SKU, g.Key.Category, g.Key.ReorderLevel,
                Qty = g.Sum(x => x.QuantityAvailable), Value = g.Sum(x => x.QuantityAvailable * x.PurchasePrice) })
            .ToListAsync(ct);
        return rows.Select(x => new GodownStockRowDto(x.GodownId!.Value, x.GodownName, x.BranchName, x.ProductId, x.ProductName, x.SKU, x.Category,
                x.Qty, x.ReorderLevel, x.Qty <= 0 ? "Out of stock" : x.Qty <= x.ReorderLevel ? "Low stock" : "In stock", x.Value))
            .OrderBy(x => x.Godown).ThenBy(x => x.Product).ToList();
    }

    public async Task<IReadOnlyList<InTransitStockRowDto>> InTransitStockAsync(Guid? branchId, CancellationToken ct)
    {
        var items = db.StockTransferItems.AsNoTracking().Where(x => x.QuantityDispatched > x.QuantityReceived &&
            (x.StockTransfer!.Status == StockTransferStatus.Dispatched || x.StockTransfer.Status == StockTransferStatus.PartiallyReceived) &&
            (!branchId.HasValue || x.StockTransfer.SourceBranchId == branchId || x.StockTransfer.DestinationBranchId == branchId));
        var rows = await items.Select(x => new
        {
            x.StockTransfer!.TransferNumber, Product = x.Product!.Name, x.Product.SKU, x.BatchNumber,
            SourceBranch = x.StockTransfer.SourceBranch!.Name, SourceGodown = x.StockTransfer.SourceGodown!.Name,
            DestinationBranch = x.StockTransfer.DestinationBranch!.Name, DestinationGodown = x.StockTransfer.DestinationGodown!.Name,
            x.QuantityDispatched, x.QuantityReceived, x.StockTransfer.DispatchedAtUtc
        }).ToListAsync(ct);
        return rows.Select(x => new InTransitStockRowDto(x.TransferNumber, x.Product, x.SKU, x.BatchNumber,
                x.SourceBranch, x.SourceGodown, x.DestinationBranch, x.DestinationGodown,
                x.QuantityDispatched, x.QuantityReceived, x.QuantityDispatched - x.QuantityReceived, x.DispatchedAtUtc))
            .OrderBy(x => x.DispatchedAtUtc).ToList();
    }

    public async Task<StockTransferSummaryDto> TransferSummaryAsync(Guid? branchId, ReportQuery q, CancellationToken ct)
    {
        var counts = await TransferHeaders(branchId, q).GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(),
            Draft = g.Count(x => x.Status == StockTransferStatus.Draft),
            Requested = g.Count(x => x.Status == StockTransferStatus.Requested),
            Approved = g.Count(x => x.Status == StockTransferStatus.Approved),
            Dispatched = g.Count(x => x.Status == StockTransferStatus.Dispatched),
            PartiallyReceived = g.Count(x => x.Status == StockTransferStatus.PartiallyReceived),
            Received = g.Count(x => x.Status == StockTransferStatus.Received),
            Cancelled = g.Count(x => x.Status == StockTransferStatus.Cancelled),
        }).SingleOrDefaultAsync(ct);
        var qty = await TransferItems(branchId, q).GroupBy(_ => 1).Select(g => new
        {
            Requested = g.Sum(x => x.QuantityRequested),
            Approved = g.Sum(x => x.QuantityApproved),
            Dispatched = g.Sum(x => x.QuantityDispatched),
            Received = g.Sum(x => x.QuantityReceived),
            DispatchedValue = g.Sum(x => x.QuantityDispatched * x.UnitCostSnapshot),
            ReceivedValue = g.Sum(x => x.QuantityReceived * x.UnitCostSnapshot),
        }).SingleOrDefaultAsync(ct);
        return new(counts?.Total ?? 0, counts?.Draft ?? 0, counts?.Requested ?? 0, counts?.Approved ?? 0,
            counts?.Dispatched ?? 0, counts?.PartiallyReceived ?? 0, counts?.Received ?? 0, counts?.Cancelled ?? 0,
            qty?.Requested ?? 0, qty?.Approved ?? 0, qty?.Dispatched ?? 0, qty?.Received ?? 0,
            (qty?.Dispatched ?? 0) - (qty?.Received ?? 0), qty?.DispatchedValue ?? 0, qty?.ReceivedValue ?? 0);
    }

    public async Task<PagedReport<DailyTransferDto>> DailyTransfersAsync(Guid? branchId, ReportQuery q, CancellationToken ct)
    {
        var query = TransferHeaders(branchId, q);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.TransferDate).ThenByDescending(x => x.TransferNumber)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new DailyTransferDto(x.Id, x.TransferNumber, x.TransferDate, x.Status.ToString(),
                x.SourceBranch!.Name, x.SourceGodown!.Name, x.DestinationBranch!.Name, x.DestinationGodown!.Name,
                x.Items.Sum(i => i.QuantityRequested), x.Items.Sum(i => i.QuantityDispatched), x.Items.Sum(i => i.QuantityReceived),
                x.RequestedByUser != null ? x.RequestedByUser.FullName : null, x.CreatedByUser!.FullName))
            .ToListAsync(ct);
        return new(rows, total, q.Page, q.PageSize);
    }

    public async Task<PagedReport<TransferDetailRowDto>> TransferDetailAsync(Guid? branchId, ReportQuery q, CancellationToken ct)
    {
        var query = TransferItems(branchId, q);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.StockTransfer!.TransferDate).ThenBy(x => x.StockTransfer!.TransferNumber)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new TransferDetailRowDto(x.StockTransfer!.TransferNumber, x.StockTransfer.TransferDate, x.StockTransfer.Status.ToString(),
                x.Product!.Name, x.Product.SKU, x.BatchNumber, x.UnitCostSnapshot,
                x.StockTransfer.SourceBranch!.Name, x.StockTransfer.SourceGodown!.Name,
                x.StockTransfer.DestinationBranch!.Name, x.StockTransfer.DestinationGodown!.Name,
                x.QuantityRequested, x.QuantityApproved, x.QuantityDispatched, x.QuantityReceived, x.QuantityDispatched - x.QuantityReceived))
            .ToListAsync(ct);
        return new(rows, total, q.Page, q.PageSize);
    }

    public async Task<PagedReport<TransferDiscrepancyRowDto>> TransferDiscrepancyAsync(Guid? branchId, ReportQuery q, string? resolutionFilter, CancellationToken ct)
    {
        var query = TransferItems(branchId, q).Where(x => x.QuantityDispatched > x.QuantityReceived);
        if (resolutionFilter == "outstanding") query = query.Where(x => x.StockTransfer!.Status == StockTransferStatus.Dispatched || x.StockTransfer.Status == StockTransferStatus.PartiallyReceived);
        else if (resolutionFilter == "resolved") query = query.Where(x => x.StockTransfer!.Status == StockTransferStatus.Received);
        var total = await query.CountAsync(ct);
        var raw = await query.OrderByDescending(x => x.StockTransfer!.DispatchedAtUtc)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new
            {
                x.StockTransfer!.TransferNumber, x.StockTransfer.TransferDate, x.StockTransfer.Status,
                Product = x.Product!.Name, x.Product.SKU, x.BatchNumber,
                SourceBranch = x.StockTransfer.SourceBranch!.Name, SourceGodown = x.StockTransfer.SourceGodown!.Name,
                DestinationBranch = x.StockTransfer.DestinationBranch!.Name, DestinationGodown = x.StockTransfer.DestinationGodown!.Name,
                x.QuantityDispatched, x.QuantityReceived, x.UnitCostSnapshot,
                x.StockTransfer.DispatchedAtUtc, x.StockTransfer.ReceivedAtUtc, x.StockTransfer.Notes
            }).ToListAsync(ct);
        var rows = raw.Select(x =>
        {
            var unresolved = x.QuantityDispatched - x.QuantityReceived;
            var resolved = x.Status == StockTransferStatus.Received;
            return new TransferDiscrepancyRowDto(x.TransferNumber, x.TransferDate, x.Product, x.SKU, x.BatchNumber,
                x.SourceBranch, x.SourceGodown, x.DestinationBranch, x.DestinationGodown,
                x.QuantityDispatched, x.QuantityReceived, unresolved, unresolved * x.UnitCostSnapshot,
                resolved ? "Resolved" : "Outstanding", resolved ? x.Notes : null, x.DispatchedAtUtc, resolved ? x.ReceivedAtUtc : null);
        }).ToList();
        return new(rows, total, q.Page, q.PageSize);
    }

    public async Task<PagedReport<StockCountVarianceRowDto>> StockCountVarianceAsync(Guid? branchId, ReportQuery q, CancellationToken ct)
    {
        var start = BusinessDate(q.FromUtc); var end = BusinessDate(q.ToUtc);
        var query = db.StockCountItems.AsNoTracking().Where(x =>
            x.StockCountSession!.Status == StockCountStatus.Completed &&
            x.StockCountSession.CountDate >= start && x.StockCountSession.CountDate < end &&
            x.CountedQuantity != null &&
            (!branchId.HasValue || x.StockCountSession.BranchId == branchId) &&
            (!q.GodownId.HasValue || x.StockCountSession.GodownId == q.GodownId) &&
            (!q.ProductId.HasValue || x.ProductId == q.ProductId) &&
            (string.IsNullOrWhiteSpace(q.Search) || x.StockCountSession.CountNumber.Contains(q.Search) || x.Product!.Name.Contains(q.Search)));
        var total = await query.CountAsync(ct);
        var raw = await query.OrderByDescending(x => x.StockCountSession!.CountDate).ThenBy(x => x.StockCountSession!.CountNumber)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new
            {
                x.StockCountSession!.CountNumber, x.StockCountSession.CountDate, Branch = x.StockCountSession.Branch!.Name,
                Godown = x.StockCountSession.Godown != null ? x.StockCountSession.Godown.Name : null,
                Product = x.Product!.Name, x.Product.SKU, Batch = x.ProductBatch!.BatchNumber,
                x.SystemQuantity, x.CountedQuantity, x.UnitCostSnapshot, x.Reason
            }).ToListAsync(ct);
        var rows = raw.Select(x =>
        {
            var counted = x.CountedQuantity!.Value;
            var variance = counted - x.SystemQuantity;
            return new StockCountVarianceRowDto(x.CountNumber, x.CountDate, x.Branch, x.Godown, x.Product, x.SKU, x.Batch,
                x.SystemQuantity, counted, variance, x.UnitCostSnapshot, variance * x.UnitCostSnapshot, x.Reason);
        }).ToList();
        return new(rows, total, q.Page, q.PageSize);
    }

    private IQueryable<StockTransfer> TransferHeaders(Guid? branchId, ReportQuery q)
    {
        var start = BusinessDate(q.FromUtc); var end = BusinessDate(q.ToUtc);
        var status = Enum.TryParse<StockTransferStatus>(q.Status, true, out var st) ? st : (StockTransferStatus?)null;
        return db.StockTransfers.AsNoTracking().Where(x =>
            x.TransferDate >= start && x.TransferDate < end &&
            (!branchId.HasValue || x.SourceBranchId == branchId || x.DestinationBranchId == branchId) &&
            (!q.SourceGodownId.HasValue || x.SourceGodownId == q.SourceGodownId) &&
            (!q.DestinationGodownId.HasValue || x.DestinationGodownId == q.DestinationGodownId) &&
            (!status.HasValue || x.Status == status) &&
            (string.IsNullOrWhiteSpace(q.Search) || x.TransferNumber.Contains(q.Search)) &&
            (!q.ProductId.HasValue || x.Items.Any(i => i.ProductId == q.ProductId)));
    }

    private IQueryable<StockTransferItem> TransferItems(Guid? branchId, ReportQuery q)
    {
        var start = BusinessDate(q.FromUtc); var end = BusinessDate(q.ToUtc);
        var status = Enum.TryParse<StockTransferStatus>(q.Status, true, out var st) ? st : (StockTransferStatus?)null;
        return db.StockTransferItems.AsNoTracking().Where(x =>
            x.StockTransfer!.TransferDate >= start && x.StockTransfer.TransferDate < end &&
            (!branchId.HasValue || x.StockTransfer.SourceBranchId == branchId || x.StockTransfer.DestinationBranchId == branchId) &&
            (!q.SourceGodownId.HasValue || x.StockTransfer.SourceGodownId == q.SourceGodownId) &&
            (!q.DestinationGodownId.HasValue || x.StockTransfer.DestinationGodownId == q.DestinationGodownId) &&
            (!status.HasValue || x.StockTransfer.Status == status) &&
            (string.IsNullOrWhiteSpace(q.Search) || x.StockTransfer.TransferNumber.Contains(q.Search)) &&
            (!q.ProductId.HasValue || x.ProductId == q.ProductId));
    }

    public async Task<InventorySummaryDto> InventorySummaryAsync(Guid? branchId, DateTime nowUtc, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(nowUtc, "Pakistan Standard Time")); var batches = db.ProductBatches.AsNoTracking().Where(x => !branchId.HasValue || x.BranchId == branchId); var value = await batches.SumAsync(x => (decimal?)(x.QuantityAvailable * x.PurchasePrice), ct) ?? 0; var stock = await CurrentStockAsync(branchId, null, ct); return new(value, stock.Count(x => x.CurrentQuantity > 0 && x.CurrentQuantity <= x.ReorderLevel), stock.Count(x => x.CurrentQuantity <= 0), await batches.CountAsync(x => x.QuantityAvailable > 0 && x.ExpiryDate >= today && x.ExpiryDate <= today.AddDays(30), ct));
    }

    public async Task<PagedReport<ExpenseReportDto>> ExpensesAsync(Guid? branchId, ReportQuery q, CancellationToken ct) { var query = db.Expenses.AsNoTracking().Where(x => x.ExpenseDateUtc >= q.FromUtc && x.ExpenseDateUtc < q.ToUtc && (!branchId.HasValue || x.BranchId == branchId)); var total = await query.CountAsync(ct); var rows = await query.OrderByDescending(x => x.ExpenseDateUtc).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(x => new ExpenseReportDto(x.ExpenseNumber, x.ExpenseDateUtc, x.ExpenseCategory!.Name, x.FinancialAccount!.Name, x.Branch!.Name, x.CreatedByUser!.FullName, x.Amount, x.Description)).ToListAsync(ct); return new(rows, total, q.Page, q.PageSize); }
    public async Task<PagedReport<OtherIncomeReportDto>> OtherIncomeAsync(Guid? branchId, ReportQuery q, CancellationToken ct) { var query = db.OtherIncomes.AsNoTracking().Where(x => x.OccurredAtUtc >= q.FromUtc && x.OccurredAtUtc < q.ToUtc && (!branchId.HasValue || x.BranchId == branchId)); var total = await query.CountAsync(ct); var rows = await query.OrderByDescending(x => x.OccurredAtUtc).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(x => new OtherIncomeReportDto(x.IncomeNumber, x.OccurredAtUtc, x.FinancialAccount!.Name, x.Branch!.Name, x.CreatedByUser!.FullName, x.Amount, x.Description)).ToListAsync(ct); return new(rows, total, q.Page, q.PageSize); }

    public async Task<IReadOnlyList<OutstandingDto>> CustomerOutstandingAsync(Guid? branchId, CancellationToken ct)
    {
        var balances = await db.CustomerLedgerEntries.AsNoTracking().Where(x => !branchId.HasValue || x.BranchId == branchId).GroupBy(x => x.CustomerId).Select(g => new { Id = g.Key, Balance = g.Sum(x => x.Amount), LastPayment = g.Where(x => x.EntryType == CustomerLedgerEntryType.Payment).Max(x => (DateOnly?)x.EntryDate), LastSale = g.Where(x => x.EntryType == CustomerLedgerEntryType.CreditSale).Max(x => (DateOnly?)x.EntryDate) }).Where(x => x.Balance != 0).ToListAsync(ct);
        var ids = balances.Select(x => x.Id).ToList(); var parties = await db.Customers.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.CustomerCode, x.Name, x.PhoneNumber, x.CreditLimit }).ToListAsync(ct);
        return balances.Join(parties, b => b.Id, p => p.Id, (b, p) => new OutstandingDto(p.Id, p.CustomerCode, p.Name, p.PhoneNumber, p.CreditLimit, b.Balance, p.CreditLimit - b.Balance, b.LastPayment, b.LastSale, b.Balance > 0 ? "Receivable" : "Advance")).OrderByDescending(x => x.Balance).ToList();
    }
    public async Task<IReadOnlyList<OutstandingDto>> SupplierOutstandingAsync(Guid? branchId, CancellationToken ct)
    {
        var balances = await db.SupplierLedgerEntries.AsNoTracking().Where(x => !branchId.HasValue || x.BranchId == branchId).GroupBy(x => x.SupplierId).Select(g => new { Id = g.Key, Balance = g.Sum(x => x.Amount), LastPayment = g.Where(x => x.EntryType == SupplierLedgerEntryType.Payment).Max(x => (DateOnly?)x.EntryDate), LastPurchase = g.Where(x => x.EntryType == SupplierLedgerEntryType.Purchase).Max(x => (DateOnly?)x.EntryDate) }).Where(x => x.Balance != 0).ToListAsync(ct);
        var ids = balances.Select(x => x.Id).ToList(); var parties = await db.Suppliers.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.PhoneNumber }).ToListAsync(ct);
        return balances.Join(parties, b => b.Id, p => p.Id, (b, p) => new OutstandingDto(p.Id, "", p.Name, p.PhoneNumber, null, b.Balance, null, b.LastPayment, b.LastPurchase, b.Balance > 0 ? "Payable" : "Advance/Credit")).OrderByDescending(x => x.Balance).ToList();
    }

    public async Task<PagedReport<LedgerReportDto>> AccountLedgerAsync(Guid? branchId, Guid? accountId, ReportQuery q, CancellationToken ct) { var query = db.FinancialLedgerEntries.AsNoTracking().Where(x => x.OccurredAtUtc >= q.FromUtc && x.OccurredAtUtc < q.ToUtc && (!branchId.HasValue || x.BranchId == branchId) && (!accountId.HasValue || x.FinancialAccountId == accountId)); var total = await query.CountAsync(ct); var raw = await query.OrderByDescending(x => x.OccurredAtUtc).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(x => new { x.OccurredAtUtc, x.EntryType, Reference = x.ReferenceNumber ?? x.ReferenceType, x.Description, x.Amount, Account = x.FinancialAccount!.Name }).ToListAsync(ct); var rows = raw.Select(x => new LedgerReportDto(x.OccurredAtUtc, x.EntryType.ToString(), x.Reference, x.Description, x.Amount, x.Account)).ToList(); return new(rows, total, q.Page, q.PageSize); }
    public async Task<CashPositionReportDto> CashPositionAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct) { var q = db.FinancialLedgerEntries.AsNoTracking().Where(x => !branchId.HasValue || x.BranchId == branchId); var opening = await q.Where(x => x.OccurredAtUtc < from).SumAsync(x => (decimal?)x.Amount, ct) ?? 0; var rows = await q.Where(x => x.OccurredAtUtc >= from && x.OccurredAtUtc < to).GroupBy(x => x.EntryType).Select(g => new { Type = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync(ct); decimal V(FinancialLedgerEntryType t) => rows.FirstOrDefault(x => x.Type == t)?.Amount ?? 0; var externalIn = V(FinancialLedgerEntryType.SalePayment) + V(FinancialLedgerEntryType.CustomerPayment) + V(FinancialLedgerEntryType.OtherIncome) + V(FinancialLedgerEntryType.AdjustmentCredit); var externalOut = -(V(FinancialLedgerEntryType.SupplierPayment) + V(FinancialLedgerEntryType.Expense) + V(FinancialLedgerEntryType.SalesRefund) + V(FinancialLedgerEntryType.AdjustmentDebit)); var closing = opening + rows.Sum(x => x.Amount); return new(opening, externalIn, externalOut, closing, V(FinancialLedgerEntryType.SalePayment), V(FinancialLedgerEntryType.CustomerPayment), -V(FinancialLedgerEntryType.SupplierPayment), -V(FinancialLedgerEntryType.Expense), -V(FinancialLedgerEntryType.SalesRefund), V(FinancialLedgerEntryType.OtherIncome), V(FinancialLedgerEntryType.TransferIn), -V(FinancialLedgerEntryType.TransferOut)); }
    public async Task<IReadOnlyList<TrendPointDto>> TrendAsync(Guid? branchId, DateTime from, DateTime to, CancellationToken ct) { var sales = await Allocations(branchId, from, to).GroupBy(x => DateOnly.FromDateTime(x.SaleItem!.Sale!.PostedAtUtc!.Value)).Select(g => new { Date = g.Key, Net = g.Sum(x => x.NetAmount), Cost = g.Sum(x => x.UnitCostPriceSnapshot * x.Quantity) }).ToListAsync(ct); var returns = await ReturnAllocations(branchId, from, to).GroupBy(x => DateOnly.FromDateTime(x.SalesReturnItem!.SalesReturn!.PostedAtUtc!.Value)).Select(g => new { Date = g.Key, Net = g.Sum(x => x.RefundAmount), Cost = g.Sum(x => x.UnitCostPriceSnapshot * x.Quantity) }).ToListAsync(ct); return sales.Select(x => { var r = returns.FirstOrDefault(y => y.Date == x.Date); var net = x.Net - (r?.Net ?? 0); var cost = x.Cost - (r?.Cost ?? 0); return new TrendPointDto(x.Date, net, net - cost); }).OrderBy(x => x.Date).ToList(); }

    private IQueryable<Sale> Sales(Guid? branch, DateTime from, DateTime to, SaleType? saleType = null) => db.Sales.AsNoTracking().Where(x => x.Status == SaleStatus.Posted && x.PostedAtUtc >= from && x.PostedAtUtc < to && (!branch.HasValue || x.BranchId == branch) && (!saleType.HasValue || x.SaleType == saleType));
    private IQueryable<SalesReturn> Returns(Guid? branch, DateTime from, DateTime to) => db.SalesReturns.AsNoTracking().Where(x => x.Status == SalesReturnStatus.Posted && x.PostedAtUtc >= from && x.PostedAtUtc < to && (!branch.HasValue || x.BranchId == branch));
    private IQueryable<SaleItemBatchAllocation> Allocations(Guid? branch, DateTime from, DateTime to) => db.SaleItemBatchAllocations.AsNoTracking().Where(x => x.SaleItem!.Sale!.Status == SaleStatus.Posted && x.SaleItem.Sale.PostedAtUtc >= from && x.SaleItem.Sale.PostedAtUtc < to && (!branch.HasValue || x.SaleItem.Sale.BranchId == branch));
    private IQueryable<SalesReturnAllocation> ReturnAllocations(Guid? branch, DateTime from, DateTime to) => db.SalesReturnAllocations.AsNoTracking().Where(x => x.SalesReturnItem!.SalesReturn!.PostedAtUtc >= from && x.SalesReturnItem.SalesReturn.PostedAtUtc < to && (!branch.HasValue || x.SalesReturnItem.SalesReturn.BranchId == branch));
    private IQueryable<GoodsReceipt> Receipts(Guid? branch, DateTime from, DateTime to) { var start = BusinessDate(from); var end = BusinessDate(to); return db.GoodsReceipts.AsNoTracking().Where(x => x.Status == GoodsReceiptStatus.Posted && x.ReceiptDate >= start && x.ReceiptDate < end && (!branch.HasValue || x.BranchId == branch)); }
    private IQueryable<PurchaseReturn> PurchaseReturns(Guid? branch, DateTime from, DateTime to) => db.PurchaseReturns.AsNoTracking().Where(x => x.Status == PurchaseReturnStatus.Posted && x.PostedAtUtc >= from && x.PostedAtUtc < to && (!branch.HasValue || x.BranchId == branch));
    private static DateOnly BusinessDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(utc, "Pakistan Standard Time"));
}
