using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class AccountingRepository(PharmacyDbContext context) : IAccountingRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);
    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) => context.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default) => context.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default) => context.ChartOfAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<ChartOfAccount?> GetAccountByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) =>
        context.ChartOfAccounts.FirstOrDefaultAsync(x => x.NormalizedCode == normalizedCode, cancellationToken);
    public async Task<IReadOnlyList<ChartOfAccount>> ListAccountsAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        await context.ChartOfAccounts.AsNoTracking().Where(x => includeInactive || x.IsActive).OrderBy(x => x.Code).ToListAsync(cancellationToken);
    public async Task AddAccountAsync(ChartOfAccount account, CancellationToken cancellationToken = default) => await context.ChartOfAccounts.AddAsync(account, cancellationToken);

    public async Task<decimal> GetAccountBalanceAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await context.ChartOfAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == accountId, cancellationToken);
        if (account is null) return 0;
        var debit = await context.JournalEntryLines.AsNoTracking().Where(x => x.ChartOfAccountId == accountId).SumAsync(x => (decimal?)x.Debit, cancellationToken) ?? 0;
        var credit = await context.JournalEntryLines.AsNoTracking().Where(x => x.ChartOfAccountId == accountId).SumAsync(x => (decimal?)x.Credit, cancellationToken) ?? 0;
        return account.NormalBalance == NormalBalance.Debit ? debit - credit : credit - debit;
    }

    public async Task<IReadOnlyList<AccountMapping>> ListAccountMappingsAsync(CancellationToken cancellationToken = default) =>
        await context.AccountMappings.AsNoTracking().Include(x => x.ChartOfAccount).OrderBy(x => x.MappingKey).ToListAsync(cancellationToken);
    public async Task<Dictionary<AccountMappingKey, Guid>> GetAccountMappingLookupAsync(CancellationToken cancellationToken = default) =>
        await context.AccountMappings.AsNoTracking().ToDictionaryAsync(x => x.MappingKey, x => x.ChartOfAccountId, cancellationToken);
    public Task<AccountMapping?> GetAccountMappingAsync(AccountMappingKey key, CancellationToken cancellationToken = default) =>
        context.AccountMappings.FirstOrDefaultAsync(x => x.MappingKey == key, cancellationToken);
    public async Task AddAccountMappingAsync(AccountMapping mapping, CancellationToken cancellationToken = default) => await context.AccountMappings.AddAsync(mapping, cancellationToken);
    public Task<bool> IsAccountMappedAsync(Guid accountId, CancellationToken cancellationToken = default) => context.AccountMappings.AnyAsync(x => x.ChartOfAccountId == accountId, cancellationToken);

    public async Task<string> NextJournalEntryNumberAsync(DateTime entryDateUtc, CancellationToken cancellationToken = default)
    {
        var next = await context.Database.SqlQueryRaw<long>("SELECT nextval('\"JournalEntryNumberSequence\"'::regclass) AS \"Value\"").SingleAsync(cancellationToken);
        return $"JV-{entryDateUtc.Year}-{next:000000}";
    }

    public Task<bool> JournalEntryExistsForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default) =>
        context.JournalEntries.AnyAsync(x => x.SourceType == sourceType && x.SourceId == sourceId, cancellationToken);

    public async Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default) => await context.JournalEntries.AddAsync(entry, cancellationToken);

    public Task<JournalEntry?> GetJournalEntryAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.JournalEntries.Include(x => x.Branch).Include(x => x.PostedByUser)
            .Include(x => x.Lines).ThenInclude(x => x.ChartOfAccount)
            .Include(x => x.Lines).ThenInclude(x => x.Customer)
            .Include(x => x.Lines).ThenInclude(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<PagedResult<JournalEntryListItemDto>> ListJournalEntriesAsync(JournalEntryListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var entries = context.JournalEntries.AsNoTracking().Include(x => x.Branch).Include(x => x.PostedByUser).Include(x => x.Lines).AsQueryable();
        if (!canSelectBranch && actorBranchId.HasValue) entries = entries.Where(x => x.BranchId == actorBranchId);
        if (query.BranchId.HasValue) entries = entries.Where(x => x.BranchId == query.BranchId);
        if (query.SourceType.HasValue) entries = entries.Where(x => x.SourceType == query.SourceType);
        if (query.FromUtc.HasValue) entries = entries.Where(x => x.EntryDateUtc >= query.FromUtc);
        if (query.ToUtc.HasValue) entries = entries.Where(x => x.EntryDateUtc <= query.ToUtc);
        if (query.ChartOfAccountId.HasValue) entries = entries.Where(x => x.Lines.Any(l => l.ChartOfAccountId == query.ChartOfAccountId));
        var total = await entries.CountAsync(cancellationToken);
        var items = await entries.OrderByDescending(x => x.EntryDateUtc).ThenByDescending(x => x.EntryNumber)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new JournalEntryListItemDto(x.Id, x.EntryNumber, x.EntryDateUtc, x.SourceType, x.Reference, x.Description,
                x.BranchId, x.Branch!.Name, x.PostedByUser!.FullName, x.Lines.Sum(l => l.Debit)))
            .ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var lines = context.JournalEntryLines.AsNoTracking().Include(x => x.ChartOfAccount)
            .Where(x => x.JournalEntry!.EntryDateUtc <= asOfUtc);
        if (branchId.HasValue) lines = lines.Where(x => x.BranchId == branchId);
        var grouped = await lines.GroupBy(x => new { x.ChartOfAccountId, x.ChartOfAccount!.Code, x.ChartOfAccount.Name, x.ChartOfAccount.AccountType })
            .Select(g => new { g.Key.ChartOfAccountId, g.Key.Code, g.Key.Name, g.Key.AccountType, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .ToListAsync(cancellationToken);
        return grouped.Where(x => x.Debit != 0 || x.Credit != 0).OrderBy(x => x.Code)
            .Select(x => new TrialBalanceRowDto(x.ChartOfAccountId, x.Code, x.Name, x.AccountType, x.Debit, x.Credit)).ToList();
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
            throw new ResourceConflictException("An accounting record with the same unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Accounting constraints were violated.");
        }
    }
}
