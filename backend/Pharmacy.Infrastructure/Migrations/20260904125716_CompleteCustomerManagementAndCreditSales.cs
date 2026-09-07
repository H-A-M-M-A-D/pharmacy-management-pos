using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCustomerManagementAndCreditSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE SEQUENCE \"CustomerCodeSequence\" START WITH 1 INCREMENT BY 1;");
            migrationBuilder.Sql("CREATE SEQUENCE \"CustomerPaymentReceiptSequence\" START WITH 1 INCREMENT BY 1;");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesReturns_Money_NonNegative",
                table: "SalesReturns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_Money_NonNegative",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_Posted_Paid",
                table: "Sales");

            migrationBuilder.AddColumn<decimal>(
                name: "CashRefundAmount",
                table: "SalesReturns",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CustomerCreditReductionAmount",
                table: "SalesReturns",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditAmount",
                table: "Sales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AlternatePhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NTN = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OpeningBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.CheckConstraint("CK_Customers_CreditLimit_NonNegative", "\"CreditLimit\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "CustomerLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_CustomerLedgerEntries_AmountSign", "\"Amount\" <> 0 AND ((\"EntryType\" = 1) OR (\"EntryType\" IN (2, 5) AND \"Amount\" > 0) OR (\"EntryType\" IN (3, 4, 6) AND \"Amount\" < 0))");
                    table.ForeignKey(
                        name: "FK_CustomerLedgerEntries_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerLedgerEntries_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerLedgerEntries_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaymentDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPayments", x => x.Id);
                    table.CheckConstraint("CK_CustomerPayments_Amount_Positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_CustomerPayments_Method", "\"PaymentMethod\" IN (1, 2, 3, 4, 5, 6, 7)");
                    table.ForeignKey(
                        name: "FK_CustomerPayments_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPayments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPayments_Users_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesReturns_Money_NonNegative",
                table: "SalesReturns",
                sql: "\"GrossReturnAmount\" >= 0 AND \"DiscountReturnAmount\" >= 0 AND \"TaxReturnAmount\" >= 0 AND \"RefundAmount\" >= 0 AND \"CustomerCreditReductionAmount\" >= 0 AND \"CashRefundAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesReturns_Settlement",
                table: "SalesReturns",
                sql: "\"RefundAmount\" = \"CustomerCreditReductionAmount\" + \"CashRefundAmount\"");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerId_PostedAtUtc",
                table: "Sales",
                columns: new[] { "CustomerId", "PostedAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_CreditRequiresCustomer",
                table: "Sales",
                sql: "\"CreditAmount\" = 0 OR \"CustomerId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_Money_NonNegative",
                table: "Sales",
                sql: "\"Subtotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"TaxTotal\" >= 0 AND \"NetTotal\" >= 0 AND \"AmountPaid\" >= 0 AND \"CreditAmount\" >= 0 AND \"ChangeGiven\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_Posted_Settled",
                table: "Sales",
                sql: "(\"Status\" <> 2) OR (\"AmountPaid\" + \"CreditAmount\" = \"NetTotal\")");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerEntries_BranchId_CreatedAt",
                table: "CustomerLedgerEntries",
                columns: new[] { "BranchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerEntries_CreatedByUserId",
                table: "CustomerLedgerEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerEntries_CustomerId_BranchId_CreatedAt",
                table: "CustomerLedgerEntries",
                columns: new[] { "CustomerId", "BranchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerEntries_CustomerId_CreatedAt",
                table: "CustomerLedgerEntries",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerEntries_EntryType_CreatedAt",
                table: "CustomerLedgerEntries",
                columns: new[] { "EntryType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLedgerEntries_ReferenceType_ReferenceId",
                table: "CustomerLedgerEntries",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_BranchId_PaymentDateUtc",
                table: "CustomerPayments",
                columns: new[] { "BranchId", "PaymentDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_CustomerId_PaymentDateUtc",
                table: "CustomerPayments",
                columns: new[] { "CustomerId", "PaymentDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_PaymentMethod_PaymentDateUtc",
                table: "CustomerPayments",
                columns: new[] { "PaymentMethod", "PaymentDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_ReceiptNumber",
                table: "CustomerPayments",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_ReceivedByUserId",
                table: "CustomerPayments",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_City",
                table: "Customers",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerCode",
                table: "Customers",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_IsActive",
                table: "Customers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Name",
                table: "Customers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_NormalizedName",
                table: "Customers",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PhoneNumber",
                table: "Customers",
                column: "PhoneNumber");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Customers_CustomerId",
                table: "Sales",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000060', 'customers.view', 'View customer master records and lookup customers at POS', 'customers', now(), now()),
                    ('20000000-0000-0000-0000-000000000061', 'customers.create', 'Create customer master records', 'customers', now(), now()),
                    ('20000000-0000-0000-0000-000000000062', 'customers.update', 'Update customer master contact and credit settings', 'customers', now(), now()),
                    ('20000000-0000-0000-0000-000000000063', 'customers.activate', 'Activate customer records', 'customers', now(), now()),
                    ('20000000-0000-0000-0000-000000000064', 'customers.deactivate', 'Deactivate customer records for new credit sales', 'customers', now(), now()),
                    ('20000000-0000-0000-0000-000000000065', 'customers.ledger.view', 'View customer credit ledger and statements', 'customers', now(), now()),
                    ('20000000-0000-0000-0000-000000000066', 'customers.payment.create', 'Record customer account payments', 'customers', now(), now()),
                    ('20000000-0000-0000-0000-000000000067', 'customers.adjust_balance', 'Record controlled customer balance adjustments', 'customers', now(), now()),
                    ('20000000-0000-0000-0000-000000000068', 'sales.credit', 'Post sales with customer credit settlement', 'sales', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000205'::uuid, 'Owner', 'customers.view'),
                    ('30000000-0000-0000-0000-000000000206'::uuid, 'Owner', 'customers.create'),
                    ('30000000-0000-0000-0000-000000000207'::uuid, 'Owner', 'customers.update'),
                    ('30000000-0000-0000-0000-000000000208'::uuid, 'Owner', 'customers.activate'),
                    ('30000000-0000-0000-0000-000000000209'::uuid, 'Owner', 'customers.deactivate'),
                    ('30000000-0000-0000-0000-000000000210'::uuid, 'Owner', 'customers.ledger.view'),
                    ('30000000-0000-0000-0000-000000000211'::uuid, 'Owner', 'customers.payment.create'),
                    ('30000000-0000-0000-0000-000000000212'::uuid, 'Owner', 'customers.adjust_balance'),
                    ('30000000-0000-0000-0000-000000000213'::uuid, 'Owner', 'sales.credit'),
                    ('30000000-0000-0000-0000-000000000214'::uuid, 'Manager', 'customers.view'),
                    ('30000000-0000-0000-0000-000000000215'::uuid, 'Manager', 'customers.create'),
                    ('30000000-0000-0000-0000-000000000216'::uuid, 'Manager', 'customers.update'),
                    ('30000000-0000-0000-0000-000000000217'::uuid, 'Manager', 'customers.activate'),
                    ('30000000-0000-0000-0000-000000000218'::uuid, 'Manager', 'customers.deactivate'),
                    ('30000000-0000-0000-0000-000000000219'::uuid, 'Manager', 'customers.ledger.view'),
                    ('30000000-0000-0000-0000-000000000220'::uuid, 'Manager', 'customers.payment.create'),
                    ('30000000-0000-0000-0000-000000000221'::uuid, 'Manager', 'customers.adjust_balance'),
                    ('30000000-0000-0000-0000-000000000222'::uuid, 'Manager', 'sales.credit'),
                    ('30000000-0000-0000-0000-000000000223'::uuid, 'Cashier', 'customers.view'),
                    ('30000000-0000-0000-0000-000000000224'::uuid, 'Cashier', 'customers.create'),
                    ('30000000-0000-0000-0000-000000000225'::uuid, 'Cashier', 'sales.credit'),
                    ('30000000-0000-0000-0000-000000000226'::uuid, 'Accountant', 'customers.view'),
                    ('30000000-0000-0000-0000-000000000227'::uuid, 'Accountant', 'customers.ledger.view'),
                    ('30000000-0000-0000-0000-000000000228'::uuid, 'Accountant', 'customers.payment.create'),
                    ('30000000-0000-0000-0000-000000000229'::uuid, 'Accountant', 'customers.adjust_balance'),
                    ('30000000-0000-0000-0000-000000000230'::uuid, 'Pharmacist', 'customers.view')
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
                        ('customers.view','customers.create','customers.update','customers.activate',
                         'customers.deactivate','customers.ledger.view','customers.payment.create',
                         'customers.adjust_balance','sales.credit'));
                DELETE FROM "Permissions" WHERE "Code" IN
                    ('customers.view','customers.create','customers.update','customers.activate',
                     'customers.deactivate','customers.ledger.view','customers.payment.create',
                     'customers.adjust_balance','sales.credit');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Customers_CustomerId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "CustomerLedgerEntries");

            migrationBuilder.DropTable(
                name: "CustomerPayments");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesReturns_Money_NonNegative",
                table: "SalesReturns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesReturns_Settlement",
                table: "SalesReturns");

            migrationBuilder.DropIndex(
                name: "IX_Sales_CustomerId_PostedAtUtc",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_CreditRequiresCustomer",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_Money_NonNegative",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_Posted_Settled",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CashRefundAmount",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "CustomerCreditReductionAmount",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "CreditAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Sales");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesReturns_Money_NonNegative",
                table: "SalesReturns",
                sql: "\"GrossReturnAmount\" >= 0 AND \"DiscountReturnAmount\" >= 0 AND \"TaxReturnAmount\" >= 0 AND \"RefundAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_Money_NonNegative",
                table: "Sales",
                sql: "\"Subtotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"TaxTotal\" >= 0 AND \"NetTotal\" >= 0 AND \"AmountPaid\" >= 0 AND \"ChangeGiven\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_Posted_Paid",
                table: "Sales",
                sql: "(\"Status\" <> 2) OR (\"AmountPaid\" = \"NetTotal\")");

            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS \"CustomerPaymentReceiptSequence\";");
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS \"CustomerCodeSequence\";");
        }
    }
}
