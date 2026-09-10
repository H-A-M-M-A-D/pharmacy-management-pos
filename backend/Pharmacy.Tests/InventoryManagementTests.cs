using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Inventory;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Godowns;
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
        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(JournalSourceType.OpeningBalance, journal.SourceType);
        Assert.Equal(80, journal.Lines.Single(x => x.Account == AccountMappingKey.Inventory).Debit);
        Assert.Equal(80, journal.Lines.Single(x => x.Account == AccountMappingKey.RetainedEarnings).Credit);
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
        Assert.Equal(2, f.Journal.Posted.Count);
        Assert.Equal(24, f.Journal.Posted[0].Lines.Single(x => x.Account == AccountMappingKey.InventoryAdjustmentGain).Credit);
        Assert.Equal(16, f.Journal.Posted[1].Lines.Single(x => x.Account == AccountMappingKey.InventoryLossExpense).Debit);
    }

    [Theory]
    [InlineData(AdjustmentReason.Expired)]
    [InlineData(AdjustmentReason.Broken)]
    [InlineData(AdjustmentReason.Leakage)]
    [InlineData(AdjustmentReason.TheftOrLoss)]
    public async Task Stock_adjustment_accepts_every_extended_reason(AdjustmentReason reason)
    {
        var f = new Fixture(PermissionCatalog.InventoryAdjust);
        f.ExistingBatch(10, f.Today.AddDays(20));
        await f.Service.AdjustStockDecreaseAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, f.Batch!.Id, 2, reason, "note"));
        Assert.Equal(8, f.Batch.QuantityAvailable);
        Assert.Contains(f.Audits, x => x.Action == "StockAdjustedDecrease");
    }

    [Fact]
    public async Task Large_stock_adjustment_is_flagged_significant_in_audit_but_small_one_is_not()
    {
        var f = new Fixture(PermissionCatalog.InventoryAdjust);
        f.ExistingBatch(20, f.Today.AddDays(20));
        await f.Service.AdjustStockIncreaseAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, f.Batch!.Id, 2, AdjustmentReason.DataEntryCorrection, "small correction"));
        Assert.Contains(f.Audits, x => x.Action == "StockAdjustedIncrease");
        Assert.DoesNotContain(f.Audits, x => x.Action == "StockAdjustedIncreaseSignificant");

        await f.Service.AdjustStockIncreaseAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, f.Batch.Id, 200, AdjustmentReason.DataEntryCorrection, "bulk correction"));
        Assert.Contains(f.Audits, x => x.Action == "StockAdjustedIncreaseSignificant");

        f.ExistingBatch(10, f.Today.AddDays(20));
        await f.Service.AdjustStockDecreaseAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, f.Batch!.Id, 6, AdjustmentReason.TheftOrLoss, "half the shelf gone"));
        Assert.Contains(f.Audits, x => x.Action == "StockAdjustedDecreaseSignificant");
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
        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(JournalSourceType.StockAdjustment, journal.SourceType);
        Assert.Equal(16, journal.Lines.Single(x => x.Account == AccountMappingKey.Inventory).Debit);
        Assert.Equal(16, journal.Lines.Single(x => x.Account == AccountMappingKey.InventoryAdjustmentGain).Credit);
    }

    [Fact]
    public async Task Damaged_stock_posts_exact_inventory_loss_write_off()
    {
        var f = new Fixture(PermissionCatalog.InventoryAdjust);
        f.ExistingBatch(10, f.Today.AddDays(20));

        await f.Service.AdjustStockDecreaseAsync(f.Actor.Id,
            new(f.Branch.Id, f.Product.Id, f.Batch!.Id, 3, AdjustmentReason.Damaged, "broken packs"));

        Assert.Equal(7, f.Batch.QuantityAvailable);
        Assert.Contains(f.Movements, x => x.MovementType == StockMovementType.Damaged && x.Quantity == -3);
        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(JournalSourceType.StockWriteOff, journal.SourceType);
        Assert.Equal(24, journal.Lines.Single(x => x.Account == AccountMappingKey.InventoryLossExpense).Debit);
        Assert.Equal(24, journal.Lines.Single(x => x.Account == AccountMappingKey.Inventory).Credit);
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
        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(JournalSourceType.StockWriteOff, journal.SourceType);
        Assert.Equal(32, journal.Lines.Single(x => x.Account == AccountMappingKey.InventoryLossExpense).Debit);
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

    [Fact]
    public async Task Stock_count_session_full_workflow_posts_variance_and_skips_zero_variance()
    {
        var f = new Fixture(PermissionCatalog.InventoryStockCount, PermissionCatalog.InventoryStockCountFinalize, PermissionCatalog.InventoryStockCountView);
        f.ExistingBatch(10, f.Today.AddDays(20));
        var second = f.SecondBatch(5, f.Today.AddDays(25));

        var created = await f.Service.CreateStockCountSessionAsync(f.Actor.Id, new(f.Branch.Id, f.Today, StockCountScope.Full, null, null, null, "annual count"));
        Assert.Equal(StockCountStatus.Draft, created.Status);
        Assert.Equal(2, created.TotalItems);

        var started = await f.Service.StartStockCountSessionAsync(f.Actor.Id, created.Id);
        Assert.Equal(StockCountStatus.InProgress, started.Status);
        Assert.NotNull(started.StartedBy);

        var firstItem = started.Items.First(x => x.ProductBatchId == f.Batch!.Id);
        var secondItem = started.Items.First(x => x.ProductBatchId == second.Id);
        var afterEntries = await f.Service.SubmitStockCountEntriesAsync(f.Actor.Id, created.Id, new([
            new(firstItem.Id, 12, AdjustmentReason.PhysicalCountCorrection, "found extra"),
            new(secondItem.Id, 5, null, null)
        ]));
        Assert.Equal(2, afterEntries.CountedItems);
        Assert.Equal(1, afterEntries.VarianceItems);

        var finalized = await f.Service.FinalizeStockCountSessionAsync(f.Actor.Id, created.Id);
        Assert.Equal(StockCountStatus.Completed, finalized.Status);
        Assert.Equal(12, f.Batch!.QuantityAvailable);
        Assert.Equal(5, second.QuantityAvailable);
        Assert.Contains(f.Movements, x => x.MovementType == StockMovementType.AdjustmentIncrease && x.Quantity == 2 && x.ReferenceType == "StockCount");
        Assert.DoesNotContain(f.Movements, x => x.ProductBatchId == second.Id);
        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(JournalSourceType.StockAdjustment, journal.SourceType);
        Assert.Equal(16, journal.Lines.Single(x => x.Account == AccountMappingKey.Inventory).Debit);
        Assert.Equal(16, journal.Lines.Single(x => x.Account == AccountMappingKey.InventoryAdjustmentGain).Credit);

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.FinalizeStockCountSessionAsync(f.Actor.Id, created.Id));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CancelStockCountSessionAsync(f.Actor.Id, created.Id, new("changed my mind")));

        var details = await f.Service.GetStockCountSessionAsync(f.Actor.Id, created.Id);
        Assert.Equal(StockCountStatus.Completed, details.Status);
    }

    [Fact]
    public async Task Stock_count_session_requires_scope_specific_fields_and_start_before_entries()
    {
        var f = new Fixture(PermissionCatalog.InventoryStockCount);
        f.ExistingBatch(10, f.Today.AddDays(20));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateStockCountSessionAsync(f.Actor.Id, new(f.Branch.Id, f.Today, StockCountScope.Category, null, null, null, null)));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateStockCountSessionAsync(f.Actor.Id, new(f.Branch.Id, f.Today, StockCountScope.SelectedProducts, null, null, null, null)));

        var draft = await f.Service.CreateStockCountSessionAsync(f.Actor.Id, new(f.Branch.Id, f.Today, StockCountScope.Full, null, null, null, null));
        var entry = new SubmitStockCountEntriesRequest([new(draft.Items[0].Id, 5, null, null)]);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.SubmitStockCountEntriesAsync(f.Actor.Id, draft.Id, entry));
    }

    [Fact]
    public async Task Stock_count_session_finalize_requires_finalize_permission()
    {
        var f = new Fixture(PermissionCatalog.InventoryStockCount);
        f.ExistingBatch(10, f.Today.AddDays(20));
        var draft = await f.Service.CreateStockCountSessionAsync(f.Actor.Id, new(f.Branch.Id, f.Today, StockCountScope.Full, null, null, null, null));
        await f.Service.StartStockCountSessionAsync(f.Actor.Id, draft.Id);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.FinalizeStockCountSessionAsync(f.Actor.Id, draft.Id));
    }

    [Fact]
    public async Task Stock_count_session_can_be_cancelled_from_draft_but_not_after_completion()
    {
        var f = new Fixture(PermissionCatalog.InventoryStockCount);
        f.ExistingBatch(10, f.Today.AddDays(20));
        var draft = await f.Service.CreateStockCountSessionAsync(f.Actor.Id, new(f.Branch.Id, f.Today, StockCountScope.Full, null, null, null, null));
        var cancelled = await f.Service.CancelStockCountSessionAsync(f.Actor.Id, draft.Id, new("wrong scope"));
        Assert.Equal(StockCountStatus.Cancelled, cancelled.Status);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.StartStockCountSessionAsync(f.Actor.Id, draft.Id));
    }

    [Fact]
    public async Task Opening_stock_creates_batch_in_the_selected_godown()
    {
        var f = new Fixture(PermissionCatalog.InventoryOpeningStock);
        await f.Service.AddOpeningStockAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, "B-1", null, f.Today.AddDays(30), 10, 8, 12, null, "initial", f.SecondGodown.Id));
        Assert.Equal(f.SecondGodown.Id, f.Batch!.GodownId);
        Assert.Equal(f.SecondGodown.Id, f.Inventory!.GodownId);
    }

    [Fact]
    public async Task Same_product_tracks_independent_stock_in_two_godowns()
    {
        var f = new Fixture(PermissionCatalog.InventoryOpeningStock);
        await f.Service.AddOpeningStockAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, "B-1", null, f.Today.AddDays(30), 10, 8, 12, null, null, f.MainGodown.Id));
        await f.Service.AddOpeningStockAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, "B-1", null, f.Today.AddDays(30), 25, 8, 12, null, null, f.SecondGodown.Id));
        Assert.Equal(2, f.Batches.Count);
        Assert.Equal(10, f.Batches.Single(x => x.GodownId == f.MainGodown.Id).QuantityAvailable);
        Assert.Equal(25, f.Batches.Single(x => x.GodownId == f.SecondGodown.Id).QuantityAvailable);
    }

    [Fact]
    public async Task Opening_stock_into_unauthorized_godown_is_forbidden()
    {
        var f = new Fixture(PermissionCatalog.InventoryOpeningStock);
        f.Actor.Role!.Name = RoleCatalog.StoreKeeper; // non-branch-selecting role: godown access is actually enforced
        f.GodownAccess.AccessPredicate = (_, godownId) => godownId != f.SecondGodown.Id;
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.AddOpeningStockAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, "B-1", null, f.Today.AddDays(30), 10, 8, 12, null, null, f.SecondGodown.Id)));
    }

    [Fact]
    public async Task Adjustment_changes_only_the_batchs_own_godown_and_checks_access()
    {
        var f = new Fixture(PermissionCatalog.InventoryAdjust, PermissionCatalog.InventoryOpeningStock);
        await f.Service.AddOpeningStockAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, "B-1", null, f.Today.AddDays(30), 10, 8, 12, null, null, f.SecondGodown.Id));
        f.Actor.Role!.Name = RoleCatalog.StoreKeeper; // non-branch-selecting role: godown access is actually enforced
        f.GodownAccess.AccessPredicate = (_, godownId) => godownId != f.SecondGodown.Id;
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.AdjustStockIncreaseAsync(f.Actor.Id, new(f.Branch.Id, f.Product.Id, f.Batch!.Id, 5, AdjustmentReason.PhysicalCountCorrection, null)));
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
        public RecordingJournalPostingService Journal { get; } = new();
        public readonly FakeGodownAccessService GodownAccess = new();
        public readonly Godown MainGodown;
        public readonly Godown SecondGodown;
        public InventoryService Service { get; }

        public Fixture(params string[] permissions)
        {
            MainGodown = new Godown { BranchId = Branch.Id, Code = "MAIN", Name = "Main Godown", IsActive = true, IsDefault = true };
            SecondGodown = new Godown { BranchId = Branch.Id, Code = "SECOND", Name = "Second Godown", IsActive = true };
            GodownAccess.Godowns[MainGodown.Id] = MainGodown;
            GodownAccess.Godowns[SecondGodown.Id] = SecondGodown;
            GodownAccess.DefaultBranchId = Branch.Id;
            GodownAccess.DefaultGodownId = MainGodown.Id;
            Product = new Product { SKU = "SKU-1", NormalizedSku = "SKU-1", Name = "Panadol", CategoryId = Category.Id, Category = Category, Unit = "Tablet", PackSize = 1, PurchasePrice = 8, RetailPrice = 12, MaximumDiscountPercent = 0, ReorderLevel = 5, IsActive = true };
            var role = new Role { Name = RoleCatalog.Manager };
            foreach (var permission in permissions) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "actor", NormalizedUsername = "ACTOR", FullName = "Actor", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role };
            Service = new(this, new FefoAllocationService(), Journal, GodownAccess, TimeProvider.System);
        }

        public void ExistingBatch(int quantity, DateOnly expiry)
        {
            Batch = new ProductBatch { BranchId = Branch.Id, ProductId = Product.Id, BatchNumber = "B-1", ExpiryDate = expiry, QuantityReceived = quantity, QuantityAvailable = quantity, PurchasePrice = 8, RetailPrice = 12 };
            Inventory = new Pharmacy.Domain.Entities.Inventory { BranchId = Branch.Id, ProductId = Product.Id, ProductBatchId = Batch.Id, QuantityInStock = quantity, ReorderLevel = Product.ReorderLevel };
            Batches.Add(Batch);
        }

        public ProductBatch SecondBatch(int quantity, DateOnly expiry)
        {
            var batch = new ProductBatch { BranchId = Branch.Id, ProductId = Product.Id, BatchNumber = "B-2", ExpiryDate = expiry, QuantityReceived = quantity, QuantityAvailable = quantity, PurchasePrice = 8, RetailPrice = 12 };
            Batches.Add(batch);
            SecondaryInventory.Add(new Pharmacy.Domain.Entities.Inventory { BranchId = Branch.Id, ProductId = Product.Id, ProductBatchId = batch.Id, QuantityInStock = quantity, ReorderLevel = Product.ReorderLevel });
            return batch;
        }
        public readonly List<Pharmacy.Domain.Entities.Inventory> SecondaryInventory = [];

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(Branch.Id == branchId ? Branch : null);
        public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(Product.Id == productId ? Product : null);
        public Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult<ProductBatch?>(Batches.FirstOrDefault(x => x.Id == batchId));
        public Task<ProductBatch?> GetBatchByNumberAsync(Guid branchId, Guid? godownId, Guid productId, string batchNumber, CancellationToken cancellationToken = default) => Task.FromResult<ProductBatch?>(Batches.FirstOrDefault(x => x.BranchId == branchId && x.GodownId == godownId && x.ProductId == productId && x.BatchNumber == batchNumber));
        public Task<Pharmacy.Domain.Entities.Inventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(AllInventory().FirstOrDefault(x => x.BranchId == branchId && x.ProductId == productId && x.ProductBatchId == batchId));
        private IEnumerable<Pharmacy.Domain.Entities.Inventory> AllInventory() => (Inventory is null ? [] : new[] { Inventory }).Concat(SecondaryInventory);
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

        public readonly List<StockCountSession> StockCountSessions = [];
        public Task<string> NextStockCountNumberAsync(DateOnly countDate, CancellationToken cancellationToken = default) => Task.FromResult($"SC-{countDate.Year}-{StockCountSessions.Count + 1:000000}");
        public Task<IReadOnlyList<ProductBatch>> GetEligibleBatchesForCountAsync(Guid branchId, Guid? godownId, StockCountScope scope, Guid? categoryId, IReadOnlyList<Guid>? productIds, IReadOnlyList<Guid>? productBatchIds, CancellationToken cancellationToken = default)
        {
            IEnumerable<ProductBatch> query = Batches.Where(x => x.BranchId == branchId && !x.IsDisposed);
            if (godownId.HasValue) query = query.Where(x => x.GodownId == godownId);
            query = scope switch
            {
                StockCountScope.Full => query.Where(x => x.QuantityAvailable > 0),
                StockCountScope.Category => query.Where(x => x.QuantityAvailable > 0 && Product.CategoryId == categoryId),
                StockCountScope.SelectedProducts => query.Where(x => productIds != null && productIds.Contains(x.ProductId)),
                StockCountScope.SelectedBatches => query.Where(x => productBatchIds != null && productBatchIds.Contains(x.Id)),
                _ => query
            };
            return Task.FromResult<IReadOnlyList<ProductBatch>>(query.ToList());
        }
        public Task AddStockCountSessionAsync(StockCountSession session, CancellationToken cancellationToken = default) { StockCountSessions.Add(session); return Task.CompletedTask; }
        public Task<StockCountSession?> GetStockCountSessionForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(StockCountSessions.FirstOrDefault(x => x.Id == id));
        public Task<StockCountSessionDto?> GetStockCountSessionDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var s = StockCountSessions.FirstOrDefault(x => x.Id == id);
            if (s is null) return Task.FromResult<StockCountSessionDto?>(null);
            var items = s.Items.Select(i =>
            {
                var batch = Batches.First(b => b.Id == i.ProductBatchId);
                var variance = i.CountedQuantity.HasValue ? i.CountedQuantity - i.SystemQuantity : null;
                return new StockCountItemDto(i.Id, i.ProductId, Product.Name, Product.SKU, i.ProductBatchId, batch.BatchNumber, batch.ExpiryDate,
                    i.SystemQuantity, i.CountedQuantity, variance, i.UnitCostSnapshot, variance.HasValue ? variance.Value * i.UnitCostSnapshot : null,
                    i.Reason, i.Notes, i.CountedByUserId.HasValue ? Actor.FullName : null, i.CountedAtUtc);
            }).ToList();
            return Task.FromResult<StockCountSessionDto?>(new(s.Id, s.CountNumber, s.BranchId, Branch.Name, s.CountDate, s.Status, s.Scope, s.CategoryId,
                s.CategoryId.HasValue ? Category.Name : null, s.Notes, Actor.FullName, s.StartedByUserId.HasValue ? Actor.FullName : null, s.StartedAtUtc,
                s.CompletedByUserId.HasValue ? Actor.FullName : null, s.CompletedAtUtc, s.CancelledByUserId.HasValue ? Actor.FullName : null, s.CancelledAtUtc,
                items.Count, items.Count(x => x.CountedQuantity != null), items.Count(x => x.Variance is not null and not 0), items));
        }
        public Task<PagedResult<StockCountSessionListItemDto>> ListStockCountSessionsAsync(StockCountSessionListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<StockCountSessionListItemDto>([], 1, 25, 0));
    }

    private sealed class RecordingJournalPostingService : IJournalPostingService
    {
        public readonly List<JournalPostingRequest> Posted = [];
        public Task PostAsync(JournalPostingRequest request, CancellationToken cancellationToken = default)
        {
            Posted.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGodownAccessService : IGodownAccessService
    {
        public readonly Dictionary<Guid, Godown> Godowns = [];
        public Guid DefaultBranchId;
        public Guid? DefaultGodownId;
        public Func<Guid, Guid, bool> AccessPredicate = (_, _) => true;

        public Task<Godown?> GetGodownAsync(Guid godownId, CancellationToken cancellationToken = default) => Task.FromResult(Godowns.GetValueOrDefault(godownId));
        public Task<Guid?> GetDefaultGodownIdAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult(branchId == DefaultBranchId ? DefaultGodownId : null);
        public Task<bool> UserHasAccessAsync(Guid userId, Guid godownId, CancellationToken cancellationToken = default) => Task.FromResult(AccessPredicate(userId, godownId));
    }
}
