using Npgsql;

namespace Pharmacy.Tests;

public sealed class PostgreSqlIntegrationTests
{
    private const string ConnectionVariable = "PHARMACY_TEST_CONNECTION_STRING";

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
            9L,
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
                    '20260903231207_CompletePurchaseReturns');
                """));

        Assert.Equal(
            28L,
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
                  ('PurchaseReturnItems', 'NetSupplierCredit'));
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

        Assert.Equal(10L, await ScalarAsync<long>(connection, null, """
            SELECT count(*) FROM information_schema.table_constraints
            WHERE table_schema = 'public'
              AND constraint_name IN (
                'CK_SalesReturns_Status',
                'CK_SalesReturns_Reason',
                'CK_SalesReturns_Posted',
                'CK_SalesReturns_Money_NonNegative',
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
            INSERT INTO "SalesReturns" ("Id","ReturnNumber","OriginalSaleId","BranchId","ProcessedByUserId","ReturnDateUtc","Reason","GrossReturnAmount","DiscountReturnAmount","TaxReturnAmount","RefundAmount","Status","PostedAtUtc","CreatedAt","UpdatedAt")
            VALUES (@id,'RET-PG-1',@sale,@branch,@user,now(),1,60,0,0,60,1,now(),now(),now());
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
                INSERT INTO "SalesReturns" ("Id","ReturnNumber","OriginalSaleId","BranchId","ProcessedByUserId","ReturnDateUtc","Reason","GrossReturnAmount","DiscountReturnAmount","TaxReturnAmount","RefundAmount","Status","PostedAtUtc","CreatedAt","UpdatedAt")
                VALUES (@id,'RET-PG-1',@sale,@branch,@user,now(),1,1,0,0,1,1,now(),now(),now());
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
    private static async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable)
            ?? throw new InvalidOperationException($"{ConnectionVariable} is not configured.");
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
                ("Id", "Code", "Name", "IsHeadOffice", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES (@id, @code, @name, false, true, now(), now());
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
