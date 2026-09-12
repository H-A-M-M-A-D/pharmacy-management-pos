using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting.Periods;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/accounts/periods")]
public sealed class AccountingPeriodsController(IAccountingPeriodService periods) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.AccountsPeriodsView)]
    public Task<IReadOnlyList<AccountingPeriodDto>> List([FromQuery] int? fiscalYear, CancellationToken ct) => periods.ListPeriodsAsync(UserId(), fiscalYear, ct);

    [HttpPost, HasPermission(PermissionCatalog.AccountsPeriodsManage)]
    public Task<AccountingPeriodDto> Create(AccountingPeriodRequest request, CancellationToken ct) => periods.CreatePeriodAsync(UserId(), request, ct);

    [HttpPost("{id:guid}/soft-close"), HasPermission(PermissionCatalog.AccountsPeriodsClose)]
    public Task<AccountingPeriodDto> SoftClose(Guid id, CloseAccountingPeriodRequest request, CancellationToken ct) => periods.SoftClosePeriodAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/close"), HasPermission(PermissionCatalog.AccountsPeriodsClose)]
    public Task<AccountingPeriodDto> Close(Guid id, CloseAccountingPeriodRequest request, CancellationToken ct) => periods.ClosePeriodAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/reopen"), HasPermission(PermissionCatalog.AccountsPeriodsReopen)]
    public Task<AccountingPeriodDto> Reopen(Guid id, ReopenAccountingPeriodRequest request, CancellationToken ct) => periods.ReopenPeriodAsync(UserId(), id, request, ct);

    [HttpGet("fiscal-years/{fiscalYear:int}"), HasPermission(PermissionCatalog.AccountsPeriodsView)]
    public Task<FiscalYearCloseDto?> GetFiscalYearClose(int fiscalYear, CancellationToken ct) => periods.GetFiscalYearCloseAsync(UserId(), fiscalYear, ct);

    [HttpPost("fiscal-years/close"), HasPermission(PermissionCatalog.AccountsPeriodsClose)]
    public Task<FiscalYearCloseDto> CloseFiscalYear(CloseFiscalYearRequest request, CancellationToken ct) => periods.CloseFiscalYearAsync(UserId(), request, ct);

    [HttpPost("fiscal-years/{fiscalYear:int}/reopen"), HasPermission(PermissionCatalog.AccountsPeriodsReopen)]
    public Task<FiscalYearCloseDto> ReopenFiscalYear(int fiscalYear, ReopenFiscalYearRequest request, CancellationToken ct) => periods.ReopenFiscalYearAsync(UserId(), fiscalYear, request, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
