using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase6ReorderDraftMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GodownId",
                table: "PurchaseOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SuggestedOrderQuantity",
                table: "PurchaseOrderItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_GodownId",
                table: "PurchaseOrders",
                column: "GodownId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseOrderItems_SuggestedQuantity",
                table: "PurchaseOrderItems",
                sql: "\"SuggestedOrderQuantity\" IS NULL OR \"SuggestedOrderQuantity\" > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Godowns_GodownId",
                table: "PurchaseOrders",
                column: "GodownId",
                principalTable: "Godowns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Godowns_GodownId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_GodownId",
                table: "PurchaseOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseOrderItems_SuggestedQuantity",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "GodownId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SuggestedOrderQuantity",
                table: "PurchaseOrderItems");
        }
    }
}
