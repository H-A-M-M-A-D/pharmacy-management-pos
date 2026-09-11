using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Customers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Customers;

public interface ICustomerRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<bool> CustomerCodeExistsAsync(string customerCode, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task<bool> PriceLevelIsValidAsync(Guid id, CancellationToken cancellationToken = default);
    Task<string> NextCustomerCodeAsync(CancellationToken cancellationToken = default);
    Task<string> NextPaymentReceiptNumberAsync(DateTime paymentDateUtc, CancellationToken cancellationToken = default);
    Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default);
    Task AddPaymentAsync(CustomerPayment payment, CancellationToken cancellationToken = default);
    Task AddLedgerEntryAsync(CustomerLedgerEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpenReceivableDto>> GetOpenReceivablesAsync(Guid customerId, Guid? branchId, CancellationToken cancellationToken = default);
    Task AddPaymentAllocationAsync(CustomerPaymentAllocation allocation, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task<PagedResult<CustomerListItemDto>> ListCustomersAsync(CustomerListQuery query, CancellationToken cancellationToken = default);
    Task<CustomerDetailsDto?> GetCustomerDetailsAsync(Guid customerId, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerLookupDto>> LookupCustomersAsync(string? search, bool activeOnly, Guid? branchId, CancellationToken cancellationToken = default);
    Task<PagedResult<CustomerLedgerEntryDto>> ListLedgerAsync(Guid customerId, CustomerLedgerQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<CustomerPaymentReceiptDto?> GetPaymentReceiptAsync(Guid paymentId, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
