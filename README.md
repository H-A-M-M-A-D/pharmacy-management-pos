# Pharmacy Management System POS

Pharmacy management system with completed Phase 11 Accounts, Expenses, and Cash Management foundations for an ASP.NET Core API and Flutter Windows client. The code and PostgreSQL schema are verified locally through PostgreSQL integration tests. Full general-ledger accounting, bank reconciliation, and reporting workflows have not started.

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
- Controlled opening stock, stock adjustments, stock count reconciliation, expiry disposal, branch inventory views, batch views, movement ledger, valuation, and FEFO preview
- Supplier master management with activation, search, lookup, branch-scoped financial ledger, opening balances, payments, and balance adjustments
- Purchase orders, direct purchases, goods receiving, supplier invoice uniqueness, paid/bonus quantity handling, inventory posting, supplier payable ledger integration, immutable original-GRN purchase returns, supplier credit ledger entries, POS checkout, sales posting, held sales, split payments, customer credit settlement, receipt preview/reprint, sales history, original-allocation sales returns, customer-credit reduction, refunds, and return receipt history
- Customer master management with activation, lookup, branch-scoped receivable ledger, opening balances, payments, balance adjustments, and credit-limit enforcement
- Backend unit/foundation and PostgreSQL integration tests, plus Flutter widget tests

Full general-ledger accounting, supplier cash-refund settlement for purchase returns, exchange/store-credit returns, bank reconciliation, and reporting modules are not implemented.

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
```

All eleven migrations are applied to local `pharmacy_dev` and `pharmacy_test` through the non-superuser `pharmacy_app_dev` role. The schema has 37 application tables plus `__EFMigrationsHistory`; Phase 11 adds branch financial accounts, an immutable financial ledger, expense categories, posted expenses, other income, transfers, finance permissions, sequences, constraints, indexes, and database guards. Credentials remain outside the repository. See [QUICK_START.md](QUICK_START.md) for safe local configuration.

## Operational Finance Policy

`FinancialLedgerEntries` is the permanent source of truth for balances. Positive entries mean money enters an account; negative entries mean money leaves it. Balances are derived from ledger sums and cannot be edited directly. Opening balances are recorded once. Posted expenses, other income, transfers, and ledger entries are immutable.

Accounts are branch-scoped. PostgreSQL locks the account row during ledger insertion and rejects outflows that would make an account negative. Actual sales payments and customer receipts create positive entries; supplier payments and cash refunds create negative entries. Credit portions of sales affect only the customer receivable ledger, and purchase returns affect only supplier payable unless a separate cash receipt is posted.

Daily cash position is calculated only from the financial ledger: opening plus inflows minus outflows equals closing. This operational layer is not a chart of accounts, double-entry general ledger, tax system, bank reconciliation system, or financial-statement engine.

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

