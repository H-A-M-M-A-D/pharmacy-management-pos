using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Inventory;
using Pharmacy.Application.Services.Sales;
using Pharmacy.Domain.Entities;
using DomainInventory = Pharmacy.Domain.Entities.Inventory;

namespace Pharmacy.Tests;

public sealed class SalesServiceTests
{
    [Fact]
    public async Task Posting_sale_allocates_fefo_across_batches_updates_stock_and_records_payment()
    {
        var f = new Fixture(PermissionCatalog.SalesCreate, PermissionCatalog.SalesView);
        var early = f.AddBatch("A", 3, f.Today.AddDays(5), 8, 12);
        var later = f.AddBatch("B", 10, f.Today.AddDays(10), 9, 13);

        var sale = await f.Service.PostSaleAsync(f.Actor.Id, new(
            f.Branch.Id,
            null,
            "Walk-in",
            null,
            null,
            [new(f.Product.Id, 5)],
            [new(SalePaymentMethod.Cash, 62, 70)]));

        Assert.Equal(SaleStatus.Posted, sale.Status);
        Assert.Equal(62, sale.NetTotal);
        Assert.Equal(8, sale.ChangeGiven);
        Assert.Equal([3, 2], sale.Items.Single().Allocations.Select(x => x.Quantity));
        Assert.Equal(0, early.QuantityAvailable);
        Assert.Equal(8, later.QuantityAvailable);
        Assert.Equal(0, f.Inventory.Single(x => x.ProductBatchId == early.Id).QuantityInStock);
        Assert.Equal(8, f.Inventory.Single(x => x.ProductBatchId == later.Id).QuantityInStock);
        Assert.Equal([-3, -2], f.Movements.Select(x => x.Quantity));
        Assert.All(f.Movements, x => Assert.Equal(StockMovementType.Sale, x.MovementType));
        Assert.Contains(f.Audits, x => x.Action == "SalePosted" && x.NewValues?.Contains("Password", StringComparison.OrdinalIgnoreCase) != true);

        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(JournalSourceType.Sale, journal.SourceType);
        Assert.Equal(sale.Id, journal.SourceId);
        Assert.Equal(62, journal.Lines.Single(x => x.Account == AccountMappingKey.Cash).Debit);
        Assert.Equal(62, journal.Lines.Single(x => x.Account == AccountMappingKey.SalesRevenue).Credit);
        Assert.Equal(42, journal.Lines.Single(x => x.Account == AccountMappingKey.CostOfGoodsSold).Debit);
        Assert.Equal(42, journal.Lines.Single(x => x.Account == AccountMappingKey.Inventory).Credit);
        Assert.Equal(journal.Lines.Sum(x => x.Debit), journal.Lines.Sum(x => x.Credit));
    }

    [Fact]
    public async Task Posting_sale_excludes_expired_disposed_and_other_branch_batches()
    {
        var f = new Fixture(PermissionCatalog.SalesCreate, PermissionCatalog.SalesView);
        f.AddBatch("expired", 10, f.Today.AddDays(-1), 8, 12);
        f.AddBatch("disposed", 10, f.Today.AddDays(1), 8, 12, disposed: true);
        f.AddBatch("other", 10, f.Today.AddDays(1), 8, 12, branchId: Guid.NewGuid());
        f.AddBatch("good", 4, f.Today, 8, 12);

        var sale = await f.Service.PostSaleAsync(f.Actor.Id, new(
            f.Branch.Id,
            null,
            null,
            null,
            null,
            [new(f.Product.Id, 4)],
            [new(SalePaymentMethod.Cash, 48, 48)]));

        Assert.Equal("good", sale.Items.Single().Allocations.Single().BatchNumber);
        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.PostSaleAsync(f.Actor.Id, new(
            f.Branch.Id,
            null,
            null,
            null,
            null,
            [new(f.Product.Id, 1)],
            [new(SalePaymentMethod.Cash, 12, 12)])));
    }

    [Fact]
    public async Task Held_sale_does_not_reserve_stock_and_posts_with_current_stock()
    {
        var f = new Fixture(PermissionCatalog.SalesHold, PermissionCatalog.SalesCreate, PermissionCatalog.SalesView);
        var batch = f.AddBatch("A", 5, f.Today.AddDays(5), 8, 12);

        var held = await f.Service.HoldSaleAsync(f.Actor.Id, new(f.Branch.Id, "Ali", null, null, [new(f.Product.Id, 5)]));
        Assert.Equal(SaleStatus.Held, held.Status);
        Assert.Equal(5, batch.QuantityAvailable);
        Assert.Empty(f.Movements);

        batch.QuantityAvailable = 3;
        f.Inventory.Single(x => x.ProductBatchId == batch.Id).QuantityInStock = 3;
        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.PostHeldSaleAsync(
            f.Actor.Id,
            held.Id,
            new(null, [new(SalePaymentMethod.Cash, 60, 60)])));
    }

    [Fact]
    public async Task Discount_requires_permission_and_respects_product_maximum()
    {
        var noDiscount = new Fixture(PermissionCatalog.SalesCreate, PermissionCatalog.SalesView);
        noDiscount.Product.MaximumDiscountPercent = 10;
        noDiscount.AddBatch("A", 2, noDiscount.Today.AddDays(5), 8, 12);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => noDiscount.Service.PostSaleAsync(noDiscount.Actor.Id, new(
            noDiscount.Branch.Id, null, null, null, null, [new(noDiscount.Product.Id, 1, 5)], [new(SalePaymentMethod.Cash, 11.40m, 11.40m)])));

        var tooHigh = new Fixture(PermissionCatalog.SalesCreate, PermissionCatalog.SalesView, PermissionCatalog.SalesDiscount);
        tooHigh.Product.MaximumDiscountPercent = 5;
        tooHigh.AddBatch("A", 2, tooHigh.Today.AddDays(5), 8, 12);
        await Assert.ThrowsAsync<RequestValidationException>(() => tooHigh.Service.PostSaleAsync(tooHigh.Actor.Id, new(
            tooHigh.Branch.Id, null, null, null, null, [new(tooHigh.Product.Id, 1, 10)], [new(SalePaymentMethod.Cash, 10.80m, 10.80m)])));
    }

    [Fact]
    public async Task Split_payment_and_cash_change_are_validated()
    {
        var f = new Fixture(PermissionCatalog.SalesCreate, PermissionCatalog.SalesView);
        f.AddBatch("A", 1, f.Today.AddDays(5), 8, 12);

        var sale = await f.Service.PostSaleAsync(f.Actor.Id, new(
            f.Branch.Id,
            null,
            null,
            null,
            null,
            [new(f.Product.Id, 1)],
            [new(SalePaymentMethod.Cash, 5, 10), new(SalePaymentMethod.Card, 7, null, "card-1")]));

        Assert.Equal(12, sale.AmountPaid);
        Assert.Equal(5, sale.ChangeGiven);
        Assert.Equal(2, sale.Payments.Count);

        var invalid = new Fixture(PermissionCatalog.SalesCreate, PermissionCatalog.SalesView);
        invalid.AddBatch("A", 1, invalid.Today.AddDays(5), 8, 12);
        await Assert.ThrowsAsync<RequestValidationException>(() => invalid.Service.PostSaleAsync(invalid.Actor.Id, new(
            invalid.Branch.Id,
            null,
            null,
            null,
            null,
            [new(invalid.Product.Id, 1)],
            [new(SalePaymentMethod.Cash, 10, 10)])));
    }

    [Fact]
    public async Task Credit_sale_records_customer_ledger_and_allows_partial_payment()
    {
        var f = new Fixture(PermissionCatalog.SalesCreate, PermissionCatalog.SalesView, PermissionCatalog.SalesCredit);
        var customer = f.AddCustomer(creditLimit: 100);
        f.AddBatch("A", 5, f.Today.AddDays(5), 8, 12);

        var sale = await f.Service.PostSaleAsync(f.Actor.Id, new(
            f.Branch.Id,
            customer.Id,
            null,
            null,
            null,
            [new(f.Product.Id, 5)],
            [new(SalePaymentMethod.Cash, 20, 20)]));

        Assert.Equal(60, sale.NetTotal);
        Assert.Equal(20, sale.AmountPaid);
        Assert.Equal(40, sale.CreditAmount);
        var ledger = Assert.Single(f.CustomerLedger);
        Assert.Equal(CustomerLedgerEntryType.CreditSale, ledger.EntryType);
        Assert.Equal(40, ledger.Amount);
        Assert.Equal(sale.Id, ledger.ReferenceId);
        Assert.Equal(customer.Id, sale.CustomerId);

        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(20, journal.Lines.Single(x => x.Account == AccountMappingKey.Cash).Debit);
        var receivable = journal.Lines.Single(x => x.Account == AccountMappingKey.AccountsReceivable);
        Assert.Equal(40, receivable.Debit);
        Assert.Equal(customer.Id, receivable.CustomerId);
        Assert.Equal(60, journal.Lines.Single(x => x.Account == AccountMappingKey.SalesRevenue).Credit);
        Assert.Equal(40, journal.Lines.Single(x => x.Account == AccountMappingKey.CostOfGoodsSold).Debit);
        Assert.Equal(40, journal.Lines.Single(x => x.Account == AccountMappingKey.Inventory).Credit);
        Assert.Equal(journal.Lines.Sum(x => x.Debit), journal.Lines.Sum(x => x.Credit));
    }

    [Fact]
    public async Task Fully_credit_sale_requires_customer_and_credit_permission()
    {
        var missingCustomer = new Fixture(PermissionCatalog.SalesCreate, PermissionCatalog.SalesView, PermissionCatalog.SalesCredit);
        missingCustomer.AddBatch("A", 1, missingCustomer.Today.AddDays(5), 8, 12);
        await Assert.ThrowsAsync<RequestValidationException>(() => missingCustomer.Service.PostSaleAsync(missingCustomer.Actor.Id, new(
            missingCustomer.Branch.Id,
            null,
            null,
            null,
            null,
            [new(missingCustomer.Product.Id, 1)],
            [])));

        var missingPermission = new Fixture(PermissionCatalog.SalesCreate, PermissionCatalog.SalesView);
        var customer = missingPermission.AddCustomer(creditLimit: 100);
        missingPermission.AddBatch("A", 1, missingPermission.Today.AddDays(5), 8, 12);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => missingPermission.Service.PostSaleAsync(missingPermission.Actor.Id, new(
            missingPermission.Branch.Id,
            customer.Id,
            null,
            null,
            null,
            [new(missingPermission.Product.Id, 1)],
            [])));
    }

    [Fact]
    public async Task Credit_sale_rejects_inactive_customer_and_credit_limit_overrun()
    {
        var inactive = new Fixture(PermissionCatalog.SalesCreate, PermissionCatalog.SalesView, PermissionCatalog.SalesCredit);
        var inactiveCustomer = inactive.AddCustomer(creditLimit: 100, active: false);
        inactive.AddBatch("A", 1, inactive.Today.AddDays(5), 8, 12);
        await Assert.ThrowsAsync<RequestValidationException>(() => inactive.Service.PostSaleAsync(inactive.Actor.Id, new(
            inactive.Branch.Id,
            inactiveCustomer.Id,
            null,
            null,
            null,
            [new(inactive.Product.Id, 1)],
            [])));

        var overLimit = new Fixture(PermissionCatalog.SalesCreate, PermissionCatalog.SalesView, PermissionCatalog.SalesCredit);
        var customer = overLimit.AddCustomer(creditLimit: 10);
        overLimit.AddBatch("A", 1, overLimit.Today.AddDays(5), 8, 12);
        await Assert.ThrowsAsync<ResourceConflictException>(() => overLimit.Service.PostSaleAsync(overLimit.Actor.Id, new(
            overLimit.Branch.Id,
            customer.Id,
            null,
            null,
            null,
            [new(overLimit.Product.Id, 1)],
            [])));
    }

    [Fact]
    public async Task Missing_sales_permission_is_forbidden()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.PostSaleAsync(f.Actor.Id, new(
            f.Branch.Id,
            null,
            null,
            null,
            null,
            [new(f.Product.Id, 1)],
            [new(SalePaymentMethod.Cash, 12, 12)])));
    }

    private sealed class Fixture : ISalesRepository
    {
        public readonly DateOnly Today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time")));
        public readonly Branch Branch = new() { Code = "MAIN", Name = "Main Branch", IsActive = true };
        public readonly ProductCategory Category = new() { Name = "Medicine", NormalizedName = "MEDICINE" };
        public readonly Product Product;
        public readonly User Actor;
        public readonly List<Sale> Sales = [];
        public readonly List<ProductBatch> Batches = [];
        public readonly List<DomainInventory> Inventory = [];
        public readonly List<StockMovement> Movements = [];
        public readonly List<Customer> Customers = [];
        public readonly List<CustomerLedgerEntry> CustomerLedger = [];
        public readonly List<AuditLog> Audits = [];
        public readonly FakeJournalPostingService Journal = new();
        public SalesService Service { get; }

        public Fixture(params string[] permissions)
        {
            Product = new Product
            {
                SKU = "SKU-1",
                NormalizedSku = "SKU-1",
                Barcode = "12345",
                NormalizedBarcode = "12345",
                Name = "Panadol",
                GenericName = "Paracetamol",
                CategoryId = Category.Id,
                Category = Category,
                Unit = "Tablet",
                PackSize = 1,
                PurchasePrice = 8,
                RetailPrice = 12,
                MaximumDiscountPercent = 0,
                ReorderLevel = 5,
                IsActive = true
            };
            var role = new Role { Name = RoleCatalog.Cashier };
            foreach (var permission in permissions)
            {
                role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            }
            Actor = new User { Username = "cashier", NormalizedUsername = "CASHIER", FullName = "Cashier User", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role, IsActive = true };
            Service = new(this, new FefoAllocationService(), Journal, TimeProvider.System);
        }

        public ProductBatch AddBatch(string number, int quantity, DateOnly expiry, decimal purchasePrice, decimal retailPrice, bool disposed = false, Guid? branchId = null)
        {
            var batch = new ProductBatch
            {
                ProductId = Product.Id,
                Product = Product,
                BranchId = branchId ?? Branch.Id,
                BatchNumber = number,
                ExpiryDate = expiry,
                PurchasePrice = purchasePrice,
                RetailPrice = retailPrice,
                QuantityReceived = quantity,
                QuantityAvailable = quantity,
                IsDisposed = disposed
            };
            Batches.Add(batch);
            Inventory.Add(new DomainInventory { BranchId = batch.BranchId, ProductId = Product.Id, ProductBatchId = batch.Id, ProductBatch = batch, QuantityInStock = quantity, ReorderLevel = Product.ReorderLevel });
            return batch;
        }

        public Customer AddCustomer(decimal creditLimit, bool active = true)
        {
            var customer = new Customer
            {
                CustomerCode = $"CUS-{Customers.Count + 1:000000}",
                Name = $"Customer {Customers.Count + 1}",
                NormalizedName = $"CUSTOMER {Customers.Count + 1}",
                CreditLimit = creditLimit,
                IsActive = active
            };
            Customers.Add(customer);
            return customer;
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(Branch.Id == branchId ? Branch : null);
        public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(Product.Id == productId ? Product : null);
        public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<Customer?>(Customers.FirstOrDefault(x => x.Id == customerId));
        public Task<decimal> GetCustomerBalanceAsync(Guid customerId, Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult(CustomerLedger.Where(x => x.CustomerId == customerId && x.BranchId == branchId).Sum(x => x.Amount));
        public Task<IReadOnlyList<ProductBatch>> GetEligibleBatchesAsync(Guid branchId, Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductBatch>>(Batches.OrderBy(x => x.ExpiryDate).ThenBy(x => x.CreatedAt).ThenBy(x => x.BatchNumber).ThenBy(x => x.Id).ToList());
        public Task<DomainInventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult<DomainInventory?>(Inventory.FirstOrDefault(x => x.BranchId == branchId && x.ProductId == productId && x.ProductBatchId == batchId));
        public Task<Sale?> GetSaleAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Sale?>(Sales.FirstOrDefault(x => x.Id == id));
        public Task<string> NextInvoiceNumberAsync(DateTime postedAtUtc, CancellationToken cancellationToken = default) => Task.FromResult($"INV-{postedAtUtc.Year}-{Sales.Count(x => x.Status == SaleStatus.Posted) + 1:000000}");
        public Task<string> NextHoldNumberAsync(DateTime createdAtUtc, CancellationToken cancellationToken = default) => Task.FromResult($"HOLD-{createdAtUtc.Year}-{Sales.Count(x => x.Status == SaleStatus.Held) + 1:000000}");
        public Task AddSaleAsync(Sale sale, CancellationToken cancellationToken = default) { Sales.Add(sale); return Task.CompletedTask; }
        public Task AddCustomerLedgerEntryAsync(CustomerLedgerEntry entry, CancellationToken cancellationToken = default) { CustomerLedger.Add(entry); return Task.CompletedTask; }
        public Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default) { Movements.Add(movement); return Task.CompletedTask; }
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public Task<IReadOnlyList<PosProductDto>> SearchProductsAsync(PosProductSearchQuery query, Guid actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PosProductDto>>([]);
        public Task<PagedResult<SaleListItemDto>> ListSalesAsync(SalesHistoryQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<SaleListItemDto>([], query.Page, query.PageSize, 0));
        public Task<PagedResult<SaleListItemDto>> ListHeldSalesAsync(HeldSalesQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<SaleListItemDto>([], query.Page, query.PageSize, 0));
        public Task<SaleDetailsDto?> GetSaleDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
        {
            var sale = Sales.FirstOrDefault(x => x.Id == id && (canSelectBranch || x.BranchId == actorBranchId));
            return Task.FromResult(sale is null ? null : Map(sale));
        }
        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => await operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        private SaleDetailsDto Map(Sale sale)
        {
            var items = sale.Items.Select(item =>
            {
                var allocations = item.Allocations.Select(allocation =>
                {
                    var batch = Batches.Single(x => x.Id == allocation.ProductBatchId);
                    return new SaleItemAllocationDto(allocation.Id, allocation.ProductBatchId, batch.BatchNumber, allocation.ExpiryDateSnapshot, allocation.Quantity, allocation.UnitRetailPriceSnapshot, allocation.UnitSalePriceSnapshot, allocation.GrossAmount, allocation.DiscountAmount, allocation.NetAmount);
                }).ToList();
                return new SaleItemDto(item.Id, item.ProductId, Product.Name, Product.SKU, item.RequestedQuantity, item.DiscountPercent, item.GrossAmount, item.DiscountAmount, item.TaxAmount, item.NetAmount, false, allocations);
            }).ToList();
            var payments = sale.Payments.Select(x => new SalePaymentDto(x.Id, x.Method, x.AmountApplied, x.TenderedAmount, x.ReferenceNumber)).ToList();
            var customer = Customers.FirstOrDefault(x => x.Id == sale.CustomerId);
            return new SaleDetailsDto(sale.Id, sale.InvoiceNumber, sale.HoldNumber, sale.Status, sale.CreatedAt, sale.PostedAtUtc, sale.BranchId, Branch.Name, Branch.Address, Branch.PhoneNumber, sale.CashierUserId, Actor.FullName, sale.CustomerId, customer?.CustomerCode, sale.CustomerName, sale.CustomerPhone, sale.Subtotal, sale.DiscountTotal, sale.TaxTotal, sale.NetTotal, sale.AmountPaid, sale.CreditAmount, sale.ChangeGiven, sale.Notes, items, payments);
        }
    }

    private sealed class FakeJournalPostingService : IJournalPostingService
    {
        public readonly List<JournalPostingRequest> Posted = [];
        public Task PostAsync(JournalPostingRequest request, CancellationToken cancellationToken = default)
        {
            Posted.Add(request);
            return Task.CompletedTask;
        }
    }
}
