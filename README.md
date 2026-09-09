# Pharmacy Management System POS

Pharmacy management system with completed Phase 12 operational Reports and Analytics, plus a double-entry accounting engine (Chart of Accounts, Journal, automatic posting), for an ASP.NET Core API and Flutter Windows client. Reports are read-only projections over verified PostgreSQL transactional data. Core sales, purchasing, returns, payment, expense, income, transfer, opening-balance, and inventory-adjustment flows are wired to the accounting engine. Bank reconciliation has not started.

## Current Scope

- Clean Architecture backend targeting .NET 10
- PostgreSQL persistence through EF Core and Npgsql
- JWT login, PBKDF2 password hashing, temporary lockout, forced password changes, token-version invalidation, and dynamic permission policies
- Paged user management, profile editing, activation/deactivation, password reset, Owner protection, and security audit events
- Idempotently seeded roles, focused permission catalog, and customizable role-permission defaults
- Branch-aware inventory entities, permanent stock ledger, and controlled current-balance projections
- Reusable FEFO batch allocation service
- Flutter desktop shell, secure token storage, session restoration, login, forced password change, users, customers, and profile screens
- Product, category, and manufacturer administration with permission-aware desktop screens, server-side product paging/filtering, immutable SKU, and activation workflows
- Controlled opening stock, stock adjustments, single-batch stock count reconciliation, session-based physical stock counting (Draft/InProgress/Completed/Cancelled with full/category/selected-product/selected-batch scope and variance posting on finalize), expiry disposal, branch inventory views, batch views, movement ledger, valuation, and FEFO preview
- Supplier master management with activation, search, lookup, branch-scoped financial ledger, opening balances, payments, and balance adjustments
- Purchase orders, direct purchases, goods receiving, supplier invoice uniqueness, paid/bonus quantity handling, inventory posting, supplier payable ledger integration, immutable original-GRN purchase returns, supplier credit ledger entries, POS checkout, sales posting, held sales, split payments, customer credit settlement, receipt preview/reprint, sales history, original-allocation sales returns, customer-credit reduction, refunds, and return receipt history
- Customer master management with activation, lookup, branch-scoped receivable ledger, opening balances, payments, balance adjustments, and credit-limit enforcement
- Cashier shift management: open/close/reconcile with expected-vs-actual cash variance, manual drawer cash-in/cash-out, per-payment-method breakdown, shift history, and a daily branch closing summary
- Double-entry accounting engine: global chart of accounts (seeded), semantic account mappings, balanced/immutable journal entries, manual journal vouchers, trial balance, and automatic journals for sales, purchases, returns, payments, expenses, other income, account transfers, opening balances, and stock adjustments/write-offs
- Backend unit/foundation and PostgreSQL integration tests, plus Flutter widget tests
- Branch- and permission-scoped sales, purchase, inventory, financial, profitability, and dashboard reports with safe CSV export

A double-entry general ledger now exists and the core operational posting paths are wired to it (see Accounting Engine Policy below), including manual customer/supplier/financial-account adjustments and cashier drawer and reconciliation variances. Supplier cash-refund settlement for purchase returns, exchange/store-credit returns, bank reconciliation, and true invoice aging are not implemented.

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
backend/Pharmacy.Infrastructure/Migrations/20260902051500_CompleteSupplierManagement.cs
backend/Pharmacy.Infrastructure/Migrations/20260902055022_CompletePurchasingAndGoodsReceiving.cs
backend/Pharmacy.Infrastructure/Migrations/20260902114037_CompletePosAndSales.cs
backend/Pharmacy.Infrastructure/Migrations/20260902222101_CompleteSalesReturnsAndRefunds.cs
backend/Pharmacy.Infrastructure/Migrations/20260903231207_CompletePurchaseReturns.cs
backend/Pharmacy.Infrastructure/Migrations/20260904125716_CompleteCustomerManagementAndCreditSales.cs
backend/Pharmacy.Infrastructure/Migrations/20260907195029_CompleteAccountsExpensesAndCashManagement.cs
backend/Pharmacy.Infrastructure/Migrations/20260907210154_AddReportingPermissions.cs
backend/Pharmacy.Infrastructure/Migrations/20260907220809_CompleteSystemAdministration.cs
backend/Pharmacy.Infrastructure/Migrations/20260907222557_EnforceAuditImmutability.cs
backend/Pharmacy.Infrastructure/Migrations/20260909151127_CompletePhysicalStockCounting.cs
backend/Pharmacy.Infrastructure/Migrations/20260909160946_CompleteCashierShiftManagement.cs
backend/Pharmacy.Infrastructure/Migrations/20260909162909_CompleteAccountingEngine.cs
backend/Pharmacy.Infrastructure/Migrations/20260909165535_AddInventoryAdjustmentGainMapping.cs
backend/Pharmacy.Infrastructure/Migrations/20260909182137_EnforceJournalSourceIdempotency.cs
backend/Pharmacy.Infrastructure/Migrations/20260909184503_CompleteAdjustmentAndCashControlAccounting.cs
```

All twenty migrations are applied to local `pharmacy_dev` and `pharmacy_test` through the non-superuser `pharmacy_app_dev` role. The schema is 48 application tables plus `__EFMigrationsHistory`. Credentials remain outside the repository. See [QUICK_START.md](QUICK_START.md) for safe local configuration.

## Reporting Policy

Reports never mutate data or persist report snapshots. Sales and returns remain separate transactions; net sales is posted sale net less posted return value. Cash collected at sale is distinct from credit created and later customer receipts. Product cost and gross profit use historical sale-allocation cost snapshots, including allocation-linked return reversals. Inventory value is the operational sum of available batch quantity times batch purchase price, not an accounting valuation journal. Purchases keep paid and bonus quantities separate, and bonus-only returns have zero supplier credit.

Financial reports use the financial, customer, and supplier ledgers. Consolidated cash activity excludes internal transfers from external inflow/outflow, while per-account ledgers retain transfer entries. True customer/supplier invoice aging is intentionally deferred because payments are not allocated to individual documents. API date ranges are explicit UTC half-open boundaries derived from Asia/Karachi business dates by the client. CSV export uses quoted escaping and the same permission and branch scope as screen reports.

## Operational Finance Policy

`FinancialLedgerEntries` is the permanent source of truth for balances. Positive entries mean money enters an account; negative entries mean money leaves it. Balances are derived from ledger sums and cannot be edited directly. Opening balances are recorded once. Posted expenses, other income, transfers, and ledger entries are immutable.

Accounts are branch-scoped. PostgreSQL locks the account row during ledger insertion and rejects outflows that would make an account negative. Actual sales payments and customer receipts create positive entries; supplier payments and cash refunds create negative entries. Credit portions of sales affect only the customer receivable ledger, and purchase returns affect only supplier payable unless a separate cash receipt is posted.

Daily cash position is calculated only from the financial ledger: opening plus inflows minus outflows equals closing. This operational cashbook remains distinct from the double-entry accounting engine described in Accounting Engine Policy below, even though its core posting paths now create atomic journal entries too; it is not a tax system, bank reconciliation system, or financial-statement engine on its own.

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
- `StockCountSession`/`StockCountItem` provide a session-based physical count workflow (Draft → InProgress → Completed/Cancelled) layered on top of the same stock movement primitives: Draft snapshots system quantity and cost per selected batch (full branch, category, selected products, or selected batches); Start locks the count into InProgress; count entries are staged on the session without touching stock; Finalize posts one `AdjustmentIncrease`/`AdjustmentDecrease` movement per counted line with a non-zero variance in a single transaction and is blocked once the session is no longer InProgress (no duplicate finalization). Completed and cancelled sessions are immutable. The original single-batch `POST /api/inventory/stock-count` quick-reconcile endpoint remains for ad-hoc corrections and is unrelated to sessions.
- Stock adjustment reasons cover physical count correction, damaged, expired, broken, leakage, theft/loss, missing, data correction, and other. An adjustment that moves at least 100 units or at least half of a batch's current stock is flagged `Significant` in its audit entry (and the audit action name itself gains a `Significant` suffix) so large or unusual manual adjustments stand out from routine ones without a separate review workflow.
- Positive movements: `OpeningStock`, `Purchase`, `SaleReturn`, `TransferIn`, `AdjustmentIncrease`.
- Negative movements: `Sale`, `PurchaseReturn`, `TransferOut`, `AdjustmentDecrease`, `Expired`, `Damaged`.
- Application validation and a PostgreSQL check constraint reject zero quantities and incorrect signs.
- PostgreSQL also rejects negative batch/inventory projections and negative batch prices.

## Supplier Management Policy

- `Supplier` is a global master record. Supplier selection is shared across branches, while financial ledger entries are branch-scoped.
- Supplier names are normalized and globally unique. Inactive suppliers remain in historical records and are hidden from normal lookup.
- `SupplierLedgerEntry` is the supplier balance source of truth. Outstanding balance is calculated from immutable ledger entries, not a mutable balance column.
- Positive supplier ledger amounts mean payable to the supplier. Negative amounts mean supplier advance/credit in favor of the pharmacy.
- Opening balance can be positive or negative and is recorded once as a ledger entry when a supplier is created. Editing supplier master data does not rewrite opening balance.
- Payments are recorded as negative ledger entries. Debit adjustments are positive; credit adjustments are negative.
- Supplier ledger entries cannot be updated or deleted through the DbContext. Purchase-linked supplier ledger entries are created by posted goods receipts.

## Customer Management and Credit Sales Policy

- `Customer` is a global master record. Customer financial activity is branch-scoped through `CustomerLedgerEntry`.
- Customer names are normalized for search, customer codes are unique, and optional email values are unique only when provided. Phone numbers are not unique.
- `CustomerLedgerEntry` is the receivable source of truth. Positive amounts mean customer receivable; negative amounts mean customer payment, credit reduction, adjustment credit, or customer advance.
- Opening balance can be positive or negative and is recorded once during customer creation. Editing customer master data does not rewrite opening balance.
- Payments are recorded as positive `CustomerPayment` documents plus negative ledger entries. Debit adjustments are positive; credit adjustments are negative.
- Customer credit sales require `sales.credit`, an active customer, and available credit. Posting a credit or partial-credit sale creates a positive customer ledger entry in the same transaction as stock movement and sale posting.
- Sales returns for credit sales reduce the customer ledger before requiring cash refund settlement. The backend records the split as `CustomerCreditReductionAmount` and `CashRefundAmount`.
- Customer ledger entries and payments are immutable through `PharmacyDbContext` validation and PostgreSQL check constraints.


## Purchasing and Goods Receiving Policy

- `PurchaseOrder` is a branch-scoped planning document. Draft orders can be edited, submitted orders can be received, and received orders are not cancelled in this phase.
- Goods receipt posting is the inventory-affecting event. Direct purchases have no purchase order; ordered receipts must reference the matching purchase order item.
- Paid quantity updates purchase-order received quantity and supplier payable. Bonus quantity increases stock without increasing payable.
- Posting creates or reuses a compatible product batch, increments batch and inventory projections, records a positive `Purchase` stock movement, and creates a positive supplier ledger purchase entry when net total is above zero.
- Supplier invoice number is optional, but when present it is unique per supplier by normalized value. Multiple receipts without invoice numbers are allowed.
- Posted goods receipts and receipt items are immutable through the DbContext. Supplier payment allocation and accounting settlement remain future phases.

## Purchase Returns Policy

- Purchase returns must reference a posted original goods receipt and its receipt items. Operators cannot return arbitrary products or batches.
- Paid and bonus quantities are tracked separately. Paid returns reduce supplier payable through a negative `PurchaseReturn` supplier ledger entry; bonus-only returns remove physical stock without creating supplier credit.
- Return posting removes stock from the original `ProductBatch` and matching `Inventory` projection, and records a negative `PurchaseReturn` stock movement for the same branch/product/batch.
- Partial returns are cumulative. The backend rejects over-return of paid quantity, over-return of bonus quantity, and attempts that exceed current physical stock.
- Return credit is calculated from the original receipt economics, including discount/tax adjustments. The final paid return receives residual rounding so cumulative supplier credit does not exceed the original receipt item net amount.
- Inactive suppliers/products and expired batches remain returnable when they belong to the historical original receipt. Disposed batches are still subject to current physical-stock checks.
- Posted purchase returns and return items are immutable through DbContext validation. Return numbers use a PostgreSQL sequence and unique index.


## POS and Sales Policy

- POS search uses active products and branch-scoped available batches. Exact barcode/SKU matches are prioritized, with name, brand, and generic search as fallback.
- Sale posting is the inventory-affecting event. Held sales do not reserve inventory and can become stale if stock changes before posting.
- Posting uses `FefoAllocationService` as the authoritative batch allocator. Operators do not manually override sale batches in Phase 7.
- FEFO allocations are persisted in `SaleItemBatchAllocations` with quantity, batch, expiry, retail price, sale price, and cost snapshots so future return/report workflows can trace the original source batch.
- Posting creates negative `Sale` stock movements and updates `ProductBatch.QuantityAvailable` plus `Inventory.QuantityInStock` in one serializable PostgreSQL transaction.
- Posted sales, sale payments, and sale batch allocations are immutable through DbContext validation. Held sales may be edited or cancelled before posting.
- Split payments are supported for cash, card, bank transfer, Easypaisa, JazzCash, and other. Cash requires tendered amount and records change. Non-cash payments may include a reference number.
- Customer credit and partial-credit sales require `sales.credit`, an active customer, and available credit. The unpaid amount is recorded as a positive customer ledger entry.
- Product-level discounts require `sales.discount` and cannot exceed `Product.MaximumDiscountPercent`. Tax is intentionally fixed at zero until a real tax model is introduced.
- Invoice numbers are generated in a serializable transaction and protected by a unique index. The current MVP does not include an automatic retry loop if a rare serial collision still reaches the database.

## Sales Returns and Refunds Policy

- Returns must reference a posted original sale and its persisted `SaleItemBatchAllocation` rows. Returns do not run FEFO and do not pick replacement batches.
- Partial and full returns are supported cumulatively. The backend rejects over-return attempts against the original allocation quantity.
- Restockable returns create a positive `SaleReturn` movement and increase the original batch and inventory projections.
- Non-resellable returns create a positive `SaleReturn` movement plus a negative `Damaged` or `Expired` movement for the same original batch, leaving sellable projections unchanged.
- Disposed batches cannot be returned as restockable. Expired batches can only be processed as non-resellable.
- Refund amounts are calculated from original sale price, discount, tax, and cost snapshots. Final partial returns receive any rounding residual so cumulative refund does not exceed the original allocation economics.
- For cash sales, refund payment totals must equal the backend-calculated refund. For customer-credit sales, the backend reduces customer receivable first and requires refund payments only for the remaining cash refund amount. Cash, card, bank transfer, Easypaisa, JazzCash, and other methods are supported.
- Posted sales returns, return items, return allocations, and refund payments are immutable through DbContext validation. Return numbers use a PostgreSQL sequence and a unique index.
## Cashier Shift Policy

- `CashierShift` bounds a till session (Open → Closed → Reconciled). Sales, refunds, and customer cash receipts are attributed to a shift by cashier + branch + time window (`[OpenedAtUtc, ClosedAtUtc or now)`), not a foreign key, since exactly one shift can be open per cashier at a time — opening a second shift while one is already open is rejected.
- `CashierShiftDrawerEntry` records manual cash-in/cash-out drawer movements (float top-ups, bank deposits, petty payouts) that aren't a sale, refund, or customer payment. Entries are permanent once recorded and only accepted while the shift is Open. Cash-in debits Cash and credits Drawer Clearing; cash-out reverses that policy.
- Closing a shift computes `ExpectedCash = OpeningCash + cash sales + cash customer receipts + manual cash in − cash refunds − cash-paid expenses − manual cash out`, freezes it alongside the counted `ActualCountedCash`, the resulting `CashVariance`, and a `CashierShiftPaymentSummary` row per payment method (for the full, not just cash, sales/refunds breakdown). A shift can only be closed once.
- Only the shift's own cashier can close it or add drawer entries unless the actor holds `cashier_shift.close_any`. Reconciliation is a separate manager-only step (`cashier_shift.reconcile`) available only on a Closed shift, and reconciled shifts are permanent. The approved shortage/excess is journaled at reconciliation against Cash Over and Short; a zero variance creates no journal.
- The daily closing summary aggregates every shift closed on a branch's Asia/Karachi business date (closed shifts only; a currently-open shift contributes to the open-shift count but not to the totals until it closes).

## Accounting Engine Policy

- `ChartOfAccount` is a global, hierarchical, company-wide chart of accounts (not branch-scoped); the branch dimension lives on `JournalEntryLine` so financial statements can still be filtered per branch. A default 34-account pharmacy chart is seeded (Assets/Liabilities/Equity/Income/Cost of Sales/Expenses), including contra-revenue, inventory gain/loss, adjustment suspense, drawer clearing, and Cash Over and Short accounts.
- `AccountMapping` binds a stable semantic role (`AccountMappingKey`, e.g. `Cash`, `AccountsReceivable`, `CostOfGoodsSold`) to the actual `ChartOfAccount` a business has configured for it. Posting logic never hard-codes a raw account id — it resolves the mapping at posting time and fails loudly if one is missing. All 18 mapping keys are seeded to sensible defaults out of the box. An account cannot be deactivated while it's the target of an active mapping, and mappings can only point to active, posting-level (non-header) accounts.
- `JournalEntry`/`JournalEntryLine` are the double-entry source of truth. `PharmacyDbContext` enforces, for every newly-added entry, that its lines' total debit equals total credit before the save is allowed to reach PostgreSQL, and posted entries (and their lines) can never be updated or deleted — corrections are new entries, never edits.
- `IJournalPostingService.PostAsync` is the internal capability other bounded-context services (Sales, Purchasing, Customers, Suppliers, Finance) call to post automatically. It takes semantic `AccountMappingKey` lines, never a raw account id, and only *stages* the entry into the shared `DbContext` — it never opens its own transaction or calls `SaveChanges`, so it always commits (or rolls back) atomically with whatever operational transaction it was posted from.
- Automatic journal sources are idempotent by `(SourceType, SourceId)`: the posting service detects both already-persisted and already-staged entries, and PostgreSQL enforces a filtered unique index for every non-null source id. Manual vouchers retain a null source id and are unaffected.
- Manual journal vouchers (`POST /api/accounts/journal`, permission `accounts.journal.post`) take raw `ChartOfAccountId` lines directly (the operator picks accounts from the chart) rather than semantic keys, and require at least two lines that balance.
- **Operational posting is wired to the engine**: sales post consideration/revenue and exact allocation-snapshot COGS/inventory; goods receipts post inventory/payables; purchase returns reverse inventory/payables; sales returns post contra-revenue/refunds or receivable reduction and reverse COGS for restockable allocations; customer and supplier payments post against their control accounts; expenses and other income post against the selected cash/bank account; account transfers move value between cash/bank mappings; customer, supplier, financial-account, and inventory opening balances offset retained earnings.
- Inventory increases use the batch purchase-price valuation to debit Inventory and credit Inventory Adjustment Gain. Manual decreases, damaged stock, and expired disposal debit Inventory Loss Expense and credit Inventory. Session-based counts post one journal per non-zero batch variance inside the same finalization transaction.
- Manual customer adjustments offset Accounts Receivable Adjustment Suspense; supplier adjustments offset Accounts Payable Adjustment Suspense; and financial-account corrections offset Cash and Bank Adjustment Suspense. These flows, drawer entries, and reconciled shift variances use dedicated semantic mappings and remain atomic with their operational records. Zero-value operational postings are omitted.
- There is no Chart of Accounts / Journal / Trial Balance screen in the Flutter app yet; the engine is API-only for now (`GET/POST /api/accounts/chart`, `/api/accounts/mappings`, `/api/accounts/journal`, `/api/accounts/trial-balance`).

## FEFO and Expiry

`FefoAllocationService` filters by branch and product, excludes disposed, expired, and empty batches, orders deterministically by expiry/creation/batch/id, and allocates across batches. Expiry and manufacturing dates use `DateOnly` and PostgreSQL `date`. A batch expiring on the sale date is considered sellable for that entire date in the current fixed MVP policy.

All stock quantities operate in the product's configured inventory unit. Box/strip/tablet conversion remains deferred. Batch pricing is independent and does not rewrite Product Master catalog prices.

## Precision

- Money, sales totals, purchase-return totals, return totals, supplier credits, customer credits, ledger amounts, payments, and refund payments: `decimal(18,2)`
- Supplier/customer opening balances, credit limits, purchase prices, and purchase receipt totals: `decimal(18,2)`
- Maximum discount, purchase discount percentage, and purchase tax percentage: `decimal(5,2)`
- Quantities and pack sizes: integer base-unit counts
- Product unit labels and pack size preserve room for future box/strip/tablet/bottle/piece conversion, but conversion is not implemented.
- Operational timestamps use UTC `DateTime`; medicine expiry/manufacturing values are date-only.

## Verification

## Release Candidate Operations

The API version is `0.1.0-rc.1`. Production migrations are explicit deployment operations and never run silently at API startup. Public liveness is process-only; readiness verifies PostgreSQL. Detailed system and backup diagnostics remain permission-protected.

Backups are written atomically through a `.partial` file, validated with `pg_restore --list`, then renamed to `.backup`. The default retention is the latest 10 completed archives inside the configured server-controlled directory. See `docs/DEPLOYMENT_CHECKLIST.md`, `docs/OPERATIONS_RUNBOOK.md`, and `docs/DISASTER_RECOVERY.md` before deployment.

## System Administration Policy

- Audit events are append-only. `PharmacyDbContext` rejects updates/deletes and centrally redacts password, secret, token, and connection-string fields from audit JSON.
- The recycle bin is deliberately limited to unreferenced `ProductCategory`, `Manufacturer`, and `ExpenseCategory` records. Transaction documents, ledgers, payments, stock movements, batches, users, branches, products, suppliers, customers, and financial accounts cannot enter it. There is no permanent-delete API.
- Branch codes are normalized and globally unique. A branch cannot be deactivated when it is the last active branch or has active assigned users.
- Settings use a controlled key catalog with optimistic version checks. Currency is fixed to PKR and the business timezone to Asia/Karachi in this phase.
- Sign-out-everywhere increments the user's token version, immediately invalidating existing JWTs at the next authenticated request.
- Backups use PostgreSQL `pg_dump` custom format, generated filenames, and a server-controlled local application-data directory. Database passwords are passed only through the child-process environment. Restore is a documented maintenance operation, not an application endpoint.

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
