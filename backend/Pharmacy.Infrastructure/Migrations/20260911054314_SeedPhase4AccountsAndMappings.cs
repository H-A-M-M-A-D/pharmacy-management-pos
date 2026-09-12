using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedPhase4AccountsAndMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH new_accounts AS (
                    INSERT INTO "ChartOfAccounts" ("Id", "Code", "NormalizedCode", "Name", "ParentAccountId", "AccountType", "NormalBalance", "IsPostingAccount", "IsActive", "CreatedAt", "UpdatedAt")
                    VALUES
                        (gen_random_uuid(), '6090', '6090', 'Bad Debt Expense', '40000000-0000-0000-0000-000000000060', 6, 1, true, true, now(), now()),
                        (gen_random_uuid(), '4092', '4092', 'Payables Write-off Income', '40000000-0000-0000-0000-000000000040', 4, 2, true, true, now(), now()),
                        (gen_random_uuid(), '2092', '2092', 'Customer Advances', '40000000-0000-0000-0000-000000000020', 2, 2, true, true, now(), now()),
                        (gen_random_uuid(), '1094', '1094', 'Supplier Advances', '40000000-0000-0000-0000-000000000010', 1, 1, true, true, now(), now())
                    ON CONFLICT ("NormalizedCode") DO NOTHING
                    RETURNING "Id", "NormalizedCode"
                )
                INSERT INTO "AccountMappings" ("Id", "MappingKey", "ChartOfAccountId", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), mapping.key, new_accounts."Id", now(), now()
                FROM new_accounts
                JOIN (VALUES ('6090', 19), ('4092', 20), ('2092', 21), ('1094', 22)) AS mapping(code, key) ON mapping.code = new_accounts."NormalizedCode"
                ON CONFLICT ("MappingKey") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "AccountMappings" WHERE "MappingKey" IN (19, 20, 21, 22);
                DELETE FROM "ChartOfAccounts" WHERE "NormalizedCode" IN ('6090', '4092', '2092', '1094');
                """);
        }
    }
}
