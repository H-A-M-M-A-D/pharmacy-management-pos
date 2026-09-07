using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSystemAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Branches_Code",
                table: "Branches");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "ProductCategories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "ProductCategories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ProductCategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Manufacturers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "Manufacturers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Manufacturers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "ExpenseCategories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "ExpenseCategories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ExpenseCategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedCode",
                table: "Branches",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE \"Branches\" SET \"NormalizedCode\" = upper(trim(\"Code\"));");

            migrationBuilder.CreateTable(
                name: "BackupRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_DeletedAtUtc",
                table: "ProductCategories",
                column: "DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Manufacturers_DeletedAtUtc",
                table: "Manufacturers",
                column: "DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_DeletedAtUtc",
                table: "ExpenseCategories",
                column: "DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_NormalizedCode",
                table: "Branches",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_CreatedAt",
                table: "BackupRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_Status_CreatedAt",
                table: "BackupRecords",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_UpdatedAt",
                table: "SystemSettings",
                column: "UpdatedAt");

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt") VALUES
                ('20000000-0000-0000-0000-000000000085', 'system.view', 'View system information and settings', 'system', now(), now()),
                ('20000000-0000-0000-0000-000000000086', 'system.settings.manage', 'Manage system settings', 'system', now(), now()),
                ('20000000-0000-0000-0000-000000000087', 'system.backup', 'Create and view database backups', 'system', now(), now()),
                ('20000000-0000-0000-0000-000000000088', 'branches.view', 'View branches', 'branches', now(), now()),
                ('20000000-0000-0000-0000-000000000089', 'branches.manage', 'Manage branches', 'branches', now(), now()),
                ('20000000-0000-0000-0000-000000000090', 'recycle_bin.view', 'View safely deleted reference data', 'system', now(), now()),
                ('20000000-0000-0000-0000-000000000091', 'recycle_bin.restore', 'Delete and restore eligible reference data', 'system', now(), now()),
                ('20000000-0000-0000-0000-000000000092', 'audit.export', 'Export audit events', 'audit', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000288'::uuid, 'Owner', 'system.view'),
                    ('30000000-0000-0000-0000-000000000289'::uuid, 'Owner', 'system.settings.manage'),
                    ('30000000-0000-0000-0000-000000000290'::uuid, 'Owner', 'system.backup'),
                    ('30000000-0000-0000-0000-000000000291'::uuid, 'Owner', 'branches.view'),
                    ('30000000-0000-0000-0000-000000000292'::uuid, 'Owner', 'branches.manage'),
                    ('30000000-0000-0000-0000-000000000293'::uuid, 'Owner', 'recycle_bin.view'),
                    ('30000000-0000-0000-0000-000000000294'::uuid, 'Owner', 'recycle_bin.restore'),
                    ('30000000-0000-0000-0000-000000000295'::uuid, 'Owner', 'audit.export'),
                    ('30000000-0000-0000-0000-000000000296'::uuid, 'Manager', 'system.view'),
                    ('30000000-0000-0000-0000-000000000297'::uuid, 'Manager', 'branches.view'),
                    ('30000000-0000-0000-0000-000000000298'::uuid, 'Manager', 'recycle_bin.view')
                ) AS mapping(id, role_name, permission_code)
                JOIN "Roles" role ON role."Name" = mapping.role_name
                JOIN "Permissions" permission ON permission."Code" = mapping.permission_code
                ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT '30000000-0000-0000-0000-000000000299', role."Id", permission."Id", now(), now()
                FROM "Roles" role CROSS JOIN "Permissions" permission
                WHERE role."Name" = 'Accountant' AND permission."Code" = 'audit.view'
                ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions" WHERE "PermissionId" IN
                    (SELECT "Id" FROM "Permissions" WHERE "Code" IN ('system.view','system.settings.manage','system.backup','branches.view','branches.manage','recycle_bin.view','recycle_bin.restore','audit.export'));
                DELETE FROM "Permissions" WHERE "Code" IN ('system.view','system.settings.manage','system.backup','branches.view','branches.manage','recycle_bin.view','recycle_bin.restore','audit.export');
                """);
            migrationBuilder.DropTable(
                name: "BackupRecords");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropIndex(
                name: "IX_ProductCategories_DeletedAtUtc",
                table: "ProductCategories");

            migrationBuilder.DropIndex(
                name: "IX_Manufacturers_DeletedAtUtc",
                table: "Manufacturers");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseCategories_DeletedAtUtc",
                table: "ExpenseCategories");

            migrationBuilder.DropIndex(
                name: "IX_Branches_NormalizedCode",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "ProductCategories");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "ProductCategories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ProductCategories");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Manufacturers");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Manufacturers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Manufacturers");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "ExpenseCategories");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "ExpenseCategories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ExpenseCategories");

            migrationBuilder.DropColumn(
                name: "NormalizedCode",
                table: "Branches");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Code",
                table: "Branches",
                column: "Code",
                unique: true);
        }
    }
}
