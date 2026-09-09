using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompletePhysicalStockCounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "StockCountNumberSequence");

            migrationBuilder.CreateTable(
                name: "StockCountSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCountSessions", x => x.Id);
                    table.CheckConstraint("CK_StockCountSessions_Scope", "\"Scope\" IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_StockCountSessions_Status", "\"Status\" IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_StockCountSessions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCountSessions_ProductCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCountSessions_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCountSessions_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCountSessions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCountSessions_Users_StartedByUserId",
                        column: x => x.StartedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockCountItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockCountSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemQuantity = table.Column<int>(type: "integer", nullable: false),
                    CountedQuantity = table.Column<int>(type: "integer", nullable: true),
                    UnitCostSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CountedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CountedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockCountItems", x => x.Id);
                    table.CheckConstraint("CK_StockCountItems_CountedQuantity_NonNegative", "\"CountedQuantity\" IS NULL OR \"CountedQuantity\" >= 0");
                    table.CheckConstraint("CK_StockCountItems_SystemQuantity_NonNegative", "\"SystemQuantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_StockCountItems_ProductBatches_ProductBatchId",
                        column: x => x.ProductBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCountItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockCountItems_StockCountSessions_StockCountSessionId",
                        column: x => x.StockCountSessionId,
                        principalTable: "StockCountSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockCountItems_Users_CountedByUserId",
                        column: x => x.CountedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockCountItems_CountedByUserId",
                table: "StockCountItems",
                column: "CountedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCountItems_ProductBatchId",
                table: "StockCountItems",
                column: "ProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCountItems_ProductId",
                table: "StockCountItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCountItems_StockCountSessionId",
                table: "StockCountItems",
                column: "StockCountSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCountItems_StockCountSessionId_ProductBatchId",
                table: "StockCountItems",
                columns: new[] { "StockCountSessionId", "ProductBatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockCountSessions_BranchId_CountDate",
                table: "StockCountSessions",
                columns: new[] { "BranchId", "CountDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockCountSessions_CancelledByUserId",
                table: "StockCountSessions",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCountSessions_CategoryId",
                table: "StockCountSessions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCountSessions_CompletedByUserId",
                table: "StockCountSessions",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCountSessions_CountNumber",
                table: "StockCountSessions",
                column: "CountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockCountSessions_CreatedByUserId",
                table: "StockCountSessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCountSessions_StartedByUserId",
                table: "StockCountSessions",
                column: "StartedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCountSessions_Status",
                table: "StockCountSessions",
                column: "Status");

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000093', 'inventory.stock_count.view', 'View physical stock count sessions', 'inventory', now(), now()),
                    ('20000000-0000-0000-0000-000000000094', 'inventory.stock_count.finalize', 'Finalize physical stock count sessions and post variances', 'inventory', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000300'::uuid, 'Owner', 'inventory.stock_count.view'),
                    ('30000000-0000-0000-0000-000000000301'::uuid, 'Owner', 'inventory.stock_count.finalize'),
                    ('30000000-0000-0000-0000-000000000302'::uuid, 'Manager', 'inventory.stock_count.view'),
                    ('30000000-0000-0000-0000-000000000303'::uuid, 'Manager', 'inventory.stock_count.finalize'),
                    ('30000000-0000-0000-0000-000000000304'::uuid, 'Pharmacist', 'inventory.stock_count.view'),
                    ('30000000-0000-0000-0000-000000000305'::uuid, 'StoreKeeper', 'inventory.stock_count.view')
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
                        ('inventory.stock_count.view', 'inventory.stock_count.finalize'));
                DELETE FROM "Permissions" WHERE "Code" IN
                    ('inventory.stock_count.view', 'inventory.stock_count.finalize');
                """);

            migrationBuilder.DropTable(
                name: "StockCountItems");

            migrationBuilder.DropTable(
                name: "StockCountSessions");

            migrationBuilder.DropSequence(
                name: "StockCountNumberSequence");
        }
    }
}
