using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class AccountingRepository
{
    // The same read source used by the accounting activity report, grouped by branch for MIS.
    private IQueryable<JournalEntryLine> AccountActivityLines(DateTime fromUtc, DateTime toUtc, Guid? branchId)
    {
        var lines = context.JournalEntryLines.AsNoTracking().Where(x => x.JournalEntry!.EntryDateUtc >= fromUtc && x.JournalEntry.EntryDateUtc <= toUtc);
        return branchId.HasValue ? lines.Where(x => x.BranchId == branchId) : lines;
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TrialBalanceRowDto>>> GetBranchAccountActivityAsync(
        DateTime fromUtc, DateTime toUtc, IReadOnlyList<Guid> branchIds, CancellationToken ct)
    {
        var rows = await AccountActivityLines(fromUtc, toUtc, null).Where(x => branchIds.Contains(x.BranchId))
            .GroupBy(x => new { x.BranchId, x.ChartOfAccountId, x.ChartOfAccount!.Code, x.ChartOfAccount.Name, x.ChartOfAccount.AccountType, x.ChartOfAccount.NormalBalance })
            .Select(g => new { g.Key, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) }).ToListAsync(ct);
        return rows.GroupBy(x => x.Key.BranchId).ToDictionary(g => g.Key, g => (IReadOnlyList<TrialBalanceRowDto>)g.Select(x =>
            new TrialBalanceRowDto(x.Key.ChartOfAccountId, x.Key.Code, x.Key.Name, x.Key.AccountType, x.Key.NormalBalance, x.Debit, x.Credit)).ToList());
    }
}
