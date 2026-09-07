# Quick Start - Pharmacy Management System

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

Expected migrations include the Phase 1 through Phase 11 migrations plus `20260907210154_AddReportingPermissions`. Seeing them in the list verifies discovery, not application to a database.

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

The local verification environment uses PostgreSQL `17.11`. All twelve migrations are applied to both databases, and `__EFMigrationsHistory` contains all twelve migration identifiers. Keep the application password outside the repository; the verified machine uses user-scoped environment variables.

## Run API

With the environment variables or user-secrets configured:

```powershell
cd backend
dotnet run --project Pharmacy.Api
```

Development OpenAPI JSON is mapped at `/openapi/v1.json`; health is at `/api/health`.

`POST /api/auth/setup-owner` is only for an empty database and requires a strong user-selected password. The application rejects it after any user exists. `POST /api/auth/login` uses a generic failure response for invalid, inactive, or locked accounts. `GET /api/auth/me` is the authoritative current-session profile.

Phase 4 inventory endpoints are under `/api/inventory`, `/api/batches`, and `/api/stock-movements`. Opening stock, adjustments, stock counts, and expired disposal are controlled stock commands; do not edit `ProductBatch.QuantityAvailable` or `Inventory.QuantityInStock` directly.

Phase 5 supplier endpoints are under `/api/suppliers`. Supplier master records are global, supplier ledger entries are branch-scoped, and payments/adjustments are controlled financial ledger commands.

Phase 6 purchasing endpoints are under `/api/purchase-orders`, `/api/goods-receipts`, `/api/purchases`, `/api/purchases/direct`, and `/api/purchasing/options`. Purchase orders do not affect stock until goods are received; posted goods receipts update batches, inventory projections, stock movements, and supplier payables atomically.

Phase 7 POS and sales endpoints are under `/api/pos/products/search` and `/api/sales`. Posted sales allocate batches through FEFO, update stock projections, create negative sale stock movements, persist batch allocation price snapshots, and support held sales, split payments, cash tender/change, sales history, receipt preview, and permission-gated receipt reprint.

Phase 8 sales-return endpoints are under `/api/sales/{saleId}/returnable` and `/api/sales-returns`. Returns reverse original sale batch allocations, not FEFO. Restockable returns increase the original batch/inventory projections; non-resellable returns record `SaleReturn` plus `Damaged` or `Expired` movements with no sellable stock increase. Refund payments must equal the backend-calculated refund.

Phase 9 purchase-return endpoints are under `/api/goods-receipts/{goodsReceiptId}/returnable`, `/api/goods-receipts/{goodsReceiptId}/purchase-returns`, and `/api/purchase-returns`. Purchase returns must use original goods-receipt items and original batches. Paid return quantities reduce stock and supplier payable; bonus returns reduce physical stock without supplier credit. Posted purchase returns are immutable and can be viewed/reprinted as return notes.

Phase 10 customer endpoints are under `/api/customers`. Customer master records are global, customer receivable ledger entries are branch-scoped, and payments/adjustments are controlled ledger commands. Customer credit sales require an active customer, `sales.credit`, and available credit. Sales returns for credit sales reduce customer receivable before requiring any cash refund payment.

Phase 11 finance endpoints are under `/api/financial-accounts`, `/api/expense-categories`, `/api/expenses`, and `/api/finance`. Create an active branch financial account before posting new monetary sales, customer payments, supplier payments, or cash refunds; each request selects the account used for the movement.

Phase 12 read-only reports are under `/api/reports`. Supply explicit `fromUtc` and exclusive `toUtc` boundaries, with optional authorized `branchId`. CSV routes end in `/export.csv` and additionally require `reports.export`. Reports use transactional tables and ledgers directly; they do not create report snapshots.

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


