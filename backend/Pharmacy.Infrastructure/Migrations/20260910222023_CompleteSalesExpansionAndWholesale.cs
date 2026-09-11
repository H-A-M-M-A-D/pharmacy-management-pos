using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSalesExpansionAndWholesale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "QuotationNumberSequence");

            migrationBuilder.CreateSequence(
                name: "SalesOrderNumberSequence");

            migrationBuilder.AddColumn<string>(
                name: "CustomerPoNumber",
                table: "Sales",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceLevelId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QuotationId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SaleType",
                table: "Sales",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "SalesOrderId",
                table: "Sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BelowCostOverrideReason",
                table: "SaleItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountOverrideReason",
                table: "SaleItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBelowCost",
                table: "SaleItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDiscountOverride",
                table: "SaleItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsManualPriceOverride",
                table: "SaleItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PriceOverrideReason",
                table: "SaleItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceSource",
                table: "SaleItems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "ResolvedUnitPrice",
                table: "SaleItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPerson",
                table: "Customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CreditAllowed",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerType",
                table: "Customers",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Customers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceLevelId",
                table: "Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingAddress",
                table: "Customers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PriceLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceLevels_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductPriceBreaks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceLevelId = table.Column<Guid>(type: "uuid", nullable: true),
                    MinimumQuantity = table.Column<int>(type: "integer", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPriceBreaks", x => x.Id);
                    table.CheckConstraint("CK_ProductPriceBreaks_MinimumQuantity_Positive", "\"MinimumQuantity\" > 0");
                    table.CheckConstraint("CK_ProductPriceBreaks_SellingPrice_NonNegative", "\"SellingPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProductPriceBreaks_PriceLevels_PriceLevelId",
                        column: x => x.PriceLevelId,
                        principalTable: "PriceLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductPriceBreaks_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductPriceLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceLevelId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellingPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPriceLevels", x => x.Id);
                    table.CheckConstraint("CK_ProductPriceLevels_SellingPrice_NonNegative", "\"SellingPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProductPriceLevels_PriceLevels_PriceLevelId",
                        column: x => x.PriceLevelId,
                        principalTable: "PriceLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductPriceLevels_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderedQuantity = table.Column<int>(type: "integer", nullable: false),
                    FulfilledQuantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderItems", x => x.Id);
                    table.CheckConstraint("CK_SalesOrderItems_Discount_Range", "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100");
                    table.CheckConstraint("CK_SalesOrderItems_Fulfilled_Range", "\"FulfilledQuantity\" >= 0 AND \"FulfilledQuantity\" <= \"OrderedQuantity\"");
                    table.CheckConstraint("CK_SalesOrderItems_Money_NonNegative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"DiscountAmount\" >= 0 AND \"NetAmount\" >= 0");
                    table.CheckConstraint("CK_SalesOrderItems_Quantity_Positive", "\"OrderedQuantity\" > 0");
                    table.ForeignKey(
                        name: "FK_SalesOrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    GodownId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceLevelId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuotationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrders", x => x.Id);
                    table.CheckConstraint("CK_SalesOrders_Money_NonNegative", "\"Subtotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"NetTotal\" >= 0");
                    table.CheckConstraint("CK_SalesOrders_Status", "\"Status\" IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_SalesOrders_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_Godowns_GodownId",
                        column: x => x.GodownId,
                        principalTable: "Godowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_PriceLevels_PriceLevelId",
                        column: x => x.PriceLevelId,
                        principalTable: "PriceLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesOrders_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesQuotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    GodownId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceLevelId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuotationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConvertedToSalesOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConvertedToSaleId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesQuotations", x => x.Id);
                    table.CheckConstraint("CK_SalesQuotations_Money_NonNegative", "\"Subtotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"NetTotal\" >= 0");
                    table.CheckConstraint("CK_SalesQuotations_Status", "\"Status\" IN (1, 2, 3, 4, 5, 6, 7)");
                    table.ForeignKey(
                        name: "FK_SalesQuotations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesQuotations_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesQuotations_Godowns_GodownId",
                        column: x => x.GodownId,
                        principalTable: "Godowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesQuotations_PriceLevels_PriceLevelId",
                        column: x => x.PriceLevelId,
                        principalTable: "PriceLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SalesQuotations_SalesOrders_ConvertedToSalesOrderId",
                        column: x => x.ConvertedToSalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesQuotations_Sales_ConvertedToSaleId",
                        column: x => x.ConvertedToSaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesQuotations_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesQuotations_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesQuotationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesQuotationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesQuotationItems", x => x.Id);
                    table.CheckConstraint("CK_SalesQuotationItems_Discount_Range", "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100");
                    table.CheckConstraint("CK_SalesQuotationItems_Money_NonNegative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"DiscountAmount\" >= 0 AND \"NetAmount\" >= 0");
                    table.CheckConstraint("CK_SalesQuotationItems_Quantity_Positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_SalesQuotationItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesQuotationItems_SalesQuotations_SalesQuotationId",
                        column: x => x.SalesQuotationId,
                        principalTable: "SalesQuotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_PriceLevelId",
                table: "Sales",
                column: "PriceLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_QuotationId",
                table: "Sales",
                column: "QuotationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_SalesOrderId",
                table: "Sales",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_SaleType",
                table: "Sales",
                column: "SaleType");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sales_SaleType",
                table: "Sales",
                sql: "\"SaleType\" IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SaleItems_PriceSource",
                table: "SaleItems",
                sql: "\"PriceSource\" IN (1, 2, 3, 4, 5)");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CustomerType",
                table: "Customers",
                column: "CustomerType");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PriceLevelId",
                table: "Customers",
                column: "PriceLevelId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Customers_CustomerType",
                table: "Customers",
                sql: "\"CustomerType\" IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_PriceLevels_BranchId",
                table: "PriceLevels",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceLevels_Code",
                table: "PriceLevels",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceLevels_IsDefault",
                table: "PriceLevels",
                column: "IsDefault",
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPriceBreaks_PriceLevelId",
                table: "ProductPriceBreaks",
                column: "PriceLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPriceBreaks_ProductId_PriceLevelId_MinimumQuantity",
                table: "ProductPriceBreaks",
                columns: new[] { "ProductId", "PriceLevelId", "MinimumQuantity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPriceLevels_PriceLevelId",
                table: "ProductPriceLevels",
                column: "PriceLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPriceLevels_ProductId_PriceLevelId",
                table: "ProductPriceLevels",
                columns: new[] { "ProductId", "PriceLevelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderItems_ProductId",
                table: "SalesOrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderItems_SalesOrderId",
                table: "SalesOrderItems",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_BranchId_OrderDate",
                table: "SalesOrders",
                columns: new[] { "BranchId", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_ConfirmedByUserId",
                table: "SalesOrders",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CreatedByUserId",
                table: "SalesOrders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustomerId_OrderDate",
                table: "SalesOrders",
                columns: new[] { "CustomerId", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_GodownId",
                table: "SalesOrders",
                column: "GodownId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_OrderNumber",
                table: "SalesOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_PriceLevelId",
                table: "SalesOrders",
                column: "PriceLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_QuotationId",
                table: "SalesOrders",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Status",
                table: "SalesOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotationItems_ProductId",
                table: "SalesQuotationItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotationItems_SalesQuotationId",
                table: "SalesQuotationItems",
                column: "SalesQuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_ApprovedByUserId",
                table: "SalesQuotations",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_BranchId_QuotationDate",
                table: "SalesQuotations",
                columns: new[] { "BranchId", "QuotationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_ConvertedToSaleId",
                table: "SalesQuotations",
                column: "ConvertedToSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_ConvertedToSalesOrderId",
                table: "SalesQuotations",
                column: "ConvertedToSalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_CreatedByUserId",
                table: "SalesQuotations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_CustomerId_QuotationDate",
                table: "SalesQuotations",
                columns: new[] { "CustomerId", "QuotationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_GodownId",
                table: "SalesQuotations",
                column: "GodownId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_PriceLevelId",
                table: "SalesQuotations",
                column: "PriceLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_QuotationNumber",
                table: "SalesQuotations",
                column: "QuotationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_Status",
                table: "SalesQuotations",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_PriceLevels_PriceLevelId",
                table: "Customers",
                column: "PriceLevelId",
                principalTable: "PriceLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_PriceLevels_PriceLevelId",
                table: "Sales",
                column: "PriceLevelId",
                principalTable: "PriceLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_SalesOrders_SalesOrderId",
                table: "Sales",
                column: "SalesOrderId",
                principalTable: "SalesOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_SalesQuotations_QuotationId",
                table: "Sales",
                column: "QuotationId",
                principalTable: "SalesQuotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrderItems_SalesOrders_SalesOrderId",
                table: "SalesOrderItems",
                column: "SalesOrderId",
                principalTable: "SalesOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_SalesQuotations_QuotationId",
                table: "SalesOrders",
                column: "QuotationId",
                principalTable: "SalesQuotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                INSERT INTO "PriceLevels" ("Id", "Name", "Code", "Priority", "IsDefault", "IsActive", "BranchId", "CreatedAt", "UpdatedAt") VALUES
                (gen_random_uuid(), 'Retail', 'RETAIL', 1, true, true, NULL, now(), now()),
                (gen_random_uuid(), 'Wholesale', 'WHOLESALE', 2, false, true, NULL, now(), now()),
                (gen_random_uuid(), 'Trade', 'TRADE', 3, false, true, NULL, now(), now());

                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt") VALUES
                (gen_random_uuid(), 'quotations.view', 'View sales quotations', 'quotations', now(), now()),
                (gen_random_uuid(), 'quotations.create', 'Create and edit draft sales quotations', 'quotations', now(), now()),
                (gen_random_uuid(), 'quotations.update', 'Edit draft sales quotations', 'quotations', now(), now()),
                (gen_random_uuid(), 'quotations.send', 'Mark a quotation as sent to the customer', 'quotations', now(), now()),
                (gen_random_uuid(), 'quotations.accept', 'Record a customer accepting or rejecting a quotation', 'quotations', now(), now()),
                (gen_random_uuid(), 'quotations.cancel', 'Cancel a sales quotation', 'quotations', now(), now()),
                (gen_random_uuid(), 'quotations.convert', 'Convert a quotation into a sales order or sale', 'quotations', now(), now()),
                (gen_random_uuid(), 'sales_orders.view', 'View sales orders', 'sales_orders', now(), now()),
                (gen_random_uuid(), 'sales_orders.create', 'Create and edit draft sales orders', 'sales_orders', now(), now()),
                (gen_random_uuid(), 'sales_orders.update', 'Edit draft sales orders', 'sales_orders', now(), now()),
                (gen_random_uuid(), 'sales_orders.confirm', 'Confirm a draft sales order', 'sales_orders', now(), now()),
                (gen_random_uuid(), 'sales_orders.fulfill', 'Fulfill a confirmed sales order into a sale', 'sales_orders', now(), now()),
                (gen_random_uuid(), 'sales_orders.cancel', 'Cancel a sales order', 'sales_orders', now(), now()),
                (gen_random_uuid(), 'sales.wholesale', 'Access the wholesale sales workspace', 'sales', now(), now()),
                (gen_random_uuid(), 'sales.credit_limit_override', 'Post a credit sale that exceeds the customer credit limit', 'sales', now(), now()),
                (gen_random_uuid(), 'sales.discount_override', 'Apply a line discount beyond the product maximum', 'sales', now(), now()),
                (gen_random_uuid(), 'sales.price_override', 'Manually override a resolved selling price', 'sales', now(), now()),
                (gen_random_uuid(), 'sales.sell_below_cost', 'Sell at a price below the batch cost', 'sales', now(), now()),
                (gen_random_uuid(), 'sales.cost_view', 'View cost/margin figures on sales', 'sales', now(), now()),
                (gen_random_uuid(), 'pricing.view', 'View price levels and product pricing', 'pricing', now(), now()),
                (gen_random_uuid(), 'pricing.manage', 'Manage price levels, product pricing, and quantity breaks', 'pricing', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('Owner', 'quotations.view'), ('Owner', 'quotations.create'), ('Owner', 'quotations.update'), ('Owner', 'quotations.send'),
                    ('Owner', 'quotations.accept'), ('Owner', 'quotations.cancel'), ('Owner', 'quotations.convert'),
                    ('Owner', 'sales_orders.view'), ('Owner', 'sales_orders.create'), ('Owner', 'sales_orders.update'),
                    ('Owner', 'sales_orders.confirm'), ('Owner', 'sales_orders.fulfill'), ('Owner', 'sales_orders.cancel'),
                    ('Owner', 'sales.wholesale'), ('Owner', 'sales.credit_limit_override'), ('Owner', 'sales.discount_override'),
                    ('Owner', 'sales.price_override'), ('Owner', 'sales.sell_below_cost'), ('Owner', 'sales.cost_view'),
                    ('Owner', 'pricing.view'), ('Owner', 'pricing.manage'),
                    ('Manager', 'quotations.view'), ('Manager', 'quotations.create'), ('Manager', 'quotations.update'), ('Manager', 'quotations.send'),
                    ('Manager', 'quotations.accept'), ('Manager', 'quotations.cancel'), ('Manager', 'quotations.convert'),
                    ('Manager', 'sales_orders.view'), ('Manager', 'sales_orders.create'), ('Manager', 'sales_orders.update'),
                    ('Manager', 'sales_orders.confirm'), ('Manager', 'sales_orders.fulfill'), ('Manager', 'sales_orders.cancel'),
                    ('Manager', 'sales.wholesale'), ('Manager', 'sales.credit_limit_override'), ('Manager', 'sales.discount_override'),
                    ('Manager', 'sales.price_override'), ('Manager', 'sales.sell_below_cost'), ('Manager', 'sales.cost_view'),
                    ('Manager', 'pricing.view'), ('Manager', 'pricing.manage'),
                    ('Cashier', 'quotations.view'), ('Cashier', 'quotations.create'), ('Cashier', 'quotations.update'), ('Cashier', 'quotations.send'),
                    ('Cashier', 'quotations.accept'), ('Cashier', 'quotations.convert'),
                    ('Cashier', 'sales_orders.view'), ('Cashier', 'sales_orders.create'), ('Cashier', 'sales_orders.update'),
                    ('Cashier', 'sales_orders.confirm'), ('Cashier', 'sales_orders.fulfill'),
                    ('Cashier', 'sales.wholesale'), ('Cashier', 'pricing.view'),
                    ('Pharmacist', 'quotations.view'), ('Pharmacist', 'quotations.create'), ('Pharmacist', 'quotations.update'), ('Pharmacist', 'quotations.send'),
                    ('Pharmacist', 'quotations.accept'), ('Pharmacist', 'quotations.convert'),
                    ('Pharmacist', 'sales_orders.view'), ('Pharmacist', 'sales_orders.create'), ('Pharmacist', 'sales_orders.update'),
                    ('Pharmacist', 'sales_orders.confirm'), ('Pharmacist', 'sales_orders.fulfill'),
                    ('Pharmacist', 'sales.wholesale'), ('Pharmacist', 'pricing.view'),
                    ('Accountant', 'quotations.view'), ('Accountant', 'sales_orders.view'), ('Accountant', 'sales.cost_view'), ('Accountant', 'pricing.view'),
                    ('PurchaseManager', 'quotations.view'), ('PurchaseManager', 'sales_orders.view'), ('PurchaseManager', 'pricing.view'),
                    ('StoreKeeper', 'quotations.view'), ('StoreKeeper', 'sales_orders.view'), ('StoreKeeper', 'pricing.view')
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
                    (SELECT "Id" FROM "Permissions" WHERE "Code" LIKE 'quotations.%' OR "Code" LIKE 'sales_orders.%' OR "Code" LIKE 'pricing.%'
                        OR "Code" IN ('sales.wholesale', 'sales.credit_limit_override', 'sales.discount_override', 'sales.price_override', 'sales.sell_below_cost', 'sales.cost_view'));
                DELETE FROM "Permissions" WHERE "Code" LIKE 'quotations.%' OR "Code" LIKE 'sales_orders.%' OR "Code" LIKE 'pricing.%'
                    OR "Code" IN ('sales.wholesale', 'sales.credit_limit_override', 'sales.discount_override', 'sales.price_override', 'sales.sell_below_cost', 'sales.cost_view');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_PriceLevels_PriceLevelId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_PriceLevels_PriceLevelId",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_SalesOrders_SalesOrderId",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_SalesQuotations_QuotationId",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_PriceLevels_PriceLevelId",
                table: "SalesOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesQuotations_PriceLevels_PriceLevelId",
                table: "SalesQuotations");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesQuotations_SalesOrders_ConvertedToSalesOrderId",
                table: "SalesQuotations");

            migrationBuilder.DropTable(
                name: "ProductPriceBreaks");

            migrationBuilder.DropTable(
                name: "ProductPriceLevels");

            migrationBuilder.DropTable(
                name: "SalesOrderItems");

            migrationBuilder.DropTable(
                name: "SalesQuotationItems");

            migrationBuilder.DropTable(
                name: "PriceLevels");

            migrationBuilder.DropTable(
                name: "SalesOrders");

            migrationBuilder.DropTable(
                name: "SalesQuotations");

            migrationBuilder.DropIndex(
                name: "IX_Sales_PriceLevelId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_QuotationId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_SalesOrderId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_SaleType",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sales_SaleType",
                table: "Sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SaleItems_PriceSource",
                table: "SaleItems");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CustomerType",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_PriceLevelId",
                table: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Customers_CustomerType",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomerPoNumber",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PriceLevelId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "QuotationId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "SaleType",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "SalesOrderId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "BelowCostOverrideReason",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "DiscountOverrideReason",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "IsBelowCost",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "IsDiscountOverride",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "IsManualPriceOverride",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "PriceOverrideReason",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "PriceSource",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ResolvedUnitPrice",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "ContactPerson",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CreditAllowed",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CustomerType",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PriceLevelId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ShippingAddress",
                table: "Customers");

            migrationBuilder.DropSequence(
                name: "QuotationNumberSequence");

            migrationBuilder.DropSequence(
                name: "SalesOrderNumberSequence");
        }
    }
}
