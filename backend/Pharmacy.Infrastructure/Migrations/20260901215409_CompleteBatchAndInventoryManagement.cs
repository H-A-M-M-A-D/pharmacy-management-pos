using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteBatchAndInventoryManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ProductBatchId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_ProductId",
                table: "ProductBatches");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_BranchId_ProductId_CreatedAt",
                table: "StockMovements",
                columns: new[] { "BranchId", "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_MovementType_CreatedAt",
                table: "StockMovements",
                columns: new[] { "MovementType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductBatchId_CreatedAt",
                table: "StockMovements",
                columns: new[] { "ProductBatchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_BranchId_ExpiryDate",
                table: "ProductBatches",
                columns: new[] { "BranchId", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_ProductId_BatchNumber",
                table: "ProductBatches",
                columns: new[] { "ProductId", "BatchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_QuantityAvailable",
                table: "ProductBatches",
                column: "QuantityAvailable");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductBatches_Manufacturing_Before_Expiry",
                table: "ProductBatches",
                sql: "\"ManufacturingDate\" IS NULL OR \"ManufacturingDate\" <= \"ExpiryDate\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductBatches_Prices_NonNegative",
                table: "ProductBatches",
                sql: "\"PurchasePrice\" >= 0 AND \"RetailPrice\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductBatches_QuantityAvailable_NonNegative",
                table: "ProductBatches",
                sql: "\"QuantityAvailable\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductBatches_QuantityReceived_NonNegative",
                table: "ProductBatches",
                sql: "\"QuantityReceived\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_BranchId_ProductId",
                table: "Inventory",
                columns: new[] { "BranchId", "ProductId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_QuantityInStock_NonNegative",
                table: "Inventory",
                sql: "\"QuantityInStock\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventory_ReorderLevel_NonNegative",
                table: "Inventory",
                sql: "\"ReorderLevel\" >= 0");

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000024', 'inventory.view', 'View inventory', 'inventory', now(), now()),
                    ('20000000-0000-0000-0000-000000000025', 'inventory.opening_stock', 'Add opening stock', 'inventory', now(), now()),
                    ('20000000-0000-0000-0000-000000000026', 'inventory.adjust', 'Adjust inventory stock', 'inventory', now(), now()),
                    ('20000000-0000-0000-0000-000000000027', 'inventory.stock_count', 'Reconcile physical stock counts', 'inventory', now(), now()),
                    ('20000000-0000-0000-0000-000000000028', 'inventory.expiry_manage', 'Manage expired stock disposal', 'inventory', now(), now()),
                    ('20000000-0000-0000-0000-000000000029', 'inventory.movements.view', 'View stock movement ledger', 'inventory', now(), now()),
                    ('20000000-0000-0000-0000-000000000030', 'batches.view', 'View product batches', 'inventory', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000073'::uuid, 'Owner', 'inventory.view'),
                    ('30000000-0000-0000-0000-000000000074'::uuid, 'Owner', 'inventory.opening_stock'),
                    ('30000000-0000-0000-0000-000000000075'::uuid, 'Owner', 'inventory.adjust'),
                    ('30000000-0000-0000-0000-000000000076'::uuid, 'Owner', 'inventory.stock_count'),
                    ('30000000-0000-0000-0000-000000000077'::uuid, 'Owner', 'inventory.expiry_manage'),
                    ('30000000-0000-0000-0000-000000000078'::uuid, 'Owner', 'inventory.movements.view'),
                    ('30000000-0000-0000-0000-000000000079'::uuid, 'Owner', 'batches.view'),
                    ('30000000-0000-0000-0000-000000000080'::uuid, 'Manager', 'inventory.view'),
                    ('30000000-0000-0000-0000-000000000081'::uuid, 'Manager', 'inventory.opening_stock'),
                    ('30000000-0000-0000-0000-000000000082'::uuid, 'Manager', 'inventory.adjust'),
                    ('30000000-0000-0000-0000-000000000083'::uuid, 'Manager', 'inventory.stock_count'),
                    ('30000000-0000-0000-0000-000000000084'::uuid, 'Manager', 'inventory.expiry_manage'),
                    ('30000000-0000-0000-0000-000000000085'::uuid, 'Manager', 'inventory.movements.view'),
                    ('30000000-0000-0000-0000-000000000086'::uuid, 'Manager', 'batches.view'),
                    ('30000000-0000-0000-0000-000000000087'::uuid, 'Pharmacist', 'inventory.view'),
                    ('30000000-0000-0000-0000-000000000088'::uuid, 'Pharmacist', 'inventory.expiry_manage'),
                    ('30000000-0000-0000-0000-000000000089'::uuid, 'Pharmacist', 'inventory.movements.view'),
                    ('30000000-0000-0000-0000-000000000090'::uuid, 'Pharmacist', 'batches.view'),
                    ('30000000-0000-0000-0000-000000000091'::uuid, 'Cashier', 'inventory.view'),
                    ('30000000-0000-0000-0000-000000000092'::uuid, 'PurchaseManager', 'inventory.view'),
                    ('30000000-0000-0000-0000-000000000093'::uuid, 'PurchaseManager', 'batches.view'),
                    ('30000000-0000-0000-0000-000000000094'::uuid, 'StoreKeeper', 'inventory.view'),
                    ('30000000-0000-0000-0000-000000000095'::uuid, 'StoreKeeper', 'inventory.opening_stock'),
                    ('30000000-0000-0000-0000-000000000096'::uuid, 'StoreKeeper', 'inventory.adjust'),
                    ('30000000-0000-0000-0000-000000000097'::uuid, 'StoreKeeper', 'inventory.stock_count'),
                    ('30000000-0000-0000-0000-000000000098'::uuid, 'StoreKeeper', 'inventory.expiry_manage'),
                    ('30000000-0000-0000-0000-000000000099'::uuid, 'StoreKeeper', 'inventory.movements.view'),
                    ('30000000-0000-0000-0000-000000000100'::uuid, 'StoreKeeper', 'batches.view')
                ) AS mapping(id, role_name, permission_code)
                JOIN "Roles" role ON role."Name" = mapping.role_name
                JOIN "Permissions" permission ON permission."Code" = mapping.permission_code
                ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_BranchId_ProductId_CreatedAt",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_MovementType_CreatedAt",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ProductBatchId_CreatedAt",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_BranchId_ExpiryDate",
                table: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_ProductId_BatchNumber",
                table: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_QuantityAvailable",
                table: "ProductBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductBatches_Manufacturing_Before_Expiry",
                table: "ProductBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductBatches_Prices_NonNegative",
                table: "ProductBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductBatches_QuantityAvailable_NonNegative",
                table: "ProductBatches");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductBatches_QuantityReceived_NonNegative",
                table: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_BranchId_ProductId",
                table: "Inventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_QuantityInStock_NonNegative",
                table: "Inventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_ReorderLevel_NonNegative",
                table: "Inventory");

            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions" WHERE "PermissionId" IN
                    (SELECT "Id" FROM "Permissions" WHERE "Code" IN
                        ('inventory.view','inventory.opening_stock','inventory.adjust','inventory.stock_count',
                         'inventory.expiry_manage','inventory.movements.view','batches.view'));
                DELETE FROM "Permissions" WHERE "Code" IN
                    ('inventory.view','inventory.opening_stock','inventory.adjust','inventory.stock_count',
                     'inventory.expiry_manage','inventory.movements.view','batches.view');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductBatchId",
                table: "StockMovements",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_ProductId",
                table: "ProductBatches",
                column: "ProductId");
        }
    }
}
