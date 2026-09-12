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
    Task<IReadOnlyList<TrialBalanceRowDto>> GetAccountActivityAsync(DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default);
    Task<GeneralLedgerDto> GetGeneralLedgerAsync(GeneralLedgerQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ArAgingSummaryRowDto>> GetArAgingSummaryAsync(DateTime asOfUtc, Guid? branchId, Guid? customerId, CancellationToken cancellationToken = default);
    Task<ArAgingDetailDto?> GetArAgingDetailAsync(Guid customerId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApAgingSummaryRowDto>> GetApAgingSummaryAsync(DateTime asOfUtc, Guid? branchId, Guid? supplierId, CancellationToken cancellationToken = default);
    Task<ApAgingDetailDto?> GetApAgingDetailAsync(Guid supplierId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    // ---- Journal reversal ----
    Task<bool> JournalEntryHasReversalAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
    Task<bool> JournalEntryLinkedToVoucherAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
    Task<AccountingPeriod?> GetCoveringPeriodAsync(DateOnly date, CancellationToken cancellationToken = default);
    void AllowPostingIntoSoftClosedPeriod();

    // ---- Cost centers ----
    Task<IReadOnlyList<CostCenter>> ListCostCentersAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<CostCenter?> GetCostCenterAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CostCenter?> GetCostCenterByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default);
    Task AddCostCenterAsync(CostCenter costCenter, CancellationToken cancellationToken = default);

    // ---- Cash Book / Bank Book / Day Book ----
    Task<CashBankBookDto> GetCashBankBookAsync(AccountMappingKey mappingKey, CashBankBookQuery query, CancellationToken cancellationToken = default);
    Task<DayBookDto> GetDayBookAsync(DayBookQuery query, CancellationToken cancellationToken = default);

    // ---- Cash flow statement ----
    Task<decimal> GetCashAndBankBalanceAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(Guid ChartOfAccountId, AccountType AccountType, CashFlowClassification? Classification, decimal OpeningBalance, decimal ClosingBalance)>>
        GetNonCashBalanceMovementsAsync(DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default);

    // ---- Control-account reconciliations ----
    Task<ControlReconciliationDto> GetArControlReconciliationAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);
    Task<ControlReconciliationDto> GetApControlReconciliationAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);
    Task<CashBankControlReconciliationDto> GetCashBankControlReconciliationAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);
    Task<decimal> GetInventoryValuationAsync(Guid? branchId, Guid? godownId, CancellationToken cancellationToken = default);
    Task<decimal> GetMappedAccountBalanceAsync(AccountMappingKey key, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default);
}
