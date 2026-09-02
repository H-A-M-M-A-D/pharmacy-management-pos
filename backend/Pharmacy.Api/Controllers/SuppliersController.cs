using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Suppliers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Suppliers;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
public sealed class SuppliersController(ISupplierService suppliers) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.SuppliersView)]
    public Task<PagedResult<SupplierListItemDto>> List([FromQuery] SupplierListQuery query, CancellationToken ct) =>
        suppliers.ListSuppliersAsync(UserId(), query, ct);

    [HttpGet("lookup"), HasPermission(PermissionCatalog.SuppliersView)]
    public Task<IReadOnlyList<SupplierLookupDto>> Lookup([FromQuery] string? search, [FromQuery] bool activeOnly = true, CancellationToken ct = default) =>
        suppliers.LookupSuppliersAsync(UserId(), search, activeOnly, ct);

    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.SuppliersView)]
    public Task<SupplierDetailsDto> Get(Guid id, CancellationToken ct) =>
        suppliers.GetSupplierAsync(UserId(), id, ct);

    [HttpPost, HasPermission(PermissionCatalog.SuppliersCreate)]
    public async Task<ActionResult<SupplierDetailsDto>> Create(SupplierRequest request, CancellationToken ct)
    {
        var result = await suppliers.CreateSupplierAsync(UserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}"), HasPermission(PermissionCatalog.SuppliersUpdate)]
    public Task<SupplierDetailsDto> Update(Guid id, SupplierUpdateRequest request, CancellationToken ct) =>
        suppliers.UpdateSupplierAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/activate"), HasPermission(PermissionCatalog.SuppliersActivate)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await suppliers.SetSupplierActiveAsync(UserId(), id, true, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate"), HasPermission(PermissionCatalog.SuppliersDeactivate)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await suppliers.SetSupplierActiveAsync(UserId(), id, false, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/ledger"), HasPermission(PermissionCatalog.SuppliersLedgerView)]
    public Task<PagedResult<SupplierLedgerEntryDto>> Ledger(Guid id, [FromQuery] SupplierLedgerQuery query, CancellationToken ct) =>
        suppliers.ListLedgerAsync(UserId(), id, query, ct);

    [HttpPost("{id:guid}/payments"), HasPermission(PermissionCatalog.SuppliersPaymentCreate)]
    public Task<SupplierDetailsDto> Payment(Guid id, SupplierPaymentRequest request, CancellationToken ct) =>
        suppliers.RecordPaymentAsync(UserId(), request with { SupplierId = id }, ct);

    [HttpPost("{id:guid}/adjustments"), HasPermission(PermissionCatalog.SuppliersAdjustBalance)]
    public Task<SupplierDetailsDto> Adjustment(Guid id, SupplierAdjustmentRequest request, CancellationToken ct) =>
        suppliers.AdjustBalanceAsync(UserId(), request with { SupplierId = id }, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
