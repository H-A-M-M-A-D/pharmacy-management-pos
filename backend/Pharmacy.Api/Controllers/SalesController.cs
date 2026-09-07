using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Sales;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/sales")]
public sealed class SalesController(ISalesService sales) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.SalesView)]
    public Task<PagedResult<SaleListItemDto>> List([FromQuery] SalesHistoryQuery query, CancellationToken ct) =>
        sales.ListSalesAsync(UserId(), query, ct);

    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.SalesView)]
    public Task<SaleDetailsDto> Get(Guid id, CancellationToken ct) =>
        sales.GetSaleAsync(UserId(), id, ct);

    [HttpPost("post"), HasPermission(PermissionCatalog.SalesCreate)]
    public async Task<ActionResult<SaleDetailsDto>> Post(PostSaleRequest request, CancellationToken ct)
    {
        RequirePaymentAccounts(request.Payments);
        var result = await sales.PostSaleAsync(UserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("hold"), HasPermission(PermissionCatalog.SalesHold)]
    public async Task<ActionResult<SaleDetailsDto>> Hold(HoldSaleRequest request, CancellationToken ct)
    {
        var result = await sales.HoldSaleAsync(UserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet("held"), HasPermission(PermissionCatalog.SalesHold)]
    public Task<PagedResult<SaleListItemDto>> Held([FromQuery] HeldSalesQuery query, CancellationToken ct) =>
        sales.ListHeldSalesAsync(UserId(), query, ct);

    [HttpPut("held/{id:guid}"), HasPermission(PermissionCatalog.SalesHold)]
    public Task<SaleDetailsDto> UpdateHeld(Guid id, HoldSaleRequest request, CancellationToken ct) =>
        sales.UpdateHeldSaleAsync(UserId(), id, request, ct);

    [HttpPost("held/{id:guid}/post"), HasPermission(PermissionCatalog.SalesCreate)]
    public Task<SaleDetailsDto> PostHeld(Guid id, PostHeldSaleRequest request, CancellationToken ct)
    {
        RequirePaymentAccounts(request.Payments);
        return sales.PostHeldSaleAsync(UserId(), id, request, ct);
    }

    [HttpPost("held/{id:guid}/cancel"), HasPermission(PermissionCatalog.SalesHold)]
    public async Task<IActionResult> CancelHeld(Guid id, CancellationToken ct)
    {
        await sales.CancelHeldSaleAsync(UserId(), id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/receipt"), HasPermission(PermissionCatalog.SalesView)]
    public Task<ReceiptDto> Receipt(Guid id, CancellationToken ct) =>
        sales.ReceiptAsync(UserId(), id, false, ct);

    [HttpPost("{id:guid}/reprint"), HasPermission(PermissionCatalog.SalesReprint)]
    public Task<ReceiptDto> Reprint(Guid id, CancellationToken ct) =>
        sales.ReceiptAsync(UserId(), id, true, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static void RequirePaymentAccounts(IReadOnlyList<SalePaymentRequest> payments)
    {
        if (payments.Any(x => x.FinancialAccountId is null)) throw new RequestValidationException("Every monetary payment must select a financial account.");
    }
}
