using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceAuditImmutability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_audit_log_mutation()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'Audit events are append-only.' USING ERRCODE = '23514';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_AuditLogs_AppendOnly"
                BEFORE UPDATE OR DELETE ON "AuditLogs"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_log_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TR_AuditLogs_AppendOnly" ON "AuditLogs";
                DROP FUNCTION IF EXISTS prevent_audit_log_mutation();
                """);
        }
    }
}
