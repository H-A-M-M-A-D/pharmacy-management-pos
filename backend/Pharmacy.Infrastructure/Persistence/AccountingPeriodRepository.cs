using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.Services.Accounting.Periods;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class AccountingPeriodRepository(PharmacyDbContext context) : IAccountingPeriodRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);

    public async Task<IReadOnlyList<AccountingPeriod>> ListPeriodsAsync(int? fiscalYear, CancellationToken cancellationToken = default)
    {
        var query = context.AccountingPeriods.AsNoTracking().Include(x => x.ClosedByUser).Include(x => x.ReopenedByUser).AsQueryable();
        if (fiscalYear.HasValue) query = query.Where(x => x.FiscalYear == fiscalYear);
        return await query.OrderBy(x => x.StartDate).ToListAsync(cancellationToken);
    }

    public Task<AccountingPeriod?> GetPeriodAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.AccountingPeriods.Include(x => x.ClosedByUser).Include(x => x.ReopenedByUser).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AccountingPeriod>> GetOverlappingPeriodsAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        await context.AccountingPeriods.AsNoTracking().Where(x => x.StartDate <= endDate && x.EndDate >= startDate).ToListAsync(cancellationToken);

    public async Task AddPeriodAsync(AccountingPeriod period, CancellationToken cancellationToken = default) => await context.AccountingPeriods.AddAsync(period, cancellationToken);

    public Task<FiscalYearClose?> GetFiscalYearCloseAsync(int fiscalYear, CancellationToken cancellationToken = default) =>
        context.FiscalYearCloses.Include(x => x.ClosedByUser).Include(x => x.ReopenedByUser).FirstOrDefaultAsync(x => x.FiscalYear == fiscalYear, cancellationToken);

    public async Task AddFiscalYearCloseAsync(FiscalYearClose close, CancellationToken cancellationToken = default) => await context.FiscalYearCloses.AddAsync(close, cancellationToken);

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
            throw new ResourceConflictException("An accounting period record with the same unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Accounting period constraints were violated.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            throw new ResourceConflictException("This period changed while saving. Refresh and try again.");
        }
    }
}
