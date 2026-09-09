using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting;

public interface IAccountingService
{
    Task<IReadOnlyList<ChartOfAccountListItemDto>> ListAccountsAsync(Guid actorId, bool includeInactive, CancellationToken cancellationToken = default);
    Task<ChartOfAccountDto> GetAccountAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<ChartOfAccountDto> CreateAccountAsync(Guid actorId, ChartOfAccountRequest request, CancellationToken cancellationToken = default);
    Task<ChartOfAccountDto> UpdateAccountAsync(Guid actorId, Guid id, ChartOfAccountUpdateRequest request, CancellationToken cancellationToken = default);
    Task SetAccountActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountMappingDto>> ListAccountMappingsAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<AccountMappingDto> SetAccountMappingAsync(Guid actorId, SetAccountMappingRequest request, CancellationToken cancellationToken = default);

    Task<JournalEntryDto> PostManualJournalAsync(Guid actorId, PostManualJournalRequest request, CancellationToken cancellationToken = default);
    Task<JournalEntryDto> GetJournalEntryAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<JournalEntryListItemDto>> ListJournalEntriesAsync(Guid actorId, JournalEntryListQuery query, CancellationToken cancellationToken = default);
    Task<TrialBalanceDto> GetTrialBalanceAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);
    Task<GeneralLedgerDto> GetGeneralLedgerAsync(Guid actorId, GeneralLedgerQuery query, CancellationToken cancellationToken = default);
    Task<ProfitAndLossDto> GetProfitAndLossAsync(Guid actorId, DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default);
    Task<BalanceSheetDto> GetBalanceSheetAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);
}
