# Flutter Windows UI redesign handoff

## Baseline and audit

Resumed Claude's uncommitted dashboard, grouped navigation, widget tests and Windows naming changes. Preserved the existing committed repository, including its Phase 6 checkpoint and later pre-existing commercial-hardening/sidebar checkpoints. No new business phase or backend work was undertaken; redesign changes remain uncommitted. Pre-existing empty root files `cls` and `dotnet` were left alone.

The audit is recorded in [UI_REDESIGN_AUDIT.md](UI_REDESIGN_AUDIT.md). The application uses MaterialApp, AuthState/ChangeNotifier and locally selected StatefulWidget modules. Duplicate headers, tables, status badges, input/dialog styles and blank states were the main consistency problems. POS lacked a clear search/cart/checkout hierarchy, and shell navigation discarded local page state.

## Implemented design system

Central medical-tech palette: soft gray-green background, white bordered surfaces, dark emerald sidebar and restrained emerald actions. Semantic blue/amber/red statuses retain readable text, rather than relying on color alone. Local Inter font and its OFL license are bundled; Material icons remain the sole icon family. Central themes cover typography, dense tables, focus borders, 8px inputs/buttons, 10px cards, 12px dialogs and floating snackbars. No new runtime package was added.

Shared components: AppSidebar, AppPageHeader, AppSectionCard, AppStatCard, AppFilterBar, AppFormGrid, AppWorkflowStrip, AppStatusChip, AppDataTable, AppReportGrid, AppEmptyState, AppLoadingState and AppErrorState. Existing Material buttons, fields, date/select controls and dialogs use the global theme rather than redundant wrapper classes. Accounting header/error helpers delegate to this system.

## Screens and operational improvements

- Shell: permission-filtered grouped navigation, expanded/collapsed sidebar, hover/selected state, fixed logout/collapse controls and slim branch/user bar. Visited modules are retained lazily; inactive tickers/focus scopes are disabled.
- POS: dominant barcode/search field, visible single/multi-godown context, stock/nearest-expiry results, bounded cart area and separate checkout panel with prominent total/checkout and secondary hold. Click-to-edit quantity retains existing quantity/availability validation and pricing resolution; +/- controls remain. F2 search, F4 customer, F6 hold, F8 payment and Ctrl+N safe new-sale entry are supported. Returning to POS restores scanner focus. Existing payment dialog, receipt, returns, customer credit and price-source permissions are preserved.
- Dashboard: compact adaptive KPIs, revenue trend, operational alerts and recent transactions. Typed sales/PO records now render correctly; unavailable feeds are identified instead of silently appearing as empty results.
- Inventory, godowns and transfers: shared dense numeric tables, wrapping filter surfaces, consistent status/empty/error presentation, visible location controls and a transfer workflow strip.
- Purchasing/GRN: consistent tables and statuses, receiving workflow strip, spaced two-column desktop fields and visible single-godown context. Existing inline validators and posting caution remain.
- Sales/history/returns, customers/suppliers, catalog, wholesale, quotations/orders, price levels and Phase 6 automation: global forms/dialogs/typography/table design, semantic statuses and common states. Existing module-specific workflows and permission gates remain.
- Finance/accounts: common headers, serious ledger/table presentation, right-aligned numeric columns and tabular numerals. Account formulas and journal-derived results are unchanged.
- Reports/MIS: common filter/table/card styling; overview KPI cards are compact. ReportsScreen uses a fixed-header, hoverable, sortable grid with pagination over already loaded rows. MIS retains its existing drill-capable table interactions through AppDataTable.
- Users, administration and login: shared theme/forms/states and more informative login context.

## Accessibility and usability

Compact readable desktop typography, visible keyboard focus, labeled fields, tooltips, semantic status text, appropriate disabled actions, restrained motion and independently scrollable navigation. Layout checks cover 1366x768, 1440x900 and 1920x1080; existing 800x600 tests remain supported. These are widget/layout checks with fixture data, not a claim of a formal accessibility certification or live cashier hardware acceptance test.

## Genuine defects corrected

1. Dashboard map-only parsing dropped typed SaleListItem and PurchaseOrderListItem records.
2. Dashboard feed failures were silently represented as absent/zero data.
3. Replacing modules on navigation destroyed an unfinished POS cart.
4. Sidebar focus survived a return to POS; scanner focus is now restored and inactive modules cannot capture it.

No backend API contract, database schema, authentication/role logic, sales/purchase calculation, FEFO allocation, inventory posting, accounting formula, audit, credit/payment or return/refund rule was changed.

## Limits and deliberate preservation

Actual FEFO batch IDs/details are not exposed by the existing pre-sale search API. POS displays stock and nearest-expiry metadata; actual allocated batches remain in completed receipt snapshots. The presentation uses a 30-day near-expiry indicator; backend configured alerts and sale eligibility remain authoritative. A nearest expired date does not by itself block other valid batches. No fabricated availability or client-side FEFO rule was introduced.

Existing CSV report export is preserved; new Excel/PDF/print backends were not invented. Report pagination/sorting applies to loaded rows, and MIS retains its specialized drill presentation. Existing operational tables retain their original module-specific scrolling, selection, sorting and paging capabilities; fixed headers are provided by AppReportGrid, not retrofitted into every DataTable. Existing focused/high-risk dialogs were retained rather than converting financial workflows to new drawers. A complete live-data visual review of every report variant and hardware scanner test remains a deployment acceptance activity.

## Material files

New shared files under desktop/pharmacy_pos/lib/ui/: app_theme.dart, app_widgets.dart, app_sidebar.dart, app_report_grid.dart. DashboardScreen under lib/features/dashboard/. Major layout changes: lib/features/shell/app_shell.dart, lib/features/sales/pos_screen.dart, lib/features/dashboard/dashboard_screen.dart, lib/features/reports/reports_screen.dart and lib/features/purchasing/purchasing_screen.dart. Common visual adoption spans catalog, inventory/godowns/transfers, customers/suppliers, finance/accounting, pricing/Phase 6, wholesale/quotations/orders, users/admin and authentication. main.dart, pubspec.yaml, bundled Inter assets and tests are also changed. Windows runner naming changes originated in Claude's retained work.

## Validation

Commands run from desktop/pharmacy_pos:

- flutter analyze: No issues found.
- flutter test --reporter json: 164/164 passed (existing tests retained).
- flutter test test/redesign_desktop_test.dart --reporter json: 5/5 passed against the final shared components.
- flutter build windows --release: passed against the final workspace (87.7 seconds).
- flutter pub get: passed; existing dependency constraints retained.
- git diff --check: clean.

No backend-affecting files were changed, so backend tests were not rerun for this frontend redesign. Prior Phase 6 totals are historical verification, not a new backend test run. The full widget run plus final focused desktop run cover the last header/badge refinements. Screenshot checks generate 39 artifacts across the three target resolutions. Final git status: 42 tracked files modified plus new design/font/dashboard/test/documentation files, all uncommitted; no staged content changes. Pre-existing root artifacts remain untracked.

Desktop screenshot artifacts are generated by test/redesign_desktop_test.dart in artifacts/ui-redesign/ using fixture data. Examples: [POS](../artifacts/ui-redesign/1366x768-sales.png), [Dashboard](../artifacts/ui-redesign/1920x1080-dashboard.png), [Inventory](../artifacts/ui-redesign/1440x900-inventory.png), [GRN](../artifacts/ui-redesign/1366x768-grn-dialog.png), [Accounts](../artifacts/ui-redesign/1366x768-accounts.png). These ignored local artifacts can be regenerated by the desktop test.
