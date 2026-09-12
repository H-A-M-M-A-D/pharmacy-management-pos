using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Finance;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Finance;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class FinanceController(IFinanceService finance) : ControllerBase
{
    [HttpGet("financial-accounts"), HasPermission(PermissionCatalog.AccountsView)]
    public Task<IReadOnlyList<FinancialAccountDto>> Accounts([FromQuery] Guid? branchId, CancellationToken ct) => finance.ListAccountsAsync(UserId(), branchId, ct);
    [HttpGet("financial-accounts/{id:guid}"), HasPermission(PermissionCatalog.AccountsView)]
    public Task<FinancialAccountDto> Account(Guid id, CancellationToken ct) => finance.GetAccountAsync(UserId(), id, ct);
    [HttpPost("financial-accounts"), HasPermission(PermissionCatalog.AccountsManage)]
    public async Task<ActionResult<FinancialAccountDto>> CreateAccount(FinancialAccountRequest request, CancellationToken ct)
    {
        var result = await finance.CreateAccountAsync(UserId(), request, ct);
        return CreatedAtAction(nameof(Account), new { id = result.Id }, result);
    }
    [HttpPut("financial-accounts/{id:guid}"), HasPermission(PermissionCatalog.AccountsManage)]
    public Task<FinancialAccountDto> UpdateAccount(Guid id, FinancialAccountUpdateRequest request, CancellationToken ct) => finance.UpdateAccountAsync(UserId(), id, request, ct);
    [HttpPost("financial-accounts/{id:guid}/activate"), HasPermission(PermissionCatalog.AccountsManage)]
    public async Task<IActionResult> ActivateAccount(Guid id, CancellationToken ct) { await finance.SetAccountActiveAsync(UserId(), id, true, ct); return NoContent(); }
    [HttpPost("financial-accounts/{id:guid}/deactivate"), HasPermission(PermissionCatalog.AccountsManage)]
    public async Task<IActionResult> DeactivateAccount(Guid id, CancellationToken ct) { await finance.SetAccountActiveAsync(UserId(), id, false, ct); return NoContent(); }
    [HttpGet("financial-accounts/{id:guid}/ledger"), HasPermission(PermissionCatalog.FinanceLedgerView)]
    public Task<IReadOnlyList<FinancialLedgerEntryDto>> Ledger(Guid id, [FromQuery] FinancialLedgerQuery query, CancellationToken ct) => finance.LedgerAsync(UserId(), id, query, ct);

    [HttpGet("expense-categories")]
    public Task<IReadOnlyList<ExpenseCategoryDto>> Categories([FromQuery] bool? active, CancellationToken ct) => finance.ListCategoriesAsync(UserId(), active, ct);
    [HttpPost("expense-categories"), HasPermission(PermissionCatalog.AccountsManage)]
    public Task<ExpenseCategoryDto> CreateCategory(ExpenseCategoryRequest request, CancellationToken ct) => finance.CreateCategoryAsync(UserId(), request, ct);
    [HttpPut("expense-categories/{id:guid}"), HasPermission(PermissionCatalog.AccountsManage)]
    public Task<ExpenseCategoryDto> UpdateCategory(Guid id, ExpenseCategoryRequest request, CancellationToken ct) => finance.UpdateCategoryAsync(UserId(), id, request, ct);
    [HttpPost("expense-categories/{id:guid}/activate"), HasPermission(PermissionCatalog.AccountsManage)]
    public async Task<IActionResult> ActivateCategory(Guid id, CancellationToken ct) { await finance.SetCategoryActiveAsync(UserId(), id, true, ct); return NoContent(); }
    [HttpPost("expense-categories/{id:guid}/deactivate"), HasPermission(PermissionCatalog.AccountsManage)]
    public async Task<IActionResult> DeactivateCategory(Guid id, CancellationToken ct) { await finance.SetCategoryActiveAsync(UserId(), id, false, ct); return NoContent(); }

    [HttpGet("expenses"), HasPermission(PermissionCatalog.ExpensesView)]
    public Task<IReadOnlyList<ExpenseDto>> Expenses([FromQuery] ExpenseQuery query, CancellationToken ct) => finance.ListExpensesAsync(UserId(), query, ct);
    [HttpPost("expenses"), HasPermission(PermissionCatalog.ExpensesCreate)]
    public async Task<ActionResult<ExpenseDto>> PostExpense(PostExpenseRequest request, CancellationToken ct) => Created("/api/expenses", await finance.PostExpenseAsync(UserId(), request, ct));
    [HttpPost("expenses/{id:guid}/reverse"), HasPermission(PermissionCatalog.ExpensesPost)]
    public Task<ExpenseDto> ReverseExpense(Guid id, ReverseExpenseRequest request, CancellationToken ct) => finance.ReverseExpenseAsync(UserId(), id, request, ct);
    [HttpPost("finance/other-income"), HasPermission(PermissionCatalog.FinanceIncomeCreate)]
    public async Task<ActionResult<OtherIncomeDto>> OtherIncome(PostOtherIncomeRequest request, CancellationToken ct) => Created("/api/finance/other-income", await finance.PostOtherIncomeAsync(UserId(), request, ct));
    [HttpPost("finance/other-income/{id:guid}/reverse"), HasPermission(PermissionCatalog.FinanceIncomeCreate)]
    public Task<OtherIncomeDto> ReverseOtherIncome(Guid id, ReverseOtherIncomeRequest request, CancellationToken ct) => finance.ReverseOtherIncomeAsync(UserId(), id, request, ct);
    [HttpPost("finance/transfers"), HasPermission(PermissionCatalog.FinanceTransfer)]
    public async Task<ActionResult<FinancialTransferDto>> Transfer(PostTransferRequest request, CancellationToken ct) => Created("/api/finance/transfers", await finance.PostTransferAsync(UserId(), request, ct));
    [HttpPost("finance/adjustments"), HasPermission(PermissionCatalog.FinanceAdjust)]
    public async Task<IActionResult> Adjustment(PostFinancialAdjustmentRequest request, CancellationToken ct) { await finance.PostAdjustmentAsync(UserId(), request, ct); return NoContent(); }
    [HttpGet("finance/cash-position"), HasPermission(PermissionCatalog.FinanceLedgerView)]
    public Task<DailyCashPositionDto> CashPosition([FromQuery] Guid branchId, [FromQuery] DateOnly date, [FromQuery] Guid? accountId, CancellationToken ct) => finance.DailyPositionAsync(UserId(), branchId, date, accountId, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
