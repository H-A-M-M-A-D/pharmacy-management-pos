using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompletePosAndSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HoldNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CashierUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ChangeGiven = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.CheckConstraint("CK_Sales_Money_NonNegative", "\"Subtotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"TaxTotal\" >= 0 AND \"NetTotal\" >= 0 AND \"AmountPaid\" >= 0 AND \"ChangeGiven\" >= 0");
                    table.CheckConstraint("CK_Sales_Posted_HasInvoice", "(\"Status\" <> 2) OR (\"InvoiceNumber\" IS NOT NULL AND \"PostedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_Sales_Posted_Paid", "(\"Status\" <> 2) OR (\"AmountPaid\" = \"NetTotal\")");
                    table.CheckConstraint("CK_Sales_Status", "\"Status\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Sales_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sales_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaleItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedQuantity = table.Column<int>(type: "integer", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleItems", x => x.Id);
                    table.CheckConstraint("CK_SaleItems_Discount_Range", "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100 AND \"DiscountAmount\" >= 0");
                    table.CheckConstraint("CK_SaleItems_Money_NonNegative", "\"GrossAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"NetAmount\" >= 0");
                    table.CheckConstraint("CK_SaleItems_Quantity_Positive", "\"RequestedQuantity\" > 0");
                    table.ForeignKey(
                        name: "FK_SaleItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleItems_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    AmountApplied = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TenderedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalePayments", x => x.Id);
                    table.CheckConstraint("CK_SalePayments_Amount_Positive", "\"AmountApplied\" > 0");
                    table.CheckConstraint("CK_SalePayments_CashTender", "(\"Method\" = 1 AND \"TenderedAmount\" IS NOT NULL AND \"TenderedAmount\" >= \"AmountApplied\") OR (\"Method\" <> 1 AND \"TenderedAmount\" IS NULL)");
                    table.CheckConstraint("CK_SalePayments_Method", "\"Method\" IN (1, 2, 3, 4, 5, 6)");
                    table.ForeignKey(
                        name: "FK_SalePayments_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaleItemBatchAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitRetailPriceSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitSalePriceSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCostPriceSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpiryDateSnapshot = table.Column<DateOnly>(type: "date", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleItemBatchAllocations", x => x.Id);
                    table.CheckConstraint("CK_SaleItemBatchAllocations_Money_NonNegative", "\"UnitRetailPriceSnapshot\" >= 0 AND \"UnitSalePriceSnapshot\" >= 0 AND \"UnitCostPriceSnapshot\" >= 0 AND \"GrossAmount\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"NetAmount\" >= 0");
                    table.CheckConstraint("CK_SaleItemBatchAllocations_Quantity_Positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_SaleItemBatchAllocations_ProductBatches_ProductBatchId",
                        column: x => x.ProductBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SaleItemBatchAllocations_SaleItems_SaleItemId",
                        column: x => x.SaleItemId,
                        principalTable: "SaleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleItemBatchAllocations_ProductBatchId",
                table: "SaleItemBatchAllocations",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItemBatchAllocations_SaleItemId",
                table: "SaleItemBatchAllocations",
                column: "SaleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_ProductId",
                table: "SaleItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleItems_SaleId",
                table: "SaleItems",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_Method_CreatedAt",
                table: "SalePayments",
                columns: new[] { "Method", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_SaleId",
                table: "SalePayments",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_BranchId_PostedAtUtc",
                table: "Sales",
                columns: new[] { "BranchId", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CashierUserId_PostedAtUtc",
                table: "Sales",
                columns: new[] { "CashierUserId", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerPhone",
                table: "Sales",
                column: "CustomerPhone");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_HoldNumber",
                table: "Sales",
                column: "HoldNumber",
                unique: true,
                filter: "\"HoldNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_InvoiceNumber",
                table: "Sales",
                column: "InvoiceNumber",
                unique: true,
                filter: "\"InvoiceNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_Status_CreatedAt",
                table: "Sales",
                columns: new[] { "Status", "CreatedAt" });
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000048', 'sales.view', 'View sales history, held sales, and receipts', 'sales', now(), now()),
                    ('20000000-0000-0000-0000-000000000049', 'sales.create', 'Create and post sales invoices', 'sales', now(), now()),
                    ('20000000-0000-0000-0000-000000000050', 'sales.hold', 'Hold, recall, edit, and cancel held sales', 'sales', now(), now()),
                    ('20000000-0000-0000-0000-000000000051', 'sales.discount', 'Apply POS line discounts within product limits', 'sales', now(), now()),
                    ('20000000-0000-0000-0000-000000000052', 'sales.reprint', 'Reprint posted sales receipts', 'sales', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000160'::uuid, 'Owner', 'sales.view'),
                    ('30000000-0000-0000-0000-000000000161'::uuid, 'Owner', 'sales.create'),
                    ('30000000-0000-0000-0000-000000000162'::uuid, 'Owner', 'sales.hold'),
                    ('30000000-0000-0000-0000-000000000163'::uuid, 'Owner', 'sales.discount'),
                    ('30000000-0000-0000-0000-000000000164'::uuid, 'Owner', 'sales.reprint'),
                    ('30000000-0000-0000-0000-000000000165'::uuid, 'Manager', 'sales.view'),
                    ('30000000-0000-0000-0000-000000000166'::uuid, 'Manager', 'sales.create'),
                    ('30000000-0000-0000-0000-000000000167'::uuid, 'Manager', 'sales.hold'),
                    ('30000000-0000-0000-0000-000000000168'::uuid, 'Manager', 'sales.discount'),
                    ('30000000-0000-0000-0000-000000000169'::uuid, 'Manager', 'sales.reprint'),
                    ('30000000-0000-0000-0000-000000000170'::uuid, 'Pharmacist', 'sales.view'),
                    ('30000000-0000-0000-0000-000000000171'::uuid, 'Pharmacist', 'sales.create'),
                    ('30000000-0000-0000-0000-000000000172'::uuid, 'Pharmacist', 'sales.hold'),
                    ('30000000-0000-0000-0000-000000000173'::uuid, 'Pharmacist', 'sales.reprint'),
                    ('30000000-0000-0000-0000-000000000174'::uuid, 'Cashier', 'sales.view'),
                    ('30000000-0000-0000-0000-000000000175'::uuid, 'Cashier', 'sales.create'),
                    ('30000000-0000-0000-0000-000000000176'::uuid, 'Cashier', 'sales.hold'),
                    ('30000000-0000-0000-0000-000000000177'::uuid, 'Cashier', 'sales.reprint'),
                    ('30000000-0000-0000-0000-000000000178'::uuid, 'Accountant', 'sales.view')
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
                        ('sales.view','sales.create','sales.hold','sales.discount','sales.reprint'));
                DELETE FROM "Permissions" WHERE "Code" IN
                    ('sales.view','sales.create','sales.hold','sales.discount','sales.reprint');
                """);
            migrationBuilder.DropTable(
                name: "SaleItemBatchAllocations");

            migrationBuilder.DropTable(
                name: "SalePayments");

            migrationBuilder.DropTable(
                name: "SaleItems");

            migrationBuilder.DropTable(
                name: "Sales");
        }
    }
}
