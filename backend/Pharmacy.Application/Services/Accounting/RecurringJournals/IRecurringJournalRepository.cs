using System.Data;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.RecurringJournals;

public interface IRecurringJournalRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringJournalTemplate>> ListTemplatesAsync(bool includeInactive, CancellationToken cancellationToken = default, Guid? branchId = null);
    Task<RecurringJournalTemplate?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RecurringJournalTemplate?> LockTemplateForGenerationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringJournalTemplate>> ListDueTemplatesAsync(DateOnly asOfDate, CancellationToken cancellationToken = default, Guid? branchId = null);
    Task AddTemplateAsync(RecurringJournalTemplate template, CancellationToken cancellationToken = default);

    Task<bool> OccurrenceExistsAsync(Guid templateId, DateOnly scheduledDate, CancellationToken cancellationToken = default);
    Task AddOccurrenceAsync(RecurringJournalOccurrence occurrence, CancellationToken cancellationToken = default);

    Task<string> NextJournalEntryNumberAsync(DateTime entryDateUtc, CancellationToken cancellationToken = default);
    Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    void AllowPostingIntoSoftClosedPeriod();
    /// <summary>Clears tracking after rollback, including entities accepted by an earlier
    /// SaveChanges inside the rolled-back batch, so retry cannot observe phantom history.</summary>
    void DiscardPendingChanges();
}
