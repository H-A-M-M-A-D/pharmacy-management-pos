using System.Data;
using Pharmacy.Application.DTOs.Suppliers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Suppliers;

public interface ISupplierRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Supplier?> GetSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<bool> NormalizedNameExistsAsync(string normalizedName, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task AddSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default);
    Task AddLedgerEntryAsync(SupplierLedgerEntry entry, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task<PagedResult<SupplierListItemDto>> ListSuppliersAsync(SupplierListQuery query, CancellationToken cancellationToken = default);
    Task<SupplierDetailsDto?> GetSupplierDetailsAsync(Guid supplierId, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierLookupDto>> LookupSuppliersAsync(string? search, bool activeOnly, CancellationToken cancellationToken = default);
    Task<PagedResult<SupplierLedgerEntryDto>> ListLedgerAsync(Guid supplierId, SupplierLedgerQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
