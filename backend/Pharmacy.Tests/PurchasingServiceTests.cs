using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Purchasing;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Purchasing;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class PurchasingServiceTests
{
    [Fact]
    public async Task Create_submit_and_cancel_purchase_order_follow_state_rules()
    {
        var f = new Fixture(PermissionCatalog.PurchaseOrdersCreate, PermissionCatalog.PurchaseOrdersUpdate, PermissionCatalog.PurchaseOrdersCancel);
        var order = await f.Service.CreatePurchaseOrderAsync(f.Actor.Id, f.OrderRequest(100));
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
        Assert.Single(f.Orders);
        order = await f.Service.SubmitPurchaseOrderAsync(f.Actor.Id, order.Id);
        Assert.Equal(PurchaseOrderStatus.Submitted, order.Status);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.UpdatePurchaseOrderAsync(f.Actor.Id, order.Id, f.OrderRequest(50)));
        order = await f.Service.CancelPurchaseOrderAsync(f.Actor.Id, order.Id);
        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
        Assert.Contains(f.Audits, x => x.Action == "PurchaseOrderSubmitted");
    }

    [Fact]
    public async Task Direct_purchase_posts_inventory_stock_movement_supplier_ledger_and_bonus_correctly()
    {
        var f = new Fixture(PermissionCatalog.PurchasesReceive, PermissionCatalog.PurchasesCreate);
        var receipt = await f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.DirectRequest(100, 10, 50, 5, 2));
        Assert.Equal(5000, receipt.Subtotal);
        Assert.Equal(250, receipt.DiscountTotal);
        Assert.Equal(95, receipt.TaxTotal);
        Assert.Equal(4845, receipt.NetTotal);
        Assert.Equal(110, f.Batches.Single().QuantityAvailable);
        Assert.Equal(110, f.Inventory.Single().QuantityInStock);
        Assert.Contains(f.Movements, x => x.MovementType == StockMovementType.Purchase && x.Quantity == 110);
        Assert.Contains(f.Ledger, x => x.EntryType == SupplierLedgerEntryType.Purchase && x.Amount == 4845);
        Assert.Contains(f.Audits, x => x.Action == "DirectPurchasePosted");
    }

    [Fact]
    public async Task Direct_purchase_rejects_duplicate_invoice_expired_batch_and_conflicting_batch()
    {
        var f = new Fixture(PermissionCatalog.PurchasesReceive, PermissionCatalog.PurchasesCreate);
        await f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.DirectRequest(10, 0, 10, 0, 0, invoice: "INV-1"));
        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.DirectRequest(10, 0, 10, 0, 0, invoice: " inv-1 ")));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.DirectRequest(10, 0, 10, 0, 0, batch: "B-OLD", expiry: f.Today.AddDays(-1))));
        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.DirectRequest(10, 0, 11, 0, 0, batch: "B-1")));
    }

    [Fact]
    public async Task Ordered_purchase_supports_partial_receiving_completion_and_rejects_over_receipt()
    {
        var f = new Fixture(PermissionCatalog.PurchaseOrdersCreate, PermissionCatalog.PurchaseOrdersUpdate, PermissionCatalog.PurchaseOrdersView, PermissionCatalog.PurchasesReceive);
        var order = await f.Service.CreatePurchaseOrderAsync(f.Actor.Id, f.OrderRequest(100));
        order = await f.Service.SubmitPurchaseOrderAsync(f.Actor.Id, order.Id);
        var itemId = order.Items.Single().Id;
        var first = await f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.OrderedReceipt(order.Id, itemId, 60, "B-PO-A"));
        Assert.Equal(60, first.Items.Single().InventoryQuantity);
        order = await f.Service.GetPurchaseOrderAsync(f.Actor.Id, order.Id);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        await f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.OrderedReceipt(order.Id, itemId, 40, "B-PO-B"));
        order = await f.Service.GetPurchaseOrderAsync(f.Actor.Id, order.Id);
        Assert.Equal(PurchaseOrderStatus.Completed, order.Status);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.OrderedReceipt(order.Id, itemId, 1, "B-PO-C")));
    }

    [Fact]
    public async Task Posting_rolls_back_when_one_line_fails()
    {
        var f = new Fixture(PermissionCatalog.PurchasesReceive, PermissionCatalog.PurchasesCreate) { FailOnSave = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.DirectRequest(10, 1, 20, 0, 0)));
        Assert.Empty(f.Receipts);
        Assert.Empty(f.Batches);
        Assert.Empty(f.Inventory);
        Assert.Empty(f.Movements);
        Assert.Empty(f.Ledger);
    }

    [Fact]
    public async Task Missing_purchase_permission_is_forbidden()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.DirectRequest(10, 0, 10, 0, 0)));
    }

    [Fact]
    public async Task Direct_purchase_requires_create_and_receive_permissions()
    {
        var receiveOnly = new Fixture(PermissionCatalog.PurchasesReceive);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => receiveOnly.Service.PostGoodsReceiptAsync(receiveOnly.Actor.Id, receiveOnly.DirectRequest(10, 0, 10, 0, 0)));

        var createOnly = new Fixture(PermissionCatalog.PurchasesCreate);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => createOnly.Service.PostGoodsReceiptAsync(createOnly.Actor.Id, createOnly.DirectRequest(10, 0, 10, 0, 0)));
    }

    private sealed class Fixture : IPurchasingRepository
    {
        public readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        public readonly Branch Branch = new() { Code = "MAIN", Name = "Main" };
        public readonly Supplier Supplier = new() { Name = "ABC Pharma", NormalizedName = "ABC PHARMA", IsActive = true };
        public readonly ProductCategory Category = new() { Name = "Medicine", NormalizedName = "MEDICINE" };
        public readonly Product Product;
        public readonly User Actor;
        public readonly List<PurchaseOrder> Orders = [];
        public readonly List<GoodsReceipt> Receipts = [];
        public readonly List<ProductBatch> Batches = [];
        public readonly List<Pharmacy.Domain.Entities.Inventory> Inventory = [];
        public readonly List<StockMovement> Movements = [];
        public readonly List<SupplierLedgerEntry> Ledger = [];
        public readonly List<AuditLog> Audits = [];
        public bool FailOnSave;
        public PurchasingService Service { get; }

        public Fixture(params string[] permissions)
        {
            Product = new Product { SKU = "SKU-1", NormalizedSku = "SKU-1", Name = "Panadol", CategoryId = Category.Id, Category = Category, Unit = "Tablet", PackSize = 1, PurchasePrice = 50, RetailPrice = 60, MaximumDiscountPercent = 0, ReorderLevel = 5, IsActive = true };
            var role = new Role { Name = RoleCatalog.Manager };
            foreach (var permission in permissions) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "actor", NormalizedUsername = "ACTOR", FullName = "Actor", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role };
            Service = new(this, TimeProvider.System);
        }

        public PurchaseOrderRequest OrderRequest(int quantity) => new(Branch.Id, Supplier.Id, Today, Today.AddDays(3), null, null, [new(Product.Id, quantity, 50, null)]);
        public GoodsReceiptRequest DirectRequest(int paid, int bonus, decimal price, decimal discount, decimal tax, string? invoice = null, string batch = "B-1", DateOnly? expiry = null) =>
            new(Branch.Id, Supplier.Id, null, invoice, Today, "direct", [new(Product.Id, null, batch, null, expiry ?? Today.AddDays(365), paid, bonus, price, 60, discount, tax)]);
        public GoodsReceiptRequest OrderedReceipt(Guid orderId, Guid orderItemId, int paid, string batch) =>
            new(Branch.Id, Supplier.Id, orderId, Unique("inv"), Today, "ordered", [new(Product.Id, orderItemId, batch, null, Today.AddDays(365), paid, 0, 50, 60, 0, 0)]);

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(Branch.Id == branchId ? Branch : null);
        public Task<Supplier?> GetSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default) => Task.FromResult<Supplier?>(Supplier.Id == supplierId ? Supplier : null);
        public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(Product.Id == productId ? Product : null);
        public Task<ProductBatch?> GetBatchByNumberAsync(Guid branchId, Guid productId, string batchNumber, CancellationToken cancellationToken = default) => Task.FromResult<ProductBatch?>(Batches.FirstOrDefault(x => x.BranchId == branchId && x.ProductId == productId && x.BatchNumber == batchNumber));
        public Task<Pharmacy.Domain.Entities.Inventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult<Pharmacy.Domain.Entities.Inventory?>(Inventory.FirstOrDefault(x => x.BranchId == branchId && x.ProductId == productId && x.ProductBatchId == batchId));
        public Task<PurchaseOrder?> GetPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PurchaseOrder?>(Orders.FirstOrDefault(x => x.Id == id));
        public Task<GoodsReceipt?> GetGoodsReceiptAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<GoodsReceipt?>(Receipts.FirstOrDefault(x => x.Id == id));
        public Task<bool> SupplierInvoiceExistsAsync(Guid supplierId, string normalizedInvoiceNumber, CancellationToken cancellationToken = default) => Task.FromResult(Receipts.Any(x => x.SupplierId == supplierId && x.NormalizedSupplierInvoiceNumber == normalizedInvoiceNumber));
        public Task<string> NextPurchaseOrderNumberAsync(DateOnly orderDate, CancellationToken cancellationToken = default) => Task.FromResult($"PO-{orderDate.Year}-{Orders.Count + 1:000000}");
        public Task<string> NextGrnNumberAsync(DateOnly receiptDate, CancellationToken cancellationToken = default) => Task.FromResult($"GRN-{receiptDate.Year}-{Receipts.Count + 1:000000}");
        public Task AddPurchaseOrderAsync(PurchaseOrder order, CancellationToken cancellationToken = default) { Orders.Add(order); return Task.CompletedTask; }
        public Task AddGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken cancellationToken = default) { Receipts.Add(receipt); return Task.CompletedTask; }
        public Task AddBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default) { Batches.Add(batch); return Task.CompletedTask; }
        public Task AddInventoryAsync(Pharmacy.Domain.Entities.Inventory inventory, CancellationToken cancellationToken = default) { Inventory.Add(inventory); return Task.CompletedTask; }
        public Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default) { Movements.Add(movement); return Task.CompletedTask; }
        public Task AddSupplierLedgerEntryAsync(SupplierLedgerEntry entry, CancellationToken cancellationToken = default) { Ledger.Add(entry); return Task.CompletedTask; }
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public Task<PagedResult<PurchaseOrderListItemDto>> ListPurchaseOrdersAsync(PurchaseOrderListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<PurchaseOrderListItemDto>([], query.Page, query.PageSize, 0));
        public Task<PurchaseOrderDetailsDto?> GetPurchaseOrderDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(MapOrder(Orders.FirstOrDefault(x => x.Id == id)));
        public Task<PagedResult<PurchaseHistoryItemDto>> ListPurchasesAsync(PurchaseHistoryQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<PurchaseHistoryItemDto>([], query.Page, query.PageSize, 0));
        public Task<GoodsReceiptDetailsDto?> GetGoodsReceiptDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(MapReceipt(Receipts.FirstOrDefault(x => x.Id == id)));
        public Task<PurchasingOptionsDto> GetOptionsAsync(string? productSearch, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PurchasingOptionsDto([], [], []));
        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
        {
            var snapshot = Snapshot();
            try { await operation(cancellationToken); }
            catch { Restore(snapshot); throw; }
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailOnSave) throw new InvalidOperationException("forced failure");
            return Task.CompletedTask;
        }

        private PurchaseOrderDetailsDto? MapOrder(PurchaseOrder? order) => order is null ? null : new(order.Id, order.OrderNumber, order.OrderDate, order.ExpectedDate, order.SupplierId, Supplier.Name, order.BranchId, Branch.Name, order.SupplierReference, order.Status, order.Notes, order.CreatedAt, order.UpdatedAt,
            order.Items.Select(x => new PurchaseOrderItemDto(x.Id, x.ProductId, Product.Name, Product.SKU, x.OrderedQuantity, x.ReceivedQuantity, x.OrderedQuantity - x.ReceivedQuantity, x.ExpectedPurchasePrice, x.Notes)).ToList());
        private GoodsReceiptDetailsDto? MapReceipt(GoodsReceipt? receipt) => receipt is null ? null : new(receipt.Id, receipt.GrnNumber, receipt.SupplierInvoiceNumber, receipt.ReceiptDate, receipt.SupplierId, Supplier.Name, receipt.BranchId, Branch.Name, Orders.FirstOrDefault(x => x.Id == receipt.PurchaseOrderId)?.OrderNumber, receipt.Status, receipt.Subtotal, receipt.DiscountTotal, receipt.TaxTotal, receipt.NetTotal, receipt.Notes, receipt.CreatedAt, receipt.UpdatedAt,
            receipt.Items.Select(x => new GoodsReceiptItemDto(x.Id, x.ProductId, Product.Name, Product.SKU, x.PurchaseOrderItemId, x.BatchNumber, x.ManufacturingDate, x.ExpiryDate, x.PurchasedQuantity, x.BonusQuantity, x.PurchasedQuantity + x.BonusQuantity, x.PurchasePrice, x.RetailPrice, x.DiscountPercent, x.DiscountAmount, x.TaxPercent, x.TaxAmount, x.NetLineAmount)).ToList());
        private (List<PurchaseOrder> Orders, List<GoodsReceipt> Receipts, List<ProductBatch> Batches, List<Pharmacy.Domain.Entities.Inventory> Inventory, List<StockMovement> Movements, List<SupplierLedgerEntry> Ledger, List<AuditLog> Audits) Snapshot() =>
            (Orders.ToList(), Receipts.ToList(), Batches.ToList(), Inventory.ToList(), Movements.ToList(), Ledger.ToList(), Audits.ToList());
        private void Restore((List<PurchaseOrder> Orders, List<GoodsReceipt> Receipts, List<ProductBatch> Batches, List<Pharmacy.Domain.Entities.Inventory> Inventory, List<StockMovement> Movements, List<SupplierLedgerEntry> Ledger, List<AuditLog> Audits) s)
        {
            Orders.Clear(); Orders.AddRange(s.Orders); Receipts.Clear(); Receipts.AddRange(s.Receipts); Batches.Clear(); Batches.AddRange(s.Batches);
            Inventory.Clear(); Inventory.AddRange(s.Inventory); Movements.Clear(); Movements.AddRange(s.Movements); Ledger.Clear(); Ledger.AddRange(s.Ledger); Audits.Clear(); Audits.AddRange(s.Audits);
        }
        private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
    }
}
