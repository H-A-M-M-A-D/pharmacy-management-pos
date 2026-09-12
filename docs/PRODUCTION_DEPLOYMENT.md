# Production Deployment Guide

Practical, step-by-step deployment of the Pharmacy Management System (backend: .NET 10 Web API + PostgreSQL; client: Flutter Windows desktop) onto a Windows + PostgreSQL environment. This is the narrative walkthrough; `docs/DEPLOYMENT_CHECKLIST.md` is the condensed checklist to tick off during the actual deployment, and `docs/RELEASE_CHECKLIST.md` covers cutting a release build. No real passwords are included below — every secret is a placeholder to replace.

## 1. Prerequisites

- Windows Server or Windows 10/11 host for the API (a Windows Service is the supported hosting model — see §6).
- .NET 10 ASP.NET Core Runtime (or the full SDK if you'll build on the host) installed on the API host.
- PostgreSQL 17 server (matching what this project's schema/constraints were verified against), plus its `pg_dump`/`pg_restore` client tools available on the API host for the backup feature (see §9).
- A Windows machine per pharmacy counter/office workstation for the Flutter desktop client (`flutter build windows --release` output).
- Network path between the API host and both PostgreSQL and every Flutter client (LAN, VPN, or equivalent — this system requires a live connection; see `docs/OPERATIONS_RUNBOOK.md` for offline-behavior notes).

## 2. PostgreSQL setup

Install PostgreSQL 17 on the database host. Create the application database and two distinct roles — never reuse the superuser for the application:

```sql
CREATE DATABASE pharmacy_prod;

-- Runtime role: what the API connects as day-to-day. No CREATEDB, no SUPERUSER, no CREATEROLE.
CREATE ROLE pharmacy_app WITH LOGIN PASSWORD '<choose-a-strong-password>';
GRANT ALL PRIVILEGES ON DATABASE pharmacy_prod TO pharmacy_app;

-- Maintenance role: used only for isolated restore validation (see docs/DISASTER_RECOVERY.md).
-- LOGIN + CREATEDB only. Never SUPERUSER, CREATEROLE, replication, or bypass-RLS.
CREATE ROLE pharmacy_maintenance WITH LOGIN CREATEDB PASSWORD '<a-different-strong-password>';
```

After connecting to `pharmacy_prod` as an administrator, grant schema privileges to `pharmacy_app` (EF Core migrations will create the actual tables — see §5):

```sql
\c pharmacy_prod
GRANT ALL ON SCHEMA public TO pharmacy_app;
```

## 3. Secure connection string

Build the runtime connection string for `pharmacy_app` and keep it out of any file that gets committed to source control:

```
Host=<db-host>;Port=5432;Database=pharmacy_prod;Username=pharmacy_app;Password=<strong-password>;SSL Mode=Require
```

Use `SSL Mode=Require` (or stronger) whenever the API and database are not on the same trusted host/network segment.

## 4. JWT secret

Generate a random signing key of at least 32 characters (a GUID or two concatenated is not recommended — use a real random string, e.g. `openssl rand -base64 48` or PowerShell `[Convert]::ToBase64String((1..48 | % {Get-Random -Max 256}))`). The API refuses to start if the key is missing, shorter than 32 characters, contains `change-this`, or starts with `<` (a common placeholder marker) — so a copy-pasted example value will fail fast rather than silently running insecurely.

## 5. Backend hosting and configuration

This project is a standard ASP.NET Core Web API (`backend/Pharmacy.Api`) with no built-in auto-migration and no bundled reverse proxy — you host it directly as a Windows Service (or IIS with the ASP.NET Core Module, if preferred) and terminate HTTPS at Kestrel or a reverse proxy in front of it.

Publish it:

```
dotnet publish backend/Pharmacy.Api -c Release -o C:\PharmacyPOS\api
```

Supply all secrets and environment-specific settings via environment variables on the Windows Service (never commit them to `appsettings.json`):

| Variable | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | The `pharmacy_app` connection string from §3 |
| `Jwt__Key` | The signing key from §4 |
| `Jwt__Issuer` / `Jwt__Audience` | Keep the shipped defaults (`PharmacySystem` / `PharmacySystemUsers`) unless you have a reason to change them |
| `Jwt__ExpirationMinutes` | Token lifetime (default 60) |
| `AuthenticationSecurity__MaximumFailedAttempts` / `AuthenticationSecurity__LockoutMinutes` | Lockout policy (defaults 5 / 15) |
| `Cors__AllowedOrigins__0` (and `__1`, `__2`, ...) | Only if a browser-based client needs cross-origin access; leave unset for the Flutter desktop client, which is not subject to CORS |
| `Backup__Directory` | Where backups are written (default `%LOCALAPPDATA%\PharmacyPOS\Backups` under the service account's profile — set an explicit path for a service account) |
| `Backup__RetentionCount` | How many completed backups to keep (default 10) |
| `Backup__MinimumFreeSpaceBytes` | Refuse to start a backup below this free-space threshold (default 100MB) |
| `Backup__PostgreSqlToolsPath` | Explicit folder containing `pg_dump.exe`/`pg_restore.exe`, if not resolvable via PATH or the default PostgreSQL install location |
| `Logging__FileDirectory` | Where rotated log files are written (default `%LOCALAPPDATA%\PharmacyPOS\Logs` under the service account's profile) |
| `Logging__RetainedDays` | How many days of rotated log files to keep (default 31) |
| `ASPNETCORE_ENVIRONMENT` | Set to `Production` |

Run the API under a dedicated, non-interactive Windows service account (not a real user's login, not LocalSystem) with:
- Read/execute access to the publish directory.
- Write access only to `Backup__Directory` and `Logging__FileDirectory`.

Register it as a Windows Service (built-in `sc.exe`, or NSSM for more control over restart/log behavior):

```
sc create PharmacyPosApi binPath= "C:\PharmacyPOS\api\Pharmacy.Api.exe" start= auto obj= ".\pharmacy_svc" password= "<service-account-password>"
```

Configure HTTPS: either bind a real certificate to Kestrel directly (`ASPNETCORE_URLS` + Kestrel HTTPS configuration), or place a reverse proxy (IIS, nginx, or a load balancer) in front of Kestrel on plain HTTP and terminate TLS there. Either way, the Flutter client must never be configured to skip certificate validation.

## 6. Firewall / network

- Open only the API's listening port to the specific subnet(s)/VPN your pharmacy workstations use — not the open internet.
- PostgreSQL's port (5432 by default) should be reachable only from the API host, not from client workstations directly.
- If the database is remote from the API host, ensure the network path supports the `SSL Mode=Require` connection from §3.

## 7. Migrations

Migrations are **not** applied automatically on startup — this is deliberate, so a deployment never runs an unreviewed schema change. Apply them as an explicit, reviewed step before starting the service for the first time and before every upgrade:

```
dotnet ef database update --project backend/Pharmacy.Infrastructure --startup-project backend/Pharmacy.Api --connection "<the pharmacy_app connection string>"
```

Check current status first with `dotnet ef migrations list` (applied migrations are marked) if you need to confirm what will change.

## 8. Flutter client configuration

Build the release client:

```
flutter build windows --release
```

The client resolves its backend URL in this order: the `PHARMACY_API_URL` process environment variable, then the `API_BASE_URL` compile-time define (`flutter build windows --release --dart-define=API_BASE_URL=https://pharmacy-api.yourcompany.local`), then a `localhost` default (development-only fallback). For a production rollout, either bake the correct URL in at build time with `--dart-define`, or set `PHARMACY_API_URL` in the environment of every workstation that launches the client (e.g. a machine-level environment variable, or a wrapper shortcut that sets it before launching `pharmacy_pos.exe`). Confirm the URL points at your HTTPS endpoint, not a bare HTTP/localhost address, before distributing the build.

Distribute the built `build/windows/x64/runner/Release/` folder to each workstation (see `docs/RELEASE_CHECKLIST.md` for packaging options).

## 9. Verification

1. Start the API service. Confirm `GET https://<api-host>/api/health/live` returns `{"status":"healthy"}` and `GET .../api/health/ready` also reports healthy (this confirms database connectivity).
2. Complete first-run owner bootstrap (`POST /api/auth/setup-owner`, only works once against an empty user table) — see `docs/OPERATIONS_RUNBOOK.md`.
3. Log in from a Flutter client using the owner account; confirm the client reaches the configured server (not `localhost`).
4. Create the pharmacy's branch(es) and default financial accounts.
5. Confirm a permission-denied action (e.g. log in as a lower-privilege role and attempt an Owner-only screen) returns a clean 403, not a stack trace.
6. Trigger a manual backup (`system.backup` permission) and confirm a file appears in `Backup__Directory`.

## 10. Backup setup

Confirm `Backup__Directory` points at storage with enough free space and, ideally, that is itself backed up/replicated off the API host (a backup that lives only on the machine it protects is not a real backup). See `docs/DISASTER_RECOVERY.md` for the full backup/restore/recovery procedure, and `docs/SECURITY_CHECKLIST.md` for who should be able to reach that directory.

---

Related documents: `docs/DEPLOYMENT_CHECKLIST.md` (condensed checklist), `docs/RELEASE_CHECKLIST.md` (cutting a release build), `docs/OPERATIONS_RUNBOOK.md` (day-2 operations), `docs/DISASTER_RECOVERY.md` (backup/restore/recovery), `docs/SECURITY_CHECKLIST.md`.
