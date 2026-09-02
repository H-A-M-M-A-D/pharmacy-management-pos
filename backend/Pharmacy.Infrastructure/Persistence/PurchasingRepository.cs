using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Purchasing;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.Purchasing;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class PurchasingRepository(PharmacyDbContext context) : IPurchasingRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);
    public Task<Supplier?> GetSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default) => context.Suppliers.FirstOrDefaultAsync(x => x.Id == supplierId, cancellationToken);
    public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) => context.Products.Include(x => x.Manufacturer).FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
    public Task<ProductBatch?> GetBatchByNumberAsync(Guid branchId, Guid productId, string batchNumber, CancellationToken cancellationToken = default) =>
        context.ProductBatches.FirstOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == productId && x.BatchNumber == batchNumber, cancellationToken);
    public Task<Inventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default) =>
        context.Inventory.FirstOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == productId && x.ProductBatchId == batchId, cancellationToken);
    public Task<PurchaseOrder?> GetPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.PurchaseOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<GoodsReceipt?> GetGoodsReceiptAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.GoodsReceipts.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<bool> SupplierInvoiceExistsAsync(Guid supplierId, string normalizedInvoiceNumber, CancellationToken cancellationToken = default) =>
        context.GoodsReceipts.AnyAsync(x => x.SupplierId == supplierId && x.NormalizedSupplierInvoiceNumber == normalizedInvoiceNumber, cancellationToken);
    public async Task<string> NextPurchaseOrderNumberAsync(DateOnly orderDate, CancellationToken cancellationToken = default) =>
        $"PO-{orderDate.Year}-{await context.PurchaseOrders.CountAsync(x => x.OrderDate.Year == orderDate.Year, cancellationToken) + 1:000000}";
    public async Task<string> NextGrnNumberAsync(DateOnly receiptDate, CancellationToken cancellationToken = default) =>
        $"GRN-{receiptDate.Year}-{await context.GoodsReceipts.CountAsync(x => x.ReceiptDate.Year == receiptDate.Year, cancellationToken) + 1:000000}";
    public async Task AddPurchaseOrderAsync(PurchaseOrder order, CancellationToken cancellationToken = default) => await context.PurchaseOrders.AddAsync(order, cancellationToken);
    public async Task AddGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken cancellationToken = default) => await context.GoodsReceipts.AddAsync(receipt, cancellationToken);
    public async Task AddBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default) => await context.ProductBatches.AddAsync(batch, cancellationToken);
    public async Task AddInventoryAsync(Inventory inventory, CancellationToken cancellationToken = default) => await context.Inventory.AddAsync(inventory, cancellationToken);
    public async Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default) => await context.StockMovements.AddAsync(movement, cancellationToken);
    public async Task AddSupplierLedgerEntryAsync(SupplierLedgerEntry entry, CancellationToken cancellationToken = default) => await context.SupplierLedgerEntries.AddAsync(entry, cancellationToken);
    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task<PagedResult<PurchaseOrderListItemDto>> ListPurchaseOrdersAsync(PurchaseOrderListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var orders = context.PurchaseOrders.AsNoTracking().Include(x => x.Branch).Include(x => x.Supplier).Include(x => x.Items).AsQueryable();
        if (!canSelectBranch && actorBranchId.HasValue) orders = orders.Where(x => x.BranchId == actorBranchId);
        if (query.BranchId.HasValue) orders = orders.Where(x => x.BranchId == query.BranchId);
        if (query.SupplierId.HasValue) orders = orders.Where(x => x.SupplierId == query.SupplierId);
        if (query.Status.HasValue) orders = orders.Where(x => x.Status == query.Status);
        if (query.DateFrom.HasValue) orders = orders.Where(x => x.OrderDate >= query.DateFrom);
        if (query.DateTo.HasValue) orders = orders.Where(x => x.OrderDate <= query.DateTo);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            orders = orders.Where(x => EF.Functions.ILike(x.OrderNumber, pattern) || (x.SupplierReference != null && EF.Functions.ILike(x.SupplierReference, pattern)) || EF.Functions.ILike(x.Supplier!.Name, pattern));
        }
        var total = await orders.CountAsync(cancellationToken);
        var items = await orders.OrderByDescending(x => x.OrderDate).ThenByDescending(x => x.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new PurchaseOrderListItemDto(x.Id, x.OrderNumber, x.OrderDate, x.ExpectedDate, x.SupplierId, x.Supplier!.Name, x.BranchId, x.Branch!.Name,
                x.Items.Count, x.Items.Sum(i => i.OrderedQuantity), x.Items.Sum(i => i.ReceivedQuantity), x.Status)).ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<PurchaseOrderDetailsDto?> GetPurchaseOrderDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var order = await context.PurchaseOrders.AsNoTracking().Include(x => x.Branch).Include(x => x.Supplier).Include(x => x.Items).ThenInclude(x => x.Product)
            .Where(x => x.Id == id && (canSelectBranch || x.BranchId == actorBranchId)).FirstOrDefaultAsync(cancellationToken);
        return order is null ? null : new PurchaseOrderDetailsDto(order.Id, order.OrderNumber, order.OrderDate, order.ExpectedDate,
            order.SupplierId, order.Supplier!.Name, order.BranchId, order.Branch!.Name, order.SupplierReference, order.Status, order.Notes,
            order.CreatedAt, order.UpdatedAt, order.Items.OrderBy(x => x.CreatedAt).Select(x => new PurchaseOrderItemDto(
                x.Id, x.ProductId, x.Product!.Name, x.Product.SKU, x.OrderedQuantity, x.ReceivedQuantity,
                x.OrderedQuantity - x.ReceivedQuantity, x.ExpectedPurchasePrice, x.Notes)).ToList());
    }

    public async Task<PagedResult<PurchaseHistoryItemDto>> ListPurchasesAsync(PurchaseHistoryQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var purchases = context.GoodsReceipts.AsNoTracking().Include(x => x.Branch).Include(x => x.Supplier).Include(x => x.ReceivedByUser).AsQueryable();
        if (!canSelectBranch && actorBranchId.HasValue) purchases = purchases.Where(x => x.BranchId == actorBranchId);
        if (query.BranchId.HasValue) purchases = purchases.Where(x => x.BranchId == query.BranchId);
        if (query.SupplierId.HasValue) purchases = purchases.Where(x => x.SupplierId == query.SupplierId);
        if (query.Status.HasValue) purchases = purchases.Where(x => x.Status == query.Status);
        if (query.DateFrom.HasValue) purchases = purchases.Where(x => x.ReceiptDate >= query.DateFrom);
        if (query.DateTo.HasValue) purchases = purchases.Where(x => x.ReceiptDate <= query.DateTo);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            purchases = purchases.Where(x => EF.Functions.ILike(x.GrnNumber, pattern) || (x.SupplierInvoiceNumber != null && EF.Functions.ILike(x.SupplierInvoiceNumber, pattern)) || EF.Functions.ILike(x.Supplier!.Name, pattern));
        }
        var total = await purchases.CountAsync(cancellationToken);
        var items = await purchases.OrderByDescending(x => x.ReceiptDate).ThenByDescending(x => x.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new PurchaseHistoryItemDto(x.Id, x.GrnNumber, x.SupplierInvoiceNumber, x.ReceiptDate,
                x.SupplierId, x.Supplier!.Name, x.BranchId, x.Branch!.Name, x.Subtotal, x.DiscountTotal, x.TaxTotal,
                x.NetTotal, x.Status, x.ReceivedByUser == null ? null : x.ReceivedByUser.FullName)).ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<GoodsReceiptDetailsDto?> GetGoodsReceiptDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var receipt = await context.GoodsReceipts.AsNoTracking().Include(x => x.Branch).Include(x => x.Supplier).Include(x => x.PurchaseOrder)
            .Include(x => x.Items).ThenInclude(x => x.Product).Where(x => x.Id == id && (canSelectBranch || x.BranchId == actorBranchId)).FirstOrDefaultAsync(cancellationToken);
        return receipt is null ? null : new GoodsReceiptDetailsDto(receipt.Id, receipt.GrnNumber, receipt.SupplierInvoiceNumber, receipt.ReceiptDate,
            receipt.SupplierId, receipt.Supplier!.Name, receipt.BranchId, receipt.Branch!.Name, receipt.PurchaseOrder == null ? null : receipt.PurchaseOrder.OrderNumber,
            receipt.Status, receipt.Subtotal, receipt.DiscountTotal, receipt.TaxTotal, receipt.NetTotal, receipt.Notes, receipt.CreatedAt, receipt.UpdatedAt,
            receipt.Items.OrderBy(x => x.CreatedAt).Select(x => new GoodsReceiptItemDto(x.Id, x.ProductId, x.Product!.Name, x.Product.SKU,
                x.PurchaseOrderItemId, x.BatchNumber, x.ManufacturingDate, x.ExpiryDate, x.PurchasedQuantity, x.BonusQuantity,
                x.PurchasedQuantity + x.BonusQuantity, x.PurchasePrice, x.RetailPrice, x.DiscountPercent, x.DiscountAmount,
                x.TaxPercent, x.TaxAmount, x.NetLineAmount)).ToList());
    }

    public async Task<PurchasingOptionsDto> GetOptionsAsync(string? productSearch, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => new(
        await context.Branches.AsNoTracking().Where(x => x.IsActive && (canSelectBranch || x.Id == actorBranchId)).OrderBy(x => x.Name).Select(x => new PurchasingLookupDto(x.Id, x.Name)).ToListAsync(cancellationToken),
        await context.Suppliers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new PurchasingLookupDto(x.Id, x.Name)).ToListAsync(cancellationToken),
        await context.Products.AsNoTracking().Include(x => x.Manufacturer).Where(x => x.IsActive)
            .Where(x => string.IsNullOrWhiteSpace(productSearch) || EF.Functions.ILike(x.Name, $"%{productSearch.Trim()}%") || EF.Functions.ILike(x.SKU, $"%{productSearch.Trim()}%") || (x.Barcode != null && EF.Functions.ILike(x.Barcode, $"%{productSearch.Trim()}%")) || (x.GenericName != null && EF.Functions.ILike(x.GenericName, $"%{productSearch.Trim()}%")))
            .OrderBy(x => x.Name).Take(50).Select(x => new PurchasingProductLookupDto(x.Id, x.Name, x.SKU, x.GenericName, x.Manufacturer == null ? null : x.Manufacturer.Name, x.PurchasePrice, x.RetailPrice)).ToListAsync(cancellationToken));

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
            throw new ResourceConflictException("A purchase document with this unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Purchase constraints were violated.");
        }
    }
}
