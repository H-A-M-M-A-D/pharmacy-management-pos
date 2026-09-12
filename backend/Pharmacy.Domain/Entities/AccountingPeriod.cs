using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A named date range (typically a fiscal month) that gates financial posting. Central enforcement
/// lives in <see cref="Pharmacy.Infrastructure.Data.PharmacyDbContext"/>'s SaveChanges interceptor,
/// which blocks any new <see cref="JournalEntryLine"/>/<see cref="JournalEntry"/> dated inside a
/// <see cref="AccountingPeriodStatus.Closed"/> period outright, and inside a
/// <see cref="AccountingPeriodStatus.SoftClosed"/> period unless the caller has already verified the
/// posting actor holds an override permission and set
/// <see cref="Pharmacy.Infrastructure.Data.PharmacyDbContext.AllowPostingIntoSoftClosedPeriod"/>.
/// Periods are opt-in: a date with no covering period at all is always postable, so introducing this
/// feature never breaks existing history or in-flight posting for a business that hasn't set up
/// periods yet.
/// </summary>
public class AccountingPeriod : Entity
{
    public int FiscalYear { get; set; }
    public int PeriodNumber { get; set; }
    public required string Name { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public AccountingPeriodStatus Status { get; set; } = AccountingPeriodStatus.Open;
    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public User? ClosedByUser { get; set; }
    public DateTime? ReopenedAtUtc { get; set; }
    public Guid? ReopenedByUserId { get; set; }
    public User? ReopenedByUser { get; set; }
    public string? Notes { get; set; }
}

public enum AccountingPeriodStatus
{
    Open = 1,
    SoftClosed = 2,
    Closed = 3
}

/// <summary>
/// A procedural, audit-trailed lock over an entire fiscal year plus a frozen snapshot of its final
/// P&amp;L figures. This system's <see cref="ChartOfAccount"/>/<see cref="JournalEntry"/> engine never
/// needs a GL-posting "closing journal" that zeroes P&amp;L accounts into Retained Earnings — the
/// Balance Sheet already derives current-period earnings dynamically from full Income/CostOfSales/
/// Expense account activity (see AccountingService.GetBalanceSheetAsync), and posted journal entries
/// are immutable so a real closing entry could never be un-posted if the year were reopened, which
/// would double-count earnings forever. Closing a fiscal year therefore requires every
/// <see cref="AccountingPeriod"/> in that year to already be <see cref="AccountingPeriodStatus.Closed"/>
/// (which is what actually blocks further ordinary posting) and simply records the figures as they
/// stood at close time for audit/history purposes.
/// </summary>
public class FiscalYearClose : Entity
{
    public int FiscalYear { get; set; }
    public DateTime ClosedAtUtc { get; set; }
    public Guid ClosedByUserId { get; set; }
    public User? ClosedByUser { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCostOfGoodsSold { get; set; }
    public decimal TotalOperatingExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public string? Notes { get; set; }
    public FiscalYearCloseStatus Status { get; set; } = FiscalYearCloseStatus.Closed;
    public DateTime? ReopenedAtUtc { get; set; }
    public Guid? ReopenedByUserId { get; set; }
    public User? ReopenedByUser { get; set; }
    public string? ReopenReason { get; set; }
}

public enum FiscalYearCloseStatus
{
    Closed = 1,
    Reopened = 2
}
