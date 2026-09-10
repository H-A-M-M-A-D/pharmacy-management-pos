using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteStockTransfersAndGodownScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "StockTransferNumberSequence");

            migrationBuilder.AddColumn<Guid>(
                name: "GodownId",
                table: "StockCountSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceGodownId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationGodownId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DispatchedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers", x => x.Id);
                    table.CheckConstraint("CK_StockTransfers_Source_Destination_Godown_Different", "\"SourceGodownId\" <> \"DestinationGodownId\"");
                    table.CheckConstraint("CK_StockTransfers_Status", "\"Status\" IN (1, 2, 3, 4, 5, 6, 7)");
                    table.ForeignKey(
                        name: "FK_StockTransfers_Branches_DestinationBranchId",
                        column: x => x.DestinationBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Branches_SourceBranchId",
                        column: x => x.SourceBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Godowns_DestinationGodownId",
                        column: x => x.DestinationGodownId,
                        principalTable: "Godowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Godowns_SourceGodownId",
                        column: x => x.SourceGodownId,
                        principalTable: "Godowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Users_DispatchedByUserId",
                        column: x => x.DispatchedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Users_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockTransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceProductBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationProductBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UnitCostSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityRequested = table.Column<int>(type: "integer", nullable: false),
                    QuantityApproved = table.Column<int>(type: "integer", nullable: false),
                    QuantityDispatched = table.Column<int>(type: "integer", nullable: false),
                    QuantityReceived = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferItems", x => x.Id);
                    table.CheckConstraint("CK_StockTransferItems_Approved_Range", "\"QuantityApproved\" >= 0 AND \"QuantityApproved\" <= \"QuantityRequested\"");
                    table.CheckConstraint("CK_StockTransferItems_Dispatched_Range", "\"QuantityDispatched\" >= 0 AND \"QuantityDispatched\" <= \"QuantityApproved\"");
                    table.CheckConstraint("CK_StockTransferItems_Received_Range", "\"QuantityReceived\" >= 0 AND \"QuantityReceived\" <= \"QuantityDispatched\"");
                    table.CheckConstraint("CK_StockTransferItems_Requested_Positive", "\"QuantityRequested\" > 0");
                    table.CheckConstraint("CK_StockTransferItems_UnitCost_NonNegative", "\"UnitCostSnapshot\" >= 0");
                    table.ForeignKey(
                        name: "FK_StockTransferItems_ProductBatches_DestinationProductBatchId",
                        column: x => x.DestinationProductBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferItems_ProductBatches_SourceProductBatchId",
                        column: x => x.SourceProductBatchId,
                        principalTable: "ProductBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferItems_StockTransfers_StockTransferId",
                        column: x => x.StockTransferId,
                        principalTable: "StockTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockCountSessions_GodownId_CountDate",
                table: "StockCountSessions",
                columns: new[] { "GodownId", "CountDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_DestinationProductBatchId",
                table: "StockTransferItems",
                column: "DestinationProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_ProductId",
                table: "StockTransferItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_SourceProductBatchId",
                table: "StockTransferItems",
                column: "SourceProductBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_StockTransferId",
                table: "StockTransferItems",
                column: "StockTransferId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferItems_StockTransferId_SourceProductBatchId",
                table: "StockTransferItems",
                columns: new[] { "StockTransferId", "SourceProductBatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_ApprovedByUserId",
                table: "StockTransfers",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CancelledByUserId",
                table: "StockTransfers",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_CreatedByUserId",
                table: "StockTransfers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DestinationBranchId_TransferDate",
                table: "StockTransfers",
                columns: new[] { "DestinationBranchId", "TransferDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DestinationGodownId_TransferDate",
                table: "StockTransfers",
                columns: new[] { "DestinationGodownId", "TransferDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_DispatchedByUserId",
                table: "StockTransfers",
                column: "DispatchedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_ReceivedByUserId",
                table: "StockTransfers",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_RequestedByUserId",
                table: "StockTransfers",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_SourceBranchId_TransferDate",
                table: "StockTransfers",
                columns: new[] { "SourceBranchId", "TransferDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_SourceGodownId_TransferDate",
                table: "StockTransfers",
                columns: new[] { "SourceGodownId", "TransferDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_Status",
                table: "StockTransfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_TransferNumber",
                table: "StockTransfers",
                column: "TransferNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StockCountSessions_Godowns_GodownId",
                table: "StockCountSessions",
                column: "GodownId",
                principalTable: "Godowns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt") VALUES
                (gen_random_uuid(), 'stock_transfers.view', 'View inter-godown stock transfers', 'stock_transfers', now(), now()),
                (gen_random_uuid(), 'stock_transfers.create', 'Create and edit draft stock transfers', 'stock_transfers', now(), now()),
                (gen_random_uuid(), 'stock_transfers.request', 'Submit a draft stock transfer for approval', 'stock_transfers', now(), now()),
                (gen_random_uuid(), 'stock_transfers.approve', 'Approve a requested stock transfer', 'stock_transfers', now(), now()),
                (gen_random_uuid(), 'stock_transfers.dispatch', 'Dispatch stock out of the source godown', 'stock_transfers', now(), now()),
                (gen_random_uuid(), 'stock_transfers.receive', 'Receive stock into the destination godown', 'stock_transfers', now(), now()),
                (gen_random_uuid(), 'stock_transfers.cancel', 'Cancel a transfer or resolve/write off a transfer discrepancy', 'stock_transfers', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('Owner', 'stock_transfers.view'), ('Owner', 'stock_transfers.create'), ('Owner', 'stock_transfers.request'),
                    ('Owner', 'stock_transfers.approve'), ('Owner', 'stock_transfers.dispatch'), ('Owner', 'stock_transfers.receive'), ('Owner', 'stock_transfers.cancel'),
                    ('Manager', 'stock_transfers.view'), ('Manager', 'stock_transfers.create'), ('Manager', 'stock_transfers.request'),
                    ('Manager', 'stock_transfers.approve'), ('Manager', 'stock_transfers.dispatch'), ('Manager', 'stock_transfers.receive'), ('Manager', 'stock_transfers.cancel'),
                    ('StoreKeeper', 'stock_transfers.view'), ('StoreKeeper', 'stock_transfers.create'), ('StoreKeeper', 'stock_transfers.request'),
                    ('StoreKeeper', 'stock_transfers.dispatch'), ('StoreKeeper', 'stock_transfers.receive'),
                    ('PurchaseManager', 'stock_transfers.view'),
                    ('Pharmacist', 'stock_transfers.view'),
                    ('Accountant', 'stock_transfers.view'),
                    ('Cashier', 'stock_transfers.view')
                ) AS mapping(role_name, permission_code)
                JOIN "Roles" role ON role."Name" = mapping.role_name
                JOIN "Permissions" permission ON permission."Code" = mapping.permission_code
                ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions" WHERE "PermissionId" IN
                    (SELECT "Id" FROM "Permissions" WHERE "Code" LIKE 'stock_transfers.%');
                DELETE FROM "Permissions" WHERE "Code" LIKE 'stock_transfers.%';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_StockCountSessions_Godowns_GodownId",
                table: "StockCountSessions");

            migrationBuilder.DropTable(
                name: "StockTransferItems");

            migrationBuilder.DropTable(
                name: "StockTransfers");

            migrationBuilder.DropIndex(
                name: "IX_StockCountSessions_GodownId_CountDate",
                table: "StockCountSessions");

            migrationBuilder.DropColumn(
                name: "GodownId",
                table: "StockCountSessions");

            migrationBuilder.DropSequence(
                name: "StockTransferNumberSequence");
        }
    }
}
