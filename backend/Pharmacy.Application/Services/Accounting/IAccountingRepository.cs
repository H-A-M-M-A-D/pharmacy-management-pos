using System.Data;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting;

public interface IAccountingRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ChartOfAccount?> GetAccountByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChartOfAccount>> ListAccountsAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task AddAccountAsync(ChartOfAccount account, CancellationToken cancellationToken = default);
    Task<decimal> GetAccountBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountMapping>> ListAccountMappingsAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<AccountMappingKey, Guid>> GetAccountMappingLookupAsync(CancellationToken cancellationToken = default);
    Task<AccountMapping?> GetAccountMappingAsync(AccountMappingKey key, CancellationToken cancellationToken = default);
    Task AddAccountMappingAsync(AccountMapping mapping, CancellationToken cancellationToken = default);
    Task<bool> IsAccountMappedAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<string> NextJournalEntryNumberAsync(DateTime entryDateUtc, CancellationToken cancellationToken = default);
    Task<bool> JournalEntryExistsForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default);
    Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default);
    Task<JournalEntry?> GetJournalEntryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<JournalEntryListItemDto>> ListJournalEntriesAsync(JournalEntryListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
