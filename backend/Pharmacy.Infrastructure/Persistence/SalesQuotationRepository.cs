using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Quotations;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.Quotations;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class SalesQuotationRepository(PharmacyDbContext context) : ISalesQuotationRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);

    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);

    public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) =>
        context.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);

    public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        context.Customers.FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);

    public Task<SalesQuotation?> GetQuotationAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.SalesQuotations.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<SalesQuotation?> GetQuotationForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.SalesQuotations.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<string> NextQuotationNumberAsync(CancellationToken cancellationToken = default)
    {
        var next = await context.Database.SqlQueryRaw<long>("SELECT nextval('\"QuotationNumberSequence\"'::regclass) AS \"Value\"").SingleAsync(cancellationToken);
        return $"QT-{DateTime.UtcNow.Year}-{next:000000}";
    }

    public async Task AddQuotationAsync(SalesQuotation quotation, CancellationToken cancellationToken = default) => await context.SalesQuotations.AddAsync(quotation, cancellationToken);

    public void ReplaceQuotationItems(SalesQuotation quotation, List<SalesQuotationItem> items)
    {
        context.SalesQuotationItems.RemoveRange(quotation.Items);
        quotation.Items = items;
    }

    public async Task<QuotationDetailsDto?> GetQuotationDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quotation = await context.SalesQuotations.AsNoTracking()
            .Include(x => x.Branch).Include(x => x.Godown).Include(x => x.Customer).Include(x => x.PriceLevel)
            .Include(x => x.CreatedByUser).Include(x => x.ApprovedByUser)
            .Include(x => x.ConvertedToSalesOrder).Include(x => x.ConvertedToSale)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (quotation is null) return null;
        var effectiveStatus = EffectiveStatus(quotation.Status, quotation.ValidUntil);
        var items = quotation.Items.OrderBy(x => x.CreatedAt)
            .Select(x => new QuotationItemDto(x.Id, x.ProductId, x.Product!.Name, x.Product.SKU, x.Quantity, x.UnitPrice, x.DiscountPercent, x.GrossAmount, x.DiscountAmount, x.NetAmount))
            .ToList();
        return new QuotationDetailsDto(quotation.Id, quotation.QuotationNumber, quotation.BranchId, quotation.Branch!.Name, quotation.GodownId, quotation.Godown?.Name,
            quotation.CustomerId, quotation.Customer!.CustomerCode, quotation.Customer.Name, quotation.PriceLevelId, quotation.PriceLevel?.Name,
            quotation.QuotationDate, quotation.ValidUntil, effectiveStatus, quotation.Notes,
            quotation.Subtotal, quotation.DiscountTotal, quotation.NetTotal,
            quotation.CreatedByUserId, quotation.CreatedByUser!.FullName, quotation.ApprovedByUserId, quotation.ApprovedByUser?.FullName,
            quotation.ConvertedToSalesOrderId, quotation.ConvertedToSalesOrder?.OrderNumber, quotation.ConvertedToSaleId, quotation.ConvertedToSale?.InvoiceNumber,
            quotation.SentAtUtc, quotation.RespondedAtUtc, quotation.CancelledAtUtc, quotation.CancellationReason, quotation.CreatedAt, items);
    }

    public async Task<PagedResult<QuotationListItemDto>> ListQuotationsAsync(QuotationListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var quotations = context.SalesQuotations.AsNoTracking().Include(x => x.Customer).Include(x => x.CreatedByUser).AsQueryable();
        if (!canSelectBranch && actorBranchId.HasValue) quotations = quotations.Where(x => x.BranchId == actorBranchId);
        if (query.BranchId.HasValue) quotations = quotations.Where(x => x.BranchId == query.BranchId);
        if (query.CustomerId.HasValue) quotations = quotations.Where(x => x.CustomerId == query.CustomerId);
        if (query.Status.HasValue) quotations = quotations.Where(x => x.Status == query.Status);
        if (query.FromDate.HasValue) quotations = quotations.Where(x => x.QuotationDate >= query.FromDate);
        if (query.ToDate.HasValue) quotations = quotations.Where(x => x.QuotationDate <= query.ToDate);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            quotations = quotations.Where(x => EF.Functions.ILike(x.QuotationNumber, pattern) || EF.Functions.ILike(x.Customer!.Name, pattern));
        }
        var total = await quotations.CountAsync(cancellationToken);
        var rows = await quotations.OrderByDescending(x => x.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        var items = rows.Select(x => new QuotationListItemDto(x.Id, x.QuotationNumber, x.CustomerId, x.Customer!.Name, x.QuotationDate, x.ValidUntil,
            EffectiveStatus(x.Status, x.ValidUntil), x.NetTotal, x.CreatedByUser!.FullName)).ToList();
        return new(items, query.Page, query.PageSize, total);
    }

    private static SalesQuotationStatus EffectiveStatus(SalesQuotationStatus status, DateOnly? validUntil) =>
        status is SalesQuotationStatus.Draft or SalesQuotationStatus.Sent && validUntil.HasValue && validUntil.Value < DateOnly.FromDateTime(DateTime.UtcNow)
            ? SalesQuotationStatus.Expired
            : status;

    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        await using var tx = await context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        try { await operation(cancellationToken); await tx.CommitAsync(cancellationToken); }
        catch { await tx.RollbackAsync(cancellationToken); throw; }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ResourceConflictException("A quotation with this unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Quotation constraints were violated.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            throw new ResourceConflictException("This quotation changed while your request was in progress. Please retry.");
        }
    }
}
