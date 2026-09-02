using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompletePurchasingAndGoodsReceiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                    table.CheckConstraint("CK_PurchaseOrders_Status", "\"Status\" IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    GrnNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierInvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NormalizedSupplierInvoiceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceipts", x => x.Id);
                    table.CheckConstraint("CK_GoodsReceipts_Status", "\"Status\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_GoodsReceipts_Totals_NonNegative", "\"Subtotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"TaxTotal\" >= 0 AND \"NetTotal\" >= 0");
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_Users_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderedQuantity = table.Column<int>(type: "integer", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "integer", nullable: false),
                    ExpectedPurchasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderItems", x => x.Id);
                    table.CheckConstraint("CK_PurchaseOrderItems_ExpectedPurchasePrice_NonNegative", "\"ExpectedPurchasePrice\" IS NULL OR \"ExpectedPurchasePrice\" >= 0");
                    table.CheckConstraint("CK_PurchaseOrderItems_OrderedQuantity_Positive", "\"OrderedQuantity\" > 0");
                    table.CheckConstraint("CK_PurchaseOrderItems_ReceivedQuantity_Range", "\"ReceivedQuantity\" >= 0 AND \"ReceivedQuantity\" <= \"OrderedQuantity\"");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ManufacturingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PurchasedQuantity = table.Column<int>(type: "integer", nullable: false),
                    BonusQuantity = table.Column<int>(type: "integer", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RetailPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetLineAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptItems", x => x.Id);
                    table.CheckConstraint("CK_GoodsReceiptItems_Discount_Range", "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100 AND \"DiscountAmount\" >= 0");
                    table.CheckConstraint("CK_GoodsReceiptItems_Manufacturing_Before_Expiry", "\"ManufacturingDate\" IS NULL OR \"ManufacturingDate\" <= \"ExpiryDate\"");
                    table.CheckConstraint("CK_GoodsReceiptItems_NetLineAmount_NonNegative", "\"NetLineAmount\" >= 0");
                    table.CheckConstraint("CK_GoodsReceiptItems_Prices_NonNegative", "\"PurchasePrice\" >= 0 AND \"RetailPrice\" >= 0");
                    table.CheckConstraint("CK_GoodsReceiptItems_Quantities", "\"PurchasedQuantity\" > 0 AND \"BonusQuantity\" >= 0");
                    table.CheckConstraint("CK_GoodsReceiptItems_Tax_Range", "\"TaxPercent\" >= 0 AND \"TaxPercent\" <= 100 AND \"TaxAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_GoodsReceiptItems_GoodsReceipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptItems_ProductBatches_ProductBatchId",
                        column: x => x.ProductBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptItems_PurchaseOrderItems_PurchaseOrderItemId",
                        column: x => x.PurchaseOrderItemId,
                        principalTable: "PurchaseOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_GoodsReceiptId",
                table: "GoodsReceiptItems",
                column: "GoodsReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_ProductBatchId",
                table: "GoodsReceiptItems",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_ProductId",
                table: "GoodsReceiptItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_ProductId_BatchNumber",
                table: "GoodsReceiptItems",
                columns: new[] { "ProductId", "BatchNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_PurchaseOrderItemId",
                table: "GoodsReceiptItems",
                column: "PurchaseOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_BranchId_ReceiptDate",
                table: "GoodsReceipts",
                columns: new[] { "BranchId", "ReceiptDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_GrnNumber",
                table: "GoodsReceipts",
                column: "GrnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_PurchaseOrderId",
                table: "GoodsReceipts",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_ReceivedByUserId",
                table: "GoodsReceipts",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_Status_ReceiptDate",
                table: "GoodsReceipts",
                columns: new[] { "Status", "ReceiptDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_SupplierId_NormalizedSupplierInvoiceNumber",
                table: "GoodsReceipts",
                columns: new[] { "SupplierId", "NormalizedSupplierInvoiceNumber" },
                unique: true,
                filter: "\"NormalizedSupplierInvoiceNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_SupplierId_ReceiptDate",
                table: "GoodsReceipts",
                columns: new[] { "SupplierId", "ReceiptDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_ProductId",
                table: "PurchaseOrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_PurchaseOrderId",
                table: "PurchaseOrderItems",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_BranchId_OrderDate",
                table: "PurchaseOrders",
                columns: new[] { "BranchId", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CreatedByUserId",
                table: "PurchaseOrders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_OrderNumber",
                table: "PurchaseOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Status_OrderDate",
                table: "PurchaseOrders",
                columns: new[] { "Status", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId_OrderDate",
                table: "PurchaseOrders",
                columns: new[] { "SupplierId", "OrderDate" });

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000039', 'purchases.view', 'View purchases and goods receipts', 'purchasing', now(), now()),
                    ('20000000-0000-0000-0000-000000000040', 'purchases.create', 'Create purchase documents', 'purchasing', now(), now()),
                    ('20000000-0000-0000-0000-000000000041', 'purchases.update_draft', 'Update draft purchase documents', 'purchasing', now(), now()),
                    ('20000000-0000-0000-0000-000000000042', 'purchases.cancel', 'Cancel purchase documents', 'purchasing', now(), now()),
                    ('20000000-0000-0000-0000-000000000043', 'purchases.receive', 'Receive and post purchased goods', 'purchasing', now(), now()),
                    ('20000000-0000-0000-0000-000000000044', 'purchase_orders.view', 'View purchase orders', 'purchasing', now(), now()),
                    ('20000000-0000-0000-0000-000000000045', 'purchase_orders.create', 'Create purchase orders', 'purchasing', now(), now()),
                    ('20000000-0000-0000-0000-000000000046', 'purchase_orders.update', 'Update and submit purchase orders', 'purchasing', now(), now()),
                    ('20000000-0000-0000-0000-000000000047', 'purchase_orders.cancel', 'Cancel purchase orders', 'purchasing', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000126'::uuid, 'Owner', 'purchases.view'),
                    ('30000000-0000-0000-0000-000000000127'::uuid, 'Owner', 'purchases.create'),
                    ('30000000-0000-0000-0000-000000000128'::uuid, 'Owner', 'purchases.update_draft'),
                    ('30000000-0000-0000-0000-000000000129'::uuid, 'Owner', 'purchases.cancel'),
                    ('30000000-0000-0000-0000-000000000130'::uuid, 'Owner', 'purchases.receive'),
                    ('30000000-0000-0000-0000-000000000131'::uuid, 'Owner', 'purchase_orders.view'),
                    ('30000000-0000-0000-0000-000000000132'::uuid, 'Owner', 'purchase_orders.create'),
                    ('30000000-0000-0000-0000-000000000133'::uuid, 'Owner', 'purchase_orders.update'),
                    ('30000000-0000-0000-0000-000000000134'::uuid, 'Owner', 'purchase_orders.cancel'),
                    ('30000000-0000-0000-0000-000000000135'::uuid, 'Manager', 'purchases.view'),
                    ('30000000-0000-0000-0000-000000000136'::uuid, 'Manager', 'purchases.create'),
                    ('30000000-0000-0000-0000-000000000137'::uuid, 'Manager', 'purchases.update_draft'),
                    ('30000000-0000-0000-0000-000000000138'::uuid, 'Manager', 'purchases.cancel'),
                    ('30000000-0000-0000-0000-000000000139'::uuid, 'Manager', 'purchases.receive'),
                    ('30000000-0000-0000-0000-000000000140'::uuid, 'Manager', 'purchase_orders.view'),
                    ('30000000-0000-0000-0000-000000000141'::uuid, 'Manager', 'purchase_orders.create'),
                    ('30000000-0000-0000-0000-000000000142'::uuid, 'Manager', 'purchase_orders.update'),
                    ('30000000-0000-0000-0000-000000000143'::uuid, 'Manager', 'purchase_orders.cancel'),
                    ('30000000-0000-0000-0000-000000000144'::uuid, 'PurchaseManager', 'purchases.view'),
                    ('30000000-0000-0000-0000-000000000145'::uuid, 'PurchaseManager', 'purchases.create'),
                    ('30000000-0000-0000-0000-000000000146'::uuid, 'PurchaseManager', 'purchases.update_draft'),
                    ('30000000-0000-0000-0000-000000000147'::uuid, 'PurchaseManager', 'purchases.cancel'),
                    ('30000000-0000-0000-0000-000000000148'::uuid, 'PurchaseManager', 'purchases.receive'),
                    ('30000000-0000-0000-0000-000000000149'::uuid, 'PurchaseManager', 'purchase_orders.view'),
                    ('30000000-0000-0000-0000-000000000150'::uuid, 'PurchaseManager', 'purchase_orders.create'),
                    ('30000000-0000-0000-0000-000000000151'::uuid, 'PurchaseManager', 'purchase_orders.update'),
                    ('30000000-0000-0000-0000-000000000152'::uuid, 'PurchaseManager', 'purchase_orders.cancel'),
                    ('30000000-0000-0000-0000-000000000153'::uuid, 'StoreKeeper', 'purchases.view'),
                    ('30000000-0000-0000-0000-000000000154'::uuid, 'StoreKeeper', 'purchases.receive'),
                    ('30000000-0000-0000-0000-000000000155'::uuid, 'StoreKeeper', 'purchase_orders.view'),
                    ('30000000-0000-0000-0000-000000000156'::uuid, 'Accountant', 'purchases.view'),
                    ('30000000-0000-0000-0000-000000000157'::uuid, 'Accountant', 'purchase_orders.view'),
                    ('30000000-0000-0000-0000-000000000158'::uuid, 'Pharmacist', 'purchase_orders.view')
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
                        ('purchases.view','purchases.create','purchases.update_draft','purchases.cancel',
                         'purchases.receive','purchase_orders.view','purchase_orders.create',
                         'purchase_orders.update','purchase_orders.cancel'));
                DELETE FROM "Permissions" WHERE "Code" IN
                    ('purchases.view','purchases.create','purchases.update_draft','purchases.cancel',
                     'purchases.receive','purchase_orders.view','purchase_orders.create',
                     'purchase_orders.update','purchase_orders.cancel');
                """);

            migrationBuilder.DropTable(
                name: "GoodsReceiptItems");

            migrationBuilder.DropTable(
                name: "GoodsReceipts");

            migrationBuilder.DropTable(
                name: "PurchaseOrderItems");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");
        }
    }
}
