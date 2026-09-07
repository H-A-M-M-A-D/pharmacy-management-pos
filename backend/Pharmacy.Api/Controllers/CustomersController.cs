using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Customers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Customers;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.CustomersView)]
    public Task<PagedResult<CustomerListItemDto>> List([FromQuery] CustomerListQuery query, CancellationToken ct) =>
        customers.ListCustomersAsync(UserId(), query, ct);

    [HttpGet("lookup"), HasPermission(PermissionCatalog.CustomersView)]
    public Task<IReadOnlyList<CustomerLookupDto>> Lookup([FromQuery] string? search, [FromQuery] bool activeOnly, [FromQuery] Guid? branchId, CancellationToken ct) =>
        customers.LookupCustomersAsync(UserId(), search, activeOnly, branchId, ct);

    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.CustomersView)]
    public Task<CustomerDetailsDto> Get(Guid id, CancellationToken ct) =>
        customers.GetCustomerAsync(UserId(), id, ct);

    [HttpPost, HasPermission(PermissionCatalog.CustomersCreate)]
    public async Task<ActionResult<CustomerDetailsDto>> Create(CustomerRequest request, CancellationToken ct)
    {
        var result = await customers.CreateCustomerAsync(UserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}"), HasPermission(PermissionCatalog.CustomersUpdate)]
    public Task<CustomerDetailsDto> Update(Guid id, CustomerUpdateRequest request, CancellationToken ct) =>
        customers.UpdateCustomerAsync(UserId(), id, request, ct);

    [HttpPost("{id:guid}/activate"), HasPermission(PermissionCatalog.CustomersActivate)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await customers.SetCustomerActiveAsync(UserId(), id, true, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate"), HasPermission(PermissionCatalog.CustomersDeactivate)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await customers.SetCustomerActiveAsync(UserId(), id, false, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/ledger"), HasPermission(PermissionCatalog.CustomersLedgerView)]
    public Task<PagedResult<CustomerLedgerEntryDto>> Ledger(Guid id, [FromQuery] CustomerLedgerQuery query, CancellationToken ct) =>
        customers.ListLedgerAsync(UserId(), id, query, ct);

    [HttpPost("{id:guid}/payments"), HasPermission(PermissionCatalog.CustomersPaymentCreate)]
    public Task<CustomerDetailsDto> Payment(Guid id, CustomerPaymentRequest request, CancellationToken ct)
    {
        if (request.FinancialAccountId is null) throw new RequestValidationException("A receiving financial account is required.");
        return customers.RecordPaymentAsync(UserId(), request with { CustomerId = id }, ct);
    }

    [HttpPost("{id:guid}/adjustments"), HasPermission(PermissionCatalog.CustomersAdjustBalance)]
    public Task<CustomerDetailsDto> Adjustment(Guid id, CustomerAdjustmentRequest request, CancellationToken ct) =>
        customers.AdjustBalanceAsync(UserId(), request with { CustomerId = id }, ct);

    [HttpGet("payments/{paymentId:guid}/receipt"), HasPermission(PermissionCatalog.CustomersLedgerView)]
    public Task<CustomerPaymentReceiptDto> PaymentReceipt(Guid paymentId, CancellationToken ct) =>
        customers.PaymentReceiptAsync(UserId(), paymentId, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
