# Release Notes

## Pharmacy Management System / POS — Initial Release

A complete point-of-sale, inventory, purchasing, accounting, and management system for a multi-branch pharmacy business, covering everything from the sales counter to the general ledger.

### Sales and point of sale

- Fast barcode/search-driven checkout with FEFO (first-expiry-first-out) batch selection, split payments (cash, card, bank), change calculation, and printed/reprintable receipts.
- Hold and recall sales for interrupted transactions.
- Retail, wholesale, and price-level-driven pricing, with quantity-break pricing, configurable pricing rules and promotions, and controlled bulk price updates.
- Sales quotations and sales orders, with conversion into an invoice that always honors the originally quoted/ordered price.
- Credit sales with per-customer credit limits, and full or partial sales returns/refunds against the original invoice.
- Cashier shift tracking with expected-vs-counted cash reconciliation.

### Inventory and purchasing

- Multi-branch, multi-godown (warehouse/store) stock tracking with a full transaction ledger — every stock change is traceable to its source.
- Purchase orders, goods receiving (direct or against an order), and purchase returns, with supplier ledger integration and paid/bonus quantity handling.
- Physical stock counting sessions, stock adjustments, expiry tracking and disposal, and inter-godown stock transfers with dispatch/receive tracking and shortfall write-off.
- Automated low-stock and near-expiry alerts, and reorder suggestions that can be turned into draft purchase orders for manager review.

### Customers and suppliers

- Customer and supplier master records with credit terms, activation/deactivation (never hard deletion, so history stays intact), and full payment/ledger history.
- Payment recording with automatic allocation against the oldest open invoices, and accounts receivable/payable aging.

### Accounting

- A full double-entry chart of accounts and journal engine underlies every posting in the system — sales, purchases, returns, payments, and expenses all post a real, balanced journal entry automatically.
- Typed vouchers (cash/bank receipts and payments, contra, manual journal) with reversal support.
- Bank reconciliation, accounting periods with soft/hard close, budgets vs. actuals, cost centers, and party (customer/supplier) credit notes, debit notes, write-offs, and advances.
- General ledger, trial balance, profit & loss, and balance sheet reporting, all reading directly from posted journal entries.

### Reporting and management information

- Sales, purchase, inventory, financial, and profitability reports with branch/date/product/category filtering and CSV export.
- A management overview dashboard, cash position and cash-flow reporting, and dedicated aging/outstanding-balance reports.
- Cost, margin, and profit figures are only ever shown to users with the appropriate permission — this is enforced by the server, not just hidden in the interface.

### Security, administration, and operations

- Role-based permissions (Owner, Manager, Accountant, Pharmacist, Cashier, PurchaseManager, StoreKeeper by default) enforced on every server action, independent of what the desktop client displays.
- Full audit logging of sensitive actions, a recycle bin for safely reversible deletions, and branch/system administration screens.
- Manual, verified database backups (native PostgreSQL format) with configurable retention, and a documented, tested disaster-recovery procedure.
- Health-check endpoints for monitoring, rotated log files for support diagnostics, and a System Information view for on-the-spot troubleshooting.
- Windows desktop client with single-instance protection, a configurable server address, and clear messaging when the server or database is unreachable.

### Known scope boundaries for this release

- The system requires a live connection to its backend/database; there is no offline transaction mode.
- Receipts (sale, return, payment) are generated and can be viewed and reprinted at any time from sale/return history; direct OS-level/thermal-printer output is not yet wired up in this release, so a completed transaction is never at risk from a printing step that doesn't exist yet. Treat this as a decision point before go-live if a physical printed receipt at the counter is required.
- Mobile apps, e-commerce/loyalty platforms, AI-driven forecasting or autonomous purchasing, and clinical/prescription workflows are explicitly out of scope for this release.
