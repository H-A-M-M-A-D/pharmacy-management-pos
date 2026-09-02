using Pharmacy.Application.DTOs.Suppliers;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.Suppliers;

public interface ISupplierService
{
    Task<PagedResult<SupplierListItemDto>> ListSuppliersAsync(Guid actorId, SupplierListQuery query, CancellationToken cancellationToken = default);
    Task<SupplierDetailsDto> GetSupplierAsync(Guid actorId, Guid supplierId, CancellationToken cancellationToken = default);
    Task<SupplierDetailsDto> CreateSupplierAsync(Guid actorId, SupplierRequest request, CancellationToken cancellationToken = default);
    Task<SupplierDetailsDto> UpdateSupplierAsync(Guid actorId, Guid supplierId, SupplierUpdateRequest request, CancellationToken cancellationToken = default);
    Task SetSupplierActiveAsync(Guid actorId, Guid supplierId, bool active, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierLookupDto>> LookupSuppliersAsync(Guid actorId, string? search, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<PagedResult<SupplierLedgerEntryDto>> ListLedgerAsync(Guid actorId, Guid supplierId, SupplierLedgerQuery query, CancellationToken cancellationToken = default);
    Task<SupplierDetailsDto> RecordPaymentAsync(Guid actorId, SupplierPaymentRequest request, CancellationToken cancellationToken = default);
    Task<SupplierDetailsDto> AdjustBalanceAsync(Guid actorId, SupplierAdjustmentRequest request, CancellationToken cancellationToken = default);
}
