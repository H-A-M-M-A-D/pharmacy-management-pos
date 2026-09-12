using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class ReportingRepository
{
    public Task<object> AgingReportAsync(ReportQuery q, bool supplier, CancellationToken ct) => new AccountingRepository(db).GetPagedAgingReportAsync(q, supplier, ct);
    public async Task<object> PartyDetailAsync(ReportQuery q, bool supplier, string report, CancellationToken ct)
    {
        var from = BusinessDate(q.FromUtc); var to = BusinessDate(q.ToUtc);
        if (supplier)
        {
            var ledger = db.SupplierLedgerEntries.AsNoTracking().Where(x => (!q.BranchId.HasValue || x.BranchId == q.BranchId) &&
                (!q.SupplierId.HasValue || x.SupplierId == q.SupplierId) && x.EntryDate >= from && x.EntryDate < to);
            if (report == "supplier-payments") ledger = ledger.Where(x => x.EntryType == SupplierLedgerEntryType.Payment);
            if (report is "supplier-payments" or "supplier-statement")
                return await ReportPageAsync(ledger.OrderBy(x => x.EntryDate).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new
                { x.Id, x.SupplierId, Supplier = x.Supplier!.Name, x.EntryDate, EntryType = x.EntryType.ToString(), x.Amount, x.PaymentMethod, x.ReferenceType, x.ReferenceId, x.ReferenceNumber, x.Notes,
                    OpeningBalance = db.SupplierLedgerEntries.Where(l => l.SupplierId == x.SupplierId && (!q.BranchId.HasValue || l.BranchId == q.BranchId) && l.EntryDate < from).Sum(l => (decimal?)l.Amount) ?? 0,
                    ClosingBalance = db.SupplierLedgerEntries.Where(l => l.SupplierId == x.SupplierId && (!q.BranchId.HasValue || l.BranchId == q.BranchId) && l.EntryDate < to).Sum(l => (decimal?)l.Amount) ?? 0 }), q, ct);
            var balances = db.SupplierLedgerEntries.AsNoTracking().Where(x => (!q.BranchId.HasValue || x.BranchId == q.BranchId) && x.EntryDate < to)
                .GroupBy(x => new { x.SupplierId, x.Supplier!.Name }).Select(g => new
                { g.Key.SupplierId, Supplier = g.Key.Name, Outstanding = g.Sum(x => x.Amount), LastPurchase = g.Where(x => x.EntryType == SupplierLedgerEntryType.Purchase).Max(x => (DateOnly?)x.EntryDate),
                    LastPayment = g.Where(x => x.EntryType == SupplierLedgerEntryType.Payment).Max(x => (DateOnly?)x.EntryDate) });
            if (report == "supplier-outstanding") balances = balances.Where(x => x.Outstanding != 0);
            if (!string.IsNullOrWhiteSpace(q.Search)) balances = balances.Where(x => x.Supplier.Contains(q.Search));
            return await ReportPageAsync(balances.Where(x => !q.SupplierId.HasValue || x.SupplierId == q.SupplierId).OrderByDescending(x => x.Outstanding).ThenBy(x => x.SupplierId), q, ct);
        }
        else
        {
            var ledger = db.CustomerLedgerEntries.AsNoTracking().Where(x => (!q.BranchId.HasValue || x.BranchId == q.BranchId) &&
                (!q.CustomerId.HasValue || x.CustomerId == q.CustomerId) && x.EntryDate >= from && x.EntryDate < to);
            if (report == "customer-payments") ledger = ledger.Where(x => x.EntryType == CustomerLedgerEntryType.Payment);
            if (report is "customer-payments" or "customer-statement")
                return await ReportPageAsync(ledger.OrderBy(x => x.EntryDate).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new
                { x.Id, x.CustomerId, Customer = x.Customer!.Name, x.EntryDate, EntryType = x.EntryType.ToString(), x.Amount, x.PaymentMethod, x.ReferenceType, x.ReferenceId, x.ReferenceNumber, x.Notes,
                    OpeningBalance = db.CustomerLedgerEntries.Where(l => l.CustomerId == x.CustomerId && (!q.BranchId.HasValue || l.BranchId == q.BranchId) && l.EntryDate < from).Sum(l => (decimal?)l.Amount) ?? 0,
                    ClosingBalance = db.CustomerLedgerEntries.Where(l => l.CustomerId == x.CustomerId && (!q.BranchId.HasValue || l.BranchId == q.BranchId) && l.EntryDate < to).Sum(l => (decimal?)l.Amount) ?? 0 }), q, ct);
            var facts = SalesFacts(q, "customer");
            var balances = db.Customers.AsNoTracking().Where(c =>
                c.LedgerEntries.Any(l => (!q.BranchId.HasValue || l.BranchId == q.BranchId) && l.EntryDate < to) ||
                c.Sales.Any(s => (!q.BranchId.HasValue || s.BranchId == q.BranchId) && s.Status == SaleStatus.Posted && s.PostedAtUtc < q.ToUtc))
                .Select(c => new
                { CustomerId = c.Id, Customer = c.Name, c.CreditLimit,
                    Outstanding = c.LedgerEntries.Where(l => (!q.BranchId.HasValue || l.BranchId == q.BranchId) && l.EntryDate < to).Sum(l => (decimal?)l.Amount) ?? 0,
                    LastSale = db.Sales.Where(s => s.CustomerId == c.Id && s.Status == SaleStatus.Posted && s.PostedAtUtc < q.ToUtc &&
                        (!q.BranchId.HasValue || s.BranchId == q.BranchId)).Max(s => s.PostedAtUtc),
                    LastPayment = c.LedgerEntries.Where(l => l.EntryType == CustomerLedgerEntryType.Payment && l.EntryDate < to && (!q.BranchId.HasValue || l.BranchId == q.BranchId)).Max(l => (DateOnly?)l.EntryDate),
                    NetSales = facts.Where(f => f.Key == c.Id.ToString()).Sum(f => (decimal?)f.Net) ?? 0,
                    InvoiceCount = facts.Where(f => f.Key == c.Id.ToString() && f.SoldQuantity > 0).Select(f => f.InvoiceId).Distinct().Count(),
                    GrossProfit = facts.Where(f => f.Key == c.Id.ToString()).Sum(f => (decimal?)(f.Net - f.Cost)) ?? 0 });
            if (report == "customer-outstanding") balances = balances.Where(x => x.Outstanding != 0);
            if (report == "credit-limit-utilization") balances = balances.Where(x => x.CreditLimit > 0);
            if (!string.IsNullOrWhiteSpace(q.Search)) balances = balances.Where(x => x.Customer.Contains(q.Search));
            if (report is "customer-outstanding" or "credit-limit-utilization")
                return await ReportPageAsync(balances.Where(x => !q.CustomerId.HasValue || x.CustomerId == q.CustomerId)
                    .OrderByDescending(x => x.Outstanding).ThenBy(x => x.CustomerId).Select(x => new
                    { x.CustomerId, x.Customer, x.CreditLimit, x.Outstanding, x.LastSale, x.LastPayment,
                        AvailableCredit = x.CreditLimit - Math.Max(0, x.Outstanding),
                        CreditUtilizationPercent = x.CreditLimit > 0 ? (decimal?)(Math.Max(0, x.Outstanding) / x.CreditLimit * 100) : null }), q, ct);
            return await ReportPageAsync(balances.Where(x => !q.CustomerId.HasValue || x.CustomerId == q.CustomerId)
                .OrderByDescending(x => x.Outstanding).ThenBy(x => x.CustomerId).Select(x => new
                { x.CustomerId, x.Customer, x.CreditLimit, x.Outstanding, x.LastSale, x.LastPayment, x.NetSales, x.GrossProfit, x.InvoiceCount,
                    AverageInvoice = x.InvoiceCount == 0 ? 0 : x.NetSales / x.InvoiceCount,
                    CreditUtilizationPercent = x.CreditLimit > 0 ? (decimal?)(Math.Max(0, x.Outstanding) / x.CreditLimit * 100) : null }), q, ct);
        }
    }

    public async Task<object> StaffPerformanceAsync(ReportQuery q, CancellationToken ct)
    {
        var query = FilterSales(db.Sales.AsNoTracking()).Where(s => s.Status == SaleStatus.Posted && s.PostedAtUtc >= q.FromUtc && s.PostedAtUtc < q.ToUtc &&
            (!q.BranchId.HasValue || s.BranchId == q.BranchId) && (!q.GodownId.HasValue || s.GodownId == q.GodownId) &&
            (!q.UserId.HasValue || s.CashierUserId == q.UserId));
        var grouped = query.GroupBy(s => new { s.CashierUserId, s.CashierUser!.FullName }).Select(g => new
        {
            UserId = g.Key.CashierUserId, Cashier = g.Key.FullName, SalesAmount = g.Sum(s => s.NetTotal), InvoiceCount = g.Count(),
            Discounts = g.Sum(s => s.DiscountTotal), CreditSales = g.Sum(s => s.CreditAmount),
            CashCollected = g.Sum(s => s.Payments.Where(p => p.Method == SalePaymentMethod.Cash).Sum(p => (decimal?)p.AmountApplied) ?? 0),
            Returns = db.SalesReturns.Where(r => r.OriginalSale!.CashierUserId == g.Key.CashierUserId && r.Status == SalesReturnStatus.Posted &&
                r.PostedAtUtc >= q.FromUtc && r.PostedAtUtc < q.ToUtc && (!q.BranchId.HasValue || r.BranchId == q.BranchId) &&
                (!q.GodownId.HasValue || r.OriginalSale.GodownId == q.GodownId)).Sum(r => (decimal?)r.RefundAmount) ?? 0,
            ShiftCount = db.CashierShifts.Count(s => s.CashierUserId == g.Key.CashierUserId && s.OpenedAtUtc >= q.FromUtc && s.OpenedAtUtc < q.ToUtc &&
                (!q.BranchId.HasValue || s.BranchId == q.BranchId)),
            CashVariance = db.CashierShifts.Where(s => s.CashierUserId == g.Key.CashierUserId && s.ClosedAtUtc >= q.FromUtc && s.ClosedAtUtc < q.ToUtc &&
                (!q.BranchId.HasValue || s.BranchId == q.BranchId)).Sum(s => s.CashVariance) ?? 0
        });
        return await ReportPageAsync(grouped.OrderByDescending(x => x.SalesAmount).ThenBy(x => x.UserId).Select(x => new
        { x.UserId, x.Cashier, x.SalesAmount, x.InvoiceCount, AverageInvoice = x.InvoiceCount == 0 ? 0 : x.SalesAmount / x.InvoiceCount,
            x.Discounts, x.CreditSales, x.CashCollected, x.Returns, x.ShiftCount, x.CashVariance }), q, ct);
    }
}
