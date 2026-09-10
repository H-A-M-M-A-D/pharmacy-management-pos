using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteAgingVouchersAndPaymentAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "BankPaymentVoucherNumberSequence");

            migrationBuilder.CreateSequence(
                name: "BankReceiptVoucherNumberSequence");

            migrationBuilder.CreateSequence(
                name: "CashPaymentVoucherNumberSequence");

            migrationBuilder.CreateSequence(
                name: "CashReceiptVoucherNumberSequence");

            migrationBuilder.CreateSequence(
                name: "ContraVoucherNumberSequence");

            migrationBuilder.CreateSequence(
                name: "JournalVoucherNumberSequence");

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDateUtc",
                table: "Sales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "GoodsReceipts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreditDays",
                table: "Customers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerPaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AllocatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPaymentAllocations", x => x.Id);
                    table.CheckConstraint("CK_CustomerPaymentAllocations_Amount_Positive", "\"AllocatedAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_CustomerPaymentAllocations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentAllocations_CustomerPayments_CustomerPayment~",
                        column: x => x.CustomerPaymentId,
                        principalTable: "CustomerPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentAllocations_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentAllocations_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPaymentAllocations_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierPaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AllocatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPaymentAllocations", x => x.Id);
                    table.CheckConstraint("CK_SupplierPaymentAllocations_Amount_Positive", "\"AllocatedAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_SupplierPaymentAllocations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPaymentAllocations_GoodsReceipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPaymentAllocations_SupplierLedgerEntries_SupplierLe~",
                        column: x => x.SupplierLedgerEntryId,
                        principalTable: "SupplierLedgerEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPaymentAllocations_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPaymentAllocations_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    VoucherDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChartOfAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContraToChartOfAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalOfVoucherId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                    table.CheckConstraint("CK_Vouchers_Posted_HasJournalEntry", "(\"Status\" <> 2) OR (\"JournalEntryId\" IS NOT NULL AND \"PostedByUserId\" IS NOT NULL AND \"PostedAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_Vouchers_Status", "\"Status\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_Vouchers_Type", "\"Type\" BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_Vouchers_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_ChartOfAccounts_ChartOfAccountId",
                        column: x => x.ChartOfAccountId,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_ChartOfAccounts_ContraToChartOfAccountId",
                        column: x => x.ContraToChartOfAccountId,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_Users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vouchers_Vouchers_ReversalOfVoucherId",
                        column: x => x.ReversalOfVoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VoucherLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChartOfAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherLines", x => x.Id);
                    table.CheckConstraint("CK_VoucherLines_Amounts", "\"Debit\" >= 0 AND \"Credit\" >= 0 AND NOT (\"Debit\" > 0 AND \"Credit\" > 0) AND (\"Debit\" > 0 OR \"Credit\" > 0)");
                    table.ForeignKey(
                        name: "FK_VoucherLines_ChartOfAccounts_ChartOfAccountId",
                        column: x => x.ChartOfAccountId,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoucherLines_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoucherLines_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoucherLines_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_DueDateUtc",
                table: "Sales",
                column: "DueDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_DueDate",
                table: "GoodsReceipts",
                column: "DueDate");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Customers_CreditDays_NonNegative",
                table: "Customers",
                sql: "\"CreditDays\" IS NULL OR \"CreditDays\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentAllocations_BranchId",
                table: "CustomerPaymentAllocations",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentAllocations_CreatedByUserId",
                table: "CustomerPaymentAllocations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentAllocations_CustomerId_BranchId",
                table: "CustomerPaymentAllocations",
                columns: new[] { "CustomerId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentAllocations_CustomerPaymentId",
                table: "CustomerPaymentAllocations",
                column: "CustomerPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPaymentAllocations_SaleId",
                table: "CustomerPaymentAllocations",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentAllocations_BranchId",
                table: "SupplierPaymentAllocations",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentAllocations_CreatedByUserId",
                table: "SupplierPaymentAllocations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentAllocations_GoodsReceiptId",
                table: "SupplierPaymentAllocations",
                column: "GoodsReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentAllocations_SupplierId_BranchId",
                table: "SupplierPaymentAllocations",
                columns: new[] { "SupplierId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentAllocations_SupplierLedgerEntryId",
                table: "SupplierPaymentAllocations",
                column: "SupplierLedgerEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherLines_ChartOfAccountId",
                table: "VoucherLines",
                column: "ChartOfAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherLines_CustomerId",
                table: "VoucherLines",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherLines_SupplierId",
                table: "VoucherLines",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherLines_VoucherId",
                table: "VoucherLines",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_BranchId_VoucherDateUtc",
                table: "Vouchers",
                columns: new[] { "BranchId", "VoucherDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_ChartOfAccountId",
                table: "Vouchers",
                column: "ChartOfAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_ContraToChartOfAccountId",
                table: "Vouchers",
                column: "ContraToChartOfAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_CreatedByUserId",
                table: "Vouchers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_CustomerId",
                table: "Vouchers",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_JournalEntryId",
                table: "Vouchers",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_PostedByUserId",
                table: "Vouchers",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_ReversalOfVoucherId",
                table: "Vouchers",
                column: "ReversalOfVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_SupplierId",
                table: "Vouchers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Type_Status",
                table: "Vouchers",
                columns: new[] { "Type", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_VoucherNumber",
                table: "Vouchers",
                column: "VoucherNumber",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000201', 'accounts.aging.receivables.view', 'View accounts receivable aging', 'accounting', now(), now()),
                    ('20000000-0000-0000-0000-000000000202', 'accounts.aging.payables.view', 'View accounts payable aging', 'accounting', now(), now()),
                    ('20000000-0000-0000-0000-000000000203', 'accounts.voucher.view', 'View accounting vouchers', 'accounting', now(), now()),
                    ('20000000-0000-0000-0000-000000000204', 'accounts.voucher.create', 'Create and cancel draft accounting vouchers', 'accounting', now(), now()),
                    ('20000000-0000-0000-0000-000000000205', 'accounts.voucher.post', 'Post accounting vouchers', 'accounting', now(), now()),
                    ('20000000-0000-0000-0000-000000000206', 'accounts.voucher.reverse', 'Reverse posted accounting vouchers', 'accounting', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000401'::uuid, 'Owner', 'accounts.aging.receivables.view'),
                    ('30000000-0000-0000-0000-000000000402'::uuid, 'Owner', 'accounts.aging.payables.view'),
                    ('30000000-0000-0000-0000-000000000403'::uuid, 'Owner', 'accounts.voucher.view'),
                    ('30000000-0000-0000-0000-000000000404'::uuid, 'Owner', 'accounts.voucher.create'),
                    ('30000000-0000-0000-0000-000000000405'::uuid, 'Owner', 'accounts.voucher.post'),
                    ('30000000-0000-0000-0000-000000000406'::uuid, 'Owner', 'accounts.voucher.reverse'),
                    ('30000000-0000-0000-0000-000000000407'::uuid, 'Manager', 'accounts.aging.receivables.view'),
                    ('30000000-0000-0000-0000-000000000408'::uuid, 'Manager', 'accounts.aging.payables.view'),
                    ('30000000-0000-0000-0000-000000000409'::uuid, 'Manager', 'accounts.voucher.view'),
                    ('30000000-0000-0000-0000-000000000410'::uuid, 'Manager', 'accounts.voucher.create'),
                    ('30000000-0000-0000-0000-000000000411'::uuid, 'Manager', 'accounts.voucher.post'),
                    ('30000000-0000-0000-0000-000000000412'::uuid, 'Manager', 'accounts.voucher.reverse'),
                    ('30000000-0000-0000-0000-000000000413'::uuid, 'Accountant', 'accounts.aging.receivables.view'),
                    ('30000000-0000-0000-0000-000000000414'::uuid, 'Accountant', 'accounts.aging.payables.view'),
                    ('30000000-0000-0000-0000-000000000415'::uuid, 'Accountant', 'accounts.voucher.view'),
                    ('30000000-0000-0000-0000-000000000416'::uuid, 'Accountant', 'accounts.voucher.create'),
                    ('30000000-0000-0000-0000-000000000417'::uuid, 'Accountant', 'accounts.voucher.post'),
                    ('30000000-0000-0000-0000-000000000418'::uuid, 'Accountant', 'accounts.voucher.reverse')
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
                        ('accounts.aging.receivables.view', 'accounts.aging.payables.view',
                         'accounts.voucher.view', 'accounts.voucher.create', 'accounts.voucher.post', 'accounts.voucher.reverse'));
                DELETE FROM "Permissions" WHERE "Code" IN
                    ('accounts.aging.receivables.view', 'accounts.aging.payables.view',
                     'accounts.voucher.view', 'accounts.voucher.create', 'accounts.voucher.post', 'accounts.voucher.reverse');
                """);

            migrationBuilder.DropTable(
                name: "CustomerPaymentAllocations");

            migrationBuilder.DropTable(
                name: "SupplierPaymentAllocations");

            migrationBuilder.DropTable(
                name: "VoucherLines");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Sales_DueDateUtc",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_DueDate",
                table: "GoodsReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Customers_CreditDays_NonNegative",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DueDateUtc",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "CreditDays",
                table: "Customers");

            migrationBuilder.DropSequence(
                name: "BankPaymentVoucherNumberSequence");

            migrationBuilder.DropSequence(
                name: "BankReceiptVoucherNumberSequence");

            migrationBuilder.DropSequence(
                name: "CashPaymentVoucherNumberSequence");

            migrationBuilder.DropSequence(
                name: "CashReceiptVoucherNumberSequence");

            migrationBuilder.DropSequence(
                name: "ContraVoucherNumberSequence");

            migrationBuilder.DropSequence(
                name: "JournalVoucherNumberSequence");
        }
    }
}
