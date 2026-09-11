using System.Data;
using Pharmacy.Application.DTOs.Quotations;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Quotations;

public interface ISalesQuotationRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<SalesQuotation?> GetQuotationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked load (quotation + items), used both by this service's own mutating actions
    /// and, via ISalesQuotationRepository, by SalesService when converting a quotation directly into
    /// a Sale within the same atomic transaction.</summary>
    Task<SalesQuotation?> GetQuotationForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<string> NextQuotationNumberAsync(CancellationToken cancellationToken = default);
    Task AddQuotationAsync(SalesQuotation quotation, CancellationToken cancellationToken = default);
    void ReplaceQuotationItems(SalesQuotation quotation, List<SalesQuotationItem> items);
    Task<QuotationDetailsDto?> GetQuotationDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<QuotationListItemDto>> ListQuotationsAsync(QuotationListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
