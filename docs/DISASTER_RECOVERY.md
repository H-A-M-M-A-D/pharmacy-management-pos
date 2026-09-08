# Disaster Recovery

## Backup Strategy

Keep multiple custom-format PostgreSQL backups on storage separate from the database host. The built-in local retention default is 10 completed archives; operators must copy verified archives to protected storage and periodically test restoration. RPO and RTO are operator-defined goals, not software guarantees.

## Restore Validation

Use `pg_restore --list` first. Create an isolated database with a generated validation name, restore into it, confirm connectivity, `__EFMigrationsHistory`, the latest migration, core tables, and representative ledger/document row counts, then drop the validation database.

Use the external maintenance identity only for creating, validating, and dropping the isolated database. It should have `LOGIN` and `CREATEDB`, but not `SUPERUSER`, `CREATEROLE`, replication, or bypass-RLS. The normal `pharmacy_app_dev` runtime identity must remain without `CREATEDB`.

## Live Recovery

1. Stop the API and all writers.
2. Preserve the current database and create a safety backup when possible.
3. Create a clean target database; do not overwrite the live database blindly.
4. Restore the selected archive using externally supplied administrator credentials.
5. Verify migrations, schema, constraints, ledgers, and row-count sanity.
6. Configure runtime secrets and point the API to the recovered target.
7. Start the API and verify liveness/readiness.
8. Validate login and one read-only critical workflow before reopening operations.

Never manually alter stock movements, financial/customer/supplier ledgers, posted documents, audit logs, or migration history.
