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
public sealed class SalesReturnsController(ISalesReturnService returns) : ControllerBase
{
    [HttpGet("api/sales/{saleId:guid}/returnable"), HasPermission(PermissionCatalog.SalesReturnsView)]
    public Task<ReturnableSaleDto> Returnable(Guid saleId, CancellationToken ct) =>
        returns.GetReturnableSaleAsync(UserId(), saleId, ct);

    [HttpPost("api/sales/{saleId:guid}/returns"), HasPermission(PermissionCatalog.SalesReturnsCreate)]
    public async Task<ActionResult<SalesReturnDetailsDto>> Post(Guid saleId, PostSalesReturnRequest request, CancellationToken ct)
    {
        if (request.RefundPayments.Any(x => x.FinancialAccountId is null)) throw new RequestValidationException("Every cash refund must select a financial account.");
        var result = await returns.PostReturnAsync(UserId(), saleId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet("api/sales-returns"), HasPermission(PermissionCatalog.SalesReturnsView)]
    public Task<PagedResult<SalesReturnListItemDto>> List([FromQuery] SalesReturnsQuery query, CancellationToken ct) =>
        returns.ListReturnsAsync(UserId(), query, ct);

    [HttpGet("api/sales-returns/{id:guid}"), HasPermission(PermissionCatalog.SalesReturnsView)]
    public Task<SalesReturnDetailsDto> Get(Guid id, CancellationToken ct) =>
        returns.GetReturnAsync(UserId(), id, ct);

    [HttpGet("api/sales-returns/{id:guid}/receipt"), HasPermission(PermissionCatalog.SalesReturnsView)]
    public Task<SalesReturnReceiptDto> Receipt(Guid id, CancellationToken ct) =>
        returns.ReceiptAsync(UserId(), id, false, ct);

    [HttpPost("api/sales-returns/{id:guid}/reprint-audit"), HasPermission(PermissionCatalog.SalesReturnsReprint)]
    public Task<SalesReturnReceiptDto> Reprint(Guid id, CancellationToken ct) =>
        returns.ReceiptAsync(UserId(), id, true, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
