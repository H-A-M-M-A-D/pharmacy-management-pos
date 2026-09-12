using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedPhase4Permissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Id", "Code", "Description", "Category", "CreatedAt", "UpdatedAt") VALUES
                (gen_random_uuid(), 'accounts.periods.view', 'View accounting periods', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.periods.manage', 'Create and edit accounting periods', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.periods.close', 'Close an accounting period or fiscal year', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.periods.reopen', 'Reopen a closed accounting period or fiscal year', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.post_to_soft_closed', 'Post into a soft-closed accounting period', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.journal.reverse', 'Reverse a posted journal entry', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.recurring.view', 'View recurring journal templates', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.recurring.manage', 'Manage recurring journal templates and generate due entries', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.reconciliation.view', 'View bank reconciliations and control-account reconciliation reports', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.reconciliation.manage', 'Create, finalize, and reopen bank reconciliations', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.budgets.view', 'View budgets and budget vs actual reports', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.budgets.manage', 'Create and edit budgets', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.cost_centers.view', 'View cost centers', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.cost_centers.manage', 'Create and edit cost centers', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.credit_notes.view', 'View customer credit notes', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.credit_notes.create', 'Issue a customer credit note', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.debit_notes.view', 'View supplier debit notes', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.debit_notes.create', 'Issue a supplier debit note', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.writeoffs.view', 'View customer and supplier write-offs', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.writeoffs.create', 'Write off a customer or supplier balance', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.advances.view', 'View customer and supplier advances', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.advances.create', 'Record a customer or supplier advance', 'accounting', now(), now()),
                (gen_random_uuid(), 'accounts.advances.apply', 'Apply an advance to a specific invoice or bill', 'accounting', now(), now())
                ON CONFLICT ("Code") DO NOTHING;

                INSERT INTO "RolePermissions" ("Id", "RoleId", "PermissionId", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), role."Id", permission."Id", now(), now()
                FROM (VALUES
                    ('Owner', 'accounts.periods.view'), ('Owner', 'accounts.periods.manage'), ('Owner', 'accounts.periods.close'), ('Owner', 'accounts.periods.reopen'),
                    ('Owner', 'accounts.post_to_soft_closed'), ('Owner', 'accounts.journal.reverse'),
                    ('Owner', 'accounts.recurring.view'), ('Owner', 'accounts.recurring.manage'),
                    ('Owner', 'accounts.reconciliation.view'), ('Owner', 'accounts.reconciliation.manage'),
                    ('Owner', 'accounts.budgets.view'), ('Owner', 'accounts.budgets.manage'),
                    ('Owner', 'accounts.cost_centers.view'), ('Owner', 'accounts.cost_centers.manage'),
                    ('Owner', 'accounts.credit_notes.view'), ('Owner', 'accounts.credit_notes.create'),
                    ('Owner', 'accounts.debit_notes.view'), ('Owner', 'accounts.debit_notes.create'),
                    ('Owner', 'accounts.writeoffs.view'), ('Owner', 'accounts.writeoffs.create'),
                    ('Owner', 'accounts.advances.view'), ('Owner', 'accounts.advances.create'), ('Owner', 'accounts.advances.apply'),

                    ('Manager', 'accounts.periods.view'), ('Manager', 'accounts.periods.manage'), ('Manager', 'accounts.periods.close'), ('Manager', 'accounts.periods.reopen'),
                    ('Manager', 'accounts.post_to_soft_closed'), ('Manager', 'accounts.journal.reverse'),
                    ('Manager', 'accounts.recurring.view'), ('Manager', 'accounts.recurring.manage'),
                    ('Manager', 'accounts.reconciliation.view'), ('Manager', 'accounts.reconciliation.manage'),
                    ('Manager', 'accounts.budgets.view'), ('Manager', 'accounts.budgets.manage'),
                    ('Manager', 'accounts.cost_centers.view'), ('Manager', 'accounts.cost_centers.manage'),
                    ('Manager', 'accounts.credit_notes.view'), ('Manager', 'accounts.credit_notes.create'),
                    ('Manager', 'accounts.debit_notes.view'), ('Manager', 'accounts.debit_notes.create'),
                    ('Manager', 'accounts.writeoffs.view'), ('Manager', 'accounts.writeoffs.create'),
                    ('Manager', 'accounts.advances.view'), ('Manager', 'accounts.advances.create'), ('Manager', 'accounts.advances.apply'),

                    ('Accountant', 'accounts.periods.view'), ('Accountant', 'accounts.periods.manage'), ('Accountant', 'accounts.periods.close'), ('Accountant', 'accounts.periods.reopen'),
                    ('Accountant', 'accounts.post_to_soft_closed'), ('Accountant', 'accounts.journal.reverse'),
                    ('Accountant', 'accounts.recurring.view'), ('Accountant', 'accounts.recurring.manage'),
                    ('Accountant', 'accounts.reconciliation.view'), ('Accountant', 'accounts.reconciliation.manage'),
                    ('Accountant', 'accounts.budgets.view'), ('Accountant', 'accounts.budgets.manage'),
                    ('Accountant', 'accounts.cost_centers.view'), ('Accountant', 'accounts.cost_centers.manage'),
                    ('Accountant', 'accounts.credit_notes.view'), ('Accountant', 'accounts.credit_notes.create'),
                    ('Accountant', 'accounts.debit_notes.view'), ('Accountant', 'accounts.debit_notes.create'),
                    ('Accountant', 'accounts.writeoffs.view'), ('Accountant', 'accounts.writeoffs.create'),
                    ('Accountant', 'accounts.advances.view'), ('Accountant', 'accounts.advances.create'), ('Accountant', 'accounts.advances.apply')
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
                    (SELECT "Id" FROM "Permissions" WHERE "Code" LIKE 'accounts.periods.%' OR "Code" LIKE 'accounts.recurring.%'
                        OR "Code" LIKE 'accounts.reconciliation.%' OR "Code" LIKE 'accounts.budgets.%' OR "Code" LIKE 'accounts.cost_centers.%'
                        OR "Code" LIKE 'accounts.credit_notes.%' OR "Code" LIKE 'accounts.debit_notes.%' OR "Code" LIKE 'accounts.writeoffs.%'
                        OR "Code" LIKE 'accounts.advances.%' OR "Code" IN ('accounts.post_to_soft_closed', 'accounts.journal.reverse'));
                DELETE FROM "Permissions" WHERE "Code" LIKE 'accounts.periods.%' OR "Code" LIKE 'accounts.recurring.%'
                    OR "Code" LIKE 'accounts.reconciliation.%' OR "Code" LIKE 'accounts.budgets.%' OR "Code" LIKE 'accounts.cost_centers.%'
                    OR "Code" LIKE 'accounts.credit_notes.%' OR "Code" LIKE 'accounts.debit_notes.%' OR "Code" LIKE 'accounts.writeoffs.%'
                    OR "Code" LIKE 'accounts.advances.%' OR "Code" IN ('accounts.post_to_soft_closed', 'accounts.journal.reverse');
                """);
        }
    }
}
