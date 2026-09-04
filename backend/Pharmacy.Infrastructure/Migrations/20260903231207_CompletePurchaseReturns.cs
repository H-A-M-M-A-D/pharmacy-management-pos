using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompletePurchaseReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE SEQUENCE \"PurchaseReturnNumberSequence\" START WITH 1 INCREMENT BY 1;");

            migrationBuilder.CreateTable(
                name: "PurchaseReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OriginalGoodsReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GrossReturnAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAdjustment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAdjustment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetSupplierCredit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturns", x => x.Id);
                    table.CheckConstraint("CK_PurchaseReturns_Money_NonNegative", "\"GrossReturnAmount\" >= 0 AND \"DiscountAdjustment\" >= 0 AND \"TaxAdjustment\" >= 0 AND \"NetSupplierCredit\" >= 0");
                    table.CheckConstraint("CK_PurchaseReturns_Posted", "\"Status\" = 1 AND \"PostedAtUtc\" IS NOT NULL");
                    table.CheckConstraint("CK_PurchaseReturns_Reason", "\"Reason\" IN (1, 2, 3, 4, 5, 6)");
                    table.CheckConstraint("CK_PurchaseReturns_Status", "\"Status\" IN (1)");
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_GoodsReceipts_OriginalGoodsReceiptId",
                        column: x => x.OriginalGoodsReceiptId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReturns_Users_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturnItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalGoodsReceiptItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidReturnQuantity = table.Column<int>(type: "integer", nullable: false),
                    BonusReturnQuantity = table.Column<int>(type: "integer", nullable: false),
                    PurchasePriceSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossReturnAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAdjustment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAdjustment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetSupplierCredit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturnItems", x => x.Id);
                    table.CheckConstraint("CK_PurchaseReturnItems_Money_NonNegative", "\"PurchasePriceSnapshot\" >= 0 AND \"GrossReturnAmount\" >= 0 AND \"DiscountAdjustment\" >= 0 AND \"TaxAdjustment\" >= 0 AND \"NetSupplierCredit\" >= 0");
                    table.CheckConstraint("CK_PurchaseReturnItems_Quantities", "\"PaidReturnQuantity\" >= 0 AND \"BonusReturnQuantity\" >= 0 AND (\"PaidReturnQuantity\" + \"BonusReturnQuantity\") > 0");
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_GoodsReceiptItems_OriginalGoodsReceiptI~",
                        column: x => x.OriginalGoodsReceiptItemId,
                        principalTable: "GoodsReceiptItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_ProductBatches_ProductBatchId",
                        column: x => x.ProductBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReturnItems_PurchaseReturns_PurchaseReturnId",
                        column: x => x.PurchaseReturnId,
                        principalTable: "PurchaseReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_OriginalGoodsReceiptItemId",
                table: "PurchaseReturnItems",
                column: "OriginalGoodsReceiptItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_OriginalGoodsReceiptItemId_PurchaseRetu~",
                table: "PurchaseReturnItems",
                columns: new[] { "OriginalGoodsReceiptItemId", "PurchaseReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_ProductBatchId",
                table: "PurchaseReturnItems",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_ProductId",
                table: "PurchaseReturnItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_PurchaseReturnId",
                table: "PurchaseReturnItems",
                column: "PurchaseReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_BranchId_PostedAtUtc",
                table: "PurchaseReturns",
                columns: new[] { "BranchId", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_OriginalGoodsReceiptId",
                table: "PurchaseReturns",
                column: "OriginalGoodsReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_ProcessedByUserId_PostedAtUtc",
                table: "PurchaseReturns",
                columns: new[] { "ProcessedByUserId", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_Reason",
                table: "PurchaseReturns",
                column: "Reason");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_ReturnNumber",
                table: "PurchaseReturns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_SupplierId_PostedAtUtc",
                table: "PurchaseReturns",
                columns: new[] { "SupplierId", "PostedAtUtc" });
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000057', 'purchase_returns.view', 'View purchase returns, returnable goods receipts, and supplier return notes', 'purchasing', now(), now()),
                    ('20000000-0000-0000-0000-000000000058', 'purchase_returns.create', 'Post purchase returns against original goods receipts', 'purchasing', now(), now()),
                    ('20000000-0000-0000-0000-000000000059', 'purchase_returns.reprint', 'Reprint purchase return supplier notes', 'purchasing', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000193'::uuid, 'Owner', 'purchase_returns.view'),
                    ('30000000-0000-0000-0000-000000000194'::uuid, 'Owner', 'purchase_returns.create'),
                    ('30000000-0000-0000-0000-000000000195'::uuid, 'Owner', 'purchase_returns.reprint'),
                    ('30000000-0000-0000-0000-000000000196'::uuid, 'Manager', 'purchase_returns.view'),
                    ('30000000-0000-0000-0000-000000000197'::uuid, 'Manager', 'purchase_returns.create'),
                    ('30000000-0000-0000-0000-000000000198'::uuid, 'Manager', 'purchase_returns.reprint'),
                    ('30000000-0000-0000-0000-000000000199'::uuid, 'PurchaseManager', 'purchase_returns.view'),
                    ('30000000-0000-0000-0000-000000000200'::uuid, 'PurchaseManager', 'purchase_returns.create'),
                    ('30000000-0000-0000-0000-000000000201'::uuid, 'PurchaseManager', 'purchase_returns.reprint'),
                    ('30000000-0000-0000-0000-000000000202'::uuid, 'StoreKeeper', 'purchase_returns.view'),
                    ('30000000-0000-0000-0000-000000000203'::uuid, 'StoreKeeper', 'purchase_returns.create'),
                    ('30000000-0000-0000-0000-000000000204'::uuid, 'Accountant', 'purchase_returns.view')
                ) AS mapping(id, role_name, permission_code)
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
                    (SELECT "Id" FROM "Permissions" WHERE "Code" IN
                        ('purchase_returns.view','purchase_returns.create','purchase_returns.reprint'));
                DELETE FROM "Permissions" WHERE "Code" IN
                    ('purchase_returns.view','purchase_returns.create','purchase_returns.reprint');
                """);
            migrationBuilder.DropTable(
                name: "PurchaseReturnItems");

            migrationBuilder.DropTable(
                name: "PurchaseReturns");

            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS \"PurchaseReturnNumberSequence\";");
        }
    }
}

