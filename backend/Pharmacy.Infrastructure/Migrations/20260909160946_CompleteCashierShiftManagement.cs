using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCashierShiftManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashierShifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TerminalName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OpeningCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpeningNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpectedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ActualCountedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CashVariance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CustomerCashReceivedSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CashPaidOutSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ClosingNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReconciledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReconciledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReconciliationNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierShifts", x => x.Id);
                    table.CheckConstraint("CK_CashierShifts_ActualCountedCash_NonNegative", "\"ActualCountedCash\" IS NULL OR \"ActualCountedCash\" >= 0");
                    table.CheckConstraint("CK_CashierShifts_Closed_Fields", "(\"Status\" = 1 AND \"ClosedAtUtc\" IS NULL) OR (\"Status\" <> 1 AND \"ClosedAtUtc\" IS NOT NULL AND \"ExpectedCash\" IS NOT NULL AND \"ActualCountedCash\" IS NOT NULL)");
                    table.CheckConstraint("CK_CashierShifts_OpeningCash_NonNegative", "\"OpeningCash\" >= 0");
                    table.CheckConstraint("CK_CashierShifts_Reconciled_Fields", "\"Status\" <> 3 OR (\"ReconciledByUserId\" IS NOT NULL AND \"ReconciledAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_CashierShifts_Status", "\"Status\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_CashierShifts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierShifts_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashierShifts_Users_ReconciledByUserId",
                        column: x => x.ReconciledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashierShiftDrawerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierShiftDrawerEntries", x => x.Id);
                    table.CheckConstraint("CK_CashierShiftDrawerEntries_Amount_Positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_CashierShiftDrawerEntries_CashierShifts_CashierShiftId",
                        column: x => x.CashierShiftId,
                        principalTable: "CashierShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CashierShiftDrawerEntries_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashierShiftPaymentSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    SalesAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundsAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierShiftPaymentSummaries", x => x.Id);
                    table.CheckConstraint("CK_CashierShiftPaymentSummaries_NonNegative", "\"SalesAmount\" >= 0 AND \"RefundsAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_CashierShiftPaymentSummaries_CashierShifts_CashierShiftId",
                        column: x => x.CashierShiftId,
                        principalTable: "CashierShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashierShiftDrawerEntries_CashierShiftId",
                table: "CashierShiftDrawerEntries",
                column: "CashierShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShiftDrawerEntries_CreatedByUserId",
                table: "CashierShiftDrawerEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShiftPaymentSummaries_CashierShiftId_PaymentMethod",
                table: "CashierShiftPaymentSummaries",
                columns: new[] { "CashierShiftId", "PaymentMethod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_BranchId_ClosedAtUtc",
                table: "CashierShifts",
                columns: new[] { "BranchId", "ClosedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_BranchId_OpenedAtUtc",
                table: "CashierShifts",
                columns: new[] { "BranchId", "OpenedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_CashierUserId_Status",
                table: "CashierShifts",
                columns: new[] { "CashierUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_ReconciledByUserId",
                table: "CashierShifts",
                column: "ReconciledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_Status",
                table: "CashierShifts",
                column: "Status");

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000095', 'cashier_shift.open', 'Open a cashier shift', 'cashier_shift', now(), now()),
                    ('20000000-0000-0000-0000-000000000096', 'cashier_shift.view', 'View cashier shifts and closing reports', 'cashier_shift', now(), now()),
                    ('20000000-0000-0000-0000-000000000097', 'cashier_shift.close', 'Close own cashier shift', 'cashier_shift', now(), now()),
                    ('20000000-0000-0000-0000-000000000098', 'cashier_shift.close_any', 'Close any cashier''s shift', 'cashier_shift', now(), now()),
                    ('20000000-0000-0000-0000-000000000099', 'cashier_shift.reconcile', 'Reconcile a closed cashier shift', 'cashier_shift', now(), now()),
                    ('20000000-0000-0000-0000-000000000100', 'cashier_shift.drawer_adjust', 'Record manual cash drawer entries', 'cashier_shift', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000306'::uuid, 'Owner', 'cashier_shift.open'),
                    ('30000000-0000-0000-0000-000000000307'::uuid, 'Owner', 'cashier_shift.view'),
                    ('30000000-0000-0000-0000-000000000308'::uuid, 'Owner', 'cashier_shift.close'),
                    ('30000000-0000-0000-0000-000000000309'::uuid, 'Owner', 'cashier_shift.close_any'),
                    ('30000000-0000-0000-0000-000000000310'::uuid, 'Owner', 'cashier_shift.reconcile'),
                    ('30000000-0000-0000-0000-000000000311'::uuid, 'Owner', 'cashier_shift.drawer_adjust'),
                    ('30000000-0000-0000-0000-000000000312'::uuid, 'Manager', 'cashier_shift.open'),
                    ('30000000-0000-0000-0000-000000000313'::uuid, 'Manager', 'cashier_shift.view'),
                    ('30000000-0000-0000-0000-000000000314'::uuid, 'Manager', 'cashier_shift.close'),
                    ('30000000-0000-0000-0000-000000000315'::uuid, 'Manager', 'cashier_shift.close_any'),
                    ('30000000-0000-0000-0000-000000000316'::uuid, 'Manager', 'cashier_shift.reconcile'),
                    ('30000000-0000-0000-0000-000000000317'::uuid, 'Manager', 'cashier_shift.drawer_adjust'),
                    ('30000000-0000-0000-0000-000000000318'::uuid, 'Cashier', 'cashier_shift.open'),
                    ('30000000-0000-0000-0000-000000000319'::uuid, 'Cashier', 'cashier_shift.view'),
                    ('30000000-0000-0000-0000-000000000320'::uuid, 'Cashier', 'cashier_shift.close'),
                    ('30000000-0000-0000-0000-000000000321'::uuid, 'Cashier', 'cashier_shift.drawer_adjust'),
                    ('30000000-0000-0000-0000-000000000322'::uuid, 'Pharmacist', 'cashier_shift.open'),
                    ('30000000-0000-0000-0000-000000000323'::uuid, 'Pharmacist', 'cashier_shift.view'),
                    ('30000000-0000-0000-0000-000000000324'::uuid, 'Pharmacist', 'cashier_shift.close'),
                    ('30000000-0000-0000-0000-000000000325'::uuid, 'Pharmacist', 'cashier_shift.drawer_adjust'),
                    ('30000000-0000-0000-0000-000000000326'::uuid, 'Accountant', 'cashier_shift.view')
                ) AS mapping(id, role_name, permission_code)
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
                    (SELECT "Id" FROM "Permissions" WHERE "Code" IN
                        ('cashier_shift.open', 'cashier_shift.view', 'cashier_shift.close',
                         'cashier_shift.close_any', 'cashier_shift.reconcile', 'cashier_shift.drawer_adjust'));
                DELETE FROM "Permissions" WHERE "Code" IN
                    ('cashier_shift.open', 'cashier_shift.view', 'cashier_shift.close',
                     'cashier_shift.close_any', 'cashier_shift.reconcile', 'cashier_shift.drawer_adjust');
                """);

            migrationBuilder.DropTable(
                name: "CashierShiftDrawerEntries");

            migrationBuilder.DropTable(
                name: "CashierShiftPaymentSummaries");

            migrationBuilder.DropTable(
                name: "CashierShifts");
        }
    }
}
