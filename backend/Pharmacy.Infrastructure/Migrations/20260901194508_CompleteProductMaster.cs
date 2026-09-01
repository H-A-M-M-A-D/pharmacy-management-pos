using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteProductMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Barcode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SKU",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductCategories_Name",
                table: "ProductCategories");

            migrationBuilder.DropIndex(
                name: "IX_Manufacturers_Name",
                table: "Manufacturers");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedBarcode",
                table: "Products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedSku",
                table: "Products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "ProductCategories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Manufacturers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "Manufacturers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Manufacturers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Products"
                SET "NormalizedSku" = upper(btrim("SKU")),
                    "NormalizedBarcode" = CASE WHEN "Barcode" IS NULL OR btrim("Barcode") = '' THEN NULL ELSE upper(btrim("Barcode")) END;
                UPDATE "ProductCategories" SET "NormalizedName" = upper(btrim("Name"));
                UPDATE "Manufacturers" SET "NormalizedName" = upper(btrim("Name"));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Products_BrandName",
                table: "Products",
                column: "BrandName");

            migrationBuilder.CreateIndex(
                name: "IX_Products_NormalizedBarcode",
                table: "Products",
                column: "NormalizedBarcode",
                unique: true,
                filter: "\"NormalizedBarcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_NormalizedSku",
                table: "Products",
                column: "NormalizedSku",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Discount_Range",
                table: "Products",
                sql: "\"MaximumDiscountPercent\" >= 0 AND \"MaximumDiscountPercent\" <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_PackSize_Positive",
                table: "Products",
                sql: "\"PackSize\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Prices_NonNegative",
                table: "Products",
                sql: "\"PurchasePrice\" >= 0 AND \"RetailPrice\" >= 0 AND (\"TradePrice\" IS NULL OR \"TradePrice\" >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_ReorderLevel_NonNegative",
                table: "Products",
                sql: "\"ReorderLevel\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_NormalizedName",
                table: "ProductCategories",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Manufacturers_NormalizedName",
                table: "Manufacturers",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt")
                VALUES
                    ('20000000-0000-0000-0000-000000000015', 'products.view', 'View products', 'products', now(), now()),
                    ('20000000-0000-0000-0000-000000000016', 'products.create', 'Create products', 'products', now(), now()),
                    ('20000000-0000-0000-0000-000000000017', 'products.update', 'Update products', 'products', now(), now()),
                    ('20000000-0000-0000-0000-000000000018', 'products.activate', 'Activate products', 'products', now(), now()),
                    ('20000000-0000-0000-0000-000000000019', 'products.deactivate', 'Deactivate products', 'products', now(), now()),
                    ('20000000-0000-0000-0000-000000000020', 'categories.view', 'View product categories', 'catalog', now(), now()),
                    ('20000000-0000-0000-0000-000000000021', 'categories.manage', 'Manage product categories', 'catalog', now(), now()),
                    ('20000000-0000-0000-0000-000000000022', 'manufacturers.view', 'View manufacturers', 'catalog', now(), now()),
                    ('20000000-0000-0000-0000-000000000023', 'manufacturers.manage', 'Manage manufacturers', 'catalog', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000042'::uuid, 'Owner', 'products.view'),
                    ('30000000-0000-0000-0000-000000000043'::uuid, 'Owner', 'products.create'),
                    ('30000000-0000-0000-0000-000000000044'::uuid, 'Owner', 'products.update'),
                    ('30000000-0000-0000-0000-000000000045'::uuid, 'Owner', 'products.activate'),
                    ('30000000-0000-0000-0000-000000000046'::uuid, 'Owner', 'products.deactivate'),
                    ('30000000-0000-0000-0000-000000000047'::uuid, 'Owner', 'categories.view'),
                    ('30000000-0000-0000-0000-000000000048'::uuid, 'Owner', 'categories.manage'),
                    ('30000000-0000-0000-0000-000000000049'::uuid, 'Owner', 'manufacturers.view'),
                    ('30000000-0000-0000-0000-000000000050'::uuid, 'Owner', 'manufacturers.manage'),
                    ('30000000-0000-0000-0000-000000000051'::uuid, 'Manager', 'products.view'),
                    ('30000000-0000-0000-0000-000000000052'::uuid, 'Manager', 'products.create'),
                    ('30000000-0000-0000-0000-000000000053'::uuid, 'Manager', 'products.update'),
                    ('30000000-0000-0000-0000-000000000054'::uuid, 'Manager', 'products.activate'),
                    ('30000000-0000-0000-0000-000000000055'::uuid, 'Manager', 'products.deactivate'),
                    ('30000000-0000-0000-0000-000000000056'::uuid, 'Manager', 'categories.view'),
                    ('30000000-0000-0000-0000-000000000057'::uuid, 'Manager', 'categories.manage'),
                    ('30000000-0000-0000-0000-000000000058'::uuid, 'Manager', 'manufacturers.view'),
                    ('30000000-0000-0000-0000-000000000059'::uuid, 'Manager', 'manufacturers.manage'),
                    ('30000000-0000-0000-0000-000000000060'::uuid, 'Pharmacist', 'products.view'),
                    ('30000000-0000-0000-0000-000000000061'::uuid, 'Pharmacist', 'products.update'),
                    ('30000000-0000-0000-0000-000000000062'::uuid, 'Pharmacist', 'categories.view'),
                    ('30000000-0000-0000-0000-000000000063'::uuid, 'Pharmacist', 'manufacturers.view'),
                    ('30000000-0000-0000-0000-000000000064'::uuid, 'Cashier', 'products.view'),
                    ('30000000-0000-0000-0000-000000000065'::uuid, 'PurchaseManager', 'products.view'),
                    ('30000000-0000-0000-0000-000000000066'::uuid, 'PurchaseManager', 'products.create'),
                    ('30000000-0000-0000-0000-000000000067'::uuid, 'PurchaseManager', 'products.update'),
                    ('30000000-0000-0000-0000-000000000068'::uuid, 'PurchaseManager', 'categories.view'),
                    ('30000000-0000-0000-0000-000000000069'::uuid, 'PurchaseManager', 'manufacturers.view'),
                    ('30000000-0000-0000-0000-000000000070'::uuid, 'StoreKeeper', 'products.view'),
                    ('30000000-0000-0000-0000-000000000071'::uuid, 'StoreKeeper', 'categories.view'),
                    ('30000000-0000-0000-0000-000000000072'::uuid, 'StoreKeeper', 'manufacturers.view')
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
                        ('products.view','products.create','products.update','products.activate','products.deactivate',
                         'categories.view','categories.manage','manufacturers.view','manufacturers.manage'));
                DELETE FROM "Permissions" WHERE "Code" IN
                    ('products.view','products.create','products.update','products.activate','products.deactivate',
                     'categories.view','categories.manage','manufacturers.view','manufacturers.manage');
                """);
            migrationBuilder.DropIndex(
                name: "IX_Products_BrandName",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_NormalizedBarcode",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_NormalizedSku",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Discount_Range",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_PackSize_Positive",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Prices_NonNegative",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_ReorderLevel_NonNegative",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductCategories_NormalizedName",
                table: "ProductCategories");

            migrationBuilder.DropIndex(
                name: "IX_Manufacturers_NormalizedName",
                table: "Manufacturers");

            migrationBuilder.DropColumn(
                name: "NormalizedBarcode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NormalizedSku",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "ProductCategories");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Manufacturers");

            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "Manufacturers");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Manufacturers");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_SKU",
                table: "Products",
                column: "SKU",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_Name",
                table: "ProductCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Manufacturers_Name",
                table: "Manufacturers",
                column: "Name");
        }
    }
}
