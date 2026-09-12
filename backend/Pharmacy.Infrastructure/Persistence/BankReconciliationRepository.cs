using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.Services.Accounting.BankReconciliations;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class BankReconciliationRepository(PharmacyDbContext context) : IBankReconciliationRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);

    public Task<FinancialAccount?> GetFinancialAccountAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.FinancialAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<decimal> GetBookBalanceAsync(Guid financialAccountId, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var movement = await context.FinancialLedgerEntries.AsNoTracking()
            .Where(x => x.FinancialAccountId == financialAccountId && x.OccurredAtUtc <= asOfUtc)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        // Account opening balances are already posted as OpeningBalance ledger entries.
        return movement;
    }

    public async Task<(decimal Matched, decimal Unmatched)> GetCandidateTotalsAsync(Guid financialAccountId, DateOnly statementEndDate, Guid currentReconciliationId, CancellationToken cancellationToken = default)
    {
        var cutoff = statementEndDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var totals = await context.FinancialLedgerEntries.AsNoTracking()
            .Where(x => x.FinancialAccountId == financialAccountId && x.OccurredAtUtc <= cutoff && (x.BankReconciliationId == null || x.BankReconciliationId == currentReconciliationId))
            .GroupBy(x => x.BankReconciliationId == currentReconciliationId)
            .Select(x => new { Matched = x.Key, Total = x.Sum(e => e.Amount) }).ToListAsync(cancellationToken);
        return (totals.Where(x => x.Matched).Sum(x => x.Total), totals.Where(x => !x.Matched).Sum(x => x.Total));
    }

    public Task<BankReconciliation?> GetOpenReconciliationForAccountAsync(Guid financialAccountId, CancellationToken cancellationToken = default) =>
        context.BankReconciliations.FirstOrDefaultAsync(x => x.FinancialAccountId == financialAccountId && x.Status == BankReconciliationStatus.InProgress, cancellationToken);

    public Task<BankReconciliation?> GetReconciliationAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.BankReconciliations.Include(x => x.FinancialAccount).Include(x => x.CreatedByUser).Include(x => x.FinalizedByUser).Include(x => x.ReopenedByUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BankReconciliation>> ListReconciliationsAsync(Guid? financialAccountId, CancellationToken cancellationToken = default, Guid? branchId = null, int limit = 100)
    {
        var query = context.BankReconciliations.AsNoTracking().Include(x => x.FinancialAccount).Include(x => x.CreatedByUser).Include(x => x.FinalizedByUser).AsQueryable();
        if (financialAccountId.HasValue) query = query.Where(x => x.FinancialAccountId == financialAccountId);
        if (branchId.HasValue) query = query.Where(x => x.FinancialAccount!.BranchId == branchId);
        return await query.OrderByDescending(x => x.StatementEndDate).ThenByDescending(x => x.Id)
            .Take(Math.Clamp(limit, 1, 100)).ToListAsync(cancellationToken);
    }

    public async Task AddReconciliationAsync(BankReconciliation reconciliation, CancellationToken cancellationToken = default) => await context.BankReconciliations.AddAsync(reconciliation, cancellationToken);

    public async Task<(IReadOnlyList<FinancialLedgerEntry> Lines, int TotalCount)> GetReconciliationLineEntriesAsync(
        Guid financialAccountId, DateOnly statementEndDate, Guid? currentReconciliationId, int maxRows, CancellationToken cancellationToken = default)
    {
        var cutoff = statementEndDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var query = context.FinancialLedgerEntries.AsNoTracking()
            .Where(x => x.FinancialAccountId == financialAccountId && x.OccurredAtUtc <= cutoff &&
                (x.BankReconciliationId == null || x.BankReconciliationId == currentReconciliationId));
        var total = await query.CountAsync(cancellationToken);
        var lines = await query.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id)
            .Take(Math.Clamp(maxRows, 1, 500)).ToListAsync(cancellationToken);
        return (lines, total);
    }

    public async Task<IReadOnlyList<FinancialLedgerEntry>> GetEntriesByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) =>
        await context.FinancialLedgerEntries.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);

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
            throw new ResourceConflictException("A bank reconciliation record with the same unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Bank reconciliation constraints were violated.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            throw new ResourceConflictException("This reconciliation changed while saving. Refresh and try again.");
        }
    }
}
