using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting.BankReconciliations;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/accounts/bank-reconciliations")]
public sealed class BankReconciliationsController(IBankReconciliationService reconciliations) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.AccountsReconciliationView)]
    public Task<IReadOnlyList<BankReconciliationDto>> List([FromQuery] Guid? financialAccountId, CancellationToken ct) => reconciliations.ListReconciliationsAsync(UserId(), financialAccountId, ct);

    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.AccountsReconciliationView)]
    public Task<BankReconciliationDto> Get(Guid id, CancellationToken ct) => reconciliations.GetReconciliationAsync(UserId(), id, ct);

    [HttpPost, HasPermission(PermissionCatalog.AccountsReconciliationManage)]
    public Task<BankReconciliationDto> Start(StartBankReconciliationRequest request, CancellationToken ct) => reconciliations.StartReconciliationAsync(UserId(), request, ct);

    [HttpPost("{id:guid}/match"), HasPermission(PermissionCatalog.AccountsReconciliationManage)]
    public Task<BankReconciliationDto> Match(Guid id, MatchReconciliationLinesRequest request, CancellationToken ct) => reconciliations.MatchLinesAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/unmatch"), HasPermission(PermissionCatalog.AccountsReconciliationManage)]
    public Task<BankReconciliationDto> Unmatch(Guid id, UnmatchReconciliationLinesRequest request, CancellationToken ct) => reconciliations.UnmatchLinesAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/finalize"), HasPermission(PermissionCatalog.AccountsReconciliationManage)]
    public Task<BankReconciliationDto> Finalize(Guid id, FinalizeReconciliationRequest request, CancellationToken ct) => reconciliations.FinalizeReconciliationAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/reopen"), HasPermission(PermissionCatalog.AccountsReconciliationManage)]
    public Task<BankReconciliationDto> Reopen(Guid id, ReopenReconciliationRequest request, CancellationToken ct) => reconciliations.ReopenReconciliationAsync(UserId(), id, request, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
