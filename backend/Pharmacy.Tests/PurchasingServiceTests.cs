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


    [Fact]
    public async Task Purchase_return_paid_bonus_and_mixed_update_stock_and_supplier_credit()
    {
        var f = new Fixture(PermissionCatalog.PurchasesReceive, PermissionCatalog.PurchasesCreate, PermissionCatalog.PurchaseReturnsCreate, PermissionCatalog.PurchaseReturnsView);
        var receipt = await f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.DirectRequest(100, 10, 50, 0, 0));
        var itemId = f.Receipts.Single().Items.Single().Id;

        var paid = await f.Service.PostPurchaseReturnAsync(f.Actor.Id, receipt.Id, new(PurchaseReturnReason.Damaged, null, [new(itemId, 20, 0)]));
        Assert.Equal(1000, paid.NetSupplierCredit);
        Assert.Equal(90, f.Batches.Single().QuantityAvailable);
        Assert.Contains(f.Movements, x => x.MovementType == StockMovementType.PurchaseReturn && x.Quantity == -20);
        Assert.Contains(f.Ledger, x => x.EntryType == SupplierLedgerEntryType.PurchaseReturn && x.Amount == -1000);

        var bonus = await f.Service.PostPurchaseReturnAsync(f.Actor.Id, receipt.Id, new(PurchaseReturnReason.ExcessSupply, null, [new(itemId, 0, 5)]));
        Assert.Equal(0, bonus.NetSupplierCredit);
        Assert.Equal(85, f.Batches.Single().QuantityAvailable);
        Assert.DoesNotContain(f.Ledger, x => x.ReferenceId == bonus.Id);

        var mixed = await f.Service.PostPurchaseReturnAsync(f.Actor.Id, receipt.Id, new(PurchaseReturnReason.WrongItem, null, [new(itemId, 10, 5)]));
        Assert.Equal(500, mixed.NetSupplierCredit);
        Assert.Equal(70, f.Batches.Single().QuantityAvailable);
    }

    [Fact]
    public async Task Purchase_return_rejects_over_paid_over_bonus_and_physical_shortage()
    {
        var f = new Fixture(PermissionCatalog.PurchasesReceive, PermissionCatalog.PurchasesCreate, PermissionCatalog.PurchaseReturnsCreate);
        var receipt = await f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.DirectRequest(100, 10, 50, 0, 0));
        var itemId = f.Receipts.Single().Items.Single().Id;
        await f.Service.PostPurchaseReturnAsync(f.Actor.Id, receipt.Id, new(PurchaseReturnReason.Damaged, null, [new(itemId, 90, 8)]));
        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.PostPurchaseReturnAsync(f.Actor.Id, receipt.Id, new(PurchaseReturnReason.Damaged, null, [new(itemId, 20, 0)])));
        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.PostPurchaseReturnAsync(f.Actor.Id, receipt.Id, new(PurchaseReturnReason.Damaged, null, [new(itemId, 1, 3)])));
        f.Batches.Single().QuantityAvailable = 1;
        f.Inventory.Single().QuantityInStock = 1;
        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.PostPurchaseReturnAsync(f.Actor.Id, receipt.Id, new(PurchaseReturnReason.Damaged, null, [new(itemId, 1, 1)])));
    }

    [Fact]
    public async Task Purchase_return_uses_original_batch_and_original_discount_rounding()
    {
        var f = new Fixture(PermissionCatalog.PurchasesReceive, PermissionCatalog.PurchasesCreate, PermissionCatalog.PurchaseReturnsCreate);
        var receipt = await f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.DirectRequest(3, 0, 33.333m, 0, 0, batch: "B-ORIGINAL"));
        var originalBatch = f.Batches.Single();
        f.Batches.Add(new ProductBatch { BranchId = f.Branch.Id, ProductId = f.Product.Id, SupplierId = f.Supplier.Id, BatchNumber = "B-EARLY", ExpiryDate = f.Today.AddDays(10), PurchasePrice = 99, RetailPrice = 120, QuantityReceived = 10, QuantityAvailable = 10 });
        var itemId = f.Receipts.Single().Items.Single().Id;
        var first = await f.Service.PostPurchaseReturnAsync(f.Actor.Id, receipt.Id, new(PurchaseReturnReason.Damaged, null, [new(itemId, 1, 0)]));
        var second = await f.Service.PostPurchaseReturnAsync(f.Actor.Id, receipt.Id, new(PurchaseReturnReason.Damaged, null, [new(itemId, 2, 0)]));
        Assert.Equal(0, originalBatch.QuantityAvailable);
        Assert.Equal(10, f.Batches.Single(x => x.BatchNumber == "B-EARLY").QuantityAvailable);
        Assert.Equal(100.00m, first.NetSupplierCredit + second.NetSupplierCredit);
    }

    [Fact]
    public async Task Purchase_return_allows_inactive_supplier_product_and_expired_physical_stock()
    {
        var f = new Fixture(PermissionCatalog.PurchasesReceive, PermissionCatalog.PurchasesCreate, PermissionCatalog.PurchaseReturnsCreate);
        var receipt = await f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.DirectRequest(10, 0, 50, 0, 0));
        f.Supplier.IsActive = false;
        f.Product.IsActive = false;
        f.Batches.Single().ExpiryDate = f.Today.AddDays(-1);
        var itemId = f.Receipts.Single().Items.Single().Id;
        var posted = await f.Service.PostPurchaseReturnAsync(f.Actor.Id, receipt.Id, new(PurchaseReturnReason.Expired, null, [new(itemId, 1, 0)]));
        Assert.Equal(50, posted.NetSupplierCredit);
    }

    [Fact]
    public async Task Purchase_return_does_not_modify_original_grn_or_purchase_order()
    {
        var f = new Fixture(PermissionCatalog.PurchaseOrdersCreate, PermissionCatalog.PurchaseOrdersUpdate, PermissionCatalog.PurchaseOrdersView, PermissionCatalog.PurchasesReceive, PermissionCatalog.PurchaseReturnsCreate);
        var order = await f.Service.CreatePurchaseOrderAsync(f.Actor.Id, f.OrderRequest(10));
        order = await f.Service.SubmitPurchaseOrderAsync(f.Actor.Id, order.Id);
        var receipt = await f.Service.PostGoodsReceiptAsync(f.Actor.Id, f.OrderedReceipt(order.Id, order.Items.Single().Id, 10, "B-PO-R"));
        var item = f.Receipts.Single().Items.Single();
        await f.Service.PostPurchaseReturnAsync(f.Actor.Id, receipt.Id, new(PurchaseReturnReason.Damaged, null, [new(item.Id, 2, 0)]));
        Assert.Equal(10, item.PurchasedQuantity);
        Assert.Equal(PurchaseOrderStatus.Completed, f.Orders.Single().Status);
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
        public readonly List<PurchaseReturn> PurchaseReturns = [];
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
        public Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult<ProductBatch?>(Batches.FirstOrDefault(x => x.Id == batchId));
        public Task<bool> SupplierInvoiceExistsAsync(Guid supplierId, string normalizedInvoiceNumber, CancellationToken cancellationToken = default) => Task.FromResult(Receipts.Any(x => x.SupplierId == supplierId && x.NormalizedSupplierInvoiceNumber == normalizedInvoiceNumber));
        public Task<string> NextPurchaseOrderNumberAsync(DateOnly orderDate, CancellationToken cancellationToken = default) => Task.FromResult($"PO-{orderDate.Year}-{Orders.Count + 1:000000}");
        public Task<string> NextGrnNumberAsync(DateOnly receiptDate, CancellationToken cancellationToken = default) => Task.FromResult($"GRN-{receiptDate.Year}-{Receipts.Count + 1:000000}");
        public Task<string> NextPurchaseReturnNumberAsync(DateTime returnDateUtc, CancellationToken cancellationToken = default) => Task.FromResult($"PR-{returnDateUtc.Year}-{PurchaseReturns.Count + 1:000000}");
        public Task AddPurchaseOrderAsync(PurchaseOrder order, CancellationToken cancellationToken = default) { Orders.Add(order); return Task.CompletedTask; }
        public Task AddGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken cancellationToken = default) { Receipts.Add(receipt); return Task.CompletedTask; }
        public Task AddPurchaseReturnAsync(PurchaseReturn purchaseReturn, CancellationToken cancellationToken = default) { PurchaseReturns.Add(purchaseReturn); return Task.CompletedTask; }
        public Task AddBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default) { Batches.Add(batch); return Task.CompletedTask; }
        public Task AddInventoryAsync(Pharmacy.Domain.Entities.Inventory inventory, CancellationToken cancellationToken = default) { Inventory.Add(inventory); return Task.CompletedTask; }
        public Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default) { Movements.Add(movement); return Task.CompletedTask; }
        public Task AddSupplierLedgerEntryAsync(SupplierLedgerEntry entry, CancellationToken cancellationToken = default) { Ledger.Add(entry); return Task.CompletedTask; }
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public Task<PagedResult<PurchaseOrderListItemDto>> ListPurchaseOrdersAsync(PurchaseOrderListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<PurchaseOrderListItemDto>([], query.Page, query.PageSize, 0));
        public Task<PurchaseOrderDetailsDto?> GetPurchaseOrderDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(MapOrder(Orders.FirstOrDefault(x => x.Id == id)));
        public Task<PagedResult<PurchaseHistoryItemDto>> ListPurchasesAsync(PurchaseHistoryQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<PurchaseHistoryItemDto>([], query.Page, query.PageSize, 0));
        public Task<GoodsReceiptDetailsDto?> GetGoodsReceiptDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(MapReceipt(Receipts.FirstOrDefault(x => x.Id == id)));
        public Task<ReturnableGoodsReceiptDto?> GetReturnableGoodsReceiptAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default) => Task.FromResult(MapReturnable(Receipts.FirstOrDefault(x => x.Id == id), businessDate));
        public Task<Dictionary<Guid, (int PaidQuantity, int BonusQuantity, decimal Gross, decimal Discount, decimal Tax, decimal Net)>> GetPurchaseReturnTotalsAsync(Guid goodsReceiptId, CancellationToken cancellationToken = default) => Task.FromResult(PurchaseReturns.Where(x => x.OriginalGoodsReceiptId == goodsReceiptId).SelectMany(x => x.Items).GroupBy(x => x.OriginalGoodsReceiptItemId).ToDictionary(g => g.Key, g => (g.Sum(x => x.PaidReturnQuantity), g.Sum(x => x.BonusReturnQuantity), g.Sum(x => x.GrossReturnAmount), g.Sum(x => x.DiscountAdjustment), g.Sum(x => x.TaxAdjustment), g.Sum(x => x.NetSupplierCredit))));
        public Task<PagedResult<PurchaseReturnListItemDto>> ListPurchaseReturnsAsync(PurchaseReturnListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<PurchaseReturnListItemDto>(PurchaseReturns.Select(MapReturnList).ToList(), query.Page, query.PageSize, PurchaseReturns.Count));
        public Task<PurchaseReturnDetailsDto?> GetPurchaseReturnDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(MapReturn(PurchaseReturns.FirstOrDefault(x => x.Id == id)));
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
        private ReturnableGoodsReceiptDto? MapReturnable(GoodsReceipt? receipt, DateOnly businessDate) => receipt is null ? null : new(receipt.Id, receipt.GrnNumber, receipt.SupplierInvoiceNumber, receipt.ReceiptDate, receipt.SupplierId, Supplier.Name, receipt.BranchId, Branch.Name, receipt.NetTotal, PurchaseReturns.Count == 0 ? PurchaseReturnState.NoReturns : PurchaseReturnState.PartiallyReturned, receipt.Items.Select(x => { var prior = PurchaseReturns.SelectMany(r => r.Items).Where(r => r.OriginalGoodsReceiptItemId == x.Id).ToList(); var batch = Batches.First(b => b.Id == x.ProductBatchId); var paidReturned = prior.Sum(r => r.PaidReturnQuantity); var bonusReturned = prior.Sum(r => r.BonusReturnQuantity); return new ReturnableGoodsReceiptItemDto(x.Id, x.ProductId, Product.Name, Product.SKU, batch.Id, x.BatchNumber, x.ExpiryDate, x.ExpiryDate < businessDate, batch.IsDisposed, x.PurchasedQuantity, paidReturned, x.PurchasedQuantity - paidReturned, x.BonusQuantity, bonusReturned, x.BonusQuantity - bonusReturned, batch.QuantityAvailable, x.PurchasePrice, x.PurchasedQuantity * x.PurchasePrice - prior.Sum(r => r.GrossReturnAmount), x.DiscountAmount - prior.Sum(r => r.DiscountAdjustment), x.TaxAmount - prior.Sum(r => r.TaxAdjustment), x.NetLineAmount - prior.Sum(r => r.NetSupplierCredit), Math.Min(batch.QuantityAvailable, x.PurchasedQuantity + x.BonusQuantity - paidReturned - bonusReturned)); }).ToList());
        private PurchaseReturnListItemDto MapReturnList(PurchaseReturn x) => new(x.Id, x.ReturnNumber, Receipts.First(r => r.Id == x.OriginalGoodsReceiptId).GrnNumber, Receipts.First(r => r.Id == x.OriginalGoodsReceiptId).SupplierInvoiceNumber, x.ReturnDateUtc, x.SupplierId, Supplier.Name, x.BranchId, Branch.Name, x.Items.Sum(i => i.PaidReturnQuantity), x.Items.Sum(i => i.BonusReturnQuantity), x.Items.Sum(i => i.PaidReturnQuantity + i.BonusReturnQuantity), x.NetSupplierCredit, x.Status, x.Reason, Actor.FullName);
        private PurchaseReturnDetailsDto? MapReturn(PurchaseReturn? x) => x is null ? null : new(x.Id, x.ReturnNumber, x.OriginalGoodsReceiptId, Receipts.First(r => r.Id == x.OriginalGoodsReceiptId).GrnNumber, Receipts.First(r => r.Id == x.OriginalGoodsReceiptId).SupplierInvoiceNumber, x.SupplierId, Supplier.Name, x.BranchId, Branch.Name, x.ProcessedByUserId, Actor.FullName, x.ReturnDateUtc, x.Reason, x.Notes, x.GrossReturnAmount, x.DiscountAdjustment, x.TaxAdjustment, x.NetSupplierCredit, x.Status, x.Items.Select(i => new PurchaseReturnItemDto(i.Id, i.OriginalGoodsReceiptItemId, i.ProductId, Product.Name, Product.SKU, i.ProductBatchId, i.BatchNumber, i.ExpiryDate, i.PaidReturnQuantity, i.BonusReturnQuantity, i.PaidReturnQuantity + i.BonusReturnQuantity, i.PurchasePriceSnapshot, i.GrossReturnAmount, i.DiscountAdjustment, i.TaxAdjustment, i.NetSupplierCredit)).ToList());
        private (List<PurchaseOrder> Orders, List<GoodsReceipt> Receipts, List<PurchaseReturn> PurchaseReturns, List<ProductBatch> Batches, List<Pharmacy.Domain.Entities.Inventory> Inventory, List<StockMovement> Movements, List<SupplierLedgerEntry> Ledger, List<AuditLog> Audits) Snapshot() =>
            (Orders.ToList(), Receipts.ToList(), PurchaseReturns.ToList(), Batches.ToList(), Inventory.ToList(), Movements.ToList(), Ledger.ToList(), Audits.ToList());
        private void Restore((List<PurchaseOrder> Orders, List<GoodsReceipt> Receipts, List<PurchaseReturn> PurchaseReturns, List<ProductBatch> Batches, List<Pharmacy.Domain.Entities.Inventory> Inventory, List<StockMovement> Movements, List<SupplierLedgerEntry> Ledger, List<AuditLog> Audits) s)
        {
            Orders.Clear(); Orders.AddRange(s.Orders); Receipts.Clear(); Receipts.AddRange(s.Receipts); PurchaseReturns.Clear(); PurchaseReturns.AddRange(s.PurchaseReturns); Batches.Clear(); Batches.AddRange(s.Batches);
            Inventory.Clear(); Inventory.AddRange(s.Inventory); Movements.Clear(); Movements.AddRange(s.Movements); Ledger.Clear(); Ledger.AddRange(s.Ledger); Audits.Clear(); Audits.AddRange(s.Audits);
        }
        private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
    }
}
