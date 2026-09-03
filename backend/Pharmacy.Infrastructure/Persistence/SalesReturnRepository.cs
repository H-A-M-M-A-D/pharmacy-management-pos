using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.Sales;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;
using DomainInventory = Pharmacy.Domain.Entities.Inventory;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class SalesReturnRepository(PharmacyDbContext context) : ISalesReturnRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);

    public Task<Sale?> GetOriginalSaleAsync(Guid saleId, CancellationToken cancellationToken = default) =>
        context.Sales.Include(x => x.Branch).Include(x => x.CashierUser).Include(x => x.Payments)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Items).ThenInclude(x => x.Allocations).ThenInclude(x => x.ProductBatch)
            .FirstOrDefaultAsync(x => x.Id == saleId, cancellationToken);

    public Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        context.ProductBatches.FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken);

    public Task<DomainInventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default) =>
        context.Inventory.FirstOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == productId && x.ProductBatchId == batchId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> GetReturnedQuantitiesAsync(IEnumerable<Guid> allocationIds, CancellationToken cancellationToken = default)
    {
        var ids = allocationIds.Distinct().ToArray();
        return await context.SalesReturnAllocations.AsNoTracking()
            .Where(x => ids.Contains(x.OriginalSaleItemBatchAllocationId) && x.SalesReturnItem!.SalesReturn!.Status == SalesReturnStatus.Posted)
            .GroupBy(x => x.OriginalSaleItemBatchAllocationId)
            .Select(x => new { AllocationId = x.Key, Quantity = x.Sum(a => a.Quantity) })
            .ToDictionaryAsync(x => x.AllocationId, x => x.Quantity, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetRefundedAmountsAsync(IEnumerable<Guid> allocationIds, CancellationToken cancellationToken = default)
    {
        var ids = allocationIds.Distinct().ToArray();
        return await context.SalesReturnAllocations.AsNoTracking()
            .Where(x => ids.Contains(x.OriginalSaleItemBatchAllocationId) && x.SalesReturnItem!.SalesReturn!.Status == SalesReturnStatus.Posted)
            .GroupBy(x => x.OriginalSaleItemBatchAllocationId)
            .Select(x => new { AllocationId = x.Key, Refund = x.Sum(a => a.RefundAmount) })
            .ToDictionaryAsync(x => x.AllocationId, x => x.Refund, cancellationToken);
    }

    public async Task<string> NextReturnNumberAsync(DateTime returnDateUtc, CancellationToken cancellationToken = default)
    {
        var next = await context.Database
            .SqlQueryRaw<long>("SELECT nextval('\"SalesReturnNumberSequence\"'::regclass)")
            .SingleAsync(cancellationToken);
        return $"RET-{returnDateUtc.Year}-{next:000000}";
    }

    public async Task AddSalesReturnAsync(SalesReturn salesReturn, CancellationToken cancellationToken = default) => await context.SalesReturns.AddAsync(salesReturn, cancellationToken);
    public async Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default) => await context.StockMovements.AddAsync(movement, cancellationToken);
    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task<ReturnableSaleDto?> GetReturnableSaleAsync(Guid saleId, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var sale = await context.Sales.AsNoTracking().Include(x => x.Branch).Include(x => x.CashierUser).Include(x => x.Payments)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Items).ThenInclude(x => x.Allocations).ThenInclude(x => x.ProductBatch)
            .Where(x => x.Id == saleId && x.Status == SaleStatus.Posted && (canSelectBranch || x.BranchId == actorBranchId))
            .FirstOrDefaultAsync(cancellationToken);
        if (sale is null || sale.InvoiceNumber is null || sale.PostedAtUtc is null) return null;
        var allocationIds = sale.Items.SelectMany(x => x.Allocations).Select(x => x.Id).ToArray();
        var returned = await GetReturnedQuantitiesAsync(allocationIds, cancellationToken);
        var refunded = await GetRefundedAmountsAsync(allocationIds, cancellationToken);
        var items = sale.Items.OrderBy(x => x.CreatedAt).Select(item =>
        {
            var allocations = item.Allocations.OrderBy(x => x.ExpiryDateSnapshot).ThenBy(x => x.ProductBatch!.BatchNumber).Select(allocation =>
            {
                var already = returned.GetValueOrDefault(allocation.Id);
                var remaining = allocation.Quantity - already;
                return new ReturnableAllocationDto(allocation.Id, allocation.ProductBatchId, allocation.ProductBatch!.BatchNumber, allocation.ExpiryDateSnapshot,
                    allocation.Quantity, already, remaining, allocation.UnitSalePriceSnapshot, allocation.NetAmount - refunded.GetValueOrDefault(allocation.Id), allocation.ProductBatch.IsDisposed, allocation.ProductBatch.ExpiryDate < businessDate);
            }).ToList();
            return new ReturnableSaleItemDto(item.Id, item.ProductId, item.Product!.Name, item.Product.SKU, item.RequestedQuantity,
                allocations.Sum(x => x.AlreadyReturnedQuantity), allocations.Sum(x => x.RemainingQuantity), item.NetAmount, allocations.Sum(x => x.RefundRemaining), allocations);
        }).ToList();
        var sold = items.Sum(x => x.SoldQuantity);
        var remainingTotal = items.Sum(x => x.RemainingQuantity);
        var state = remainingTotal == sold ? SalesReturnState.NotReturned : remainingTotal == 0 ? SalesReturnState.FullyReturned : SalesReturnState.PartiallyReturned;
        return new ReturnableSaleDto(sale.Id, sale.InvoiceNumber, sale.PostedAtUtc.Value, sale.BranchId, sale.Branch!.Name, sale.CashierUser!.FullName,
            sale.CustomerName, sale.CustomerPhone, sale.NetTotal, state, items, sale.Payments.OrderBy(x => x.CreatedAt).Select(x => new SalePaymentDto(x.Id, x.Method, x.AmountApplied, x.TenderedAmount, x.ReferenceNumber)).ToList());
    }

    public async Task<PagedResult<SalesReturnListItemDto>> ListReturnsAsync(SalesReturnsQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var returns = context.SalesReturns.AsNoTracking().Include(x => x.Branch).Include(x => x.ProcessedByUser).Include(x => x.OriginalSale).Include(x => x.Items).AsQueryable();
        if (!canSelectBranch && actorBranchId.HasValue) returns = returns.Where(x => x.BranchId == actorBranchId);
        if (query.BranchId.HasValue) returns = returns.Where(x => x.BranchId == query.BranchId);
        if (query.ProcessedByUserId.HasValue) returns = returns.Where(x => x.ProcessedByUserId == query.ProcessedByUserId);
        if (query.Reason.HasValue) returns = returns.Where(x => x.Reason == query.Reason);
        if (query.FromUtc.HasValue) returns = returns.Where(x => x.ReturnDateUtc >= query.FromUtc);
        if (query.ToUtc.HasValue) returns = returns.Where(x => x.ReturnDateUtc <= query.ToUtc);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            returns = returns.Where(x => EF.Functions.ILike(x.ReturnNumber, pattern) || (x.OriginalSale!.InvoiceNumber != null && EF.Functions.ILike(x.OriginalSale.InvoiceNumber, pattern)) || (x.OriginalSale.CustomerName != null && EF.Functions.ILike(x.OriginalSale.CustomerName, pattern)));
        }
        var total = await returns.CountAsync(cancellationToken);
        var rows = await returns.OrderByDescending(x => x.ReturnDateUtc).ThenByDescending(x => x.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return new(rows.Select(x => new SalesReturnListItemDto(x.Id, x.ReturnNumber, x.OriginalSale!.InvoiceNumber!, x.ReturnDateUtc, x.BranchId, x.Branch!.Name, x.ProcessedByUser!.FullName, x.OriginalSale.CustomerName, x.Items.Count, x.RefundAmount, x.Status, x.Reason)).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<SalesReturnDetailsDto?> GetReturnDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var salesReturn = await context.SalesReturns.AsNoTracking().Include(x => x.Branch).Include(x => x.ProcessedByUser).Include(x => x.OriginalSale)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Items).ThenInclude(x => x.Allocations).ThenInclude(x => x.ProductBatch)
            .Include(x => x.RefundPayments)
            .Where(x => x.Id == id && (canSelectBranch || x.BranchId == actorBranchId)).FirstOrDefaultAsync(cancellationToken);
        return salesReturn is null ? null : Map(salesReturn);
    }

    private static SalesReturnDetailsDto Map(SalesReturn x)
    {
        var items = x.Items.OrderBy(i => i.CreatedAt).Select(item => new SalesReturnItemDto(item.Id, item.OriginalSaleItemId, item.ProductId, item.Product!.Name, item.Product.SKU, item.Quantity, item.GrossReturnAmount, item.DiscountReturnAmount, item.TaxReturnAmount, item.RefundAmount,
            item.Allocations.OrderBy(a => a.CreatedAt).Select(a => new SalesReturnAllocationDto(a.Id, a.OriginalSaleItemBatchAllocationId, a.ProductBatchId, a.ProductBatch!.BatchNumber, a.ExpiryDateSnapshot, a.Quantity, a.Disposition, a.UnitSalePriceSnapshot, a.GrossReturnAmount, a.DiscountReturnAmount, a.TaxReturnAmount, a.RefundAmount)).ToList())).ToList();
        var payments = x.RefundPayments.OrderBy(p => p.CreatedAt).Select(p => new SalesRefundPaymentDto(p.Id, p.Method, p.Amount, p.ReferenceNumber)).ToList();
        return new SalesReturnDetailsDto(x.Id, x.ReturnNumber, x.OriginalSaleId, x.OriginalSale!.InvoiceNumber!, x.BranchId, x.Branch!.Name, x.Branch.Address, x.Branch.PhoneNumber, x.ProcessedByUserId, x.ProcessedByUser!.FullName, x.ReturnDateUtc, x.Reason, x.Notes, x.GrossReturnAmount, x.DiscountReturnAmount, x.TaxReturnAmount, x.RefundAmount, x.Status, x.OriginalSale.CustomerName, x.OriginalSale.CustomerPhone, items, payments);
    }

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
            throw new ResourceConflictException("A sales return with this unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Sales return constraints were violated.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            throw new ResourceConflictException("Returnable quantity changed while posting the return. Please refresh and try again.");
        }
    }
}

