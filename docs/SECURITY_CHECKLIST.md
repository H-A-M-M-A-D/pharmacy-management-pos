# Security Checklist

- Store database passwords and JWT signing keys outside Git; reject placeholder or short JWT keys.
- Use a least-privilege PostgreSQL runtime role without `CREATEDB` and a separate externally configured maintenance identity with only the additional database-creation privilege needed for isolated restore validation.
- Require HTTPS for deployed API traffic and trusted certificates on Windows clients.
- Configure explicit production CORS origins or leave cross-origin browser access disabled.
- Run API and PostgreSQL under dedicated Windows accounts with restricted filesystem permissions.
- Restrict the backup directory and off-host backup destination to authorized operators.
- Keep audit append-only protections and permission checks enabled.
- Do not share Owner accounts or passwords; deactivate departed users and revoke sessions.
- Patch .NET, Flutter, PostgreSQL, and OS dependencies under a tested maintenance process.
- Review logs by correlation ID without recording passwords, JWTs, connection strings, or request bodies containing secrets.
