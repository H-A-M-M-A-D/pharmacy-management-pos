using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.StockTransfers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.StockTransfers;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/stock-transfers")]
public sealed class StockTransfersController(IStockTransferService transfers) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.StockTransfersView)]
    public Task<PagedResult<StockTransferListItemDto>> List([FromQuery] StockTransferListQuery query, CancellationToken ct) =>
        transfers.ListTransfersAsync(UserId(), query, ct);

    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.StockTransfersView)]
    public Task<StockTransferDetailsDto> Get(Guid id, CancellationToken ct) => transfers.GetTransferAsync(UserId(), id, ct);

    [HttpGet("transferable-batches"), HasPermission(PermissionCatalog.StockTransfersCreate)]
    public Task<IReadOnlyList<TransferableBatchDto>> TransferableBatches([FromQuery] Guid branchId, [FromQuery] Guid godownId, [FromQuery] Guid? productId, [FromQuery] string? search, CancellationToken ct) =>
        transfers.ListTransferableBatchesAsync(UserId(), branchId, godownId, productId, search, ct);

    [HttpPost, HasPermission(PermissionCatalog.StockTransfersCreate)]
    public async Task<ActionResult<StockTransferDetailsDto>> Create(StockTransferRequest request, CancellationToken ct)
    {
        var result = await transfers.CreateTransferAsync(UserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}"), HasPermission(PermissionCatalog.StockTransfersCreate)]
    public Task<StockTransferDetailsDto> Update(Guid id, StockTransferRequest request, CancellationToken ct) =>
        transfers.UpdateTransferAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/request"), HasPermission(PermissionCatalog.StockTransfersRequest)]
    public Task<StockTransferDetailsDto> RequestTransfer(Guid id, CancellationToken ct) => transfers.RequestTransferAsync(UserId(), id, ct);

    [HttpPost("{id:guid}/approve"), HasPermission(PermissionCatalog.StockTransfersApprove)]
    public Task<StockTransferDetailsDto> Approve(Guid id, ApproveStockTransferRequest request, CancellationToken ct) =>
        transfers.ApproveTransferAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/dispatch"), HasPermission(PermissionCatalog.StockTransfersDispatch)]
    public Task<StockTransferDetailsDto> Dispatch(Guid id, DispatchStockTransferRequest request, CancellationToken ct) =>
        transfers.DispatchTransferAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/receive"), HasPermission(PermissionCatalog.StockTransfersReceive)]
    public Task<StockTransferDetailsDto> Receive(Guid id, ReceiveStockTransferRequest request, CancellationToken ct) =>
        transfers.ReceiveTransferAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/cancel"), HasPermission(PermissionCatalog.StockTransfersCancel)]
    public Task<StockTransferDetailsDto> Cancel(Guid id, CancelStockTransferRequest request, CancellationToken ct) =>
        transfers.CancelTransferAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/resolve-discrepancy"), HasPermission(PermissionCatalog.StockTransfersCancel)]
    public Task<StockTransferDetailsDto> ResolveDiscrepancy(Guid id, ResolveStockTransferDiscrepancyRequest request, CancellationToken ct) =>
        transfers.ResolveDiscrepancyAsync(UserId(), id, request, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
