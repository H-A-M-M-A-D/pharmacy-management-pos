using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Quotations;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.SalesOrders;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Quotations;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/sales-quotations")]
public sealed class SalesQuotationsController(ISalesQuotationService quotations) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.QuotationsView)]
    public Task<PagedResult<QuotationListItemDto>> List([FromQuery] QuotationListQuery query, CancellationToken ct) =>
        quotations.ListQuotationsAsync(UserId(), query, ct);

    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.QuotationsView)]
    public Task<QuotationDetailsDto> Get(Guid id, CancellationToken ct) =>
        quotations.GetQuotationAsync(UserId(), id, ct);

    [HttpPost, HasPermission(PermissionCatalog.QuotationsCreate)]
    public async Task<ActionResult<QuotationDetailsDto>> Create(QuotationRequest request, CancellationToken ct)
    {
        var result = await quotations.CreateQuotationAsync(UserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}"), HasPermission(PermissionCatalog.QuotationsUpdate)]
    public Task<QuotationDetailsDto> Update(Guid id, QuotationRequest request, CancellationToken ct) =>
        quotations.UpdateQuotationAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/send"), HasPermission(PermissionCatalog.QuotationsSend)]
    public Task<QuotationDetailsDto> Send(Guid id, CancellationToken ct) =>
        quotations.SendQuotationAsync(UserId(), id, ct);

    [HttpPost("{id:guid}/accept"), HasPermission(PermissionCatalog.QuotationsAccept)]
    public Task<QuotationDetailsDto> Accept(Guid id, CancellationToken ct) =>
        quotations.AcceptQuotationAsync(UserId(), id, ct);

    [HttpPost("{id:guid}/reject"), HasPermission(PermissionCatalog.QuotationsAccept)]
    public Task<QuotationDetailsDto> Reject(Guid id, RejectQuotationRequest request, CancellationToken ct) =>
        quotations.RejectQuotationAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/cancel"), HasPermission(PermissionCatalog.QuotationsCancel)]
    public Task<QuotationDetailsDto> Cancel(Guid id, CancelQuotationRequest request, CancellationToken ct) =>
        quotations.CancelQuotationAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/convert-to-order"), HasPermission(PermissionCatalog.QuotationsConvert)]
    public Task<SalesOrderDetailsDto> ConvertToOrder(Guid id, ConvertQuotationToOrderRequest request, CancellationToken ct) =>
        quotations.ConvertToSalesOrderAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/convert-to-sale"), HasPermission(PermissionCatalog.QuotationsConvert)]
    public Task<SaleDetailsDto> ConvertToSale(Guid id, IReadOnlyList<SalePaymentRequest> payments, CancellationToken ct) =>
        quotations.ConvertToSaleAsync(UserId(), id, payments, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
