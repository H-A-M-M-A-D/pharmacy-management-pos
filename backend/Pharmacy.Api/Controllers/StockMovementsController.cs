using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Inventory;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Inventory;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/stock-movements")]
public sealed class StockMovementsController(IInventoryService inventory) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.InventoryMovementsView)]
    public async Task<object> List([FromQuery] StockMovementListQuery query, CancellationToken ct) =>
        await inventory.ListMovementsAsync(UserId(), query, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
