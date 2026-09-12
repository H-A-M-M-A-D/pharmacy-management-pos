using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class AccountingRepository
{
    private sealed class AgingFact
    {
        public Guid PartyId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public DateOnly Due { get; set; }
        public decimal Outstanding { get; set; }
    }
    private sealed class AgingSummary
    {
        public Guid PartyId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Current { get; set; }
        public decimal Days1To30 { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Over90 { get; set; }
        public decimal Total { get; set; }
    }

    // The Phase 4 aging semantics: positive open documents, active allocations at cutoff,
    // return credits, positive opening entries, and UTC due-date buckets.
    private IQueryable<AgingFact> ArAgingFacts(DateTime cutoff, Guid? branch, Guid? party)
    {
        var day = DateOnly.FromDateTime(cutoff);
        var sales = context.Sales.AsNoTracking().Where(s => s.CustomerId != null && s.Status == SaleStatus.Posted && s.CreditAmount > 0 && s.PostedAtUtc <= cutoff &&
            (!branch.HasValue || s.BranchId == branch) && (!party.HasValue || s.CustomerId == party))
            .Select(s => new AgingFact { PartyId = s.CustomerId!.Value, Code = s.Customer!.CustomerCode, Name = s.Customer.Name,
                Due = DateOnly.FromDateTime(s.DueDateUtc ?? s.PostedAtUtc!.Value),
                Outstanding = decimal.Round(s.CreditAmount - (context.CustomerPaymentAllocations.Where(a => a.SaleId == s.Id && a.AllocatedAtUtc <= cutoff).Sum(a => (decimal?)a.AllocatedAmount) ?? 0)
                    - (context.SalesReturns.Where(r => r.OriginalSaleId == s.Id && r.PostedAtUtc <= cutoff).Sum(r => (decimal?)r.CustomerCreditReductionAmount) ?? 0), 2) });
        var openings = context.CustomerLedgerEntries.AsNoTracking().Where(e => e.EntryType == CustomerLedgerEntryType.OpeningBalance && e.Amount > 0 && e.EntryDate <= day &&
            (!branch.HasValue || e.BranchId == branch) && (!party.HasValue || e.CustomerId == party))
            .Select(e => new AgingFact { PartyId = e.CustomerId, Code = e.Customer!.CustomerCode, Name = e.Customer.Name, Due = e.EntryDate, Outstanding = e.Amount });
        return sales.Concat(openings).Where(x => x.Outstanding > 0);
    }

    private IQueryable<AgingFact> ApAgingFacts(DateTime cutoff, Guid? branch, Guid? party)
    {
        var day = DateOnly.FromDateTime(cutoff);
        var receipts = context.GoodsReceipts.AsNoTracking().Where(r => r.Status == GoodsReceiptStatus.Posted && r.NetTotal > 0 && r.ReceiptDate <= day &&
            (!branch.HasValue || r.BranchId == branch) && (!party.HasValue || r.SupplierId == party))
            .Select(r => new AgingFact { PartyId = r.SupplierId, Code = "", Name = r.Supplier!.Name, Due = r.DueDate ?? r.ReceiptDate,
                Outstanding = decimal.Round(r.NetTotal - (context.SupplierPaymentAllocations.Where(a => a.GoodsReceiptId == r.Id && a.AllocatedAtUtc <= cutoff).Sum(a => (decimal?)a.AllocatedAmount) ?? 0)
                    - (context.PurchaseReturns.Where(p => p.OriginalGoodsReceiptId == r.Id && p.PostedAtUtc <= cutoff).Sum(p => (decimal?)p.NetSupplierCredit) ?? 0), 2) });
        var openings = context.SupplierLedgerEntries.AsNoTracking().Where(e => e.EntryType == SupplierLedgerEntryType.OpeningBalance && e.Amount > 0 && e.EntryDate <= day &&
            (!branch.HasValue || e.BranchId == branch) && (!party.HasValue || e.SupplierId == party))
            .Select(e => new AgingFact { PartyId = e.SupplierId, Code = "", Name = e.Supplier!.Name, Due = e.EntryDate, Outstanding = e.Amount });
        return receipts.Concat(openings).Where(x => x.Outstanding > 0);
    }

    private static IQueryable<AgingSummary> AgingSummaries(IQueryable<AgingFact> facts, DateTime cutoff)
    {
        var day = DateOnly.FromDateTime(cutoff); var d30 = day.AddDays(-30); var d60 = day.AddDays(-60); var d90 = day.AddDays(-90);
        return facts.GroupBy(x => new { x.PartyId, x.Code, x.Name }).Select(g => new AgingSummary {
            PartyId = g.Key.PartyId, Code = g.Key.Code, Name = g.Key.Name,
            Current = g.Sum(x => x.Due >= day ? x.Outstanding : 0),
            Days1To30 = g.Sum(x => x.Due < day && x.Due >= d30 ? x.Outstanding : 0),
            Days31To60 = g.Sum(x => x.Due < d30 && x.Due >= d60 ? x.Outstanding : 0),
            Days61To90 = g.Sum(x => x.Due < d60 && x.Due >= d90 ? x.Outstanding : 0),
            Over90 = g.Sum(x => x.Due < d90 ? x.Outstanding : 0), Total = g.Sum(x => x.Outstanding)
        });
    }

    public async Task<object> GetPagedAgingReportAsync(ReportQuery q, bool supplier, CancellationToken ct)
    {
        var cutoff = q.ToUtc.AddTicks(-1);
        var rows = AgingSummaries(supplier ? ApAgingFacts(cutoff, q.BranchId, q.SupplierId) : ArAgingFacts(cutoff, q.BranchId, q.CustomerId), cutoff);
        if (!string.IsNullOrWhiteSpace(q.Search)) rows = rows.Where(x => x.Name.Contains(q.Search) || x.Code.Contains(q.Search));
        if (q.Status == "overdue") rows = rows.Where(x => x.Total > x.Current);
        var total = await rows.CountAsync(ct);
        var page = rows.OrderBy(x => x.Name).ThenBy(x => x.PartyId).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize);
        if (supplier) return new PagedReport<ApAgingSummaryRowDto>(await page.Select(x => new ApAgingSummaryRowDto(x.PartyId, x.Name,
            x.Current, x.Days1To30, x.Days31To60, x.Days61To90, x.Over90, x.Total)).ToListAsync(ct), total, q.Page, q.PageSize);
        return new PagedReport<ArAgingSummaryRowDto>(await page.Select(x => new ArAgingSummaryRowDto(x.PartyId, x.Code, x.Name,
            x.Current, x.Days1To30, x.Days31To60, x.Days61To90, x.Over90, x.Total)).ToListAsync(ct), total, q.Page, q.PageSize);
    }
}
