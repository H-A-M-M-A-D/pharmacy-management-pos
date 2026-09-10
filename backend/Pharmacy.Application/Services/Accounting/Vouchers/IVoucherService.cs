using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.Accounting.Vouchers;

public interface IVoucherService
{
    Task<VoucherDto> CreateDraftAsync(Guid actorId, VoucherCreateRequest request, CancellationToken cancellationToken = default);
    Task<VoucherDto> PostAsync(Guid actorId, Guid voucherId, CancellationToken cancellationToken = default);
    Task<VoucherDto> CancelAsync(Guid actorId, Guid voucherId, CancellationToken cancellationToken = default);
    Task<VoucherDto> ReverseAsync(Guid actorId, Guid voucherId, CancellationToken cancellationToken = default);
    Task<VoucherDto> GetVoucherAsync(Guid actorId, Guid voucherId, CancellationToken cancellationToken = default);
    Task<PagedResult<VoucherListItemDto>> ListVouchersAsync(Guid actorId, VoucherListQuery query, CancellationToken cancellationToken = default);
}
