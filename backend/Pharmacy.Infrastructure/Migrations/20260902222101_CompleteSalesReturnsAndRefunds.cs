using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSalesReturnsAndRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE SEQUENCE \"SalesReturnNumberSequence\" START WITH 1 INCREMENT BY 1;");

            migrationBuilder.CreateTable(
                name: "SalesReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OriginalSaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GrossReturnAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountReturnAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxReturnAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReturns", x => x.Id);
                    table.CheckConstraint("CK_SalesReturns_Money_NonNegative", "\"GrossReturnAmount\" >= 0 AND \"DiscountReturnAmount\" >= 0 AND \"TaxReturnAmount\" >= 0 AND \"RefundAmount\" >= 0");
                    table.CheckConstraint("CK_SalesReturns_Posted", "\"Status\" = 1 AND \"PostedAtUtc\" IS NOT NULL");
                    table.CheckConstraint("CK_SalesReturns_Reason", "\"Reason\" IN (1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_SalesReturns_Status", "\"Status\" IN (1)");
                    table.ForeignKey(
                        name: "FK_SalesReturns_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesReturns_Sales_OriginalSaleId",
                        column: x => x.OriginalSaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesReturns_Users_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesRefundPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesRefundPayments", x => x.Id);
                    table.CheckConstraint("CK_SalesRefundPayments_Amount_Positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_SalesRefundPayments_Method", "\"Method\" IN (1, 2, 3, 4, 5, 6)");
                    table.ForeignKey(
                        name: "FK_SalesRefundPayments_SalesReturns_SalesReturnId",
                        column: x => x.SalesReturnId,
                        principalTable: "SalesReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesReturnItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalSaleItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    GrossReturnAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountReturnAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxReturnAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReturnItems", x => x.Id);
                    table.CheckConstraint("CK_SalesReturnItems_Money_NonNegative", "\"GrossReturnAmount\" >= 0 AND \"DiscountReturnAmount\" >= 0 AND \"TaxReturnAmount\" >= 0 AND \"RefundAmount\" >= 0");
                    table.CheckConstraint("CK_SalesReturnItems_Quantity_Positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_SalesReturnItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesReturnItems_SaleItems_OriginalSaleItemId",
                        column: x => x.OriginalSaleItemId,
                        principalTable: "SaleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesReturnItems_SalesReturns_SalesReturnId",
                        column: x => x.SalesReturnId,
                        principalTable: "SalesReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesReturnAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesReturnItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalSaleItemBatchAllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Disposition = table.Column<int>(type: "integer", nullable: false),
                    UnitRetailPriceSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitSalePriceSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCostPriceSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpiryDateSnapshot = table.Column<DateOnly>(type: "date", nullable: false),
                    GrossReturnAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountReturnAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxReturnAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReturnAllocations", x => x.Id);
                    table.CheckConstraint("CK_SalesReturnAllocations_Disposition", "\"Disposition\" IN (1, 2)");
                    table.CheckConstraint("CK_SalesReturnAllocations_Money_NonNegative", "\"UnitRetailPriceSnapshot\" >= 0 AND \"UnitSalePriceSnapshot\" >= 0 AND \"UnitCostPriceSnapshot\" >= 0 AND \"GrossReturnAmount\" >= 0 AND \"DiscountReturnAmount\" >= 0 AND \"TaxReturnAmount\" >= 0 AND \"RefundAmount\" >= 0");
                    table.CheckConstraint("CK_SalesReturnAllocations_Quantity_Positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_SalesReturnAllocations_ProductBatches_ProductBatchId",
                        column: x => x.ProductBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesReturnAllocations_SaleItemBatchAllocations_OriginalSal~",
                        column: x => x.OriginalSaleItemBatchAllocationId,
                        principalTable: "SaleItemBatchAllocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesReturnAllocations_SalesReturnItems_SalesReturnItemId",
                        column: x => x.SalesReturnItemId,
                        principalTable: "SalesReturnItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesRefundPayments_Method_CreatedAt",
                table: "SalesRefundPayments",
                columns: new[] { "Method", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesRefundPayments_SalesReturnId",
                table: "SalesRefundPayments",
                column: "SalesReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnAllocations_OriginalSaleItemBatchAllocationId",
                table: "SalesReturnAllocations",
                column: "OriginalSaleItemBatchAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnAllocations_ProductBatchId",
                table: "SalesReturnAllocations",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnAllocations_SalesReturnItemId",
                table: "SalesReturnAllocations",
                column: "SalesReturnItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnItems_OriginalSaleItemId",
                table: "SalesReturnItems",
                column: "OriginalSaleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnItems_ProductId",
                table: "SalesReturnItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnItems_SalesReturnId",
                table: "SalesReturnItems",
                column: "SalesReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_BranchId_PostedAtUtc",
                table: "SalesReturns",
                columns: new[] { "BranchId", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_OriginalSaleId",
                table: "SalesReturns",
                column: "OriginalSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_ProcessedByUserId_PostedAtUtc",
                table: "SalesReturns",
                columns: new[] { "ProcessedByUserId", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_Reason",
                table: "SalesReturns",
                column: "Reason");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_ReturnNumber",
                table: "SalesReturns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_Status_ReturnDateUtc",
                table: "SalesReturns",
                columns: new[] { "Status", "ReturnDateUtc" });
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000053', 'sales.returns.view', 'View sales returns, returnable invoices, and return receipts', 'sales', now(), now()),
                    ('20000000-0000-0000-0000-000000000054', 'sales.returns.create', 'Create posted sales returns against original invoices', 'sales', now(), now()),
                    ('20000000-0000-0000-0000-000000000055', 'sales.returns.refund', 'Record refund payments for sales returns', 'sales', now(), now()),
                    ('20000000-0000-0000-0000-000000000056', 'sales.returns.reprint', 'Reprint sales return receipts', 'sales', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000180'::uuid, 'Owner', 'sales.returns.view'),
                    ('30000000-0000-0000-0000-000000000181'::uuid, 'Owner', 'sales.returns.create'),
                    ('30000000-0000-0000-0000-000000000182'::uuid, 'Owner', 'sales.returns.refund'),
                    ('30000000-0000-0000-0000-000000000183'::uuid, 'Owner', 'sales.returns.reprint'),
                    ('30000000-0000-0000-0000-000000000184'::uuid, 'Manager', 'sales.returns.view'),
                    ('30000000-0000-0000-0000-000000000185'::uuid, 'Manager', 'sales.returns.create'),
                    ('30000000-0000-0000-0000-000000000186'::uuid, 'Manager', 'sales.returns.refund'),
                    ('30000000-0000-0000-0000-000000000187'::uuid, 'Manager', 'sales.returns.reprint'),
                    ('30000000-0000-0000-0000-000000000188'::uuid, 'Pharmacist', 'sales.returns.view'),
                    ('30000000-0000-0000-0000-000000000189'::uuid, 'Pharmacist', 'sales.returns.create'),
                    ('30000000-0000-0000-0000-000000000190'::uuid, 'Cashier', 'sales.returns.view'),
                    ('30000000-0000-0000-0000-000000000191'::uuid, 'Accountant', 'sales.returns.view'),
                    ('30000000-0000-0000-0000-000000000192'::uuid, 'Accountant', 'sales.returns.refund')
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
                        ('sales.returns.view','sales.returns.create','sales.returns.refund','sales.returns.reprint'));
                DELETE FROM "Permissions" WHERE "Code" IN
                    ('sales.returns.view','sales.returns.create','sales.returns.refund','sales.returns.reprint');
                """);
            migrationBuilder.DropTable(
                name: "SalesRefundPayments");

            migrationBuilder.DropTable(
                name: "SalesReturnAllocations");

            migrationBuilder.DropTable(
                name: "SalesReturnItems");

            migrationBuilder.DropTable(
                name: "SalesReturns");

            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS \"SalesReturnNumberSequence\";");
        }
    }
}

