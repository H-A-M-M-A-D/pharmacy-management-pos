# Pharmacy Management System POS

Pharmacy management foundation with completed authentication, user management, Product Master, and Batch & Inventory modules for an ASP.NET Core API and Flutter Windows client. The code and PostgreSQL schema are verified locally. Purchase, POS, sales, and reporting workflows have not started.

## Current Scope

- Clean Architecture backend targeting .NET 10
- PostgreSQL persistence through EF Core and Npgsql
- JWT login, PBKDF2 password hashing, temporary lockout, forced password changes, token-version invalidation, and dynamic permission policies
- Paged user management, profile editing, activation/deactivation, password reset, Owner protection, and security audit events
- Idempotently seeded roles, focused permission catalog, and customizable role-permission defaults
- Branch-aware inventory entities, permanent stock ledger, and controlled current-balance projections
- Reusable FEFO batch allocation service
- Flutter desktop shell, secure token storage, session restoration, login, forced password change, users, and profile screens
- Product, category, and manufacturer administration with permission-aware desktop screens, server-side product paging/filtering, immutable SKU, and activation workflows
- Controlled opening stock, stock adjustments, stock count reconciliation, expiry disposal, branch inventory views, batch views, movement ledger, valuation, and FEFO preview
- Backend unit/foundation and PostgreSQL integration tests, plus a Flutter widget smoke test

No POS, purchasing, supplier workflow, sales, customer billing, or reporting module is implemented.

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
backend/Pharmacy.Infrastructure/Migrations/20260901194508_CompleteProductMaster.cs
backend/Pharmacy.Infrastructure/Migrations/20260901215409_CompleteBatchAndInventoryManagement.cs
```

All four migrations are applied to local `pharmacy_dev` and `pharmacy_test` through the non-superuser `pharmacy_app_dev` role. The schema remains 13 application tables plus `__EFMigrationsHistory`; Phase 4 adds inventory constraints, query indexes, and inventory permissions. Credentials remain outside the repository. See [QUICK_START.md](QUICK_START.md) for safe local configuration.

## Product Master Policy

- SKU is trimmed, globally unique by normalized value, and immutable after creation.
- Barcode is optional, stored as text, and unique by normalized value when present; multiple null barcodes are allowed.
- Product name is the primary display name. Brand and generic names are optional so general retail products remain valid.
- Unit is selected from a controlled Phase 3 value list and pack size is a positive integer count. Unit conversion is intentionally deferred.
- Product prices are current catalog defaults. `ProductBatch` prices remain actual batch-specific values and are not rewritten by Product Master operations.
- Product, category, and manufacturer records use activation/deactivation; no stock, batch, inventory, or movement row is created by catalog operations.

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
- Inventory commands create a stock movement, update batch and inventory projections, and commit in one serializable PostgreSQL transaction.
- The DbContext requires projection deltas and stock movements to be saved atomically with matching quantities.
- Existing stock movements cannot be updated or deleted through the DbContext.
- Opening stock creates or reuses a valid batch and records `OpeningStock`; after setup, corrections should use stock adjustment.
- Stock count reconciliation records an adjustment only for the variance and does not overwrite balances directly.
- Expired stock disposal records a negative `Expired` movement; damaged stock uses a controlled stock adjustment with `Damaged`.
- Low stock is `QuantityInStock > 0 && QuantityInStock <= ReorderLevel`; out of stock is `QuantityInStock <= 0`.
- Inventory value is operational cost value: available quantity multiplied by batch purchase price.
- Positive movements: `OpeningStock`, `Purchase`, `SaleReturn`, `TransferIn`, `AdjustmentIncrease`.
- Negative movements: `Sale`, `PurchaseReturn`, `TransferOut`, `AdjustmentDecrease`, `Expired`, `Damaged`.
- Application validation and a PostgreSQL check constraint reject zero quantities and incorrect signs.
- PostgreSQL also rejects negative batch/inventory projections and negative batch prices.

## FEFO and Expiry

`FefoAllocationService` filters by branch and product, excludes disposed, expired, and empty batches, orders deterministically by expiry/creation/batch/id, and allocates across batches. Expiry and manufacturing dates use `DateOnly` and PostgreSQL `date`. A batch expiring on the sale date is considered sellable for that entire date in the current fixed MVP policy.

All stock quantities operate in the product's configured inventory unit. Box/strip/tablet conversion remains deferred. Batch pricing is independent and does not rewrite Product Master catalog prices.

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
