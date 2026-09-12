using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Finance;
using Pharmacy.Application.Services.Finance;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class FinanceRepository(PharmacyDbContext context) : IFinanceRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission).FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);
    public Task<FinancialAccount?> GetAccountAsync(Guid accountId, bool forUpdate = false, CancellationToken cancellationToken = default) =>
        forUpdate
            ? context.FinancialAccounts.FromSqlInterpolated($"SELECT * FROM \"FinancialAccounts\" WHERE \"Id\" = {accountId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            : context.FinancialAccounts.FirstOrDefaultAsync(x => x.Id == accountId, cancellationToken);
    public Task<ExpenseCategory?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) => context.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);
    public Task<CostCenter?> GetCostCenterAsync(Guid id, CancellationToken cancellationToken = default) => context.CostCenters.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<bool> AccountNameExistsAsync(Guid branchId, string normalizedName, Guid? excludingId = null, CancellationToken cancellationToken = default) => context.FinancialAccounts.AnyAsync(x => x.BranchId == branchId && x.NormalizedName == normalizedName && (!excludingId.HasValue || x.Id != excludingId), cancellationToken);
    public Task<bool> CategoryNameExistsAsync(string normalizedName, Guid? excludingId = null, CancellationToken cancellationToken = default) => context.ExpenseCategories.AnyAsync(x => x.NormalizedName == normalizedName && (!excludingId.HasValue || x.Id != excludingId), cancellationToken);
    public async Task<decimal> GetBalanceAsync(Guid accountId, DateTime? beforeUtc = null, CancellationToken cancellationToken = default)
    {
        var query = context.FinancialLedgerEntries.Where(x => x.FinancialAccountId == accountId);
        if (beforeUtc.HasValue) query = query.Where(x => x.OccurredAtUtc < beforeUtc);
        return await query.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
    }
    public Task<string> NextExpenseNumberAsync(DateTime occurredAtUtc, CancellationToken cancellationToken = default) => NextNumber("ExpenseNumberSequence", "EXP", occurredAtUtc, cancellationToken);
    public Task<string> NextIncomeNumberAsync(DateTime occurredAtUtc, CancellationToken cancellationToken = default) => NextNumber("OtherIncomeNumberSequence", "INC", occurredAtUtc, cancellationToken);
    public Task<string> NextTransferNumberAsync(DateTime occurredAtUtc, CancellationToken cancellationToken = default) => NextNumber("FinancialTransferNumberSequence", "TRF", occurredAtUtc, cancellationToken);
    private async Task<string> NextNumber(string sequence, string prefix, DateTime date, CancellationToken ct)
    {
        var next = sequence switch
        {
            "ExpenseNumberSequence" => await context.Database.SqlQueryRaw<long>("SELECT nextval('\"ExpenseNumberSequence\"'::regclass) AS \"Value\"").SingleAsync(ct),
            "OtherIncomeNumberSequence" => await context.Database.SqlQueryRaw<long>("SELECT nextval('\"OtherIncomeNumberSequence\"'::regclass) AS \"Value\"").SingleAsync(ct),
            "FinancialTransferNumberSequence" => await context.Database.SqlQueryRaw<long>("SELECT nextval('\"FinancialTransferNumberSequence\"'::regclass) AS \"Value\"").SingleAsync(ct),
            _ => throw new InvalidOperationException("Unknown finance sequence.")
        };
        return $"{prefix}-{date.Year}-{next:000000}";
    }
    public async Task AddAccountAsync(FinancialAccount account, CancellationToken cancellationToken = default) => await context.FinancialAccounts.AddAsync(account, cancellationToken);
    public async Task AddCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default) => await context.ExpenseCategories.AddAsync(category, cancellationToken);
    public async Task AddExpenseAsync(Expense expense, CancellationToken cancellationToken = default) => await context.Expenses.AddAsync(expense, cancellationToken);
    public Task<Expense?> GetExpenseEntityAsync(Guid id, CancellationToken cancellationToken = default) => context.Expenses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<OtherIncome?> GetOtherIncomeEntityAsync(Guid id, CancellationToken cancellationToken = default) => context.OtherIncomes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task AddOtherIncomeAsync(OtherIncome income, CancellationToken cancellationToken = default) => await context.OtherIncomes.AddAsync(income, cancellationToken);
    public async Task AddTransferAsync(FinancialTransfer transfer, CancellationToken cancellationToken = default) => await context.FinancialTransfers.AddAsync(transfer, cancellationToken);
    public async Task AddLedgerEntryAsync(FinancialLedgerEntry entry, CancellationToken cancellationToken = default) => await context.FinancialLedgerEntries.AddAsync(entry, cancellationToken);
    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task<IReadOnlyList<FinancialAccountDto>> ListAccountsAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        var query = context.FinancialAccounts.AsNoTracking().Include(x => x.Branch).AsQueryable();
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        return await query.OrderBy(x => x.Branch!.Name).ThenBy(x => x.Name).Select(x => new FinancialAccountDto(x.Id, x.BranchId, x.Branch!.Name, x.Name, x.AccountType,
            x.OpeningBalance, context.FinancialLedgerEntries.Where(e => e.FinancialAccountId == x.Id).Sum(e => (decimal?)e.Amount) ?? 0m,
            x.IsActive, x.Notes, x.CreatedAt, x.UpdatedAt)).ToListAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(bool? active, CancellationToken cancellationToken = default)
    {
        var query = context.ExpenseCategories.AsNoTracking().AsQueryable();
        if (active.HasValue) query = query.Where(x => x.IsActive == active);
        return await query.OrderBy(x => x.Name).Select(x => new ExpenseCategoryDto(x.Id, x.Name, x.Description, x.IsActive, x.CreatedAt, x.UpdatedAt)).ToListAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<ExpenseDto>> ListExpensesAsync(ExpenseQuery request, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var query = context.Expenses.AsNoTracking().Include(x => x.Branch).Include(x => x.ExpenseCategory).Include(x => x.FinancialAccount).Include(x => x.CreatedByUser).Include(x => x.ReversedByUser).Include(x => x.CostCenter).AsQueryable();
        if (!canSelectBranch && actorBranchId.HasValue) query = query.Where(x => x.BranchId == actorBranchId);
        if (request.BranchId.HasValue) query = query.Where(x => x.BranchId == request.BranchId);
        if (request.CategoryId.HasValue) query = query.Where(x => x.ExpenseCategoryId == request.CategoryId);
        if (request.FinancialAccountId.HasValue) query = query.Where(x => x.FinancialAccountId == request.FinancialAccountId);
        if (request.DateFrom.HasValue) query = query.Where(x => x.ExpenseDateUtc >= request.DateFrom);
        if (request.DateTo.HasValue) query = query.Where(x => x.ExpenseDateUtc <= request.DateTo);
        return await query.OrderByDescending(x => x.ExpenseDateUtc).ThenByDescending(x => x.ExpenseNumber)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new ExpenseDto(x.Id, x.ExpenseNumber, x.BranchId, x.Branch!.Name, x.ExpenseCategoryId, x.ExpenseCategory!.Name,
                x.FinancialAccountId, x.FinancialAccount!.Name, x.ExpenseDateUtc, x.Amount, x.Description, x.Payee, x.ReferenceNumber, x.Notes, x.CreatedByUser!.FullName, x.PostedAtUtc,
                x.ReversedAtUtc, x.ReversedByUser!.FullName, x.ReversalReason, x.CostCenterId, x.CostCenter!.Name))
            .ToListAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<FinancialLedgerEntryDto>> ListLedgerAsync(Guid accountId, FinancialLedgerQuery request, CancellationToken cancellationToken = default)
    {
        var query = context.FinancialLedgerEntries.AsNoTracking().Include(x => x.CreatedByUser).Where(x => x.FinancialAccountId == accountId);
        if (request.DateFrom.HasValue) query = query.Where(x => x.OccurredAtUtc >= request.DateFrom);
        if (request.DateTo.HasValue) query = query.Where(x => x.OccurredAtUtc <= request.DateTo);
        if (request.EntryType.HasValue) query = query.Where(x => x.EntryType == request.EntryType);
        var rows = await query.OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id).Select(x => new { x.Id, x.OccurredAtUtc, x.EntryType, x.ReferenceType, x.ReferenceId, x.ReferenceNumber, x.Description, x.Amount, CreatedByName = x.CreatedByUser!.FullName }).ToListAsync(cancellationToken);
        var running = 0m;
        var mapped = rows.Select(x => { running += x.Amount; return new FinancialLedgerEntryDto(x.Id, x.OccurredAtUtc, x.EntryType, x.ReferenceType, x.ReferenceId, x.ReferenceNumber, x.Description, x.Amount, running, x.CreatedByName); }).ToList();
        return mapped.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
    }
    public async Task<IReadOnlyList<FinancialLedgerEntry>> ListPositionEntriesAsync(Guid branchId, Guid? accountId, DateTime dayStartUtc, DateTime dayEndUtc, CancellationToken cancellationToken = default)
    {
        var query = context.FinancialLedgerEntries.AsNoTracking().Where(x => x.BranchId == branchId && x.OccurredAtUtc >= dayStartUtc && x.OccurredAtUtc < dayEndUtc);
        if (accountId.HasValue) query = query.Where(x => x.FinancialAccountId == accountId);
        return await query.ToListAsync(cancellationToken);
    }
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        try { await operation(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch (Exception ex) when (IsSerializationConflict(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ResourceConflictException("This financial transaction could not be completed because the same account was changed by another operation at the same time. Please retry.");
        }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }
    }

    // Under IsolationLevel.Serializable, a genuine concurrent-update conflict surfaces from
    // Postgres as SqlState 40001/40P01, but EF Core's default (non-retrying) execution strategy
    // may rewrap it deeper in InvalidOperationException before it reaches the repository boundary.
    // Walk the whole chain instead of checking one fixed depth.
    private static bool IsSerializationConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected })
                return true;
            if (current is InvalidOperationException && current.Message.Contains("transient failure", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (IsSerializationConflict(ex)) { throw new ResourceConflictException("This financial transaction could not be completed because the same account was changed by another operation at the same time. Please retry."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { throw new ResourceConflictException("A finance record with this unique value already exists."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation }) { throw new RequestValidationException("A financial database constraint was violated."); }
    }
}
