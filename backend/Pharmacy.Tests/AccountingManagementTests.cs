using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class AccountingManagementTests
{
    [Fact]
    public async Task Journal_posting_service_resolves_semantic_accounts_and_stages_a_balanced_entry()
    {
        var f = new Fixture();
        var posting = new JournalPostingService(f, TimeProvider.System);
        await posting.PostAsync(new(JournalSourceType.Sale, Guid.NewGuid(), f.Branch.Id, DateTime.UtcNow, "INV-1", "Cash sale",
            f.Actor.Id, [new(AccountMappingKey.Cash, 100, 0), new(AccountMappingKey.SalesRevenue, 0, 100)]));

        Assert.Single(f.JournalEntries);
        var entry = f.JournalEntries[0];
        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal(100, entry.Lines.Sum(x => x.Debit));
        Assert.Equal(100, entry.Lines.Sum(x => x.Credit));
        Assert.Equal(f.CashAccount.Id, entry.Lines.First(x => x.Debit > 0).ChartOfAccountId);
    }

    [Fact]
    public async Task Journal_posting_service_rejects_unbalanced_entries_and_missing_mappings()
    {
        var f = new Fixture();
        var posting = new JournalPostingService(f, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => posting.PostAsync(new(
            JournalSourceType.Sale, Guid.NewGuid(), f.Branch.Id, DateTime.UtcNow, null, "unbalanced", f.Actor.Id,
            [new(AccountMappingKey.Cash, 100, 0), new(AccountMappingKey.SalesRevenue, 0, 90)])));

        f.Mappings.RemoveAll(x => x.MappingKey == AccountMappingKey.RetainedEarnings);
        await Assert.ThrowsAsync<InvalidOperationException>(() => posting.PostAsync(new(
            JournalSourceType.OpeningBalance, Guid.NewGuid(), f.Branch.Id, DateTime.UtcNow, null, "no mapping", f.Actor.Id,
            [new(AccountMappingKey.Cash, 50, 0), new(AccountMappingKey.RetainedEarnings, 0, 50)])));
    }

    [Fact]
    public async Task Journal_posting_service_is_idempotent_per_source_type_and_id()
    {
        var f = new Fixture();
        var posting = new JournalPostingService(f, TimeProvider.System);
        var sourceId = Guid.NewGuid();
        await posting.PostAsync(new(JournalSourceType.Expense, sourceId, f.Branch.Id, DateTime.UtcNow, "EXP-1", "Rent",
            f.Actor.Id, [new(AccountMappingKey.Cash, 0, 100), new(AccountMappingKey.SalesRevenue, 100, 0)]));
        Assert.Single(f.JournalEntries);

        await posting.PostAsync(new(JournalSourceType.Expense, sourceId, f.Branch.Id, DateTime.UtcNow, "EXP-1", "Rent (retry)",
            f.Actor.Id, [new(AccountMappingKey.Cash, 0, 100), new(AccountMappingKey.SalesRevenue, 100, 0)]));
        Assert.Single(f.JournalEntries);

        await posting.PostAsync(new(JournalSourceType.Expense, Guid.NewGuid(), f.Branch.Id, DateTime.UtcNow, "EXP-2", "Rent",
            f.Actor.Id, [new(AccountMappingKey.Cash, 0, 100), new(AccountMappingKey.SalesRevenue, 100, 0)]));
        Assert.Equal(2, f.JournalEntries.Count);
    }

    [Fact]
    public async Task Create_account_rejects_duplicate_code_and_missing_parent()
    {
        var f = new Fixture(PermissionCatalog.AccountsCoaManage);
        var created = await f.Service.CreateAccountAsync(f.Actor.Id, new("9010", "Test Account", null, AccountType.Asset, NormalBalance.Debit, true, null));
        Assert.Equal("9010", created.Code);

        await Assert.ThrowsAsync<ResourceConflictException>(() =>
            f.Service.CreateAccountAsync(f.Actor.Id, new("9010", "Dup", null, AccountType.Asset, NormalBalance.Debit, true, null)));
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            f.Service.CreateAccountAsync(f.Actor.Id, new("9020", "Orphan", Guid.NewGuid(), AccountType.Asset, NormalBalance.Debit, true, null)));
    }

    [Fact]
    public async Task Manual_journal_requires_balance_active_posting_accounts_and_two_lines()
    {
        var f = new Fixture(PermissionCatalog.AccountsJournalPost);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.PostManualJournalAsync(f.Actor.Id, new(
            DateTime.UtcNow, f.Branch.Id, null, "single line", [new(f.CashAccount.Id, 100, 0)])));

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.PostManualJournalAsync(f.Actor.Id, new(
            DateTime.UtcNow, f.Branch.Id, null, "unbalanced",
            [new(f.CashAccount.Id, 100, 0), new(f.SalesAccount.Id, 0, 90)])));

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.PostManualJournalAsync(f.Actor.Id, new(
            DateTime.UtcNow, f.Branch.Id, null, "header account",
            [new(f.HeaderAccount.Id, 100, 0), new(f.SalesAccount.Id, 0, 100)])));

        var posted = await f.Service.PostManualJournalAsync(f.Actor.Id, new(
            DateTime.UtcNow, f.Branch.Id, "REF-1", "opening cash",
            [new(f.CashAccount.Id, 500, 0), new(f.SalesAccount.Id, 0, 500)]));
        Assert.Equal(500, posted.TotalDebit);
        Assert.Equal(500, posted.TotalCredit);
        Assert.StartsWith("JV-", posted.EntryNumber);
    }

    [Fact]
    public async Task Account_mapping_rejects_inactive_or_header_target_then_upserts()
    {
        var f = new Fixture(PermissionCatalog.AccountsCoaManage);
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            f.Service.SetAccountMappingAsync(f.Actor.Id, new(AccountMappingKey.Cash, f.HeaderAccount.Id)));

        f.CashAccount.IsActive = false;
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            f.Service.SetAccountMappingAsync(f.Actor.Id, new(AccountMappingKey.Cash, f.CashAccount.Id)));

        f.CashAccount.IsActive = true;
        var mapped = await f.Service.SetAccountMappingAsync(f.Actor.Id, new(AccountMappingKey.Cash, f.CashAccount.Id));
        Assert.Equal(f.CashAccount.Id, mapped.ChartOfAccountId);
        var remapped = await f.Service.SetAccountMappingAsync(f.Actor.Id, new(AccountMappingKey.Cash, f.SalesAccount.Id));
        Assert.Equal(f.SalesAccount.Id, remapped.ChartOfAccountId);
        Assert.Single(f.Mappings, x => x.MappingKey == AccountMappingKey.Cash);
    }

    [Fact]
    public async Task Deactivating_a_mapped_account_is_rejected()
    {
        var f = new Fixture(PermissionCatalog.AccountsCoaManage);
        await f.Service.SetAccountMappingAsync(f.Actor.Id, new(AccountMappingKey.Cash, f.CashAccount.Id));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.SetAccountActiveAsync(f.Actor.Id, f.CashAccount.Id, false));
    }

    [Fact]
    public async Task Missing_permission_is_forbidden()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            f.Service.CreateAccountAsync(f.Actor.Id, new("9999", "x", null, AccountType.Asset, NormalBalance.Debit, true, null)));
    }

    private sealed class Fixture : IAccountingRepository
    {
        public readonly Branch Branch = new() { Code = "MAIN", Name = "Main" };
        public readonly List<ChartOfAccount> Accounts = [];
        public readonly List<AccountMapping> Mappings = [];
        public readonly List<JournalEntry> JournalEntries = [];
        public readonly List<AuditLog> Audits = [];
        public readonly ChartOfAccount HeaderAccount;
        public readonly ChartOfAccount CashAccount;
        public readonly ChartOfAccount SalesAccount;
        public readonly ChartOfAccount RetainedEarningsAccount;
        public User Actor { get; }
        public AccountingService Service { get; }

        public Fixture(params string[] permissions)
        {
            var role = new Role { Name = RoleCatalog.Accountant };
            foreach (var permission in permissions) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "acct", NormalizedUsername = "ACCT", FullName = "Accountant", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role };

            HeaderAccount = new ChartOfAccount { Code = "1000", NormalizedCode = "1000", Name = "Assets", AccountType = AccountType.Asset, NormalBalance = NormalBalance.Debit, IsPostingAccount = false, IsActive = true };
            CashAccount = new ChartOfAccount { Code = "1010", NormalizedCode = "1010", Name = "Cash", ParentAccountId = HeaderAccount.Id, AccountType = AccountType.Asset, NormalBalance = NormalBalance.Debit, IsPostingAccount = true, IsActive = true };
            SalesAccount = new ChartOfAccount { Code = "4010", NormalizedCode = "4010", Name = "Sales Revenue", AccountType = AccountType.Income, NormalBalance = NormalBalance.Credit, IsPostingAccount = true, IsActive = true };
            RetainedEarningsAccount = new ChartOfAccount { Code = "3020", NormalizedCode = "3020", Name = "Retained Earnings", AccountType = AccountType.Equity, NormalBalance = NormalBalance.Credit, IsPostingAccount = true, IsActive = true };
            Accounts.AddRange([HeaderAccount, CashAccount, SalesAccount, RetainedEarningsAccount]);
            Mappings.Add(new AccountMapping { MappingKey = AccountMappingKey.Cash, ChartOfAccountId = CashAccount.Id });
            Mappings.Add(new AccountMapping { MappingKey = AccountMappingKey.SalesRevenue, ChartOfAccountId = SalesAccount.Id });
            Mappings.Add(new AccountMapping { MappingKey = AccountMappingKey.RetainedEarnings, ChartOfAccountId = RetainedEarningsAccount.Id });

            Service = new(this, TimeProvider.System);
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(Branch.Id == branchId ? Branch : null);
        public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Customer?>(null);
        public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Supplier?>(null);

        public Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Accounts.FirstOrDefault(x => x.Id == id));
        public Task<ChartOfAccount?> GetAccountByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) => Task.FromResult(Accounts.FirstOrDefault(x => x.NormalizedCode == normalizedCode));
        public Task<IReadOnlyList<ChartOfAccount>> ListAccountsAsync(bool includeInactive, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ChartOfAccount>>(Accounts.Where(x => includeInactive || x.IsActive).ToList());
        public Task AddAccountAsync(ChartOfAccount account, CancellationToken cancellationToken = default) { Accounts.Add(account); return Task.CompletedTask; }
        public Task<decimal> GetAccountBalanceAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult(0m);

        public Task<IReadOnlyList<AccountMapping>> ListAccountMappingsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AccountMapping>>(Mappings.ToList());
        public Task<Dictionary<AccountMappingKey, Guid>> GetAccountMappingLookupAsync(CancellationToken cancellationToken = default) => Task.FromResult(Mappings.ToDictionary(x => x.MappingKey, x => x.ChartOfAccountId));
        public Task<AccountMapping?> GetAccountMappingAsync(AccountMappingKey key, CancellationToken cancellationToken = default) => Task.FromResult(Mappings.FirstOrDefault(x => x.MappingKey == key));
        public Task AddAccountMappingAsync(AccountMapping mapping, CancellationToken cancellationToken = default) { Mappings.Add(mapping); return Task.CompletedTask; }
        public Task<bool> IsAccountMappedAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult(Mappings.Any(x => x.ChartOfAccountId == accountId));

        public Task<string> NextJournalEntryNumberAsync(DateTime entryDateUtc, CancellationToken cancellationToken = default) => Task.FromResult($"JV-{entryDateUtc.Year}-{JournalEntries.Count + 1:000000}");
        public Task<bool> JournalEntryExistsForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult(JournalEntries.Any(x => x.SourceType == sourceType && x.SourceId == sourceId));
        public Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default) { JournalEntries.Add(entry); return Task.CompletedTask; }
        public Task<JournalEntry?> GetJournalEntryAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(JournalEntries.FirstOrDefault(x => x.Id == id));
        public Task<PagedResult<JournalEntryListItemDto>> ListJournalEntriesAsync(JournalEntryListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<JournalEntryListItemDto>([], 1, 25, 0));
        public Task<IReadOnlyList<TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TrialBalanceRowDto>>([]);

        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
