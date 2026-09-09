using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAdjustmentGainMapping : Migration
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
                sql: "\"MappingKey\" BETWEEN 1 AND 13");

            migrationBuilder.Sql("""
                INSERT INTO "ChartOfAccounts" ("Id", "Code", "NormalizedCode", "Name", "ParentAccountId", "AccountType", "NormalBalance", "IsPostingAccount", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                    ('40000000-0000-0000-0000-000000000044', '4091', '4091', 'Inventory Adjustment Gain', '40000000-0000-0000-0000-000000000040', 4, 2, true, true, now(), now())
                ON CONFLICT ("NormalizedCode") DO NOTHING;

                INSERT INTO "AccountMappings" ("Id", "MappingKey", "ChartOfAccountId", "CreatedAt", "UpdatedAt")
                VALUES
                    ('50000000-0000-0000-0000-000000000013', 13, '40000000-0000-0000-0000-000000000044', now(), now())
                ON CONFLICT ("MappingKey") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "AccountMappings" WHERE "MappingKey" = 13;
                DELETE FROM "ChartOfAccounts" WHERE "NormalizedCode" = '4091';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountMappings_MappingKey",
                table: "AccountMappings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountMappings_MappingKey",
                table: "AccountMappings",
                sql: "\"MappingKey\" BETWEEN 1 AND 12");
        }
    }
}
