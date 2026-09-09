using System.Data;
using Moq;
using Pharmacy.Application.DTOs.Finance;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Finance;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class FinanceAccountingIntegrationTests
{
    [Fact]
    public async Task Financial_account_debit_and_credit_adjustments_post_exact_balanced_suspense_journals()
    {
        var branch = new Branch { Code = "MAIN", Name = "Main" };
        var role = new Role { Name = RoleCatalog.Manager };
        role.RolePermissions.Add(new RolePermission
        {
            Permission = new Permission { Code = PermissionCatalog.FinanceAdjust, Description = "Adjust finance", Category = "test" }
        });
        var actor = new User
        {
            Username = "manager", NormalizedUsername = "MANAGER", FullName = "Manager", PasswordHash = "hash",
            BranchId = branch.Id, RoleId = role.Id, Role = role
        };
        var account = new FinancialAccount
        {
            BranchId = branch.Id, Name = "Main Cash", NormalizedName = "MAIN CASH",
            AccountType = FinancialAccountType.Cash, IsActive = true
        };
        var ledger = new List<FinancialLedgerEntry>();
        var repository = new Mock<IFinanceRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetActorAsync(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(actor);
        repository.Setup(x => x.GetAccountAsync(account.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        repository.Setup(x => x.GetBalanceAsync(account.Id, null, It.IsAny<CancellationToken>())).ReturnsAsync(1000m);
        repository.Setup(x => x.AddLedgerEntryAsync(It.IsAny<FinancialLedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<FinancialLedgerEntry, CancellationToken>((entry, _) => ledger.Add(entry)).Returns(Task.CompletedTask);
        repository.Setup(x => x.AddAuditAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), IsolationLevel.Serializable, It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> operation, IsolationLevel _, CancellationToken ct) => operation(ct));
        var journal = new RecordingJournalPostingService();
        var service = new FinanceService(repository.Object, journal, TimeProvider.System);
        var occurredAt = DateTime.UtcNow;

        await service.PostAdjustmentAsync(actor.Id, new(branch.Id, account.Id, FinancialAdjustmentType.Debit, 125m, occurredAt, "cash shortage correction"));
        await service.PostAdjustmentAsync(actor.Id, new(branch.Id, account.Id, FinancialAdjustmentType.Credit, 75m, occurredAt, "cash excess correction"));

        Assert.Equal([-125m, 75m], ledger.Select(x => x.Amount));
        Assert.Equal(2, journal.Posted.Count);
        var decrease = journal.Posted[0];
        Assert.Equal(JournalSourceType.FinancialAccountAdjustment, decrease.SourceType);
        Assert.Equal(125m, decrease.Lines.Single(x => x.Account == AccountMappingKey.CashBankAdjustmentSuspense).Debit);
        Assert.Equal(125m, decrease.Lines.Single(x => x.Account == AccountMappingKey.Cash).Credit);
        var increase = journal.Posted[1];
        Assert.Equal(75m, increase.Lines.Single(x => x.Account == AccountMappingKey.Cash).Debit);
        Assert.Equal(75m, increase.Lines.Single(x => x.Account == AccountMappingKey.CashBankAdjustmentSuspense).Credit);
        Assert.All(journal.Posted, entry =>
        {
            Assert.Equal(branch.Id, entry.BranchId);
            Assert.Equal(entry.Lines.Sum(x => x.Debit), entry.Lines.Sum(x => x.Credit));
        });
    }

    private sealed class RecordingJournalPostingService : IJournalPostingService
    {
        public readonly List<JournalPostingRequest> Posted = [];
        public Task PostAsync(JournalPostingRequest request, CancellationToken cancellationToken = default)
        {
            Posted.Add(request);
            return Task.CompletedTask;
        }
    }
}
