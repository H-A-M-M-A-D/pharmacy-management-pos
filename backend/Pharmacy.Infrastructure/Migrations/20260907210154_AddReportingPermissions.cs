using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt") VALUES
                ('20000000-0000-0000-0000-000000000078', 'reports.view', 'View reporting dashboard', 'reports', now(), now()),
                ('20000000-0000-0000-0000-000000000079', 'reports.sales', 'View sales reports', 'reports', now(), now()),
                ('20000000-0000-0000-0000-000000000080', 'reports.purchases', 'View purchasing reports', 'reports', now(), now()),
                ('20000000-0000-0000-0000-000000000081', 'reports.inventory', 'View inventory reports', 'reports', now(), now()),
                ('20000000-0000-0000-0000-000000000082', 'reports.financial', 'View operational financial reports', 'reports', now(), now()),
                ('20000000-0000-0000-0000-000000000083', 'reports.profitability', 'View cost and profitability reports', 'reports', now(), now()),
                ('20000000-0000-0000-0000-000000000084', 'reports.export', 'Export authorized reports', 'reports', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT mapping.id, role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('30000000-0000-0000-0000-000000000260'::uuid, 'Owner', 'reports.view'),
                    ('30000000-0000-0000-0000-000000000261'::uuid, 'Owner', 'reports.sales'),
                    ('30000000-0000-0000-0000-000000000262'::uuid, 'Owner', 'reports.purchases'),
                    ('30000000-0000-0000-0000-000000000263'::uuid, 'Owner', 'reports.inventory'),
                    ('30000000-0000-0000-0000-000000000264'::uuid, 'Owner', 'reports.financial'),
                    ('30000000-0000-0000-0000-000000000265'::uuid, 'Owner', 'reports.profitability'),
                    ('30000000-0000-0000-0000-000000000266'::uuid, 'Owner', 'reports.export'),
                    ('30000000-0000-0000-0000-000000000267'::uuid, 'Manager', 'reports.view'),
                    ('30000000-0000-0000-0000-000000000268'::uuid, 'Manager', 'reports.sales'),
                    ('30000000-0000-0000-0000-000000000269'::uuid, 'Manager', 'reports.purchases'),
                    ('30000000-0000-0000-0000-000000000270'::uuid, 'Manager', 'reports.inventory'),
                    ('30000000-0000-0000-0000-000000000271'::uuid, 'Manager', 'reports.financial'),
                    ('30000000-0000-0000-0000-000000000272'::uuid, 'Manager', 'reports.profitability'),
                    ('30000000-0000-0000-0000-000000000273'::uuid, 'Manager', 'reports.export'),
                    ('30000000-0000-0000-0000-000000000274'::uuid, 'Accountant', 'reports.view'),
                    ('30000000-0000-0000-0000-000000000275'::uuid, 'Accountant', 'reports.sales'),
                    ('30000000-0000-0000-0000-000000000276'::uuid, 'Accountant', 'reports.purchases'),
                    ('30000000-0000-0000-0000-000000000277'::uuid, 'Accountant', 'reports.financial'),
                    ('30000000-0000-0000-0000-000000000278'::uuid, 'Accountant', 'reports.export'),
                    ('30000000-0000-0000-0000-000000000279'::uuid, 'PurchaseManager', 'reports.view'),
                    ('30000000-0000-0000-0000-000000000280'::uuid, 'PurchaseManager', 'reports.purchases'),
                    ('30000000-0000-0000-0000-000000000281'::uuid, 'PurchaseManager', 'reports.inventory'),
                    ('30000000-0000-0000-0000-000000000282'::uuid, 'PurchaseManager', 'reports.export'),
                    ('30000000-0000-0000-0000-000000000283'::uuid, 'StoreKeeper', 'reports.view'),
                    ('30000000-0000-0000-0000-000000000284'::uuid, 'StoreKeeper', 'reports.inventory'),
                    ('30000000-0000-0000-0000-000000000285'::uuid, 'StoreKeeper', 'reports.export'),
                    ('30000000-0000-0000-0000-000000000286'::uuid, 'Cashier', 'reports.sales'),
                    ('30000000-0000-0000-0000-000000000287'::uuid, 'Pharmacist', 'reports.inventory')
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
                    (SELECT "Id" FROM "Permissions" WHERE "Code" LIKE 'reports.%');
                DELETE FROM "Permissions" WHERE "Code" LIKE 'reports.%';
                """);
        }
    }
}
