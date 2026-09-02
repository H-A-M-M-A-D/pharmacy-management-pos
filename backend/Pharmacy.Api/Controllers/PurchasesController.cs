using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Purchasing;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Purchasing;

namespace Pharmacy.Api.Controllers;

[ApiController]
public sealed class PurchasesController(IPurchasingService purchasing) : ControllerBase
{
    [HttpGet("api/purchases"), HasPermission(PermissionCatalog.PurchasesView)]
    public Task<PagedResult<PurchaseHistoryItemDto>> List([FromQuery] PurchaseHistoryQuery query, CancellationToken ct) =>
        purchasing.ListPurchasesAsync(UserId(), query, ct);

    [HttpGet("api/purchases/{id:guid}"), HasPermission(PermissionCatalog.PurchasesView)]
    public Task<GoodsReceiptDetailsDto> Get(Guid id, CancellationToken ct) =>
        purchasing.GetGoodsReceiptAsync(UserId(), id, ct);

    [HttpPost("api/goods-receipts"), HasPermission(PermissionCatalog.PurchasesReceive)]
    public async Task<ActionResult<GoodsReceiptDetailsDto>> PostGoodsReceipt(GoodsReceiptRequest request, CancellationToken ct)
    {
        var result = await purchasing.PostGoodsReceiptAsync(UserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("api/purchases/direct"), HasPermission(PermissionCatalog.PurchasesReceive), HasPermission(PermissionCatalog.PurchasesCreate)]
    public async Task<ActionResult<GoodsReceiptDetailsDto>> DirectPurchase(GoodsReceiptRequest request, CancellationToken ct)
    {
        var result = await purchasing.PostGoodsReceiptAsync(UserId(), request with { PurchaseOrderId = null }, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet("api/purchasing/options"), HasPermission(PermissionCatalog.PurchasesView)]
    public Task<PurchasingOptionsDto> Options([FromQuery] string? productSearch, CancellationToken ct) =>
        purchasing.GetOptionsAsync(UserId(), productSearch, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
