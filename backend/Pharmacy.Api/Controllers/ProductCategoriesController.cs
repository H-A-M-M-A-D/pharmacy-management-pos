using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Catalog;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Catalog;

namespace Pharmacy.Api.Controllers;

[ApiController, Route("api/product-categories"), Authorize]
public sealed class ProductCategoriesController(IProductMasterService service) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.CategoriesView)] public async Task<object> List(string? search, bool? isActive, CancellationToken ct) => new { items = await service.ListCategoriesAsync(search, isActive, ct) };
    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.CategoriesView)] public Task<CategoryDto> Get(Guid id, CancellationToken ct) => service.GetCategoryAsync(id, ct);
    [HttpPost, HasPermission(PermissionCatalog.CategoriesManage)] public async Task<ActionResult<CategoryDto>> Create(CategoryRequest request, CancellationToken ct) { var result = await service.CreateCategoryAsync(UserId(), request, ct); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
    [HttpPut("{id:guid}"), HasPermission(PermissionCatalog.CategoriesManage)] public Task<CategoryDto> Update(Guid id, CategoryRequest request, CancellationToken ct) => service.UpdateCategoryAsync(UserId(), id, request, ct);
    [HttpPost("{id:guid}/activate"), HasPermission(PermissionCatalog.CategoriesManage)] public async Task<IActionResult> Activate(Guid id, CancellationToken ct) { await service.SetCategoryActiveAsync(UserId(), id, true, ct); return NoContent(); }
    [HttpPost("{id:guid}/deactivate"), HasPermission(PermissionCatalog.CategoriesManage)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) { await service.SetCategoryActiveAsync(UserId(), id, false, ct); return NoContent(); }
    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
