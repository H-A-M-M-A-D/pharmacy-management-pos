using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.SalesOrders;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.SalesOrders;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/sales-orders")]
public sealed class SalesOrdersController(ISalesOrderService orders) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.SalesOrdersView)]
    public Task<PagedResult<SalesOrderListItemDto>> List([FromQuery] SalesOrderListQuery query, CancellationToken ct) =>
        orders.ListOrdersAsync(UserId(), query, ct);

    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.SalesOrdersView)]
    public Task<SalesOrderDetailsDto> Get(Guid id, CancellationToken ct) =>
        orders.GetOrderAsync(UserId(), id, ct);

    [HttpPost, HasPermission(PermissionCatalog.SalesOrdersCreate)]
    public async Task<ActionResult<SalesOrderDetailsDto>> Create(SalesOrderRequest request, CancellationToken ct)
    {
        var result = await orders.CreateOrderAsync(UserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}"), HasPermission(PermissionCatalog.SalesOrdersUpdate)]
    public Task<SalesOrderDetailsDto> Update(Guid id, SalesOrderRequest request, CancellationToken ct) =>
        orders.UpdateOrderAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/confirm"), HasPermission(PermissionCatalog.SalesOrdersConfirm)]
    public Task<SalesOrderDetailsDto> Confirm(Guid id, CancellationToken ct) =>
        orders.ConfirmOrderAsync(UserId(), id, ct);

    [HttpPost("{id:guid}/cancel"), HasPermission(PermissionCatalog.SalesOrdersCancel)]
    public Task<SalesOrderDetailsDto> Cancel(Guid id, CancelSalesOrderRequest request, CancellationToken ct) =>
        orders.CancelOrderAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/fulfill"), HasPermission(PermissionCatalog.SalesOrdersFulfill)]
    public Task<SaleDetailsDto> Fulfill(Guid id, FulfillSalesOrderRequest request, CancellationToken ct) =>
        orders.FulfillOrderAsync(UserId(), id, request, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
