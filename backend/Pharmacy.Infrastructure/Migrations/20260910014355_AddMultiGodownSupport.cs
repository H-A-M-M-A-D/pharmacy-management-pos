using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiGodownSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_BranchId_ProductId_BatchNumber",
                table: "ProductBatches");

            migrationBuilder.AddColumn<Guid>(
                name: "GodownId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GodownId",
                table: "SalesReturns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GodownId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GodownId",
                table: "PurchaseReturns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GodownId",
                table: "ProductBatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GodownId",
                table: "Inventory",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GodownId",
                table: "GoodsReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Godowns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Godowns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Godowns_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserGodowns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GodownId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGodowns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGodowns_Godowns_GodownId",
                        column: x => x.GodownId,
                        principalTable: "Godowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserGodowns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_BranchId_GodownId_CreatedAt",
                table: "StockMovements",
                columns: new[] { "BranchId", "GodownId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_GodownId",
                table: "StockMovements",
                column: "GodownId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_GodownId",
                table: "SalesReturns",
                column: "GodownId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_GodownId",
                table: "Sales",
                column: "GodownId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_GodownId",
                table: "PurchaseReturns",
                column: "GodownId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_BranchId_GodownId_ProductId",
                table: "ProductBatches",
                columns: new[] { "BranchId", "GodownId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_BranchId_GodownId_ProductId_BatchNumber",
                table: "ProductBatches",
                columns: new[] { "BranchId", "GodownId", "ProductId", "BatchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_GodownId",
                table: "ProductBatches",
                column: "GodownId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_BranchId_GodownId_ProductId",
                table: "Inventory",
                columns: new[] { "BranchId", "GodownId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_GodownId",
                table: "Inventory",
                column: "GodownId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_GodownId",
                table: "GoodsReceipts",
                column: "GodownId");

            migrationBuilder.CreateIndex(
                name: "IX_Godowns_BranchId_IsActive",
                table: "Godowns",
                columns: new[] { "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Godowns_BranchId_NormalizedCode",
                table: "Godowns",
                columns: new[] { "BranchId", "NormalizedCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Godowns_BranchId_OneDefault",
                table: "Godowns",
                column: "BranchId",
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_UserGodowns_GodownId",
                table: "UserGodowns",
                column: "GodownId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGodowns_UserId_GodownId",
                table: "UserGodowns",
                columns: new[] { "UserId", "GodownId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGodowns_UserId_IsDefault",
                table: "UserGodowns",
                columns: new[] { "UserId", "IsDefault" });

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceipts_Godowns_GodownId",
                table: "GoodsReceipts",
                column: "GodownId",
                principalTable: "Godowns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_Godowns_GodownId",
                table: "Inventory",
                column: "GodownId",
                principalTable: "Godowns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductBatches_Godowns_GodownId",
                table: "ProductBatches",
                column: "GodownId",
                principalTable: "Godowns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Godowns_GodownId",
                table: "PurchaseReturns",
                column: "GodownId",
                principalTable: "Godowns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Godowns_GodownId",
                table: "Sales",
                column: "GodownId",
                principalTable: "Godowns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturns_Godowns_GodownId",
                table: "SalesReturns",
                column: "GodownId",
                principalTable: "Godowns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Godowns_GodownId",
                table: "StockMovements",
                column: "GodownId",
                principalTable: "Godowns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // --- Multi-godown data backfill -----------------------------------------------------
            // Every existing branch gets exactly one "Main Godown", marked as its default and active.
            // This preserves all existing stock: nothing is deleted or recreated as a synthetic
            // purchase, we simply assign existing rows a valid GodownId. Works identically for a
            // brand-new empty database (no branches -> no rows inserted/updated) and for a live
            // database with years of history (every branch-scoped row gets backfilled in one pass).
            migrationBuilder.Sql("""
                INSERT INTO "Godowns" ("Id", "BranchId", "Code", "NormalizedCode", "Name", "Description", "IsDefault", "IsActive", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), b."Id", 'MAIN', 'MAIN', 'Main Godown', 'Created automatically by the multi-godown migration as the default stock location for this branch.', true, true, now(), now()
                FROM "Branches" b
                WHERE NOT EXISTS (SELECT 1 FROM "Godowns" g WHERE g."BranchId" = b."Id");

                UPDATE "ProductBatches" pb SET "GodownId" = g."Id"
                FROM "Godowns" g WHERE g."BranchId" = pb."BranchId" AND g."IsDefault" = true AND pb."GodownId" IS NULL;

                UPDATE "Inventory" i SET "GodownId" = g."Id"
                FROM "Godowns" g WHERE g."BranchId" = i."BranchId" AND g."IsDefault" = true AND i."GodownId" IS NULL;

                UPDATE "StockMovements" sm SET "GodownId" = g."Id"
                FROM "Godowns" g WHERE g."BranchId" = sm."BranchId" AND g."IsDefault" = true AND sm."GodownId" IS NULL;

                UPDATE "Sales" s SET "GodownId" = g."Id"
                FROM "Godowns" g WHERE g."BranchId" = s."BranchId" AND g."IsDefault" = true AND s."GodownId" IS NULL;

                UPDATE "GoodsReceipts" gr SET "GodownId" = g."Id"
                FROM "Godowns" g WHERE g."BranchId" = gr."BranchId" AND g."IsDefault" = true AND gr."GodownId" IS NULL;

                UPDATE "PurchaseReturns" pr SET "GodownId" = g."Id"
                FROM "Godowns" g WHERE g."BranchId" = pr."BranchId" AND g."IsDefault" = true AND pr."GodownId" IS NULL;

                UPDATE "SalesReturns" sr SET "GodownId" = g."Id"
                FROM "Godowns" g WHERE g."BranchId" = sr."BranchId" AND g."IsDefault" = true AND sr."GodownId" IS NULL;

                -- Every existing user is granted access to their own branch's default godown so no one
                -- (administrators included) is locked out of POS/GRN/adjustment screens after this
                -- upgrade. Users with branch-selection privilege (Owner/Manager, or anyone holding
                -- users.view) are not restricted to this list at the application layer, but the row is
                -- still seeded here so they see a sensible default in the UI.
                INSERT INTO "UserGodowns" ("Id", "UserId", "GodownId", "IsDefault", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), u."Id", g."Id", true, now(), now()
                FROM "Users" u
                JOIN "Godowns" g ON g."BranchId" = u."BranchId" AND g."IsDefault" = true
                WHERE NOT EXISTS (SELECT 1 FROM "UserGodowns" ug WHERE ug."UserId" = u."Id" AND ug."GodownId" = g."Id");

                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt") VALUES
                (gen_random_uuid(), 'godowns.view', 'View godowns', 'godowns', now(), now()),
                (gen_random_uuid(), 'godowns.create', 'Create godowns', 'godowns', now(), now()),
                (gen_random_uuid(), 'godowns.update', 'Update godowns', 'godowns', now(), now()),
                (gen_random_uuid(), 'godowns.activate', 'Activate godowns', 'godowns', now(), now()),
                (gen_random_uuid(), 'godowns.deactivate', 'Deactivate godowns', 'godowns', now(), now()),
                (gen_random_uuid(), 'godowns.manage', 'Manage which users may transact against a godown', 'godowns', now(), now()),
                (gen_random_uuid(), 'godowns.set_default', 'Set the default godown for a branch', 'godowns', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('Owner', 'godowns.view'), ('Owner', 'godowns.create'), ('Owner', 'godowns.update'),
                    ('Owner', 'godowns.activate'), ('Owner', 'godowns.deactivate'), ('Owner', 'godowns.manage'), ('Owner', 'godowns.set_default'),
                    ('Manager', 'godowns.view'), ('Manager', 'godowns.create'), ('Manager', 'godowns.update'),
                    ('Manager', 'godowns.activate'), ('Manager', 'godowns.deactivate'), ('Manager', 'godowns.manage'), ('Manager', 'godowns.set_default'),
                    ('StoreKeeper', 'godowns.view'), ('StoreKeeper', 'godowns.manage'),
                    ('PurchaseManager', 'godowns.view'),
                    ('Pharmacist', 'godowns.view'),
                    ('Cashier', 'godowns.view')
                ) AS mapping(role_name, permission_code)
                JOIN "Roles" role ON role."Name" = mapping.role_name
                JOIN "Permissions" permission ON permission."Code" = mapping.permission_code
                ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions" WHERE "PermissionId" IN
                    (SELECT "Id" FROM "Permissions" WHERE "Code" LIKE 'godowns.%');
                DELETE FROM "Permissions" WHERE "Code" LIKE 'godowns.%';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceipts_Godowns_GodownId",
                table: "GoodsReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_Inventory_Godowns_GodownId",
                table: "Inventory");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductBatches_Godowns_GodownId",
                table: "ProductBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Godowns_GodownId",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Godowns_GodownId",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturns_Godowns_GodownId",
                table: "SalesReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Godowns_GodownId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "UserGodowns");

            migrationBuilder.DropTable(
                name: "Godowns");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_BranchId_GodownId_CreatedAt",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_GodownId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_SalesReturns_GodownId",
                table: "SalesReturns");

            migrationBuilder.DropIndex(
                name: "IX_Sales_GodownId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_GodownId",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_BranchId_GodownId_ProductId",
                table: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_BranchId_GodownId_ProductId_BatchNumber",
                table: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_ProductBatches_GodownId",
                table: "ProductBatches");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_BranchId_GodownId_ProductId",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_GodownId",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_GodownId",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "GodownId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "GodownId",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "GodownId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "GodownId",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "GodownId",
                table: "ProductBatches");

            migrationBuilder.DropColumn(
                name: "GodownId",
                table: "Inventory");

            migrationBuilder.DropColumn(
                name: "GodownId",
                table: "GoodsReceipts");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_BranchId_ProductId_BatchNumber",
                table: "ProductBatches",
                columns: new[] { "BranchId", "ProductId", "BatchNumber" },
                unique: true);
        }
    }
}
