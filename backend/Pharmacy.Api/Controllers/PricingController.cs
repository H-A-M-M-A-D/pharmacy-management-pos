using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Pricing;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Pricing;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/price-levels")]
public sealed class PricingController(IPricingService pricing) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.PricingView)]
    public Task<IReadOnlyList<PriceLevelDto>> ListLevels([FromQuery] bool activeOnly = true, CancellationToken ct = default) =>
        pricing.ListPriceLevelsAsync(UserId(), activeOnly, ct);

    [HttpPost, HasPermission(PermissionCatalog.PricingManage)]
    public Task<PriceLevelDto> CreateLevel(PriceLevelRequest request, CancellationToken ct) =>
        pricing.CreatePriceLevelAsync(UserId(), request, ct);

    [HttpPut("{id:guid}"), HasPermission(PermissionCatalog.PricingManage)]
    public Task<PriceLevelDto> UpdateLevel(Guid id, PriceLevelRequest request, CancellationToken ct) =>
        pricing.UpdatePriceLevelAsync(UserId(), id, request, ct);

    [HttpGet("product-prices"), HasPermission(PermissionCatalog.PricingView)]
    public Task<IReadOnlyList<ProductPriceLevelDto>> ListProductPrices([FromQuery] Guid? productId, [FromQuery] Guid? priceLevelId, CancellationToken ct) =>
        pricing.ListProductPricesAsync(UserId(), productId, priceLevelId, ct);

    [HttpPost("product-prices"), HasPermission(PermissionCatalog.PricingManage)]
    public Task<ProductPriceLevelDto> SetProductPrice(ProductPriceLevelRequest request, CancellationToken ct) =>
        pricing.SetProductPriceAsync(UserId(), request, ct);

    [HttpDelete("product-prices/{id:guid}"), HasPermission(PermissionCatalog.PricingManage)]
    public async Task<IActionResult> RemoveProductPrice(Guid id, CancellationToken ct)
    {
        await pricing.RemoveProductPriceAsync(UserId(), id, ct);
        return NoContent();
    }

    [HttpGet("product-breaks"), HasPermission(PermissionCatalog.PricingView)]
    public Task<IReadOnlyList<ProductPriceBreakDto>> ListProductPriceBreaks([FromQuery] Guid? productId, CancellationToken ct) =>
        pricing.ListProductPriceBreaksAsync(UserId(), productId, ct);

    [HttpPost("product-breaks"), HasPermission(PermissionCatalog.PricingManage)]
    public Task<ProductPriceBreakDto> SetProductPriceBreak(ProductPriceBreakRequest request, CancellationToken ct) =>
        pricing.SetProductPriceBreakAsync(UserId(), request, ct);

    [HttpDelete("product-breaks/{id:guid}"), HasPermission(PermissionCatalog.PricingManage)]
    public async Task<IActionResult> RemoveProductPriceBreak(Guid id, CancellationToken ct)
    {
        await pricing.RemoveProductPriceBreakAsync(UserId(), id, ct);
        return NoContent();
    }

    [HttpGet("resolve"), HasPermission(PermissionCatalog.PricingView)]
    public Task<ResolvedPriceDto> Resolve([FromQuery] Guid? customerId, [FromQuery] Guid productId, [FromQuery] int quantity, CancellationToken ct) =>
        pricing.PreviewResolvedPriceAsync(UserId(), customerId, productId, quantity, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
