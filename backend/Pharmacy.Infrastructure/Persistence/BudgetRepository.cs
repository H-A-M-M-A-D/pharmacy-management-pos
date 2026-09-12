using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Services.Accounting.Budgets;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class BudgetRepository(PharmacyDbContext context) : IBudgetRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
    public Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default) => context.ChartOfAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);

    public async Task<IReadOnlyList<AccountBudget>> ListBudgetsAsync(int fiscalYear, int? periodNumber, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var query = context.AccountBudgets.AsNoTracking().Include(x => x.ChartOfAccount).Include(x => x.Branch)
            .Where(x => x.FiscalYear == fiscalYear && x.PeriodNumber == periodNumber && x.BranchId == branchId);
        return await query.OrderBy(x => x.ChartOfAccount!.Code).ToListAsync(cancellationToken);
    }

    public Task<AccountBudget?> GetBudgetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.AccountBudgets.Include(x => x.ChartOfAccount).Include(x => x.Branch).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<AccountBudget?> FindExistingAsync(int fiscalYear, int? periodNumber, Guid chartOfAccountId, Guid? branchId, CancellationToken cancellationToken = default) =>
        context.AccountBudgets.FirstOrDefaultAsync(x => x.FiscalYear == fiscalYear && x.PeriodNumber == periodNumber && x.ChartOfAccountId == chartOfAccountId && x.BranchId == branchId, cancellationToken);

    public async Task AddBudgetAsync(AccountBudget budget, CancellationToken cancellationToken = default) => await context.AccountBudgets.AddAsync(budget, cancellationToken);

    public async Task<IReadOnlyList<TrialBalanceRowDto>> GetActualActivityAsync(DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var lines = context.JournalEntryLines.AsNoTracking().Where(x => x.JournalEntry!.EntryDateUtc >= fromUtc && x.JournalEntry.EntryDateUtc <= toUtc);
        if (branchId.HasValue) lines = lines.Where(x => x.BranchId == branchId);
        var grouped = await lines.GroupBy(x => new { x.ChartOfAccountId, x.ChartOfAccount!.Code, x.ChartOfAccount.Name, x.ChartOfAccount.AccountType, x.ChartOfAccount.NormalBalance })
            .Select(g => new { g.Key.ChartOfAccountId, g.Key.Code, g.Key.Name, g.Key.AccountType, g.Key.NormalBalance, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .ToListAsync(cancellationToken);
        return grouped.Select(x => new TrialBalanceRowDto(x.ChartOfAccountId, x.Code, x.Name, x.AccountType, x.NormalBalance, x.Debit, x.Credit)).ToList();
    }

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
            throw new ResourceConflictException("A budget already exists for this account/period/branch combination.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Budget constraints were violated.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            throw new ResourceConflictException("This budget changed while saving. Refresh and try again.");
        }
    }
}
