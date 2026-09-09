using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteAdjustmentAndCashControlAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountMappings_MappingKey",
                table: "AccountMappings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountMappings_MappingKey",
                table: "AccountMappings",
                sql: "\"MappingKey\" BETWEEN 1 AND 18");

            migrationBuilder.Sql("""
                INSERT INTO "ChartOfAccounts" ("Id", "Code", "NormalizedCode", "Name", "ParentAccountId", "AccountType", "NormalBalance", "IsPostingAccount", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                    ('40000000-0000-0000-0000-000000000016', '1091', '1091', 'Accounts Receivable Adjustment Suspense', '40000000-0000-0000-0000-000000000010', 1, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000017', '1092', '1092', 'Cash and Bank Adjustment Suspense', '40000000-0000-0000-0000-000000000010', 1, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000018', '1093', '1093', 'Drawer Clearing', '40000000-0000-0000-0000-000000000010', 1, 1, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000024', '2091', '2091', 'Accounts Payable Adjustment Suspense', '40000000-0000-0000-0000-000000000020', 2, 2, true, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000069', '6080', '6080', 'Cash Over and Short', '40000000-0000-0000-0000-000000000060', 6, 1, true, true, now(), now())
                ON CONFLICT ("NormalizedCode") DO NOTHING;

                INSERT INTO "AccountMappings" ("Id", "MappingKey", "ChartOfAccountId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, mapping.mapping_key, account."Id", now(), now()
                FROM (VALUES
                    ('50000000-0000-0000-0000-000000000014'::uuid, 14, '1091'),
                    ('50000000-0000-0000-0000-000000000015'::uuid, 15, '2091'),
                    ('50000000-0000-0000-0000-000000000016'::uuid, 16, '1092'),
                    ('50000000-0000-0000-0000-000000000017'::uuid, 17, '1093'),
                    ('50000000-0000-0000-0000-000000000018'::uuid, 18, '6080')
                ) AS mapping(id, mapping_key, account_code)
                JOIN "ChartOfAccounts" account ON account."NormalizedCode" = mapping.account_code
                ON CONFLICT ("MappingKey") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "AccountMappings" WHERE "MappingKey" BETWEEN 14 AND 18;
                DELETE FROM "ChartOfAccounts" WHERE "NormalizedCode" IN ('1091', '1092', '1093', '2091', '6080');
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountMappings_MappingKey",
                table: "AccountMappings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountMappings_MappingKey",
                table: "AccountMappings",
                sql: "\"MappingKey\" BETWEEN 1 AND 13");
        }
    }
}
