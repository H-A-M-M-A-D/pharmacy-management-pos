using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteAccountsExpensesAndCashManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ExpenseNumberSequence");

            migrationBuilder.CreateSequence(
                name: "FinancialTransferNumberSequence");

            migrationBuilder.CreateSequence(
                name: "OtherIncomeNumberSequence");

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialAccountId",
                table: "SupplierLedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialAccountId",
                table: "SalesRefundPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialAccountId",
                table: "SalePayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialAccountId",
                table: "CustomerPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinancialAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialAccounts", x => x.Id);
                    table.CheckConstraint("CK_FinancialAccounts_Type", "\"AccountType\" IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_FinancialAccounts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Payee = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.CheckConstraint("CK_Expenses_Amount_Positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_Expenses_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialLedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_FinancialLedgerEntries_Amount_NonZero", "\"Amount\" <> 0");
                    table.CheckConstraint("CK_FinancialLedgerEntries_Sign", "(\"EntryType\" IN (2,3,7,9,11) AND \"Amount\" > 0) OR (\"EntryType\" IN (4,5,6,8,10) AND \"Amount\" < 0) OR \"EntryType\" = 1");
                    table.CheckConstraint("CK_FinancialLedgerEntries_Type", "\"EntryType\" IN (1,2,3,4,5,6,7,8,9,10,11)");
                    table.ForeignKey(
                        name: "FK_FinancialLedgerEntries_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialLedgerEntries_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialLedgerEntries_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialTransfers", x => x.Id);
                    table.CheckConstraint("CK_FinancialTransfers_Amount_Positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_FinancialTransfers_DifferentAccounts", "\"SourceAccountId\" <> \"DestinationAccountId\"");
                    table.ForeignKey(
                        name: "FK_FinancialTransfers_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialTransfers_FinancialAccounts_DestinationAccountId",
                        column: x => x.DestinationAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialTransfers_FinancialAccounts_SourceAccountId",
                        column: x => x.SourceAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialTransfers_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OtherIncomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherIncomes", x => x.Id);
                    table.CheckConstraint("CK_OtherIncomes_Amount_Positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_OtherIncomes_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OtherIncomes_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OtherIncomes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLedgerEntries_FinancialAccountId",
                table: "SupplierLedgerEntries",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesRefundPayments_FinancialAccountId",
                table: "SalesRefundPayments",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_FinancialAccountId",
                table: "SalePayments",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPayments_FinancialAccountId",
                table: "CustomerPayments",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_IsActive",
                table: "ExpenseCategories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_NormalizedName",
                table: "ExpenseCategories",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BranchId_ExpenseDateUtc",
                table: "Expenses",
                columns: new[] { "BranchId", "ExpenseDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CreatedByUserId",
                table: "Expenses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseCategoryId_ExpenseDateUtc",
                table: "Expenses",
                columns: new[] { "ExpenseCategoryId", "ExpenseDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseNumber",
                table: "Expenses",
                column: "ExpenseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_FinancialAccountId_ExpenseDateUtc",
                table: "Expenses",
                columns: new[] { "FinancialAccountId", "ExpenseDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_BranchId_IsActive",
                table: "FinancialAccounts",
                columns: new[] { "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_BranchId_NormalizedName",
                table: "FinancialAccounts",
                columns: new[] { "BranchId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialLedgerEntries_BranchId_OccurredAtUtc",
                table: "FinancialLedgerEntries",
                columns: new[] { "BranchId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialLedgerEntries_CreatedByUserId",
                table: "FinancialLedgerEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialLedgerEntries_EntryType_OccurredAtUtc",
                table: "FinancialLedgerEntries",
                columns: new[] { "EntryType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialLedgerEntries_FinancialAccountId_EntryType_Referen~",
                table: "FinancialLedgerEntries",
                columns: new[] { "FinancialAccountId", "EntryType", "ReferenceType", "ReferenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialLedgerEntries_FinancialAccountId_OccurredAtUtc",
                table: "FinancialLedgerEntries",
                columns: new[] { "FinancialAccountId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialLedgerEntries_ReferenceType_ReferenceId",
                table: "FinancialLedgerEntries",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransfers_BranchId_OccurredAtUtc",
                table: "FinancialTransfers",
                columns: new[] { "BranchId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransfers_CreatedByUserId",
                table: "FinancialTransfers",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransfers_DestinationAccountId",
                table: "FinancialTransfers",
                column: "DestinationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransfers_SourceAccountId",
                table: "FinancialTransfers",
                column: "SourceAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialTransfers_TransferNumber",
                table: "FinancialTransfers",
                column: "TransferNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomes_BranchId_OccurredAtUtc",
                table: "OtherIncomes",
                columns: new[] { "BranchId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomes_CreatedByUserId",
                table: "OtherIncomes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomes_FinancialAccountId_OccurredAtUtc",
                table: "OtherIncomes",
                columns: new[] { "FinancialAccountId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomes_IncomeNumber",
                table: "OtherIncomes",
                column: "IncomeNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPayments_FinancialAccounts_FinancialAccountId",
                table: "CustomerPayments",
                column: "FinancialAccountId",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalePayments_FinancialAccounts_FinancialAccountId",
                table: "SalePayments",
                column: "FinancialAccountId",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesRefundPayments_FinancialAccounts_FinancialAccountId",
                table: "SalesRefundPayments",
                column: "FinancialAccountId",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierLedgerEntries_FinancialAccounts_FinancialAccountId",
                table: "SupplierLedgerEntries",
                column: "FinancialAccountId",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000069', 'accounts.view', 'View branch financial accounts and balances', 'finance', now(), now()),
                    ('20000000-0000-0000-0000-000000000070', 'accounts.manage', 'Manage financial account metadata and expense categories', 'finance', now(), now()),
                    ('20000000-0000-0000-0000-000000000071', 'expenses.view', 'View posted expenses', 'finance', now(), now()),
                    ('20000000-0000-0000-0000-000000000072', 'expenses.create', 'Create operational expenses', 'finance', now(), now()),
                    ('20000000-0000-0000-0000-000000000073', 'expenses.post', 'Post operational expenses to the financial ledger', 'finance', now(), now()),
                    ('20000000-0000-0000-0000-000000000074', 'finance.ledger.view', 'View financial ledger and daily cash position', 'finance', now(), now()),
                    ('20000000-0000-0000-0000-000000000075', 'finance.income.create', 'Post non-sales operational income', 'finance', now(), now()),
                    ('20000000-0000-0000-0000-000000000076', 'finance.transfer', 'Transfer money between branch financial accounts', 'finance', now(), now()),
                    ('20000000-0000-0000-0000-000000000077', 'finance.adjust', 'Post controlled financial adjustments', 'finance', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000231'::uuid, 'Owner', 'accounts.view'),
                    ('30000000-0000-0000-0000-000000000232'::uuid, 'Owner', 'accounts.manage'),
                    ('30000000-0000-0000-0000-000000000233'::uuid, 'Owner', 'expenses.view'),
                    ('30000000-0000-0000-0000-000000000234'::uuid, 'Owner', 'expenses.create'),
                    ('30000000-0000-0000-0000-000000000235'::uuid, 'Owner', 'expenses.post'),
                    ('30000000-0000-0000-0000-000000000236'::uuid, 'Owner', 'finance.ledger.view'),
                    ('30000000-0000-0000-0000-000000000237'::uuid, 'Owner', 'finance.income.create'),
                    ('30000000-0000-0000-0000-000000000238'::uuid, 'Owner', 'finance.transfer'),
                    ('30000000-0000-0000-0000-000000000239'::uuid, 'Owner', 'finance.adjust'),
                    ('30000000-0000-0000-0000-000000000240'::uuid, 'Manager', 'accounts.view'),
                    ('30000000-0000-0000-0000-000000000241'::uuid, 'Manager', 'accounts.manage'),
                    ('30000000-0000-0000-0000-000000000242'::uuid, 'Manager', 'expenses.view'),
                    ('30000000-0000-0000-0000-000000000243'::uuid, 'Manager', 'expenses.create'),
                    ('30000000-0000-0000-0000-000000000244'::uuid, 'Manager', 'expenses.post'),
                    ('30000000-0000-0000-0000-000000000245'::uuid, 'Manager', 'finance.ledger.view'),
                    ('30000000-0000-0000-0000-000000000246'::uuid, 'Manager', 'finance.income.create'),
                    ('30000000-0000-0000-0000-000000000247'::uuid, 'Manager', 'finance.transfer'),
                    ('30000000-0000-0000-0000-000000000248'::uuid, 'Accountant', 'accounts.view'),
                    ('30000000-0000-0000-0000-000000000249'::uuid, 'Accountant', 'accounts.manage'),
                    ('30000000-0000-0000-0000-000000000250'::uuid, 'Accountant', 'expenses.view'),
                    ('30000000-0000-0000-0000-000000000251'::uuid, 'Accountant', 'expenses.create'),
                    ('30000000-0000-0000-0000-000000000252'::uuid, 'Accountant', 'expenses.post'),
                    ('30000000-0000-0000-0000-000000000253'::uuid, 'Accountant', 'finance.ledger.view'),
                    ('30000000-0000-0000-0000-000000000254'::uuid, 'Accountant', 'finance.income.create'),
                    ('30000000-0000-0000-0000-000000000255'::uuid, 'Accountant', 'finance.transfer'),
                    ('30000000-0000-0000-0000-000000000256'::uuid, 'Accountant', 'finance.adjust'),
                    ('30000000-0000-0000-0000-000000000257'::uuid, 'Cashier', 'accounts.view'),
                    ('30000000-0000-0000-0000-000000000258'::uuid, 'Cashier', 'finance.ledger.view'),
                    ('30000000-0000-0000-0000-000000000259'::uuid, 'PurchaseManager', 'accounts.view')
                ) AS mapping(id, role_name, permission_code)
                JOIN "Roles" role ON role."Name" = mapping.role_name
                JOIN "Permissions" permission ON permission."Code" = mapping.permission_code
                ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;

                INSERT INTO "ExpenseCategories" ("Id", "Name", "NormalizedName", "Description", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                    ('40000000-0000-0000-0000-000000000001', 'Rent', 'RENT', NULL, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000002', 'Electricity', 'ELECTRICITY', NULL, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000003', 'Internet', 'INTERNET', NULL, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000004', 'Salaries', 'SALARIES', NULL, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000005', 'Transport', 'TRANSPORT', NULL, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000006', 'Maintenance', 'MAINTENANCE', NULL, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000007', 'Stationery', 'STATIONERY', NULL, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000008', 'Refreshments', 'REFRESHMENTS', NULL, true, now(), now()),
                    ('40000000-0000-0000-0000-000000000009', 'Miscellaneous', 'MISCELLANEOUS', NULL, true, now(), now())
                ON CONFLICT ("NormalizedName") DO NOTHING;

                CREATE OR REPLACE FUNCTION pharmacy_financial_ledger_guard() RETURNS trigger AS $$
                DECLARE account_branch uuid; account_active boolean; current_balance numeric(18,2);
                BEGIN
                    SELECT "BranchId", "IsActive" INTO account_branch, account_active
                    FROM "FinancialAccounts" WHERE "Id" = NEW."FinancialAccountId" FOR UPDATE;
                    IF account_branch IS NULL OR account_branch <> NEW."BranchId" THEN
                        RAISE EXCEPTION 'Financial account branch mismatch' USING ERRCODE = '23514';
                    END IF;
                    IF NOT account_active AND NEW."EntryType" <> 1 THEN
                        RAISE EXCEPTION 'Financial account is inactive' USING ERRCODE = '23514';
                    END IF;
                    IF NEW."Amount" < 0 THEN
                        SELECT COALESCE(SUM("Amount"), 0) INTO current_balance FROM "FinancialLedgerEntries" WHERE "FinancialAccountId" = NEW."FinancialAccountId";
                        IF current_balance + NEW."Amount" < 0 THEN
                            RAISE EXCEPTION 'Insufficient financial account balance' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    RETURN NEW;
                END; $$ LANGUAGE plpgsql;
                CREATE TRIGGER "TR_FinancialLedgerEntries_Guard" BEFORE INSERT ON "FinancialLedgerEntries"
                    FOR EACH ROW EXECUTE FUNCTION pharmacy_financial_ledger_guard();

                CREATE OR REPLACE FUNCTION pharmacy_immutable_finance() RETURNS trigger AS $$
                BEGIN RAISE EXCEPTION 'Posted financial history is immutable' USING ERRCODE = '23514'; END; $$ LANGUAGE plpgsql;
                CREATE TRIGGER "TR_FinancialLedgerEntries_Immutable" BEFORE UPDATE OR DELETE ON "FinancialLedgerEntries" FOR EACH ROW EXECUTE FUNCTION pharmacy_immutable_finance();
                CREATE TRIGGER "TR_Expenses_Immutable" BEFORE UPDATE OR DELETE ON "Expenses" FOR EACH ROW EXECUTE FUNCTION pharmacy_immutable_finance();
                CREATE TRIGGER "TR_OtherIncomes_Immutable" BEFORE UPDATE OR DELETE ON "OtherIncomes" FOR EACH ROW EXECUTE FUNCTION pharmacy_immutable_finance();
                CREATE TRIGGER "TR_FinancialTransfers_Immutable" BEFORE UPDATE OR DELETE ON "FinancialTransfers" FOR EACH ROW EXECUTE FUNCTION pharmacy_immutable_finance();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TR_FinancialLedgerEntries_Guard" ON "FinancialLedgerEntries";
                DROP TRIGGER IF EXISTS "TR_FinancialLedgerEntries_Immutable" ON "FinancialLedgerEntries";
                DROP TRIGGER IF EXISTS "TR_Expenses_Immutable" ON "Expenses";
                DROP TRIGGER IF EXISTS "TR_OtherIncomes_Immutable" ON "OtherIncomes";
                DROP TRIGGER IF EXISTS "TR_FinancialTransfers_Immutable" ON "FinancialTransfers";
                DROP FUNCTION IF EXISTS pharmacy_financial_ledger_guard();
                DROP FUNCTION IF EXISTS pharmacy_immutable_finance();
                DELETE FROM "RolePermissions" WHERE "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" IN
                    ('accounts.view','accounts.manage','expenses.view','expenses.create','expenses.post','finance.ledger.view','finance.income.create','finance.transfer','finance.adjust'));
                DELETE FROM "Permissions" WHERE "Code" IN
                    ('accounts.view','accounts.manage','expenses.view','expenses.create','expenses.post','finance.ledger.view','finance.income.create','finance.transfer','finance.adjust');
                DELETE FROM "ExpenseCategories" WHERE "Id"::text LIKE '40000000-0000-0000-0000-00000000000%';
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPayments_FinancialAccounts_FinancialAccountId",
                table: "CustomerPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_SalePayments_FinancialAccounts_FinancialAccountId",
                table: "SalePayments");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesRefundPayments_FinancialAccounts_FinancialAccountId",
                table: "SalesRefundPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierLedgerEntries_FinancialAccounts_FinancialAccountId",
                table: "SupplierLedgerEntries");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "FinancialLedgerEntries");

            migrationBuilder.DropTable(
                name: "FinancialTransfers");

            migrationBuilder.DropTable(
                name: "OtherIncomes");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");

            migrationBuilder.DropTable(
                name: "FinancialAccounts");

            migrationBuilder.DropIndex(
                name: "IX_SupplierLedgerEntries_FinancialAccountId",
                table: "SupplierLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_SalesRefundPayments_FinancialAccountId",
                table: "SalesRefundPayments");

            migrationBuilder.DropIndex(
                name: "IX_SalePayments_FinancialAccountId",
                table: "SalePayments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPayments_FinancialAccountId",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "FinancialAccountId",
                table: "SupplierLedgerEntries");

            migrationBuilder.DropColumn(
                name: "FinancialAccountId",
                table: "SalesRefundPayments");

            migrationBuilder.DropColumn(
                name: "FinancialAccountId",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "FinancialAccountId",
                table: "CustomerPayments");

            migrationBuilder.DropSequence(
                name: "ExpenseNumberSequence");

            migrationBuilder.DropSequence(
                name: "FinancialTransferNumberSequence");

            migrationBuilder.DropSequence(
                name: "OtherIncomeNumberSequence");
        }
    }
}
