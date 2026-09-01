using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Catalog;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Catalog;

namespace Pharmacy.Api.Controllers;

[ApiController, Route("api/products"), Authorize]
public sealed class ProductsController(IProductMasterService service) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.ProductsView)]
    public Task<Pharmacy.Application.DTOs.Users.PagedResult<ProductListItemDto>> List([FromQuery] ProductListQuery query, CancellationToken ct) => service.ListProductsAsync(query, ct);
    [HttpGet("options"), HasPermission(PermissionCatalog.ProductsView)]
    public Task<ProductMasterOptionsDto> Options(CancellationToken ct) => service.GetOptionsAsync(ct);
    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.ProductsView)]
    public Task<ProductDetailsDto> Get(Guid id, CancellationToken ct) => service.GetProductAsync(id, ct);
    [HttpPost, HasPermission(PermissionCatalog.ProductsCreate)]
    public async Task<ActionResult<ProductDetailsDto>> Create(ProductRequest request, CancellationToken ct)
    { var result = await service.CreateProductAsync(UserId(), request, ct); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
    [HttpPut("{id:guid}"), HasPermission(PermissionCatalog.ProductsUpdate)]
    public Task<ProductDetailsDto> Update(Guid id, ProductUpdateRequest request, CancellationToken ct) => service.UpdateProductAsync(UserId(), id, request, ct);
    [HttpPost("{id:guid}/activate"), HasPermission(PermissionCatalog.ProductsActivate)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) { await service.SetProductActiveAsync(UserId(), id, true, ct); return NoContent(); }
    [HttpPost("{id:guid}/deactivate"), HasPermission(PermissionCatalog.ProductsDeactivate)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) { await service.SetProductActiveAsync(UserId(), id, false, ct); return NoContent(); }
    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
