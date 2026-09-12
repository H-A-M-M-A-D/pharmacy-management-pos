using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashFlowClassificationAndExpenseReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReversalJournalEntryId",
                table: "OtherIncomes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "OtherIncomes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAtUtc",
                table: "OtherIncomes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversedByUserId",
                table: "OtherIncomes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalJournalEntryId",
                table: "Expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "Expenses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAtUtc",
                table: "Expenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversedByUserId",
                table: "Expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CashFlowClassification",
                table: "ChartOfAccounts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomes_ReversalJournalEntryId",
                table: "OtherIncomes",
                column: "ReversalJournalEntryId",
                unique: true,
                filter: "\"ReversalJournalEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomes_ReversedByUserId",
                table: "OtherIncomes",
                column: "ReversedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ReversalJournalEntryId",
                table: "Expenses",
                column: "ReversalJournalEntryId",
                unique: true,
                filter: "\"ReversalJournalEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ReversedByUserId",
                table: "Expenses",
                column: "ReversedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChartOfAccounts_CashFlowClassification",
                table: "ChartOfAccounts",
                sql: "\"CashFlowClassification\" IS NULL OR \"CashFlowClassification\" BETWEEN 1 AND 3");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_JournalEntries_ReversalJournalEntryId",
                table: "Expenses",
                column: "ReversalJournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Users_ReversedByUserId",
                table: "Expenses",
                column: "ReversedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OtherIncomes_JournalEntries_ReversalJournalEntryId",
                table: "OtherIncomes",
                column: "ReversalJournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OtherIncomes_Users_ReversedByUserId",
                table: "OtherIncomes",
                column: "ReversedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_JournalEntries_ReversalJournalEntryId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Users_ReversedByUserId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_OtherIncomes_JournalEntries_ReversalJournalEntryId",
                table: "OtherIncomes");

            migrationBuilder.DropForeignKey(
                name: "FK_OtherIncomes_Users_ReversedByUserId",
                table: "OtherIncomes");

            migrationBuilder.DropIndex(
                name: "IX_OtherIncomes_ReversalJournalEntryId",
                table: "OtherIncomes");

            migrationBuilder.DropIndex(
                name: "IX_OtherIncomes_ReversedByUserId",
                table: "OtherIncomes");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_ReversalJournalEntryId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_ReversedByUserId",
                table: "Expenses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChartOfAccounts_CashFlowClassification",
                table: "ChartOfAccounts");

            migrationBuilder.DropColumn(
                name: "ReversalJournalEntryId",
                table: "OtherIncomes");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "OtherIncomes");

            migrationBuilder.DropColumn(
                name: "ReversedAtUtc",
                table: "OtherIncomes");

            migrationBuilder.DropColumn(
                name: "ReversedByUserId",
                table: "OtherIncomes");

            migrationBuilder.DropColumn(
                name: "ReversalJournalEntryId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ReversedAtUtc",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ReversedByUserId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CashFlowClassification",
                table: "ChartOfAccounts");
        }
    }
}
