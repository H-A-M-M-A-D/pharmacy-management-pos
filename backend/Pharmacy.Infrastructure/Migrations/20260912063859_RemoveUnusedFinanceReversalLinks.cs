using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    public partial class RemoveUnusedFinanceReversalLinks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "Expenses", "OtherIncomes" })
            {
                migrationBuilder.DropForeignKey(name: $"FK_{table}_JournalEntries_ReversalJournalEntryId", table: table);
                migrationBuilder.DropIndex(name: $"IX_{table}_ReversalJournalEntryId", table: table);
                migrationBuilder.DropColumn(name: "ReversalJournalEntryId", table: table);
            }
            migrationBuilder.DropIndex(name: "IX_Vouchers_ReversalOfVoucherId", table: "Vouchers");
            migrationBuilder.CreateIndex(name: "IX_Vouchers_ReversalOfVoucherId", table: "Vouchers", column: "ReversalOfVoucherId", unique: true, filter: "\"ReversalOfVoucherId\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Vouchers_ReversalOfVoucherId", table: "Vouchers");
            migrationBuilder.CreateIndex(name: "IX_Vouchers_ReversalOfVoucherId", table: "Vouchers", column: "ReversalOfVoucherId");
            foreach (var table in new[] { "Expenses", "OtherIncomes" })
            {
                migrationBuilder.AddColumn<Guid>(name: "ReversalJournalEntryId", table: table, type: "uuid", nullable: true);
                migrationBuilder.CreateIndex(name: $"IX_{table}_ReversalJournalEntryId", table: table, column: "ReversalJournalEntryId", unique: true, filter: "\"ReversalJournalEntryId\" IS NOT NULL");
                migrationBuilder.AddForeignKey(name: $"FK_{table}_JournalEntries_ReversalJournalEntryId", table: table, column: "ReversalJournalEntryId", principalTable: "JournalEntries", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }
        }
    }
}
