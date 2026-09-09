using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteAccountingEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "JournalEntryNumberSequence");

            migrationBuilder.CreateTable(
                name: "ChartOfAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    NormalBalance = table.Column<int>(type: "integer", nullable: false),
                    IsPostingAccount = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartOfAccounts", x => x.Id);
                    table.CheckConstraint("CK_ChartOfAccounts_AccountType", "\"AccountType\" IN (1, 2, 3, 4, 5, 6)");
                    table.CheckConstraint("CK_ChartOfAccounts_NormalBalance", "\"NormalBalance\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_ChartOfAccounts_ChartOfAccounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntryDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                    table.CheckConstraint("CK_JournalEntries_Status", "\"Status\" = 1");
                    table.ForeignKey(
                        name: "FK_JournalEntries_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntries_Users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingKey = table.Column<int>(type: "integer", nullable: false),
                    ChartOfAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountMappings", x => x.Id);
                    table.CheckConstraint("CK_AccountMappings_MappingKey", "\"MappingKey\" BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_AccountMappings_ChartOfAccounts_ChartOfAccountId",
                        column: x => x.ChartOfAccountId,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntryLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChartOfAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntryLines", x => x.Id);
                    table.CheckConstraint("CK_JournalEntryLines_Amounts", "\"Debit\" >= 0 AND \"Credit\" >= 0 AND NOT (\"Debit\" > 0 AND \"Credit\" > 0) AND (\"Debit\" > 0 OR \"Credit\" > 0)");
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_ChartOfAccounts_ChartOfAccountId",
                        column: x => x.ChartOfAccountId,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JournalEntryLines_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_ChartOfAccountId",
                table: "AccountMappings",
                column: "ChartOfAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_MappingKey",
                table: "AccountMappings",
                column: "MappingKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_AccountType_IsActive",
                table: "ChartOfAccounts",
                columns: new[] { "AccountType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_NormalizedCode",
                table: "ChartOfAccounts",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_ParentAccountId",
                table: "ChartOfAccounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_BranchId_EntryDateUtc",
                table: "JournalEntries",
                columns: new[] { "BranchId", "EntryDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_EntryDateUtc",
                table: "JournalEntries",
                column: "EntryDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_EntryNumber",
                table: "JournalEntries",
                column: "EntryNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_PostedByUserId",
                table: "JournalEntries",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_SourceType_SourceId",
                table: "JournalEntries",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_BranchId",
                table: "JournalEntryLines",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_ChartOfAccountId_BranchId",
                table: "JournalEntryLines",
                columns: new[] { "ChartOfAccountId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_CustomerId",
                table: "JournalEntryLines",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_JournalEntryId",
                table: "JournalEntryLines",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_SupplierId",
                table: "JournalEntryLines",
                column: "SupplierId");

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000101', 'accounts.coa.view', 'View chart of accounts', 'accounting', now(), now()),
                    ('20000000-0000-0000-0000-000000000102', 'accounts.coa.manage', 'Manage chart of accounts and account mappings', 'accounting', now(), now()),
                    ('20000000-0000-0000-0000-000000000103', 'accounts.journal.view', 'View journal entries, general ledger, and trial balance', 'accounting', now(), now()),
                    ('20000000-0000-0000-0000-000000000104', 'accounts.journal.post', 'Post manual journal vouchers', 'accounting', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000327'::uuid, 'Owner', 'accounts.coa.view'),
                    ('30000000-0000-0000-0000-000000000328'::uuid, 'Owner', 'accounts.coa.manage'),
                    ('30000000-0000-0000-0000-000000000329'::uuid, 'Owner', 'accounts.journal.view'),
                    ('30000000-0000-0000-0000-000000000330'::uuid, 'Owner', 'accounts.journal.post'),
                    ('30000000-0000-0000-0000-000000000331'::uuid, 'Manager', 'accounts.coa.view'),
                    ('30000000-0000-0000-0000-000000000332'::uuid, 'Manager', 'accounts.coa.manage'),
                    ('30000000-0000-0000-0000-000000000333'::uuid, 'Manager', 'accounts.journal.view'),
                    ('30000000-0000-0000-0000-000000000334'::uuid, 'Manager', 'accounts.journal.post'),
                    ('30000000-0000-0000-0000-000000000335'::uuid, 'Accountant', 'accounts.coa.view'),
                    ('30000000-0000-0000-0000-000000000336'::uuid, 'Accountant', 'accounts.coa.manage'),
                    ('30000000-0000-0000-0000-000000000337'::uuid, 'Accountant', 'accounts.journal.view'),
                    ('30000000-0000-0000-0000-000000000338'::uuid, 'Accountant', 'accounts.journal.post')
                ) AS mapping(id, role_name, permission_code)
                JOIN "Roles" role ON role."Name" = mapping.role_name
                JOIN "Permissions" permission ON permission."Code" = mapping.permission_code
                ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;

                INSERT INTO "ChartOfAccounts" ("Id", "Code", "NormalizedCode", "Name", "ParentAccountId", "AccountType", "NormalBalance", "IsPostingAccount", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                    ('40000000-0000-0000-0000-000000000010', '1000', '1000', 'Assets', NULL, 1, 1, false, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000011', '1010', '1010', 'Cash', '40000000-0000-0000-0000-000000000010', 1, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000012', '1020', '1020', 'Bank', '40000000-0000-0000-0000-000000000010', 1, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000013', '1030', '1030', 'Accounts Receivable', '40000000-0000-0000-0000-000000000010', 1, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000014', '1040', '1040', 'Inventory', '40000000-0000-0000-0000-000000000010', 1, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000015', '1090', '1090', 'Other Assets', '40000000-0000-0000-0000-000000000010', 1, 1, true, true, now(), now()),

                    ('40000000-0000-0000-0000-000000000020', '2000', '2000', 'Liabilities', NULL, 2, 2, false, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000021', '2010', '2010', 'Accounts Payable', '40000000-0000-0000-0000-000000000020', 2, 2, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000022', '2020', '2020', 'Taxes Payable', '40000000-0000-0000-0000-000000000020', 2, 2, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000023', '2090', '2090', 'Other Liabilities', '40000000-0000-0000-0000-000000000020', 2, 2, true, true, now(), now()),

                    ('40000000-0000-0000-0000-000000000030', '3000', '3000', 'Equity', NULL, 3, 2, false, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000031', '3010', '3010', 'Owner Capital', '40000000-0000-0000-0000-000000000030', 3, 2, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000032', '3020', '3020', 'Retained Earnings', '40000000-0000-0000-0000-000000000030', 3, 2, true, true, now(), now()),

                    ('40000000-0000-0000-0000-000000000040', '4000', '4000', 'Income', NULL, 4, 2, false, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000041', '4010', '4010', 'Sales Revenue', '40000000-0000-0000-0000-000000000040', 4, 2, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000042', '4020', '4020', 'Sales Returns and Allowances', '40000000-0000-0000-0000-000000000040', 4, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000043', '4090', '4090', 'Other Income', '40000000-0000-0000-0000-000000000040', 4, 2, true, true, now(), now()),

                    ('40000000-0000-0000-0000-000000000050', '5000', '5000', 'Cost of Sales', NULL, 5, 1, false, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000051', '5010', '5010', 'Cost of Goods Sold', '40000000-0000-0000-0000-000000000050', 5, 1, true, true, now(), now()),

                    ('40000000-0000-0000-0000-000000000060', '6000', '6000', 'Expenses', NULL, 6, 1, false, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000061', '6010', '6010', 'Rent', '40000000-0000-0000-0000-000000000060', 6, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000062', '6020', '6020', 'Utilities', '40000000-0000-0000-0000-000000000060', 6, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000063', '6030', '6030', 'Salaries', '40000000-0000-0000-0000-000000000060', 6, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000064', '6040', '6040', 'Delivery', '40000000-0000-0000-0000-000000000060', 6, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000065', '6050', '6050', 'Repairs', '40000000-0000-0000-0000-000000000060', 6, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000066', '6060', '6060', 'Office Expense', '40000000-0000-0000-0000-000000000060', 6, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000067', '6070', '6070', 'Inventory Loss (Damage/Expiry)', '40000000-0000-0000-0000-000000000060', 6, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000068', '6990', '6990', 'Miscellaneous Expense', '40000000-0000-0000-0000-000000000060', 6, 1, true, true, now(), now())
                ON CONFLICT ("NormalizedCode") DO NOTHING;

                INSERT INTO "AccountMappings" ("Id", "MappingKey", "ChartOfAccountId", "CreatedAt", "UpdatedAt")
                VALUES
                    ('50000000-0000-0000-0000-000000000001', 1, '40000000-0000-0000-0000-000000000011', now(), now()),
                    ('50000000-0000-0000-0000-000000000002', 2, '40000000-0000-0000-0000-000000000012', now(), now()),
                    ('50000000-0000-0000-0000-000000000003', 3, '40000000-0000-0000-0000-000000000013', now(), now()),
                    ('50000000-0000-0000-0000-000000000004', 4, '40000000-0000-0000-0000-000000000021', now(), now()),
                    ('50000000-0000-0000-0000-000000000005', 5, '40000000-0000-0000-0000-000000000014', now(), now()),
                    ('50000000-0000-0000-0000-000000000006', 6, '40000000-0000-0000-0000-000000000041', now(), now()),
                    ('50000000-0000-0000-0000-000000000007', 7, '40000000-0000-0000-0000-000000000042', now(), now()),
                    ('50000000-0000-0000-0000-000000000008', 8, '40000000-0000-0000-0000-000000000051', now(), now()),
                    ('50000000-0000-0000-0000-000000000009', 9, '40000000-0000-0000-0000-000000000067', now(), now()),
                    ('50000000-0000-0000-0000-000000000010', 10, '40000000-0000-0000-0000-000000000068', now(), now()),
                    ('50000000-0000-0000-0000-000000000011', 11, '40000000-0000-0000-0000-000000000043', now(), now()),
                    ('50000000-0000-0000-0000-000000000012', 12, '40000000-0000-0000-0000-000000000032', now(), now())
                ON CONFLICT ("MappingKey") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions" WHERE "PermissionId" IN
                    (SELECT "Id" FROM "Permissions" WHERE "Code" IN
                        ('accounts.coa.view', 'accounts.coa.manage', 'accounts.journal.view', 'accounts.journal.post'));
                DELETE FROM "Permissions" WHERE "Code" IN
                    ('accounts.coa.view', 'accounts.coa.manage', 'accounts.journal.view', 'accounts.journal.post');
                """);

            migrationBuilder.DropTable(
                name: "AccountMappings");

            migrationBuilder.DropTable(
                name: "JournalEntryLines");

            migrationBuilder.DropTable(
                name: "ChartOfAccounts");

            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropSequence(
                name: "JournalEntryNumberSequence");
        }
    }
}
