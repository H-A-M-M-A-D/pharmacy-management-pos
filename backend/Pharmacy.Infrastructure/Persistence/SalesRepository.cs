using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.Sales;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class SalesRepository(PharmacyDbContext context) : ISalesRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);
    public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) => context.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
    public async Task<IReadOnlyList<ProductBatch>> GetEligibleBatchesAsync(Guid branchId, Guid productId, CancellationToken cancellationToken = default) =>
        await context.ProductBatches.Where(x => x.BranchId == branchId && x.ProductId == productId).OrderBy(x => x.ExpiryDate).ThenBy(x => x.CreatedAt).ThenBy(x => x.BatchNumber).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    public Task<Inventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default) =>
        context.Inventory.FirstOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == productId && x.ProductBatchId == batchId, cancellationToken);
    public Task<Sale?> GetSaleAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Sales.Include(x => x.Items).ThenInclude(x => x.Allocations).Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task<string> NextInvoiceNumberAsync(DateTime postedAtUtc, CancellationToken cancellationToken = default) =>
        $"INV-{postedAtUtc.Year}-{await context.Sales.CountAsync(x => x.PostedAtUtc != null && x.PostedAtUtc.Value.Year == postedAtUtc.Year, cancellationToken) + 1:000000}";
    public async Task<string> NextHoldNumberAsync(DateTime createdAtUtc, CancellationToken cancellationToken = default) =>
        $"HOLD-{createdAtUtc.Year}-{await context.Sales.CountAsync(x => x.HoldNumber != null && x.CreatedAt.Year == createdAtUtc.Year, cancellationToken) + 1:000000}";
    public async Task AddSaleAsync(Sale sale, CancellationToken cancellationToken = default) => await context.Sales.AddAsync(sale, cancellationToken);
    public async Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default) => await context.StockMovements.AddAsync(movement, cancellationToken);
    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task<IReadOnlyList<PosProductDto>> SearchProductsAsync(PosProductSearchQuery query, Guid actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var branchId = query.BranchId ?? actorBranchId;
        var term = query.Q?.Trim();
        var normalized = term?.ToUpperInvariant();
        var products = context.Products.AsNoTracking().Where(x => x.IsActive).AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term}%";
            products = products.Where(x => x.NormalizedBarcode == normalized || x.NormalizedSku == normalized || EF.Functions.ILike(x.Name, pattern) || (x.BrandName != null && EF.Functions.ILike(x.BrandName, pattern)) || (x.GenericName != null && EF.Functions.ILike(x.GenericName, pattern)));
        }
        var rows = await products.Select(x => new
        {
            Product = x,
            Batches = x.ProductBatches.Where(b => b.BranchId == branchId && b.QuantityAvailable > 0 && !b.IsDisposed && b.ExpiryDate >= businessDate)
                .OrderBy(b => b.ExpiryDate).ThenBy(b => b.CreatedAt).ThenBy(b => b.BatchNumber).Select(b => new { b.QuantityAvailable, b.ExpiryDate, b.RetailPrice }).ToList()
        })
        .OrderByDescending(x => !string.IsNullOrWhiteSpace(term) && x.Product.NormalizedBarcode == normalized)
        .ThenByDescending(x => !string.IsNullOrWhiteSpace(term) && x.Product.NormalizedSku == normalized)
        .ThenBy(x => x.Product.Name).Take(query.Take).ToListAsync(cancellationToken);
        return rows.Select(x => new PosProductDto(x.Product.Id, x.Product.Name, x.Product.SKU, x.Product.Barcode, x.Product.GenericName, x.Product.BrandName, x.Product.Unit, x.Batches.Sum(b => b.QuantityAvailable), x.Batches.Select(b => (DateOnly?)b.ExpiryDate).FirstOrDefault(), x.Batches.Select(b => (decimal?)b.RetailPrice).FirstOrDefault(), x.Product.MaximumDiscountPercent, x.Product.IsActive)).ToList();
    }

    public Task<PagedResult<SaleListItemDto>> ListSalesAsync(SalesHistoryQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) =>
        ListSalesCore(query, actorBranchId, canSelectBranch, false, cancellationToken);
    public Task<PagedResult<SaleListItemDto>> ListHeldSalesAsync(HeldSalesQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) =>
        ListSalesCore(new SalesHistoryQuery(query.Page, query.PageSize, query.Search, query.BranchId, null, SaleStatus.Held), actorBranchId, canSelectBranch, true, cancellationToken);

    private async Task<PagedResult<SaleListItemDto>> ListSalesCore(SalesHistoryQuery query, Guid? actorBranchId, bool canSelectBranch, bool heldOnly, CancellationToken cancellationToken)
    {
        var sales = context.Sales.AsNoTracking().Include(x => x.Branch).Include(x => x.CashierUser).Include(x => x.Items).Include(x => x.Payments).AsQueryable();
        if (!canSelectBranch && actorBranchId.HasValue) sales = sales.Where(x => x.BranchId == actorBranchId);
        if (query.BranchId.HasValue) sales = sales.Where(x => x.BranchId == query.BranchId);
        if (query.CashierUserId.HasValue) sales = sales.Where(x => x.CashierUserId == query.CashierUserId);
        if (heldOnly) sales = sales.Where(x => x.Status == SaleStatus.Held); else if (query.Status.HasValue) sales = sales.Where(x => x.Status == query.Status);
        if (query.PaymentMethod.HasValue) sales = sales.Where(x => x.Payments.Any(p => p.Method == query.PaymentMethod));
        if (query.FromUtc.HasValue) sales = sales.Where(x => (x.PostedAtUtc ?? x.CreatedAt) >= query.FromUtc);
        if (query.ToUtc.HasValue) sales = sales.Where(x => (x.PostedAtUtc ?? x.CreatedAt) <= query.ToUtc);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            sales = sales.Where(x => (x.InvoiceNumber != null && EF.Functions.ILike(x.InvoiceNumber, pattern)) || (x.HoldNumber != null && EF.Functions.ILike(x.HoldNumber, pattern)) || (x.CustomerName != null && EF.Functions.ILike(x.CustomerName, pattern)) || (x.CustomerPhone != null && EF.Functions.ILike(x.CustomerPhone, pattern)));
        }
        var total = await sales.CountAsync(cancellationToken);
        var rows = await sales.OrderByDescending(x => x.PostedAtUtc ?? x.CreatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var saleIds = rows.Select(x => x.Id).ToArray();
        var returnedBySale = await context.SalesReturnAllocations.AsNoTracking()
            .Where(x => saleIds.Contains(x.SalesReturnItem!.SalesReturn!.OriginalSaleId) && x.SalesReturnItem.SalesReturn.Status == SalesReturnStatus.Posted)
            .GroupBy(x => x.SalesReturnItem!.SalesReturn!.OriginalSaleId)
            .Select(x => new { SaleId = x.Key, Quantity = x.Sum(a => a.Quantity) })
            .ToDictionaryAsync(x => x.SaleId, x => x.Quantity, cancellationToken);
        var items = rows.Select(x => new SaleListItemDto(
                x.Id,
                x.InvoiceNumber,
                x.HoldNumber,
                x.Status,
                x.CreatedAt,
                x.PostedAtUtc,
                x.BranchId,
                x.Branch!.Name,
                x.CashierUser!.FullName,
                x.CustomerName,
                x.CustomerPhone,
                x.Items.Count,
                x.NetTotal,
                x.AmountPaid,
                x.ChangeGiven,
                string.Join(", ", x.Payments.Select(p => $"{p.Method}:{p.AmountApplied}")),
                ReturnState(x.Items.Sum(i => i.RequestedQuantity), returnedBySale.GetValueOrDefault(x.Id))))
            .ToList();
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<SaleDetailsDto?> GetSaleDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var sale = await context.Sales.AsNoTracking().Include(x => x.Branch).Include(x => x.CashierUser)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Items).ThenInclude(x => x.Allocations).ThenInclude(x => x.ProductBatch)
            .Include(x => x.Payments)
            .Where(x => x.Id == id && (canSelectBranch || x.BranchId == actorBranchId)).FirstOrDefaultAsync(cancellationToken);
        if (sale is null) return null;
        var items = sale.Items.OrderBy(x => x.CreatedAt).Select(item =>
        {
            var allocations = item.Allocations.OrderBy(x => x.ExpiryDateSnapshot).ThenBy(x => x.ProductBatch!.BatchNumber).Select(a => new SaleItemAllocationDto(a.Id, a.ProductBatchId, a.ProductBatch!.BatchNumber, a.ExpiryDateSnapshot, a.Quantity, a.UnitRetailPriceSnapshot, a.UnitSalePriceSnapshot, a.GrossAmount, a.DiscountAmount, a.NetAmount)).ToList();
            return new SaleItemDto(item.Id, item.ProductId, item.Product!.Name, item.Product.SKU, item.RequestedQuantity, item.DiscountPercent, item.GrossAmount, item.DiscountAmount, item.TaxAmount, item.NetAmount, allocations.Select(x => x.UnitRetailPriceSnapshot).Distinct().Count() > 1, allocations);
        }).ToList();
        var payments = sale.Payments.OrderBy(x => x.CreatedAt).Select(x => new SalePaymentDto(x.Id, x.Method, x.AmountApplied, x.TenderedAmount, x.ReferenceNumber)).ToList();
        return new SaleDetailsDto(sale.Id, sale.InvoiceNumber, sale.HoldNumber, sale.Status, sale.CreatedAt, sale.PostedAtUtc, sale.BranchId, sale.Branch!.Name, sale.Branch.Address, sale.Branch.PhoneNumber, sale.CashierUserId, sale.CashierUser!.FullName, sale.CustomerName, sale.CustomerPhone, sale.Subtotal, sale.DiscountTotal, sale.TaxTotal, sale.NetTotal, sale.AmountPaid, sale.ChangeGiven, sale.Notes, items, payments);
    }

    private static SalesReturnState ReturnState(int soldQuantity, int returnedQuantity) =>
        returnedQuantity <= 0 ? SalesReturnState.NotReturned : returnedQuantity >= soldQuantity ? SalesReturnState.FullyReturned : SalesReturnState.PartiallyReturned;

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
            throw new ResourceConflictException("A sale with this unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Sale constraints were violated.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            throw new ResourceConflictException("Stock changed while completing the sale. The cart has been refreshed.");
        }
    }
}
