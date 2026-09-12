using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pharmacy.Infrastructure.Data;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceCostCenters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "Expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "OtherIncomes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CostCenterId",
                table: "Expenses",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomes_CostCenterId",
                table: "OtherIncomes",
                column: "CostCenterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_CostCenters_CostCenterId",
                table: "Expenses",
                column: "CostCenterId",
                principalTable: "CostCenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OtherIncomes_CostCenters_CostCenterId",
                table: "OtherIncomes",
                column: "CostCenterId",
                principalTable: "CostCenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_CostCenters_CostCenterId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_OtherIncomes_CostCenters_CostCenterId",
                table: "OtherIncomes");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_CostCenterId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_OtherIncomes_CostCenterId",
                table: "OtherIncomes");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "OtherIncomes");
        }
    }
}