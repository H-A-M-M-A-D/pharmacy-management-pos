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

Application owns auth, Product Master, inventory, supplier-management, purchasing, POS/sales use cases, and FEFO contracts/use-case orchestration. Infrastructure owns EF Core repositories, PostgreSQL transactions, PostgreSQL configuration, PBKDF2 hashing, and JWT creation. API owns HTTP endpoints, authentication validation, dependency injection, and permission-policy integration.

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

## Product Master

Product Master manages global `Product`, `ProductCategory`, and `Manufacturer` catalog data only. SKU is immutable after creation and unique through `NormalizedSku`; optional barcode uniqueness uses `NormalizedBarcode` with a nullable filtered index. Category and manufacturer names use normalized unique keys. Products support medicines and general pharmacy retail goods, so brand, generic name, barcode, and manufacturer are optional.

`Unit` is selected from a controlled application list and `PackSize` is a positive integer count. This preserves an upgrade path to structured packaging and conversions without implementing conversion in Phase 3. Product prices are catalog/default prices; `ProductBatch` prices remain batch-specific operational values. Catalog actions never create batches, inventory balances, or stock movements.

Products, categories, and manufacturers are activated/deactivated rather than deleted. Product list queries use SQL filtering, projection, deterministic sorting, pagination, and `AsNoTracking`. Product Master changes produce focused audit events.

## Batch and Inventory Management

Phase 4 inventory operations are exposed through `IInventoryService`. Controllers do not directly mutate quantity columns. The service validates branch access, product/batch state, expiry rules, quantities, reasons, and permissions before asking Infrastructure to run the stock change in a serializable PostgreSQL transaction.

Opening stock is the controlled initialization/import workflow. It creates a batch when needed, reuses an existing valid batch for incremental initialization entries, records a positive `OpeningStock` movement, and updates `ProductBatch.QuantityAvailable` plus the matching `Inventory.QuantityInStock` projection. Expired or inactive products are rejected for sellable opening stock. Product catalog prices are not overwritten by batch pricing.

Stock adjustments accept positive user-entered quantities only. The backend derives ledger signs: increases create `AdjustmentIncrease`, decreases create `AdjustmentDecrease`, and damaged removal uses `Damaged`. Decreases cannot take batch or inventory projections below zero. Stock count reconciliation compares physical quantity to the current batch projection and records only the variance; zero variance records audit but no movement.

Expired disposal creates a negative `Expired` stock movement and reduces projections. It requires the batch to be expired by the explicit business date policy and never silently removes stock. Supplier return workflow remains later purchase-return work, not Phase 4.

Inventory listing groups batch-level projections into branch/product operational totals and reports status, active batch count, nearest expiry, and estimated stock value. Low stock is `QuantityInStock > 0 && QuantityInStock <= ReorderLevel`; out of stock is `QuantityInStock <= 0`; healthy is above reorder level. Inventory value is an operational cost view: available quantity multiplied by batch purchase price. It is not accounting journal valuation.

All stock quantities currently operate in the product's configured inventory unit. Structured box/strip/tablet/bottle conversion is intentionally deferred.

## Supplier Management

Phase 5 supplier operations are exposed through `ISupplierService`. Supplier master records are global catalog data, while supplier financial activity is recorded in branch-scoped `SupplierLedgerEntry` rows. Controllers do not calculate balances or mutate ledger state directly.

Supplier names are normalized and globally unique. Contact, city, NTN, STRN, payment terms, and credit limit are master-data attributes. Suppliers are activated/deactivated rather than deleted, so batch history and financial history remain stable. Inactive suppliers are excluded from normal lookup but remain available for historical display.

`SupplierLedgerEntry` is the supplier balance source of truth. Outstanding balances and running balances are calculated from immutable ledger entries. Positive amounts mean payable to the supplier; negative amounts mean pharmacy advance/credit. Opening balance allows either sign, payments are negative, debit adjustments are positive, and credit adjustments are negative. The DbContext rejects ledger updates/deletes, and PostgreSQL enforces sign validity through `CK_SupplierLedgerEntries_AmountSign`.

Opening balance is recorded only during supplier creation when non-zero. Editing supplier master data does not rewrite opening balance. Payments and balance adjustments run in serializable PostgreSQL transactions and create audit events. Purchase ledger entries are created by Phase 6 goods-receipt posting. Purchase-return ledger entries remain reserved for Phase 9 and are not exposed as standalone supplier UI actions.


## Purchasing and Goods Receiving

Phase 6 purchasing operations are exposed through `IPurchasingService`. Purchase orders are branch-scoped planning documents: draft orders can be edited, submitted orders can be received, and partially/completely received orders are protected from cancellation in this phase.

Goods receipt posting is the stock and payable boundary. Direct purchases post without a purchase order; ordered receipts must reference a matching purchase order item. Paid quantity increments purchase-order received quantity and payable value. Bonus quantity increases inventory but does not increase supplier payable.

Posting runs in a serializable PostgreSQL transaction. It creates or reuses a compatible `ProductBatch`, updates `ProductBatch.QuantityAvailable` and `Inventory.QuantityInStock`, creates a positive `Purchase` `StockMovement`, and creates a positive supplier ledger `Purchase` entry when the receipt net total is greater than zero. Existing batch metadata must match expiry, manufacturing date, supplier, and prices.

Supplier invoice numbers are optional and normalized. PostgreSQL enforces uniqueness per supplier when an invoice number is present while allowing multiple null invoices. Posted goods receipts and receipt items are immutable through DbContext validation. Purchase returns, supplier payment allocation, tax reporting, and accounting general ledger posting remain future phases.

## POS and Sales

Phase 7 sales operations are exposed through `ISalesService`. POS product search is branch-aware and returns active catalog products with currently sellable stock, nearest expiry, indicative batch retail price, and maximum discount. Exact barcode/SKU matches are prioritized before text matches.

Sale posting is the inventory and receipt boundary. The service validates branch access, line quantities, payment shape, product activity, discount permission, and product maximum discount before entering a serializable PostgreSQL transaction. It then calls `FefoAllocationService` for each line and persists the resulting batch allocations in `SaleItemBatchAllocations` with quantity, expiry, retail price, sale price, and cost snapshots.

Posting updates `ProductBatch.QuantityAvailable` and `Inventory.QuantityInStock`, records matching negative `Sale` stock movements, stores sale items/payments, and writes audit entries in one unit of work. Held sales store the cart and customer snapshot only; they do not reserve inventory. A held sale is revalidated against current stock when posted.

Posted sales are immutable through `PharmacyDbContext`: existing posted sale rows cannot be changed or deleted, and sale payment/allocation history cannot be edited or deleted. Held sales can be updated or cancelled. Sales returns, customer credit sales, tax rules, shift/cash-drawer controls, and accounting journal integration remain later phases.
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

Entity validation and `CK_StockMovements_QuantitySign` enforce the convention. Zero is invalid. Phase 4 adds the controlled transaction workflow that creates movements and updates projections atomically; `PharmacyDbContext` still rejects projection-only quantity edits.

## Entity and Table Truth

There are 22 mapped application entities/tables. PostgreSQL also creates `__EFMigrationsHistory` when migrations are applied.

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
| SupplierLedgerEntry | SupplierLedgerEntries | Branch/supplier financial ledger |
| PurchaseOrder | PurchaseOrders | Branch/supplier planning document |
| PurchaseOrderItem | PurchaseOrderItems | Purchase order line |
| GoodsReceipt | GoodsReceipts | Branch/supplier posted receipt |
| GoodsReceiptItem | GoodsReceiptItems | Posted receipt line linked to product/batch |
| Sale | Sales | Branch/cashier held or posted sale document |
| SaleItem | SaleItems | Sale line linked to product |
| SaleItemBatchAllocation | SaleItemBatchAllocations | Posted FEFO batch allocation and price snapshot |
| SalePayment | SalePayments | Posted sale payment line |

Audit data:

| Entity | Table | Scope |
|---|---|---|
| AuditLog | AuditLogs | User-linked; no direct BranchId column |

## Database Constraints

- Product normalized SKU has a unique index.
- Product normalized barcode has a nullable filtered unique index. PostgreSQL permits multiple null barcodes while enforcing uniqueness for non-null values.
- Category and manufacturer normalized names are unique.
- Product check constraints require positive pack size, non-negative prices/reorder level, and a discount from 0 through 100.
- Batch uniqueness is `(BranchId, ProductId, BatchNumber)`; a batch number is not globally unique.
- Batch checks require non-negative received/available quantities, non-negative prices, and manufacturing date not after expiry when present.
- Inventory uniqueness is `(BranchId, ProductId, ProductBatchId)`.
- Inventory checks require non-negative quantity and reorder level.
- Role/permission, usernames, emails, branch codes, and permission codes have appropriate unique indexes.
- Supplier normalized name is unique.
- Supplier credit limit and payment terms have non-negative checks.
- Supplier ledger sign rules are enforced by `CK_SupplierLedgerEntries_AmountSign`.
- Purchase order status, ordered/received quantity range, receipt status, receipt totals, receipt item quantity, price, discount, tax, net amount, and manufacturing-before-expiry rules have PostgreSQL checks.
- Sales status, posted invoice requirement, paid posted sale totals, non-negative sale money, sale item quantity/discount/money, sale allocation quantity/money, and sale payment method/cash tender rules have PostgreSQL checks.
- Purchase order numbers, GRN numbers, supplier invoice numbers, sale invoice numbers, and held sale numbers have unique indexes scoped to their business rules.
- FEFO, branch, active-state, audit, stock-ledger, supplier, supplier-ledger, purchase-order, goods-receipt, POS search, sales history, sale payment, and receipt query paths have supporting indexes.
- Foreign keys and delete behaviors are defined in `PharmacyDbContext` and generated into the migration.
- Audit old/new value columns are configured as PostgreSQL `jsonb`.

These statements are verified in the EF model, migrations, and real PostgreSQL 17 catalogs. Rollback-isolated integration tests also exercise nullable unique barcodes, scoped batch uniqueness, JSONB/date/timestamp mappings, stock sign checks, non-negative inventory constraints, ledger/projection consistency, supplier uniqueness, supplier financial checks, supplier ledger sign checks, purchase/receipt uniqueness, purchase quantity checks, receipt item checks, purchasing permission seeds, sales permission seeds, sale/receipt uniqueness, sale payment checks, and sale allocation checks.

## Dates, Time, Money, and Quantity

- Base entity timestamps, login/count timestamps, and health timestamps are UTC `DateTime` values mapped to PostgreSQL timestamps with time zone.
- Manufacturing and expiry are date-only business values mapped to PostgreSQL `date`.
- Product and batch money use `decimal(18,2)`.
- Supplier opening balance, credit limit, supplier ledger amounts, purchase prices, and purchase receipt totals use `decimal(18,2)`.
- Maximum discount percentage, purchase discount percentage, and purchase tax percentage use `decimal(5,2)`.
- Stock quantity, reorder levels, and pack sizes use integers.
- Unit is a product label and pack size is retained, so future box/strip/tablet/bottle/piece modeling is not blocked. Unit conversion is not implemented.

## Migration Status

- Directory: `backend/Pharmacy.Infrastructure/Migrations`
- Migration: `20260829211152_InitialCreate`
- Migration: `20260829223012_AddUserSecurityAndManagement`
- Migration: `20260901194508_CompleteProductMaster`
- Migration: `20260901215409_CompleteBatchAndInventoryManagement`
- Migration: `20260902051500_CompleteSupplierManagement`
- Migration: `20260902055022_CompletePurchasingAndGoodsReceiving`
- Migration: `20260902114037_CompletePosAndSales`
- Snapshot: `PharmacyDbContextModelSnapshot.cs`
- EF reports no pending model changes.
- Applied to: local `pharmacy_dev` and isolated `pharmacy_test`
- EF history: Phase 1 through Phase 7 migrations recorded with product version `10.0.11`
- Real schema: 22 application tables plus `__EFMigrationsHistory`, with foreign keys and catalog/operational indexes verified in PostgreSQL

## Flutter Foundation

The Flutter project contains a Material desktop shell, `ApiClient`, `AuthState`, login, forced-password, user-management, profile, products, categories, manufacturers, inventory, and supplier screens, and purchasing screens. Inventory UI includes stock, batches, expiry, movement history, opening stock, adjustment, and stock count workflows. Supplier UI includes supplier list/search, add/edit, activate/deactivate, ledger statement, payment, and balance-adjustment dialogs. Purchasing UI includes purchase-order list/create/submit/cancel, goods receiving, direct purchase posting, and purchase history. POS UI includes product/barcode search, cart, discount-aware line editing, held-sale action, checkout payment dialog, sales history, and receipt preview/reprint actions. The API base URL is supplied with `API_BASE_URL`. Tokens are stored through `flutter_secure_storage`, restored through `/api/auth/me`, and cleared on logout. Navigation and actions follow permission codes while the backend remains authoritative.

## Phase 1 Limitations

- Local PostgreSQL verification is complete; deployment database provisioning and production operations remain out of scope.
- No refresh tokens or general-purpose server-side token revocation list; token versions invalidate sessions after security-sensitive user changes.
- No role-permission mutation UI/API yet; migration defaults remain directly customizable in later administration work.
- No sales returns, customer credit billing, purchase returns, transfers, reports, unit conversion, accounting general ledger, supplier payment allocation, shift/cash drawer closing, or background expiry processing.
- CORS is permissive for local foundation development and must be restricted before deployment.
- API error handling and setup-owner exposure require deployment hardening.
