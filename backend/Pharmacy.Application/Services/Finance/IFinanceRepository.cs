using System.Data;
using Pharmacy.Application.DTOs.Finance;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Finance;

public interface IFinanceRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<FinancialAccount?> GetAccountAsync(Guid accountId, bool forUpdate = false, CancellationToken cancellationToken = default);
    Task<ExpenseCategory?> GetCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<CostCenter?> GetCostCenterAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> AccountNameExistsAsync(Guid branchId, string normalizedName, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task<bool> CategoryNameExistsAsync(string normalizedName, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task<decimal> GetBalanceAsync(Guid accountId, DateTime? beforeUtc = null, CancellationToken cancellationToken = default);
    Task<string> NextExpenseNumberAsync(DateTime occurredAtUtc, CancellationToken cancellationToken = default);
    Task<string> NextIncomeNumberAsync(DateTime occurredAtUtc, CancellationToken cancellationToken = default);
    Task<string> NextTransferNumberAsync(DateTime occurredAtUtc, CancellationToken cancellationToken = default);
    Task AddAccountAsync(FinancialAccount account, CancellationToken cancellationToken = default);
    Task AddCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default);
    Task AddExpenseAsync(Expense expense, CancellationToken cancellationToken = default);
    Task AddOtherIncomeAsync(OtherIncome income, CancellationToken cancellationToken = default);
    Task AddTransferAsync(FinancialTransfer transfer, CancellationToken cancellationToken = default);
    Task AddLedgerEntryAsync(FinancialLedgerEntry entry, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task<Expense?> GetExpenseEntityAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OtherIncome?> GetOtherIncomeEntityAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialAccountDto>> ListAccountsAsync(Guid? branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(bool? active, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseDto>> ListExpensesAsync(ExpenseQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialLedgerEntryDto>> ListLedgerAsync(Guid accountId, FinancialLedgerQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialLedgerEntry>> ListPositionEntriesAsync(Guid branchId, Guid? accountId, DateTime dayStartUtc, DateTime dayEndUtc, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
