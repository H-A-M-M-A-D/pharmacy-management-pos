using Npgsql;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Finance;
using Pharmacy.Application.DTOs.Purchasing;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Application.Services.Finance;
using Pharmacy.Application.Services.Purchasing;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Tests;

public sealed class PostgreSqlIntegrationTests
{
    private const string ConnectionVariable = "PHARMACY_TEST_CONNECTION_STRING";

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Draft_purchase_order_lines_can_be_replaced_and_removed_without_false_concurrency()
    {
        await using var seedConnection = await OpenConnectionAsync();
        await using var seedTransaction = await seedConnection.BeginTransactionAsync();
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable)!;
        var branchId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var productAId = Guid.NewGuid();
        var productBId = Guid.NewGuid();
        var productCId = Guid.NewGuid();
        var roleId = await ScalarAsync<Guid>(seedConnection, seedTransaction,
            "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\"='PurchaseManager';");

        await InsertBranchAsync(seedConnection, seedTransaction, branchId);
        await InsertSupplierAsync(seedConnection, seedTransaction, supplierId,
            Unique("draft-update-supplier"), Unique("DRAFT-UPDATE-SUPPLIER"));
        await InsertCategoryAsync(seedConnection, seedTransaction, categoryId);
        await InsertProductAsync(seedConnection, seedTransaction, categoryId, Unique("DRAFT-A"), null, productAId);
        await InsertProductAsync(seedConnection, seedTransaction, categoryId, Unique("DRAFT-B"), null, productBId);
        await InsertProductAsync(seedConnection, seedTransaction, categoryId, Unique("DRAFT-C"), null, productCId);
        var username = Unique("draft-update-user");
        await InsertUserAsync(seedConnection, seedTransaction, branchId, roleId, username,
            username.ToUpperInvariant(), null, null, actorId);
        await seedTransaction.CommitAsync();

        static DbContextOptions<PharmacyDbContext> Options(string value) =>
            new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql(value).Options;

        var orderDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var createRequest = new PurchaseOrderRequest(branchId, supplierId, orderDate,
            orderDate.AddDays(7), "acceptance-create", "Original notes",
            [
                new PurchaseOrderItemRequest(productAId, 10, 100m, "A"),
                new PurchaseOrderItemRequest(productBId, 5, 200m, "B")
            ]);
        PurchaseOrderDetailsDto created;
        await using (var createContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new PurchasingService(new PurchasingRepository(createContext), TimeProvider.System);
            created = await service.CreatePurchaseOrderAsync(actorId, createRequest);
        }

        var updateRequest = new PurchaseOrderRequest(branchId, supplierId, orderDate,
            orderDate.AddDays(14), "acceptance-update", "Updated notes",
            [
                new PurchaseOrderItemRequest(productAId, 11, 101m, "A updated"),
                new PurchaseOrderItemRequest(productBId, 6, 201m, "B updated"),
                new PurchaseOrderItemRequest(productCId, 2, 30m, "C added")
            ]);
        await using (var updateContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new PurchasingService(new PurchasingRepository(updateContext), TimeProvider.System);
            await service.UpdatePurchaseOrderAsync(actorId, created.Id, updateRequest);
        }

        await using (var verifyContext = new PharmacyDbContext(Options(connectionString)))
        {
            var order = await verifyContext.PurchaseOrders.AsNoTracking().Include(x => x.Items)
                .SingleAsync(x => x.Id == created.Id);
            Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
            Assert.Equal("Updated notes", order.Notes);
            Assert.Equal("acceptance-update", order.SupplierReference);
            Assert.Equal(orderDate.AddDays(14), order.ExpectedDate);
            Assert.Equal(3, order.Items.Count);
            Assert.Equal((11, 101m), order.Items.Where(x => x.ProductId == productAId)
                .Select(x => (x.OrderedQuantity, x.ExpectedPurchasePrice!.Value)).Single());
            Assert.Equal((6, 201m), order.Items.Where(x => x.ProductId == productBId)
                .Select(x => (x.OrderedQuantity, x.ExpectedPurchasePrice!.Value)).Single());
            Assert.Equal((2, 30m), order.Items.Where(x => x.ProductId == productCId)
                .Select(x => (x.OrderedQuantity, x.ExpectedPurchasePrice!.Value)).Single());
            Assert.Equal(2377m, order.Items.Sum(x => x.OrderedQuantity * x.ExpectedPurchasePrice!.Value));
            Assert.Equal(1, await verifyContext.AuditLogs.CountAsync(x => x.EntityType == "PurchaseOrder" &&
                x.EntityId == created.Id && x.Action == "PurchaseOrderUpdated"));
            Assert.False(await verifyContext.GoodsReceipts.AnyAsync(x => x.PurchaseOrderId == created.Id));
            Assert.False(await verifyContext.ProductBatches.AnyAsync(x =>
                x.ProductId == productAId || x.ProductId == productBId || x.ProductId == productCId));
            Assert.False(await verifyContext.Inventory.AnyAsync(x =>
                x.ProductId == productAId || x.ProductId == productBId || x.ProductId == productCId));
            Assert.False(await verifyContext.StockMovements.AnyAsync(x =>
                x.ProductId == productAId || x.ProductId == productBId || x.ProductId == productCId));
            Assert.False(await verifyContext.SupplierLedgerEntries.AnyAsync(x => x.SupplierId == supplierId));
            Assert.False(await verifyContext.FinancialLedgerEntries.AnyAsync(x => x.ReferenceId == created.Id));
        }

        var removeRequest = updateRequest with
        {
            Notes = "Removed one line",
            Items =
            [
                new PurchaseOrderItemRequest(productAId, 12, 102m, "A retained"),
                new PurchaseOrderItemRequest(productCId, 3, 31m, "C retained")
            ]
        };
        await using (var removeContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new PurchasingService(new PurchasingRepository(removeContext), TimeProvider.System);
            await service.UpdatePurchaseOrderAsync(actorId, created.Id, removeRequest);
        }

        await using (var finalContext = new PharmacyDbContext(Options(connectionString)))
        {
            var order = await finalContext.PurchaseOrders.AsNoTracking().Include(x => x.Items)
                .SingleAsync(x => x.Id == created.Id);
            Assert.Equal(2, order.Items.Count);
            Assert.DoesNotContain(order.Items, x => x.ProductId == productBId);
            Assert.Equal(2, await finalContext.AuditLogs.CountAsync(x => x.EntityType == "PurchaseOrder" &&
                x.EntityId == created.Id && x.Action == "PurchaseOrderUpdated"));
        }

        await using (var stateContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new PurchasingService(new PurchasingRepository(stateContext), TimeProvider.System);
            await service.SubmitPurchaseOrderAsync(actorId, created.Id);
        }
        await using (var immutableContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new PurchasingService(new PurchasingRepository(immutableContext), TimeProvider.System);
            await Assert.ThrowsAsync<RequestValidationException>(() =>
                service.UpdatePurchaseOrderAsync(actorId, created.Id, removeRequest));
            await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
                service.UpdatePurchaseOrderAsync(actorId, Guid.NewGuid(), removeRequest));
            Assert.Equal(2, await immutableContext.AuditLogs.CountAsync(x => x.EntityType == "PurchaseOrder" &&
                x.EntityId == created.Id && x.Action == "PurchaseOrderUpdated"));
        }
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Purchase_return_sequence_posts_paid_bonus_and_mixed_lines_with_unique_numbers_stock_and_credit()
    {
        await using var seedConnection = await OpenConnectionAsync();
        await using var seedTransaction = await seedConnection.BeginTransactionAsync();
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable)!;
        var branchId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var roleId = await ScalarAsync<Guid>(seedConnection, seedTransaction,
            "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\"='PurchaseManager';");

        await InsertBranchAsync(seedConnection, seedTransaction, branchId);
        await InsertSupplierAsync(seedConnection, seedTransaction, supplierId,
            Unique("pr-seq-supplier"), Unique("PR-SEQ-SUPPLIER"));
        await InsertCategoryAsync(seedConnection, seedTransaction, categoryId);
        await InsertProductAsync(seedConnection, seedTransaction, categoryId, Unique("PR-SEQ"), null, productId);
        var username = Unique("pr-seq-user");
        await InsertUserAsync(seedConnection, seedTransaction, branchId, roleId, username,
            username.ToUpperInvariant(), null, null, actorId);
        await seedTransaction.CommitAsync();

        static DbContextOptions<PharmacyDbContext> Options(string value) =>
            new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql(value).Options;

        var receiptDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var receiptRequest = new GoodsReceiptRequest(branchId, supplierId, null, Unique("INV-PR-SEQ"), receiptDate, null,
            [new GoodsReceiptItemRequest(productId, null, "B-PR-SEQ", null, receiptDate.AddDays(365), 100, 10, 50m, 60m, 0, 0)]);
        GoodsReceiptDetailsDto receipt;
        await using (var receiptContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new PurchasingService(new PurchasingRepository(receiptContext), TimeProvider.System);
            receipt = await service.PostGoodsReceiptAsync(actorId, receiptRequest);
        }
        var itemId = receipt.Items.Single().Id;

        // Historically, NextPurchaseReturnNumberAsync failed here with PostgreSQL 42703
        // ("column s.Value does not exist") because the scalar nextval(...) projection was unaliased.
        PurchaseReturnDetailsDto paid;
        await using (var paidContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new PurchasingService(new PurchasingRepository(paidContext), TimeProvider.System);
            paid = await service.PostPurchaseReturnAsync(actorId, receipt.Id,
                new PostPurchaseReturnRequest(PurchaseReturnReason.Damaged, null, [new PurchaseReturnItemRequest(itemId, 20, 0)]));
        }
        Assert.Equal(1000m, paid.NetSupplierCredit);

        PurchaseReturnDetailsDto bonus;
        await using (var bonusContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new PurchasingService(new PurchasingRepository(bonusContext), TimeProvider.System);
            bonus = await service.PostPurchaseReturnAsync(actorId, receipt.Id,
                new PostPurchaseReturnRequest(PurchaseReturnReason.ExcessSupply, null, [new PurchaseReturnItemRequest(itemId, 0, 5)]));
        }
        Assert.Equal(0m, bonus.NetSupplierCredit);

        PurchaseReturnDetailsDto mixed;
        await using (var mixedContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new PurchasingService(new PurchasingRepository(mixedContext), TimeProvider.System);
            mixed = await service.PostPurchaseReturnAsync(actorId, receipt.Id,
                new PostPurchaseReturnRequest(PurchaseReturnReason.WrongItem, null, [new PurchaseReturnItemRequest(itemId, 10, 5)]));
        }
        Assert.Equal(500m, mixed.NetSupplierCredit);

        var returnNumbers = new[] { paid.ReturnNumber, bonus.ReturnNumber, mixed.ReturnNumber };
        Assert.All(returnNumbers, x => Assert.False(string.IsNullOrWhiteSpace(x)));
        Assert.All(returnNumbers, x => Assert.Matches($@"^PR-{receiptDate.Year}-\d{{6}}$", x));
        Assert.Equal(3, returnNumbers.Distinct().Count());

        await using var verifyContext = new PharmacyDbContext(Options(connectionString));
        var batch = await verifyContext.ProductBatches.AsNoTracking().SingleAsync(x => x.ProductId == productId);
        var inventory = await verifyContext.Inventory.AsNoTracking().SingleAsync(x => x.ProductId == productId);
        Assert.Equal(70, batch.QuantityAvailable);
        Assert.Equal(70, inventory.QuantityInStock);

        Assert.Equal(-20, await verifyContext.StockMovements.Where(x => x.ReferenceId == paid.Id).SumAsync(x => x.Quantity));
        Assert.Equal(-5, await verifyContext.StockMovements.Where(x => x.ReferenceId == bonus.Id).SumAsync(x => x.Quantity));
        Assert.Equal(-15, await verifyContext.StockMovements.Where(x => x.ReferenceId == mixed.Id).SumAsync(x => x.Quantity));

        Assert.Equal(-1000m, await verifyContext.SupplierLedgerEntries.Where(x => x.ReferenceId == paid.Id).SumAsync(x => x.Amount));
        Assert.False(await verifyContext.SupplierLedgerEntries.AnyAsync(x => x.ReferenceId == bonus.Id));
        Assert.Equal(-500m, await verifyContext.SupplierLedgerEntries.Where(x => x.ReferenceId == mixed.Id).SumAsync(x => x.Amount));

        foreach (var returnId in new[] { paid.Id, bonus.Id, mixed.Id })
        {
            Assert.Equal(1, await verifyContext.AuditLogs.CountAsync(x =>
                x.EntityType == "PurchaseReturn" && x.EntityId == returnId && x.Action == "PurchaseReturnPosted"));
        }
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Purchase_return_rejects_over_return_and_mismatched_grn_item_without_partial_mutation()
    {
        await using var seedConnection = await OpenConnectionAsync();
        await using var seedTransaction = await seedConnection.BeginTransactionAsync();
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable)!;
        var branchId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var productAId = Guid.NewGuid();
        var productBId = Guid.NewGuid();
        var roleId = await ScalarAsync<Guid>(seedConnection, seedTransaction,
            "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\"='PurchaseManager';");

        await InsertBranchAsync(seedConnection, seedTransaction, branchId);
        await InsertSupplierAsync(seedConnection, seedTransaction, supplierId,
            Unique("pr-guard-supplier"), Unique("PR-GUARD-SUPPLIER"));
        await InsertCategoryAsync(seedConnection, seedTransaction, categoryId);
        await InsertProductAsync(seedConnection, seedTransaction, categoryId, Unique("PR-GUARD-A"), null, productAId);
        await InsertProductAsync(seedConnection, seedTransaction, categoryId, Unique("PR-GUARD-B"), null, productBId);
        var username = Unique("pr-guard-user");
        await InsertUserAsync(seedConnection, seedTransaction, branchId, roleId, username,
            username.ToUpperInvariant(), null, null, actorId);
        await seedTransaction.CommitAsync();

        static DbContextOptions<PharmacyDbContext> Options(string value) =>
            new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql(value).Options;

        var receiptDate = DateOnly.FromDateTime(DateTime.UtcNow);
        GoodsReceiptDetailsDto receiptA;
        GoodsReceiptDetailsDto receiptB;
        await using (var receiptContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new PurchasingService(new PurchasingRepository(receiptContext), TimeProvider.System);
            receiptA = await service.PostGoodsReceiptAsync(actorId, new GoodsReceiptRequest(branchId, supplierId, null, Unique("INV-PR-GUARD-A"), receiptDate, null,
                [new GoodsReceiptItemRequest(productAId, null, "B-PR-GUARD-A", null, receiptDate.AddDays(365), 10, 0, 50m, 60m, 0, 0)]));
            receiptB = await service.PostGoodsReceiptAsync(actorId, new GoodsReceiptRequest(branchId, supplierId, null, Unique("INV-PR-GUARD-B"), receiptDate, null,
                [new GoodsReceiptItemRequest(productBId, null, "B-PR-GUARD-B", null, receiptDate.AddDays(365), 10, 0, 50m, 60m, 0, 0)]));
        }
        var itemAId = receiptA.Items.Single().Id;
        var itemBId = receiptB.Items.Single().Id;

        await using (var wrongItemContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new PurchasingService(new PurchasingRepository(wrongItemContext), TimeProvider.System);
            await Assert.ThrowsAsync<RequestValidationException>(() => service.PostPurchaseReturnAsync(actorId, receiptA.Id,
                new PostPurchaseReturnRequest(PurchaseReturnReason.Damaged, null, [new PurchaseReturnItemRequest(itemBId, 1, 0)])));
        }

        await using (var overReturnContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new PurchasingService(new PurchasingRepository(overReturnContext), TimeProvider.System);
            await Assert.ThrowsAsync<ResourceConflictException>(() => service.PostPurchaseReturnAsync(actorId, receiptA.Id,
                new PostPurchaseReturnRequest(PurchaseReturnReason.Damaged, null, [new PurchaseReturnItemRequest(itemAId, 11, 0)])));
        }

        await using var verifyContext = new PharmacyDbContext(Options(connectionString));
        Assert.False(await verifyContext.PurchaseReturns.AnyAsync(x => x.OriginalGoodsReceiptId == receiptA.Id));
        var batchA = await verifyContext.ProductBatches.AsNoTracking().SingleAsync(x => x.ProductId == productAId);
        var inventoryA = await verifyContext.Inventory.AsNoTracking().SingleAsync(x => x.ProductId == productAId);
        Assert.Equal(10, batchA.QuantityAvailable);
        Assert.Equal(10, inventoryA.QuantityInStock);
        Assert.False(await verifyContext.StockMovements.AnyAsync(x => x.ProductId == productAId && x.MovementType == StockMovementType.PurchaseReturn));
        Assert.False(await verifyContext.SupplierLedgerEntries.AnyAsync(x => x.ReferenceType == "PurchaseReturn" && x.SupplierId == supplierId));
        Assert.False(await verifyContext.AuditLogs.AnyAsync(x => x.EntityType == "PurchaseReturn" && x.UserId == actorId));
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Purchase_return_concurrent_requests_against_the_same_batch_resolve_safely_without_unhandled_500()
    {
        await using var seedConnection = await OpenConnectionAsync();
        await using var seedTransaction = await seedConnection.BeginTransactionAsync();
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable)!;
        var branchId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var roleId = await ScalarAsync<Guid>(seedConnection, seedTransaction,
            "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\"='PurchaseManager';");

        await InsertBranchAsync(seedConnection, seedTransaction, branchId);
        await InsertSupplierAsync(seedConnection, seedTransaction, supplierId,
            Unique("pr-conc-supplier"), Unique("PR-CONC-SUPPLIER"));
        await InsertCategoryAsync(seedConnection, seedTransaction, categoryId);
        await InsertProductAsync(seedConnection, seedTransaction, categoryId, Unique("PR-CONC"), null, productId);
        var username = Unique("pr-conc-user");
        await InsertUserAsync(seedConnection, seedTransaction, branchId, roleId, username,
            username.ToUpperInvariant(), null, null, actorId);
        await seedTransaction.CommitAsync();

        static DbContextOptions<PharmacyDbContext> Options(string value) =>
            new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql(value).Options;

        // Exactly one final returnable quantity (10 paid, 0 bonus) so two competing
        // full-quantity returns against it cannot both succeed.
        var receiptDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var receiptRequest = new GoodsReceiptRequest(branchId, supplierId, null, Unique("INV-PR-CONC"), receiptDate, null,
            [new GoodsReceiptItemRequest(productId, null, "B-PR-CONC", null, receiptDate.AddDays(365), 10, 0, 50m, 60m, 0, 0)]);
        GoodsReceiptDetailsDto receipt;
        await using (var receiptContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new PurchasingService(new PurchasingRepository(receiptContext), TimeProvider.System);
            receipt = await service.PostGoodsReceiptAsync(actorId, receiptRequest);
        }
        var itemId = receipt.Items.Single().Id;
        var raceRequest = new PostPurchaseReturnRequest(PurchaseReturnReason.Damaged, null,
            [new PurchaseReturnItemRequest(itemId, 10, 0)]);

        // Each attempt gets its own DbContext/connection, mirroring two independent
        // HTTP requests, and both are launched without awaiting between them so their
        // Serializable transactions can genuinely overlap.
        async Task<(PurchaseReturnDetailsDto? Result, Exception? Error)> AttemptAsync()
        {
            await using var raceContext = new PharmacyDbContext(Options(connectionString));
            var service = new PurchasingService(new PurchasingRepository(raceContext), TimeProvider.System);
            try { return (await service.PostPurchaseReturnAsync(actorId, receipt.Id, raceRequest), null); }
            catch (Exception ex) { return (null, ex); }
        }

        var outcomes = await Task.WhenAll(Task.Run(AttemptAsync), Task.Run(AttemptAsync));

        var winners = outcomes.Where(x => x.Result is not null).ToList();
        var losers = outcomes.Where(x => x.Error is not null).ToList();
        Assert.Single(winners);
        Assert.Single(losers);

        // Whether the loser lost a genuine Postgres 40001 race (now mapped by
        // ExecuteInTransactionAsync) or simply observed the depleted quantity after the
        // winner committed, it must always surface as the application's own safe
        // conflict - never a raw/unhandled Postgres or EF exception (HTTP 500).
        var loserError = Assert.IsType<ResourceConflictException>(losers[0].Error);
        Assert.True(
            loserError.Message.Contains("remain returnable", StringComparison.OrdinalIgnoreCase) ||
            loserError.Message.Contains("changed by another operation", StringComparison.OrdinalIgnoreCase),
            $"Unexpected conflict message: {loserError.Message}");

        var winner = winners[0].Result!;
        Assert.Equal(500m, winner.NetSupplierCredit);

        await using var verifyContext = new PharmacyDbContext(Options(connectionString));
        var batch = await verifyContext.ProductBatches.AsNoTracking().SingleAsync(x => x.ProductId == productId);
        var inventory = await verifyContext.Inventory.AsNoTracking().SingleAsync(x => x.ProductId == productId);
        Assert.Equal(0, batch.QuantityAvailable);
        Assert.Equal(0, inventory.QuantityInStock);

        Assert.Equal(1, await verifyContext.PurchaseReturns.CountAsync(x => x.OriginalGoodsReceiptId == receipt.Id));
        Assert.Equal(1, await verifyContext.StockMovements.CountAsync(x =>
            x.ProductId == productId && x.MovementType == StockMovementType.PurchaseReturn));
        Assert.Equal(-10, await verifyContext.StockMovements.Where(x =>
            x.ProductId == productId && x.MovementType == StockMovementType.PurchaseReturn).SumAsync(x => x.Quantity));
        Assert.Equal(1, await verifyContext.SupplierLedgerEntries.CountAsync(x =>
            x.SupplierId == supplierId && x.EntryType == SupplierLedgerEntryType.PurchaseReturn));
        Assert.Equal(-500m, await verifyContext.SupplierLedgerEntries
            .Where(x => x.ReferenceId == winner.Id).SumAsync(x => x.Amount));
        Assert.Equal(1, await verifyContext.AuditLogs.CountAsync(x =>
            x.EntityType == "PurchaseReturn" && x.EntityId == winner.Id && x.Action == "PurchaseReturnPosted"));
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Sale_payments_generate_exact_financial_entries_without_mutating_live_tracker_enumeration()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var options = new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql(connection).Options;
        await using var context = new PharmacyDbContext(options);
        await context.Database.UseTransactionAsync(transaction);

        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();
        var cashAccountId = Guid.NewGuid();
        var cardAccountId = Guid.NewGuid();
        var roleId = await ScalarAsync<Guid>(connection, transaction,
            "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\"='Owner';");

        await InsertBranchAsync(connection, transaction, branchId);
        var username = Unique("sale-payment-user");
        await InsertUserAsync(connection, transaction, branchId, roleId, username,
            username.ToUpperInvariant(), null, null, userId);
        await InsertCategoryAsync(connection, transaction, categoryId);
        await InsertProductAsync(connection, transaction, categoryId, Unique("SALE-PAYMENT"), null, productId);
        await InsertBatchAsync(connection, transaction, branchId, productId, Unique("SALE-BATCH"), batchId);
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "Inventory"
                ("Id","BranchId","ProductId","ProductBatchId","QuantityInStock","ReorderLevel","LastCountedAt","CreatedAt","UpdatedAt")
            VALUES (@id,@branch,@product,@batch,100,0,now(),now(),now());
            """, ("id", inventoryId), ("branch", branchId), ("product", productId), ("batch", batchId));
        await InsertMovementAsync(connection, transaction, branchId, productId, batchId, (int)StockMovementType.OpeningStock, 100);
        foreach (var (id, name, type) in new[]
                 {
                     (cashAccountId, Unique("Sale cash"), (int)FinancialAccountType.Cash),
                     (cardAccountId, Unique("Sale card"), (int)FinancialAccountType.CardSettlement)
                 })
        {
            await ExecuteAsync(connection, transaction, """
                INSERT INTO "FinancialAccounts"
                    ("Id","BranchId","Name","NormalizedName","AccountType","OpeningBalance","IsActive","CreatedAt","UpdatedAt")
                VALUES (@id,@branch,@name,@normalized,@type,0,true,now(),now());
                """, ("id", id), ("branch", branchId), ("name", name),
                ("normalized", name.ToUpperInvariant()), ("type", type));
        }

        var batch = await context.ProductBatches.SingleAsync(x => x.Id == batchId);
        var inventory = await context.Inventory.SingleAsync(x => x.Id == inventoryId);
        var cashSale = CreatePostedSale(branchId, userId, productId, batch, 1, 4500m,
            [new SalePayment { Method = SalePaymentMethod.Cash, AmountApplied = 4500m, TenderedAmount = 5000m, FinancialAccountId = cashAccountId }]);
        batch.QuantityAvailable -= 1;
        inventory.QuantityInStock -= 1;
        context.Sales.Add(cashSale);
        context.StockMovements.Add(CreateSaleMovement(branchId, productId, batchId, userId, cashSale.Id, -1));

        await context.SaveChangesAsync();

        Assert.Equal(SaleStatus.Posted, cashSale.Status);
        Assert.Equal(500m, cashSale.ChangeGiven);
        Assert.Equal(1L, await context.SalePayments.CountAsync(x => x.SaleId == cashSale.Id));
        var cashEntries = await context.FinancialLedgerEntries.AsNoTracking()
            .Where(x => x.ReferenceType == "SalePayment" && x.ReferenceId == cashSale.Payments.Single().Id)
            .ToListAsync();
        Assert.Single(cashEntries);
        Assert.Equal(4500m, cashEntries[0].Amount);
        Assert.Equal(99, batch.QuantityAvailable);
        Assert.Equal(99, inventory.QuantityInStock);
        Assert.Equal(-1, await context.StockMovements.Where(x => x.ReferenceId == cashSale.Id).SumAsync(x => x.Quantity));

        var splitSale = CreatePostedSale(branchId, userId, productId, batch, 5, 5000m,
            [
                new SalePayment { Method = SalePaymentMethod.Cash, AmountApplied = 2000m, TenderedAmount = 2000m, FinancialAccountId = cashAccountId },
                new SalePayment { Method = SalePaymentMethod.Card, AmountApplied = 3000m, FinancialAccountId = cardAccountId }
            ]);
        batch.QuantityAvailable -= 5;
        inventory.QuantityInStock -= 5;
        context.Sales.Add(splitSale);
        context.StockMovements.Add(CreateSaleMovement(branchId, productId, batchId, userId, splitSale.Id, -5));

        await context.SaveChangesAsync();

        var splitPaymentIds = splitSale.Payments.Select(x => x.Id).ToArray();
        var splitEntries = await context.FinancialLedgerEntries.AsNoTracking()
            .Where(x => x.ReferenceType == "SalePayment" && splitPaymentIds.Contains(x.ReferenceId))
            .ToListAsync();
        Assert.Equal(2, splitSale.Payments.Count);
        Assert.Equal(2, splitEntries.Count);
        Assert.Equal(5000m, splitEntries.Sum(x => x.Amount));
        Assert.Equal(2000m, splitEntries.Single(x => x.FinancialAccountId == cashAccountId).Amount);
        Assert.Equal(3000m, splitEntries.Single(x => x.FinancialAccountId == cardAccountId).Amount);
        Assert.Equal(94, batch.QuantityAvailable);
        Assert.Equal(94, inventory.QuantityInStock);

        await transaction.RollbackAsync();
    }

    private static Sale CreatePostedSale(Guid branchId, Guid userId, Guid productId,
        ProductBatch batch, int quantity, decimal total, IReadOnlyList<SalePayment> payments)
    {
        var sale = new Sale
        {
            BranchId = branchId,
            CashierUserId = userId,
            InvoiceNumber = Unique("INV-PAYMENT"),
            Status = SaleStatus.Posted,
            PostedAtUtc = DateTime.UtcNow,
            Subtotal = total,
            NetTotal = total,
            AmountPaid = total,
            ChangeGiven = payments.Sum(x => (x.TenderedAmount ?? x.AmountApplied) - x.AmountApplied)
        };
        var item = new SaleItem
        {
            ProductId = productId,
            RequestedQuantity = quantity,
            GrossAmount = total,
            NetAmount = total
        };
        item.Allocations.Add(new SaleItemBatchAllocation
        {
            ProductBatchId = batch.Id,
            Quantity = quantity,
            UnitRetailPriceSnapshot = total / quantity,
            UnitSalePriceSnapshot = total / quantity,
            UnitCostPriceSnapshot = batch.PurchasePrice,
            ExpiryDateSnapshot = batch.ExpiryDate,
            GrossAmount = total,
            NetAmount = total
        });
        sale.Items.Add(item);
        foreach (var payment in payments) sale.Payments.Add(payment);
        return sale;
    }

    private static StockMovement CreateSaleMovement(Guid branchId, Guid productId, Guid batchId,
        Guid userId, Guid saleId, int quantity) => new()
    {
        MovementType = StockMovementType.Sale,
        BranchId = branchId,
        ProductId = productId,
        ProductBatchId = batchId,
        Quantity = quantity,
        ReferenceType = "Sale",
        ReferenceId = saleId,
        PerformedByUserId = userId
    };

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Customer_repository_sequences_generate_unique_codes_and_payment_receipts()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var options = new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql(connection).Options;
        await using var context = new PharmacyDbContext(options);
        await context.Database.UseTransactionAsync(transaction);
        var repository = new CustomerRepository(context);

        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var roleId = await ScalarAsync<Guid>(connection, transaction,
            "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\"='Owner';");
        await InsertBranchAsync(connection, transaction, branchId);
        var username = Unique("customer-sequence-user");
        await InsertUserAsync(connection, transaction, branchId, roleId, username,
            username.ToUpperInvariant(), null, null, userId);
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "FinancialAccounts"
                ("Id","BranchId","Name","NormalizedName","AccountType","OpeningBalance","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id,@branch,@name,@normalized,1,0,true,now(),now());
            """, ("id", accountId), ("branch", branchId),
            ("name", Unique("Customer sequence cash")), ("normalized", Unique("CUSTOMER SEQUENCE CASH")));

        var firstCode = await repository.NextCustomerCodeAsync();
        var secondCode = await repository.NextCustomerCodeAsync();
        Assert.NotEqual(firstCode, secondCode);
        Assert.StartsWith("CUS-", firstCode);

        var firstCustomer = new Customer
        {
            CustomerCode = firstCode,
            Name = "Sequence Customer One",
            NormalizedName = "SEQUENCE CUSTOMER ONE",
            CreditLimit = 1000m
        };
        var secondCustomer = new Customer
        {
            CustomerCode = secondCode,
            Name = "Sequence Customer Two",
            NormalizedName = "SEQUENCE CUSTOMER TWO",
            CreditLimit = 1000m
        };
        await repository.AddCustomerAsync(firstCustomer);
        await repository.AddCustomerAsync(secondCustomer);
        await repository.SaveChangesAsync();

        var paymentDate = DateTime.UtcNow;
        var firstReceipt = await repository.NextPaymentReceiptNumberAsync(paymentDate);
        var secondReceipt = await repository.NextPaymentReceiptNumberAsync(paymentDate);
        Assert.NotEqual(firstReceipt, secondReceipt);
        Assert.StartsWith($"CR-{paymentDate.Year}-", firstReceipt);

        foreach (var (customer, receipt) in new[] { (firstCustomer, firstReceipt), (secondCustomer, secondReceipt) })
        {
            var payment = new CustomerPayment
            {
                ReceiptNumber = receipt,
                CustomerId = customer.Id,
                BranchId = branchId,
                Amount = 100m,
                PaymentMethod = CustomerPaymentMethod.Cash,
                PaymentDateUtc = paymentDate,
                ReceivedByUserId = userId,
                FinancialAccountId = accountId
            };
            await repository.AddPaymentAsync(payment);
            await repository.AddLedgerEntryAsync(new CustomerLedgerEntry
            {
                CustomerId = customer.Id,
                BranchId = branchId,
                EntryType = CustomerLedgerEntryType.Payment,
                Amount = -payment.Amount,
                EntryDate = DateOnly.FromDateTime(paymentDate),
                ReferenceType = "CustomerPayment",
                ReferenceId = payment.Id,
                ReferenceNumber = receipt,
                CreatedByUserId = userId
            });
        }
        await repository.SaveChangesAsync();

        Assert.Equal(2L, await ScalarAsync<long>(connection, transaction,
            "SELECT count(*) FROM \"Customers\" WHERE \"Id\" IN (@first,@second);",
            ("first", firstCustomer.Id), ("second", secondCustomer.Id)));
        Assert.Equal(2L, await ScalarAsync<long>(connection, transaction,
            "SELECT count(*) FROM \"CustomerPayments\" WHERE \"ReceiptNumber\" IN (@first,@second);",
            ("first", firstReceipt), ("second", secondReceipt)));
        Assert.Equal(2L, await ScalarAsync<long>(connection, transaction,
            "SELECT count(*) FROM \"CustomerLedgerEntries\" WHERE \"ReferenceNumber\" IN (@first,@second);",
            ("first", firstReceipt), ("second", secondReceipt)));
        Assert.Equal(2L, await ScalarAsync<long>(connection, transaction,
            "SELECT count(*) FROM \"FinancialLedgerEntries\" WHERE \"ReferenceNumber\" IN (@first,@second);",
            ("first", firstReceipt), ("second", secondReceipt)));

        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Migration_creates_expected_tables_and_postgresql_types()
    {
        await using var connection = await OpenConnectionAsync();

        Assert.Equal(
            "10.0.11",
            await ScalarAsync<string>(connection, null, """
                SELECT "ProductVersion"
                FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20260829211152_InitialCreate';
                """));

        Assert.Equal(
            14L,
            await ScalarAsync<long>(connection, null, """
                SELECT count(*)
                FROM "__EFMigrationsHistory"
                WHERE "MigrationId" IN (
                    '20260829211152_InitialCreate',
                    '20260829223012_AddUserSecurityAndManagement',
                    '20260901194508_CompleteProductMaster',
                    '20260901215409_CompleteBatchAndInventoryManagement',
                    '20260902051500_CompleteSupplierManagement',
                    '20260902055022_CompletePurchasingAndGoodsReceiving',
                    '20260902114037_CompletePosAndSales',
                    '20260902222101_CompleteSalesReturnsAndRefunds',
                    '20260903231207_CompletePurchaseReturns',
                    '20260904125716_CompleteCustomerManagementAndCreditSales',
                    '20260907195029_CompleteAccountsExpensesAndCashManagement',
                    '20260907210154_AddReportingPermissions',
                    '20260907220809_CompleteSystemAdministration',
                    '20260907222557_EnforceAuditImmutability');
                """));

        Assert.Equal(
            39L,
            await ScalarAsync<long>(connection, null, """
                SELECT count(*)
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_type = 'BASE TABLE'
                  AND table_name <> '__EFMigrationsHistory';
                """));

        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand("""
            SELECT table_name || '.' || column_name, data_type
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND (table_name, column_name) IN (
                  ('AuditLogs', 'OldValues'),
                  ('AuditLogs', 'NewValues'),
                  ('ProductBatches', 'ExpiryDate'),
                  ('ProductBatches', 'ManufacturingDate'),
                  ('PurchaseOrders', 'OrderDate'),
                  ('GoodsReceipts', 'ReceiptDate'),
                  ('GoodsReceiptItems', 'ExpiryDate'),
                  ('GoodsReceiptItems', 'DiscountPercent'),
                  ('GoodsReceiptItems', 'NetLineAmount'),
                  ('StockMovements', 'CreatedAt'),
                  ('Sales', 'PostedAtUtc'),
                  ('Sales', 'NetTotal'),
                  ('SaleItemBatchAllocations', 'ExpiryDateSnapshot'),
                  ('SalesReturns', 'ReturnDateUtc'),
                  ('SalesReturns', 'RefundAmount'),
                  ('SalesReturnAllocations', 'ExpiryDateSnapshot'),
                  ('SalesRefundPayments', 'Amount'),
                  ('PurchaseReturns', 'ReturnDateUtc'),
                  ('PurchaseReturns', 'NetSupplierCredit'),
                  ('PurchaseReturnItems', 'ExpiryDate'),
                  ('PurchaseReturnItems', 'NetSupplierCredit'),
                  ('Customers', 'CreditLimit'),
                  ('CustomerLedgerEntries', 'EntryDate'),
                  ('CustomerLedgerEntries', 'Amount'),
                  ('CustomerPayments', 'PaymentDateUtc'),
                  ('CustomerPayments', 'Amount'),
                  ('Sales', 'CreditAmount'),
                  ('SalesReturns', 'CustomerCreditReductionAmount'),
                  ('SalesReturns', 'CashRefundAmount'));
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                types.Add(reader.GetString(0), reader.GetString(1));
            }
        }

        Assert.Equal("jsonb", types["AuditLogs.OldValues"]);
        Assert.Equal("jsonb", types["AuditLogs.NewValues"]);
        Assert.Equal("date", types["ProductBatches.ExpiryDate"]);
        Assert.Equal("date", types["ProductBatches.ManufacturingDate"]);
        Assert.Equal("date", types["PurchaseOrders.OrderDate"]);
        Assert.Equal("date", types["GoodsReceipts.ReceiptDate"]);
        Assert.Equal("date", types["GoodsReceiptItems.ExpiryDate"]);
        Assert.Equal("numeric", types["GoodsReceiptItems.DiscountPercent"]);
        Assert.Equal("numeric", types["GoodsReceiptItems.NetLineAmount"]);
        Assert.Equal("timestamp with time zone", types["StockMovements.CreatedAt"]);
        Assert.Equal("timestamp with time zone", types["Sales.PostedAtUtc"]);
        Assert.Equal("numeric", types["Sales.NetTotal"]);
        Assert.Equal("date", types["SaleItemBatchAllocations.ExpiryDateSnapshot"]);
        Assert.Equal("timestamp with time zone", types["SalesReturns.ReturnDateUtc"]);
        Assert.Equal("numeric", types["SalesReturns.RefundAmount"]);
        Assert.Equal("date", types["SalesReturnAllocations.ExpiryDateSnapshot"]);
        Assert.Equal("numeric", types["SalesRefundPayments.Amount"]);
        Assert.Equal("timestamp with time zone", types["PurchaseReturns.ReturnDateUtc"]);
        Assert.Equal("numeric", types["PurchaseReturns.NetSupplierCredit"]);
        Assert.Equal("date", types["PurchaseReturnItems.ExpiryDate"]);
        Assert.Equal("numeric", types["PurchaseReturnItems.NetSupplierCredit"]);
        Assert.Equal("numeric", types["Customers.CreditLimit"]);
        Assert.Equal("date", types["CustomerLedgerEntries.EntryDate"]);
        Assert.Equal("numeric", types["CustomerLedgerEntries.Amount"]);
        Assert.Equal("timestamp with time zone", types["CustomerPayments.PaymentDateUtc"]);
        Assert.Equal("numeric", types["CustomerPayments.Amount"]);
        Assert.Equal("numeric", types["Sales.CreditAmount"]);
        Assert.Equal("numeric", types["SalesReturns.CustomerCreditReductionAmount"]);
        Assert.Equal("numeric", types["SalesReturns.CashRefundAmount"]);
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase12_reporting_permissions_and_queries_are_real()
    {
        await using var connection = await OpenConnectionAsync();
        Assert.Equal(7L, await ScalarAsync<long>(connection, null,
            "SELECT count(*) FROM \"Permissions\" WHERE \"Code\" LIKE 'reports.%';"));
        Assert.Equal(7L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM "RolePermissions" rp
            JOIN "Roles" r ON r."Id"=rp."RoleId"
            JOIN "Permissions" p ON p."Id"=rp."PermissionId"
            WHERE r."Name"='Owner' AND p."Code" LIKE 'reports.%';
            """));
        Assert.Equal(0L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM "RolePermissions" rp
            JOIN "Roles" r ON r."Id"=rp."RoleId"
            JOIN "Permissions" p ON p."Id"=rp."PermissionId"
            WHERE r."Name" IN ('Cashier','Pharmacist','StoreKeeper','PurchaseManager','Accountant')
              AND p."Code"='reports.profitability';
            """));

        var options = new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql(connection).Options;
        await using var context = new PharmacyDbContext(options);
        var reports = new ReportingRepository(context);
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var query = new ReportQuery(null, from, to, 1, 20);
        _ = await reports.SalesSummaryAsync(null, from, to, default);
        _ = await reports.DailySalesAsync(null, query, default);
        _ = await reports.ProductSalesAsync(null, from, to, default);
        _ = await reports.SalesByCategoryAsync(null, from, to, default);
        _ = await reports.SalesByCashierAsync(null, from, to, default);
        _ = await reports.SalesByPaymentAsync(null, from, to, default);
        _ = await reports.DiscountsAsync(null, query, default);
        _ = await reports.CreditSalesAsync(null, query, default);
        _ = await reports.PurchaseSummaryAsync(null, from, to, default);
        _ = await reports.PurchasesBySupplierAsync(null, from, to, default);
        _ = await reports.PurchasesByProductAsync(null, from, to, default);
        _ = await reports.PurchaseReturnsAsync(null, query, default);
        _ = await reports.CurrentStockAsync(null, null, default);
        _ = await reports.BatchStockAsync(null, query, 30, false, default);
        _ = await reports.StockMovementsAsync(null, query, null, default);
        _ = await reports.InventorySummaryAsync(null, from, default);
        _ = await reports.ExpensesAsync(null, query, default);
        _ = await reports.OtherIncomeAsync(null, query, default);
        _ = await reports.CustomerOutstandingAsync(null, default);
        _ = await reports.SupplierOutstandingAsync(null, default);
        _ = await reports.AccountLedgerAsync(null, null, query, default);
        _ = await reports.CashPositionAsync(null, from, to, default);
        _ = await reports.TrendAsync(null, from, to, default);
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task User_security_columns_and_normalized_uniqueness_are_enforced()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var branchId = Guid.NewGuid();
        await InsertBranchAsync(connection, transaction, branchId);
        var roleId = await ScalarAsync<Guid>(connection, transaction, """
            SELECT "Id" FROM "Roles" WHERE "Name" = 'Cashier';
            """);

        await InsertUserAsync(connection, transaction, branchId, roleId, "case-user", "CASE-USER", null, null);
        await InsertUserAsync(connection, transaction, branchId, roleId, "null-email", "NULL-EMAIL", null, null);
        await AssertDatabaseErrorAsync(
            connection,
            transaction,
            "duplicate_normalized_username",
            PostgresErrorCodes.UniqueViolation,
            () => InsertUserAsync(connection, transaction, branchId, roleId, "Case-User", "CASE-USER", null, null));

        await InsertUserAsync(
            connection, transaction, branchId, roleId, "email-one", "EMAIL-ONE", "One@Example.test", "ONE@EXAMPLE.TEST");
        await AssertDatabaseErrorAsync(
            connection,
            transaction,
            "duplicate_normalized_email",
            PostgresErrorCodes.UniqueViolation,
            () => InsertUserAsync(
                connection, transaction, branchId, roleId, "email-two", "EMAIL-TWO", "one@example.test", "ONE@EXAMPLE.TEST"));

        await ExecuteAsync(connection, transaction, """
            UPDATE "Users"
            SET "FailedLoginAttempts" = 5,
                "LockoutEndUtc" = now() + interval '15 minutes'
            WHERE "NormalizedUsername" = 'CASE-USER';
            """);
        Assert.Equal(
            5,
            await ScalarAsync<int>(connection, transaction, """
                SELECT "FailedLoginAttempts"
                FROM "Users"
                WHERE "NormalizedUsername" = 'CASE-USER'
                  AND "LockoutEndUtc" > now()
                  AND "RoleId" = (SELECT "Id" FROM "Roles" WHERE "Name" = 'Cashier');
                """));
        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Product_unique_indexes_allow_null_barcodes_but_reject_duplicates()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var categoryId = Guid.NewGuid();

        await ExecuteAsync(connection, transaction, """
            INSERT INTO "ProductCategories"
                ("Id", "Name", "NormalizedName", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES (@id, @name, @normalized, true, now(), now());
            """, ("id", categoryId), ("name", Unique("category")), ("normalized", Unique("category").ToUpperInvariant()));

        await InsertProductAsync(connection, transaction, categoryId, Unique("sku"), null);
        await InsertProductAsync(connection, transaction, categoryId, Unique("sku"), null);

        var duplicateSku = Unique("sku");
        await InsertProductAsync(connection, transaction, categoryId, duplicateSku, null);
        await AssertDatabaseErrorAsync(
            connection,
            transaction,
            "duplicate_sku",
            PostgresErrorCodes.UniqueViolation,
            () => InsertProductAsync(connection, transaction, categoryId, duplicateSku, null));

        var duplicateBarcode = Unique("barcode");
        await InsertProductAsync(connection, transaction, categoryId, Unique("sku"), duplicateBarcode);
        await AssertDatabaseErrorAsync(
            connection,
            transaction,
            "duplicate_barcode",
            PostgresErrorCodes.UniqueViolation,
            () => InsertProductAsync(connection, transaction, categoryId, Unique("sku"), duplicateBarcode));

        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Product_master_constraints_and_normalized_catalog_names_are_enforced()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var categoryId = Guid.NewGuid();
        var manufacturerId = Guid.NewGuid();
        await InsertCategoryAsync(connection, transaction, categoryId, "Tablets", "TABLETS");
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "Manufacturers" ("Id","Name","NormalizedName","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id,'Acme Pharma','ACME PHARMA',true,now(),now());
            """, ("id", manufacturerId));
        await AssertDatabaseErrorAsync(connection, transaction, "duplicate_category_name", PostgresErrorCodes.UniqueViolation,
            () => InsertCategoryAsync(connection, transaction, Guid.NewGuid(), " tablets ", "TABLETS"));
        await AssertDatabaseErrorAsync(connection, transaction, "duplicate_manufacturer_name", PostgresErrorCodes.UniqueViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "Manufacturers" ("Id","Name","NormalizedName","IsActive","CreatedAt","UpdatedAt")
                VALUES (@id,'acme pharma','ACME PHARMA',true,now(),now());
                """, ("id", Guid.NewGuid())));
        await InsertProductAsync(connection, transaction, categoryId, "Mixed-Case", null, null, "MIXED-CASE", manufacturerId);
        await AssertDatabaseErrorAsync(connection, transaction, "duplicate_normalized_sku", PostgresErrorCodes.UniqueViolation,
            () => InsertProductAsync(connection, transaction, categoryId, "mixed-case", null, null, "MIXED-CASE"));
        await AssertDatabaseErrorAsync(connection, transaction, "invalid_product_fk", PostgresErrorCodes.ForeignKeyViolation,
            () => InsertProductAsync(connection, transaction, Guid.NewGuid(), Unique("sku"), null));
        await AssertDatabaseErrorAsync(connection, transaction, "negative_price", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "Products" ("Id","SKU","NormalizedSku","Name","CategoryId","Unit","PackSize","PurchasePrice","RetailPrice","ReorderLevel","MaximumDiscountPercent","IsActive","CreatedAt","UpdatedAt")
                VALUES (@id,@sku,@normalized,'Invalid',@category,'Piece',1,-1,1,0,0,true,now(),now());
                """, ("id", Guid.NewGuid()), ("sku", Unique("sku")), ("normalized", Unique("sku").ToUpperInvariant()), ("category", categoryId)));
        await AssertDatabaseErrorAsync(connection, transaction, "invalid_discount", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "Products" ("Id","SKU","NormalizedSku","Name","CategoryId","Unit","PackSize","PurchasePrice","RetailPrice","ReorderLevel","MaximumDiscountPercent","IsActive","CreatedAt","UpdatedAt")
                VALUES (@id,@sku,@normalized,'Invalid',@category,'Piece',1,1,1,0,101,true,now(),now());
                """, ("id", Guid.NewGuid()), ("sku", Unique("sku")), ("normalized", Unique("sku").ToUpperInvariant()), ("category", categoryId)));
        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Batch_number_uniqueness_is_scoped_by_branch_and_product()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var categoryId = Guid.NewGuid();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var batchNumber = Unique("batch");

        await InsertCategoryAsync(connection, transaction, categoryId);
        await InsertBranchAsync(connection, transaction, branchA);
        await InsertBranchAsync(connection, transaction, branchB);
        await InsertProductAsync(connection, transaction, categoryId, Unique("sku"), null, productA);
        await InsertProductAsync(connection, transaction, categoryId, Unique("sku"), null, productB);
        await InsertBatchAsync(connection, transaction, branchA, productA, batchNumber);

        await AssertDatabaseErrorAsync(
            connection,
            transaction,
            "duplicate_batch",
            PostgresErrorCodes.UniqueViolation,
            () => InsertBatchAsync(connection, transaction, branchA, productA, batchNumber));

        await InsertBatchAsync(connection, transaction, branchA, productB, batchNumber);
        await InsertBatchAsync(connection, transaction, branchB, productA, batchNumber);
        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Stock_movement_check_constraint_enforces_quantity_signs()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var categoryId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        await InsertCategoryAsync(connection, transaction, categoryId);
        await InsertBranchAsync(connection, transaction, branchId);
        await InsertProductAsync(connection, transaction, categoryId, Unique("sku"), null, productId);
        await InsertBatchAsync(connection, transaction, branchId, productId, Unique("batch"), batchId);

        await AssertDatabaseErrorAsync(
            connection,
            transaction,
            "sale_positive",
            PostgresErrorCodes.CheckViolation,
            () => InsertMovementAsync(connection, transaction, branchId, productId, batchId, 3, 10));
        await AssertDatabaseErrorAsync(
            connection,
            transaction,
            "purchase_negative",
            PostgresErrorCodes.CheckViolation,
            () => InsertMovementAsync(connection, transaction, branchId, productId, batchId, 2, -10));

        await InsertMovementAsync(connection, transaction, branchId, productId, batchId, 3, -10);
        await InsertMovementAsync(connection, transaction, branchId, productId, batchId, 2, 10);
        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase4_inventory_constraints_indexes_and_permissions_exist()
    {
        await using var connection = await OpenConnectionAsync();

        Assert.Equal(
            7L,
            await ScalarAsync<long>(connection, null, """
                SELECT count(*) FROM "Permissions"
                WHERE "Code" IN ('inventory.view','inventory.opening_stock','inventory.adjust','inventory.stock_count',
                    'inventory.expiry_manage','inventory.movements.view','batches.view');
                """));

        Assert.Equal(
            6L,
            await ScalarAsync<long>(connection, null, """
                SELECT count(*) FROM information_schema.table_constraints
                WHERE table_schema = 'public'
                  AND constraint_name IN (
                    'CK_ProductBatches_QuantityAvailable_NonNegative',
                    'CK_ProductBatches_QuantityReceived_NonNegative',
                    'CK_ProductBatches_Prices_NonNegative',
                    'CK_ProductBatches_Manufacturing_Before_Expiry',
                    'CK_Inventory_QuantityInStock_NonNegative',
                    'CK_Inventory_ReorderLevel_NonNegative');
                """));

        Assert.Equal(
            7L,
            await ScalarAsync<long>(connection, null, """
                SELECT count(*) FROM pg_indexes
                WHERE schemaname = 'public'
                  AND indexname IN (
                    'IX_ProductBatches_BranchId_ExpiryDate',
                    'IX_ProductBatches_BranchId_ProductId',
                    'IX_ProductBatches_ProductId_BatchNumber',
                    'IX_ProductBatches_QuantityAvailable',
                    'IX_Inventory_BranchId_ProductId',
                    'IX_StockMovements_BranchId_ProductId_CreatedAt',
                    'IX_StockMovements_ProductBatchId_CreatedAt');
                """));
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase4_non_negative_projection_constraints_are_enforced()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var categoryId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        await InsertCategoryAsync(connection, transaction, categoryId);
        await InsertBranchAsync(connection, transaction, branchId);
        await InsertProductAsync(connection, transaction, categoryId, Unique("sku"), null, productId);

        await AssertDatabaseErrorAsync(connection, transaction, "negative_batch_available", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "ProductBatches"
                    ("Id","ProductId","BranchId","BatchNumber","ExpiryDate","PurchasePrice","RetailPrice","QuantityReceived","QuantityAvailable","IsDisposed","CreatedAt","UpdatedAt")
                VALUES (@id,@product,@branch,@batch,current_date + 30,1,1,0,-1,false,now(),now());
                """, ("id", batchId), ("product", productId), ("branch", branchId), ("batch", Unique("batch"))));

        await InsertBatchAsync(connection, transaction, branchId, productId, Unique("batch"), batchId);
        await AssertDatabaseErrorAsync(connection, transaction, "negative_inventory", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "Inventory" ("Id","BranchId","ProductId","ProductBatchId","QuantityInStock","ReorderLevel","LastCountedAt","CreatedAt","UpdatedAt")
                VALUES (@id,@branch,@product,@batch,-1,0,now(),now(),now());
                """, ("id", Guid.NewGuid()), ("branch", branchId), ("product", productId), ("batch", batchId)));

        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase4_opening_stock_rows_keep_ledger_and_projections_consistent()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var categoryId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        await InsertCategoryAsync(connection, transaction, categoryId);
        await InsertBranchAsync(connection, transaction, branchId);
        await InsertProductAsync(connection, transaction, categoryId, Unique("sku"), null, productId);
        await InsertBatchAsync(connection, transaction, branchId, productId, Unique("batch"), batchId);
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "Inventory" ("Id","BranchId","ProductId","ProductBatchId","QuantityInStock","ReorderLevel","LastCountedAt","CreatedAt","UpdatedAt")
            VALUES (@id,@branch,@product,@batch,100,10,now(),now(),now());
            """, ("id", Guid.NewGuid()), ("branch", branchId), ("product", productId), ("batch", batchId));
        await InsertMovementAsync(connection, transaction, branchId, productId, batchId, 1, 100);

        Assert.Equal(
            100,
            await ScalarAsync<int>(connection, transaction, """
                SELECT batch."QuantityAvailable"
                FROM "ProductBatches" batch
                JOIN "Inventory" inventory ON inventory."ProductBatchId" = batch."Id"
                JOIN (
                    SELECT "ProductBatchId", sum("Quantity") AS quantity
                    FROM "StockMovements"
                    GROUP BY "ProductBatchId"
                ) movement ON movement."ProductBatchId" = batch."Id"
                WHERE batch."Id" = @batch
                  AND batch."QuantityAvailable" = inventory."QuantityInStock"
                  AND batch."QuantityAvailable" = movement.quantity;
                """, ("batch", batchId)));

        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase5_supplier_constraints_permissions_and_ledger_are_enforced()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var branchId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        await InsertBranchAsync(connection, transaction, branchId);
        await InsertSupplierAsync(connection, transaction, supplierId, "ABC Pharma", "ABC PHARMA");

        Assert.Equal(
            8L,
            await ScalarAsync<long>(connection, transaction, """
                SELECT count(*) FROM "Permissions"
                WHERE "Code" IN ('suppliers.view','suppliers.create','suppliers.update','suppliers.activate',
                    'suppliers.deactivate','suppliers.ledger.view','suppliers.payment.create','suppliers.adjust_balance');
                """));

        await AssertDatabaseErrorAsync(connection, transaction, "duplicate_supplier_name", PostgresErrorCodes.UniqueViolation,
            () => InsertSupplierAsync(connection, transaction, Guid.NewGuid(), "abc pharma", "ABC PHARMA"));
        await AssertDatabaseErrorAsync(connection, transaction, "negative_credit_limit", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "Suppliers" ("Id","Name","NormalizedName","CreditLimit","IsActive","OpeningBalance","CreatedAt","UpdatedAt")
                VALUES (@id,'Bad','BAD',-1,true,0,now(),now());
                """, ("id", Guid.NewGuid())));

        await InsertSupplierLedgerAsync(connection, transaction, supplierId, branchId, 1, 10000);
        await InsertSupplierLedgerAsync(connection, transaction, supplierId, branchId, 1, -5000);
        await InsertSupplierLedgerAsync(connection, transaction, supplierId, branchId, 2, -4000);
        await InsertSupplierLedgerAsync(connection, transaction, supplierId, branchId, 3, 2000);
        await InsertSupplierLedgerAsync(connection, transaction, supplierId, branchId, 4, -1500);
        await AssertDatabaseErrorAsync(connection, transaction, "payment_positive", PostgresErrorCodes.CheckViolation,
            () => InsertSupplierLedgerAsync(connection, transaction, supplierId, branchId, 2, 1));
        await AssertDatabaseErrorAsync(connection, transaction, "zero_ledger", PostgresErrorCodes.CheckViolation,
            () => InsertSupplierLedgerAsync(connection, transaction, supplierId, branchId, 3, 0));

        Assert.Equal(1500, await ScalarAsync<decimal>(connection, transaction, """
            SELECT sum("Amount") FROM "SupplierLedgerEntries" WHERE "SupplierId" = @supplier;
            """, ("supplier", supplierId)));
        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase6_purchase_constraints_permissions_and_invoice_uniqueness_are_enforced()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var branchId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();

        await InsertBranchAsync(connection, transaction, branchId);
        await InsertSupplierAsync(connection, transaction, supplierId, Unique("supplier"), Unique("supplier").ToUpperInvariant());
        await InsertCategoryAsync(connection, transaction, categoryId);
        await InsertProductAsync(connection, transaction, categoryId, Unique("sku"), null, productId);

        Assert.Equal(9L, await ScalarAsync<long>(connection, transaction, """
            SELECT count(*) FROM "Permissions" WHERE "Code" LIKE 'purchases.%' OR "Code" LIKE 'purchase_orders.%';
            """));

        await InsertPurchaseOrderAsync(connection, transaction, orderId, branchId, supplierId, "PO-TEST-001");
        await AssertDatabaseErrorAsync(connection, transaction, "duplicate_po_number", PostgresErrorCodes.UniqueViolation,
            () => InsertPurchaseOrderAsync(connection, transaction, Guid.NewGuid(), branchId, supplierId, "PO-TEST-001"));
        await InsertPurchaseOrderItemAsync(connection, transaction, orderItemId, orderId, productId, 100, 60);
        await AssertDatabaseErrorAsync(connection, transaction, "po_over_received", PostgresErrorCodes.CheckViolation,
            () => InsertPurchaseOrderItemAsync(connection, transaction, Guid.NewGuid(), orderId, productId, 100, 101));

        await InsertGoodsReceiptAsync(connection, transaction, receiptId, branchId, supplierId, orderId, "GRN-TEST-001", "INV-001", 5000);
        await AssertDatabaseErrorAsync(connection, transaction, "duplicate_grn", PostgresErrorCodes.UniqueViolation,
            () => InsertGoodsReceiptAsync(connection, transaction, Guid.NewGuid(), branchId, supplierId, null, "GRN-TEST-001", null, 100));
        await AssertDatabaseErrorAsync(connection, transaction, "duplicate_supplier_invoice", PostgresErrorCodes.UniqueViolation,
            () => InsertGoodsReceiptAsync(connection, transaction, Guid.NewGuid(), branchId, supplierId, null, "GRN-TEST-002", " inv-001 ", 100));
        await InsertGoodsReceiptAsync(connection, transaction, Guid.NewGuid(), branchId, supplierId, null, "GRN-TEST-003", null, 100);
        await InsertGoodsReceiptAsync(connection, transaction, Guid.NewGuid(), branchId, supplierId, null, "GRN-TEST-004", null, 100);

        await InsertGoodsReceiptItemAsync(connection, transaction, receiptId, productId, orderItemId, "B-PUR-1", 100, 10, 50, 5, 2, 4845);
        await AssertDatabaseErrorAsync(connection, transaction, "negative_bonus", PostgresErrorCodes.CheckViolation,
            () => InsertGoodsReceiptItemAsync(connection, transaction, receiptId, productId, null, "B-PUR-2", 1, -1, 50, 0, 0, 50));
        await AssertDatabaseErrorAsync(connection, transaction, "bad_discount", PostgresErrorCodes.CheckViolation,
            () => InsertGoodsReceiptItemAsync(connection, transaction, receiptId, productId, null, "B-PUR-3", 1, 0, 50, 101, 0, 50));
        await AssertDatabaseErrorAsync(connection, transaction, "manufacturing_after_expiry", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "GoodsReceiptItems"
                    ("Id","GoodsReceiptId","ProductId","BatchNumber","ManufacturingDate","ExpiryDate","PurchasedQuantity","BonusQuantity","PurchasePrice","RetailPrice","DiscountPercent","DiscountAmount","TaxPercent","TaxAmount","NetLineAmount","CreatedAt","UpdatedAt")
                VALUES (@id,@receipt,@product,'B-PUR-4',current_date + 20,current_date + 10,1,0,50,60,0,0,0,0,50,now(),now());
                """, ("id", Guid.NewGuid()), ("receipt", receiptId), ("product", productId)));

        await InsertSupplierLedgerAsync(connection, transaction, supplierId, branchId, 5, 5000);
        await AssertDatabaseErrorAsync(connection, transaction, "purchase_negative", PostgresErrorCodes.CheckViolation,
            () => InsertSupplierLedgerAsync(connection, transaction, supplierId, branchId, 5, -1));

        await transaction.RollbackAsync();
    }


    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase7_sales_constraints_indexes_permissions_and_allocations_are_enforced()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var branchId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var roleId = await ScalarAsync<Guid>(connection, transaction, """
            SELECT "Id" FROM "Roles" WHERE "Name" = 'Cashier';
            """);
        var userId = Guid.NewGuid();

        await InsertBranchAsync(connection, transaction, branchId);
        await InsertCategoryAsync(connection, transaction, categoryId);
        await InsertProductAsync(connection, transaction, categoryId, Unique("sku"), Unique("barcode"), productId);
        await InsertUserAsync(connection, transaction, branchId, roleId, Unique("cashier"), Unique("cashier").ToUpperInvariant(), null, null, userId);
        await InsertBatchAsync(connection, transaction, branchId, productId, "SALE-BATCH", batchId);
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "Inventory" ("Id","BranchId","ProductId","ProductBatchId","QuantityInStock","ReorderLevel","LastCountedAt","CreatedAt","UpdatedAt")
            VALUES (@id,@branch,@product,@batch,100,0,now(),now(),now());
            """, ("id", inventoryId), ("branch", branchId), ("product", productId), ("batch", batchId));

        Assert.Equal(5L, await ScalarAsync<long>(connection, transaction, """
            SELECT count(*) FROM "Permissions"
            WHERE "Code" IN ('sales.view','sales.create','sales.hold','sales.discount','sales.reprint');
            """));

        Assert.Equal(15L, await ScalarAsync<long>(connection, transaction, """
            SELECT count(*) FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname IN (
                'IX_Sales_InvoiceNumber',
                'IX_Sales_HoldNumber',
                'IX_Sales_BranchId_PostedAtUtc',
                'IX_Sales_CashierUserId_PostedAtUtc',
                'IX_Sales_Status_CreatedAt',
                'IX_Sales_CustomerPhone',
                'IX_SaleItems_SaleId',
                'IX_SaleItems_ProductId',
                'IX_SaleItemBatchAllocations_SaleItemId',
                'IX_SaleItemBatchAllocations_ProductBatchId',
                'IX_SalePayments_SaleId',
                'IX_SalePayments_Method_CreatedAt',
                'IX_ProductBatches_ExpiryDate',
                'IX_ProductBatches_BranchId_ProductId',
                'IX_StockMovements_ReferenceType_ReferenceId');
            """));

        await AssertDatabaseErrorAsync(connection, transaction, "posted_without_invoice", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "Sales" ("Id","BranchId","Status","PostedAtUtc","CashierUserId","Subtotal","DiscountTotal","TaxTotal","NetTotal","AmountPaid","ChangeGiven","CreatedAt","UpdatedAt")
                VALUES (@id,@branch,2,now(),@cashier,12,0,0,12,12,0,now(),now());
                """, ("id", Guid.NewGuid()), ("branch", branchId), ("cashier", userId)));

        await InsertPostedSaleAsync(connection, transaction, saleId, branchId, userId, "INV-PG-SALE-1", 12);
        await AssertDatabaseErrorAsync(connection, transaction, "duplicate_invoice", PostgresErrorCodes.UniqueViolation,
            () => InsertPostedSaleAsync(connection, transaction, Guid.NewGuid(), branchId, userId, "INV-PG-SALE-1", 12));
        await InsertHeldSaleAsync(connection, transaction, Guid.NewGuid(), branchId, userId, "HOLD-PG-1");
        await AssertDatabaseErrorAsync(connection, transaction, "duplicate_hold", PostgresErrorCodes.UniqueViolation,
            () => InsertHeldSaleAsync(connection, transaction, Guid.NewGuid(), branchId, userId, "HOLD-PG-1"));

        await ExecuteAsync(connection, transaction, """
            INSERT INTO "SaleItems" ("Id","SaleId","ProductId","RequestedQuantity","DiscountPercent","GrossAmount","DiscountAmount","TaxAmount","NetAmount","CreatedAt","UpdatedAt")
            VALUES (@id,@sale,@product,1,0,12,0,0,12,now(),now());
            """, ("id", itemId), ("sale", saleId), ("product", productId));
        await AssertDatabaseErrorAsync(connection, transaction, "zero_sale_item_quantity", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "SaleItems" ("Id","SaleId","ProductId","RequestedQuantity","DiscountPercent","GrossAmount","DiscountAmount","TaxAmount","NetAmount","CreatedAt","UpdatedAt")
                VALUES (@id,@sale,@product,0,0,12,0,0,12,now(),now());
                """, ("id", Guid.NewGuid()), ("sale", saleId), ("product", productId)));

        await ExecuteAsync(connection, transaction, """
            INSERT INTO "SaleItemBatchAllocations" ("Id","SaleItemId","ProductBatchId","Quantity","UnitRetailPriceSnapshot","UnitSalePriceSnapshot","UnitCostPriceSnapshot","ExpiryDateSnapshot","GrossAmount","DiscountAmount","TaxAmount","NetAmount","CreatedAt","UpdatedAt")
            VALUES (@id,@item,@batch,1,12,12,10,current_date + 365,12,0,0,12,now(),now());
            """, ("id", Guid.NewGuid()), ("item", itemId), ("batch", batchId));
        await AssertDatabaseErrorAsync(connection, transaction, "zero_allocation_quantity", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "SaleItemBatchAllocations" ("Id","SaleItemId","ProductBatchId","Quantity","UnitRetailPriceSnapshot","UnitSalePriceSnapshot","UnitCostPriceSnapshot","ExpiryDateSnapshot","GrossAmount","DiscountAmount","TaxAmount","NetAmount","CreatedAt","UpdatedAt")
                VALUES (@id,@item,@batch,0,12,12,10,current_date + 365,12,0,0,12,now(),now());
                """, ("id", Guid.NewGuid()), ("item", itemId), ("batch", batchId)));

        await ExecuteAsync(connection, transaction, """
            INSERT INTO "SalePayments" ("Id","SaleId","Method","AmountApplied","TenderedAmount","CreatedAt","UpdatedAt")
            VALUES (@id,@sale,1,12,20,now(),now());
            """, ("id", Guid.NewGuid()), ("sale", saleId));
        await AssertDatabaseErrorAsync(connection, transaction, "card_with_tender", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "SalePayments" ("Id","SaleId","Method","AmountApplied","TenderedAmount","CreatedAt","UpdatedAt")
                VALUES (@id,@sale,2,12,12,now(),now());
                """, ("id", Guid.NewGuid()), ("sale", saleId)));
        await AssertDatabaseErrorAsync(connection, transaction, "cash_under_tender", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "SalePayments" ("Id","SaleId","Method","AmountApplied","TenderedAmount","CreatedAt","UpdatedAt")
                VALUES (@id,@sale,1,12,11,now(),now());
                """, ("id", Guid.NewGuid()), ("sale", saleId)));

        await InsertMovementAsync(connection, transaction, branchId, productId, batchId, 3, -1);
        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase8_sales_return_constraints_indexes_and_permissions_exist()
    {
        await using var connection = await OpenConnectionAsync();

        Assert.Equal(4L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM "Permissions"
            WHERE "Code" IN ('sales.returns.view','sales.returns.create','sales.returns.refund','sales.returns.reprint');
            """));

        Assert.Equal(10L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname IN (
                'IX_SalesReturns_ReturnNumber',
                'IX_SalesReturns_OriginalSaleId',
                'IX_SalesReturns_BranchId_PostedAtUtc',
                'IX_SalesReturns_ProcessedByUserId_PostedAtUtc',
                'IX_SalesReturns_Status_ReturnDateUtc',
                'IX_SalesReturnItems_SalesReturnId',
                'IX_SalesReturnItems_OriginalSaleItemId',
                'IX_SalesReturnAllocations_OriginalSaleItemBatchAllocationId',
                'IX_SalesRefundPayments_SalesReturnId',
                'IX_SalesRefundPayments_Method_CreatedAt');
            """));

        Assert.Equal(11L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM information_schema.table_constraints
            WHERE table_schema = 'public'
              AND constraint_name IN (
                'CK_SalesReturns_Status',
                'CK_SalesReturns_Reason',
                'CK_SalesReturns_Posted',
                'CK_SalesReturns_Money_NonNegative',
                'CK_SalesReturns_Settlement',
                'CK_SalesReturnItems_Quantity_Positive',
                'CK_SalesReturnItems_Money_NonNegative',
                'CK_SalesReturnAllocations_Quantity_Positive',
                'CK_SalesReturnAllocations_Disposition',
                'CK_SalesRefundPayments_Method',
                'CK_SalesRefundPayments_Amount_Positive');
            """));
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase8_sales_return_database_constraints_and_fks_are_enforced()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var categoryId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var saleItemId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var returnId = Guid.NewGuid();
        var returnItemId = Guid.NewGuid();
        var roleId = await ScalarAsync<Guid>(connection, transaction, "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\" = 'Manager';");
        var userId = Guid.NewGuid();

        await InsertCategoryAsync(connection, transaction, categoryId);
        await InsertBranchAsync(connection, transaction, branchId);
        await InsertProductAsync(connection, transaction, categoryId, Unique("sku"), null, productId);
        await InsertBatchAsync(connection, transaction, branchId, productId, Unique("batch"), batchId);
        await InsertUserAsync(connection, transaction, branchId, roleId, Unique("manager"), Unique("MANAGER"), null, null, userId);
        await InsertPostedSaleAsync(connection, transaction, saleId, branchId, userId, Unique("INV-PG-RET"), 120);
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "SaleItems" ("Id","SaleId","ProductId","RequestedQuantity","DiscountPercent","GrossAmount","DiscountAmount","TaxAmount","NetAmount","CreatedAt","UpdatedAt")
            VALUES (@id,@sale,@product,2,0,120,0,0,120,now(),now());
            """, ("id", saleItemId), ("sale", saleId), ("product", productId));
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "SaleItemBatchAllocations" ("Id","SaleItemId","ProductBatchId","Quantity","UnitRetailPriceSnapshot","UnitSalePriceSnapshot","UnitCostPriceSnapshot","ExpiryDateSnapshot","GrossAmount","DiscountAmount","TaxAmount","NetAmount","CreatedAt","UpdatedAt")
            VALUES (@id,@item,@batch,2,60,60,40,current_date + 365,120,0,0,120,now(),now());
            """, ("id", allocationId), ("item", saleItemId), ("batch", batchId));
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "SalesReturns" ("Id","ReturnNumber","OriginalSaleId","BranchId","ProcessedByUserId","ReturnDateUtc","Reason","GrossReturnAmount","DiscountReturnAmount","TaxReturnAmount","RefundAmount","CustomerCreditReductionAmount","CashRefundAmount","Status","PostedAtUtc","CreatedAt","UpdatedAt")
            VALUES (@id,'RET-PG-1',@sale,@branch,@user,now(),1,60,0,0,60,0,60,1,now(),now(),now());
            """, ("id", returnId), ("sale", saleId), ("branch", branchId), ("user", userId));
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "SalesReturnItems" ("Id","SalesReturnId","OriginalSaleItemId","ProductId","Quantity","GrossReturnAmount","DiscountReturnAmount","TaxReturnAmount","RefundAmount","CreatedAt","UpdatedAt")
            VALUES (@id,@return,@item,@product,1,60,0,0,60,now(),now());
            """, ("id", returnItemId), ("return", returnId), ("item", saleItemId), ("product", productId));
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "SalesReturnAllocations" ("Id","SalesReturnItemId","OriginalSaleItemBatchAllocationId","ProductBatchId","Quantity","Disposition","UnitRetailPriceSnapshot","UnitSalePriceSnapshot","UnitCostPriceSnapshot","ExpiryDateSnapshot","GrossReturnAmount","DiscountReturnAmount","TaxReturnAmount","RefundAmount","CreatedAt","UpdatedAt")
            VALUES (@id,@item,@allocation,@batch,1,1,60,60,40,current_date + 365,60,0,0,60,now(),now());
            """, ("id", Guid.NewGuid()), ("item", returnItemId), ("allocation", allocationId), ("batch", batchId));
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "SalesRefundPayments" ("Id","SalesReturnId","Method","Amount","CreatedAt","UpdatedAt")
            VALUES (@id,@return,1,60,now(),now());
            """, ("id", Guid.NewGuid()), ("return", returnId));

        await AssertDatabaseErrorAsync(connection, transaction, "duplicate_return_number", PostgresErrorCodes.UniqueViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "SalesReturns" ("Id","ReturnNumber","OriginalSaleId","BranchId","ProcessedByUserId","ReturnDateUtc","Reason","GrossReturnAmount","DiscountReturnAmount","TaxReturnAmount","RefundAmount","CustomerCreditReductionAmount","CashRefundAmount","Status","PostedAtUtc","CreatedAt","UpdatedAt")
                VALUES (@id,'RET-PG-1',@sale,@branch,@user,now(),1,1,0,0,1,0,1,1,now(),now(),now());
                """, ("id", Guid.NewGuid()), ("sale", saleId), ("branch", branchId), ("user", userId)));
        await AssertDatabaseErrorAsync(connection, transaction, "zero_return_quantity", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "SalesReturnAllocations" ("Id","SalesReturnItemId","OriginalSaleItemBatchAllocationId","ProductBatchId","Quantity","Disposition","UnitRetailPriceSnapshot","UnitSalePriceSnapshot","UnitCostPriceSnapshot","ExpiryDateSnapshot","GrossReturnAmount","DiscountReturnAmount","TaxReturnAmount","RefundAmount","CreatedAt","UpdatedAt")
                VALUES (@id,@item,@allocation,@batch,0,1,60,60,40,current_date + 365,0,0,0,0,now(),now());
                """, ("id", Guid.NewGuid()), ("item", returnItemId), ("allocation", allocationId), ("batch", batchId)));
        await AssertDatabaseErrorAsync(connection, transaction, "invalid_disposition", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "SalesReturnAllocations" ("Id","SalesReturnItemId","OriginalSaleItemBatchAllocationId","ProductBatchId","Quantity","Disposition","UnitRetailPriceSnapshot","UnitSalePriceSnapshot","UnitCostPriceSnapshot","ExpiryDateSnapshot","GrossReturnAmount","DiscountReturnAmount","TaxReturnAmount","RefundAmount","CreatedAt","UpdatedAt")
                VALUES (@id,@item,@allocation,@batch,1,9,60,60,40,current_date + 365,60,0,0,60,now(),now());
                """, ("id", Guid.NewGuid()), ("item", returnItemId), ("allocation", allocationId), ("batch", batchId)));
        await AssertDatabaseErrorAsync(connection, transaction, "negative_refund_payment", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "SalesRefundPayments" ("Id","SalesReturnId","Method","Amount","CreatedAt","UpdatedAt")
                VALUES (@id,@return,1,-1,now(),now());
                """, ("id", Guid.NewGuid()), ("return", returnId)));
        await AssertDatabaseErrorAsync(connection, transaction, "invalid_original_allocation_fk", PostgresErrorCodes.ForeignKeyViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "SalesReturnAllocations" ("Id","SalesReturnItemId","OriginalSaleItemBatchAllocationId","ProductBatchId","Quantity","Disposition","UnitRetailPriceSnapshot","UnitSalePriceSnapshot","UnitCostPriceSnapshot","ExpiryDateSnapshot","GrossReturnAmount","DiscountReturnAmount","TaxReturnAmount","RefundAmount","CreatedAt","UpdatedAt")
                VALUES (@id,@item,@allocation,@batch,1,1,60,60,40,current_date + 365,60,0,0,60,now(),now());
                """, ("id", Guid.NewGuid()), ("item", returnItemId), ("allocation", Guid.NewGuid()), ("batch", batchId)));

        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase9_purchase_return_constraints_indexes_permissions_and_sequence_exist()
    {
        await using var connection = await OpenConnectionAsync();

        Assert.Equal(3L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM "Permissions"
            WHERE "Code" IN ('purchase_returns.view','purchase_returns.create','purchase_returns.reprint');
            """));
        Assert.Equal(2L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name IN ('PurchaseReturns','PurchaseReturnItems');
            """));
        Assert.Equal(1L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relkind = 'S' AND c.relname = 'PurchaseReturnNumberSequence';
            """));
        Assert.Equal(6L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM information_schema.table_constraints
            WHERE table_schema = 'public'
              AND constraint_name IN (
                'CK_PurchaseReturns_Status',
                'CK_PurchaseReturns_Reason',
                'CK_PurchaseReturns_Posted',
                'CK_PurchaseReturns_Money_NonNegative',
                'CK_PurchaseReturnItems_Quantities',
                'CK_PurchaseReturnItems_Money_NonNegative');
            """));
        Assert.Equal(10L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname IN (
                'IX_PurchaseReturns_ReturnNumber',
                'IX_PurchaseReturns_OriginalGoodsReceiptId',
                'IX_PurchaseReturns_SupplierId_PostedAtUtc',
                'IX_PurchaseReturns_BranchId_PostedAtUtc',
                'IX_PurchaseReturns_ProcessedByUserId_PostedAtUtc',
                'IX_PurchaseReturns_Reason',
                'IX_PurchaseReturnItems_PurchaseReturnId',
                'IX_PurchaseReturnItems_OriginalGoodsReceiptItemId',
                'IX_PurchaseReturnItems_ProductBatchId',
                'IX_PurchaseReturnItems_OriginalGoodsReceiptItemId_PurchaseRetu~');
            """));
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase9_purchase_return_database_constraints_fks_and_signs_are_enforced()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var categoryId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var receiptItemId = Guid.NewGuid();
        var returnId = Guid.NewGuid();

        await InsertCategoryAsync(connection, transaction, categoryId);
        await InsertBranchAsync(connection, transaction, branchId);
        await InsertSupplierAsync(connection, transaction, supplierId, Unique("supplier"), Unique("supplier").ToUpperInvariant());
        await InsertProductAsync(connection, transaction, categoryId, Unique("sku"), null, productId);
        await InsertBatchAsync(connection, transaction, branchId, productId, Unique("batch"), batchId);
        var roleId = await ScalarAsync<Guid>(connection, transaction, """SELECT "Id" FROM "Roles" WHERE "Name" = 'Owner';""");
        await InsertUserAsync(connection, transaction, branchId, roleId, Unique("user"), Unique("user").ToUpperInvariant(), null, null, userId);
        await InsertGoodsReceiptAsync(connection, transaction, receiptId, branchId, supplierId, null, Unique("GRN-PR"), Unique("INV-PR"), 5000);
        await InsertGoodsReceiptItemAsync(connection, transaction, receiptId, productId, null, "B-PR", 100, 10, 50, 0, 0, 5000, receiptItemId, batchId);

        await ExecuteAsync(connection, transaction, """
            INSERT INTO "PurchaseReturns" ("Id","ReturnNumber","OriginalGoodsReceiptId","SupplierId","BranchId","ProcessedByUserId","ReturnDateUtc","Reason","GrossReturnAmount","DiscountAdjustment","TaxAdjustment","NetSupplierCredit","Status","PostedAtUtc","CreatedAt","UpdatedAt")
            VALUES (@id,'PR-PG-1',@receipt,@supplier,@branch,@user,now(),1,1000,0,0,1000,1,now(),now(),now());
            """, ("id", returnId), ("receipt", receiptId), ("supplier", supplierId), ("branch", branchId), ("user", userId));
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "PurchaseReturnItems" ("Id","PurchaseReturnId","OriginalGoodsReceiptItemId","ProductId","ProductBatchId","BatchNumber","ExpiryDate","PaidReturnQuantity","BonusReturnQuantity","PurchasePriceSnapshot","GrossReturnAmount","DiscountAdjustment","TaxAdjustment","NetSupplierCredit","CreatedAt","UpdatedAt")
            VALUES (@id,@return,@receiptItem,@product,@batch,'B-PR',current_date + 365,20,0,50,1000,0,0,1000,now(),now());
            """, ("id", Guid.NewGuid()), ("return", returnId), ("receiptItem", receiptItemId), ("product", productId), ("batch", batchId));
        await InsertMovementAsync(connection, transaction, branchId, productId, batchId, 5, -20);
        await InsertSupplierLedgerAsync(connection, transaction, supplierId, branchId, 6, -1000);

        await AssertDatabaseErrorAsync(connection, transaction, "duplicate_purchase_return_number", PostgresErrorCodes.UniqueViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "PurchaseReturns" ("Id","ReturnNumber","OriginalGoodsReceiptId","SupplierId","BranchId","ProcessedByUserId","ReturnDateUtc","Reason","GrossReturnAmount","DiscountAdjustment","TaxAdjustment","NetSupplierCredit","Status","PostedAtUtc","CreatedAt","UpdatedAt")
                VALUES (@id,'PR-PG-1',@receipt,@supplier,@branch,@user,now(),1,1,0,0,1,1,now(),now(),now());
                """, ("id", Guid.NewGuid()), ("receipt", receiptId), ("supplier", supplierId), ("branch", branchId), ("user", userId)));
        await AssertDatabaseErrorAsync(connection, transaction, "zero_purchase_return_qty", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "PurchaseReturnItems" ("Id","PurchaseReturnId","OriginalGoodsReceiptItemId","ProductId","ProductBatchId","BatchNumber","ExpiryDate","PaidReturnQuantity","BonusReturnQuantity","PurchasePriceSnapshot","GrossReturnAmount","DiscountAdjustment","TaxAdjustment","NetSupplierCredit","CreatedAt","UpdatedAt")
                VALUES (@id,@return,@receiptItem,@product,@batch,'B-PR',current_date + 365,0,0,50,0,0,0,0,now(),now());
                """, ("id", Guid.NewGuid()), ("return", returnId), ("receiptItem", receiptItemId), ("product", productId), ("batch", batchId)));
        await AssertDatabaseErrorAsync(connection, transaction, "invalid_purchase_return_fk", PostgresErrorCodes.ForeignKeyViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "PurchaseReturnItems" ("Id","PurchaseReturnId","OriginalGoodsReceiptItemId","ProductId","ProductBatchId","BatchNumber","ExpiryDate","PaidReturnQuantity","BonusReturnQuantity","PurchasePriceSnapshot","GrossReturnAmount","DiscountAdjustment","TaxAdjustment","NetSupplierCredit","CreatedAt","UpdatedAt")
                VALUES (@id,@return,@receiptItem,@product,@batch,'B-PR',current_date + 365,1,0,50,50,0,0,50,now(),now());
                """, ("id", Guid.NewGuid()), ("return", returnId), ("receiptItem", Guid.NewGuid()), ("product", productId), ("batch", batchId)));
        await AssertDatabaseErrorAsync(connection, transaction, "purchase_return_stock_positive", PostgresErrorCodes.CheckViolation,
            () => InsertMovementAsync(connection, transaction, branchId, productId, batchId, 5, 1));
        await AssertDatabaseErrorAsync(connection, transaction, "purchase_return_ledger_positive", PostgresErrorCodes.CheckViolation,
            () => InsertSupplierLedgerAsync(connection, transaction, supplierId, branchId, 6, 1));

        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase10_customer_credit_constraints_indexes_permissions_and_sequences_exist()
    {
        await using var connection = await OpenConnectionAsync();

        Assert.Equal(9L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM "Permissions"
            WHERE "Code" IN (
                'customers.view','customers.create','customers.update','customers.activate',
                'customers.deactivate','customers.ledger.view','customers.payment.create',
                'customers.adjust_balance','sales.credit');
            """));
        Assert.Equal(2L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relkind = 'S'
              AND c.relname IN ('CustomerCodeSequence','CustomerPaymentReceiptSequence');
            """));
        Assert.Equal(16L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname IN (
                'IX_Customers_CustomerCode',
                'IX_Customers_Name',
                'IX_Customers_NormalizedName',
                'IX_Customers_PhoneNumber',
                'IX_Customers_Email',
                'IX_Customers_City',
                'IX_Customers_IsActive',
                'IX_CustomerLedgerEntries_CustomerId_CreatedAt',
                'IX_CustomerLedgerEntries_CustomerId_BranchId_CreatedAt',
                'IX_CustomerLedgerEntries_BranchId_CreatedAt',
                'IX_CustomerLedgerEntries_EntryType_CreatedAt',
                'IX_CustomerLedgerEntries_ReferenceType_ReferenceId',
                'IX_CustomerPayments_ReceiptNumber',
                'IX_CustomerPayments_CustomerId_PaymentDateUtc',
                'IX_CustomerPayments_BranchId_PaymentDateUtc',
                'IX_Sales_CustomerId_PostedAtUtc');
            """));
        Assert.Equal(8L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM information_schema.table_constraints
            WHERE table_schema = 'public'
              AND constraint_name IN (
                'CK_Customers_CreditLimit_NonNegative',
                'CK_CustomerLedgerEntries_AmountSign',
                'CK_CustomerPayments_Method',
                'CK_CustomerPayments_Amount_Positive',
                'CK_Sales_CreditRequiresCustomer',
                'CK_Sales_Posted_Settled',
                'CK_SalesReturns_Settlement',
                'FK_Sales_Customers_CustomerId');
            """));
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase10_customer_credit_database_constraints_and_fks_are_enforced()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var categoryId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = await ScalarAsync<Guid>(connection, transaction, "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\" = 'Owner';");

        await InsertCategoryAsync(connection, transaction, categoryId);
        await InsertBranchAsync(connection, transaction, branchId);
        await InsertProductAsync(connection, transaction, categoryId, Unique("sku"), null, productId);
        await InsertUserAsync(connection, transaction, branchId, roleId, Unique("owner"), Unique("owner").ToUpperInvariant(), null, null, userId);
        await InsertCustomerAsync(connection, transaction, customerId, "CUS-PG-1", "Customer One", "CUSTOMER ONE", 1000);

        await AssertDatabaseErrorAsync(connection, transaction, "duplicate_customer_code", PostgresErrorCodes.UniqueViolation,
            () => InsertCustomerAsync(connection, transaction, Guid.NewGuid(), "CUS-PG-1", "Customer Two", "CUSTOMER TWO", 1000));
        await AssertDatabaseErrorAsync(connection, transaction, "negative_credit_limit", PostgresErrorCodes.CheckViolation,
            () => InsertCustomerAsync(connection, transaction, Guid.NewGuid(), Unique("CUS"), "Customer Three", "CUSTOMER THREE", -1));

        await InsertCustomerLedgerAsync(connection, transaction, customerId, branchId, 1, 50);
        await InsertCustomerLedgerAsync(connection, transaction, customerId, branchId, 2, 100);
        await InsertCustomerLedgerAsync(connection, transaction, customerId, branchId, 3, -40);
        await AssertDatabaseErrorAsync(connection, transaction, "credit_sale_negative", PostgresErrorCodes.CheckViolation,
            () => InsertCustomerLedgerAsync(connection, transaction, customerId, branchId, 2, -1));
        await AssertDatabaseErrorAsync(connection, transaction, "payment_positive", PostgresErrorCodes.CheckViolation,
            () => InsertCustomerLedgerAsync(connection, transaction, customerId, branchId, 3, 1));
        await AssertDatabaseErrorAsync(connection, transaction, "ledger_unknown_customer", PostgresErrorCodes.ForeignKeyViolation,
            () => InsertCustomerLedgerAsync(connection, transaction, Guid.NewGuid(), branchId, 2, 1));

        await ExecuteAsync(connection, transaction, """
            INSERT INTO "CustomerPayments" ("Id","ReceiptNumber","CustomerId","BranchId","Amount","PaymentMethod","PaymentDateUtc","ReceivedByUserId","CreatedAt","UpdatedAt")
            VALUES (@id,'CR-PG-1',@customer,@branch,40,1,now(),@user,now(),now());
            """, ("id", Guid.NewGuid()), ("customer", customerId), ("branch", branchId), ("user", userId));
        await AssertDatabaseErrorAsync(connection, transaction, "duplicate_receipt", PostgresErrorCodes.UniqueViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "CustomerPayments" ("Id","ReceiptNumber","CustomerId","BranchId","Amount","PaymentMethod","PaymentDateUtc","ReceivedByUserId","CreatedAt","UpdatedAt")
                VALUES (@id,'CR-PG-1',@customer,@branch,40,1,now(),@user,now(),now());
                """, ("id", Guid.NewGuid()), ("customer", customerId), ("branch", branchId), ("user", userId)));
        await AssertDatabaseErrorAsync(connection, transaction, "negative_customer_payment", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "CustomerPayments" ("Id","ReceiptNumber","CustomerId","BranchId","Amount","PaymentMethod","PaymentDateUtc","ReceivedByUserId","CreatedAt","UpdatedAt")
                VALUES (@id,'CR-PG-2',@customer,@branch,-1,1,now(),@user,now(),now());
                """, ("id", Guid.NewGuid()), ("customer", customerId), ("branch", branchId), ("user", userId)));

        await ExecuteAsync(connection, transaction, """
            INSERT INTO "Sales" ("Id","BranchId","InvoiceNumber","Status","PostedAtUtc","CashierUserId","CustomerId","Subtotal","DiscountTotal","TaxTotal","NetTotal","AmountPaid","CreditAmount","ChangeGiven","CreatedAt","UpdatedAt")
            VALUES (@id,@branch,'INV-CREDIT-PG-1',2,now(),@cashier,@customer,100,0,0,100,25,75,0,now(),now());
            """, ("id", Guid.NewGuid()), ("branch", branchId), ("cashier", userId), ("customer", customerId));
        await AssertDatabaseErrorAsync(connection, transaction, "credit_without_customer", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "Sales" ("Id","BranchId","InvoiceNumber","Status","PostedAtUtc","CashierUserId","Subtotal","DiscountTotal","TaxTotal","NetTotal","AmountPaid","CreditAmount","ChangeGiven","CreatedAt","UpdatedAt")
                VALUES (@id,@branch,'INV-CREDIT-PG-2',2,now(),@cashier,100,0,0,100,25,75,0,now(),now());
                """, ("id", Guid.NewGuid()), ("branch", branchId), ("cashier", userId)));
        await AssertDatabaseErrorAsync(connection, transaction, "unsettled_posted_sale", PostgresErrorCodes.CheckViolation,
            () => ExecuteAsync(connection, transaction, """
                INSERT INTO "Sales" ("Id","BranchId","InvoiceNumber","Status","PostedAtUtc","CashierUserId","CustomerId","Subtotal","DiscountTotal","TaxTotal","NetTotal","AmountPaid","CreditAmount","ChangeGiven","CreatedAt","UpdatedAt")
                VALUES (@id,@branch,'INV-CREDIT-PG-3',2,now(),@cashier,@customer,100,0,0,100,25,70,0,now(),now());
                """, ("id", Guid.NewGuid()), ("branch", branchId), ("cashier", userId), ("customer", customerId)));

        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Phase11_financial_ledger_constraints_indexes_and_immutability_are_real()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = await ScalarAsync<Guid>(connection, transaction, "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\"='Owner';");
        await InsertBranchAsync(connection, transaction, branchId);
        var username = Unique("finance-user");
        await InsertUserAsync(connection, transaction, branchId, roleId, username, username.ToUpperInvariant(), null, null, userId);
        var categoryId = await ScalarAsync<Guid>(connection, transaction, "SELECT \"Id\" FROM \"ExpenseCategories\" ORDER BY \"Id\" LIMIT 1;");
        var accountId = Guid.NewGuid();
        var openingId = Guid.NewGuid();
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "FinancialAccounts" ("Id","BranchId","Name","NormalizedName","AccountType","OpeningBalance","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id,@branch,@name,@normalized,1,10000,true,now(),now());
            """, ("id", accountId), ("branch", branchId), ("name", Unique("Finance")), ("normalized", Unique("FINANCE")));
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "FinancialLedgerEntries" ("Id","FinancialAccountId","BranchId","EntryType","Amount","ReferenceType","ReferenceId","Description","CreatedByUserId","OccurredAtUtc","CreatedAt","UpdatedAt")
            VALUES (@id,@account,@branch,1,10000,'FinancialAccount',@account,'Opening balance',@user,now(),now(),now());
            """, ("id", openingId), ("account", accountId), ("branch", branchId), ("user", userId));

        var expenseId = Guid.NewGuid();
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "Expenses" ("Id","ExpenseNumber","BranchId","ExpenseCategoryId","FinancialAccountId","ExpenseDateUtc","Amount","Description","CreatedByUserId","PostedAtUtc","CreatedAt","UpdatedAt")
            VALUES (@id,@number,@branch,@category,@account,now(),3000,'Integration expense',@user,now(),now(),now());
            """, ("id", expenseId), ("number", Unique("EXP")), ("branch", branchId), ("category", categoryId), ("account", accountId), ("user", userId));
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "FinancialLedgerEntries" ("Id","FinancialAccountId","BranchId","EntryType","Amount","ReferenceType","ReferenceId","Description","CreatedByUserId","OccurredAtUtc","CreatedAt","UpdatedAt")
            VALUES (@id,@account,@branch,6,-3000,'Expense',@expense,'Integration expense',@user,now(),now(),now());
            """, ("id", Guid.NewGuid()), ("account", accountId), ("branch", branchId), ("expense", expenseId), ("user", userId));
        Assert.Equal(7000m, await ScalarAsync<decimal>(connection, transaction, "SELECT SUM(\"Amount\") FROM \"FinancialLedgerEntries\" WHERE \"FinancialAccountId\"=@account;", ("account", accountId)));

        var incomeId = Guid.NewGuid();
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "OtherIncomes" ("Id","IncomeNumber","BranchId","FinancialAccountId","Amount","Description","OccurredAtUtc","CreatedByUserId","CreatedAt","UpdatedAt")
            VALUES (@id,@number,@branch,@account,2000,'Other income',now(),@user,now(),now());
            """, ("id", incomeId), ("number", Unique("INC")), ("branch", branchId), ("account", accountId), ("user", userId));
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "FinancialLedgerEntries" ("Id","FinancialAccountId","BranchId","EntryType","Amount","ReferenceType","ReferenceId","Description","CreatedByUserId","OccurredAtUtc","CreatedAt","UpdatedAt")
            VALUES (@id,@account,@branch,7,2000,'OtherIncome',@income,'Other income',@user,now(),now(),now());
            """, ("id", Guid.NewGuid()), ("account", accountId), ("branch", branchId), ("income", incomeId), ("user", userId));

        var destinationId = Guid.NewGuid();
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "FinancialAccounts" ("Id","BranchId","Name","NormalizedName","AccountType","OpeningBalance","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id,@branch,@name,@normalized,2,0,true,now(),now());
            """, ("id", destinationId), ("branch", branchId), ("name", Unique("Bank")), ("normalized", Unique("BANK")));
        var transferId = Guid.NewGuid();
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "FinancialTransfers" ("Id","TransferNumber","BranchId","SourceAccountId","DestinationAccountId","Amount","OccurredAtUtc","CreatedByUserId","CreatedAt","UpdatedAt")
            VALUES (@id,@number,@branch,@source,@destination,3000,now(),@user,now(),now());
            """, ("id", transferId), ("number", Unique("TRF")), ("branch", branchId), ("source", accountId), ("destination", destinationId), ("user", userId));
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "FinancialLedgerEntries" ("Id","FinancialAccountId","BranchId","EntryType","Amount","ReferenceType","ReferenceId","Description","CreatedByUserId","OccurredAtUtc","CreatedAt","UpdatedAt") VALUES
            (@outId,@source,@branch,8,-3000,'FinancialTransfer',@transfer,'Transfer out',@user,now(),now(),now()),
            (@inId,@destination,@branch,9,3000,'FinancialTransfer',@transfer,'Transfer in',@user,now(),now(),now());
            """, ("outId", Guid.NewGuid()), ("inId", Guid.NewGuid()), ("source", accountId), ("destination", destinationId), ("branch", branchId), ("transfer", transferId), ("user", userId));
        Assert.Equal(6000m, await ScalarAsync<decimal>(connection, transaction, "SELECT SUM(\"Amount\") FROM \"FinancialLedgerEntries\" WHERE \"FinancialAccountId\"=@account;", ("account", accountId)));
        Assert.Equal(3000m, await ScalarAsync<decimal>(connection, transaction, "SELECT SUM(\"Amount\") FROM \"FinancialLedgerEntries\" WHERE \"FinancialAccountId\"=@account;", ("account", destinationId)));
        Assert.Equal(2L, await ScalarAsync<long>(connection, transaction, "SELECT count(*) FROM \"FinancialLedgerEntries\" WHERE \"ReferenceType\"='FinancialTransfer' AND \"ReferenceId\"=@transfer;", ("transfer", transferId)));

        await AssertDatabaseErrorAsync(connection, transaction, "bad_sign", "23514", () => ExecuteAsync(connection, transaction, """
            INSERT INTO "FinancialLedgerEntries" ("Id","FinancialAccountId","BranchId","EntryType","Amount","ReferenceType","ReferenceId","Description","CreatedByUserId","OccurredAtUtc","CreatedAt","UpdatedAt")
            VALUES (@id,@account,@branch,6,1,'Expense',@reference,'Bad sign',@user,now(),now(),now());
            """, ("id", Guid.NewGuid()), ("account", accountId), ("branch", branchId), ("reference", Guid.NewGuid()), ("user", userId)));
        await AssertDatabaseErrorAsync(connection, transaction, "insufficient", "23514", () => ExecuteAsync(connection, transaction, """
            INSERT INTO "FinancialLedgerEntries" ("Id","FinancialAccountId","BranchId","EntryType","Amount","ReferenceType","ReferenceId","Description","CreatedByUserId","OccurredAtUtc","CreatedAt","UpdatedAt")
            VALUES (@id,@account,@branch,6,-8000,'Expense',@reference,'Too large',@user,now(),now(),now());
            """, ("id", Guid.NewGuid()), ("account", accountId), ("branch", branchId), ("reference", Guid.NewGuid()), ("user", userId)));
        await AssertDatabaseErrorAsync(connection, transaction, "immutable", "23514", () => ExecuteAsync(connection, transaction,
            "UPDATE \"FinancialLedgerEntries\" SET \"Description\"='Changed' WHERE \"Id\"=@id;", ("id", openingId)));

        Assert.Equal(9L, await ScalarAsync<long>(connection, transaction, "SELECT count(*) FROM \"Permissions\" WHERE \"Category\"='finance';"));
        Assert.Equal(5L, await ScalarAsync<long>(connection, transaction, "SELECT count(*) FROM pg_trigger WHERE tgname IN ('TR_FinancialLedgerEntries_Guard','TR_FinancialLedgerEntries_Immutable','TR_Expenses_Immutable','TR_OtherIncomes_Immutable','TR_FinancialTransfers_Immutable') AND NOT tgisinternal;"));
        Assert.True(await ScalarAsync<long>(connection, transaction, "SELECT count(*) FROM pg_indexes WHERE schemaname='public' AND indexname IN ('IX_FinancialLedgerEntries_FinancialAccountId_OccurredAtUtc','IX_FinancialLedgerEntries_BranchId_OccurredAtUtc','IX_FinancialLedgerEntries_EntryType_OccurredAtUtc','IX_FinancialLedgerEntries_ReferenceType_ReferenceId','IX_Expenses_ExpenseNumber');") >= 5);
        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Finance_sequence_backed_postings_generate_numbers_and_correct_economics_against_postgresql()
    {
        await using var seedConnection = await OpenConnectionAsync();
        await using var seedTransaction = await seedConnection.BeginTransactionAsync();
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable)!;
        var branchId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var roleId = await ScalarAsync<Guid>(seedConnection, seedTransaction, "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\"='Owner';");
        await InsertBranchAsync(seedConnection, seedTransaction, branchId);
        var username = Unique("finance-seq-user");
        await InsertUserAsync(seedConnection, seedTransaction, branchId, roleId, username, username.ToUpperInvariant(), null, null, actorId);
        var categoryId = await ScalarAsync<Guid>(seedConnection, seedTransaction, "SELECT \"Id\" FROM \"ExpenseCategories\" ORDER BY \"Id\" LIMIT 1;");
        await seedTransaction.CommitAsync();

        static DbContextOptions<PharmacyDbContext> Options(string value) =>
            new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql(value).Options;

        FinancialAccountDto source;
        FinancialAccountDto destination;
        await using (var setupContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new FinanceService(new FinanceRepository(setupContext), TimeProvider.System);
            source = await service.CreateAccountAsync(actorId,
                new FinancialAccountRequest(branchId, Unique("Finance Seq Cash"), FinancialAccountType.Cash, 10000m, null));
            destination = await service.CreateAccountAsync(actorId,
                new FinancialAccountRequest(branchId, Unique("Finance Seq Bank"), FinancialAccountType.Bank, 0m, null));
        }

        // Two expenses prove the sequence advances and produces distinct numbers on repeated calls.
        ExpenseDto expenseOne;
        await using (var expenseContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new FinanceService(new FinanceRepository(expenseContext), TimeProvider.System);
            expenseOne = await service.PostExpenseAsync(actorId,
                new PostExpenseRequest(branchId, categoryId, source.Id, DateTime.UtcNow, 1200m, "Integration expense one", null, null, null));
        }
        ExpenseDto expenseTwo;
        await using (var expenseContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new FinanceService(new FinanceRepository(expenseContext), TimeProvider.System);
            expenseTwo = await service.PostExpenseAsync(actorId,
                new PostExpenseRequest(branchId, categoryId, source.Id, DateTime.UtcNow, 300m, "Integration expense two", null, null, null));
        }

        OtherIncomeDto income;
        await using (var incomeContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new FinanceService(new FinanceRepository(incomeContext), TimeProvider.System);
            income = await service.PostOtherIncomeAsync(actorId,
                new PostOtherIncomeRequest(branchId, source.Id, DateTime.UtcNow, 800m, "Integration other income", null, null));
        }

        FinancialTransferDto transfer;
        await using (var transferContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new FinanceService(new FinanceRepository(transferContext), TimeProvider.System);
            transfer = await service.PostTransferAsync(actorId,
                new PostTransferRequest(branchId, source.Id, destination.Id, DateTime.UtcNow, 3000m, null, null));
        }

        var currentYear = DateTime.UtcNow.Year;
        Assert.False(string.IsNullOrWhiteSpace(expenseOne.ExpenseNumber));
        Assert.StartsWith($"EXP-{currentYear}-", expenseOne.ExpenseNumber);
        Assert.NotEqual(expenseOne.ExpenseNumber, expenseTwo.ExpenseNumber);
        Assert.False(string.IsNullOrWhiteSpace(income.IncomeNumber));
        Assert.StartsWith($"INC-{currentYear}-", income.IncomeNumber);
        Assert.False(string.IsNullOrWhiteSpace(transfer.TransferNumber));
        Assert.StartsWith($"TRF-{currentYear}-", transfer.TransferNumber);

        await using var verifyContext = new PharmacyDbContext(Options(connectionString));

        Assert.Equal(1, await verifyContext.Expenses.CountAsync(x => x.Id == expenseOne.Id));
        Assert.Equal(1, await verifyContext.Expenses.CountAsync(x => x.Id == expenseTwo.Id));
        Assert.Equal(1, await verifyContext.FinancialLedgerEntries.CountAsync(x => x.ReferenceType == "Expense" && x.ReferenceId == expenseOne.Id));
        Assert.Equal(-1200m, await verifyContext.FinancialLedgerEntries.Where(x => x.ReferenceId == expenseOne.Id).SumAsync(x => x.Amount));
        Assert.Equal(-300m, await verifyContext.FinancialLedgerEntries.Where(x => x.ReferenceId == expenseTwo.Id).SumAsync(x => x.Amount));

        Assert.Equal(1, await verifyContext.OtherIncomes.CountAsync(x => x.Id == income.Id));
        Assert.Equal(1, await verifyContext.FinancialLedgerEntries.CountAsync(x => x.ReferenceType == "OtherIncome" && x.ReferenceId == income.Id));
        Assert.Equal(800m, await verifyContext.FinancialLedgerEntries.Where(x => x.ReferenceId == income.Id).SumAsync(x => x.Amount));

        Assert.Equal(1, await verifyContext.FinancialTransfers.CountAsync(x => x.Id == transfer.Id));
        Assert.Equal(2, await verifyContext.FinancialLedgerEntries.CountAsync(x => x.ReferenceType == "FinancialTransfer" && x.ReferenceId == transfer.Id));
        var sourceTransferEntry = await verifyContext.FinancialLedgerEntries.SingleAsync(x => x.ReferenceId == transfer.Id && x.FinancialAccountId == source.Id);
        var destinationTransferEntry = await verifyContext.FinancialLedgerEntries.SingleAsync(x => x.ReferenceId == transfer.Id && x.FinancialAccountId == destination.Id);
        Assert.Equal(FinancialLedgerEntryType.TransferOut, sourceTransferEntry.EntryType);
        Assert.Equal(-3000m, sourceTransferEntry.Amount);
        Assert.Equal(FinancialLedgerEntryType.TransferIn, destinationTransferEntry.EntryType);
        Assert.Equal(3000m, destinationTransferEntry.Amount);
        Assert.Equal(0m, await verifyContext.FinancialLedgerEntries.Where(x => x.ReferenceId == transfer.Id).SumAsync(x => x.Amount));

        // Opening (10000) - 1200 - 300 + 800 - 3000 = 6300; destination opening (0) + 3000 = 3000.
        Assert.Equal(6300m, await verifyContext.FinancialLedgerEntries.Where(x => x.FinancialAccountId == source.Id).SumAsync(x => x.Amount));
        Assert.Equal(3000m, await verifyContext.FinancialLedgerEntries.Where(x => x.FinancialAccountId == destination.Id).SumAsync(x => x.Amount));

        Assert.Equal(1, await verifyContext.AuditLogs.CountAsync(x => x.EntityType == "Expense" && x.EntityId == expenseOne.Id && x.Action == "ExpensePosted"));
        Assert.Equal(1, await verifyContext.AuditLogs.CountAsync(x => x.EntityType == "OtherIncome" && x.EntityId == income.Id && x.Action == "OtherIncomePosted"));
        Assert.Equal(1, await verifyContext.AuditLogs.CountAsync(x => x.EntityType == "FinancialTransfer" && x.EntityId == transfer.Id && x.Action == "AccountTransferPosted"));
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Finance_concurrent_expense_postings_against_the_same_account_resolve_safely_without_unhandled_500()
    {
        await using var seedConnection = await OpenConnectionAsync();
        await using var seedTransaction = await seedConnection.BeginTransactionAsync();
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable)!;
        var branchId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var roleId = await ScalarAsync<Guid>(seedConnection, seedTransaction, "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\"='Owner';");
        await InsertBranchAsync(seedConnection, seedTransaction, branchId);
        var username = Unique("finance-exp-conc-user");
        await InsertUserAsync(seedConnection, seedTransaction, branchId, roleId, username, username.ToUpperInvariant(), null, null, actorId);
        var categoryId = await ScalarAsync<Guid>(seedConnection, seedTransaction, "SELECT \"Id\" FROM \"ExpenseCategories\" ORDER BY \"Id\" LIMIT 1;");
        await seedTransaction.CommitAsync();

        static DbContextOptions<PharmacyDbContext> Options(string value) =>
            new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql(value).Options;

        FinancialAccountDto account;
        await using (var setupContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new FinanceService(new FinanceRepository(setupContext), TimeProvider.System);
            account = await service.CreateAccountAsync(actorId,
                new FinancialAccountRequest(branchId, Unique("Finance Exp Conc"), FinancialAccountType.Cash, 1000m, null));
        }

        var occurredAt = DateTime.UtcNow;

        // Each attempt gets its own DbContext/connection, mirroring two independent HTTP
        // requests, and both are launched without awaiting between them so their
        // Serializable transactions can genuinely overlap against the same account row.
        async Task<(ExpenseDto? Result, Exception? Error)> AttemptAsync(string label)
        {
            await using var raceContext = new PharmacyDbContext(Options(connectionString));
            var service = new FinanceService(new FinanceRepository(raceContext), TimeProvider.System);
            try
            {
                return (await service.PostExpenseAsync(actorId,
                    new PostExpenseRequest(branchId, categoryId, account.Id, occurredAt, 700m, $"Concurrent expense {label}", null, null, null)), null);
            }
            catch (Exception ex) { return (null, ex); }
        }

        var outcomes = await Task.WhenAll(Task.Run(() => AttemptAsync("A")), Task.Run(() => AttemptAsync("B")));

        var winners = outcomes.Where(x => x.Result is not null).ToList();
        var losers = outcomes.Where(x => x.Error is not null).ToList();
        Assert.Single(winners);
        Assert.Single(losers);

        // Whether the loser lost a genuine Postgres 40001 race (now mapped by
        // ExecuteInTransactionAsync) or simply observed the depleted balance after the
        // winner committed, it must always surface as the application's own safe
        // conflict - never a raw/unhandled Postgres or EF exception (HTTP 500).
        var loserError = Assert.IsType<ResourceConflictException>(losers[0].Error);
        Assert.True(
            loserError.Message.Contains("insufficient funds", StringComparison.OrdinalIgnoreCase) ||
            loserError.Message.Contains("changed by another operation", StringComparison.OrdinalIgnoreCase),
            $"Unexpected conflict message: {loserError.Message}");

        var winner = winners[0].Result!;

        await using var verifyContext = new PharmacyDbContext(Options(connectionString));
        var finalBalance = await verifyContext.FinancialLedgerEntries.Where(x => x.FinancialAccountId == account.Id).SumAsync(x => x.Amount);
        Assert.Equal(300m, finalBalance);
        Assert.True(finalBalance >= 0);
        Assert.Equal(1, await verifyContext.Expenses.CountAsync(x => x.FinancialAccountId == account.Id));
        Assert.Equal(1, await verifyContext.FinancialLedgerEntries.CountAsync(x => x.FinancialAccountId == account.Id && x.EntryType == FinancialLedgerEntryType.Expense));
        Assert.Equal(1, await verifyContext.AuditLogs.CountAsync(x => x.EntityType == "Expense" && x.EntityId == winner.Id && x.Action == "ExpensePosted"));
    }

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task Finance_concurrent_transfers_from_the_same_source_account_resolve_safely_without_unhandled_500()
    {
        await using var seedConnection = await OpenConnectionAsync();
        await using var seedTransaction = await seedConnection.BeginTransactionAsync();
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable)!;
        var branchId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var roleId = await ScalarAsync<Guid>(seedConnection, seedTransaction, "SELECT \"Id\" FROM \"Roles\" WHERE \"Name\"='Owner';");
        await InsertBranchAsync(seedConnection, seedTransaction, branchId);
        var username = Unique("finance-trf-conc-user");
        await InsertUserAsync(seedConnection, seedTransaction, branchId, roleId, username, username.ToUpperInvariant(), null, null, actorId);
        await seedTransaction.CommitAsync();

        static DbContextOptions<PharmacyDbContext> Options(string value) =>
            new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql(value).Options;

        FinancialAccountDto source, destinationA, destinationB;
        await using (var setupContext = new PharmacyDbContext(Options(connectionString)))
        {
            var service = new FinanceService(new FinanceRepository(setupContext), TimeProvider.System);
            source = await service.CreateAccountAsync(actorId,
                new FinancialAccountRequest(branchId, Unique("Finance Trf Conc Source"), FinancialAccountType.Cash, 1000m, null));
            destinationA = await service.CreateAccountAsync(actorId,
                new FinancialAccountRequest(branchId, Unique("Finance Trf Conc Dest A"), FinancialAccountType.Cash, 0m, null));
            destinationB = await service.CreateAccountAsync(actorId,
                new FinancialAccountRequest(branchId, Unique("Finance Trf Conc Dest B"), FinancialAccountType.Cash, 0m, null));
        }

        var occurredAt = DateTime.UtcNow;

        async Task<(FinancialTransferDto? Result, Exception? Error)> AttemptAsync(Guid destinationId, string label)
        {
            await using var raceContext = new PharmacyDbContext(Options(connectionString));
            var service = new FinanceService(new FinanceRepository(raceContext), TimeProvider.System);
            try
            {
                return (await service.PostTransferAsync(actorId,
                    new PostTransferRequest(branchId, source.Id, destinationId, occurredAt, 700m, $"R4-TRF-CONC-{label}", null)), null);
            }
            catch (Exception ex) { return (null, ex); }
        }

        var outcomes = await Task.WhenAll(
            Task.Run(() => AttemptAsync(destinationA.Id, "A")),
            Task.Run(() => AttemptAsync(destinationB.Id, "B")));

        var winners = outcomes.Where(x => x.Result is not null).ToList();
        var losers = outcomes.Where(x => x.Error is not null).ToList();
        Assert.Single(winners);
        Assert.Single(losers);

        // As with the expense race, a genuine 40001 (now mapped) and a plain
        // insufficient-funds recheck after the winner commits are both acceptable safe
        // outcomes - only a raw/unhandled exception (HTTP 500) would be a defect.
        var loserError = Assert.IsType<ResourceConflictException>(losers[0].Error);
        Assert.True(
            loserError.Message.Contains("insufficient funds", StringComparison.OrdinalIgnoreCase) ||
            loserError.Message.Contains("changed by another operation", StringComparison.OrdinalIgnoreCase),
            $"Unexpected conflict message: {loserError.Message}");

        var winner = winners[0].Result!;
        var winningDestinationId = winner.DestinationAccountId;
        var losingDestinationId = winningDestinationId == destinationA.Id ? destinationB.Id : destinationA.Id;

        await using var verifyContext = new PharmacyDbContext(Options(connectionString));
        var sourceBalance = await verifyContext.FinancialLedgerEntries.Where(x => x.FinancialAccountId == source.Id).SumAsync(x => x.Amount);
        var winningDestinationBalance = await verifyContext.FinancialLedgerEntries.Where(x => x.FinancialAccountId == winningDestinationId).SumAsync(x => x.Amount);
        var losingDestinationBalance = await verifyContext.FinancialLedgerEntries.Where(x => x.FinancialAccountId == losingDestinationId).SumAsync(x => x.Amount);
        Assert.Equal(300m, sourceBalance);
        Assert.True(sourceBalance >= 0);
        Assert.Equal(700m, winningDestinationBalance);
        Assert.Equal(0m, losingDestinationBalance);
        Assert.Equal(1, await verifyContext.FinancialTransfers.CountAsync(x => x.SourceAccountId == source.Id));
        Assert.Equal(1, await verifyContext.FinancialLedgerEntries.CountAsync(x => x.FinancialAccountId == source.Id && x.EntryType == FinancialLedgerEntryType.TransferOut));
        Assert.Equal(1, await verifyContext.AuditLogs.CountAsync(x => x.EntityType == "FinancialTransfer" && x.EntityId == winner.Id && x.Action == "AccountTransferPosted"));
    }

    private static async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable)
            ?? throw new InvalidOperationException($"{ConnectionVariable} is not configured.");
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = settings.Database ?? string.Empty;
        if (!databaseName.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
            databaseName.Contains("prod", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{ConnectionVariable} must target a dedicated database whose name ends with '_test'.");
        }
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }


    private static async Task InsertPostedSaleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        Guid branchId,
        Guid cashierUserId,
        string invoiceNumber,
        decimal amount) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "Sales" ("Id","BranchId","InvoiceNumber","Status","PostedAtUtc","CashierUserId","Subtotal","DiscountTotal","TaxTotal","NetTotal","AmountPaid","ChangeGiven","CreatedAt","UpdatedAt")
            VALUES (@id,@branch,@invoice,2,now(),@cashier,@amount,0,0,@amount,@amount,0,now(),now());
            """, ("id", id), ("branch", branchId), ("invoice", invoiceNumber), ("cashier", cashierUserId), ("amount", amount));

    private static async Task InsertHeldSaleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        Guid branchId,
        Guid cashierUserId,
        string holdNumber) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "Sales" ("Id","BranchId","HoldNumber","Status","CashierUserId","Subtotal","DiscountTotal","TaxTotal","NetTotal","AmountPaid","ChangeGiven","CreatedAt","UpdatedAt")
            VALUES (@id,@branch,@hold,1,@cashier,0,0,0,0,0,0,now(),now());
            """, ("id", id), ("branch", branchId), ("hold", holdNumber), ("cashier", cashierUserId));
    private static async Task InsertCategoryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string? name = null,
        string? normalized = null) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "ProductCategories"
                ("Id", "Name", "NormalizedName", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES (@id, @name, @normalized, true, now(), now());
            """, ("id", id), ("name", name ?? Unique("category")), ("normalized", normalized ?? Unique("category").ToUpperInvariant()));

    private static async Task InsertBranchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "Branches"
                ("Id", "Code", "NormalizedCode", "Name", "IsHeadOffice", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES (@id, @code, upper(@code), @name, false, true, now(), now());
            """, ("id", id), ("code", Unique("branch")), ("name", Unique("branch")));

    private static async Task InsertProductAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid categoryId,
        string sku,
        string? barcode,
        Guid? id = null,
        string? normalizedSku = null,
        Guid? manufacturerId = null) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "Products"
                ("Id", "SKU", "NormalizedSku", "Barcode", "NormalizedBarcode", "Name", "CategoryId", "ManufacturerId", "Unit", "PackSize",
                 "PurchasePrice", "RetailPrice", "ReorderLevel", "MaximumDiscountPercent",
                 "IsActive", "CreatedAt", "UpdatedAt")
            VALUES
                (@id, @sku, @normalizedSku, @barcode, @normalizedBarcode, @name, @categoryId, @manufacturerId, 'piece', 1,
                 10.00, 12.00, 0, 0.00, true, now(), now());
            """,
            ("id", id ?? Guid.NewGuid()),
            ("sku", sku),
            ("normalizedSku", normalizedSku ?? sku.Trim().ToUpperInvariant()),
            ("barcode", barcode is null ? DBNull.Value : barcode),
            ("normalizedBarcode", barcode is null ? DBNull.Value : barcode.Trim().ToUpperInvariant()),
            ("name", Unique("product")),
            ("categoryId", categoryId),
            ("manufacturerId", manufacturerId.HasValue ? manufacturerId.Value : DBNull.Value));

    private static async Task InsertBatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid branchId,
        Guid productId,
        string batchNumber,
        Guid? id = null) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "ProductBatches"
                ("Id", "ProductId", "BranchId", "BatchNumber", "ExpiryDate",
                 "PurchasePrice", "RetailPrice", "QuantityReceived", "QuantityAvailable",
                 "IsDisposed", "CreatedAt", "UpdatedAt")
            VALUES
                (@id, @productId, @branchId, @batchNumber, current_date + 365,
                 10.00, 12.00, 100, 100, false, now(), now());
            """,
            ("id", id ?? Guid.NewGuid()),
            ("productId", productId),
            ("branchId", branchId),
            ("batchNumber", batchNumber));

    private static async Task InsertSupplierAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string name,
        string normalizedName) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "Suppliers" ("Id","Name","NormalizedName","IsActive","OpeningBalance","CreatedAt","UpdatedAt")
            VALUES (@id,@name,@normalized,true,0,now(),now());
            """, ("id", id), ("name", name), ("normalized", normalizedName));

    private static async Task InsertSupplierLedgerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid supplierId,
        Guid branchId,
        int entryType,
        decimal amount) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "SupplierLedgerEntries" ("Id","SupplierId","BranchId","EntryType","Amount","EntryDate","CreatedAt","UpdatedAt")
            VALUES (@id,@supplier,@branch,@type,@amount,current_date,now(),now());
            """, ("id", Guid.NewGuid()), ("supplier", supplierId), ("branch", branchId), ("type", entryType), ("amount", amount));

    private static async Task InsertCustomerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        string customerCode,
        string name,
        string normalizedName,
        decimal creditLimit) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "Customers" ("Id","CustomerCode","Name","NormalizedName","CreditLimit","OpeningBalance","IsActive","CreatedAt","UpdatedAt")
            VALUES (@id,@code,@name,@normalized,@creditLimit,0,true,now(),now());
            """, ("id", id), ("code", customerCode), ("name", name), ("normalized", normalizedName), ("creditLimit", creditLimit));

    private static async Task InsertCustomerLedgerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid customerId,
        Guid branchId,
        int entryType,
        decimal amount) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "CustomerLedgerEntries" ("Id","CustomerId","BranchId","EntryType","Amount","EntryDate","CreatedAt","UpdatedAt")
            VALUES (@id,@customer,@branch,@type,@amount,current_date,now(),now());
            """, ("id", Guid.NewGuid()), ("customer", customerId), ("branch", branchId), ("type", entryType), ("amount", amount));

    private static async Task InsertPurchaseOrderAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        Guid branchId,
        Guid supplierId,
        string orderNumber) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "PurchaseOrders" ("Id","BranchId","SupplierId","OrderNumber","OrderDate","Status","CreatedAt","UpdatedAt")
            VALUES (@id,@branch,@supplier,@number,current_date,2,now(),now());
            """, ("id", id), ("branch", branchId), ("supplier", supplierId), ("number", orderNumber));

    private static async Task InsertPurchaseOrderItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        Guid orderId,
        Guid productId,
        int ordered,
        int received) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "PurchaseOrderItems" ("Id","PurchaseOrderId","ProductId","OrderedQuantity","ReceivedQuantity","CreatedAt","UpdatedAt")
            VALUES (@id,@order,@product,@ordered,@received,now(),now());
            """, ("id", id), ("order", orderId), ("product", productId), ("ordered", ordered), ("received", received));

    private static async Task InsertGoodsReceiptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        Guid branchId,
        Guid supplierId,
        Guid? orderId,
        string grnNumber,
        string? invoiceNumber,
        decimal netTotal) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "GoodsReceipts"
                ("Id","BranchId","SupplierId","PurchaseOrderId","GrnNumber","SupplierInvoiceNumber","NormalizedSupplierInvoiceNumber","ReceiptDate","Status","Subtotal","DiscountTotal","TaxTotal","NetTotal","CreatedAt","UpdatedAt")
            VALUES (@id,@branch,@supplier,@order,@grn,@invoice,@normalizedInvoice,current_date,2,@net,0,0,@net,now(),now());
            """, ("id", id), ("branch", branchId), ("supplier", supplierId), ("order", orderId.HasValue ? orderId.Value : DBNull.Value),
            ("grn", grnNumber), ("invoice", invoiceNumber is null ? DBNull.Value : invoiceNumber),
            ("normalizedInvoice", invoiceNumber is null ? DBNull.Value : invoiceNumber.Trim().ToUpperInvariant()), ("net", netTotal));

    private static async Task InsertGoodsReceiptItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid receiptId,
        Guid productId,
        Guid? orderItemId,
        string batchNumber,
        int purchasedQuantity,
        int bonusQuantity,
        decimal purchasePrice,
        decimal discountPercent,
        decimal taxPercent,
        decimal netLineAmount,
        Guid? id = null,
        Guid? productBatchId = null) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "GoodsReceiptItems"
                ("Id","GoodsReceiptId","ProductId","PurchaseOrderItemId","ProductBatchId","BatchNumber","ExpiryDate","PurchasedQuantity","BonusQuantity","PurchasePrice","RetailPrice","DiscountPercent","DiscountAmount","TaxPercent","TaxAmount","NetLineAmount","CreatedAt","UpdatedAt")
            VALUES (@id,@receipt,@product,@orderItem,@productBatch,@batch,current_date + 365,@purchased,@bonus,@purchasePrice,@retailPrice,@discountPercent,0,@taxPercent,0,@net,now(),now());
            """, ("id", id ?? Guid.NewGuid()), ("receipt", receiptId), ("product", productId), ("orderItem", orderItemId.HasValue ? orderItemId.Value : DBNull.Value),
            ("productBatch", productBatchId.HasValue ? productBatchId.Value : DBNull.Value), ("batch", batchNumber), ("purchased", purchasedQuantity), ("bonus", bonusQuantity), ("purchasePrice", purchasePrice),
            ("retailPrice", purchasePrice + 10), ("discountPercent", discountPercent), ("taxPercent", taxPercent), ("net", netLineAmount));

    private static async Task InsertMovementAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid branchId,
        Guid productId,
        Guid batchId,
        int movementType,
        int quantity) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "StockMovements"
                ("Id", "MovementType", "BranchId", "ProductId", "ProductBatchId",
                 "Quantity", "CreatedAt", "UpdatedAt")
            VALUES
                (@id, @movementType, @branchId, @productId, @batchId,
                 @quantity, now(), now());
            """,
            ("id", Guid.NewGuid()),
            ("movementType", movementType),
            ("branchId", branchId),
            ("productId", productId),
            ("batchId", batchId),
            ("quantity", quantity));

    [PostgreSqlFact]
    [Trait("Category", "PostgreSQL")]
    public async Task System_administration_schema_and_permissions_are_real()
    {
        await using var connection = await OpenConnectionAsync();
        Assert.Equal(1L, await ScalarAsync<long>(connection, null, "SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260907220809_CompleteSystemAdministration';"));
        Assert.Equal(2L, await ScalarAsync<long>(connection, null, "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('SystemSettings','BackupRecords');"));
        Assert.Equal(8L, await ScalarAsync<long>(connection, null, "SELECT count(*) FROM \"Permissions\" WHERE \"Code\" IN ('system.view','system.settings.manage','system.backup','branches.view','branches.manage','recycle_bin.view','recycle_bin.restore','audit.export');"));
        Assert.Equal(1L, await ScalarAsync<long>(connection, null, "SELECT count(*) FROM pg_indexes WHERE schemaname='public' AND tablename='Branches' AND indexname='IX_Branches_NormalizedCode' AND indexdef ILIKE '%UNIQUE%';"));
        Assert.Equal(9L, await ScalarAsync<long>(connection, null, "SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND column_name IN ('IsDeleted','DeletedAtUtc','DeletedByUserId') AND table_name IN ('ProductCategories','Manufacturers','ExpenseCategories');"));
        Assert.Equal(1L, await ScalarAsync<long>(connection, null, "SELECT count(*) FROM pg_trigger WHERE tgname='TR_AuditLogs_AppendOnly' AND NOT tgisinternal;"));
    }

    private static async Task InsertUserAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid branchId,
        Guid roleId,
        string username,
        string normalizedUsername,
        string? email,
        string? normalizedEmail,
        Guid? id = null) =>
        await ExecuteAsync(connection, transaction, """
            INSERT INTO "Users"
                ("Id", "Username", "NormalizedUsername", "Email", "NormalizedEmail",
                 "FullName", "PasswordHash", "BranchId", "RoleId", "IsActive",
                 "MustChangePassword", "FailedLoginAttempts", "TokenVersion", "CreatedAt", "UpdatedAt")
            VALUES
                (@id, @username, @normalizedUsername, @email, @normalizedEmail,
                 'Integration User', 'not-a-real-password-hash', @branchId, @roleId, true,
                 true, 0, 0, now(), now());
            """,
            ("id", id ?? Guid.NewGuid()),
            ("username", username),
            ("normalizedUsername", normalizedUsername),
            ("email", email is null ? DBNull.Value : email),
            ("normalizedEmail", normalizedEmail is null ? DBNull.Value : normalizedEmail),
            ("branchId", branchId),
            ("roleId", roleId));

    private static async Task AssertDatabaseErrorAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string savepoint,
        string sqlState,
        Func<Task> action)
    {
        await transaction.SaveAsync(savepoint);
        var exception = await Assert.ThrowsAsync<PostgresException>(action);
        Assert.Equal(sqlState, exception.SqlState);
        await transaction.RollbackAsync(savepoint);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static string Unique(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}";
}

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PHARMACY_TEST_CONNECTION_STRING")))
        {
            Skip = "PHARMACY_TEST_CONNECTION_STRING is not configured.";
        }
    }
}
