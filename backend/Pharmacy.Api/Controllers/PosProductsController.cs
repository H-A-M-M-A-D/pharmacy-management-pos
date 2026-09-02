using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Sales;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/pos/products")]
public sealed class PosProductsController(ISalesService sales) : ControllerBase
{
    [HttpGet("search"), HasPermission(PermissionCatalog.SalesCreate)]
    public Task<IReadOnlyList<PosProductDto>> Search([FromQuery] PosProductSearchQuery query, CancellationToken ct) =>
        sales.SearchProductsAsync(UserId(), query, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
