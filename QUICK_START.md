# Quick Start - Phase 1 Foundation

## Prerequisites

- .NET SDK 10
- Flutter with Windows desktop support
- PostgreSQL for database-backed execution

The unit tests can run without PostgreSQL. PostgreSQL integration tests skip unless `PHARMACY_TEST_CONNECTION_STRING` is available to the test process. Migration application and API execution that uses persistence require a working PostgreSQL instance.

## Verify Backend

```powershell
cd backend
dotnet restore PharmacySystem.slnx
dotnet build PharmacySystem.slnx
dotnet test PharmacySystem.slnx
dotnet ef migrations list --project Pharmacy.Infrastructure --startup-project Pharmacy.Api
```

Expected migrations are `20260829211152_InitialCreate`, `20260829223012_AddUserSecurityAndManagement`, and `20260901194508_CompleteProductMaster`. Seeing them in the list verifies discovery, not application to a database.

## Configure Local Secrets

Do not put a JWT signing key or database password in `appsettings.json`.

For the current PowerShell session:

```powershell
$env:Jwt__Key = '<local signing key of at least 32 characters>'
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=pharmacy_dev;Username=pharmacy_app_dev;Password=<local development password>'
$env:PHARMACY_TEST_CONNECTION_STRING = 'Host=localhost;Port=5432;Database=pharmacy_test;Username=pharmacy_app_dev;Password=<local development password>'
```

Alternatively, initialize and use .NET user-secrets in `backend/Pharmacy.Api`:

```powershell
cd backend\Pharmacy.Api
dotnet user-secrets init
dotnet user-secrets set 'Jwt:Key' '<local signing key of at least 32 characters>'
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' 'Host=localhost;Port=5432;Database=pharmacy_dev;Username=pharmacy_app_dev;Password=<local development password>'
```

The issuer, audience, and 60-minute token lifetime are non-secret defaults in `appsettings.json`. Production values must be supplied by deployment configuration.

## Create and Migrate Development Database

Using a local PostgreSQL administrator, create a login role such as `pharmacy_app_dev` with no PostgreSQL-wide administrative privileges. Create `pharmacy_dev` and, for integration tests, `pharmacy_test`, and grant that role the ability to create and manage objects within only those databases. Then run:

```powershell
cd backend
dotnet ef database update --project Pharmacy.Infrastructure --startup-project Pharmacy.Api
```

The local verification environment uses PostgreSQL `17.11`. All three migrations are applied to both databases, and `__EFMigrationsHistory` contains all three migration identifiers. Keep the application password outside the repository; the verified machine uses user-scoped environment variables.

## Run API

With the environment variables or user-secrets configured:

```powershell
cd backend
dotnet run --project Pharmacy.Api
```

Development OpenAPI JSON is mapped at `/openapi/v1.json`; health is at `/api/health`.

`POST /api/auth/setup-owner` is only for an empty database and requires a strong user-selected password. The application rejects it after any user exists. `POST /api/auth/login` uses a generic failure response for invalid, inactive, or locked accounts. `GET /api/auth/me` is the authoritative current-session profile.

## Verify Flutter

```powershell
cd desktop\pharmacy_pos
flutter pub get
flutter analyze
flutter test
```

Run against a chosen API URL with:

```powershell
flutter run -d windows --dart-define=API_BASE_URL=http://localhost:5000
```

The client stores the JWT through `flutter_secure_storage`; on Windows the plugin protects its encryption key with Windows Credential Manager. The API still uses self-contained access tokens rather than refresh tokens.
