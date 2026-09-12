using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting.Budgets;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/accounts/budgets")]
public sealed class BudgetsController(IBudgetService budgets) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.AccountsBudgetsView)]
    public Task<IReadOnlyList<AccountBudgetDto>> List([FromQuery] int fiscalYear, [FromQuery] int? periodNumber, [FromQuery] Guid? branchId, CancellationToken ct) =>
        budgets.ListBudgetsAsync(UserId(), fiscalYear, periodNumber, branchId, ct);

    [HttpPost, HasPermission(PermissionCatalog.AccountsBudgetsManage)]
    public Task<AccountBudgetDto> Set(AccountBudgetRequest request, CancellationToken ct) => budgets.SetBudgetAsync(UserId(), request, ct);

    [HttpGet("vs-actual"), HasPermission(PermissionCatalog.AccountsBudgetsView)]
    public Task<BudgetVsActualDto> VsActual([FromQuery] BudgetVsActualQuery query, CancellationToken ct) => budgets.GetBudgetVsActualAsync(UserId(), query, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
