using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Reports;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Persistence;
using System.Text.Json;
using Npgsql;
using Xunit.Abstractions;

namespace Pharmacy.Tests;

[Collection("Management PostgreSQL reporting")]
public sealed class ManagementPostgreSqlTests(ITestOutputHelper output)
{
    private const int SaleCount = 512;
    private const int ProductCount = 64;
    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Scoped_reader_receives_sales_without_cost_and_only_assigned_godown_stock()
    {
        await using var db = Open(); await using var tx = await db.Database.BeginTransactionAsync();
        var seed = await SeedAsync(db);
        var role = new Role { Name = "MIS reader " + Guid.NewGuid().ToString("N") };
        foreach (var permission in await db.Permissions.Where(x => x.Code == PermissionCatalog.ReportsView || x.Code == PermissionCatalog.ReportsSales || x.Code == PermissionCatalog.ReportsInventory).ToListAsync())
            role.RolePermissions.Add(new RolePermission { PermissionId = permission.Id });
        var key = Guid.NewGuid().ToString("N");
        var reader = new User { Username = key, NormalizedUsername = key.ToUpperInvariant(), FullName = "Scoped reader", PasswordHash = "hash", BranchId = seed.Branch.Id, Role = role };
        db.Add(reader);
        await db.SaveChangesAsync();
        var service = new ReportingService(new ReportingRepository(db), TimeProvider.System);
        var q = seed.Query with { GodownId = null };
        var empty = JsonSerializer.SerializeToElement(await service.ExecuteAsync(reader.Id, "sales/products", q, null, default), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        db.UserGodowns.Add(new UserGodown { UserId = reader.Id, GodownId = seed.Query.GodownId!.Value });
        await db.SaveChangesAsync();
        var sales = JsonSerializer.SerializeToElement(await service.ExecuteAsync(reader.Id, "sales/products", q, null, default), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotEmpty(sales.GetProperty("items").EnumerateArray());
        Assert.All(sales.GetProperty("items").EnumerateArray(), row =>
        {
            Assert.False(row.TryGetProperty("costOfGoodsSold", out _));
            Assert.False(row.TryGetProperty("grossProfit", out _));
            Assert.False(row.TryGetProperty("grossProfitBeforeDiscount", out _));
        });
        var stock = JsonSerializer.SerializeToElement(await service.ExecuteAsync(reader.Id, "inventory/position", q, null, default), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.All(stock.GetProperty("items").EnumerateArray(), row =>
        {
            Assert.Equal(seed.Query.GodownId.Value, row.GetProperty("godownId").GetGuid());
            Assert.False(row.TryGetProperty("stockValue", out _));
        });
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => service.ExecuteAsync(reader.Id, "sales/products", q with { BranchId = Guid.NewGuid() }, null, default));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => service.ExecuteAsync(reader.Id, "profitability/products", q, null, default));
    }
    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Major_report_queries_have_real_PostgreSql_execution_plans()
    {
        var capture = new ReportSqlCapture();
        await using var db = new PharmacyDbContext(new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("PHARMACY_TEST_CONNECTION_STRING")!).AddInterceptors(capture).Options);
        await using var tx = await db.Database.BeginTransactionAsync();
        var seed = await SeedAsync(db);
        var repo = new ReportingRepository(db);
        var accounts = new AccountingRepository(db);
        var q = seed.Query;
        async Task Plan(string name, Func<Task> query, bool paged = false)
        {
            capture.Commands.Clear();
            await query();
            Assert.NotEmpty(capture.Commands);
            if (paged) Assert.Contains(capture.Commands, c => c.Sql.Contains("LIMIT"));
            var index = 0;
            foreach (var captured in capture.Commands)
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.Transaction = tx.GetDbTransaction();
                command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) " + captured.Sql;
                foreach (var parameter in captured.Parameters) command.Parameters.Add(parameter);
                await using var reader = await command.ExecuteReaderAsync();
                var plan = new List<string>();
                while (await reader.ReadAsync()) plan.Add(reader.GetString(0));
                Assert.Contains(plan, line => line.Contains("Execution Time:"));
                output.WriteLine(name + " query " + (++index) + "\n" + captured.Sql + "\n" + string.Join("\n", plan));
            }
        }
        await Plan("Sales by Product", async () => { _ = await repo.SalesAnalyticsAsync(q, "product", default); }, true);
        await Plan("Inventory Valuation", async () => { _ = await repo.StockPositionAsync(q, default); }, true);
        await Plan("Customer Aging", async () => { _ = await accounts.GetArAgingSummaryAsync(q.ToUtc, q.BranchId, null, default); });
        await Plan("Supplier Aging", async () => { _ = await accounts.GetApAgingSummaryAsync(q.ToUtc, q.BranchId, null, default); });
        await Plan("Profitability", async () => { _ = await repo.SalesAnalyticsAsync(q, "invoice", default); }, true);
        await Plan("MIS Overview", async () => { _ = await new ReportingService(repo, TimeProvider.System, new AccountingService(accounts, TimeProvider.System))
            .ExecuteAsync(seed.Actor.Id, "management/overview", q with { GodownId = null }, null, default); });
    }
    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Management_queries_translate_scope_page_and_reconcile_on_a_real_dataset()
    {
        await using var db = Open();
        await using var tx = await db.Database.BeginTransactionAsync();
        var seed = await SeedAsync(db);
        var repository = new ReportingRepository(db);
        var q = seed.Query;
        var summary = await repository.SalesSummaryAsync(seed.Branch.Id, q.FromUtc, q.ToUtc, default);
        var analytics = await repository.SalesAnalyticsAsync(q, "total", default);
        Assert.Equal(summary.NetSalesAfterReturns, Assert.Single(analytics.Items).NetSales);
        Assert.Equal(SaleCount, analytics.Items[0].InvoiceCount);
        Assert.Equal(SaleCount * 90m - 90m, analytics.Items[0].NetSales);
        Assert.Equal(SaleCount * 30m - 30m, analytics.Items[0].GrossProfit);
        var page1 = await repository.SalesAnalyticsAsync(q with { PageSize = 7 }, "invoice", default);
        var page2 = await repository.SalesAnalyticsAsync(q with { Page = 2, PageSize = 7 }, "invoice", default);
        Assert.Equal(SaleCount, page1.Total);
        Assert.Empty(page1.Items.Select(x => x.Key).Intersect(page2.Items.Select(x => x.Key)));
        foreach (var dimension in new[] { "product", "category", "manufacturer", "customer", "cashier", "branch", "godown", "type", "price-level", "day", "month", "hour" })
        {
            var result = await repository.SalesAnalyticsAsync(q with { PageSize = 200 }, dimension, default);
            Assert.Equal(analytics.Items[0].NetSales, result.Items.Sum(x => x.NetSales));
        }
        var otherBranch = await repository.SalesAnalyticsAsync(q with { BranchId = Guid.NewGuid() }, "total", default);
        Assert.Empty(otherBranch.Items);
        Assert.Empty((await repository.SalesAnalyticsAsync(q with { GodownId = Guid.NewGuid() }, "total", default)).Items);
        var stock = await repository.StockPositionAsync(q, default);
        Assert.Equal(ProductCount, stock.Total);
        Assert.Equal(ProductCount * 100 - SaleCount + 11, stock.Items.Sum(x => x.Quantity));
        Assert.Equal((ProductCount * 100 - SaleCount + 1) * 60m + 636m, stock.Items.Sum(x => x.StockValue));
        var aging = await repository.InventoryExposureAsync(q, false, default);
        Assert.Equal(stock.Items.Sum(x => x.StockValue), aging.Sum(x => x.CostValue));
        var exposure = await repository.InventoryExposureAsync(q, true, default);
        Assert.Equal(ProductCount * 100 - SaleCount + 11, exposure.Sum(x => x.Quantity));
        var performance = await repository.ProductPerformanceAsync(q with { PageSize = 7 }, default);
        Assert.Equal(ProductCount, performance.Total);
        Assert.Equal(7, performance.Items.Count);
        foreach (var basis in new[] { "sales", "profit", "inventory" })
        {
            var abc = await repository.AbcAsync(q with { PageSize = 200 }, basis, default);
            Assert.NotEmpty(abc.Items);
            Assert.Equal(100m, abc.Items[^1].CumulativePercent);
            Assert.All(abc.Items, x => Assert.Contains(x.Classification, new[] { "A", "B", "C" }));
        }
        foreach (var dimension in new[] { "total", "supplier", "product", "category", "manufacturer", "branch", "godown", "trend" })
            Assert.Equal(636m, (await repository.PurchaseAnalyticsAsync(q, dimension, default)).Items.Sum(x => x.NetPurchaseValue));
        _ = await repository.InventoryAlertsAsync(q, default);
        foreach (var report in new[] { "batch-position", "stock-adjustments", "movement-summary", "turnover", "in-transit" })
            _ = await repository.InventoryDetailAsync(q, report, default);
        foreach (var report in new[] { "price-history", "price-comparison", "last-rate", "price-variance", "pending-po", "po-vs-grn", "supplier-performance" })
            _ = await repository.PurchasingDetailAsync(report is "pending-po" or "po-vs-grn" ? q with { GodownId = null } : q, report, default);
        _ = await repository.GodownManagementAsync(q, default);
        _ = await repository.BranchManagementAsync(q with { GodownId = null }, default);
        var accounting = new AccountingService(new AccountingRepository(db), TimeProvider.System);
        var service = new ReportingService(repository, TimeProvider.System, accounting);
        var snapshot = JsonSerializer.SerializeToElement(await service.ExecuteAsync(seed.Actor.Id, "management/overview", q, null, default), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(analytics.Items[0].NetSales, snapshot.GetProperty("sales").GetProperty("netSales").GetDecimal());
        Assert.Equal(analytics.Items[0].GrossProfit, snapshot.GetProperty("profitability").GetProperty("grossProfit").GetDecimal());
        var financialQuery = q with { GodownId = null };
        var pnl = await accounting.GetProfitAndLossAsync(seed.Actor.Id, q.FromUtc, q.ToUtc.AddTicks(-1), seed.Branch.Id);
        Assert.Equal(analytics.Items[0].GrossProfit, pnl.GrossProfit);
        var financialSnapshot = JsonSerializer.SerializeToElement(await service.ExecuteAsync(seed.Actor.Id, "management/overview", financialQuery, null, default), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(pnl.NetProfit, financialSnapshot.GetProperty("finance").GetProperty("netProfit").GetDecimal());
        var inventoryBalance = await new AccountingRepository(db).GetMappedAccountBalanceAsync(AccountMappingKey.Inventory, q.ToUtc.AddTicks(-1), seed.Branch.Id, default);
        Assert.Equal(stock.Items.Sum(x => x.StockValue), inventoryBalance);
        var ar = await accounting.GetArAgingSummaryAsync(seed.Actor.Id, q.ToUtc.AddTicks(-1), seed.Branch.Id, null);
        var ap = await accounting.GetApAgingSummaryAsync(seed.Actor.Id, q.ToUtc.AddTicks(-1), seed.Branch.Id, null);
        Assert.Equal(60m, ar.Total);
        Assert.Equal(536m, ap.Total);
        var position = await repository.FinancialPositionAsync(q, default);
        Assert.Equal(ar.Total, position.Receivables);
        Assert.Equal(ap.Total, position.Payables);
        foreach (var supplier in new[] { false, true })
            foreach (var report in supplier ? new[] { "supplier-payments", "supplier-statement", "supplier-performance" } : new[] { "customer-payments", "customer-statement", "customer-performance" })
                _ = await repository.PartyDetailAsync(q, supplier, report, default);
        var daily = JsonSerializer.SerializeToElement(await service.ExecuteAsync(seed.Actor.Id, "management/daily", q, null, default), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(TimeSpan.FromDays(1), daily.GetProperty("toUtc").GetDateTime() - daily.GetProperty("fromUtc").GetDateTime());
    }

    [PostgreSqlFact, Trait("Category", "PostgreSQL")]
    public async Task Return_only_period_and_historical_cost_survive_current_price_changes()
    {
        await using var db = Open(); await using var tx = await db.Database.BeginTransactionAsync();
        var seed = await SeedAsync(db);
        var repository = new ReportingRepository(db);
        var q = seed.Query with { FromUtc = seed.Query.FromUtc.AddDays(1), ToUtc = seed.Query.FromUtc.AddDays(2) };
        var result = await repository.SalesAnalyticsAsync(q, "product", default);
        var returned = Assert.Single(result.Items);
        Assert.Equal(-90, returned.NetSales); Assert.Equal(-60, returned.CostOfGoodsSold); Assert.Equal(-30, returned.GrossProfit);
        var product = await db.Products.SingleAsync(x => x.Id.ToString() == returned.Key);
        product.PurchasePrice = 1000;
        await db.SaveChangesAsync();
        Assert.Equal(-30, Assert.Single((await repository.SalesAnalyticsAsync(q, "day", default)).Items).GrossProfit);
        Assert.Empty((await repository.SalesAnalyticsAsync(q with { ToUtc = q.FromUtc.AddTicks(1) }, "total", default)).Items);
    }

    private static PharmacyDbContext Open() => new(new DbContextOptionsBuilder<PharmacyDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("PHARMACY_TEST_CONNECTION_STRING")!).Options);
    private sealed record Seed(Branch Branch, User Actor, ReportQuery Query);
    private static async Task<Seed> SeedAsync(PharmacyDbContext db)
    {
        var key = Guid.NewGuid().ToString("N");
        var branch = new Branch { Code = key[..12], NormalizedCode = key[..12].ToUpperInvariant(), Name = "MIS " + key };
        var godown = new Godown { BranchId = branch.Id, Name = "MIS store", Code = "MIS", NormalizedCode = "MIS", IsDefault = true };
        var role = await db.Roles.SingleAsync(x => x.Name == RoleCatalog.Owner);
        var actor = new User { Username = key, NormalizedUsername = key.ToUpperInvariant(), FullName = "MIS owner", PasswordHash = "hash", RoleId = role.Id, BranchId = branch.Id };
        var category = new ProductCategory { Name = key, NormalizedName = key.ToUpperInvariant() };
        db.AddRange(branch, godown, actor, category);
        var from = new DateTime(2026, 8, 1, 19, 0, 0, DateTimeKind.Utc);
        var supplier = new Supplier { Name = "MIS supplier " + key, NormalizedName = key.ToUpperInvariant() };
        var customer = new Customer { CustomerCode = key[..12], Name = "MIS customer", NormalizedName = key, CreditLimit = 500 };
        db.AddRange(supplier, customer);
        var products = Enumerable.Range(0, ProductCount).Select(i => new Product { Name = "MIS " + i, SKU = key[..16] + i, NormalizedSku = (key[..16] + i).ToUpperInvariant(), Unit = "unit", PackSize = 1,
            CategoryId = category.Id, PurchasePrice = 60, RetailPrice = 100 }).ToList();
        var batches = products.Select(p => new ProductBatch { ProductId = p.Id, BranchId = branch.Id, GodownId = godown.Id,
            BatchNumber = "MIS", ExpiryDate = new(2026, 9, 1), PurchasePrice = 60, RetailPrice = 100, QuantityReceived = 100, QuantityAvailable = 100 }).ToList();
        db.AddRange(products); db.AddRange(batches);
        db.AddRange(batches.Select(b => new Inventory { ProductId = b.ProductId, ProductBatchId = b.Id, BranchId = branch.Id, GodownId = godown.Id,
            QuantityInStock = 100, LastCountedAt = from }));
        db.AddRange(batches.Select(b => new StockMovement { ProductId = b.ProductId, ProductBatchId = b.Id, BranchId = branch.Id, GodownId = godown.Id,
            MovementType = StockMovementType.OpeningStock, Quantity = 100, CreatedAt = from.AddDays(-40) }));
        var sales = Enumerable.Range(0, SaleCount).Select(i => new Sale { BranchId = branch.Id, GodownId = godown.Id, CashierUserId = actor.Id,
            Status = SaleStatus.Posted, InvoiceNumber = key[..12] + i, PostedAtUtc = from.AddHours(1), Subtotal = 100, DiscountTotal = 10, NetTotal = 90, AmountPaid = 90,
            Items = [new SaleItem { ProductId = products[i % ProductCount].Id, RequestedQuantity = 1, GrossAmount = 100, DiscountAmount = 10, NetAmount = 90,
                Allocations = [new SaleItemBatchAllocation { ProductBatchId = batches[i % ProductCount].Id, Quantity = 1, UnitRetailPriceSnapshot = 100,
                    UnitSalePriceSnapshot = 90, UnitCostPriceSnapshot = 60, ExpiryDateSnapshot = batches[i % ProductCount].ExpiryDate, GrossAmount = 100, DiscountAmount = 10, NetAmount = 90 }] }] }).ToList();
        db.AddRange(sales);
        sales[1].CustomerId = customer.Id; sales[1].CreditAmount = 90; sales[1].AmountPaid = 0; sales[1].DueDateUtc = from.AddDays(-5);
        foreach (var sale in sales)
        {
            var soldItem = sale.Items.Single(); var batch = batches.Single(x => x.ProductId == soldItem.ProductId);
            batch.QuantityAvailable--;
            db.StockMovements.Add(new StockMovement { ProductId = soldItem.ProductId, ProductBatchId = batch.Id, BranchId = branch.Id, GodownId = godown.Id,
                MovementType = StockMovementType.Sale, Quantity = -1, CreatedAt = sale.PostedAtUtc!.Value });
        }
        batches[0].QuantityAvailable++;
        db.StockMovements.Add(new StockMovement { ProductId = products[0].Id, ProductBatchId = batches[0].Id, BranchId = branch.Id, GodownId = godown.Id,
            MovementType = StockMovementType.SaleReturn, Quantity = 1, CreatedAt = from.AddDays(1).AddHours(1) });
        foreach (var inventory in db.ChangeTracker.Entries<Inventory>()) inventory.Entity.QuantityInStock = batches.Single(x => x.Id == inventory.Entity.ProductBatchId).QuantityAvailable;
        var order = new PurchaseOrder { BranchId = branch.Id, SupplierId = supplier.Id, OrderNumber = key + "PO", OrderDate = new(2026, 8, 2), Status = PurchaseOrderStatus.Completed,
            Items = [new PurchaseOrderItem { ProductId = products[0].Id, OrderedQuantity = 10, ReceivedQuantity = 10 }] };
        db.Add(order);
        var receipts = new List<GoodsReceipt>();
        for (var i = 0; i < 2; i++)
        {
            var qty = i == 0 ? 4 : 6; var cost = i == 0 ? 60m : 66m;
            var batch = new ProductBatch { ProductId = products[0].Id, BranchId = branch.Id, GodownId = godown.Id, SupplierId = supplier.Id,
                BatchNumber = "MIS purchase " + i, ExpiryDate = new(2026, 9, 1), PurchasePrice = cost, RetailPrice = 100, QuantityReceived = qty, QuantityAvailable = qty };
            db.Add(batch);
            var receipt = new GoodsReceipt { BranchId = branch.Id, GodownId = godown.Id, SupplierId = supplier.Id, PurchaseOrderId = order.Id, ReceivedByUserId = actor.Id,
                GrnNumber = key + "GRN" + i, ReceiptDate = new(2026, 8, 2 + i), DueDate = new(2026, 7, 31), Subtotal = qty * cost, NetTotal = qty * cost,
                Items = [new GoodsReceiptItem { ProductId = products[0].Id, ProductBatchId = batch.Id, PurchaseOrderItemId = order.Items.Single().Id, BatchNumber = batch.BatchNumber,
                    ExpiryDate = batch.ExpiryDate, PurchasedQuantity = qty, PurchasePrice = cost, RetailPrice = 100, NetLineAmount = qty * cost }] };
            db.Add(receipt); receipts.Add(receipt);
            db.Add(new Inventory { ProductId = products[0].Id, ProductBatchId = batch.Id, BranchId = branch.Id, GodownId = godown.Id, QuantityInStock = qty });
            db.Add(new StockMovement { ProductId = products[0].Id, ProductBatchId = batch.Id, BranchId = branch.Id, GodownId = godown.Id,
                MovementType = StockMovementType.Purchase, Quantity = qty, CreatedAt = from.AddDays(i).AddHours(2) });
            db.Add(new SupplierLedgerEntry { SupplierId = supplier.Id, BranchId = branch.Id, EntryType = SupplierLedgerEntryType.Purchase,
                Amount = qty * cost, EntryDate = receipt.ReceiptDate, ReferenceId = receipt.Id, ReferenceType = "GoodsReceipt" });
        }
        var supplierPayment = new SupplierLedgerEntry { SupplierId = supplier.Id, BranchId = branch.Id, EntryType = SupplierLedgerEntryType.Payment, Amount = -100,
            EntryDate = new(2026, 8, 4), CreatedByUserId = actor.Id };
        db.Add(supplierPayment);
        db.Add(new SupplierPaymentAllocation { SupplierId = supplier.Id, BranchId = branch.Id, SupplierLedgerEntryId = supplierPayment.Id, GoodsReceiptId = receipts[0].Id,
            AllocatedAmount = 100, AllocatedAtUtc = from.AddDays(2).AddHours(3), CreatedByUserId = actor.Id });
        var payment = new CustomerPayment { CustomerId = customer.Id, BranchId = branch.Id, ReceiptNumber = key + "CP", Amount = 30,
            PaymentMethod = CustomerPaymentMethod.Cash, PaymentDateUtc = from.AddDays(2).AddHours(3), ReceivedByUserId = actor.Id };
        db.Add(payment);
        db.Add(new CustomerPaymentAllocation { CustomerId = customer.Id, BranchId = branch.Id, CustomerPaymentId = payment.Id, SaleId = sales[1].Id,
            AllocatedAmount = 30, AllocatedAtUtc = payment.PaymentDateUtc, CreatedByUserId = actor.Id });
        db.AddRange(new CustomerLedgerEntry { CustomerId = customer.Id, BranchId = branch.Id, EntryType = CustomerLedgerEntryType.CreditSale, Amount = 90, EntryDate = new(2026, 8, 2), ReferenceId = sales[1].Id },
            new CustomerLedgerEntry { CustomerId = customer.Id, BranchId = branch.Id, EntryType = CustomerLedgerEntryType.Payment, Amount = -30, EntryDate = new(2026, 8, 4), ReferenceId = payment.Id });
        var original = sales[0]; var item = original.Items.Single(); var allocation = item.Allocations.Single();
        db.SalesReturns.Add(new SalesReturn { ReturnNumber = "R" + key[..12], OriginalSaleId = original.Id, BranchId = branch.Id, GodownId = godown.Id,
            ProcessedByUserId = actor.Id, ReturnDateUtc = from.AddDays(1).AddHours(1), PostedAtUtc = from.AddDays(1).AddHours(1), Reason = SalesReturnReason.CustomerReturn,
            GrossReturnAmount = 100, DiscountReturnAmount = 10, RefundAmount = 90, CashRefundAmount = 90,
            Items = [new SalesReturnItem { OriginalSaleItemId = item.Id, ProductId = item.ProductId, Quantity = 1, GrossReturnAmount = 100, DiscountReturnAmount = 10, RefundAmount = 90,
                Allocations = [new SalesReturnAllocation { OriginalSaleItemBatchAllocationId = allocation.Id, ProductBatchId = allocation.ProductBatchId, Quantity = 1,
                    Disposition = SalesReturnDisposition.Restockable, UnitRetailPriceSnapshot = 100, UnitSalePriceSnapshot = 90, UnitCostPriceSnapshot = 60,
                    ExpiryDateSnapshot = new(2026, 9, 1), GrossReturnAmount = 100, DiscountReturnAmount = 10, RefundAmount = 90 }] }] });
        var mappings = await new AccountingRepository(db).GetAccountMappingLookupAsync(default);
        JournalEntry Journal(string suffix, DateTime date, JournalSourceType source, params (AccountMappingKey account, decimal debit, decimal credit)[] lines) => new()
        {
            EntryNumber = key + suffix, EntryDateUtc = date, PostedAtUtc = date, SourceType = source, BranchId = branch.Id, PostedByUserId = actor.Id, Description = "MIS consistency fixture",
            Lines = lines.Select(l => new JournalEntryLine { BranchId = branch.Id, ChartOfAccountId = mappings[l.account], Debit = l.debit, Credit = l.credit }).ToList()
        };
        db.JournalEntries.Add(Journal("opening", from.AddDays(-40), JournalSourceType.OpeningBalance,
            (AccountMappingKey.Inventory, ProductCount * 6000m, 0), (AccountMappingKey.RetainedEarnings, 0, ProductCount * 6000m)));
        db.JournalEntries.Add(Journal("sales", from.AddHours(1), JournalSourceType.Sale,
            (AccountMappingKey.Cash, SaleCount * 90m - 90m, 0), (AccountMappingKey.AccountsReceivable, 90m, 0), (AccountMappingKey.SalesRevenue, 0, SaleCount * 90m),
            (AccountMappingKey.CostOfGoodsSold, SaleCount * 60m, 0), (AccountMappingKey.Inventory, 0, SaleCount * 60m)));
        db.JournalEntries.Add(Journal("return", from.AddDays(1).AddHours(1), JournalSourceType.SalesReturn,
            (AccountMappingKey.SalesRevenue, 90m, 0), (AccountMappingKey.Cash, 0, 90m),
            (AccountMappingKey.Inventory, 60m, 0), (AccountMappingKey.CostOfGoodsSold, 0, 60m)));
        for (var i = 0; i < receipts.Count; i++)
            db.JournalEntries.Add(Journal("purchase" + i, from.AddDays(i).AddHours(2), JournalSourceType.Purchase,
                (AccountMappingKey.Inventory, receipts[i].NetTotal, 0), (AccountMappingKey.AccountsPayable, 0, receipts[i].NetTotal)));
        db.JournalEntries.Add(Journal("customerpayment", payment.PaymentDateUtc, JournalSourceType.CustomerPayment,
            (AccountMappingKey.Cash, 30m, 0), (AccountMappingKey.AccountsReceivable, 0, 30m)));
        db.JournalEntries.Add(Journal("supplierpayment", from.AddDays(2).AddHours(3), JournalSourceType.SupplierPayment,
            (AccountMappingKey.AccountsPayable, 100m, 0), (AccountMappingKey.Cash, 0, 100m)));
        await db.SaveChangesAsync();
        return new(branch, actor, new(branch.Id, from, from.AddDays(3), PageSize: 200, GodownId: godown.Id));
    }
}
