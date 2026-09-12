# Operations Runbook

## Start / Stop the Backend

Start: `sc start PharmacyPosApi` (or your service manager's equivalent). Stop: `sc stop PharmacyPosApi` — allow in-flight requests to finish; stopping mid-transaction is safe (each posting flow is one database transaction, so it either committed or didn't) but avoid it during a known busy period. Confirm status with `sc query PharmacyPosApi` or `/api/health/live`.

## Start the Desktop Client

Launch `pharmacy_pos.exe` from its install location or a Start Menu/desktop shortcut. Only one instance runs per Windows session — launching a second copy brings the already-running window to the front instead of opening a duplicate. If the app won't reach the server, see "Flutter Cannot Reach API" below.

## Update the App

Backend: stop the service, `dotnet publish` the new version over the existing install directory (or to a new versioned directory, then repoint the service), apply any new migrations explicitly (see "Migration Failure" below and `docs/PRODUCTION_DEPLOYMENT.md` §7), then start the service and verify health. Desktop: replace the installed `build/windows/x64/runner/Release/` contents with the new build on each workstation; user settings/tokens are unaffected (`flutter_secure_storage` persists independently of the install directory).

## Logs

Backend logs are rotated daily to `Logging:FileDirectory` (default `%LOCALAPPDATA%\PharmacyPOS\Logs\pharmacy-<date>.log` under the service account's profile), retained for `Logging:RetainedDays` (default 31) or capped at 50MB per file, whichever comes first. Every line carries a correlation ID matching the `X-Correlation-ID` response header a client received, so a user-reported error can be traced to its exact log entry. Logs never contain passwords, JWT keys, or connection strings — safe to attach to a support ticket as-is.

## Health Checks

`GET /api/health/live` confirms the process is up (no dependency checks). `GET /api/health/ready` additionally confirms database connectivity and returns 503 if the database is unreachable — use this one for monitoring/alerting. Neither endpoint requires authentication or exposes configuration/secrets.

## Support Diagnostics

For a support request, safely gather: the app's System Information screen (Administration → System Info — app/API version, database connectivity, applied migration, current user/branch), the relevant time range from the backend log file (see "Logs" above), and the correlation ID from any error dialog. None of this contains secrets and it is safe to share with support as-is.

## API Will Not Start

Check the rotated log file first — `Logging:FileDirectory` (default `%LOCALAPPDATA%\PharmacyPOS\Logs\pharmacy-<date>.log` under the service account's profile) records a `Fatal` entry with the exact reason for any startup failure, including a missing/placeholder JWT key or an unreachable database, before the process exits. Also check external connection-string configuration, JWT key length, issuer/audience settings, port conflicts, and service-account permissions. Do not paste secrets into support tickets — the log file never contains passwords, JWT keys, or connection strings.

## PostgreSQL Unavailable

Check the PostgreSQL Windows service, disk space, port reachability, and the application role. `/api/health/ready` returns 503 while the database is unavailable. Do not repeatedly post transactions during an outage.

The runtime identity must never be elevated to perform recovery. Supply the separate maintenance connection through the operator environment only while running isolated restore validation.

## Login or Session Issue

Confirm API readiness and account activation. Token-version changes and expiry require a fresh login. Use Sign out everywhere after suspected token exposure. Never reset passwords through SQL.

## Backup Failure or Disk Full

Review the backup record and server log using its correlation/context identifier. Verify `pg_dump` and `pg_restore`, database connectivity, free space, and service-account write permission to `%LOCALAPPDATA%\PharmacyPOS\Backups`. Partial files are not valid backups.

## Flutter Cannot Reach API

Verify `PHARMACY_API_URL`, DNS/IP, firewall, HTTPS trust, and readiness. Connection failures must be resolved at the API/network layer; the client has no offline transaction mode.

## Migration Failure

Stop application writes, preserve the database, inspect the failed migration and PostgreSQL log, and correct the deployment condition. Never edit `__EFMigrationsHistory` manually.

## Safe Restart

Stop client activity, stop the API service, restart PostgreSQL only if required, start the API, verify liveness/readiness, then reopen clients. Capture correlation IDs for unexpected API errors.
