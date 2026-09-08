# Operations Runbook

## API Will Not Start

Check Windows service logs, external connection-string configuration, JWT key length, issuer/audience settings, port conflicts, and service-account permissions. Do not paste secrets into support tickets.

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
