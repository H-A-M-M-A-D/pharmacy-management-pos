using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.Services.Accounting.RecurringJournals;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class RecurringJournalRepository(PharmacyDbContext context) : IRecurringJournalRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);
    public Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default) => context.ChartOfAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RecurringJournalTemplate>> ListTemplatesAsync(bool includeInactive, CancellationToken cancellationToken = default, Guid? branchId = null) =>
        await context.RecurringJournalTemplates.AsNoTracking().Include(x => x.Branch).Include(x => x.Lines).ThenInclude(l => l.ChartOfAccount)
            .Where(x => (includeInactive || x.IsActive) && (!branchId.HasValue || x.BranchId == branchId)).OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<RecurringJournalTemplate?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.RecurringJournalTemplates.Include(x => x.Branch).Include(x => x.Lines).ThenInclude(l => l.ChartOfAccount).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RecurringJournalTemplate>> ListDueTemplatesAsync(DateOnly asOfDate, CancellationToken cancellationToken = default, Guid? branchId = null) =>
        await context.RecurringJournalTemplates.AsNoTracking().Include(x => x.Lines)
            .Where(x => x.IsActive && x.NextRunDate <= asOfDate && (!branchId.HasValue || x.BranchId == branchId)).ToListAsync(cancellationToken);

    public async Task<RecurringJournalTemplate?> LockTemplateForGenerationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // A row lock serializes generation for this template across connections. Read committed
        // sees the previous generator's committed schedule after waiting for its lock.
        var template = await context.RecurringJournalTemplates
            .FromSqlInterpolated($"SELECT * FROM \"RecurringJournalTemplates\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (template is not null)
        {
            await context.Entry(template).ReloadAsync(cancellationToken);
            await context.Entry(template).Collection(x => x.Lines).LoadAsync(cancellationToken);
        }
        return template;
    }

    public async Task AddTemplateAsync(RecurringJournalTemplate template, CancellationToken cancellationToken = default) => await context.RecurringJournalTemplates.AddAsync(template, cancellationToken);

    public Task<bool> OccurrenceExistsAsync(Guid templateId, DateOnly scheduledDate, CancellationToken cancellationToken = default) =>
        context.RecurringJournalOccurrences.AnyAsync(x => x.RecurringJournalTemplateId == templateId && x.ScheduledDate == scheduledDate, cancellationToken);

    public async Task AddOccurrenceAsync(RecurringJournalOccurrence occurrence, CancellationToken cancellationToken = default) => await context.RecurringJournalOccurrences.AddAsync(occurrence, cancellationToken);

    public async Task<string> NextJournalEntryNumberAsync(DateTime entryDateUtc, CancellationToken cancellationToken = default)
    {
        var next = await context.Database.SqlQueryRaw<long>("SELECT nextval('\"JournalEntryNumberSequence\"'::regclass) AS \"Value\"").SingleAsync(cancellationToken);
        return $"JV-{entryDateUtc.Year}-{next:000000}";
    }

    public async Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default) => await context.JournalEntries.AddAsync(entry, cancellationToken);

    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ResourceConflictException("This recurring journal occurrence has already been generated.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Recurring journal constraints were violated.");
        }
    }

    public void AllowPostingIntoSoftClosedPeriod() => context.AllowPostingIntoSoftClosedPeriod = true;

    public void DiscardPendingChanges()
    {
        // Earlier SaveChanges calls inside a rolled-back batch accepted their entities as
        // Unchanged. They must also be discarded so retry cannot observe phantom history.
        context.ChangeTracker.Clear();
    }
}
