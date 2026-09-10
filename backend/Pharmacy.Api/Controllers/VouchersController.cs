using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting.Vouchers;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/accounts/vouchers")]
public sealed class VouchersController(IVoucherService vouchers) : ControllerBase
{
    [HttpGet, HasPermission(PermissionCatalog.AccountsVoucherView)]
    public Task<PagedResult<VoucherListItemDto>> List([FromQuery] VoucherListQuery query, CancellationToken ct) =>
        vouchers.ListVouchersAsync(UserId(), query, ct);

    [HttpGet("{id:guid}"), HasPermission(PermissionCatalog.AccountsVoucherView)]
    public Task<VoucherDto> Get(Guid id, CancellationToken ct) => vouchers.GetVoucherAsync(UserId(), id, ct);

    [HttpPost, HasPermission(PermissionCatalog.AccountsVoucherCreate)]
    public Task<VoucherDto> CreateDraft(VoucherCreateRequest request, CancellationToken ct) =>
        vouchers.CreateDraftAsync(UserId(), request, ct);

    [HttpPost("{id:guid}/post"), HasPermission(PermissionCatalog.AccountsVoucherPost)]
    public Task<VoucherDto> Post(Guid id, CancellationToken ct) => vouchers.PostAsync(UserId(), id, ct);

    [HttpPost("{id:guid}/cancel"), HasPermission(PermissionCatalog.AccountsVoucherCreate)]
    public Task<VoucherDto> Cancel(Guid id, CancellationToken ct) => vouchers.CancelAsync(UserId(), id, ct);

    [HttpPost("{id:guid}/reverse"), HasPermission(PermissionCatalog.AccountsVoucherReverse)]
    public Task<VoucherDto> Reverse(Guid id, CancellationToken ct) => vouchers.ReverseAsync(UserId(), id, ct);

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
