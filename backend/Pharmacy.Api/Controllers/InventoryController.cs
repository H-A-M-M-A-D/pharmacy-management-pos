using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Inventory;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Inventory;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController(IInventoryService inventory) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.InventoryView)]
    public Task<Pharmacy.Application.DTOs.Users.PagedResult<InventoryListItemDto>> List([FromQuery] InventoryListQuery query, CancellationToken ct) =>
        inventory.ListInventoryAsync(UserId(), query, ct);

    [HttpGet("options"), HasPermission(PermissionCatalog.InventoryView)]
    public Task<InventoryOptionsDto> Options([FromQuery] string? productSearch, CancellationToken ct) =>
        inventory.GetOptionsAsync(UserId(), productSearch, ct);

    [HttpGet("{branchId:guid}/{productId:guid}"), HasPermission(PermissionCatalog.InventoryView)]
    public Task<InventoryDetailsDto> Details(Guid branchId, Guid productId, CancellationToken ct) =>
        inventory.GetInventoryDetailsAsync(UserId(), branchId, productId, ct);

    [HttpPost("opening-stock"), HasPermission(PermissionCatalog.InventoryOpeningStock)]
    public Task<InventoryDetailsDto> OpeningStock(OpeningStockRequest request, CancellationToken ct) =>
        inventory.AddOpeningStockAsync(UserId(), request, ct);

    [HttpPost("adjust/increase"), HasPermission(PermissionCatalog.InventoryAdjust)]
    public Task<InventoryDetailsDto> Increase(StockAdjustmentRequest request, CancellationToken ct) =>
        inventory.AdjustStockIncreaseAsync(UserId(), request, ct);

    [HttpPost("adjust/decrease"), HasPermission(PermissionCatalog.InventoryAdjust)]
    public Task<InventoryDetailsDto> Decrease(StockAdjustmentRequest request, CancellationToken ct) =>
        inventory.AdjustStockDecreaseAsync(UserId(), request, ct);

    [HttpPost("stock-count"), HasPermission(PermissionCatalog.InventoryStockCount)]
    public Task<InventoryDetailsDto> StockCount(StockCountRequest request, CancellationToken ct) =>
        inventory.ReconcileStockCountAsync(UserId(), request, ct);

    [HttpPost("dispose-expired"), HasPermission(PermissionCatalog.InventoryExpiryManage)]
    public Task<InventoryDetailsDto> DisposeExpired(DisposeExpiredStockRequest request, CancellationToken ct) =>
        inventory.DisposeExpiredStockAsync(UserId(), request, ct);

    [HttpGet("expiry"), HasPermission(PermissionCatalog.InventoryView)]
    public Task<IReadOnlyList<ExpiryListItemDto>> Expiry([FromQuery] ExpiryQuery query, CancellationToken ct) =>
        inventory.ListExpiryAsync(UserId(), query, ct);

    [HttpGet("fefo-preview"), HasPermission(PermissionCatalog.InventoryView)]
    public Task<FefoPreviewDto> FefoPreview([FromQuery] FefoPreviewRequest request, CancellationToken ct) =>
        inventory.PreviewFefoAsync(UserId(), request, ct);

    [HttpGet("integrity"), HasPermission(PermissionCatalog.InventoryView)]
    public Task<IReadOnlyList<InventoryIntegrityIssueDto>> Integrity([FromQuery] Guid? branchId, [FromQuery] Guid? productId, CancellationToken ct) =>
        inventory.CheckIntegrityAsync(UserId(), branchId, productId, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
