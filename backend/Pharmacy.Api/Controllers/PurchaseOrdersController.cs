using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Purchasing;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Purchasing;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
public sealed class PurchaseOrdersController(IPurchasingService purchasing) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.PurchaseOrdersView)]
    public Task<PagedResult<PurchaseOrderListItemDto>> List([FromQuery] PurchaseOrderListQuery query, CancellationToken ct) =>
        purchasing.ListPurchaseOrdersAsync(UserId(), query, ct);

    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.PurchaseOrdersView)]
    public Task<PurchaseOrderDetailsDto> Get(Guid id, CancellationToken ct) =>
        purchasing.GetPurchaseOrderAsync(UserId(), id, ct);

    [HttpPost, HasPermission(PermissionCatalog.PurchaseOrdersCreate)]
    public async Task<ActionResult<PurchaseOrderDetailsDto>> Create(PurchaseOrderRequest request, CancellationToken ct)
    {
        var result = await purchasing.CreatePurchaseOrderAsync(UserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}"), HasPermission(PermissionCatalog.PurchaseOrdersUpdate)]
    public Task<PurchaseOrderDetailsDto> Update(Guid id, PurchaseOrderRequest request, CancellationToken ct) =>
        purchasing.UpdatePurchaseOrderAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/submit"), HasPermission(PermissionCatalog.PurchaseOrdersUpdate)]
    public Task<PurchaseOrderDetailsDto> Submit(Guid id, CancellationToken ct) =>
        purchasing.SubmitPurchaseOrderAsync(UserId(), id, ct);

    [HttpPost("{id:guid}/cancel"), HasPermission(PermissionCatalog.PurchaseOrdersCancel)]
    public Task<PurchaseOrderDetailsDto> Cancel(Guid id, CancellationToken ct) =>
        purchasing.CancelPurchaseOrderAsync(UserId(), id, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
