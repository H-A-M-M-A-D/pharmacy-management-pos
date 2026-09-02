using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSupplierManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlternatePhone",
                table: "Suppliers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditLimit",
                table: "Suppliers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningBalance",
                table: "Suppliers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PaymentTermsDays",
                table: "Suppliers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "STRN",
                table: "Suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "Suppliers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsApp",
                table: "Suppliers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierLedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_SupplierLedgerEntries_AmountSign", "\"Amount\" <> 0 AND ((\"EntryType\" = 1) OR (\"EntryType\" IN (3, 5) AND \"Amount\" > 0) OR (\"EntryType\" IN (2, 4, 6) AND \"Amount\" < 0))");
                    table.ForeignKey(
                        name: "FK_SupplierLedgerEntries_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierLedgerEntries_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierLedgerEntries_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_City",
                table: "Suppliers",
                column: "City");

            migrationBuilder.Sql("""
                UPDATE "Suppliers"
                SET "NormalizedName" = upper(btrim("Name"))
                WHERE "NormalizedName" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_NormalizedName",
                table: "Suppliers",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Suppliers_CreditLimit_NonNegative",
                table: "Suppliers",
                sql: "\"CreditLimit\" IS NULL OR \"CreditLimit\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Suppliers_PaymentTermsDays_NonNegative",
                table: "Suppliers",
                sql: "\"PaymentTermsDays\" IS NULL OR \"PaymentTermsDays\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgerEntries_BranchId_CreatedAt",
                table: "SupplierLedgerEntries",
                columns: new[] { "BranchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgerEntries_CreatedByUserId",
                table: "SupplierLedgerEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgerEntries_EntryType_CreatedAt",
                table: "SupplierLedgerEntries",
                columns: new[] { "EntryType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgerEntries_ReferenceType_ReferenceId",
                table: "SupplierLedgerEntries",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgerEntries_SupplierId_BranchId_CreatedAt",
                table: "SupplierLedgerEntries",
                columns: new[] { "SupplierId", "BranchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgerEntries_SupplierId_CreatedAt",
                table: "SupplierLedgerEntries",
                columns: new[] { "SupplierId", "CreatedAt" });

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000031', 'suppliers.view', 'View suppliers', 'suppliers', now(), now()),
                    ('20000000-0000-0000-0000-000000000032', 'suppliers.create', 'Create suppliers', 'suppliers', now(), now()),
                    ('20000000-0000-0000-0000-000000000033', 'suppliers.update', 'Update suppliers', 'suppliers', now(), now()),
                    ('20000000-0000-0000-0000-000000000034', 'suppliers.activate', 'Activate suppliers', 'suppliers', now(), now()),
                    ('20000000-0000-0000-0000-000000000035', 'suppliers.deactivate', 'Deactivate suppliers', 'suppliers', now(), now()),
                    ('20000000-0000-0000-0000-000000000036', 'suppliers.ledger.view', 'View supplier ledger', 'suppliers', now(), now()),
                    ('20000000-0000-0000-0000-000000000037', 'suppliers.payment.create', 'Record supplier payments', 'suppliers', now(), now()),
                    ('20000000-0000-0000-0000-000000000038', 'suppliers.adjust_balance', 'Adjust supplier balance', 'suppliers', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000101'::uuid, 'Owner', 'suppliers.view'),
                    ('30000000-0000-0000-0000-000000000102'::uuid, 'Owner', 'suppliers.create'),
                    ('30000000-0000-0000-0000-000000000103'::uuid, 'Owner', 'suppliers.update'),
                    ('30000000-0000-0000-0000-000000000104'::uuid, 'Owner', 'suppliers.activate'),
                    ('30000000-0000-0000-0000-000000000105'::uuid, 'Owner', 'suppliers.deactivate'),
                    ('30000000-0000-0000-0000-000000000106'::uuid, 'Owner', 'suppliers.ledger.view'),
                    ('30000000-0000-0000-0000-000000000107'::uuid, 'Owner', 'suppliers.payment.create'),
                    ('30000000-0000-0000-0000-000000000108'::uuid, 'Owner', 'suppliers.adjust_balance'),
                    ('30000000-0000-0000-0000-000000000109'::uuid, 'Manager', 'suppliers.view'),
                    ('30000000-0000-0000-0000-000000000110'::uuid, 'Manager', 'suppliers.create'),
                    ('30000000-0000-0000-0000-000000000111'::uuid, 'Manager', 'suppliers.update'),
                    ('30000000-0000-0000-0000-000000000112'::uuid, 'Manager', 'suppliers.activate'),
                    ('30000000-0000-0000-0000-000000000113'::uuid, 'Manager', 'suppliers.deactivate'),
                    ('30000000-0000-0000-0000-000000000114'::uuid, 'Manager', 'suppliers.ledger.view'),
                    ('30000000-0000-0000-0000-000000000115'::uuid, 'Manager', 'suppliers.payment.create'),
                    ('30000000-0000-0000-0000-000000000116'::uuid, 'Manager', 'suppliers.adjust_balance'),
                    ('30000000-0000-0000-0000-000000000117'::uuid, 'PurchaseManager', 'suppliers.view'),
                    ('30000000-0000-0000-0000-000000000118'::uuid, 'PurchaseManager', 'suppliers.create'),
                    ('30000000-0000-0000-0000-000000000119'::uuid, 'PurchaseManager', 'suppliers.update'),
                    ('30000000-0000-0000-0000-000000000120'::uuid, 'PurchaseManager', 'suppliers.ledger.view'),
                    ('30000000-0000-0000-0000-000000000121'::uuid, 'Accountant', 'suppliers.view'),
                    ('30000000-0000-0000-0000-000000000122'::uuid, 'Accountant', 'suppliers.ledger.view'),
                    ('30000000-0000-0000-0000-000000000123'::uuid, 'Accountant', 'suppliers.payment.create'),
                    ('30000000-0000-0000-0000-000000000124'::uuid, 'Accountant', 'suppliers.adjust_balance'),
                    ('30000000-0000-0000-0000-000000000125'::uuid, 'StoreKeeper', 'suppliers.view')
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
                        ('suppliers.view','suppliers.create','suppliers.update','suppliers.activate',
                         'suppliers.deactivate','suppliers.ledger.view','suppliers.payment.create',
                         'suppliers.adjust_balance'));
                DELETE FROM "Permissions" WHERE "Code" IN
                    ('suppliers.view','suppliers.create','suppliers.update','suppliers.activate',
                     'suppliers.deactivate','suppliers.ledger.view','suppliers.payment.create',
                     'suppliers.adjust_balance');
                """);

            migrationBuilder.DropTable(
                name: "SupplierLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_City",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_NormalizedName",
                table: "Suppliers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Suppliers_CreditLimit_NonNegative",
                table: "Suppliers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Suppliers_PaymentTermsDays_NonNegative",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AlternatePhone",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CreditLimit",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "OpeningBalance",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PaymentTermsDays",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "STRN",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "WhatsApp",
                table: "Suppliers");
        }
    }
}
