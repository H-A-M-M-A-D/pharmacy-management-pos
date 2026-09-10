using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Inventory;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.Inventory;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class InventoryRepository(PharmacyDbContext context) : IInventoryRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);
    public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) => context.Products.Include(x => x.Category).Include(x => x.Manufacturer).FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
    public Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default) => context.ProductBatches.FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken);
    public Task<ProductBatch?> GetBatchByNumberAsync(Guid branchId, Guid? godownId, Guid productId, string batchNumber, CancellationToken cancellationToken = default) =>
        context.ProductBatches.FirstOrDefaultAsync(x => x.BranchId == branchId && x.GodownId == godownId && x.ProductId == productId && x.BatchNumber == batchNumber, cancellationToken);
    public Task<Inventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default) =>
        context.Inventory.FirstOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == productId && x.ProductBatchId == batchId, cancellationToken);
    public Task<Supplier?> GetSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default) => context.Suppliers.FirstOrDefaultAsync(x => x.Id == supplierId, cancellationToken);
    public async Task<IReadOnlyList<ProductBatch>> GetEligibleBatchesAsync(Guid branchId, Guid productId, CancellationToken cancellationToken = default) =>
        await context.ProductBatches.AsNoTracking().Where(x => x.BranchId == branchId && x.ProductId == productId).ToListAsync(cancellationToken);
    public async Task AddBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default) => await context.ProductBatches.AddAsync(batch, cancellationToken);
    public async Task AddInventoryAsync(Inventory inventory, CancellationToken cancellationToken = default) => await context.Inventory.AddAsync(inventory, cancellationToken);
    public async Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default) => await context.StockMovements.AddAsync(movement, cancellationToken);
    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task<PagedResult<InventoryListItemDto>> ListInventoryAsync(InventoryListQuery query, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var rows = await BatchQuery(query.BranchId, actorBranchId, canSelectBranch, businessDate)
            .Where(x => query.IsProductActive == null || x.Product!.IsActive == query.IsProductActive)
            .Where(x => query.CategoryId == null || x.Product!.CategoryId == query.CategoryId)
            .Where(x => query.ManufacturerId == null || x.Product!.ManufacturerId == query.ManufacturerId)
            .Where(x => string.IsNullOrWhiteSpace(query.Search) || EF.Functions.ILike(x.Product!.Name, $"%{query.Search.Trim()}%") || EF.Functions.ILike(x.Product.SKU, $"%{query.Search.Trim()}%") || (x.Product.GenericName != null && EF.Functions.ILike(x.Product.GenericName, $"%{query.Search.Trim()}%")))
            .Select(x => new
            {
                x.ProductId, ProductName = x.Product!.Name, x.Product.SKU, x.Product.GenericName,
                Category = x.Product.Category!.Name, Manufacturer = x.Product.Manufacturer == null ? null : x.Product.Manufacturer.Name,
                x.Product.ReorderLevel, BatchId = x.Id, x.ExpiryDate, x.QuantityAvailable, x.PurchasePrice, x.IsDisposed
            }).ToListAsync(cancellationToken);
        var grouped = rows.GroupBy(x => x.ProductId).Select(g =>
        {
            var total = g.Sum(x => x.QuantityAvailable);
            var status = StockStatus(total, g.First().ReorderLevel);
            return new InventoryListItemDto(g.Key, g.First().ProductName, g.First().SKU, g.First().GenericName, g.First().Category,
                g.First().Manufacturer, total, g.First().ReorderLevel, status,
                g.Count(x => x.QuantityAvailable > 0 && !x.IsDisposed && x.ExpiryDate >= businessDate),
                g.Where(x => x.QuantityAvailable > 0 && !x.IsDisposed && x.ExpiryDate >= businessDate).Select(x => (DateOnly?)x.ExpiryDate).OrderBy(x => x).FirstOrDefault(),
                g.Sum(x => x.QuantityAvailable * x.PurchasePrice));
        });
        if (query.StockStatus.HasValue) grouped = grouped.Where(x => x.StockStatus == query.StockStatus);
        grouped = (query.SortBy.ToLowerInvariant(), query.Descending) switch
        {
            ("quantity", false) => grouped.OrderBy(x => x.QuantityInStock).ThenBy(x => x.ProductName),
            ("quantity", true) => grouped.OrderByDescending(x => x.QuantityInStock).ThenBy(x => x.ProductName),
            ("nearestexpiry", false) => grouped.OrderBy(x => x.NearestExpiryDate ?? DateOnly.MaxValue).ThenBy(x => x.ProductName),
            ("nearestexpiry", true) => grouped.OrderByDescending(x => x.NearestExpiryDate ?? DateOnly.MinValue).ThenBy(x => x.ProductName),
            ("stockvalue", false) => grouped.OrderBy(x => x.EstimatedStockValue).ThenBy(x => x.ProductName),
            ("stockvalue", true) => grouped.OrderByDescending(x => x.EstimatedStockValue).ThenBy(x => x.ProductName),
            (_, true) => grouped.OrderByDescending(x => x.ProductName),
            _ => grouped.OrderBy(x => x.ProductName)
        };
        var list = grouped.ToList();
        return new(list.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(), query.Page, query.PageSize, list.Count);
    }

    public async Task<InventoryDetailsDto?> GetInventoryDetailsAsync(Guid branchId, Guid productId, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var rows = await BatchQuery(branchId, null, true, businessDate).Where(x => x.ProductId == productId).Select(x => new
        {
            Batch = x, ProductName = x.Product!.Name, x.Product.SKU, x.Product.GenericName, Category = x.Product.Category!.Name,
            Manufacturer = x.Product.Manufacturer == null ? null : x.Product.Manufacturer.Name, x.Product.ReorderLevel, BranchName = x.Branch!.Name
        }).ToListAsync(cancellationToken);
        if (rows.Count == 0) return null;
        var batches = rows.Select(x => MapBatch(x.Batch, x.ProductName, x.SKU, x.BranchName, businessDate)).OrderBy(x => x.ExpiryDate).ThenBy(x => x.BatchNumber).ToList();
        var total = batches.Sum(x => x.QuantityAvailable);
        return new InventoryDetailsDto(branchId, rows.First().BranchName, productId, rows.First().ProductName, rows.First().SKU,
            rows.First().GenericName, rows.First().Category, rows.First().Manufacturer, total, rows.First().ReorderLevel,
            StockStatus(total, rows.First().ReorderLevel), batches.Where(x => x.State is BatchState.Active or BatchState.NearExpiry).Select(x => (DateOnly?)x.ExpiryDate).FirstOrDefault(),
            batches.Sum(x => x.EstimatedStockValue), batches);
    }

    public async Task<PagedResult<BatchListItemDto>> ListBatchesAsync(BatchListQuery query, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var rows = await BatchQuery(query.BranchId, actorBranchId, canSelectBranch, businessDate)
            .Where(x => query.ProductId == null || x.ProductId == query.ProductId)
            .Where(x => query.GodownId == null || x.GodownId == query.GodownId)
            .Where(x => !query.HasStockOnly || x.QuantityAvailable > 0)
            .Where(x => query.ExpiryFrom == null || x.ExpiryDate >= query.ExpiryFrom)
            .Where(x => query.ExpiryTo == null || x.ExpiryDate <= query.ExpiryTo)
            .Where(x => string.IsNullOrWhiteSpace(query.Search) || EF.Functions.ILike(x.BatchNumber, $"%{query.Search.Trim()}%") || EF.Functions.ILike(x.Product!.Name, $"%{query.Search.Trim()}%") || EF.Functions.ILike(x.Product.SKU, $"%{query.Search.Trim()}%"))
            .Select(x => new { Batch = x, ProductName = x.Product!.Name, x.Product.SKU, BranchName = x.Branch!.Name }).ToListAsync(cancellationToken);
        var mapped = rows.Select(x => MapBatch(x.Batch, x.ProductName, x.SKU, x.BranchName, businessDate));
        if (query.State.HasValue) mapped = mapped.Where(x => x.State == query.State.Value);
        var list = mapped.OrderBy(x => x.ExpiryDate).ThenBy(x => x.ProductName).ThenBy(x => x.BatchNumber).ToList();
        return new(list.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(), query.Page, query.PageSize, list.Count);
    }

    public async Task<IReadOnlyList<ExpiryListItemDto>> ListExpiryAsync(ExpiryQuery query, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var from = query.From ?? DateOnly.MinValue;
        var to = query.To ?? (query.Days.HasValue ? businessDate.AddDays(query.Days.Value) : businessDate.AddDays(30));
        var expiring = BatchQuery(query.BranchId, actorBranchId, canSelectBranch, businessDate)
            .Where(x => x.QuantityAvailable > 0 && !x.IsDisposed && x.ExpiryDate >= from && x.ExpiryDate <= to);
        if (query.GodownId.HasValue) expiring = expiring.Where(x => x.GodownId == query.GodownId);
        return await expiring.OrderBy(x => x.ExpiryDate).ThenBy(x => x.Product!.Name)
            .Select(x => new ExpiryListItemDto(x.Id, x.Product!.Name, x.Product.SKU, x.BatchNumber, x.Branch!.Name,
                x.ExpiryDate, x.ExpiryDate.DayNumber - businessDate.DayNumber, x.QuantityAvailable, x.PurchasePrice,
                x.QuantityAvailable * x.PurchasePrice, x.Supplier == null ? null : x.Supplier.Name,
                x.GodownId, x.Godown == null ? null : x.Godown.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<StockMovementListItemDto>> ListMovementsAsync(StockMovementListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var movements = context.StockMovements.AsNoTracking()
            .Include(x => x.Product).Include(x => x.ProductBatch).Include(x => x.Branch).Include(x => x.Godown).Include(x => x.PerformedByUser).AsQueryable();
        if (!canSelectBranch && actorBranchId.HasValue) movements = movements.Where(x => x.BranchId == actorBranchId);
        if (query.BranchId.HasValue) movements = movements.Where(x => x.BranchId == query.BranchId);
        if (query.GodownId.HasValue) movements = movements.Where(x => x.GodownId == query.GodownId);
        if (query.ProductId.HasValue) movements = movements.Where(x => x.ProductId == query.ProductId);
        if (query.ProductBatchId.HasValue) movements = movements.Where(x => x.ProductBatchId == query.ProductBatchId);
        if (query.MovementType.HasValue) movements = movements.Where(x => x.MovementType == query.MovementType);
        if (query.FromUtc.HasValue) movements = movements.Where(x => x.CreatedAt >= query.FromUtc);
        if (query.ToUtc.HasValue) movements = movements.Where(x => x.CreatedAt <= query.ToUtc);
        if (query.UserId.HasValue) movements = movements.Where(x => x.PerformedByUserId == query.UserId);
        if (!string.IsNullOrWhiteSpace(query.Search)) movements = movements.Where(x => EF.Functions.ILike(x.Product!.Name, $"%{query.Search.Trim()}%") || EF.Functions.ILike(x.ProductBatch!.BatchNumber, $"%{query.Search.Trim()}%"));
        var total = await movements.CountAsync(cancellationToken);
        var items = await movements.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new StockMovementListItemDto(x.Id, x.CreatedAt, x.Product!.Name, x.ProductBatch!.BatchNumber,
                x.Branch!.Name, x.MovementType, x.Quantity, x.PerformedByUser == null ? null : x.PerformedByUser.FullName,
                x.ReferenceType, x.ReferenceId, x.Notes, x.GodownId, x.Godown == null ? null : x.Godown.Name)).ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<InventoryIntegrityIssueDto>> CheckIntegrityAsync(Guid? branchId, Guid? productId, CancellationToken cancellationToken = default)
    {
        var batchRows = await context.ProductBatches.AsNoTracking()
            .Where(x => branchId == null || x.BranchId == branchId)
            .Where(x => productId == null || x.ProductId == productId)
            .Select(x => new { x.BranchId, x.ProductId, ProductBatchId = x.Id, x.QuantityAvailable }).ToListAsync(cancellationToken);
        var inventoryRows = await context.Inventory.AsNoTracking().Where(x => branchId == null || x.BranchId == branchId).Where(x => productId == null || x.ProductId == productId)
            .Select(x => new { x.BranchId, x.ProductId, x.ProductBatchId, x.QuantityInStock }).ToListAsync(cancellationToken);
        var movementRows = await context.StockMovements.AsNoTracking().Where(x => branchId == null || x.BranchId == branchId).Where(x => productId == null || x.ProductId == productId)
            .GroupBy(x => new { x.BranchId, x.ProductId, x.ProductBatchId }).Select(x => new { x.Key.BranchId, x.Key.ProductId, x.Key.ProductBatchId, Quantity = x.Sum(y => y.Quantity) }).ToListAsync(cancellationToken);
        var issues = new List<InventoryIntegrityIssueDto>();
        foreach (var batch in batchRows)
        {
            var inventory = inventoryRows.FirstOrDefault(x => x.BranchId == batch.BranchId && x.ProductId == batch.ProductId && x.ProductBatchId == batch.ProductBatchId)?.QuantityInStock ?? 0;
            var ledger = movementRows.FirstOrDefault(x => x.BranchId == batch.BranchId && x.ProductId == batch.ProductId && x.ProductBatchId == batch.ProductBatchId)?.Quantity ?? 0;
            if (batch.QuantityAvailable != inventory || batch.QuantityAvailable != ledger)
                issues.Add(new(batch.BranchId, batch.ProductId, batch.ProductBatchId, batch.QuantityAvailable, inventory, ledger, "Batch, inventory, and ledger quantities do not match."));
        }
        return issues;
    }

    public async Task<InventoryOptionsDto> GetOptionsAsync(string? productSearch, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => new(
        await context.Branches.AsNoTracking().Where(x => x.IsActive && (canSelectBranch || x.Id == actorBranchId)).OrderBy(x => x.Name).Select(x => new InventoryLookupDto(x.Id, x.Name)).ToListAsync(cancellationToken),
        await context.ProductCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new InventoryLookupDto(x.Id, x.Name)).ToListAsync(cancellationToken),
        await context.Manufacturers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new InventoryLookupDto(x.Id, x.Name)).ToListAsync(cancellationToken),
        await context.Products.AsNoTracking().Where(x => x.IsActive).Where(x => string.IsNullOrWhiteSpace(productSearch) || EF.Functions.ILike(x.Name, $"%{productSearch.Trim()}%") || EF.Functions.ILike(x.SKU, $"%{productSearch.Trim()}%") || (x.Barcode != null && EF.Functions.ILike(x.Barcode, $"%{productSearch.Trim()}%")) || (x.GenericName != null && EF.Functions.ILike(x.GenericName, $"%{productSearch.Trim()}%"))).OrderBy(x => x.Name).Take(50).Select(x => new ProductLookupDto(x.Id, x.Name, x.SKU, x.Barcode, x.GenericName, x.IsActive)).ToListAsync(cancellationToken),
        await context.Suppliers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new InventoryLookupDto(x.Id, x.Name)).ToListAsync(cancellationToken));

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
            throw new ResourceConflictException("An inventory record with the same unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Inventory quantity constraints were violated.");
        }
    }

    public async Task<string> NextStockCountNumberAsync(DateOnly countDate, CancellationToken cancellationToken = default)
    {
        var next = await context.Database.SqlQueryRaw<long>("SELECT nextval('\"StockCountNumberSequence\"'::regclass) AS \"Value\"").SingleAsync(cancellationToken);
        return $"SC-{countDate.Year}-{next:000000}";
    }

    public async Task<IReadOnlyList<ProductBatch>> GetEligibleBatchesForCountAsync(Guid branchId, Guid? godownId, StockCountScope scope, Guid? categoryId,
        IReadOnlyList<Guid>? productIds, IReadOnlyList<Guid>? productBatchIds, CancellationToken cancellationToken = default)
    {
        var query = context.ProductBatches.AsNoTracking().Include(x => x.Product)
            .Where(x => x.BranchId == branchId && !x.IsDisposed);
        if (godownId.HasValue) query = query.Where(x => x.GodownId == godownId);
        query = scope switch
        {
            StockCountScope.Full => query.Where(x => x.QuantityAvailable > 0),
            StockCountScope.Category => query.Where(x => x.QuantityAvailable > 0 && x.Product!.CategoryId == categoryId),
            StockCountScope.SelectedProducts => query.Where(x => productIds != null && productIds.Contains(x.ProductId)),
            StockCountScope.SelectedBatches => query.Where(x => productBatchIds != null && productBatchIds.Contains(x.Id)),
            _ => query
        };
        return await query.OrderBy(x => x.Product!.Name).ThenBy(x => x.ExpiryDate).ToListAsync(cancellationToken);
    }

    public async Task AddStockCountSessionAsync(StockCountSession session, CancellationToken cancellationToken = default) =>
        await context.StockCountSessions.AddAsync(session, cancellationToken);

    public Task<StockCountSession?> GetStockCountSessionForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.StockCountSessions
            .Include(x => x.Items).ThenInclude(x => x.ProductBatch)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<StockCountSessionDto?> GetStockCountSessionDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var session = await StockCountSessionQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return session is null ? null : MapSessionDto(session);
    }

    public async Task<PagedResult<StockCountSessionListItemDto>> ListStockCountSessionsAsync(StockCountSessionListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var sessions = StockCountSessionQuery();
        if (!canSelectBranch && actorBranchId.HasValue) sessions = sessions.Where(x => x.BranchId == actorBranchId);
        if (query.BranchId.HasValue) sessions = sessions.Where(x => x.BranchId == query.BranchId);
        if (query.GodownId.HasValue) sessions = sessions.Where(x => x.GodownId == query.GodownId);
        if (query.Status.HasValue) sessions = sessions.Where(x => x.Status == query.Status);
        if (query.From.HasValue) sessions = sessions.Where(x => x.CountDate >= query.From);
        if (query.To.HasValue) sessions = sessions.Where(x => x.CountDate <= query.To);
        var total = await sessions.CountAsync(cancellationToken);
        var items = await sessions.OrderByDescending(x => x.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new StockCountSessionListItemDto(x.Id, x.CountNumber, x.BranchId, x.Branch!.Name, x.CountDate, x.Status, x.Scope,
                x.Category == null ? null : x.Category.Name, x.Items.Count, x.Items.Count(i => i.CountedQuantity != null),
                x.Items.Count(i => i.CountedQuantity != null && i.CountedQuantity != i.SystemQuantity),
                x.CreatedByUser!.FullName, x.CreatedAt, x.CompletedAtUtc, x.GodownId, x.Godown == null ? null : x.Godown.Name))
            .ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total);
    }

    private IQueryable<StockCountSession> StockCountSessionQuery() =>
        context.StockCountSessions.AsNoTracking()
            .Include(x => x.Branch).Include(x => x.Godown).Include(x => x.Category)
            .Include(x => x.CreatedByUser).Include(x => x.StartedByUser).Include(x => x.CompletedByUser).Include(x => x.CancelledByUser)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Items).ThenInclude(x => x.ProductBatch)
            .Include(x => x.Items).ThenInclude(x => x.CountedByUser);

    private static StockCountSessionDto MapSessionDto(StockCountSession x)
    {
        var items = x.Items.OrderBy(i => i.Product!.Name).ThenBy(i => i.ProductBatch!.ExpiryDate).Select(i => new StockCountItemDto(
            i.Id, i.ProductId, i.Product!.Name, i.Product.SKU, i.ProductBatchId, i.ProductBatch!.BatchNumber, i.ProductBatch.ExpiryDate,
            i.SystemQuantity, i.CountedQuantity, i.CountedQuantity.HasValue ? i.CountedQuantity - i.SystemQuantity : null,
            i.UnitCostSnapshot, i.CountedQuantity.HasValue ? (i.CountedQuantity.Value - i.SystemQuantity) * i.UnitCostSnapshot : null,
            i.Reason, i.Notes, i.CountedByUser == null ? null : i.CountedByUser.FullName, i.CountedAtUtc)).ToList();
        return new StockCountSessionDto(x.Id, x.CountNumber, x.BranchId, x.Branch!.Name, x.CountDate, x.Status, x.Scope, x.CategoryId,
            x.Category?.Name, x.Notes, x.CreatedByUser!.FullName, x.StartedByUser?.FullName, x.StartedAtUtc,
            x.CompletedByUser?.FullName, x.CompletedAtUtc, x.CancelledByUser?.FullName, x.CancelledAtUtc,
            items.Count, items.Count(i => i.CountedQuantity != null), items.Count(i => i.Variance is not null and not 0), items,
            x.GodownId, x.Godown?.Name);
    }

    private IQueryable<ProductBatch> BatchQuery(Guid? branchId, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate)
    {
        var query = context.ProductBatches.AsNoTracking()
            .Include(x => x.Product)!.ThenInclude(x => x!.Category)
            .Include(x => x.Product)!.ThenInclude(x => x!.Manufacturer)
            .Include(x => x.Branch).Include(x => x.Godown).Include(x => x.Supplier).AsQueryable();
        if (!canSelectBranch && actorBranchId.HasValue) query = query.Where(x => x.BranchId == actorBranchId);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        return query;
    }

    private static BatchListItemDto MapBatch(ProductBatch x, string productName, string sku, string branchName, DateOnly businessDate) =>
        new(x.Id, x.ProductId, productName, sku, x.BatchNumber, x.BranchId, branchName, x.ExpiryDate, x.QuantityAvailable,
            x.PurchasePrice, x.RetailPrice, x.QuantityAvailable * x.PurchasePrice, BatchStateFor(x, businessDate),
            x.GodownId, x.Godown?.Code, x.Godown?.Name);

    private static BatchState BatchStateFor(ProductBatch x, DateOnly businessDate)
    {
        if (x.IsDisposed) return BatchState.Disposed;
        if (x.QuantityAvailable <= 0) return BatchState.Depleted;
        if (x.ExpiryDate < businessDate) return BatchState.Expired;
        if (x.ExpiryDate <= businessDate.AddDays(30)) return BatchState.NearExpiry;
        return BatchState.Active;
    }

    private static InventoryStockStatus StockStatus(int quantity, int reorderLevel)
    {
        if (quantity <= 0) return InventoryStockStatus.OutOfStock;
        return quantity <= reorderLevel ? InventoryStockStatus.LowStock : InventoryStockStatus.Healthy;
    }
}
