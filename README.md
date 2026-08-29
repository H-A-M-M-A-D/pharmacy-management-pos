# Pharmacy Management System POS

Pharmacy management foundation with a Phase 2 authentication and user-management module for an ASP.NET Core API and Flutter Windows client. The code and PostgreSQL schema are verified locally. Product, inventory transaction, and POS modules have not started.

## Current Scope

- Clean Architecture backend targeting .NET 10
- PostgreSQL persistence through EF Core and Npgsql
- JWT login, PBKDF2 password hashing, temporary lockout, forced password changes, token-version invalidation, and dynamic permission policies
- Paged user management, profile editing, activation/deactivation, password reset, Owner protection, and security audit events
- Idempotently seeded roles, focused permission catalog, and customizable role-permission defaults
- Branch-aware inventory entities, permanent stock ledger, and controlled current-balance projections
- Reusable FEFO batch allocation service
- Flutter desktop shell, secure token storage, session restoration, login, forced password change, users, and profile screens
- Backend unit/foundation and PostgreSQL integration tests, plus a Flutter widget smoke test

No POS, purchasing, product-management, inventory-management, or reporting workflow is implemented.

## Dependency Graph

```text
Pharmacy.Domain          -> no project references
Pharmacy.Application     -> Pharmacy.Domain
Pharmacy.Infrastructure  -> Pharmacy.Application, Pharmacy.Domain
Pharmacy.Api             -> Pharmacy.Application, Pharmacy.Infrastructure
Pharmacy.Tests           -> projects required by its tests
```

`Pharmacy.Application` does not reference `Pharmacy.Infrastructure`.

## Toolchain

- Target framework: `net10.0`
- EF Core: `10.0.11`
- Npgsql EF provider: `10.0.3`
- ASP.NET Core OpenAPI: `10.0.11`
- Microsoft.OpenApi resolved dependency: `2.7.5`
- JWT bearer: `10.0.11`
- IdentityModel JWT/token libraries: `8.22.0`
- Flutter SDK: use the locally installed stable SDK compatible with Dart `3.12.2`

## Database Status

PostgreSQL 17 is the verified development provider. Applied migrations are:

```text
backend/Pharmacy.Infrastructure/Migrations/20260829211152_InitialCreate.cs
backend/Pharmacy.Infrastructure/Migrations/20260829223012_AddUserSecurityAndManagement.cs
```

Both migrations are applied to local `pharmacy_dev` and `pharmacy_test` through the non-superuser `pharmacy_app_dev` role. The schema remains 13 application tables plus `__EFMigrationsHistory`; Phase 2 adds security columns, normalized identity indexes, and role/permission seed data. Credentials remain in user-scoped environment variables and are not stored in the repository. See [QUICK_START.md](QUICK_START.md) for safe local configuration.

## Identity Policy

- Usernames are immutable for the MVP, normalized case-insensitively, and globally unique.
- Email is optional and unique only when provided; phone numbers are not unique.
- Five consecutive failures lock an account for 15 minutes. Successful login clears the counter.
- New and administratively reset users must change their password before accessing other modules.
- Deactivation, password changes, resets, and role/branch changes increment a token version checked on every authenticated request.
- The last active Owner cannot be deactivated or lose the Owner role. Owner accounts require `users.manage_owner` to modify.

## Key Inventory Rules

- `StockMovement` is the permanent source of truth.
- `ProductBatch.QuantityAvailable` and `Inventory.QuantityInStock` are current-balance projections.
- The DbContext requires projection deltas and stock movements to be saved atomically with matching quantities.
- Existing stock movements cannot be updated or deleted through the DbContext.
- Positive movements: `OpeningStock`, `Purchase`, `SaleReturn`, `TransferIn`, `AdjustmentIncrease`.
- Negative movements: `Sale`, `PurchaseReturn`, `TransferOut`, `AdjustmentDecrease`, `Expired`, `Damaged`.
- Application validation and a PostgreSQL check constraint reject zero quantities and incorrect signs.

## FEFO and Expiry

`FefoAllocationService` filters by branch and product, excludes disposed, expired, and empty batches, orders deterministically by expiry/creation/batch/id, and allocates across batches. Expiry and manufacturing dates use `DateOnly` and PostgreSQL `date`. A batch expiring on the sale date is considered sellable for that entire date in the current fixed MVP policy.

## Precision

- Money: `decimal(18,2)`
- Maximum discount percentage: `decimal(5,2)`
- Quantities and pack sizes: integer base-unit counts
- Product unit labels and pack size preserve room for future box/strip/tablet/bottle/piece conversion, but conversion is not implemented.
- Operational timestamps use UTC `DateTime`; medicine expiry/manufacturing values are date-only.

## Verification

```powershell
cd backend
dotnet restore PharmacySystem.slnx
dotnet build PharmacySystem.slnx
dotnet test PharmacySystem.slnx
dotnet ef migrations list --project Pharmacy.Infrastructure --startup-project Pharmacy.Api

cd ..\desktop\pharmacy_pos
flutter pub get
flutter analyze
flutter test
```

Architecture details and the exact mapped table inventory are in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
