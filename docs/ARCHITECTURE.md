# Architecture

## Backend Layers

```text
Api -> Application
Api -> Infrastructure
Infrastructure -> Application
Infrastructure -> Domain
Application -> Domain
Domain -> no project
```

Application owns auth, Product Master, inventory, supplier-management, customer-management, purchasing, purchase-return, POS/sales, sales-return/refund use cases, and FEFO contracts/use-case orchestration. Infrastructure owns EF Core repositories, PostgreSQL transactions, PostgreSQL configuration, PBKDF2 hashing, and JWT creation. API owns HTTP endpoints, authentication validation, dependency injection, and permission-policy integration.

## Authentication and Authorization

- Passwords are stored only as versioned, randomly salted PBKDF2-SHA256 hashes using 210,000 iterations.
- The signing key is required from configuration, environment variables, or user-secrets. No signing key is committed.
- Tokens have configured issuer, audience, signing-key, and lifetime validation with zero clock skew.
- Token creation requires a positive configured expiration and includes user, role, branch, and distinct permission claims.
- Login queries only active users and loads role, role-permission, permission, and branch relationships.
- `HasPermissionAttribute` creates policies that require a specific `permission` claim. No authorization logic depends only on role-name checks.
- Initial owner setup is rejected once any user exists. Setup-owner remains limited to empty databases; user and permission administration is implemented through later phase user-management foundations.

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

Expired disposal creates a negative `Expired` stock movement and reduces projections. It requires the batch to be expired by the explicit business date policy and never silently removes stock. Supplier returns are handled by the Phase 9 purchase-return workflow, not by ad hoc inventory edits.

Inventory listing groups batch-level projections into branch/product operational totals and reports status, active batch count, nearest expiry, and estimated stock value. Low stock is `QuantityInStock > 0 && QuantityInStock <= ReorderLevel`; out of stock is `QuantityInStock <= 0`; healthy is above reorder level. Inventory value is an operational cost view: available quantity multiplied by batch purchase price. It is not accounting journal valuation.

All stock quantities currently operate in the product's configured inventory unit. Structured box/strip/tablet/bottle conversion is intentionally deferred.

## Supplier Management

Phase 5 supplier operations are exposed through `ISupplierService`. Supplier master records are global catalog data, while supplier financial activity is recorded in branch-scoped `SupplierLedgerEntry` rows. Controllers do not calculate balances or mutate ledger state directly.

Supplier names are normalized and globally unique. Contact, city, NTN, STRN, payment terms, and credit limit are master-data attributes. Suppliers are activated/deactivated rather than deleted, so batch history and financial history remain stable. Inactive suppliers are excluded from normal lookup but remain available for historical display.

`SupplierLedgerEntry` is the supplier balance source of truth. Outstanding balances and running balances are calculated from immutable ledger entries. Positive amounts mean payable to the supplier; negative amounts mean pharmacy advance/credit. Opening balance allows either sign, payments are negative, debit adjustments are positive, and credit adjustments are negative. The DbContext rejects ledger updates/deletes, and PostgreSQL enforces sign validity through `CK_SupplierLedgerEntries_AmountSign`.

Opening balance is recorded only during supplier creation when non-zero. Editing supplier master data does not rewrite opening balance. Payments and balance adjustments run in serializable PostgreSQL transactions and create audit events. Purchase ledger entries are created by Phase 6 goods-receipt posting. Purchase-return ledger entries are created by Phase 9 purchase-return posting and are not exposed as standalone supplier UI actions.

## Customer Management and Credit Sales

Phase 10 customer operations are exposed through `ICustomerService` and `CustomersController`. Customer master records are global, while customer receivable activity is branch-scoped through `CustomerLedgerEntry`. Controllers do not calculate balances or mutate ledger state directly.

Customer codes are generated by a PostgreSQL sequence and are globally unique. Customer names are normalized for search; optional email values are unique only when provided. Phone numbers are not unique. Customers are activated/deactivated rather than deleted, so sale and ledger history remains stable. Inactive customers are excluded from normal lookup and cannot be used for new credit sales.

`CustomerLedgerEntry` is the customer balance source of truth. Positive amounts mean receivable from the customer. Negative amounts mean payment, credit reduction, adjustment credit, or customer advance. Opening balance allows either sign and is recorded only during customer creation. Editing customer master data does not rewrite opening balance. Payments create immutable `CustomerPayment` rows plus negative ledger entries. Debit adjustments are positive; credit adjustments are negative. PostgreSQL enforces ledger sign validity through `CK_CustomerLedgerEntries_AmountSign`.

Credit and partial-credit sales require `sales.credit`, an active customer, and available credit. Sale posting runs in a serializable PostgreSQL transaction; it validates stock through FEFO, updates stock projections, records sale stock movements, stores sale payments, and creates a positive customer ledger `CreditSale` entry for the unpaid amount. `Sales.CreditAmount` plus `Sales.AmountPaid` must equal the posted sale net total, and PostgreSQL requires credit sales to reference a customer.

Sales returns for credit sales reduce customer receivable before requiring cash refund settlement. The return stores the split in `CustomerCreditReductionAmount` and `CashRefundAmount`; PostgreSQL enforces that their sum equals `RefundAmount`.


## Purchasing and Goods Receiving

Phase 6 purchasing operations are exposed through `IPurchasingService`. Purchase orders are branch-scoped planning documents: draft orders can be edited, submitted orders can be received, and partially/completely received orders are protected from cancellation in this phase.

Goods receipt posting is the stock and payable boundary. Direct purchases post without a purchase order; ordered receipts must reference a matching purchase order item. Paid quantity increments purchase-order received quantity and payable value. Bonus quantity increases inventory but does not increase supplier payable.

Posting runs in a serializable PostgreSQL transaction. It creates or reuses a compatible `ProductBatch`, updates `ProductBatch.QuantityAvailable` and `Inventory.QuantityInStock`, creates a positive `Purchase` `StockMovement`, and creates a positive supplier ledger `Purchase` entry when the receipt net total is greater than zero. Existing batch metadata must match expiry, manufacturing date, supplier, and prices.

Supplier invoice numbers are optional and normalized. PostgreSQL enforces uniqueness per supplier when an invoice number is present while allowing multiple null invoices. Posted goods receipts and receipt items are immutable through DbContext validation. Supplier payment allocation, tax reporting, and accounting general ledger posting remain future phases.

## Purchase Returns

Phase 9 purchase-return operations are exposed through `IPurchasingService` and `PurchaseReturnsController`. A return must reference a posted original goods receipt and must select quantities from the original `GoodsReceiptItem` rows. The service never creates an arbitrary supplier-return line because the product, batch, branch, supplier, price, discount, tax, and receipt economics come from the historical receipt.

Posting runs in a serializable PostgreSQL transaction. It verifies branch access, return permissions, posted receipt state, original receipt-item ownership, original batch identity, remaining paid quantity, remaining bonus quantity, current physical stock, and reason/notes rules. Partial returns are cumulative; over-return attempts are rejected against previous posted purchase returns.

Paid and bonus quantities are stored separately in `PurchaseReturnItem`. Paid return quantities contribute to `GrossReturnAmount`, `DiscountAdjustment`, `TaxAdjustment`, and `NetSupplierCredit`; bonus-only returns carry zero supplier credit while still reducing physical stock. Final paid returns receive rounding residuals so cumulative supplier credit cannot exceed the original receipt item net amount.

Posting reduces the original `ProductBatch.QuantityAvailable` and matching `Inventory.QuantityInStock`, records a negative `PurchaseReturn` `StockMovement`, and creates a negative supplier ledger `PurchaseReturn` entry only when `NetSupplierCredit` is greater than zero. Inactive suppliers/products and expired batches remain returnable when tied to the historical receipt. Posted `PurchaseReturn` and `PurchaseReturnItem` rows are immutable through `PharmacyDbContext`. Return numbers use a PostgreSQL sequence plus a unique index.

## POS and Sales

Phase 7 sales operations are exposed through `ISalesService`. POS product search is branch-aware and returns active catalog products with currently sellable stock, nearest expiry, indicative batch retail price, and maximum discount. Exact barcode/SKU matches are prioritized before text matches.

Sale posting is the inventory and receipt boundary. The service validates branch access, line quantities, payment shape, product activity, discount permission, and product maximum discount before entering a serializable PostgreSQL transaction. It then calls `FefoAllocationService` for each line and persists the resulting batch allocations in `SaleItemBatchAllocations` with quantity, expiry, retail price, sale price, and cost snapshots.

Posting updates `ProductBatch.QuantityAvailable` and `Inventory.QuantityInStock`, records matching negative `Sale` stock movements, stores sale items/payments, and writes audit entries in one unit of work. Held sales store the cart and customer snapshot only; they do not reserve inventory. A held sale is revalidated against current stock when posted.

Posted sales are immutable through `PharmacyDbContext`: existing posted sale rows cannot be changed or deleted, and sale payment/allocation history cannot be edited or deleted. Held sales can be updated or cancelled. Tax rules, shift/cash-drawer controls, and accounting journal integration remain later phases.

## Sales Returns and Refunds

Phase 8 sales-return operations are exposed through `ISalesReturnService`. A return must reference a posted original sale and must select quantities from persisted `SaleItemBatchAllocation` rows. The service never invokes FEFO for returns because the source batch is already known from the original sale.

Posting runs in a serializable PostgreSQL transaction. It verifies branch access, original-sale state, allocation ownership, remaining returnable quantity, return reason, refund permission, refund payment total, and batch disposition. Partial returns are cumulative; over-return attempts are rejected against the original allocation quantity and prior posted returns.

Restockable returns create a positive `SaleReturn` stock movement and increase the original `ProductBatch.QuantityAvailable` and `Inventory.QuantityInStock` projections. Non-resellable returns create a positive `SaleReturn` movement plus a negative `Damaged` movement, or `Expired` when the original batch is expired, leaving sellable projections unchanged. Disposed batches cannot be restocked.

Refund amounts are derived from the original allocation snapshots for sale price, discount, tax, and cost. The final partial return receives any rounding residual so cumulative refund cannot exceed the original sale economics. Cash-sale returns require refund payments equal to the refund amount. Customer-credit returns reduce customer receivable first and require refund payments only for the remaining cash refund amount. Posted `SalesReturn`, `SalesReturnItem`, `SalesReturnAllocation`, and `SalesRefundPayment` rows are immutable through `PharmacyDbContext`. Return numbers use a PostgreSQL sequence plus a unique index.

## Accounts, Expenses, and Cash Management

`FinancialLedgerEntry` is the permanent source of truth for operational account balances. Positive entries are inflows and negative entries are outflows. `FinancialAccount.OpeningBalance` records the account-creation input, while the actual balance is always derived from ledger entries. Opening balances, expenses, other income, transfers, adjustments, sale payments, customer receipts, supplier payments, and sales refunds use explicit entry types; clients never submit an arbitrary signed amount or editable balance.

Financial accounts are branch-scoped. Money-out operations use serializable transactions and account-row locking. A PostgreSQL insert guard enforces the matching branch and active state and rejects an outflow that would take the account below zero, including concurrent attempts. Posted finance documents and ledger rows are protected from update/delete in both `PharmacyDbContext` and PostgreSQL triggers.

Sales payments and customer payments create positive financial entries. Supplier payments and sales-refund payments create negative entries. The credit portion of a sale creates only a customer-ledger receivable. Purchase returns continue to reduce supplier payable without fabricating a cash receipt. Pre-Phase-11 payment rows remain nullable for `FinancialAccountId`; the migration deliberately does not invent historical cash movements.

Daily cash position is derived from the same ledger: balance before the business day plus the day's inflows minus outflows equals closing balance. This module is operational cash management, not a double-entry general ledger, inventory valuation journal, tax engine, bank reconciliation, payroll, or external banking integration.

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

There are 37 mapped application entities/tables. PostgreSQL also creates `__EFMigrationsHistory` when migrations are applied.

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
| Customer | Customers | Global customer catalog |
| Product | Products | Global product catalog |

Branch-scoped operational data:

| Entity | Table | Scope |
|---|---|---|
| User | Users | Assigned to one branch |
| ProductBatch | ProductBatches | Branch/product batch |
| Inventory | Inventory | Branch/product/batch projection |
| StockMovement | StockMovements | Branch/product/batch ledger |
| SupplierLedgerEntry | SupplierLedgerEntries | Branch/supplier financial ledger |
| CustomerLedgerEntry | CustomerLedgerEntries | Branch/customer receivable ledger |
| CustomerPayment | CustomerPayments | Branch/customer payment document |
| PurchaseOrder | PurchaseOrders | Branch/supplier planning document |
| PurchaseOrderItem | PurchaseOrderItems | Purchase order line |
| GoodsReceipt | GoodsReceipts | Branch/supplier posted receipt |
| GoodsReceiptItem | GoodsReceiptItems | Posted receipt line linked to product/batch |
| PurchaseReturn | PurchaseReturns | Branch/supplier posted return against original GRN |
| PurchaseReturnItem | PurchaseReturnItems | Return line linked to original receipt item and batch |
| Sale | Sales | Branch/cashier held or posted sale document |
| SaleItem | SaleItems | Sale line linked to product |
| SaleItemBatchAllocation | SaleItemBatchAllocations | Posted FEFO batch allocation and price snapshot |
| SalePayment | SalePayments | Posted sale payment line |
| SalesReturn | SalesReturns | Branch-scoped posted return/refund document |
| SalesReturnItem | SalesReturnItems | Return line linked to original sale item |
| SalesReturnAllocation | SalesReturnAllocations | Return quantity linked to original sale batch allocation |
| SalesRefundPayment | SalesRefundPayments | Refund payment line |
| FinancialAccount | FinancialAccounts | Branch-scoped cash/bank/wallet/settlement account |
| FinancialLedgerEntry | FinancialLedgerEntries | Immutable branch/account money ledger |
| Expense | Expenses | Immutable posted branch expense |
| OtherIncome | OtherIncomes | Immutable posted non-sales income |
| FinancialTransfer | FinancialTransfers | Immutable paired account transfer document |

Global finance catalog:

| Entity | Table | Scope |
|---|---|---|
| ExpenseCategory | ExpenseCategories | Global customizable expense category catalog |
| SystemSetting | SystemSettings | Global controlled configuration keys |
| BackupRecord | BackupRecords | Global backup operation metadata; never contains credentials |

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
- Customer code and optional email values have unique indexes. Customer credit limit is non-negative.
- Customer ledger sign rules are enforced by `CK_CustomerLedgerEntries_AmountSign`; payment amounts are positive.
- Purchase order status, ordered/received quantity range, receipt status, receipt totals, receipt item quantity, price, discount, tax, net amount, and manufacturing-before-expiry rules have PostgreSQL checks.
- Sales status, posted invoice requirement, posted sale settlement totals, customer-required credit sales, non-negative sale money, sale item quantity/discount/money, sale allocation quantity/money, and sale payment method/cash tender rules have PostgreSQL checks.
- Sales-return status, posted return requirement, reason, non-negative totals, customer-credit/cash-refund settlement totals, return-item quantity/money, return-allocation quantity/disposition/money, and refund-payment method/amount rules have PostgreSQL checks.
- Purchase-return status, posted return requirement, reason, non-negative totals, and paid/bonus quantity rules have PostgreSQL checks.
- Purchase order numbers, GRN numbers, supplier invoice numbers, customer codes, customer payment receipt numbers, sale invoice numbers, held sale numbers, sales-return numbers, and purchase-return numbers have unique indexes scoped to their business rules.
- FEFO, branch, active-state, audit, stock-ledger, supplier, supplier-ledger, customer, customer-ledger, customer-payment, purchase-order, goods-receipt, purchase-return, POS search, sales history, sale payment, sales-return, refund-payment, and receipt query paths have supporting indexes.
- Foreign keys and delete behaviors are defined in `PharmacyDbContext` and generated into the migration.
- Audit old/new value columns are configured as PostgreSQL `jsonb`.
- Financial account names are unique per branch. Expense and other-income amounts are positive, transfer accounts differ, and financial ledger amounts are non-zero with type-specific signs.
- PostgreSQL guards lock the account row for every financial entry, enforce account/branch consistency, prevent negative balances under concurrent spending, and reject updates/deletes to posted financial history.
- Finance lookup indexes cover account/date, branch/date, type/date, references, expense dates, and unique document numbers.

These statements are verified in the EF model, migrations, and real PostgreSQL 17 catalogs. Rollback-isolated integration tests also exercise nullable unique barcodes, scoped batch uniqueness, JSONB/date/timestamp mappings, stock sign checks, non-negative inventory constraints, ledger/projection consistency, supplier uniqueness, supplier financial checks, supplier ledger sign checks, customer uniqueness, customer ledger sign checks, customer payment checks, customer permission seeds, purchase/receipt uniqueness, purchase quantity checks, receipt item checks, purchasing permission seeds, sales permission seeds, sale/receipt uniqueness, sale payment checks, sale allocation checks, sales credit constraints, sales-return uniqueness, sales-return checks, refund payment checks, customer-credit return settlement, return allocation checks, sales-return permission seeds, purchase-return uniqueness, purchase-return checks, purchase-return foreign keys, purchase-return supplier-ledger/stock-movement signs, purchase-return permission seeds, and return-number sequence existence.

## Dates, Time, Money, and Quantity

- Base entity timestamps, login/count timestamps, and health timestamps are UTC `DateTime` values mapped to PostgreSQL timestamps with time zone.
- Manufacturing and expiry are date-only business values mapped to PostgreSQL `date`.
- Product and batch money use `decimal(18,2)`.
- Supplier/customer opening balances, credit limits, supplier/customer ledger amounts, customer payments, purchase prices, purchase receipt totals, purchase-return totals, sale totals, sales-return totals, and refund payments use `decimal(18,2)`.
- Maximum discount percentage, purchase discount percentage, and purchase tax percentage use `decimal(5,2)`.
- Stock quantity, reorder levels, and pack sizes use integers.
- Unit is a product label and pack size is retained, so future box/strip/tablet/bottle/piece modeling is not blocked. Unit conversion is not implemented.

## Migration Status

*(Updated 2026-09-13 during Phase 7 hardening — the list below was last accurate at Phase 13/EnforceAuditImmutability; this repo has since grown through the multi-godown, stock-transfer, sales-expansion/wholesale, full accounting-engine, aging/vouchers, and Phase 6 pricing/automation additions. Re-run `dotnet ef migrations list` for the current source of truth rather than trusting a static list here going forward.)*

- Directory: `backend/Pharmacy.Infrastructure/Migrations`
- Snapshot: `PharmacyDbContextModelSnapshot.cs`
- EF reports no pending model changes.
- Applied to: local `pharmacy_dev` and isolated `pharmacy_test`
- EF history: 36 migrations, latest `20260912184401_AddSubmissionFingerprintGuard`, recorded with product version `10.0.11`
- Real schema: 84 application tables plus `__EFMigrationsHistory` (85 total), with foreign keys, constraints, triggers, and operational indexes verified in PostgreSQL 17

## Flutter Foundation

The Flutter project contains a Material desktop shell and permission-aware operational screens through System Administration. Administration provides audit, recycle-bin, branch, settings, backup, and system-information workspaces based on the current user's permissions. The API base URL is supplied with `API_BASE_URL`. Tokens are stored through `flutter_secure_storage`, restored through `/api/auth/me`, and cleared on logout.

## Current Limitations

*(Updated 2026-09-13 — several bullets below were true at Phase 13 and are explicitly corrected; see `docs/PERMISSION_MATRIX.md` and the Phase 7 completion report for what actually exists today.)*

- Deployment database provisioning and production operations are now covered by `docs/PRODUCTION_DEPLOYMENT.md`, `docs/DEPLOYMENT_CHECKLIST.md`, `docs/OPERATIONS_RUNBOOK.md`, and `docs/DISASTER_RECOVERY.md`.
- No refresh tokens or general-purpose server-side token revocation list; token versions invalidate sessions after security-sensitive user changes. This is still accurate — session lifetime is bounded by `Jwt:ExpirationMinutes` plus explicit "sign out everywhere."
- **No longer accurate**: a full double-entry chart-of-accounts/journal engine, typed vouchers with reversal, bank reconciliation, accounting periods with soft/hard close, budgets, cost centers, party credit/debit notes/write-offs/advances, supplier/customer payment allocation to specific documents, and true AR/AP aging all exist today (see the `AccountsX` permission groups in `docs/PERMISSION_MATRIX.md`).
- **No longer accurate**: shift/cash-drawer closing exists (Cashier Shift feature — open/close/reconcile with expected-vs-counted cash variance).
- Audit CSV export, point-in-time audit field snapshots, scheduled/automatic backups, and in-app restore are still not implemented. Backups are manual-trigger only (with retention); restore is deliberately an offline maintenance procedure — see `docs/DISASTER_RECOVERY.md`.
- Settings currently cover business/receipt presentation only; currency is PKR and timezone is Asia/Karachi. Tax, numbering, and finance defaults remain owned by their existing modules.
- No role-permission mutation UI/API yet — role grants are seeded via migration and are not editable at runtime.
- Still genuinely absent: exchange/store-credit return flow, receipt-less return flow, supplier cash-refund settlement for purchase returns, unit conversion, background/scheduled job execution (automation and recurring journals are manually triggered), and physical (OS-level/thermal-printer) receipt printing (receipts are generated and viewable/reprintable on-screen, but nothing yet sends them to a printer — see `docs/RELEASE_NOTES.md`).

## Read-only Reporting

`ReportingService` owns permission, branch, UTC-range, and pagination validation. `ReportingRepository` uses `AsNoTracking`, projection, SQL aggregation, bounded detail pages, and existing transactional indexes. Controllers contain only HTTP routing and safe CSV serialization. Reports have no write repository methods and no report entity/table.

Sales net after returns equals posted sale net less separately posted returns. Payment-method totals include `SalePayment` only; credit is shown separately. Product profitability uses `SaleItemBatchAllocation.UnitCostPriceSnapshot` and reverses revenue and cost through `SalesReturnAllocation`, never current batch prices. Category reports use the current product category because no historical category snapshot exists.

Purchase reports distinguish paid quantity, bonus quantity, and supplier credit. Inventory valuation is operational batch quantity multiplied by batch purchase price. Customer, supplier, and financial balances are ledger sums. Consolidated cash flow excludes transfer-in/out from external business activity while closing balance still includes all account ledger movements. Asia/Karachi business dates are converted to explicit half-open UTC ranges; date-only purchase fields are compared using Karachi business dates.

*(Updated 2026-09-13)* The two bullets that used to close this document are now stale and corrected:
- CORS defaults to **deny cross-origin entirely** outside Development unless `Cors:AllowedOrigins` is explicitly configured — the opposite of "permissive." This was verified directly against `Program.cs` during Phase 7 hardening.
- API error handling is a real, consistent `ProblemDetails` envelope (with a correlation ID) for validation/404/409/403/401/500, and Setup-Owner is rejected once any user exists — see the Phase 7 completion report for the full audit. Structured file logging (rotated, retention-limited) and startup/shutdown logging were added in Phase 7 as the one genuine gap this line was pointing at.



