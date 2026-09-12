using System.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting.Vouchers;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Tests;

public sealed class VoucherServiceTests
{
    [Theory]
    [InlineData(VoucherType.CashReceipt, AccountMappingKey.Cash, 125)]
    [InlineData(VoucherType.CashPayment, AccountMappingKey.Cash, -125)]
    [InlineData(VoucherType.BankReceipt, AccountMappingKey.Bank, 125)]
    [InlineData(VoucherType.BankPayment, AccountMappingKey.Bank, -125)]
    public async Task Receipt_and_payment_post_balanced_GL_and_financial_ledger(VoucherType type, AccountMappingKey key, decimal movement)
    {
        using var f = new Fixture(type);
        var draft = await f.Create(type);
        var posted = await f.Service.PostAsync(f.Actor.Id, draft.Id);
        Assert.Equal(VoucherStatus.Posted, posted.Status);
        var journal = await f.Db.JournalEntries.Include(x => x.Lines).SingleAsync();
        Assert.Equal(125, journal.Lines.Sum(x => x.Debit));
        Assert.Equal(125, journal.Lines.Sum(x => x.Credit));
        var cash = Assert.Single(journal.Lines, x => x.ChartOfAccountId == f.Mappings[key]);
        Assert.Equal(movement, cash.Debit - cash.Credit);
        Assert.Equal(movement, (await f.Db.FinancialLedgerEntries.SingleAsync()).Amount);
    }

    [Fact]
    public async Task Contra_moves_balances_between_accounts()
    {
        using var f = new Fixture();
        var draft = await f.Service.CreateDraftAsync(f.Actor.Id, new(VoucherType.Contra, f.Date, f.Branch.Id, null, "Deposit",
            ChartOfAccountId: f.Mappings[AccountMappingKey.Cash], ContraToChartOfAccountId: f.Mappings[AccountMappingKey.Bank], Amount: 125));
        await f.Service.PostAsync(f.Actor.Id, draft.Id);
        var lines = (await f.Db.JournalEntries.Include(x => x.Lines).SingleAsync()).Lines;
        Assert.Equal(125, Assert.Single(lines, x => x.ChartOfAccountId == f.Mappings[AccountMappingKey.Bank]).Debit);
        Assert.Equal(125, Assert.Single(lines, x => x.ChartOfAccountId == f.Mappings[AccountMappingKey.Cash]).Credit);
    }

    [Theory]
    [InlineData(VoucherType.CashReceipt)]
    [InlineData(VoucherType.CashPayment)]
    [InlineData(VoucherType.BankReceipt)]
    [InlineData(VoucherType.BankPayment)]
    public async Task Reversal_compensates_GL_and_financial_ledger_and_rejects_duplicate(VoucherType type)
    {
        using var f = new Fixture(type);
        var draft = await f.Create(type);
        await f.Service.PostAsync(f.Actor.Id, draft.Id);
        var reversed = await f.Service.ReverseAsync(f.Actor.Id, draft.Id);
        Assert.Equal(draft.Id, reversed.ReversalOfVoucherId);
        var journals = await f.Db.JournalEntries.Include(x => x.Lines).ToListAsync();
        Assert.Equal(2, journals.Count);
        Assert.Equal(journals.Single(x => x.SourceId == draft.Id).Id, journals.Single(x => x.SourceId == reversed.Id).ReversesJournalEntryId);
        foreach (var account in journals.SelectMany(x => x.Lines).GroupBy(x => x.ChartOfAccountId))
            Assert.Equal(0, account.Sum(x => x.Debit - x.Credit));
        Assert.Equal(0, await f.Db.FinancialLedgerEntries.SumAsync(x => x.Amount));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ReverseAsync(f.Actor.Id, draft.Id));
        Assert.Equal(2, await f.Db.JournalEntries.CountAsync());
        Assert.Equal(2, await f.Db.FinancialLedgerEntries.CountAsync());
    }

    [Theory]
    [InlineData(AccountingPeriodStatus.Closed, false, false)]
    [InlineData(AccountingPeriodStatus.Closed, true, false)]
    [InlineData(AccountingPeriodStatus.SoftClosed, false, false)]
    [InlineData(AccountingPeriodStatus.SoftClosed, true, true)]
    public async Task Period_lock_and_soft_close_override_use_real_model_guard(AccountingPeriodStatus status, bool allowOverride, bool succeeds)
    {
        using var f = new Fixture(allowOverride: allowOverride);
        f.Db.AccountingPeriods.Add(new AccountingPeriod { FiscalYear = f.Date.Year, PeriodNumber = 1, Name = "January",
            StartDate = DateOnly.FromDateTime(f.Date), EndDate = DateOnly.FromDateTime(f.Date).AddMonths(1).AddDays(-1), Status = status });
        await f.Db.SaveChangesAsync();
        var draft = await f.Create(VoucherType.CashReceipt);
        if (succeeds) await f.Service.PostAsync(f.Actor.Id, draft.Id);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.PostAsync(f.Actor.Id, draft.Id));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(succeeds ? 1 : 0, await f.Db.JournalEntries.CountAsync());
        Assert.Equal(succeeds ? VoucherStatus.Posted : VoucherStatus.Draft, (await f.Db.Vouchers.SingleAsync()).Status);
    }

    private sealed class Fixture : IDisposable
    {
        public PharmacyDbContext Db { get; } = new(new DbContextOptionsBuilder<PharmacyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Branch Branch { get; } = new() { Code = "MAIN", Name = "Main" };
        public User Actor { get; }
        public FinancialAccount Financial { get; }
        public Dictionary<AccountMappingKey, Guid> Mappings { get; } = Enum.GetValues<AccountMappingKey>().ToDictionary(x => x, _ => Guid.NewGuid());
        public Guid OtherAccount { get; } = Guid.NewGuid();
        public DateTime Date { get; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public VoucherService Service { get; }
        private int sequence;

        public Fixture(VoucherType type = VoucherType.CashReceipt, bool allowOverride = false)
        {
            var role = new Role { Name = RoleCatalog.Accountant };
            var permissions = new List<string> { PermissionCatalog.AccountsVoucherCreate, PermissionCatalog.AccountsVoucherPost, PermissionCatalog.AccountsVoucherReverse };
            if (allowOverride) permissions.Add(PermissionCatalog.AccountsPostToSoftClosed);
            foreach (var permission in permissions) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "accounting" } });
            Actor = new User { Username = "acct", NormalizedUsername = "ACCT", FullName = "Accountant", PasswordHash = "test", BranchId = Branch.Id, RoleId = role.Id, Role = role };
            Financial = new FinancialAccount { BranchId = Branch.Id, Name = "Till", NormalizedName = "TILL", AccountType = type is VoucherType.BankReceipt or VoucherType.BankPayment ? FinancialAccountType.Bank : FinancialAccountType.Cash };
            var repository = new Mock<IVoucherRepository>();
            repository.Setup(x => x.GetActorAsync(Actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Actor);
            repository.Setup(x => x.GetBranchAsync(Branch.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Branch);
            repository.Setup(x => x.GetFinancialAccountAsync(Financial.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Financial);
            repository.Setup(x => x.GetAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => new ChartOfAccount { Id = id, Code = "1010", NormalizedCode = "1010", Name = "Account", IsPostingAccount = true });
            repository.Setup(x => x.GetAccountMappingLookupAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Mappings);
            repository.Setup(x => x.NextVoucherNumberAsync(It.IsAny<VoucherType>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => $"V-{++sequence}");
            repository.Setup(x => x.NextJournalEntryNumberAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => $"JV-{++sequence}");
            repository.Setup(x => x.AddVoucherAsync(It.IsAny<Voucher>(), It.IsAny<CancellationToken>())).Returns((Voucher v, CancellationToken ct) => { Db.Vouchers.Add(v); return Task.CompletedTask; });
            repository.Setup(x => x.GetVoucherAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns((Guid id, CancellationToken ct) => Db.Vouchers.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct));
            repository.Setup(x => x.AddJournalEntryAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>())).Returns(async (JournalEntry entry, CancellationToken ct) =>
            {
                await new Pharmacy.Infrastructure.Persistence.VoucherRepository(Db).AddJournalEntryAsync(entry, ct);
            });
            repository.Setup(x => x.AddFinancialLedgerEntryAsync(It.IsAny<FinancialLedgerEntry>(), It.IsAny<CancellationToken>())).Returns((FinancialLedgerEntry entry, CancellationToken ct) => { Db.FinancialLedgerEntries.Add(entry); return Task.CompletedTask; });
            repository.Setup(x => x.VoucherHasReversalAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns((Guid id, CancellationToken ct) => Db.Vouchers.AnyAsync(x => x.ReversalOfVoucherId == id, ct));
            repository.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>())).Returns((Func<CancellationToken, Task> action, IsolationLevel _, CancellationToken ct) => action(ct));
            repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(async (CancellationToken ct) => { await Db.SaveChangesAsync(ct); });
            repository.Setup(x => x.AllowPostingIntoSoftClosedPeriod()).Callback(() => Db.AllowPostingIntoSoftClosedPeriod = true);
            Service = new VoucherService(repository.Object, TimeProvider.System);
        }
        public Task<VoucherDto> Create(VoucherType type) => Service.CreateDraftAsync(Actor.Id, new(type, Date, Branch.Id, null, "Test voucher", ChartOfAccountId: OtherAccount, Amount: 125, FinancialAccountId: Financial.Id));
        public void Dispose() => Db.Dispose();
    }
}
