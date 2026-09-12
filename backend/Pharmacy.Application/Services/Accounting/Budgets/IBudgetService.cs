using Pharmacy.Application.DTOs.Accounting;

namespace Pharmacy.Application.Services.Accounting.Budgets;

public interface IBudgetService
{
    Task<IReadOnlyList<AccountBudgetDto>> ListBudgetsAsync(Guid actorId, int fiscalYear, int? periodNumber, Guid? branchId, CancellationToken cancellationToken = default);
    Task<AccountBudgetDto> SetBudgetAsync(Guid actorId, AccountBudgetRequest request, CancellationToken cancellationToken = default);
    Task<BudgetVsActualDto> GetBudgetVsActualAsync(Guid actorId, BudgetVsActualQuery query, CancellationToken cancellationToken = default);
}
