using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Inventory;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Inventory;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class InventoryManagementTests
{
    [Fact]
    public async Task Opening_stock_creates_batch_movement_inventory_and_audit()
    {
        var f = new Fixture(PermissionCatalog.InventoryOpeningStock);
        var result = await f.Service.AddOpeningStockAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, "B-1", null, f.Today.AddDays(30), 10, 8, 12, null, "initial"));
        Assert.Equal(10, f.Batch!.QuantityAvailable);
        Assert.Equal(10, f.Batch.QuantityReceived);
        Assert.Equal(10, f.Inventory!.QuantityInStock);
        Assert.Contains(f.Movements, x => x.MovementType == StockMovementType.OpeningStock && x.Quantity == 10);
        Assert.Contains(f.Audits, x => x.Action == "OpeningStockAdded");
        Assert.Equal(10, result.QuantityInStock);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Opening_stock_rejects_non_positive_quantity(int quantity)
    {
        var f = new Fixture(PermissionCatalog.InventoryOpeningStock);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.AddOpeningStockAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, "B-1", null, f.Today.AddDays(30), quantity, 8, 12, null, null)));
    }

    [Fact]
    public async Task Opening_stock_rejects_inactive_product_and_yesterday_expiry_but_allows_today()
    {
        var inactive = new Fixture(PermissionCatalog.InventoryOpeningStock);
        inactive.Product.IsActive = false;
        await Assert.ThrowsAsync<RequestValidationException>(() => inactive.Service.AddOpeningStockAsync(inactive.Actor.Id, new(inactive.Branch.Id, inactive.Product.Id, "B-1", null, inactive.Today.AddDays(30), 5, 8, 12, null, null)));

        var expired = new Fixture(PermissionCatalog.InventoryOpeningStock);
        await Assert.ThrowsAsync<RequestValidationException>(() => expired.Service.AddOpeningStockAsync(expired.Actor.Id, new(expired.Branch.Id, expired.Product.Id, "B-1", null, expired.Today.AddDays(-1), 5, 8, 12, null, null)));

        var today = new Fixture(PermissionCatalog.InventoryOpeningStock);
        await today.Service.AddOpeningStockAsync(today.Actor.Id, new(today.Branch.Id, today.Product.Id, "B-1", null, today.Today, 5, 8, 12, null, null));
        Assert.Equal(5, today.Batch!.QuantityAvailable);
    }

    [Fact]
    public async Task Stock_decrease_rejects_insufficient_stock_and_increase_updates_projections()
    {
        var f = new Fixture(PermissionCatalog.InventoryAdjust);
        f.ExistingBatch(10, f.Today.AddDays(20));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.AdjustStockDecreaseAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, f.Batch!.Id, 11, AdjustmentReason.Missing, "missing")));
        await f.Service.AdjustStockIncreaseAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, f.Batch!.Id, 3, AdjustmentReason.DataEntryCorrection, "found"));
        await f.Service.AdjustStockDecreaseAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, f.Batch!.Id, 2, AdjustmentReason.Missing, "missing"));
        Assert.Equal(11, f.Batch.QuantityAvailable);
        Assert.Contains(f.Movements, x => x.MovementType == StockMovementType.AdjustmentIncrease && x.Quantity == 3);
        Assert.Contains(f.Movements, x => x.MovementType == StockMovementType.AdjustmentDecrease && x.Quantity == -2);
    }

    [Fact]
    public async Task Stock_count_creates_variance_movement_or_no_movement_for_zero_variance()
    {
        var f = new Fixture(PermissionCatalog.InventoryStockCount);
        f.ExistingBatch(10, f.Today.AddDays(20));
        await f.Service.ReconcileStockCountAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, f.Batch!.Id, 12, AdjustmentReason.PhysicalCountCorrection, "count"));
        await f.Service.ReconcileStockCountAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, f.Batch.Id, 12, AdjustmentReason.PhysicalCountCorrection, "count"));
        Assert.Equal(12, f.Batch.QuantityAvailable);
        Assert.Single(f.Movements, x => x.ReferenceType == "StockCount");
    }

    [Fact]
    public async Task Expired_disposal_requires_expired_batch_and_records_negative_expired_movement()
    {
        var f = new Fixture(PermissionCatalog.InventoryExpiryManage);
        f.ExistingBatch(10, f.Today);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.DisposeExpiredStockAsync(f.Actor.Id, new(f.Batch!.Id, 1, "expired")));
        f.Batch!.ExpiryDate = f.Today.AddDays(-1);
        await f.Service.DisposeExpiredStockAsync(f.Actor.Id, new(f.Batch.Id, 4, "expired"));
        Assert.Equal(6, f.Batch.QuantityAvailable);
        Assert.Contains(f.Movements, x => x.MovementType == StockMovementType.Expired && x.Quantity == -4);
    }

    [Fact]
    public async Task Fefo_preview_excludes_expired_disposed_other_branch_and_allocates_in_order()
    {
        var f = new Fixture(PermissionCatalog.InventoryView);
        var other = Guid.NewGuid();
        f.Batches.Add(new() { ProductId = f.Product.Id, BranchId = f.Branch.Id, BatchNumber = "A", ExpiryDate = f.Today.AddDays(1), QuantityAvailable = 3 });
        f.Batches.Add(new() { ProductId = f.Product.Id, BranchId = f.Branch.Id, BatchNumber = "B", ExpiryDate = f.Today.AddDays(2), QuantityAvailable = 10 });
        f.Batches.Add(new() { ProductId = f.Product.Id, BranchId = f.Branch.Id, BatchNumber = "expired", ExpiryDate = f.Today.AddDays(-1), QuantityAvailable = 10 });
        f.Batches.Add(new() { ProductId = f.Product.Id, BranchId = other, BatchNumber = "other", ExpiryDate = f.Today.AddDays(1), QuantityAvailable = 10 });
        var result = await f.Service.PreviewFefoAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, 5, f.Today));
        Assert.Equal([3, 2], result.Allocations.Select(x => x.AllocatedQuantity));
    }

    [Fact]
    public async Task Missing_permission_is_forbidden()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.AddOpeningStockAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, "B-1", null, f.Today.AddDays(30), 10, 8, 12, null, null)));
    }

    private sealed class Fixture : IInventoryRepository
    {
        public readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        public readonly Branch Branch = new() { Code = "MAIN", Name = "Main" };
        public readonly ProductCategory Category = new() { Name = "Medicine", NormalizedName = "MEDICINE" };
        public readonly Product Product;
        public readonly User Actor;
        public readonly List<ProductBatch> Batches = [];
        public readonly List<StockMovement> Movements = [];
        public readonly List<AuditLog> Audits = [];
        public ProductBatch? Batch;
        public Pharmacy.Domain.Entities.Inventory? Inventory;
        public InventoryService Service { get; }

        public Fixture(params string[] permissions)
        {
            Product = new Product { SKU = "SKU-1", NormalizedSku = "SKU-1", Name = "Panadol", CategoryId = Category.Id, Category = Category, Unit = "Tablet", PackSize = 1, PurchasePrice = 8, RetailPrice = 12, MaximumDiscountPercent = 0, ReorderLevel = 5, IsActive = true };
            var role = new Role { Name = RoleCatalog.Manager };
            foreach (var permission in permissions) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "actor", NormalizedUsername = "ACTOR", FullName = "Actor", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role };
            Service = new(this, new FefoAllocationService(), TimeProvider.System);
        }

        public void ExistingBatch(int quantity, DateOnly expiry)
        {
            Batch = new ProductBatch { BranchId = Branch.Id, ProductId = Product.Id, BatchNumber = "B-1", ExpiryDate = expiry, QuantityReceived = quantity, QuantityAvailable = quantity, PurchasePrice = 8, RetailPrice = 12 };
            Inventory = new Pharmacy.Domain.Entities.Inventory { BranchId = Branch.Id, ProductId = Product.Id, ProductBatchId = Batch.Id, QuantityInStock = quantity, ReorderLevel = Product.ReorderLevel };
            Batches.Add(Batch);
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(Branch.Id == branchId ? Branch : null);
        public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(Product.Id == productId ? Product : null);
        public Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult<ProductBatch?>(Batches.FirstOrDefault(x => x.Id == batchId));
        public Task<ProductBatch?> GetBatchByNumberAsync(Guid branchId, Guid productId, string batchNumber, CancellationToken cancellationToken = default) => Task.FromResult<ProductBatch?>(Batches.FirstOrDefault(x => x.BranchId == branchId && x.ProductId == productId && x.BatchNumber == batchNumber));
        public Task<Pharmacy.Domain.Entities.Inventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult<Pharmacy.Domain.Entities.Inventory?>(Inventory is not null && Inventory.BranchId == branchId && Inventory.ProductId == productId && Inventory.ProductBatchId == batchId ? Inventory : null);
        public Task<Supplier?> GetSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default) => Task.FromResult<Supplier?>(null);
        public Task<IReadOnlyList<ProductBatch>> GetEligibleBatchesAsync(Guid branchId, Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductBatch>>(Batches);
        public Task AddBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default) { Batch = batch; Batches.Add(batch); return Task.CompletedTask; }
        public Task AddInventoryAsync(Pharmacy.Domain.Entities.Inventory inventory, CancellationToken cancellationToken = default) { Inventory = inventory; return Task.CompletedTask; }
        public Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default) { Movements.Add(movement); return Task.CompletedTask; }
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public Task<PagedResult<InventoryListItemDto>> ListInventoryAsync(InventoryListQuery query, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<InventoryListItemDto>([], 1, 25, 0));
        public Task<InventoryDetailsDto?> GetInventoryDetailsAsync(Guid branchId, Guid productId, DateOnly businessDate, CancellationToken cancellationToken = default) => Task.FromResult<InventoryDetailsDto?>(new(branchId, Branch.Name, productId, Product.Name, Product.SKU, Product.GenericName, Category.Name, null, Inventory?.QuantityInStock ?? 0, Product.ReorderLevel, InventoryStockStatus.Healthy, Batch?.ExpiryDate, (Inventory?.QuantityInStock ?? 0) * Product.PurchasePrice, []));
        public Task<PagedResult<BatchListItemDto>> ListBatchesAsync(BatchListQuery query, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<BatchListItemDto>([], 1, 25, 0));
        public Task<IReadOnlyList<ExpiryListItemDto>> ListExpiryAsync(ExpiryQuery query, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ExpiryListItemDto>>([]);
        public Task<PagedResult<StockMovementListItemDto>> ListMovementsAsync(StockMovementListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<StockMovementListItemDto>([], 1, 25, 0));
        public Task<IReadOnlyList<InventoryIntegrityIssueDto>> CheckIntegrityAsync(Guid? branchId, Guid? productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryIntegrityIssueDto>>([]);
        public Task<InventoryOptionsDto> GetOptionsAsync(string? productSearch, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new InventoryOptionsDto([], [], [], [], []));
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
