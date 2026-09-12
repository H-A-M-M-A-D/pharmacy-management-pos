using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase6PricingAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TriggerType = table.Column<int>(type: "integer", nullable: false),
                    ConditionsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastRunAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationRules_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationRules_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    GodownId = table.Column<Guid>(type: "uuid", nullable: true),
                    DismissedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessAlerts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessAlerts_Godowns_GodownId",
                        column: x => x.GodownId,
                        principalTable: "Godowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PricingPriceHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceLevelId = table.Column<Guid>(type: "uuid", nullable: true),
                    OldPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NewPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingPriceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingPriceHistories_PriceLevels_PriceLevelId",
                        column: x => x.PriceLevelId,
                        principalTable: "PriceLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingPriceHistories_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingPriceHistories_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PricingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManufacturerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerType = table.Column<int>(type: "integer", nullable: true),
                    PriceLevelId = table.Column<Guid>(type: "uuid", nullable: true),
                    SaleType = table.Column<int>(type: "integer", nullable: true),
                    MinimumQuantity = table.Column<int>(type: "integer", nullable: true),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdjustmentType = table.Column<int>(type: "integer", nullable: false),
                    AdjustmentValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingRules", x => x.Id);
                    table.CheckConstraint("CK_PricingRules_Adjustment_NonNegative", "\"AdjustmentValue\" >= 0");
                    table.CheckConstraint("CK_PricingRules_DateRange", "\"EndsAtUtc\" IS NULL OR \"StartsAtUtc\" IS NULL OR \"EndsAtUtc\" > \"StartsAtUtc\"");
                    table.CheckConstraint("CK_PricingRules_MinimumQuantity_Positive", "\"MinimumQuantity\" IS NULL OR \"MinimumQuantity\" > 0");
                    table.CheckConstraint("CK_PricingRules_Percent_Max", "\"AdjustmentType\" <> 2 OR \"AdjustmentValue\" <= 100");
                    table.CheckConstraint("CK_PricingRules_Priority_NonNegative", "\"Priority\" >= 0");
                    table.ForeignKey(
                        name: "FK_PricingRules_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRules_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRules_Manufacturers_ManufacturerId",
                        column: x => x.ManufacturerId,
                        principalTable: "Manufacturers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRules_PriceLevels_PriceLevelId",
                        column: x => x.PriceLevelId,
                        principalTable: "PriceLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRules_ProductCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRules_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_BranchId",
                table: "AutomationRules",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_CreatedByUserId",
                table: "AutomationRules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_IsActive_TriggerType_BranchId",
                table: "AutomationRules",
                columns: new[] { "IsActive", "TriggerType", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAlerts_BranchId",
                table: "BusinessAlerts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAlerts_DismissedAtUtc_ResolvedAtUtc_Severity",
                table: "BusinessAlerts",
                columns: new[] { "DismissedAtUtc", "ResolvedAtUtc", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAlerts_GodownId",
                table: "BusinessAlerts",
                column: "GodownId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAlerts_SourceType_SourceKey",
                table: "BusinessAlerts",
                columns: new[] { "SourceType", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingPriceHistories_ActorId",
                table: "PricingPriceHistories",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingPriceHistories_PriceLevelId",
                table: "PricingPriceHistories",
                column: "PriceLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingPriceHistories_ProductId_CreatedAt",
                table: "PricingPriceHistories",
                columns: new[] { "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_BranchId",
                table: "PricingRules",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_CategoryId",
                table: "PricingRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_CustomerId",
                table: "PricingRules",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_IsActive_Priority_StartsAtUtc_EndsAtUtc",
                table: "PricingRules",
                columns: new[] { "IsActive", "Priority", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_ManufacturerId",
                table: "PricingRules",
                column: "ManufacturerId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_PriceLevelId",
                table: "PricingRules",
                column: "PriceLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_ProductId_CategoryId_ManufacturerId",
                table: "PricingRules",
                columns: new[] { "ProductId", "CategoryId", "ManufacturerId" });

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt") VALUES
                (gen_random_uuid(), 'pricing.suggest', 'Suggest prices from target margin or markup', 'pricing', now(), now()),
                (gen_random_uuid(), 'pricing.bulk_update', 'Preview and apply bulk price changes', 'pricing', now(), now()),
                (gen_random_uuid(), 'pricing.override', 'Approve sensitive pricing changes', 'pricing', now(), now()),
                (gen_random_uuid(), 'pricing.promotions.manage', 'Manage pricing promotions', 'pricing', now(), now()),
                (gen_random_uuid(), 'automation.view', 'View business automation rules', 'automation', now(), now()),
                (gen_random_uuid(), 'automation.manage', 'Manage business automation rules', 'automation', now(), now()),
                (gen_random_uuid(), 'automation.run', 'Run business automation rules', 'automation', now(), now()),
                (gen_random_uuid(), 'alerts.view', 'View business alerts', 'automation', now(), now()),
                (gen_random_uuid(), 'alerts.manage', 'Refresh and dismiss business alerts', 'automation', now(), now()),
                (gen_random_uuid(), 'inventory.reorder.view', 'View reorder suggestions', 'inventory', now(), now()),
                (gen_random_uuid(), 'inventory.reorder.create_po', 'Create draft purchase orders from reorder suggestions', 'inventory', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('Owner', 'pricing.suggest'), ('Owner', 'pricing.bulk_update'), ('Owner', 'pricing.override'), ('Owner', 'pricing.promotions.manage'),
                    ('Owner', 'automation.view'), ('Owner', 'automation.manage'), ('Owner', 'automation.run'), ('Owner', 'alerts.view'), ('Owner', 'alerts.manage'),
                    ('Owner', 'inventory.reorder.view'), ('Owner', 'inventory.reorder.create_po'),
                    ('Manager', 'pricing.suggest'), ('Manager', 'pricing.bulk_update'), ('Manager', 'pricing.override'), ('Manager', 'pricing.promotions.manage'),
                    ('Manager', 'automation.view'), ('Manager', 'automation.manage'), ('Manager', 'automation.run'), ('Manager', 'alerts.view'), ('Manager', 'alerts.manage'),
                    ('Manager', 'inventory.reorder.view'), ('Manager', 'inventory.reorder.create_po'),
                    ('Pharmacist', 'pricing.suggest'), ('Pharmacist', 'alerts.view'), ('Pharmacist', 'inventory.reorder.view'),
                    ('PurchaseManager', 'alerts.view'), ('PurchaseManager', 'alerts.manage'), ('PurchaseManager', 'inventory.reorder.view'), ('PurchaseManager', 'inventory.reorder.create_po')
                ) AS mapping(role_name, permission_code)
                JOIN "Roles" role ON role."Name" = mapping.role_name
                JOIN "Permissions" permission ON permission."Code" = mapping.permission_code
                ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationRules");

            migrationBuilder.DropTable(
                name: "BusinessAlerts");

            migrationBuilder.DropTable(
                name: "PricingPriceHistories");

            migrationBuilder.DropTable(
                name: "PricingRules");

            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions" WHERE "PermissionId" IN
                    (SELECT "Id" FROM "Permissions" WHERE "Code" IN ('pricing.suggest', 'pricing.bulk_update', 'pricing.override', 'pricing.promotions.manage', 'automation.view', 'automation.manage', 'automation.run', 'alerts.view', 'alerts.manage', 'inventory.reorder.view', 'inventory.reorder.create_po'));
                DELETE FROM "Permissions" WHERE "Code" IN ('pricing.suggest', 'pricing.bulk_update', 'pricing.override', 'pricing.promotions.manage', 'automation.view', 'automation.manage', 'automation.run', 'alerts.view', 'alerts.manage', 'inventory.reorder.view', 'inventory.reorder.create_po');
                """);
        }
    }
}
