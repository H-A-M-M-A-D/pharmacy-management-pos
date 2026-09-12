using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting.BankReconciliations;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class BankReconciliationServiceTests
{
    [Fact]
    public async Task Only_one_in_progress_reconciliation_is_allowed_per_account()
    {
        var f = new Fixture(PermissionCatalog.AccountsReconciliationManage);
        await f.Service.StartReconciliationAsync(f.Actor.Id, new(f.BankAccount.Id, new(2026, 1, 1), new(2026, 1, 31), 1000, 1500, null));
        await Assert.ThrowsAsync<ResourceConflictException>(() =>
            f.Service.StartReconciliationAsync(f.Actor.Id, new(f.BankAccount.Id, new(2026, 1, 1), new(2026, 1, 31), 1000, 1500, null)));
    }

    [Fact]
    public async Task Finalize_requires_zero_difference_unless_acknowledged_and_locks_matched_lines()
    {
        var f = new Fixture(PermissionCatalog.AccountsReconciliationManage);
        var entry1 = f.AddLedgerEntry(300);
        var entry2 = f.AddLedgerEntry(200);
        // Book balance (all ledger activity for the account, independent of per-line matching) is 500;
        // the statement says 450, so there's a genuine unexplained difference to resolve or acknowledge.
        var reconciliation = await f.Service.StartReconciliationAsync(f.Actor.Id, new(f.BankAccount.Id, new(2026, 1, 1), new(2026, 1, 31), 0, 450, null));

        await f.Service.MatchLinesAsync(f.Actor.Id, reconciliation.Id, new([entry1.Id, entry2.Id]));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.FinalizeReconciliationAsync(f.Actor.Id, reconciliation.Id, new(null, false)));

        var finalized = await f.Service.FinalizeReconciliationAsync(f.Actor.Id, reconciliation.Id, new("Bank fee not yet recorded", true));
        Assert.Equal(BankReconciliationStatus.Finalized, finalized.Status);
        Assert.Equal(-50, finalized.Difference);

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.MatchLinesAsync(f.Actor.Id, reconciliation.Id, new([entry1.Id])));

        var reopened = await f.Service.ReopenReconciliationAsync(f.Actor.Id, reconciliation.Id, new("Statement was corrected by the bank"));
        Assert.Equal(BankReconciliationStatus.InProgress, reopened.Status);
    }

    [Fact]
    public async Task Candidate_lines_are_scoped_to_the_statement_end_date_and_omitted_from_the_list_view()
    {
        // SeedPhase4Permissions grants Accountant both read and mutation permissions.
        var f = new Fixture(PermissionCatalog.AccountsReconciliationManage, PermissionCatalog.AccountsReconciliationView);
        var inPeriod = f.AddLedgerEntry(300);
        // Dated after the statement's end date: not a candidate for this statement, even though it's on the same account.
        var afterPeriod = f.AddLedgerEntry(999);
        afterPeriod.OccurredAtUtc = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc);

        var reconciliation = await f.Service.StartReconciliationAsync(f.Actor.Id, new(f.BankAccount.Id, new(2026, 1, 1), new(2026, 1, 31), 0, 300, null));

        var detail = await f.Service.GetReconciliationAsync(f.Actor.Id, reconciliation.Id);
        Assert.Single(detail.Lines);
        Assert.Equal(inPeriod.Id, detail.Lines[0].FinancialLedgerEntryId);
        Assert.Equal(1, detail.TotalCandidateCount);

        // The list view must never pull candidate lines at all (that was the unbounded N+1 query) —
        // only headline totals, computed independently of the per-line candidate scan.
        var list = await f.Service.ListReconciliationsAsync(f.Actor.Id, f.BankAccount.Id);
        var summary = Assert.Single(list);
        Assert.Empty(summary.Lines);
        Assert.Equal(0, summary.TotalCandidateCount);
    }

    [Fact]
    public async Task Candidate_lines_beyond_the_bound_are_truncated_but_the_total_count_reflects_every_match()
    {
        var f = new Fixture(PermissionCatalog.AccountsReconciliationManage, PermissionCatalog.AccountsReconciliationView);
        for (var i = 0; i < 501; i++) f.AddLedgerEntry(1);
        var reconciliation = await f.Service.StartReconciliationAsync(f.Actor.Id, new(f.BankAccount.Id, new(2026, 1, 1), new(2026, 1, 31), 0, 501, null));

        var detail = await f.Service.GetReconciliationAsync(f.Actor.Id, reconciliation.Id);
        Assert.Equal(500, detail.Lines.Count);
        Assert.Equal(501, detail.TotalCandidateCount);
        Assert.Equal(501, detail.UnmatchedTotal);
    }

    [Fact]
    public async Task View_and_manage_permissions_remain_independent()
    {
        var manageOnly = new Fixture(PermissionCatalog.AccountsReconciliationManage);
        var reconciliation = await manageOnly.Service.StartReconciliationAsync(manageOnly.Actor.Id, new(manageOnly.BankAccount.Id, new(2026, 1, 1), new(2026, 1, 31), 0, 0, null));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => manageOnly.Service.GetReconciliationAsync(manageOnly.Actor.Id, reconciliation.Id));
        var viewOnly = new Fixture(PermissionCatalog.AccountsReconciliationView);
        Assert.Empty(await viewOnly.Service.ListReconciliationsAsync(viewOnly.Actor.Id, null));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => viewOnly.Service.StartReconciliationAsync(viewOnly.Actor.Id, new(viewOnly.BankAccount.Id, new(2026, 1, 1), new(2026, 1, 31), 0, 0, null)));
    }

    [Fact]
    public async Task Accountant_cannot_list_read_or_mutate_another_branch_reconciliation()
    {
        var f = new Fixture(PermissionCatalog.AccountsReconciliationManage, PermissionCatalog.AccountsReconciliationView);
        var reconciliation = await f.Service.StartReconciliationAsync(f.Actor.Id, new(f.BankAccount.Id, new(2026, 1, 1), new(2026, 1, 31), 0, 0, null));
        f.Actor.BranchId = Guid.NewGuid();
        Assert.Empty(await f.Service.ListReconciliationsAsync(f.Actor.Id, null));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.GetReconciliationAsync(f.Actor.Id, reconciliation.Id));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.FinalizeReconciliationAsync(f.Actor.Id, reconciliation.Id, new(null, false)));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.StartReconciliationAsync(f.Actor.Id, new(f.BankAccount.Id, new(2026, 2, 1), new(2026, 2, 28), 0, 0, null)));
    }

    private sealed class Fixture : IBankReconciliationRepository
    {
        public readonly FinancialAccount BankAccount = new() { BranchId = Guid.NewGuid(), Name = "Main Bank", NormalizedName = "MAIN BANK", AccountType = FinancialAccountType.Bank, OpeningBalance = 0, IsActive = true };
        public readonly List<BankReconciliation> Reconciliations = [];
        public readonly List<FinancialLedgerEntry> Entries = [];
        public User Actor { get; }
        public BankReconciliationService Service { get; }

        public Fixture(params string[] permissions)
        {
            var role = new Role { Name = RoleCatalog.Accountant };
            foreach (var permission in permissions) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "acct", NormalizedUsername = "ACCT", FullName = "Accountant", PasswordHash = "hash", BranchId = BankAccount.BranchId, RoleId = role.Id, Role = role };
            Service = new BankReconciliationService(this, TimeProvider.System);
        }

        public FinancialLedgerEntry AddLedgerEntry(decimal amount)
        {
            var entry = new FinancialLedgerEntry
            {
                FinancialAccountId = BankAccount.Id, BranchId = BankAccount.BranchId, EntryType = FinancialLedgerEntryType.CustomerPayment, Amount = amount,
                ReferenceType = "Test", ReferenceId = Guid.NewGuid(), Description = "Test entry", CreatedByUserId = Actor.Id, OccurredAtUtc = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            };
            Entries.Add(entry);
            return entry;
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<FinancialAccount?> GetFinancialAccountAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<FinancialAccount?>(BankAccount.Id == id ? BankAccount : null);
        public Task<decimal> GetBookBalanceAsync(Guid financialAccountId, DateTime asOfUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(BankAccount.OpeningBalance + Entries.Where(x => x.FinancialAccountId == financialAccountId && x.OccurredAtUtc <= asOfUtc).Sum(x => x.Amount));

        public Task<(decimal Matched, decimal Unmatched)> GetCandidateTotalsAsync(Guid financialAccountId, DateOnly statementEndDate, Guid currentReconciliationId, CancellationToken cancellationToken = default)
        {
            var cutoff = statementEndDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            var rows = Entries.Where(x => x.FinancialAccountId == financialAccountId && x.OccurredAtUtc <= cutoff && (x.BankReconciliationId == null || x.BankReconciliationId == currentReconciliationId)).ToList();
            return Task.FromResult((rows.Where(x => x.BankReconciliationId == currentReconciliationId).Sum(x => x.Amount), rows.Where(x => x.BankReconciliationId == null).Sum(x => x.Amount)));
        }

        public Task<BankReconciliation?> GetOpenReconciliationForAccountAsync(Guid financialAccountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Reconciliations.FirstOrDefault(x => x.FinancialAccountId == financialAccountId && x.Status == BankReconciliationStatus.InProgress));
        public Task<BankReconciliation?> GetReconciliationAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Reconciliations.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<BankReconciliation>> ListReconciliationsAsync(Guid? financialAccountId, CancellationToken cancellationToken = default, Guid? branchId = null, int limit = 100) =>
            Task.FromResult<IReadOnlyList<BankReconciliation>>(Reconciliations.Where(x => (!financialAccountId.HasValue || x.FinancialAccountId == financialAccountId) && (!branchId.HasValue || BankAccount.BranchId == branchId))
                .OrderByDescending(x => x.StatementEndDate).Take(Math.Clamp(limit, 1, 100)).ToList());
        public Task AddReconciliationAsync(BankReconciliation reconciliation, CancellationToken cancellationToken = default) { Reconciliations.Add(reconciliation); return Task.CompletedTask; }

        public Task<(IReadOnlyList<FinancialLedgerEntry> Lines, int TotalCount)> GetReconciliationLineEntriesAsync(
            Guid financialAccountId, DateOnly statementEndDate, Guid? currentReconciliationId, int maxRows, CancellationToken cancellationToken = default)
        {
            var cutoff = statementEndDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            var matches = Entries.Where(x => x.FinancialAccountId == financialAccountId && x.OccurredAtUtc <= cutoff &&
                (x.BankReconciliationId == null || x.BankReconciliationId == currentReconciliationId))
                .OrderByDescending(x => x.OccurredAtUtc).ToList();
            return Task.FromResult<(IReadOnlyList<FinancialLedgerEntry>, int)>((matches.Take(maxRows).ToList(), matches.Count));
        }
        public Task<IReadOnlyList<FinancialLedgerEntry>> GetEntriesByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FinancialLedgerEntry>>(Entries.Where(x => ids.Contains(x.Id)).ToList());

        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
