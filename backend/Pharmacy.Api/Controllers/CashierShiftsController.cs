using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.CashierShifts;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.CashierShifts;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/cashier-shifts")]
public sealed class CashierShiftsController(ICashierShiftService shifts) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.CashierShiftView)]
    public Task<PagedResult<CashierShiftListItemDto>> List([FromQuery] CashierShiftListQuery query, CancellationToken ct) =>
        shifts.ListShiftsAsync(UserId(), query, ct);

    [HttpGet("my-open"), HasPermission(PermissionCatalog.CashierShiftView)]
    public Task<CashierShiftDto?> MyOpen(CancellationToken ct) => shifts.GetMyOpenShiftAsync(UserId(), ct);

    [HttpGet("daily-summary"), HasPermission(PermissionCatalog.CashierShiftView)]
    public Task<DailyClosingSummaryDto> DailySummary([FromQuery] Guid branchId, [FromQuery] DateOnly date, CancellationToken ct) =>
        shifts.GetDailyClosingSummaryAsync(UserId(), branchId, date, ct);

    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.CashierShiftView)]
    public Task<CashierShiftDto> Details(Guid id, CancellationToken ct) => shifts.GetShiftAsync(UserId(), id, ct);

    [HttpPost, HasPermission(PermissionCatalog.CashierShiftOpen)]
    public Task<CashierShiftDto> Open(OpenCashierShiftRequest request, CancellationToken ct) =>
        shifts.OpenShiftAsync(UserId(), request, ct);

    [HttpPost("{id:guid}/drawer-entries"), HasPermission(PermissionCatalog.CashierShiftDrawerAdjust)]
    public Task<CashierShiftDto> AddDrawerEntry(Guid id, AddDrawerEntryRequest request, CancellationToken ct) =>
        shifts.AddDrawerEntryAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/close"), HasPermission(PermissionCatalog.CashierShiftClose)]
    public Task<CashierShiftDto> Close(Guid id, CloseCashierShiftRequest request, CancellationToken ct) =>
        shifts.CloseShiftAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/reconcile"), HasPermission(PermissionCatalog.CashierShiftReconcile)]
    public Task<CashierShiftDto> Reconcile(Guid id, ReconcileCashierShiftRequest request, CancellationToken ct) =>
        shifts.ReconcileShiftAsync(UserId(), id, request, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
