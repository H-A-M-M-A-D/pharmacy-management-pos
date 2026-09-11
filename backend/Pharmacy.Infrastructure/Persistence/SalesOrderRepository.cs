using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.SalesOrders;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.SalesOrders;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class SalesOrderRepository(PharmacyDbContext context) : ISalesOrderRepository
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

    public Task<SalesOrder?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.SalesOrders.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<SalesOrder?> GetOrderForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.SalesOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<string> NextOrderNumberAsync(CancellationToken cancellationToken = default)
    {
        var next = await context.Database.SqlQueryRaw<long>("SELECT nextval('\"SalesOrderNumberSequence\"'::regclass) AS \"Value\"").SingleAsync(cancellationToken);
        return $"SO-{DateTime.UtcNow.Year}-{next:000000}";
    }

    public async Task AddOrderAsync(SalesOrder order, CancellationToken cancellationToken = default) => await context.SalesOrders.AddAsync(order, cancellationToken);

    public void ReplaceOrderItems(SalesOrder order, List<SalesOrderItem> items)
    {
        context.SalesOrderItems.RemoveRange(order.Items);
        order.Items = items;
    }

    public async Task<IReadOnlyList<LinkedSaleDto>> GetLinkedSalesAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await context.Sales.AsNoTracking().Where(x => x.SalesOrderId == orderId)
            .OrderBy(x => x.PostedAtUtc)
            .Select(x => new LinkedSaleDto(x.Id, x.InvoiceNumber, x.PostedAtUtc, x.NetTotal))
            .ToListAsync(cancellationToken);

    public async Task<SalesOrderDetailsDto?> GetOrderDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await context.SalesOrders.AsNoTracking()
            .Include(x => x.Branch).Include(x => x.Godown).Include(x => x.Customer).Include(x => x.PriceLevel).Include(x => x.Quotation)
            .Include(x => x.CreatedByUser).Include(x => x.ConfirmedByUser)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (order is null) return null;
        var linkedSales = await GetLinkedSalesAsync(id, cancellationToken);
        var items = order.Items.OrderBy(x => x.CreatedAt)
            .Select(x => new SalesOrderItemDto(x.Id, x.ProductId, x.Product!.Name, x.Product.SKU, x.OrderedQuantity, x.FulfilledQuantity, x.UnitPrice, x.DiscountPercent, x.GrossAmount, x.DiscountAmount, x.NetAmount))
            .ToList();
        return new SalesOrderDetailsDto(order.Id, order.OrderNumber, order.BranchId, order.Branch!.Name, order.GodownId, order.Godown?.Name,
            order.CustomerId, order.Customer!.CustomerCode, order.Customer.Name, order.PriceLevelId, order.PriceLevel?.Name,
            order.QuotationId, order.Quotation?.QuotationNumber,
            order.OrderDate, order.ExpectedDeliveryDate, order.Status, order.Notes,
            order.Subtotal, order.DiscountTotal, order.NetTotal,
            order.CreatedByUserId, order.CreatedByUser!.FullName, order.ConfirmedByUserId, order.ConfirmedByUser?.FullName, order.ConfirmedAtUtc,
            order.CancelledAtUtc, order.CancellationReason, order.CreatedAt, items, linkedSales);
    }

    public async Task<PagedResult<SalesOrderListItemDto>> ListOrdersAsync(SalesOrderListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var orders = context.SalesOrders.AsNoTracking().Include(x => x.Customer).Include(x => x.Items).AsQueryable();
        if (!canSelectBranch && actorBranchId.HasValue) orders = orders.Where(x => x.BranchId == actorBranchId);
        if (query.BranchId.HasValue) orders = orders.Where(x => x.BranchId == query.BranchId);
        if (query.CustomerId.HasValue) orders = orders.Where(x => x.CustomerId == query.CustomerId);
        if (query.Status.HasValue) orders = orders.Where(x => x.Status == query.Status);
        if (query.FromDate.HasValue) orders = orders.Where(x => x.OrderDate >= query.FromDate);
        if (query.ToDate.HasValue) orders = orders.Where(x => x.OrderDate <= query.ToDate);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            orders = orders.Where(x => EF.Functions.ILike(x.OrderNumber, pattern) || EF.Functions.ILike(x.Customer!.Name, pattern));
        }
        var total = await orders.CountAsync(cancellationToken);
        var rows = await orders.OrderByDescending(x => x.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        var items = rows.Select(x => new SalesOrderListItemDto(x.Id, x.OrderNumber, x.CustomerId, x.Customer!.Name, x.OrderDate, x.Status, x.NetTotal,
            x.Items.Sum(i => i.OrderedQuantity), x.Items.Sum(i => i.FulfilledQuantity))).ToList();
        return new(items, query.Page, query.PageSize, total);
    }

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
            throw new ResourceConflictException("A sales order with this unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Sales order constraints were violated.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            throw new ResourceConflictException("This sales order changed while your request was in progress. Please retry.");
        }
    }
}
