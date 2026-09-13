# Desktop UI redesign audit

Baseline: existing committed Phase 6, Phase 7 commercial-hardening and sidebar-scroll checkpoints, plus Claude's uncommitted DashboardScreen, grouped shell navigation, additional widget tests, and Windows product/window naming. These changes are retained. Empty root files `cls` and `dotnet` were present before this work and are not application assets.

## Architecture and scope

MaterialApp owns authentication through AuthState (ChangeNotifier/ListenableBuilder). AppShell builds permission-filtered pages and selects them locally; module screens use StatefulWidget, local controllers and existing AuthState API methods. There is no external routing/state framework to replace.

Audited modules: login/password/profile, shell/dashboard, POS/history/returns/shifts, wholesale/quotations/orders/pricing/automation, catalog/products, inventory/counts/adjustments, godowns/transfers, purchasing/PO/GRN/returns, customers/suppliers, finance and all accounting views, reports/MIS, users and administration.

Repeated structures include Material DataTables, nested horizontal/vertical scroll views, title/action rows, TextField/dropdown decorations, status Text cells, AlertDialogs and plain loading/empty/error centers. Accounting has a small separate header/error/table helper. These need one visual system, while retaining module-specific validation, actions and calculations.

Desktop risks: long navigation needs its own scroll area; headers should not be fake destinations; tables need bounded vertical space and horizontal scrolling; wide filter/form rows must wrap; POS search/results and cart currently compete for space; a single godown is currently invisible in POS. Target viewport checks: 1366×768, 1440×900, 1920×1080, plus existing 800×600 widget-test compatibility.

## Genuine integration defect discovered before modification

Claude's dashboard converts paged typed SaleListItem and PurchaseOrderListItem objects with a map-only parser. It silently drops those records, hiding recent sales and pending POs. Fix by explicitly mapping their existing display fields; no API or calculation change. Individual feed failures also currently appear as empty/zero data; surface unavailable feeds instead of representing them as business zeroes.

The shell replaces the selected page widget on navigation, destroying local POS state and an unfinished cart. Retain visited pages lazily with inactive tickers disabled; preserve permissions and avoid eager startup API calls. This is a navigation-state correction, not a sale or inventory behavior change. The regression check also found sidebar focus surviving a return to POS; active POS restores scanner focus, and inactive page focus scopes cannot receive keyboard input.

## Boundaries

Frontend presentation and keyboard interaction only. Authentication, role gates, API contracts, database/migrations, FEFO, batch allocation, stock/ledger posting, payments, discounts, tax, credit, returns/refunds and report formulas remain unchanged. Product search API does not expose actual FEFO batch IDs/details before posting; show available nearest-expiry metadata and document the limitation rather than inventing batch selection or APIs.
