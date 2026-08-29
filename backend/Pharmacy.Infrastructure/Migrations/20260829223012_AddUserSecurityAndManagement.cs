using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSecurityAndManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "LastLoginAt",
                table: "Users",
                newName: "LastLoginAtUtc");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEndUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedUsername",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TokenVersion",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "Users"
                SET "NormalizedUsername" = upper(btrim("Username")),
                    "NormalizedEmail" = CASE
                        WHEN "Email" IS NULL OR btrim("Email") = '' THEN NULL
                        ELSE upper(btrim("Email"))
                    END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_LockoutEndUtc",
                table: "Users",
                column: "LockoutEndUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true,
                filter: "\"NormalizedEmail\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedUsername",
                table: "Users",
                column: "NormalizedUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId_IsActive",
                table: "Users",
                columns: new[] { "RoleId", "IsActive" });

            migrationBuilder.Sql("""
                INSERT INTO "Roles" ("Id", "Name", "Description", "IsSystem", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                    ('10000000-0000-0000-0000-000000000001', 'Owner', 'System owner with all current permissions', true, true, now(), now()),
                    ('10000000-0000-0000-0000-000000000002', 'Manager', 'Operational manager without Owner account control', true, true, now(), now()),
                    ('10000000-0000-0000-0000-000000000003', 'Pharmacist', 'Pharmacist profile access', true, true, now(), now()),
                    ('10000000-0000-0000-0000-000000000004', 'Cashier', 'Cashier profile access', true, true, now(), now()),
                    ('10000000-0000-0000-0000-000000000005', 'PurchaseManager', 'Purchase manager profile access', true, true, now(), now()),
                    ('10000000-0000-0000-0000-000000000006', 'Accountant', 'Accountant profile access', true, true, now(), now()),
                    ('10000000-0000-0000-0000-000000000007', 'StoreKeeper', 'Store keeper profile access', true, true, now(), now())
                ON CONFLICT ("Name") DO NOTHING;

                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000001', 'users.view', 'View users', 'users', now(), now()),
                    ('20000000-0000-0000-0000-000000000002', 'users.create', 'Create users', 'users', now(), now()),
                    ('20000000-0000-0000-0000-000000000003', 'users.update', 'Update users', 'users', now(), now()),
                    ('20000000-0000-0000-0000-000000000004', 'users.activate', 'Activate users', 'users', now(), now()),
                    ('20000000-0000-0000-0000-000000000005', 'users.deactivate', 'Deactivate users', 'users', now(), now()),
                    ('20000000-0000-0000-0000-000000000006', 'users.reset_password', 'Reset user passwords', 'users', now(), now()),
                    ('20000000-0000-0000-0000-000000000007', 'users.manage_owner', 'Manage Owner accounts', 'users', now(), now()),
                    ('20000000-0000-0000-0000-000000000008', 'roles.view', 'View roles', 'roles', now(), now()),
                    ('20000000-0000-0000-0000-000000000009', 'roles.manage', 'Manage role permissions', 'roles', now(), now()),
                    ('20000000-0000-0000-0000-000000000010', 'permissions.view', 'View permission catalog', 'permissions', now(), now()),
                    ('20000000-0000-0000-0000-000000000011', 'profile.view', 'View own profile', 'profile', now(), now()),
                    ('20000000-0000-0000-0000-000000000012', 'profile.update', 'Update own profile', 'profile', now(), now()),
                    ('20000000-0000-0000-0000-000000000013', 'profile.change_password', 'Change own password', 'profile', now(), now()),
                    ('20000000-0000-0000-0000-000000000014', 'audit.view', 'View audit events', 'audit', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000001'::uuid, 'Owner', 'users.view'),
                    ('30000000-0000-0000-0000-000000000002'::uuid, 'Owner', 'users.create'),
                    ('30000000-0000-0000-0000-000000000003'::uuid, 'Owner', 'users.update'),
                    ('30000000-0000-0000-0000-000000000004'::uuid, 'Owner', 'users.activate'),
                    ('30000000-0000-0000-0000-000000000005'::uuid, 'Owner', 'users.deactivate'),
                    ('30000000-0000-0000-0000-000000000006'::uuid, 'Owner', 'users.reset_password'),
                    ('30000000-0000-0000-0000-000000000007'::uuid, 'Owner', 'users.manage_owner'),
                    ('30000000-0000-0000-0000-000000000008'::uuid, 'Owner', 'roles.view'),
                    ('30000000-0000-0000-0000-000000000009'::uuid, 'Owner', 'roles.manage'),
                    ('30000000-0000-0000-0000-000000000010'::uuid, 'Owner', 'permissions.view'),
                    ('30000000-0000-0000-0000-000000000011'::uuid, 'Owner', 'profile.view'),
                    ('30000000-0000-0000-0000-000000000012'::uuid, 'Owner', 'profile.update'),
                    ('30000000-0000-0000-0000-000000000013'::uuid, 'Owner', 'profile.change_password'),
                    ('30000000-0000-0000-0000-000000000014'::uuid, 'Owner', 'audit.view'),
                    ('30000000-0000-0000-0000-000000000015'::uuid, 'Manager', 'users.view'),
                    ('30000000-0000-0000-0000-000000000016'::uuid, 'Manager', 'users.create'),
                    ('30000000-0000-0000-0000-000000000017'::uuid, 'Manager', 'users.update'),
                    ('30000000-0000-0000-0000-000000000018'::uuid, 'Manager', 'users.activate'),
                    ('30000000-0000-0000-0000-000000000019'::uuid, 'Manager', 'users.deactivate'),
                    ('30000000-0000-0000-0000-000000000020'::uuid, 'Manager', 'users.reset_password'),
                    ('30000000-0000-0000-0000-000000000021'::uuid, 'Manager', 'roles.view'),
                    ('30000000-0000-0000-0000-000000000022'::uuid, 'Manager', 'permissions.view'),
                    ('30000000-0000-0000-0000-000000000023'::uuid, 'Manager', 'profile.view'),
                    ('30000000-0000-0000-0000-000000000024'::uuid, 'Manager', 'profile.update'),
                    ('30000000-0000-0000-0000-000000000025'::uuid, 'Manager', 'profile.change_password'),
                    ('30000000-0000-0000-0000-000000000026'::uuid, 'Manager', 'audit.view'),
                    ('30000000-0000-0000-0000-000000000027'::uuid, 'Pharmacist', 'profile.view'),
                    ('30000000-0000-0000-0000-000000000028'::uuid, 'Pharmacist', 'profile.update'),
                    ('30000000-0000-0000-0000-000000000029'::uuid, 'Pharmacist', 'profile.change_password'),
                    ('30000000-0000-0000-0000-000000000030'::uuid, 'Cashier', 'profile.view'),
                    ('30000000-0000-0000-0000-000000000031'::uuid, 'Cashier', 'profile.update'),
                    ('30000000-0000-0000-0000-000000000032'::uuid, 'Cashier', 'profile.change_password'),
                    ('30000000-0000-0000-0000-000000000033'::uuid, 'PurchaseManager', 'profile.view'),
                    ('30000000-0000-0000-0000-000000000034'::uuid, 'PurchaseManager', 'profile.update'),
                    ('30000000-0000-0000-0000-000000000035'::uuid, 'PurchaseManager', 'profile.change_password'),
                    ('30000000-0000-0000-0000-000000000036'::uuid, 'Accountant', 'profile.view'),
                    ('30000000-0000-0000-0000-000000000037'::uuid, 'Accountant', 'profile.update'),
                    ('30000000-0000-0000-0000-000000000038'::uuid, 'Accountant', 'profile.change_password'),
                    ('30000000-0000-0000-0000-000000000039'::uuid, 'StoreKeeper', 'profile.view'),
                    ('30000000-0000-0000-0000-000000000040'::uuid, 'StoreKeeper', 'profile.update'),
                    ('30000000-0000-0000-0000-000000000041'::uuid, 'StoreKeeper', 'profile.change_password')
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
                name: "IX_Users_LockoutEndUtc",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_NormalizedUsername",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId_IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockoutEndUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedUsername",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TokenVersion",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "LastLoginAtUtc",
                table: "Users",
                newName: "LastLoginAt");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }
    }
}
