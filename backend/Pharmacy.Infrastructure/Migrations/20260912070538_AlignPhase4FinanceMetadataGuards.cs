using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharmacy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignPhase4FinanceMetadataGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION pharmacy_phase4_ledger_metadata_guard() RETURNS trigger AS $$
                DECLARE reconciliation_status integer; reconciliation_account uuid; statement_end date;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Posted financial history is immutable' USING ERRCODE = '23514';
                    END IF;
                    IF (to_jsonb(NEW) - ARRAY['BankReconciliationId','ReconciledAtUtc','UpdatedAt'])
                        IS DISTINCT FROM (to_jsonb(OLD) - ARRAY['BankReconciliationId','ReconciledAtUtc','UpdatedAt']) THEN
                        RAISE EXCEPTION 'Posted financial amounts and attribution are immutable' USING ERRCODE = '23514';
                    END IF;
                    IF NEW."BankReconciliationId" IS NOT DISTINCT FROM OLD."BankReconciliationId"
                        AND NEW."ReconciledAtUtc" IS NOT DISTINCT FROM OLD."ReconciledAtUtc" THEN RETURN NEW; END IF;
                    IF OLD."BankReconciliationId" IS NOT NULL THEN
                        SELECT "Status" INTO reconciliation_status FROM "BankReconciliations"
                            WHERE "Id" = OLD."BankReconciliationId" FOR UPDATE;
                        IF reconciliation_status = 2 THEN
                            RAISE EXCEPTION 'Finalized reconciliation lines are locked; reopen first' USING ERRCODE = '23514';
                        END IF;
                        IF NEW."BankReconciliationId" IS NOT NULL AND NEW."BankReconciliationId" <> OLD."BankReconciliationId" THEN
                            RAISE EXCEPTION 'Unmatch an entry before matching it to another reconciliation' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    IF NEW."BankReconciliationId" IS NOT NULL THEN
                        SELECT "Status", "FinancialAccountId", "StatementEndDate"
                            INTO reconciliation_status, reconciliation_account, statement_end FROM "BankReconciliations"
                            WHERE "Id" = NEW."BankReconciliationId" FOR UPDATE;
                        IF reconciliation_status <> 1 OR reconciliation_account <> NEW."FinancialAccountId"
                            OR NEW."OccurredAtUtc"::date > statement_end OR NEW."ReconciledAtUtc" IS NULL THEN
                            RAISE EXCEPTION 'Invalid reconciliation match' USING ERRCODE = '23514';
                        END IF;
                    ELSIF NEW."ReconciledAtUtc" IS NOT NULL THEN
                        RAISE EXCEPTION 'Unmatched entries cannot have a reconciliation timestamp' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END; $$ LANGUAGE plpgsql;

                CREATE FUNCTION pharmacy_phase4_reversal_metadata_guard() RETURNS trigger AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Posted financial history is immutable' USING ERRCODE = '23514';
                    END IF;
                    IF (to_jsonb(NEW) - ARRAY['ReversedAtUtc','ReversedByUserId','ReversalReason','UpdatedAt'])
                        IS DISTINCT FROM (to_jsonb(OLD) - ARRAY['ReversedAtUtc','ReversedByUserId','ReversalReason','UpdatedAt']) THEN
                        RAISE EXCEPTION 'Posted financial document amounts are immutable' USING ERRCODE = '23514';
                    END IF;
                    IF NEW."ReversedAtUtc" IS NOT DISTINCT FROM OLD."ReversedAtUtc"
                        AND NEW."ReversedByUserId" IS NOT DISTINCT FROM OLD."ReversedByUserId"
                        AND NEW."ReversalReason" IS NOT DISTINCT FROM OLD."ReversalReason" THEN RETURN NEW; END IF;
                    IF OLD."ReversedAtUtc" IS NOT NULL OR NEW."ReversedAtUtc" IS NULL
                        OR NEW."ReversedByUserId" IS NULL OR NULLIF(btrim(NEW."ReversalReason"), '') IS NULL THEN
                        RAISE EXCEPTION 'Reversal metadata can only be recorded once with a user and reason' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END; $$ LANGUAGE plpgsql;

                DROP TRIGGER "TR_FinancialLedgerEntries_Immutable" ON "FinancialLedgerEntries";
                CREATE TRIGGER "TR_FinancialLedgerEntries_Immutable" BEFORE UPDATE OR DELETE ON "FinancialLedgerEntries"
                    FOR EACH ROW EXECUTE FUNCTION pharmacy_phase4_ledger_metadata_guard();
                DROP TRIGGER "TR_Expenses_Immutable" ON "Expenses";
                CREATE TRIGGER "TR_Expenses_Immutable" BEFORE UPDATE OR DELETE ON "Expenses"
                    FOR EACH ROW EXECUTE FUNCTION pharmacy_phase4_reversal_metadata_guard();
                DROP TRIGGER "TR_OtherIncomes_Immutable" ON "OtherIncomes";
                CREATE TRIGGER "TR_OtherIncomes_Immutable" BEFORE UPDATE OR DELETE ON "OtherIncomes"
                    FOR EACH ROW EXECUTE FUNCTION pharmacy_phase4_reversal_metadata_guard();
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER "TR_FinancialLedgerEntries_Immutable" ON "FinancialLedgerEntries";
                CREATE TRIGGER "TR_FinancialLedgerEntries_Immutable" BEFORE UPDATE OR DELETE ON "FinancialLedgerEntries"
                    FOR EACH ROW EXECUTE FUNCTION pharmacy_immutable_finance();
                DROP TRIGGER "TR_Expenses_Immutable" ON "Expenses";
                CREATE TRIGGER "TR_Expenses_Immutable" BEFORE UPDATE OR DELETE ON "Expenses"
                    FOR EACH ROW EXECUTE FUNCTION pharmacy_immutable_finance();
                DROP TRIGGER "TR_OtherIncomes_Immutable" ON "OtherIncomes";
                CREATE TRIGGER "TR_OtherIncomes_Immutable" BEFORE UPDATE OR DELETE ON "OtherIncomes"
                    FOR EACH ROW EXECUTE FUNCTION pharmacy_immutable_finance();
                DROP FUNCTION pharmacy_phase4_ledger_metadata_guard();
                DROP FUNCTION pharmacy_phase4_reversal_metadata_guard();
                """);

        }
    }
}
