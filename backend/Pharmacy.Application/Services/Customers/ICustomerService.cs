using Pharmacy.Application.DTOs.Customers;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.Customers;

public interface ICustomerService
{
    Task<PagedResult<CustomerListItemDto>> ListCustomersAsync(Guid actorId, CustomerListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerLookupDto>> LookupCustomersAsync(Guid actorId, string? search, bool activeOnly = true, Guid? branchId = null, CancellationToken cancellationToken = default);
    Task<CustomerDetailsDto> GetCustomerAsync(Guid actorId, Guid customerId, CancellationToken cancellationToken = default);
    Task<CustomerDetailsDto> CreateCustomerAsync(Guid actorId, CustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerDetailsDto> UpdateCustomerAsync(Guid actorId, Guid customerId, CustomerUpdateRequest request, CancellationToken cancellationToken = default);
    Task SetCustomerActiveAsync(Guid actorId, Guid customerId, bool active, CancellationToken cancellationToken = default);
    Task<PagedResult<CustomerLedgerEntryDto>> ListLedgerAsync(Guid actorId, Guid customerId, CustomerLedgerQuery query, CancellationToken cancellationToken = default);
    Task<CustomerDetailsDto> RecordPaymentAsync(Guid actorId, CustomerPaymentRequest request, CancellationToken cancellationToken = default);
    Task<CustomerDetailsDto> AdjustBalanceAsync(Guid actorId, CustomerAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<CustomerPaymentReceiptDto> PaymentReceiptAsync(Guid actorId, Guid paymentId, CancellationToken cancellationToken = default);
}
