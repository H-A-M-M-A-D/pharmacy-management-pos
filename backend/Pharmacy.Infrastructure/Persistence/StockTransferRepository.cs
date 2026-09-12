using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.StockTransfers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.StockTransfers;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class StockTransferRepository(PharmacyDbContext context) : IStockTransferRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);
    public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) => context.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
    public Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default) => context.ProductBatches.FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken);
    public Task<ProductBatch?> GetBatchForUpdateAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        context.ProductBatches.FromSqlInterpolated($"SELECT * FROM \"ProductBatches\" WHERE \"Id\" = {batchId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
    public Task<ProductBatch?> GetBatchByNumberAsync(Guid branchId, Guid? godownId, Guid productId, string batchNumber, CancellationToken cancellationToken = default) =>
        context.ProductBatches.FirstOrDefaultAsync(x => x.BranchId == branchId && x.GodownId == godownId && x.ProductId == productId && x.BatchNumber == batchNumber, cancellationToken);
    public Task<Inventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default) =>
        context.Inventory.FirstOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == productId && x.ProductBatchId == batchId, cancellationToken);
    public async Task AddBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default) => await context.ProductBatches.AddAsync(batch, cancellationToken);
    public async Task AddInventoryAsync(Inventory inventory, CancellationToken cancellationToken = default) => await context.Inventory.AddAsync(inventory, cancellationToken);
    public async Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default) => await context.StockMovements.AddAsync(movement, cancellationToken);
    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task<string> NextTransferNumberAsync(DateOnly transferDate, CancellationToken cancellationToken = default)
    {
        var next = await context.Database.SqlQueryRaw<long>("SELECT nextval('\"StockTransferNumberSequence\"'::regclass) AS \"Value\"").SingleAsync(cancellationToken);
        return $"TRF-{transferDate.Year}-{next:000000}";
    }

    public async Task AddTransferAsync(StockTransfer transfer, CancellationToken cancellationToken = default) => await context.StockTransfers.AddAsync(transfer, cancellationToken);

    public async Task<StockTransfer?> GetTransferForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Lock the transfer row itself (a scalar FOR UPDATE cannot carry the Items include), then load
        // the tracked entity graph normally - this closes the race between two actors mutating the same
        // transfer's status/quantities concurrently instead of relying incidentally on that write itself.
        var locked = await context.Database.SqlQuery<Guid>($"SELECT \"Id\" FROM \"StockTransfers\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(cancellationToken);
        if (locked.Count == 0) return null;
        return await context.StockTransfers.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public void ReplaceTransferItems(StockTransfer transfer, IReadOnlyCollection<StockTransferItem> items)
    {
        context.StockTransferItems.RemoveRange(transfer.Items);
        transfer.Items.Clear();
        foreach (var item in items)
        {
            item.StockTransferId = transfer.Id;
            transfer.Items.Add(item);
            context.StockTransferItems.Add(item);
        }
    }

    public async Task<StockTransferDetailsDto?> GetTransferDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transfer = await DetailsQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return transfer is null ? null : Map(transfer);
    }

    public async Task<PagedResult<StockTransferListItemDto>> ListTransfersAsync(StockTransferListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var transfers = DetailsQuery();
        if (!canSelectBranch && actorBranchId.HasValue)
            transfers = transfers.Where(x => x.SourceBranchId == actorBranchId || x.DestinationBranchId == actorBranchId);
        if (!string.IsNullOrWhiteSpace(query.TransferNumber))
            transfers = transfers.Where(x => EF.Functions.ILike(x.TransferNumber, $"%{query.TransferNumber.Trim()}%"));
        if (query.DateFrom.HasValue) transfers = transfers.Where(x => x.TransferDate >= query.DateFrom);
        if (query.DateTo.HasValue) transfers = transfers.Where(x => x.TransferDate <= query.DateTo);
        if (query.Status.HasValue) transfers = transfers.Where(x => x.Status == query.Status);
        if (query.SourceBranchId.HasValue) transfers = transfers.Where(x => x.SourceBranchId == query.SourceBranchId);
        if (query.SourceGodownId.HasValue) transfers = transfers.Where(x => x.SourceGodownId == query.SourceGodownId);
        if (query.DestinationBranchId.HasValue) transfers = transfers.Where(x => x.DestinationBranchId == query.DestinationBranchId);
        if (query.DestinationGodownId.HasValue) transfers = transfers.Where(x => x.DestinationGodownId == query.DestinationGodownId);
        if (query.ProductId.HasValue) transfers = transfers.Where(x => x.Items.Any(i => i.ProductId == query.ProductId));

        var total = await transfers.CountAsync(cancellationToken);
        var page = await transfers.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.TransferNumber)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        var items = page.Select(x => new StockTransferListItemDto(
            x.Id, x.TransferNumber, x.TransferDate, x.Status,
            x.SourceBranchId, x.SourceBranch!.Name, x.SourceGodownId, x.SourceGodown!.Name,
            x.DestinationBranchId, x.DestinationBranch!.Name, x.DestinationGodownId, x.DestinationGodown!.Name,
            x.Items.Sum(i => i.QuantityRequested), x.Items.Sum(i => i.QuantityApproved), x.Items.Sum(i => i.QuantityDispatched),
            x.Items.Sum(i => i.QuantityReceived), x.Items.Sum(i => i.QuantityDispatched - i.QuantityReceived),
            x.RequestedByUser == null ? null : x.RequestedByUser.FullName, x.CreatedAt)).ToList();
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<TransferableBatchDto>> ListTransferableBatchesAsync(Guid branchId, Guid godownId, Guid? productId, string? search, CancellationToken cancellationToken = default)
    {
        var batches = context.ProductBatches.AsNoTracking().Include(x => x.Product)
            .Where(x => x.BranchId == branchId && x.GodownId == godownId && !x.IsDisposed && x.QuantityAvailable > 0);
        if (productId.HasValue) batches = batches.Where(x => x.ProductId == productId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            batches = batches.Where(x => EF.Functions.ILike(x.Product!.Name, pattern) || EF.Functions.ILike(x.Product.SKU, pattern) || EF.Functions.ILike(x.BatchNumber, pattern));
        }
        return await batches.OrderBy(x => x.Product!.Name).ThenBy(x => x.ExpiryDate)
            .Select(x => new TransferableBatchDto(x.Id, x.ProductId, x.Product!.Name, x.Product.SKU, x.BatchNumber, x.ExpiryDate, x.QuantityAvailable, x.PurchasePrice, x.RetailPrice))
            .Take(200).ToListAsync(cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ResourceConflictException("A conflicting stock transfer record already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Stock transfer quantity constraints were violated.");
        }
    }

    private IQueryable<StockTransfer> DetailsQuery() =>
        context.StockTransfers.AsNoTracking()
            .Include(x => x.SourceBranch).Include(x => x.SourceGodown)
            .Include(x => x.DestinationBranch).Include(x => x.DestinationGodown)
            .Include(x => x.CreatedByUser).Include(x => x.RequestedByUser).Include(x => x.ApprovedByUser)
            .Include(x => x.DispatchedByUser).Include(x => x.ReceivedByUser).Include(x => x.CancelledByUser)
            .Include(x => x.Items).ThenInclude(x => x.Product);

    private static StockTransferDetailsDto Map(StockTransfer x)
    {
        var items = x.Items.OrderBy(i => i.Product!.Name).ThenBy(i => i.BatchNumber).Select(i => new StockTransferItemDto(
            i.Id, i.ProductId, i.Product!.Name, i.Product.SKU,
            i.SourceProductBatchId, i.BatchNumber, i.ExpiryDate, i.UnitCostSnapshot, i.DestinationProductBatchId,
            i.QuantityRequested, i.QuantityApproved, i.QuantityDispatched, i.QuantityReceived, i.QuantityDispatched - i.QuantityReceived,
            i.Notes)).ToList();
        return new StockTransferDetailsDto(
            x.Id, x.TransferNumber, x.TransferDate, x.Status, x.Notes,
            x.SourceBranchId, x.SourceBranch!.Name, x.SourceGodownId, x.SourceGodown!.Name,
            x.DestinationBranchId, x.DestinationBranch!.Name, x.DestinationGodownId, x.DestinationGodown!.Name,
            x.CreatedByUser?.FullName, x.CreatedAt,
            x.RequestedByUser?.FullName, x.RequestedAtUtc,
            x.ApprovedByUser?.FullName, x.ApprovedAtUtc,
            x.DispatchedByUser?.FullName, x.DispatchedAtUtc,
            x.ReceivedByUser?.FullName, x.ReceivedAtUtc,
            x.CancelledByUser?.FullName, x.CancelledAtUtc, x.CancellationReason,
            items);
    }
}
