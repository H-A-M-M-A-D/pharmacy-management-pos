using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Purchasing;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Purchasing;

namespace Pharmacy.Api.Controllers;

[ApiController]
public sealed class PurchaseReturnsController(IPurchasingService purchasing) : ControllerBase
{
    [HttpGet("api/goods-receipts/{goodsReceiptId:guid}/returnable"), HasPermission(PermissionCatalog.PurchaseReturnsView)]
    public Task<ReturnableGoodsReceiptDto> Returnable(Guid goodsReceiptId, CancellationToken ct) =>
        purchasing.GetReturnableGoodsReceiptAsync(UserId(), goodsReceiptId, ct);

    [HttpPost("api/goods-receipts/{goodsReceiptId:guid}/purchase-returns"), HasPermission(PermissionCatalog.PurchaseReturnsCreate)]
    public async Task<ActionResult<PurchaseReturnDetailsDto>> Post(Guid goodsReceiptId, PostPurchaseReturnRequest request, CancellationToken ct)
    {
        var result = await purchasing.PostPurchaseReturnAsync(UserId(), goodsReceiptId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet("api/purchase-returns"), HasPermission(PermissionCatalog.PurchaseReturnsView)]
    public Task<PagedResult<PurchaseReturnListItemDto>> List([FromQuery] PurchaseReturnListQuery query, CancellationToken ct) =>
        purchasing.ListPurchaseReturnsAsync(UserId(), query, ct);

    [HttpGet("api/purchase-returns/{id:guid}"), HasPermission(PermissionCatalog.PurchaseReturnsView)]
    public Task<PurchaseReturnDetailsDto> Get(Guid id, CancellationToken ct) =>
        purchasing.GetPurchaseReturnAsync(UserId(), id, ct);

    [HttpGet("api/purchase-returns/{id:guid}/note"), HasPermission(PermissionCatalog.PurchaseReturnsView)]
    public Task<PurchaseReturnDetailsDto> Note(Guid id, CancellationToken ct) =>
        purchasing.GetPurchaseReturnAsync(UserId(), id, ct);

    [HttpPost("api/purchase-returns/{id:guid}/reprint-audit"), HasPermission(PermissionCatalog.PurchaseReturnsReprint)]
    public Task<PurchaseReturnDetailsDto> Reprint(Guid id, CancellationToken ct) =>
        purchasing.ReprintPurchaseReturnAsync(UserId(), id, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
