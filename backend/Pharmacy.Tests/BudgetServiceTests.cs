using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting.Budgets;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class BudgetServiceTests
{
    [Fact]
    public async Task Set_budget_upserts_and_vs_actual_computes_variance()
    {
        var f = new Fixture(PermissionCatalog.AccountsBudgetsManage, PermissionCatalog.AccountsBudgetsView);
        await f.Service.SetBudgetAsync(f.Actor.Id, new(2026, 1, f.RentAccount.Id, null, 1000, "Rent budget"));
        var updated = await f.Service.SetBudgetAsync(f.Actor.Id, new(2026, 1, f.RentAccount.Id, null, 1200, "Revised"));
        Assert.Equal(1200, updated.BudgetAmount);
        Assert.Single(f.Budgets);

        f.Actuals.Add(new TrialBalanceRowDto(f.RentAccount.Id, f.RentAccount.Code, f.RentAccount.Name, AccountType.Expense, NormalBalance.Debit, 1500, 0));
        var report = await f.Service.GetBudgetVsActualAsync(f.Actor.Id, new(2026, 1, null));
        var row = Assert.Single(report.Rows);
        Assert.Equal(1200, row.BudgetAmount);
        Assert.Equal(1500, row.ActualAmount);
        Assert.Equal(300, row.VarianceAmount);
        Assert.Equal(25, row.VariancePercent);
    }

    private sealed class Fixture : IBudgetRepository
    {
        public readonly List<AccountBudget> Budgets = [];
        public readonly List<TrialBalanceRowDto> Actuals = [];
        public readonly ChartOfAccount RentAccount = new() { Code = "6010", NormalizedCode = "6010", Name = "Rent", AccountType = AccountType.Expense, NormalBalance = NormalBalance.Debit, IsPostingAccount = true, IsActive = true };
        public User Actor { get; }
        public BudgetService Service { get; }

        public Fixture(params string[] permissions)
        {
            var role = new Role { Name = RoleCatalog.Manager };
            foreach (var permission in permissions) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "mgr", NormalizedUsername = "MGR", FullName = "Manager", PasswordHash = "hash", BranchId = Guid.NewGuid(), RoleId = role.Id, Role = role };
            Service = new BudgetService(this);
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ChartOfAccount?>(RentAccount.Id == id ? RentAccount : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(null);

        public Task<IReadOnlyList<AccountBudget>> ListBudgetsAsync(int fiscalYear, int? periodNumber, Guid? branchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountBudget>>(Budgets.Where(x => x.FiscalYear == fiscalYear && x.PeriodNumber == periodNumber && x.BranchId == branchId).ToList());
        public Task<AccountBudget?> GetBudgetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Budgets.FirstOrDefault(x => x.Id == id));
        public Task<AccountBudget?> FindExistingAsync(int fiscalYear, int? periodNumber, Guid chartOfAccountId, Guid? branchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Budgets.FirstOrDefault(x => x.FiscalYear == fiscalYear && x.PeriodNumber == periodNumber && x.ChartOfAccountId == chartOfAccountId && x.BranchId == branchId));
        public Task AddBudgetAsync(AccountBudget budget, CancellationToken cancellationToken = default) { budget.ChartOfAccount = RentAccount; Budgets.Add(budget); return Task.CompletedTask; }

        public Task<IReadOnlyList<TrialBalanceRowDto>> GetActualActivityAsync(DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrialBalanceRowDto>>(Actuals);

        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
