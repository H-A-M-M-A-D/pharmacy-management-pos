using System.Data;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.Budgets;

public interface IBudgetRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountBudget>> ListBudgetsAsync(int fiscalYear, int? periodNumber, Guid? branchId, CancellationToken cancellationToken = default);
    Task<AccountBudget?> GetBudgetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AccountBudget?> FindExistingAsync(int fiscalYear, int? periodNumber, Guid chartOfAccountId, Guid? branchId, CancellationToken cancellationToken = default);
    Task AddBudgetAsync(AccountBudget budget, CancellationToken cancellationToken = default);

    /// <summary>Actual account activity for budget-vs-actual: period-level budgets compare against
    /// that single period's movement, year-level (no period) budgets compare against the full year.</summary>
    Task<IReadOnlyList<TrialBalanceRowDto>> GetActualActivityAsync(DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
