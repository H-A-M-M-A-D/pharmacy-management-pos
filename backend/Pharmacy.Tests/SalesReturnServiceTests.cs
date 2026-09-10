using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Sales;
using Pharmacy.Domain.Entities;
using DomainInventory = Pharmacy.Domain.Entities.Inventory;

namespace Pharmacy.Tests;

public sealed class SalesReturnServiceTests
{
    [Fact]
    public async Task Full_restockable_return_reverses_original_allocation_and_updates_inventory()
    {
        var f = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund);
        var allocation = f.AddOriginalSaleAllocation("A", 5, 100, 90);

        var result = await f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(
            SalesReturnReason.CustomerReturn,
            null,
            [new(allocation.Id, 5, SalesReturnDisposition.Restockable)],
            [new(SalePaymentMethod.Cash, 450)]));

        Assert.Equal(450, result.RefundAmount);
        Assert.Equal(5, f.Batch.QuantityAvailable);
        Assert.Equal(5, f.Inventory.QuantityInStock);
        Assert.Single(result.Items.Single().Allocations);
        Assert.Equal(allocation.Id, result.Items.Single().Allocations.Single().OriginalAllocationId);
        Assert.Single(f.Movements);
        Assert.Equal(StockMovementType.SaleReturn, f.Movements.Single().MovementType);
        Assert.Equal(5, f.Movements.Single().Quantity);
        Assert.Equal(SalesReturnState.FullyReturned, (await f.Service.GetReturnableSaleAsync(f.Actor.Id, f.Sale.Id)).ReturnState);

        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(JournalSourceType.SalesReturn, journal.SourceType);
        Assert.Equal(result.Id, journal.SourceId);
        Assert.Equal(450, journal.Lines.Single(x => x.Account == AccountMappingKey.SalesReturnsContra).Debit);
        Assert.Equal(450, journal.Lines.Single(x => x.Account == AccountMappingKey.Cash).Credit);
        Assert.Equal(350, journal.Lines.Single(x => x.Account == AccountMappingKey.Inventory).Debit);
        Assert.Equal(350, journal.Lines.Single(x => x.Account == AccountMappingKey.CostOfGoodsSold).Credit);
        Assert.Equal(journal.Lines.Sum(x => x.Debit), journal.Lines.Sum(x => x.Credit));
    }

    [Fact]
    public async Task Non_resellable_return_does_not_reverse_cogs_but_still_reverses_revenue()
    {
        var f = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund);
        var allocation = f.AddOriginalSaleAllocation("A", 5, 100, 100);

        await f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(SalesReturnReason.Damaged, null, [new(allocation.Id, 2, SalesReturnDisposition.NonResellable)], [new(SalePaymentMethod.Cash, 200)]));

        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(200, journal.Lines.Single(x => x.Account == AccountMappingKey.SalesReturnsContra).Debit);
        Assert.Equal(200, journal.Lines.Single(x => x.Account == AccountMappingKey.Cash).Credit);
        Assert.DoesNotContain(journal.Lines, x => x.Account == AccountMappingKey.CostOfGoodsSold);
        Assert.DoesNotContain(journal.Lines, x => x.Account == AccountMappingKey.Inventory);
    }

    [Fact]
    public async Task Sales_return_journal_posting_failure_rolls_back_the_entire_return()
    {
        var f = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund) { Journal = { ThrowOnPost = true } };
        var allocation = f.AddOriginalSaleAllocation("A", 5, 100, 90);

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(
            SalesReturnReason.CustomerReturn, null, [new(allocation.Id, 5, SalesReturnDisposition.Restockable)], [new(SalePaymentMethod.Cash, 450)])));

        Assert.Empty(f.Returns);
        Assert.Empty(f.Movements);
    }

    [Fact]
    public async Task Partial_returns_track_remaining_and_block_over_return()
    {
        var f = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund);
        var allocation = f.AddOriginalSaleAllocation("A", 5, 100, 100);

        await f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(SalesReturnReason.CustomerReturn, null, [new(allocation.Id, 2, SalesReturnDisposition.Restockable)], [new(SalePaymentMethod.Cash, 200)]));
        var returnable = await f.Service.GetReturnableSaleAsync(f.Actor.Id, f.Sale.Id);
        Assert.Equal(SalesReturnState.PartiallyReturned, returnable.ReturnState);
        Assert.Equal(3, returnable.Items.Single().RemainingQuantity);

        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(SalesReturnReason.CustomerReturn, null, [new(allocation.Id, 4, SalesReturnDisposition.Restockable)], [new(SalePaymentMethod.Cash, 400)])));
        Assert.Equal(2, f.Returns.Single().Items.Single().Quantity);
        Assert.Equal(2, f.Batch.QuantityAvailable);
    }

    [Fact]
    public async Task Multi_batch_return_uses_original_allocations_not_fefo()
    {
        var f = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund);
        var laterOriginal = f.AddOriginalSaleAllocation("B", 2, 100, 100);
        var earlierUnrelated = f.AddBatch("A", 20, f.Today.AddDays(1));

        await f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(SalesReturnReason.CustomerReturn, null, [new(laterOriginal.Id, 1, SalesReturnDisposition.Restockable)], [new(SalePaymentMethod.Cash, 100)]));

        Assert.Equal(1, f.Batches.Single(x => x.Id == laterOriginal.ProductBatchId).QuantityAvailable);
        Assert.Equal(20, earlierUnrelated.QuantityAvailable);
        Assert.Equal(laterOriginal.ProductBatchId, f.Movements.Single().ProductBatchId);
    }

    [Fact]
    public async Task Non_resellable_return_records_sale_return_and_disposal_with_zero_net_stock_change()
    {
        var f = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund);
        var allocation = f.AddOriginalSaleAllocation("A", 5, 100, 100);

        await f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(SalesReturnReason.Damaged, null, [new(allocation.Id, 2, SalesReturnDisposition.NonResellable)], [new(SalePaymentMethod.Cash, 200)]));

        Assert.Equal(0, f.Batch.QuantityAvailable);
        Assert.Equal(0, f.Inventory.QuantityInStock);
        Assert.Equal([StockMovementType.SaleReturn, StockMovementType.Damaged], f.Movements.Select(x => x.MovementType));
        Assert.Equal([2, -2], f.Movements.Select(x => x.Quantity));
    }

    [Fact]
    public async Task Expired_non_resellable_return_uses_expired_disposal_movement()
    {
        var f = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund);
        var allocation = f.AddOriginalSaleAllocation("A", 5, 100, 100, expiry: f.Today.AddDays(-1));

        await f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(SalesReturnReason.CustomerReturn, null, [new(allocation.Id, 1, SalesReturnDisposition.NonResellable)], [new(SalePaymentMethod.Cash, 100)]));

        Assert.Equal(StockMovementType.Expired, f.Movements.Last().MovementType);
        Assert.Equal(0, f.Batch.QuantityAvailable);
    }

    [Fact]
    public async Task Disposed_batch_cannot_be_restocked_but_can_be_non_resellable()
    {
        var f = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund);
        var allocation = f.AddOriginalSaleAllocation("A", 2, 100, 100, disposed: true);

        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(SalesReturnReason.CustomerReturn, null, [new(allocation.Id, 1, SalesReturnDisposition.Restockable)], [new(SalePaymentMethod.Cash, 100)])));
        await f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(SalesReturnReason.CustomerReturn, null, [new(allocation.Id, 1, SalesReturnDisposition.NonResellable)], [new(SalePaymentMethod.Cash, 100)]));
        Assert.Equal(0, f.Batch.QuantityAvailable);
    }

    [Fact]
    public async Task Refund_uses_original_price_and_final_partial_gets_rounding_residual()
    {
        var f = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund);
        var allocation = f.AddOriginalSaleAllocation("A", 3, 33.333333m, 33.333333m, netAmount: 100);
        f.Batch.RetailPrice = 150;

        var first = await f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(SalesReturnReason.CustomerReturn, null, [new(allocation.Id, 1, SalesReturnDisposition.Restockable)], [new(SalePaymentMethod.Cash, 33.33m)]));
        var second = await f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(SalesReturnReason.CustomerReturn, null, [new(allocation.Id, 2, SalesReturnDisposition.Restockable)], [new(SalePaymentMethod.Cash, 66.67m)]));

        Assert.Equal(33.33m, first.RefundAmount);
        Assert.Equal(66.67m, second.RefundAmount);
        Assert.Equal(100m, f.Returns.SelectMany(x => x.Items).SelectMany(x => x.Allocations).Sum(x => x.RefundAmount));
    }

    [Fact]
    public async Task Refund_payments_must_match_backend_calculated_refund()
    {
        var f = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund);
        var allocation = f.AddOriginalSaleAllocation("A", 5, 100, 100);

        await f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(SalesReturnReason.CustomerReturn, null, [new(allocation.Id, 5, SalesReturnDisposition.Restockable)], [new(SalePaymentMethod.Cash, 300), new(SalePaymentMethod.Card, 200)]));

        var invalid = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund);
        var invalidAllocation = invalid.AddOriginalSaleAllocation("A", 5, 100, 100);
        await Assert.ThrowsAsync<RequestValidationException>(() => invalid.Service.PostReturnAsync(invalid.Actor.Id, invalid.Sale.Id, new(SalesReturnReason.CustomerReturn, null, [new(invalidAllocation.Id, 5, SalesReturnDisposition.Restockable)], [new(SalePaymentMethod.Cash, 400)])));
    }

    [Fact]
    public async Task Authorization_and_reason_rules_are_enforced()
    {
        var noCreate = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsRefund);
        var allocation = noCreate.AddOriginalSaleAllocation("A", 1, 100, 100);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => noCreate.Service.PostReturnAsync(noCreate.Actor.Id, noCreate.Sale.Id, new(SalesReturnReason.CustomerReturn, null, [new(allocation.Id, 1, SalesReturnDisposition.Restockable)], [new(SalePaymentMethod.Cash, 100)])));

        var f = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund);
        var other = f.AddOriginalSaleAllocation("B", 1, 100, 100);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(SalesReturnReason.Other, null, [new(other.Id, 1, SalesReturnDisposition.Restockable)], [new(SalePaymentMethod.Cash, 100)])));
    }

    [Fact]
    public async Task Credit_sale_return_reduces_customer_balance_before_cash_refund()
    {
        var f = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund);
        var customer = new Customer { CustomerCode = "CUS-000001", Name = "Credit Customer", NormalizedName = "CREDIT CUSTOMER", CreditLimit = 500 };
        f.Sale.CustomerId = customer.Id;
        f.Sale.Customer = customer;
        var allocation = f.AddOriginalSaleAllocation("A", 1, 100, 100);
        f.Sale.AmountPaid = 0;
        f.Sale.CreditAmount = 100;
        f.CustomerLedger.Add(new CustomerLedgerEntry { CustomerId = customer.Id, BranchId = f.Branch.Id, EntryType = CustomerLedgerEntryType.CreditSale, Amount = 100, EntryDate = f.Today });

        var posted = await f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(
            SalesReturnReason.CustomerReturn,
            null,
            [new(allocation.Id, 1, SalesReturnDisposition.Restockable)],
            []));

        Assert.Equal(100, posted.RefundAmount);
        Assert.Equal(100, posted.CustomerCreditReductionAmount);
        Assert.Equal(0, posted.CashRefundAmount);
        Assert.Empty(posted.RefundPayments);
        var reduction = Assert.Single(f.CustomerLedger, x => x.EntryType == CustomerLedgerEntryType.SalesReturn);
        Assert.Equal(-100, reduction.Amount);
        Assert.Equal(posted.Id, reduction.ReferenceId);
    }

    [Fact]
    public async Task Return_restores_stock_to_the_original_sales_godown()
    {
        var f = new Fixture(PermissionCatalog.SalesReturnsView, PermissionCatalog.SalesReturnsCreate, PermissionCatalog.SalesReturnsRefund);
        var godownId = Guid.NewGuid();
        f.Sale.GodownId = godownId;
        var allocation = f.AddOriginalSaleAllocation("A", 5, 100, 90);
        f.Batch.GodownId = godownId;

        var result = await f.Service.PostReturnAsync(f.Actor.Id, f.Sale.Id, new(
            SalesReturnReason.CustomerReturn, null,
            [new(allocation.Id, 5, SalesReturnDisposition.Restockable)],
            [new(SalePaymentMethod.Cash, 450)]));

        Assert.Equal(godownId, f.Returns.Single(x => x.Id == result.Id).GodownId);
        Assert.Equal(godownId, f.Movements.Single().GodownId);
    }

    private sealed class Fixture : ISalesReturnRepository
    {
        public readonly DateOnly Today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time")));
        public readonly Branch Branch = new() { Code = "MAIN", Name = "Main Branch", IsActive = true };
        public readonly ProductCategory Category = new() { Name = "Medicine", NormalizedName = "MEDICINE" };
        public readonly Product Product;
        public readonly User Actor;
        public readonly Sale Sale;
        public ProductBatch Batch = null!;
        public DomainInventory Inventory = null!;
        public readonly List<ProductBatch> Batches = [];
        public readonly List<DomainInventory> Inventories = [];
        public readonly List<SalesReturn> Returns = [];
        public readonly List<StockMovement> Movements = [];
        public readonly List<CustomerLedgerEntry> CustomerLedger = [];
        public readonly List<AuditLog> Audits = [];
        public readonly FakeJournalPostingService Journal = new();
        public SalesReturnService Service { get; }

        public Fixture(params string[] permissions)
        {
            Product = new Product { SKU = "SKU-1", NormalizedSku = "SKU-1", Name = "Panadol", CategoryId = Category.Id, Category = Category, Unit = "Tablet", PackSize = 1, PurchasePrice = 8, RetailPrice = 12, ReorderLevel = 5, IsActive = true };
            var role = new Role { Name = RoleCatalog.Manager };
            foreach (var permission in permissions)
            {
                role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            }
            Actor = new User { Username = "manager", NormalizedUsername = "MANAGER", FullName = "Manager User", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role, IsActive = true };
            Sale = new Sale { BranchId = Branch.Id, Branch = Branch, CashierUserId = Actor.Id, CashierUser = Actor, InvoiceNumber = "INV-2026-000001", Status = SaleStatus.Posted, PostedAtUtc = DateTime.UtcNow, NetTotal = 0, AmountPaid = 0 };
            Service = new(this, Journal, TimeProvider.System);
        }

        public SaleItemBatchAllocation AddOriginalSaleAllocation(string batchNumber, int quantity, decimal unitRetail, decimal unitSale, decimal? netAmount = null, DateOnly? expiry = null, bool disposed = false)
        {
            Batch = AddBatch(batchNumber, 0, expiry ?? Today.AddDays(20), disposed);
            Inventory = Inventories.Single(x => x.ProductBatchId == Batch.Id);
            var item = Sale.Items.SingleOrDefault() ?? new SaleItem { SaleId = Sale.Id, Sale = Sale, ProductId = Product.Id, Product = Product, RequestedQuantity = 0 };
            if (!Sale.Items.Contains(item)) Sale.Items.Add(item);
            item.RequestedQuantity += quantity;
            var gross = Math.Round(unitRetail * quantity, 2, MidpointRounding.AwayFromZero);
            var net = netAmount ?? Math.Round(unitSale * quantity, 2, MidpointRounding.AwayFromZero);
            var allocation = new SaleItemBatchAllocation { SaleItemId = item.Id, SaleItem = item, ProductBatchId = Batch.Id, ProductBatch = Batch, Quantity = quantity, UnitRetailPriceSnapshot = unitRetail, UnitSalePriceSnapshot = unitSale, UnitCostPriceSnapshot = 70, ExpiryDateSnapshot = Batch.ExpiryDate, GrossAmount = gross, DiscountAmount = gross - net, NetAmount = net };
            item.GrossAmount += allocation.GrossAmount;
            item.DiscountAmount += allocation.DiscountAmount;
            item.NetAmount += allocation.NetAmount;
            item.Allocations.Add(allocation);
            Sale.Subtotal += allocation.GrossAmount;
            Sale.DiscountTotal += allocation.DiscountAmount;
            Sale.NetTotal += allocation.NetAmount;
            Sale.AmountPaid = Sale.NetTotal;
            return allocation;
        }

        public ProductBatch AddBatch(string batchNumber, int quantity, DateOnly expiry, bool disposed = false)
        {
            var batch = new ProductBatch { ProductId = Product.Id, Product = Product, BranchId = Branch.Id, Branch = Branch, BatchNumber = batchNumber, ExpiryDate = expiry, PurchasePrice = 70, RetailPrice = 100, QuantityReceived = quantity, QuantityAvailable = quantity, IsDisposed = disposed };
            Batches.Add(batch);
            Inventories.Add(new DomainInventory { BranchId = Branch.Id, ProductId = Product.Id, ProductBatchId = batch.Id, ProductBatch = batch, QuantityInStock = quantity, ReorderLevel = Product.ReorderLevel });
            return batch;
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Sale?> GetOriginalSaleAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.FromResult<Sale?>(Sale.Id == saleId ? Sale : null);
        public Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult<ProductBatch?>(Batches.SingleOrDefault(x => x.Id == batchId));
        public Task<DomainInventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult<DomainInventory?>(Inventories.SingleOrDefault(x => x.BranchId == branchId && x.ProductId == productId && x.ProductBatchId == batchId));
        public Task<IReadOnlyDictionary<Guid, int>> GetReturnedQuantitiesAsync(IEnumerable<Guid> allocationIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<Guid, int>>(Returns.SelectMany(x => x.Items).SelectMany(x => x.Allocations).Where(x => allocationIds.Contains(x.OriginalSaleItemBatchAllocationId)).GroupBy(x => x.OriginalSaleItemBatchAllocationId).ToDictionary(x => x.Key, x => x.Sum(a => a.Quantity)));
        public Task<IReadOnlyDictionary<Guid, decimal>> GetRefundedAmountsAsync(IEnumerable<Guid> allocationIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(Returns.SelectMany(x => x.Items).SelectMany(x => x.Allocations).Where(x => allocationIds.Contains(x.OriginalSaleItemBatchAllocationId)).GroupBy(x => x.OriginalSaleItemBatchAllocationId).ToDictionary(x => x.Key, x => x.Sum(a => a.RefundAmount)));
        public Task<decimal> GetCustomerBalanceAsync(Guid customerId, Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult(CustomerLedger.Where(x => x.CustomerId == customerId && x.BranchId == branchId).Sum(x => x.Amount));
        public Task<string> NextReturnNumberAsync(DateTime returnDateUtc, CancellationToken cancellationToken = default) => Task.FromResult($"RET-{returnDateUtc.Year}-{Returns.Count + 1:000000}");
        public Task AddSalesReturnAsync(SalesReturn salesReturn, CancellationToken cancellationToken = default) { Returns.Add(salesReturn); return Task.CompletedTask; }
        public Task AddCustomerLedgerEntryAsync(CustomerLedgerEntry entry, CancellationToken cancellationToken = default) { CustomerLedger.Add(entry); return Task.CompletedTask; }
        public Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default) { Movements.Add(movement); return Task.CompletedTask; }
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public Task<PagedResult<SalesReturnListItemDto>> ListReturnsAsync(SalesReturnsQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<SalesReturnListItemDto>([], query.Page, query.PageSize, 0));
        public Task<SalesReturnDetailsDto?> GetReturnDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult<SalesReturnDetailsDto?>(Returns.SingleOrDefault(x => x.Id == id) is { } r ? Map(r) : null);
        public async Task<ReturnableSaleDto?> GetReturnableSaleAsync(Guid saleId, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default)
        {
            if (Sale.Id != saleId || (!canSelectBranch && Sale.BranchId != actorBranchId)) return null;
            var allocationIds = Sale.Items.SelectMany(x => x.Allocations).Select(x => x.Id).ToArray();
            var returned = await GetReturnedQuantitiesAsync(allocationIds, cancellationToken);
            var refunded = await GetRefundedAmountsAsync(allocationIds, cancellationToken);
            var items = Sale.Items.Select(item => new ReturnableSaleItemDto(item.Id, item.ProductId, Product.Name, Product.SKU, item.RequestedQuantity, returned.Where(x => item.Allocations.Any(a => a.Id == x.Key)).Sum(x => x.Value), item.Allocations.Sum(a => a.Quantity - returned.GetValueOrDefault(a.Id)), item.NetAmount, item.Allocations.Sum(a => a.NetAmount - refunded.GetValueOrDefault(a.Id)), item.Allocations.Select(a => new ReturnableAllocationDto(a.Id, a.ProductBatchId, a.ProductBatch!.BatchNumber, a.ExpiryDateSnapshot, a.Quantity, returned.GetValueOrDefault(a.Id), a.Quantity - returned.GetValueOrDefault(a.Id), a.UnitSalePriceSnapshot, a.NetAmount - refunded.GetValueOrDefault(a.Id), a.ProductBatch.IsDisposed, a.ProductBatch.ExpiryDate < businessDate)).ToList())).ToList();
            var remaining = items.Sum(x => x.RemainingQuantity);
            var sold = items.Sum(x => x.SoldQuantity);
            var state = remaining == sold ? SalesReturnState.NotReturned : remaining == 0 ? SalesReturnState.FullyReturned : SalesReturnState.PartiallyReturned;
            return new ReturnableSaleDto(Sale.Id, Sale.InvoiceNumber!, Sale.PostedAtUtc!.Value, Sale.BranchId, Branch.Name, Actor.FullName, Sale.CustomerName, Sale.CustomerPhone, Sale.NetTotal, Sale.CustomerId, Sale.Customer?.CustomerCode, Sale.AmountPaid, Sale.CreditAmount, state, items, []);
        }
        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
        {
            var snapshot = (Returns.ToList(), Movements.ToList(), CustomerLedger.ToList(), Audits.ToList());
            try { await operation(cancellationToken); }
            catch
            {
                Returns.Clear(); Returns.AddRange(snapshot.Item1);
                Movements.Clear(); Movements.AddRange(snapshot.Item2);
                CustomerLedger.Clear(); CustomerLedger.AddRange(snapshot.Item3);
                Audits.Clear(); Audits.AddRange(snapshot.Item4);
                throw;
            }
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        private SalesReturnDetailsDto Map(SalesReturn x) => new(x.Id, x.ReturnNumber, x.OriginalSaleId, Sale.InvoiceNumber!, x.BranchId, Branch.Name, Branch.Address, Branch.PhoneNumber, x.ProcessedByUserId, Actor.FullName, x.ReturnDateUtc, x.Reason, x.Notes, x.GrossReturnAmount, x.DiscountReturnAmount, x.TaxReturnAmount, x.RefundAmount, x.CustomerCreditReductionAmount, x.CashRefundAmount, x.Status, Sale.CustomerName, Sale.CustomerPhone,
            x.Items.Select(i => new SalesReturnItemDto(i.Id, i.OriginalSaleItemId, i.ProductId, Product.Name, Product.SKU, i.Quantity, i.GrossReturnAmount, i.DiscountReturnAmount, i.TaxReturnAmount, i.RefundAmount, i.Allocations.Select(a => new SalesReturnAllocationDto(a.Id, a.OriginalSaleItemBatchAllocationId, a.ProductBatchId, Batches.Single(b => b.Id == a.ProductBatchId).BatchNumber, a.ExpiryDateSnapshot, a.Quantity, a.Disposition, a.UnitSalePriceSnapshot, a.GrossReturnAmount, a.DiscountReturnAmount, a.TaxReturnAmount, a.RefundAmount)).ToList())).ToList(),
            x.RefundPayments.Select(p => new SalesRefundPaymentDto(p.Id, p.Method, p.Amount, p.ReferenceNumber)).ToList());
    }

    private sealed class FakeJournalPostingService : IJournalPostingService
    {
        public readonly List<JournalPostingRequest> Posted = [];
        public bool ThrowOnPost;
        public Task PostAsync(JournalPostingRequest request, CancellationToken cancellationToken = default)
        {
            if (ThrowOnPost) throw new InvalidOperationException("forced journal failure");
            Posted.Add(request);
            return Task.CompletedTask;
        }
    }
}
