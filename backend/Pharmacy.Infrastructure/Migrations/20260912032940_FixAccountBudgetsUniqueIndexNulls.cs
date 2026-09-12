using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixAccountBudgetsUniqueIndexNulls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountBudgets_FiscalYear_PeriodNumber_ChartOfAccountId_Bra~",
                table: "AccountBudgets");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBudgets_FiscalYear_PeriodNumber_ChartOfAccountId_Bra~",
                table: "AccountBudgets",
                columns: new[] { "FiscalYear", "PeriodNumber", "ChartOfAccountId", "BranchId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountBudgets_FiscalYear_PeriodNumber_ChartOfAccountId_Bra~",
                table: "AccountBudgets");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBudgets_FiscalYear_PeriodNumber_ChartOfAccountId_Bra~",
                table: "AccountBudgets",
                columns: new[] { "FiscalYear", "PeriodNumber", "ChartOfAccountId", "BranchId" },
                unique: true);
        }
    }
}
