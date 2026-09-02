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
            4L,
            await ScalarAsync<long>(connection, null, """
                SELECT count(*)
                FROM "__EFMigrationsHistory"
                WHERE "MigrationId" IN (
                    '20260829211152_InitialCreate',
                    '20260829223012_AddUserSecurityAndManagement',
                    '20260901194508_CompleteProductMaster',
                    '20260901215409_CompleteBatchAndInventoryManagement');
                """));

        Assert.Equal(
            13L,
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
                  ('StockMovements', 'CreatedAt'));
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
        Assert.Equal("timestamp with time zone", types["StockMovements.CreatedAt"]);
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

    private static async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable)
            ?? throw new InvalidOperationException($"{ConnectionVariable} is not configured.");
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

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
        string? normalizedEmail) =>
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
            ("id", Guid.NewGuid()),
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
