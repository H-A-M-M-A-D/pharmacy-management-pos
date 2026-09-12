using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowCreditNoteWriteOffAdvancePaymentMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditNotes_JournalEntries_JournalEntryId",
                table: "CreditNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAdvanceApplications_JournalEntries_JournalEntryId",
                table: "CustomerAdvanceApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAdvances_JournalEntries_JournalEntryId",
                table: "CustomerAdvances");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerWriteOffs_JournalEntries_JournalEntryId",
                table: "CustomerWriteOffs");

            migrationBuilder.DropForeignKey(
                name: "FK_DebitNotes_JournalEntries_JournalEntryId",
                table: "DebitNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierAdvanceApplications_JournalEntries_JournalEntryId",
                table: "SupplierAdvanceApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierAdvances_JournalEntries_JournalEntryId",
                table: "SupplierAdvances");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierWriteOffs_JournalEntries_JournalEntryId",
                table: "SupplierWriteOffs");

            migrationBuilder.DropIndex(
                name: "IX_SupplierWriteOffs_JournalEntryId",
                table: "SupplierWriteOffs");

            migrationBuilder.DropIndex(
                name: "IX_SupplierAdvances_JournalEntryId",
                table: "SupplierAdvances");

            migrationBuilder.DropIndex(
                name: "IX_SupplierAdvanceApplications_JournalEntryId",
                table: "SupplierAdvanceApplications");

            migrationBuilder.DropIndex(
                name: "IX_DebitNotes_JournalEntryId",
                table: "DebitNotes");

            migrationBuilder.DropIndex(
                name: "IX_CustomerWriteOffs_JournalEntryId",
                table: "CustomerWriteOffs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerPayments_Method",
                table: "CustomerPayments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAdvances_JournalEntryId",
                table: "CustomerAdvances");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAdvanceApplications_JournalEntryId",
                table: "CustomerAdvanceApplications");

            migrationBuilder.DropIndex(
                name: "IX_CreditNotes_JournalEntryId",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "SupplierWriteOffs");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "SupplierAdvances");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "SupplierAdvanceApplications");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "DebitNotes");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "CustomerWriteOffs");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "CustomerAdvances");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "CustomerAdvanceApplications");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "CreditNotes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerPayments_Method",
                table: "CustomerPayments",
                sql: "\"PaymentMethod\" IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerPayments_Method",
                table: "CustomerPayments");

            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "SupplierWriteOffs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "SupplierAdvances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "SupplierAdvanceApplications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "DebitNotes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "CustomerWriteOffs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "CustomerAdvances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "CustomerAdvanceApplications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "CreditNotes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_SupplierWriteOffs_JournalEntryId",
                table: "SupplierWriteOffs",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdvances_JournalEntryId",
                table: "SupplierAdvances",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdvanceApplications_JournalEntryId",
                table: "SupplierAdvanceApplications",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_JournalEntryId",
                table: "DebitNotes",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWriteOffs_JournalEntryId",
                table: "CustomerWriteOffs",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerPayments_Method",
                table: "CustomerPayments",
                sql: "\"PaymentMethod\" IN (1, 2, 3, 4, 5, 6, 7)");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdvances_JournalEntryId",
                table: "CustomerAdvances",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdvanceApplications_JournalEntryId",
                table: "CustomerAdvanceApplications",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_JournalEntryId",
                table: "CreditNotes",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditNotes_JournalEntries_JournalEntryId",
                table: "CreditNotes",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAdvanceApplications_JournalEntries_JournalEntryId",
                table: "CustomerAdvanceApplications",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAdvances_JournalEntries_JournalEntryId",
                table: "CustomerAdvances",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerWriteOffs_JournalEntries_JournalEntryId",
                table: "CustomerWriteOffs",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DebitNotes_JournalEntries_JournalEntryId",
                table: "DebitNotes",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierAdvanceApplications_JournalEntries_JournalEntryId",
                table: "SupplierAdvanceApplications",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierAdvances_JournalEntries_JournalEntryId",
                table: "SupplierAdvances",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierWriteOffs_JournalEntries_JournalEntryId",
                table: "SupplierWriteOffs",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
