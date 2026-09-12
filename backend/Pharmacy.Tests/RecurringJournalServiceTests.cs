using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting.RecurringJournals;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class RecurringJournalServiceTests
{
    [Fact]
    public async Task Create_template_rejects_unbalanced_lines_and_succeeds_otherwise()
    {
        var f = new Fixture(PermissionCatalog.AccountsRecurringManage);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateTemplateAsync(f.Actor.Id, new(
            "Rent", null, RecurringJournalFrequency.Monthly, new(2026, 1, 1), null, f.Branch.Id,
            [new(f.RentAccount.Id, 1000, 0), new(f.CashAccount.Id, 0, 900)])));

        var template = await f.Service.CreateTemplateAsync(f.Actor.Id, new(
            "Rent", "Monthly office rent", RecurringJournalFrequency.Monthly, new(2026, 1, 1), null, f.Branch.Id,
            [new(f.RentAccount.Id, 1000, 0), new(f.CashAccount.Id, 0, 1000)]));
        Assert.Equal(new DateOnly(2026, 1, 1), template.NextRunDate);
    }

    [Fact]
    public async Task Generate_due_entries_is_idempotent_and_advances_next_run_date()
    {
        var f = new Fixture(PermissionCatalog.AccountsRecurringManage);
        var template = await f.Service.CreateTemplateAsync(f.Actor.Id, new(
            "Rent", null, RecurringJournalFrequency.Monthly, new(2026, 1, 1), null, f.Branch.Id,
            [new(f.RentAccount.Id, 1000, 0), new(f.CashAccount.Id, 0, 1000)]));

        var first = await f.Service.GenerateDueEntriesAsync(f.Actor.Id, new(new(2026, 1, 1)));
        Assert.Single(first.Generated);
        Assert.Single(f.JournalEntries);
        Assert.Equal(new DateOnly(2026, 2, 1), f.Templates.Single(x => x.Id == template.Id).NextRunDate);

        var retry = await f.Service.GenerateDueEntriesAsync(f.Actor.Id, new(new(2026, 1, 15)));
        Assert.Empty(retry.Generated);
        Assert.Single(f.JournalEntries);

        var second = await f.Service.GenerateDueEntriesAsync(f.Actor.Id, new(new(2026, 3, 1)));
        Assert.Equal(2, second.Generated.Count);
        Assert.Equal(3, f.JournalEntries.Count);
        Assert.Equal(new DateOnly(2026, 4, 1), f.Templates.Single(x => x.Id == template.Id).NextRunDate);
    }

    [Fact]
    public async Task Partial_failure_rolls_back_every_occurrence_and_schedule_then_retry_generates_once()
    {
        var f = new Fixture(PermissionCatalog.AccountsRecurringManage);
        await f.Service.CreateTemplateAsync(f.Actor.Id, new("Rent", null, RecurringJournalFrequency.Monthly,
            new(2026, 1, 1), null, f.Branch.Id, [new(f.RentAccount.Id, 1000, 0), new(f.CashAccount.Id, 0, 1000)]));
        f.FailOnJournalNumber = 2;
        var failed = await f.Service.GenerateDueEntriesAsync(f.Actor.Id, new(new(2026, 3, 1)));
        Assert.Single(failed.Failures);
        Assert.Empty(failed.Generated);
        Assert.Empty(f.JournalEntries);
        Assert.Empty(f.Occurrences);
        Assert.Equal(new DateOnly(2026, 1, 1), f.Templates.Single().NextRunDate);
        f.FailOnJournalNumber = null;
        var retry = await f.Service.GenerateDueEntriesAsync(f.Actor.Id, new(new(2026, 3, 1)));
        Assert.Empty(retry.Failures);
        Assert.Equal(3, retry.Generated.Count);
        Assert.Equal(3, f.Occurrences.Select(x => x.ScheduledDate).Distinct().Count());
        Assert.Empty((await f.Service.GenerateDueEntriesAsync(f.Actor.Id, new(new(2026, 3, 1)))).Generated);
        Assert.Equal(3, f.JournalEntries.Count);
    }

    [Fact]
    public async Task Accountant_cannot_generate_or_manage_another_branch_template()
    {
        var f = new Fixture(PermissionCatalog.AccountsRecurringManage, PermissionCatalog.AccountsRecurringView);
        var template = await f.Service.CreateTemplateAsync(f.Actor.Id, new("Rent", null, RecurringJournalFrequency.Monthly,
            new(2026, 1, 1), null, f.Branch.Id, [new(f.RentAccount.Id, 125, 0), new(f.CashAccount.Id, 0, 125)]));
        f.Actor.BranchId = Guid.NewGuid();
        Assert.Empty(await f.Service.ListTemplatesAsync(f.Actor.Id, false));
        Assert.Empty((await f.Service.GenerateDueEntriesAsync(f.Actor.Id, new(new(2026, 3, 1)))).Generated);
        Assert.Empty(f.JournalEntries);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.SetTemplateActiveAsync(f.Actor.Id, template.Id, false));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.CreateTemplateAsync(f.Actor.Id, new("Rent", null, RecurringJournalFrequency.Monthly,
            new(2026, 1, 1), null, f.Branch.Id, [new(f.RentAccount.Id, 125, 0), new(f.CashAccount.Id, 0, 125)])));
    }

    private sealed class Fixture : IRecurringJournalRepository
    {
        public readonly Branch Branch = new() { Code = "MAIN", Name = "Main" };
        public readonly List<ChartOfAccount> Accounts = [];
        public readonly List<RecurringJournalTemplate> Templates = [];
        public readonly List<RecurringJournalOccurrence> Occurrences = [];
        public readonly List<JournalEntry> JournalEntries = [];
        public readonly ChartOfAccount CashAccount;
        public readonly ChartOfAccount RentAccount;
        public User Actor { get; }
        public RecurringJournalService Service { get; }
        public int? FailOnJournalNumber { get; set; }

        public Fixture(params string[] permissions)
        {
            var role = new Role { Name = RoleCatalog.Accountant };
            foreach (var permission in permissions) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "acct", NormalizedUsername = "ACCT", FullName = "Accountant", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role };
            CashAccount = new ChartOfAccount { Code = "1010", NormalizedCode = "1010", Name = "Cash", AccountType = AccountType.Asset, NormalBalance = NormalBalance.Debit, IsPostingAccount = true, IsActive = true };
            RentAccount = new ChartOfAccount { Code = "6010", NormalizedCode = "6010", Name = "Rent", AccountType = AccountType.Expense, NormalBalance = NormalBalance.Debit, IsPostingAccount = true, IsActive = true };
            Accounts.AddRange([CashAccount, RentAccount]);
            Service = new RecurringJournalService(this, TimeProvider.System);
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(Branch.Id == branchId ? Branch : null);
        public Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Accounts.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<RecurringJournalTemplate>> ListTemplatesAsync(bool includeInactive, CancellationToken cancellationToken = default, Guid? branchId = null) =>
            Task.FromResult<IReadOnlyList<RecurringJournalTemplate>>(Templates.Where(x => (includeInactive || x.IsActive) && (!branchId.HasValue || x.BranchId == branchId)).ToList());
        public Task<RecurringJournalTemplate?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Templates.FirstOrDefault(x => x.Id == id));
        public Task<RecurringJournalTemplate?> LockTemplateForGenerationAsync(Guid id, CancellationToken cancellationToken = default) => GetTemplateAsync(id, cancellationToken);
        public Task<IReadOnlyList<RecurringJournalTemplate>> ListDueTemplatesAsync(DateOnly asOfDate, CancellationToken cancellationToken = default, Guid? branchId = null) =>
            Task.FromResult<IReadOnlyList<RecurringJournalTemplate>>(Templates.Where(x => x.IsActive && x.NextRunDate <= asOfDate && (!branchId.HasValue || x.BranchId == branchId)).ToList());
        public Task AddTemplateAsync(RecurringJournalTemplate template, CancellationToken cancellationToken = default) { Templates.Add(template); return Task.CompletedTask; }

        public Task<bool> OccurrenceExistsAsync(Guid templateId, DateOnly scheduledDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(Occurrences.Any(x => x.RecurringJournalTemplateId == templateId && x.ScheduledDate == scheduledDate));
        public Task AddOccurrenceAsync(RecurringJournalOccurrence occurrence, CancellationToken cancellationToken = default) { Occurrences.Add(occurrence); return Task.CompletedTask; }

        public Task<string> NextJournalEntryNumberAsync(DateTime entryDateUtc, CancellationToken cancellationToken = default) => Task.FromResult($"JV-{entryDateUtc.Year}-{JournalEntries.Count + 1:000000}");
        public Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default)
        {
            if (JournalEntries.Count + 1 == FailOnJournalNumber) throw new InvalidOperationException("Injected posting failure");
            JournalEntries.Add(entry);
            return Task.CompletedTask;
        }

        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
        {
            var journalCount = JournalEntries.Count;
            var occurrenceCount = Occurrences.Count;
            var schedules = Templates.ToDictionary(x => x.Id, x => x.NextRunDate);
            try { await operation(cancellationToken); }
            catch
            {
                JournalEntries.RemoveRange(journalCount, JournalEntries.Count - journalCount);
                Occurrences.RemoveRange(occurrenceCount, Occurrences.Count - occurrenceCount);
                foreach (var template in Templates) template.NextRunDate = schedules[template.Id];
                throw;
            }
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void AllowPostingIntoSoftClosedPeriod() { }
        public void DiscardPendingChanges() { }
    }
}
