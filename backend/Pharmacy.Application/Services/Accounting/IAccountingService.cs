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

    Task<ArAgingSummaryDto> GetArAgingSummaryAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, Guid? customerId, CancellationToken cancellationToken = default);
    Task<ArAgingDetailDto> GetArAgingDetailAsync(Guid actorId, Guid customerId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);
    Task<ApAgingSummaryDto> GetApAgingSummaryAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, Guid? supplierId, CancellationToken cancellationToken = default);
    Task<ApAgingDetailDto> GetApAgingDetailAsync(Guid actorId, Guid supplierId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);

    // ---- Journal reversal ----
    Task<JournalEntryDto> ReverseJournalEntryAsync(Guid actorId, Guid journalEntryId, ReverseJournalEntryRequest request, CancellationToken cancellationToken = default);

    // ---- Cost centers ----
    Task<IReadOnlyList<CostCenterDto>> ListCostCentersAsync(Guid actorId, bool includeInactive, CancellationToken cancellationToken = default);
    Task<CostCenterDto> CreateCostCenterAsync(Guid actorId, CostCenterRequest request, CancellationToken cancellationToken = default);
    Task SetCostCenterActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default);

    // ---- Enhanced Trial Balance ----
    Task<TrialBalanceMovementDto> GetTrialBalanceMovementAsync(Guid actorId, DateTime fromUtc, DateTime asOfUtc, Guid? branchId, bool includeZeroBalances, CancellationToken cancellationToken = default);

    // ---- Cash Book / Bank Book / Day Book ----
    Task<CashBankBookDto> GetCashBookAsync(Guid actorId, CashBankBookQuery query, CancellationToken cancellationToken = default);
    Task<CashBankBookDto> GetBankBookAsync(Guid actorId, CashBankBookQuery query, CancellationToken cancellationToken = default);
    Task<DayBookDto> GetDayBookAsync(Guid actorId, DayBookQuery query, CancellationToken cancellationToken = default);

    // ---- Cash flow statement ----
    Task<CashFlowStatementDto> GetCashFlowStatementAsync(Guid actorId, DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default);

    // ---- Control-account reconciliations ----
    Task<ControlReconciliationDto> GetArControlReconciliationAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);
    Task<ControlReconciliationDto> GetApControlReconciliationAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);
    Task<CashBankControlReconciliationDto> GetCashBankControlReconciliationAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);
    Task<InventoryReconciliationDto> GetInventoryReconciliationAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, Guid? godownId, CancellationToken cancellationToken = default);
}
