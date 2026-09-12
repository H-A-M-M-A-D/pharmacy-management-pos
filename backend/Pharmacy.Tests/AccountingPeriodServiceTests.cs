using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Accounting.Periods;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class AccountingPeriodServiceTests
{
    [Fact]
    public async Task Create_period_rejects_overlap_and_succeeds_otherwise()
    {
        var f = new Fixture(PermissionCatalog.AccountsPeriodsManage);
        var jan = await f.Service.CreatePeriodAsync(f.Actor.Id, new(2026, 1, "January 2026", new(2026, 1, 1), new(2026, 1, 31), null));
        Assert.Equal(AccountingPeriodStatus.Open, jan.Status);

        await Assert.ThrowsAsync<ResourceConflictException>(() =>
            f.Service.CreatePeriodAsync(f.Actor.Id, new(2026, 1, "Overlap", new(2026, 1, 15), new(2026, 2, 10), null)));

        var feb = await f.Service.CreatePeriodAsync(f.Actor.Id, new(2026, 2, "February 2026", new(2026, 2, 1), new(2026, 2, 28), null));
        Assert.Equal(2, f.Periods.Count);
        _ = feb;
    }

    [Fact]
    public async Task Period_lifecycle_soft_close_close_and_reopen_requires_reason()
    {
        var f = new Fixture(PermissionCatalog.AccountsPeriodsManage, PermissionCatalog.AccountsPeriodsClose, PermissionCatalog.AccountsPeriodsReopen);
        var period = await f.Service.CreatePeriodAsync(f.Actor.Id, new(2026, 1, "January 2026", new(2026, 1, 1), new(2026, 1, 31), null));

        var softClosed = await f.Service.SoftClosePeriodAsync(f.Actor.Id, period.Id, new(null));
        Assert.Equal(AccountingPeriodStatus.SoftClosed, softClosed.Status);

        var closed = await f.Service.ClosePeriodAsync(f.Actor.Id, period.Id, new(null));
        Assert.Equal(AccountingPeriodStatus.Closed, closed.Status);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ClosePeriodAsync(f.Actor.Id, period.Id, new(null)));

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ReopenPeriodAsync(f.Actor.Id, period.Id, new("")));
        var reopened = await f.Service.ReopenPeriodAsync(f.Actor.Id, period.Id, new("Correcting a posting error"));
        Assert.Equal(AccountingPeriodStatus.Open, reopened.Status);
    }

    [Fact]
    public async Task Fiscal_year_close_requires_all_periods_closed_first()
    {
        var f = new Fixture(PermissionCatalog.AccountsPeriodsManage, PermissionCatalog.AccountsPeriodsClose);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CloseFiscalYearAsync(f.Actor.Id, new(2026, null)));

        var jan = await f.Service.CreatePeriodAsync(f.Actor.Id, new(2026, 1, "January 2026", new(2026, 1, 1), new(2026, 1, 31), null));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CloseFiscalYearAsync(f.Actor.Id, new(2026, null)));

        await f.Service.ClosePeriodAsync(f.Actor.Id, jan.Id, new(null));
        var close = await f.Service.CloseFiscalYearAsync(f.Actor.Id, new(2026, "Year-end close"));
        Assert.Equal(FiscalYearCloseStatus.Closed, close.Status);
        Assert.Equal(0, close.NetProfit);

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CloseFiscalYearAsync(f.Actor.Id, new(2026, null)));
    }

    [Fact]
    public async Task Reopening_a_period_is_blocked_while_its_fiscal_year_is_closed()
    {
        var f = new Fixture(PermissionCatalog.AccountsPeriodsManage, PermissionCatalog.AccountsPeriodsClose, PermissionCatalog.AccountsPeriodsReopen);
        var jan = await f.Service.CreatePeriodAsync(f.Actor.Id, new(2026, 1, "January 2026", new(2026, 1, 1), new(2026, 1, 31), null));
        await f.Service.ClosePeriodAsync(f.Actor.Id, jan.Id, new(null));
        await f.Service.CloseFiscalYearAsync(f.Actor.Id, new(2026, null));

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ReopenPeriodAsync(f.Actor.Id, jan.Id, new("Need to fix something")));

        await f.Service.ReopenFiscalYearAsync(f.Actor.Id, 2026, new("Correcting December entries"));
        var reopened = await f.Service.ReopenPeriodAsync(f.Actor.Id, jan.Id, new("Need to fix something"));
        Assert.Equal(AccountingPeriodStatus.Open, reopened.Status);
    }

    private sealed class Fixture : IAccountingPeriodRepository
    {
        public readonly List<AccountingPeriod> Periods = [];
        public readonly List<FiscalYearClose> FiscalYearCloses = [];
        public User Actor { get; }
        public AccountingPeriodService Service { get; }

        public Fixture(params string[] permissions)
        {
            var role = new Role { Name = RoleCatalog.Accountant };
            foreach (var permission in permissions) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = PermissionCatalog.AccountsJournalView, Description = "x", Category = "test" } });
            Actor = new User { Username = "acct", NormalizedUsername = "ACCT", FullName = "Accountant", PasswordHash = "hash", BranchId = Guid.NewGuid(), RoleId = role.Id, Role = role };
            role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = PermissionCatalog.AccountsPeriodsView, Description = "x", Category = "test" } });
            var accountingRepository = new EmptyAccountingRepository(Actor);
            var accountingService = new AccountingService(accountingRepository, TimeProvider.System);
            Service = new AccountingPeriodService(this, accountingService, TimeProvider.System);
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);

        public Task<IReadOnlyList<AccountingPeriod>> ListPeriodsAsync(int? fiscalYear, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountingPeriod>>(Periods.Where(x => !fiscalYear.HasValue || x.FiscalYear == fiscalYear).ToList());
        public Task<AccountingPeriod?> GetPeriodAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Periods.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<AccountingPeriod>> GetOverlappingPeriodsAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountingPeriod>>(Periods.Where(x => x.StartDate <= endDate && x.EndDate >= startDate).ToList());
        public Task AddPeriodAsync(AccountingPeriod period, CancellationToken cancellationToken = default) { Periods.Add(period); return Task.CompletedTask; }

        public Task<FiscalYearClose?> GetFiscalYearCloseAsync(int fiscalYear, CancellationToken cancellationToken = default) => Task.FromResult(FiscalYearCloses.FirstOrDefault(x => x.FiscalYear == fiscalYear));
        public Task AddFiscalYearCloseAsync(FiscalYearClose close, CancellationToken cancellationToken = default) { FiscalYearCloses.Add(close); return Task.CompletedTask; }

        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>Minimal no-op <see cref="IAccountingRepository"/> so fiscal-year close can call the
    /// real <see cref="AccountingService.GetProfitAndLossAsync"/> without a second party's fixture —
    /// an empty ledger always produces a zero P&amp;L, which is exactly what these tests assert.</summary>
    private sealed class EmptyAccountingRepository(User actor) : IAccountingRepository
    {
        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(actor.Id == actorId ? actor : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(null);
        public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Customer?>(null);
        public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Supplier?>(null);
        public Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ChartOfAccount?>(null);
        public Task<ChartOfAccount?> GetAccountByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) => Task.FromResult<ChartOfAccount?>(null);
        public Task<IReadOnlyList<ChartOfAccount>> ListAccountsAsync(bool includeInactive, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ChartOfAccount>>([]);
        public Task AddAccountAsync(ChartOfAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<decimal> GetAccountBalanceAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult(0m);
        public Task<IReadOnlyList<AccountMapping>> ListAccountMappingsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AccountMapping>>([]);
        public Task<Dictionary<AccountMappingKey, Guid>> GetAccountMappingLookupAsync(CancellationToken cancellationToken = default) => Task.FromResult(new Dictionary<AccountMappingKey, Guid>());
        public Task<AccountMapping?> GetAccountMappingAsync(AccountMappingKey key, CancellationToken cancellationToken = default) => Task.FromResult<AccountMapping?>(null);
        public Task AddAccountMappingAsync(AccountMapping mapping, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsAccountMappedAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<string> NextJournalEntryNumberAsync(DateTime entryDateUtc, CancellationToken cancellationToken = default) => Task.FromResult("JV-0");
        public Task<bool> JournalEntryExistsForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<JournalEntry?> GetJournalEntryAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<JournalEntry?>(null);
        public Task<PagedResult<JournalEntryListItemDto>> ListJournalEntriesAsync(JournalEntryListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<JournalEntryListItemDto>([], 1, 25, 0));
        public Task<IReadOnlyList<TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TrialBalanceRowDto>>([]);
        public Task<IReadOnlyList<TrialBalanceRowDto>> GetAccountActivityAsync(DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TrialBalanceRowDto>>([]);
        public Task<GeneralLedgerDto> GetGeneralLedgerAsync(GeneralLedgerQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GeneralLedgerDto(query.ChartOfAccountId, string.Empty, string.Empty, NormalBalance.Debit, 0, 0, 0, 0, new PagedResult<GeneralLedgerLineDto>([], 1, 50, 0)));
        public Task<IReadOnlyList<ArAgingSummaryRowDto>> GetArAgingSummaryAsync(DateTime asOfUtc, Guid? branchId, Guid? customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ArAgingSummaryRowDto>>([]);
        public Task<ArAgingDetailDto?> GetArAgingDetailAsync(Guid customerId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<ArAgingDetailDto?>(null);
        public Task<IReadOnlyList<ApAgingSummaryRowDto>> GetApAgingSummaryAsync(DateTime asOfUtc, Guid? branchId, Guid? supplierId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ApAgingSummaryRowDto>>([]);
        public Task<ApAgingDetailDto?> GetApAgingDetailAsync(Guid supplierId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<ApAgingDetailDto?>(null);
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> JournalEntryHasReversalAsync(Guid journalEntryId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> JournalEntryLinkedToVoucherAsync(Guid journalEntryId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<AccountingPeriod?> GetCoveringPeriodAsync(DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult<AccountingPeriod?>(null);
        public void AllowPostingIntoSoftClosedPeriod() { }
        public Task<IReadOnlyList<CostCenter>> ListCostCentersAsync(bool includeInactive, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CostCenter>>([]);
        public Task<CostCenter?> GetCostCenterAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<CostCenter?>(null);
        public Task<CostCenter?> GetCostCenterByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) => Task.FromResult<CostCenter?>(null);
        public Task AddCostCenterAsync(CostCenter costCenter, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CashBankBookDto> GetCashBankBookAsync(AccountMappingKey mappingKey, CashBankBookQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CashBankBookDto(query.FromUtc, query.ToUtc, 0, 0, 0, 0, new PagedResult<CashBankBookLineDto>([], query.Page, query.PageSize, 0)));
        public Task<DayBookDto> GetDayBookAsync(DayBookQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DayBookDto(query.FromUtc, query.ToUtc, 0, 0, new PagedResult<DayBookLineDto>([], query.Page, query.PageSize, 0)));
        public Task<decimal> GetCashAndBankBalanceAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult(0m);
        public Task<IReadOnlyList<(Guid ChartOfAccountId, AccountType AccountType, CashFlowClassification? Classification, decimal OpeningBalance, decimal ClosingBalance)>> GetNonCashBalanceMovementsAsync(
            DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<(Guid, AccountType, CashFlowClassification?, decimal, decimal)>>([]);
        public Task<ControlReconciliationDto> GetArControlReconciliationAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult(new ControlReconciliationDto(asOfUtc, 0, 0, 0, []));
        public Task<ControlReconciliationDto> GetApControlReconciliationAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult(new ControlReconciliationDto(asOfUtc, 0, 0, 0, []));
        public Task<CashBankControlReconciliationDto> GetCashBankControlReconciliationAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CashBankControlReconciliationDto(asOfUtc, 0, 0, 0, 0, 0, 0, []));
        public Task<decimal> GetInventoryValuationAsync(Guid? branchId, Guid? godownId, CancellationToken cancellationToken = default) => Task.FromResult(0m);
        public Task<decimal> GetMappedAccountBalanceAsync(AccountMappingKey key, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult(0m);
    }
}
