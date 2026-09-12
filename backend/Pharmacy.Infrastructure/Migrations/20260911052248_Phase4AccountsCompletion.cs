using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4AccountsCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountMappings_MappingKey",
                table: "AccountMappings");

            migrationBuilder.CreateSequence(
                name: "CreditNoteNumberSequence");

            migrationBuilder.CreateSequence(
                name: "CustomerAdvanceNumberSequence");

            migrationBuilder.CreateSequence(
                name: "CustomerWriteOffNumberSequence");

            migrationBuilder.CreateSequence(
                name: "DebitNoteNumberSequence");

            migrationBuilder.CreateSequence(
                name: "SupplierAdvanceNumberSequence");

            migrationBuilder.CreateSequence(
                name: "SupplierWriteOffNumberSequence");

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialAccountId",
                table: "Vouchers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CostCenterId",
                table: "JournalEntryLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "JournalEntries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversesJournalEntryId",
                table: "JournalEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankReconciliationId",
                table: "FinancialLedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReconciledAtUtc",
                table: "FinancialLedgerEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinancialAccountId",
                table: "CashierShifts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountBudgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalYear = table.Column<int>(type: "integer", nullable: false),
                    PeriodNumber = table.Column<int>(type: "integer", nullable: true),
                    ChartOfAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    BudgetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountBudgets", x => x.Id);
                    table.CheckConstraint("CK_AccountBudgets_PeriodNumber_Range", "\"PeriodNumber\" IS NULL OR \"PeriodNumber\" BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_AccountBudgets_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountBudgets_ChartOfAccounts_ChartOfAccountId",
                        column: x => x.ChartOfAccountId,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountBudgets_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountingPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalYear = table.Column<int>(type: "integer", nullable: false),
                    PeriodNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReopenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReopenedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingPeriods", x => x.Id);
                    table.CheckConstraint("CK_AccountingPeriods_DateOrder", "\"EndDate\" >= \"StartDate\"");
                    table.CheckConstraint("CK_AccountingPeriods_Status", "\"Status\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_AccountingPeriods_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingPeriods_Users_ReopenedByUserId",
                        column: x => x.ReopenedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankReconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatementStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StatementEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StatementOpeningBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StatementClosingBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinalizedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinalizedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BookBalanceAtFinalization = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DifferenceAtFinalization = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ReopenedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReopenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReopenReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankReconciliations", x => x.Id);
                    table.CheckConstraint("CK_BankReconciliations_DateOrder", "\"StatementEndDate\" >= \"StatementStartDate\"");
                    table.CheckConstraint("CK_BankReconciliations_Status", "\"Status\" BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_BankReconciliations_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankReconciliations_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankReconciliations_Users_FinalizedByUserId",
                        column: x => x.FinalizedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankReconciliations_Users_ReopenedByUserId",
                        column: x => x.ReopenedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CostCenters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCenters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditNoteNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IssueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedToSaleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditNotes", x => x.Id);
                    table.CheckConstraint("CK_CreditNotes_Amount_Positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_CreditNotes_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditNotes_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Sales_AppliedToSaleId",
                        column: x => x.AppliedToSaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditNotes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAdvances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvanceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountApplied = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAdvances", x => x.Id);
                    table.CheckConstraint("CK_CustomerAdvances_Amount_Positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_CustomerAdvances_AmountApplied_Range", "\"AmountApplied\" >= 0 AND \"AmountApplied\" <= \"Amount\"");
                    table.ForeignKey(
                        name: "FK_CustomerAdvances_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAdvances_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAdvances_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAdvances_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAdvances_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerWriteOffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WriteOffNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WriteOffDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedToSaleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerWriteOffs", x => x.Id);
                    table.CheckConstraint("CK_CustomerWriteOffs_Amount_Positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_CustomerWriteOffs_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerWriteOffs_CustomerPayments_CustomerPaymentId",
                        column: x => x.CustomerPaymentId,
                        principalTable: "CustomerPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerWriteOffs_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerWriteOffs_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerWriteOffs_Sales_AppliedToSaleId",
                        column: x => x.AppliedToSaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerWriteOffs_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DebitNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DebitNoteNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IssueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedToGoodsReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebitNotes", x => x.Id);
                    table.CheckConstraint("CK_DebitNotes_Amount_Positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_DebitNotes_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DebitNotes_GoodsReceipts_AppliedToGoodsReceiptId",
                        column: x => x.AppliedToGoodsReceiptId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DebitNotes_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DebitNotes_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DebitNotes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FiscalYearCloses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalYear = table.Column<int>(type: "integer", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCostOfGoodsSold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalOperatingExpenses = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetProfit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReopenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReopenedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReopenReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYearCloses", x => x.Id);
                    table.CheckConstraint("CK_FiscalYearCloses_Status", "\"Status\" BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_FiscalYearCloses_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FiscalYearCloses_Users_ReopenedByUserId",
                        column: x => x.ReopenedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecurringJournalTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NextRunDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringJournalTemplates", x => x.Id);
                    table.CheckConstraint("CK_RecurringJournalTemplates_Frequency", "\"Frequency\" BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_RecurringJournalTemplates_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringJournalTemplates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierAdvances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvanceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountApplied = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaidDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierAdvances", x => x.Id);
                    table.CheckConstraint("CK_SupplierAdvances_Amount_Positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_SupplierAdvances_AmountApplied_Range", "\"AmountApplied\" >= 0 AND \"AmountApplied\" <= \"Amount\"");
                    table.ForeignKey(
                        name: "FK_SupplierAdvances_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierAdvances_FinancialAccounts_FinancialAccountId",
                        column: x => x.FinancialAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierAdvances_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierAdvances_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierAdvances_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierWriteOffs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WriteOffNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WriteOffDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedToGoodsReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierWriteOffs", x => x.Id);
                    table.CheckConstraint("CK_SupplierWriteOffs_Amount_Positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_SupplierWriteOffs_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierWriteOffs_GoodsReceipts_AppliedToGoodsReceiptId",
                        column: x => x.AppliedToGoodsReceiptId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierWriteOffs_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierWriteOffs_SupplierLedgerEntries_SupplierLedgerEntry~",
                        column: x => x.SupplierLedgerEntryId,
                        principalTable: "SupplierLedgerEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierWriteOffs_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierWriteOffs_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAdvanceApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerAdvanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerPaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAdvanceApplications", x => x.Id);
                    table.CheckConstraint("CK_CustomerAdvanceApplications_Amount_Positive", "\"AppliedAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_CustomerAdvanceApplications_CustomerAdvances_CustomerAdvanc~",
                        column: x => x.CustomerAdvanceId,
                        principalTable: "CustomerAdvances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAdvanceApplications_CustomerPayments_CustomerPaymen~",
                        column: x => x.CustomerPaymentId,
                        principalTable: "CustomerPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAdvanceApplications_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAdvanceApplications_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAdvanceApplications_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecurringJournalOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecurringJournalTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringJournalOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringJournalOccurrences_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringJournalOccurrences_RecurringJournalTemplates_Recur~",
                        column: x => x.RecurringJournalTemplateId,
                        principalTable: "RecurringJournalTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringJournalOccurrences_Users_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecurringJournalTemplateLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecurringJournalTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChartOfAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringJournalTemplateLines", x => x.Id);
                    table.CheckConstraint("CK_RecurringJournalTemplateLines_Amounts", "\"Debit\" >= 0 AND \"Credit\" >= 0 AND NOT (\"Debit\" > 0 AND \"Credit\" > 0) AND (\"Debit\" > 0 OR \"Credit\" > 0)");
                    table.ForeignKey(
                        name: "FK_RecurringJournalTemplateLines_ChartOfAccounts_ChartOfAccoun~",
                        column: x => x.ChartOfAccountId,
                        principalTable: "ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringJournalTemplateLines_RecurringJournalTemplates_Rec~",
                        column: x => x.RecurringJournalTemplateId,
                        principalTable: "RecurringJournalTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierAdvanceApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierAdvanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoodsReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupplierLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierAdvanceApplications", x => x.Id);
                    table.CheckConstraint("CK_SupplierAdvanceApplications_Amount_Positive", "\"AppliedAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_SupplierAdvanceApplications_GoodsReceipts_GoodsReceiptId",
                        column: x => x.GoodsReceiptId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierAdvanceApplications_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierAdvanceApplications_SupplierAdvances_SupplierAdvanc~",
                        column: x => x.SupplierAdvanceId,
                        principalTable: "SupplierAdvances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierAdvanceApplications_SupplierLedgerEntries_SupplierL~",
                        column: x => x.SupplierLedgerEntryId,
                        principalTable: "SupplierLedgerEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierAdvanceApplications_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_FinancialAccountId",
                table: "Vouchers",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryLines_CostCenterId",
                table: "JournalEntryLines",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ReversesJournalEntryId",
                table: "JournalEntries",
                column: "ReversesJournalEntryId",
                unique: true,
                filter: "\"ReversesJournalEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialLedgerEntries_BankReconciliationId",
                table: "FinancialLedgerEntries",
                column: "BankReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_FinancialAccountId",
                table: "CashierShifts",
                column: "FinancialAccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountMappings_MappingKey",
                table: "AccountMappings",
                sql: "\"MappingKey\" BETWEEN 1 AND 22");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBudgets_BranchId",
                table: "AccountBudgets",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBudgets_ChartOfAccountId",
                table: "AccountBudgets",
                column: "ChartOfAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBudgets_CreatedByUserId",
                table: "AccountBudgets",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBudgets_FiscalYear_PeriodNumber_ChartOfAccountId_Bra~",
                table: "AccountBudgets",
                columns: new[] { "FiscalYear", "PeriodNumber", "ChartOfAccountId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriods_ClosedByUserId",
                table: "AccountingPeriods",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriods_FiscalYear_PeriodNumber",
                table: "AccountingPeriods",
                columns: new[] { "FiscalYear", "PeriodNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriods_ReopenedByUserId",
                table: "AccountingPeriods",
                column: "ReopenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriods_StartDate_EndDate",
                table: "AccountingPeriods",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriods_Status",
                table: "AccountingPeriods",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliations_CreatedByUserId",
                table: "BankReconciliations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliations_FinalizedByUserId",
                table: "BankReconciliations",
                column: "FinalizedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliations_FinancialAccountId_StatementEndDate",
                table: "BankReconciliations",
                columns: new[] { "FinancialAccountId", "StatementEndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliations_FinancialAccountId_Status",
                table: "BankReconciliations",
                columns: new[] { "FinancialAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliations_ReopenedByUserId",
                table: "BankReconciliations",
                column: "ReopenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenters_IsActive",
                table: "CostCenters",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenters_NormalizedCode",
                table: "CostCenters",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_AppliedToSaleId",
                table: "CreditNotes",
                column: "AppliedToSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_BranchId",
                table: "CreditNotes",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_CreatedByUserId",
                table: "CreditNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_CreditNoteNumber",
                table: "CreditNotes",
                column: "CreditNoteNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_CustomerId_IssueDateUtc",
                table: "CreditNotes",
                columns: new[] { "CustomerId", "IssueDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_JournalEntryId",
                table: "CreditNotes",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdvanceApplications_CreatedByUserId",
                table: "CustomerAdvanceApplications",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdvanceApplications_CustomerAdvanceId",
                table: "CustomerAdvanceApplications",
                column: "CustomerAdvanceId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdvanceApplications_CustomerPaymentId",
                table: "CustomerAdvanceApplications",
                column: "CustomerPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdvanceApplications_JournalEntryId",
                table: "CustomerAdvanceApplications",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdvanceApplications_SaleId",
                table: "CustomerAdvanceApplications",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdvances_AdvanceNumber",
                table: "CustomerAdvances",
                column: "AdvanceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdvances_BranchId",
                table: "CustomerAdvances",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdvances_CreatedByUserId",
                table: "CustomerAdvances",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdvances_CustomerId_ReceivedDateUtc",
                table: "CustomerAdvances",
                columns: new[] { "CustomerId", "ReceivedDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdvances_FinancialAccountId",
                table: "CustomerAdvances",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAdvances_JournalEntryId",
                table: "CustomerAdvances",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWriteOffs_AppliedToSaleId",
                table: "CustomerWriteOffs",
                column: "AppliedToSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWriteOffs_BranchId",
                table: "CustomerWriteOffs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWriteOffs_CreatedByUserId",
                table: "CustomerWriteOffs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWriteOffs_CustomerId_WriteOffDateUtc",
                table: "CustomerWriteOffs",
                columns: new[] { "CustomerId", "WriteOffDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWriteOffs_CustomerPaymentId",
                table: "CustomerWriteOffs",
                column: "CustomerPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWriteOffs_JournalEntryId",
                table: "CustomerWriteOffs",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerWriteOffs_WriteOffNumber",
                table: "CustomerWriteOffs",
                column: "WriteOffNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_AppliedToGoodsReceiptId",
                table: "DebitNotes",
                column: "AppliedToGoodsReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_BranchId",
                table: "DebitNotes",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_CreatedByUserId",
                table: "DebitNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_DebitNoteNumber",
                table: "DebitNotes",
                column: "DebitNoteNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_JournalEntryId",
                table: "DebitNotes",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_SupplierId_IssueDateUtc",
                table: "DebitNotes",
                columns: new[] { "SupplierId", "IssueDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearCloses_ClosedByUserId",
                table: "FiscalYearCloses",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearCloses_FiscalYear",
                table: "FiscalYearCloses",
                column: "FiscalYear",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearCloses_ReopenedByUserId",
                table: "FiscalYearCloses",
                column: "ReopenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringJournalOccurrences_GeneratedByUserId",
                table: "RecurringJournalOccurrences",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringJournalOccurrences_JournalEntryId",
                table: "RecurringJournalOccurrences",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringJournalOccurrences_RecurringJournalTemplateId_Sche~",
                table: "RecurringJournalOccurrences",
                columns: new[] { "RecurringJournalTemplateId", "ScheduledDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringJournalTemplateLines_ChartOfAccountId",
                table: "RecurringJournalTemplateLines",
                column: "ChartOfAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringJournalTemplateLines_RecurringJournalTemplateId",
                table: "RecurringJournalTemplateLines",
                column: "RecurringJournalTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringJournalTemplates_BranchId_IsActive",
                table: "RecurringJournalTemplates",
                columns: new[] { "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringJournalTemplates_CreatedByUserId",
                table: "RecurringJournalTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringJournalTemplates_NextRunDate",
                table: "RecurringJournalTemplates",
                column: "NextRunDate");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdvanceApplications_CreatedByUserId",
                table: "SupplierAdvanceApplications",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdvanceApplications_GoodsReceiptId",
                table: "SupplierAdvanceApplications",
                column: "GoodsReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdvanceApplications_JournalEntryId",
                table: "SupplierAdvanceApplications",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdvanceApplications_SupplierAdvanceId",
                table: "SupplierAdvanceApplications",
                column: "SupplierAdvanceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdvanceApplications_SupplierLedgerEntryId",
                table: "SupplierAdvanceApplications",
                column: "SupplierLedgerEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdvances_AdvanceNumber",
                table: "SupplierAdvances",
                column: "AdvanceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdvances_BranchId",
                table: "SupplierAdvances",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdvances_CreatedByUserId",
                table: "SupplierAdvances",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdvances_FinancialAccountId",
                table: "SupplierAdvances",
                column: "FinancialAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdvances_JournalEntryId",
                table: "SupplierAdvances",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAdvances_SupplierId_PaidDateUtc",
                table: "SupplierAdvances",
                columns: new[] { "SupplierId", "PaidDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierWriteOffs_AppliedToGoodsReceiptId",
                table: "SupplierWriteOffs",
                column: "AppliedToGoodsReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierWriteOffs_BranchId",
                table: "SupplierWriteOffs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierWriteOffs_CreatedByUserId",
                table: "SupplierWriteOffs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierWriteOffs_JournalEntryId",
                table: "SupplierWriteOffs",
                column: "JournalEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierWriteOffs_SupplierId_WriteOffDateUtc",
                table: "SupplierWriteOffs",
                columns: new[] { "SupplierId", "WriteOffDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierWriteOffs_SupplierLedgerEntryId",
                table: "SupplierWriteOffs",
                column: "SupplierLedgerEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierWriteOffs_WriteOffNumber",
                table: "SupplierWriteOffs",
                column: "WriteOffNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CashierShifts_FinancialAccounts_FinancialAccountId",
                table: "CashierShifts",
                column: "FinancialAccountId",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialLedgerEntries_BankReconciliations_BankReconciliati~",
                table: "FinancialLedgerEntries",
                column: "BankReconciliationId",
                principalTable: "BankReconciliations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_JournalEntries_ReversesJournalEntryId",
                table: "JournalEntries",
                column: "ReversesJournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntryLines_CostCenters_CostCenterId",
                table: "JournalEntryLines",
                column: "CostCenterId",
                principalTable: "CostCenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_FinancialAccounts_FinancialAccountId",
                table: "Vouchers",
                column: "FinancialAccountId",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashierShifts_FinancialAccounts_FinancialAccountId",
                table: "CashierShifts");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialLedgerEntries_BankReconciliations_BankReconciliati~",
                table: "FinancialLedgerEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_JournalEntries_ReversesJournalEntryId",
                table: "JournalEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntryLines_CostCenters_CostCenterId",
                table: "JournalEntryLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_FinancialAccounts_FinancialAccountId",
                table: "Vouchers");

            migrationBuilder.DropTable(
                name: "AccountBudgets");

            migrationBuilder.DropTable(
                name: "AccountingPeriods");

            migrationBuilder.DropTable(
                name: "BankReconciliations");

            migrationBuilder.DropTable(
                name: "CostCenters");

            migrationBuilder.DropTable(
                name: "CreditNotes");

            migrationBuilder.DropTable(
                name: "CustomerAdvanceApplications");

            migrationBuilder.DropTable(
                name: "CustomerWriteOffs");

            migrationBuilder.DropTable(
                name: "DebitNotes");

            migrationBuilder.DropTable(
                name: "FiscalYearCloses");

            migrationBuilder.DropTable(
                name: "RecurringJournalOccurrences");

            migrationBuilder.DropTable(
                name: "RecurringJournalTemplateLines");

            migrationBuilder.DropTable(
                name: "SupplierAdvanceApplications");

            migrationBuilder.DropTable(
                name: "SupplierWriteOffs");

            migrationBuilder.DropTable(
                name: "CustomerAdvances");

            migrationBuilder.DropTable(
                name: "RecurringJournalTemplates");

            migrationBuilder.DropTable(
                name: "SupplierAdvances");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_FinancialAccountId",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntryLines_CostCenterId",
                table: "JournalEntryLines");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_ReversesJournalEntryId",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_FinancialLedgerEntries_BankReconciliationId",
                table: "FinancialLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_CashierShifts_FinancialAccountId",
                table: "CashierShifts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountMappings_MappingKey",
                table: "AccountMappings");

            migrationBuilder.DropColumn(
                name: "FinancialAccountId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "JournalEntryLines");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ReversesJournalEntryId",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "BankReconciliationId",
                table: "FinancialLedgerEntries");

            migrationBuilder.DropColumn(
                name: "ReconciledAtUtc",
                table: "FinancialLedgerEntries");

            migrationBuilder.DropColumn(
                name: "FinancialAccountId",
                table: "CashierShifts");

            migrationBuilder.DropSequence(
                name: "CreditNoteNumberSequence");

            migrationBuilder.DropSequence(
                name: "CustomerAdvanceNumberSequence");

            migrationBuilder.DropSequence(
                name: "CustomerWriteOffNumberSequence");

            migrationBuilder.DropSequence(
                name: "DebitNoteNumberSequence");

            migrationBuilder.DropSequence(
                name: "SupplierAdvanceNumberSequence");

            migrationBuilder.DropSequence(
                name: "SupplierWriteOffNumberSequence");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountMappings_MappingKey",
                table: "AccountMappings",
                sql: "\"MappingKey\" BETWEEN 1 AND 18");
        }
    }
}
