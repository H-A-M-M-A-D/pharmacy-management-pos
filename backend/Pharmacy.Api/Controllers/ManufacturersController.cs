using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Catalog;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Catalog;

namespace Pharmacy.Api.Controllers;

[ApiController, Route("api/manufacturers"), Authorize]
public sealed class ManufacturersController(IProductMasterService service) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.ManufacturersView)] public async Task<object> List(string? search, bool? isActive, CancellationToken ct) => new { items = await service.ListManufacturersAsync(search, isActive, ct) };
    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.ManufacturersView)] public Task<ManufacturerDto> Get(Guid id, CancellationToken ct) => service.GetManufacturerAsync(id, ct);
    [HttpPost, HasPermission(PermissionCatalog.ManufacturersManage)] public async Task<ActionResult<ManufacturerDto>> Create(ManufacturerRequest request, CancellationToken ct) { var result = await service.CreateManufacturerAsync(UserId(), request, ct); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
    [HttpPut("{id:guid}"), HasPermission(PermissionCatalog.ManufacturersManage)] public Task<ManufacturerDto> Update(Guid id, ManufacturerRequest request, CancellationToken ct) => service.UpdateManufacturerAsync(UserId(), id, request, ct);
    [HttpPost("{id:guid}/activate"), HasPermission(PermissionCatalog.ManufacturersManage)] public async Task<IActionResult> Activate(Guid id, CancellationToken ct) { await service.SetManufacturerActiveAsync(UserId(), id, true, ct); return NoContent(); }
    [HttpPost("{id:guid}/deactivate"), HasPermission(PermissionCatalog.ManufacturersManage)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) { await service.SetManufacturerActiveAsync(UserId(), id, false, ct); return NoContent(); }
    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
