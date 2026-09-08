# Deployment Checklist

## Host and Database

- Install supported .NET runtime, PostgreSQL 17, and matching `pg_dump`/`pg_restore` tools.
- Create separate PostgreSQL migration/maintenance and application roles. The runtime role must not have `SUPERUSER`, `CREATEDB`, `CREATEROLE`, replication, or bypass-RLS privileges. The locally verified maintenance role has `LOGIN` and `CREATEDB` only; it is supplied externally through `PHARMACY_MAINTENANCE_CONNECTION_STRING` for isolated restore validation and is never an application runtime credential.
- Supply `ConnectionStrings__DefaultConnection` and `Jwt__Key` from the Windows service environment or another external secret provider.
- Set explicit `Cors__AllowedOrigins__0` values only when browser clients require cross-origin access.
- Apply migrations as a controlled deployment step. The API intentionally does not auto-migrate.

## Release

- Publish the API with `dotnet publish backend/Pharmacy.Api -c Release -o artifacts/api`.
- Build Flutter with `flutter build windows --release`; set `PHARMACY_API_URL` in the launched process environment for the deployed API address.
- Run the API under a dedicated, non-interactive Windows service account using a service manager such as Windows Service Control Manager or NSSM. Grant read/execute access to the publish directory and write access only to the backup/log destinations it needs.
- Configure HTTPS at Kestrel or a trusted reverse proxy. Never disable certificate validation in the client.
- Configure firewall rules only for the intended LAN clients and API port.

## Commissioning

- Verify `/api/health/live` and `/api/health/ready`.
- Complete first-Owner setup once, then create the branch and financial accounts.
- Verify login, permission denial, receipt printer, barcode scanner, and workstation navigation.
- Create a backup, validate its archive, restore it into an isolated database, and record the operator-defined RPO/RTO goals.
