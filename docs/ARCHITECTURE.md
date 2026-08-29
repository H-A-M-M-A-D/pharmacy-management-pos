# Phase 1 Architecture

## Backend Layers

```text
Api -> Application
Api -> Infrastructure
Infrastructure -> Application
Infrastructure -> Domain
Application -> Domain
Domain -> no project
```

Application owns auth and FEFO contracts/use-case orchestration. Infrastructure owns EF Core repositories, PostgreSQL configuration, PBKDF2 hashing, and JWT creation. API owns HTTP endpoints, authentication validation, dependency injection, and permission-policy integration.

## Authentication and Authorization

- Passwords are stored only as versioned, randomly salted PBKDF2-SHA256 hashes using 210,000 iterations.
- The signing key is required from configuration, environment variables, or user-secrets. No signing key is committed.
- Tokens have configured issuer, audience, signing-key, and lifetime validation with zero clock skew.
- Token creation requires a positive configured expiration and includes user, role, branch, and distinct permission claims.
- Login queries only active users and loads role, role-permission, permission, and branch relationships.
- `HasPermissionAttribute` creates policies that require a specific `permission` claim. No authorization logic depends only on role-name checks.
- Initial owner setup is rejected once any user exists. Full user and permission administration is outside Phase 1.

Phase 2 adds global case-insensitive username normalization, optional normalized-email uniqueness, configurable five-attempt/15-minute lockout defaults, forced first-login password changes, and token-version validation against the database. Username is immutable for the MVP. User changes are permission-oriented; `users.manage_owner` is additionally required for Owner targets, and backend logic prevents removal or deactivation of the last active Owner.

Default roles are `Owner`, `Manager`, `Pharmacist`, `Cashier`, `PurchaseManager`, `Accountant`, and `StoreKeeper`. Owner receives all current permissions. Manager receives user administration except Owner management and role mutation, plus catalog/profile/audit access. Other roles initially receive profile view/update/change-password permissions. Migration seed SQL is conflict-safe, runs once through EF history, and does not continuously restore mappings that administrators later customize.

## FEFO Policy

`Pharmacy.Application.Services.Inventory.FefoAllocationService` receives candidate batches plus branch, product, requested quantity, and sale date. It:

1. matches branch and product;
2. requires positive available quantity;
3. excludes disposed batches;
4. accepts `ExpiryDate >= saleDate`;
5. orders by expiry, creation timestamp, batch number, then id;
6. allocates across as many batches as needed;
7. throws when eligible stock is insufficient.

For quantities 3 and 10 in two ascending-expiry batches, a request for 5 returns allocations 3 and 2. Expiry uses `DateOnly`/PostgreSQL `date`; a batch is sellable on its expiry date under the fixed Phase 1 policy.

## Stock Ledger and Projections

`StockMovement` is permanent transaction history and the stock source of truth. DbContext persistence rejects modification or deletion of existing movements. `ProductBatch.QuantityAvailable` and `Inventory.QuantityInStock` are query projections; non-zero changes to either must be persisted in the same unit of work as equal stock-movement deltas for the same branch/product/batch.

Positive quantities: `OpeningStock`, `Purchase`, `SaleReturn`, `TransferIn`, `AdjustmentIncrease`.

Negative quantities: `Sale`, `PurchaseReturn`, `TransferOut`, `AdjustmentDecrease`, `Expired`, `Damaged`.

Entity validation and `CK_StockMovements_QuantitySign` enforce the convention. Zero is invalid. Phase 1 does not include a transaction workflow that creates movements and updates projections; later use cases must perform all three changes atomically.

## Entity and Table Truth

There are 13 mapped application entities/tables. PostgreSQL also creates `__EFMigrationsHistory` when migrations are applied.

Global/catalog and organization data:

| Entity | Table | Scope |
|---|---|---|
| Branch | Branches | Global organization catalog |
| Role | Roles | Global authorization catalog |
| Permission | Permissions | Global authorization catalog |
| RolePermission | RolePermissions | Global authorization join |
| ProductCategory | ProductCategories | Global product catalog |
| Manufacturer | Manufacturers | Global product catalog |
| Supplier | Suppliers | Global supplier catalog |
| Product | Products | Global product catalog |

Branch-scoped operational data:

| Entity | Table | Scope |
|---|---|---|
| User | Users | Assigned to one branch |
| ProductBatch | ProductBatches | Branch/product batch |
| Inventory | Inventory | Branch/product/batch projection |
| StockMovement | StockMovements | Branch/product/batch ledger |

Audit data:

| Entity | Table | Scope |
|---|---|---|
| AuditLog | AuditLogs | User-linked; no direct BranchId column |

## Database Constraints

- Product SKU has a unique index.
- Product barcode has a nullable unique index. PostgreSQL permits multiple null barcodes while enforcing uniqueness for non-null values.
- Batch uniqueness is `(BranchId, ProductId, BatchNumber)`; a batch number is not globally unique.
- Inventory uniqueness is `(BranchId, ProductId, ProductBatchId)`.
- Role/permission, usernames, emails, branch codes, and permission codes have appropriate unique indexes.
- FEFO, branch, active-state, audit, and stock-ledger query paths have supporting indexes.
- Foreign keys and delete behaviors are defined in `PharmacyDbContext` and generated into the migration.
- Audit old/new value columns are configured as PostgreSQL `jsonb`.

These statements are verified in the EF model, migration, and real PostgreSQL 17 catalogs. Rollback-isolated integration tests also exercise nullable unique barcodes, scoped batch uniqueness, JSONB/date/timestamp mappings, and the stock sign check constraint.

## Dates, Time, Money, and Quantity

- Base entity timestamps, login/count timestamps, and health timestamps are UTC `DateTime` values mapped to PostgreSQL timestamps with time zone.
- Manufacturing and expiry are date-only business values mapped to PostgreSQL `date`.
- Product and batch money use `decimal(18,2)`.
- Maximum discount percentage uses `decimal(5,2)`.
- Stock quantity, reorder levels, and pack sizes use integers.
- Unit is a product label and pack size is retained, so future box/strip/tablet/bottle/piece modeling is not blocked. Unit conversion is not implemented.

## Migration Status

- Directory: `backend/Pharmacy.Infrastructure/Migrations`
- Migration: `20260829211152_InitialCreate`
- Migration: `20260829223012_AddUserSecurityAndManagement`
- Snapshot: `PharmacyDbContextModelSnapshot.cs`
- EF reports no pending model changes.
- Applied to: local `pharmacy_dev` and isolated `pharmacy_test`
- EF history: both Phase 1 and Phase 2 migrations recorded with product version `10.0.11`
- Real schema: 13 application tables plus `__EFMigrationsHistory`, 17 foreign keys, and 59 indexes including primary keys

## Flutter Foundation

The Flutter project contains a Material desktop shell, `ApiClient`, `AuthState`, login, forced-password, user-management, and profile screens. The API base URL is supplied with `API_BASE_URL`. Tokens are stored through `flutter_secure_storage`, restored through `/api/auth/me`, and cleared on logout. Navigation and actions follow permission codes while the backend remains authoritative.

## Phase 1 Limitations

- Local PostgreSQL verification is complete; deployment database provisioning and production operations remain out of scope.
- No refresh tokens or general-purpose server-side token revocation list; token versions invalidate sessions after security-sensitive user changes.
- No role-permission mutation UI/API yet; migration defaults remain directly customizable in later administration work.
- No POS, purchases, product UI, inventory UI, transfers, reports, or background expiry processing.
- CORS is permissive for local foundation development and must be restricted before deployment.
- API error handling and setup-owner exposure require deployment hardening.
