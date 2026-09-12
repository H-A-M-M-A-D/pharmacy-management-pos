using Pharmacy.Application.DTOs.Finance;

namespace Pharmacy.Application.Services.Finance;

public interface IFinanceService
{
    Task<IReadOnlyList<FinancialAccountDto>> ListAccountsAsync(Guid actorId, Guid? branchId, CancellationToken cancellationToken = default);
    Task<FinancialAccountDto> GetAccountAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<FinancialAccountDto> CreateAccountAsync(Guid actorId, FinancialAccountRequest request, CancellationToken cancellationToken = default);
    Task<FinancialAccountDto> UpdateAccountAsync(Guid actorId, Guid id, FinancialAccountUpdateRequest request, CancellationToken cancellationToken = default);
    Task SetAccountActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialLedgerEntryDto>> LedgerAsync(Guid actorId, Guid accountId, FinancialLedgerQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(Guid actorId, bool? active, CancellationToken cancellationToken = default);
    Task<ExpenseCategoryDto> CreateCategoryAsync(Guid actorId, ExpenseCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ExpenseCategoryDto> UpdateCategoryAsync(Guid actorId, Guid id, ExpenseCategoryRequest request, CancellationToken cancellationToken = default);
    Task SetCategoryActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseDto>> ListExpensesAsync(Guid actorId, ExpenseQuery query, CancellationToken cancellationToken = default);
    Task<ExpenseDto> PostExpenseAsync(Guid actorId, PostExpenseRequest request, CancellationToken cancellationToken = default);
    Task<ExpenseDto> ReverseExpenseAsync(Guid actorId, Guid id, ReverseExpenseRequest request, CancellationToken cancellationToken = default);
    Task<OtherIncomeDto> PostOtherIncomeAsync(Guid actorId, PostOtherIncomeRequest request, CancellationToken cancellationToken = default);
    Task<OtherIncomeDto> ReverseOtherIncomeAsync(Guid actorId, Guid id, ReverseOtherIncomeRequest request, CancellationToken cancellationToken = default);
    Task<FinancialTransferDto> PostTransferAsync(Guid actorId, PostTransferRequest request, CancellationToken cancellationToken = default);
    Task PostAdjustmentAsync(Guid actorId, PostFinancialAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<DailyCashPositionDto> DailyPositionAsync(Guid actorId, Guid branchId, DateOnly date, Guid? accountId, CancellationToken cancellationToken = default);
}
