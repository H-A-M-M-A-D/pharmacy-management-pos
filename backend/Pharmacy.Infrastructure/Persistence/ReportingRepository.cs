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
        var query = Sales(branchId, q.FromUtc, q.ToUtc);
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
        var total = await query.CountAsync(ct); var items = await query.OrderByDescending(x => x.Sale!.PostedAtUtc).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(x => new DiscountRowDto(x.Sale!.InvoiceNumber!, x.Product!.Name, x.Sale.CashierUser!.FullName, x.GrossAmount, x.DiscountPercent, x.DiscountAmount, x.NetAmount)).ToListAsync(ct); return new(items, total, q.Page, q.PageSize);
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

    public async Task<PagedReport<StockMovementRowDto>> StockMovementsAsync(Guid? branchId, ReportQuery q, string? movementType, CancellationToken ct)
    {
        var query = db.StockMovements.AsNoTracking().Where(x => x.CreatedAt >= q.FromUtc && x.CreatedAt < q.ToUtc && (!branchId.HasValue || x.BranchId == branchId)); if (Enum.TryParse<StockMovementType>(movementType, true, out var type)) query = query.Where(x => x.MovementType == type); var total = await query.CountAsync(ct);
        var raw = await query.OrderByDescending(x => x.CreatedAt).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(x => new { x.CreatedAt, Branch = x.Branch!.Name, Product = x.Product!.Name, Batch = x.ProductBatch!.BatchNumber, x.MovementType, x.Quantity, User = x.PerformedByUser != null ? x.PerformedByUser.FullName : "System", x.ReferenceType, x.Notes }).ToListAsync(ct); var rows = raw.Select(x => new StockMovementRowDto(x.CreatedAt, x.Branch, x.Product, x.Batch, x.MovementType.ToString(), x.Quantity, x.User, x.ReferenceType, x.Notes)).ToList(); return new(rows, total, q.Page, q.PageSize);
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

    private IQueryable<Sale> Sales(Guid? branch, DateTime from, DateTime to) => db.Sales.AsNoTracking().Where(x => x.Status == SaleStatus.Posted && x.PostedAtUtc >= from && x.PostedAtUtc < to && (!branch.HasValue || x.BranchId == branch));
    private IQueryable<SalesReturn> Returns(Guid? branch, DateTime from, DateTime to) => db.SalesReturns.AsNoTracking().Where(x => x.Status == SalesReturnStatus.Posted && x.PostedAtUtc >= from && x.PostedAtUtc < to && (!branch.HasValue || x.BranchId == branch));
    private IQueryable<SaleItemBatchAllocation> Allocations(Guid? branch, DateTime from, DateTime to) => db.SaleItemBatchAllocations.AsNoTracking().Where(x => x.SaleItem!.Sale!.Status == SaleStatus.Posted && x.SaleItem.Sale.PostedAtUtc >= from && x.SaleItem.Sale.PostedAtUtc < to && (!branch.HasValue || x.SaleItem.Sale.BranchId == branch));
    private IQueryable<SalesReturnAllocation> ReturnAllocations(Guid? branch, DateTime from, DateTime to) => db.SalesReturnAllocations.AsNoTracking().Where(x => x.SalesReturnItem!.SalesReturn!.PostedAtUtc >= from && x.SalesReturnItem.SalesReturn.PostedAtUtc < to && (!branch.HasValue || x.SalesReturnItem.SalesReturn.BranchId == branch));
    private IQueryable<GoodsReceipt> Receipts(Guid? branch, DateTime from, DateTime to) { var start = BusinessDate(from); var end = BusinessDate(to); return db.GoodsReceipts.AsNoTracking().Where(x => x.Status == GoodsReceiptStatus.Posted && x.ReceiptDate >= start && x.ReceiptDate < end && (!branch.HasValue || x.BranchId == branch)); }
    private IQueryable<PurchaseReturn> PurchaseReturns(Guid? branch, DateTime from, DateTime to) => db.PurchaseReturns.AsNoTracking().Where(x => x.Status == PurchaseReturnStatus.Posted && x.PostedAtUtc >= from && x.PostedAtUtc < to && (!branch.HasValue || x.BranchId == branch));
    private static DateOnly BusinessDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(utc, "Pakistan Standard Time"));
}
