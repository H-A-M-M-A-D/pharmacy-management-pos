using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A reusable set of balanced journal lines (rent, utilities accrual, a subscription, etc.) that gets
/// posted on demand via a "Generate Due Entries" action rather than a background scheduler — this
/// project has no background job infrastructure, so recurring posting is deliberately a manual,
/// idempotent action an accountant runs (e.g. once a day/week), not a cron job.
/// </summary>
public class RecurringJournalTemplate : Entity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public RecurringJournalFrequency Frequency { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly NextRunDate { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<RecurringJournalTemplateLine> Lines { get; set; } = [];
    public ICollection<RecurringJournalOccurrence> Occurrences { get; set; } = [];
}

public class RecurringJournalTemplateLine : Entity
{
    public Guid RecurringJournalTemplateId { get; set; }
    public RecurringJournalTemplate? RecurringJournalTemplate { get; set; }
    public Guid ChartOfAccountId { get; set; }
    public ChartOfAccount? ChartOfAccount { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// One generated occurrence of a template for a specific scheduled date. The unique
/// (TemplateId, ScheduledDate) pairing is what makes "Generate Due Entries" idempotent — running it
/// twice in the same day, or against a template whose due date was already generated, produces no
/// duplicate journal entries.
/// </summary>
public class RecurringJournalOccurrence : Entity
{
    public Guid RecurringJournalTemplateId { get; set; }
    public RecurringJournalTemplate? RecurringJournalTemplate { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public Guid JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public Guid GeneratedByUserId { get; set; }
    public User? GeneratedByUser { get; set; }
}

public enum RecurringJournalFrequency
{
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Yearly = 4
}
