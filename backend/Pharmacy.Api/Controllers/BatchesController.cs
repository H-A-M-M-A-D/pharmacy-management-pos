using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Inventory;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Inventory;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/batches")]
public sealed class BatchesController(IInventoryService inventory) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.BatchesView)]
    public async Task<object> List([FromQuery] BatchListQuery query, CancellationToken ct) =>
        await inventory.ListBatchesAsync(UserId(), query, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
