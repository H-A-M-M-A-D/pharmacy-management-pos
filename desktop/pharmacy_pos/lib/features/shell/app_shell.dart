import 'package:flutter/material.dart';
import '../../ui/app_theme.dart';
import '../../ui/app_sidebar.dart';
import '../auth/auth_state.dart';
import '../dashboard/dashboard_screen.dart';
import '../profile/profile_screen.dart';
import '../catalog/products_screen.dart';
import '../catalog/catalog_masters_screen.dart';
import '../customers/customers_screen.dart';
import '../finance/finance_screen.dart';
import '../godowns/godowns_screen.dart';
import '../inventory/inventory_screen.dart';
import '../pricing/price_levels_screen.dart';
import '../phase6/phase6_screen.dart';
import '../purchasing/purchasing_screen.dart';
import '../quotations/quotations_screen.dart';
import '../reports/reports_screen.dart';
import '../reports/mis_screen.dart';
import '../sales/cashier_shift_screen.dart';
import '../sales/pos_screen.dart';
import '../sales_orders/sales_orders_screen.dart';
import '../stock_transfers/stock_transfers_screen.dart';
import '../wholesale/wholesale_screen.dart';
import '../suppliers/suppliers_screen.dart';
import '../users/users_screen.dart';
import '../administration/administration_screen.dart';
import '../accounts/accounts_screen.dart';

class AppShell extends StatefulWidget {
  const AppShell({required this.authState, super.key});
  final AuthState authState;
  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  int _selected = 0;
  final Set<int> _visited = {0};
  bool? _expanded;
  @override
  Widget build(BuildContext context) {
    final canViewUsers = widget.authState.can('users.view');
    final canViewProducts = widget.authState.can('products.view');
    final canViewCategories = widget.authState.can('categories.view');
    final canViewManufacturers = widget.authState.can('manufacturers.view');
    final canViewInventory = widget.authState.can('inventory.view');
    final canViewGodowns = widget.authState.can('godowns.view');
    final canViewStockTransfers = widget.authState.can('stock_transfers.view');
    final canViewSuppliers = widget.authState.can('suppliers.view');
    final canViewCustomers = widget.authState.can('customers.view');
    final canViewPurchasing =
        widget.authState.can('purchases.view') ||
        widget.authState.can('purchase_orders.view') ||
        widget.authState.can('purchase_returns.view');
    final canViewSales =
        widget.authState.can('sales.view') ||
        widget.authState.can('sales.create');
    final canViewQuotations = widget.authState.can('quotations.view');
    final canViewSalesOrders = widget.authState.can('sales_orders.view');
    final canViewWholesale = widget.authState.can('sales.wholesale');
    final canViewPricing = widget.authState.can('pricing.view');
    final canViewPhase6 = const [
      'pricing.view',
      'inventory.reorder.view',
      'alerts.view',
      'automation.view',
    ].any(widget.authState.can);
    final canViewCashierShift = const [
      'cashier_shift.open',
      'cashier_shift.view',
    ].any(widget.authState.can);
    final canViewFinance =
        widget.authState.can('accounts.view') ||
        widget.authState.can('expenses.view') ||
        widget.authState.can('finance.ledger.view');
    final canViewReports = const [
      'reports.view',
      'reports.sales',
      'reports.purchases',
      'reports.inventory',
      'reports.financial',
      'reports.profitability',
    ].any(widget.authState.can);
    final canViewAdministration = const [
      'audit.view',
      'recycle_bin.view',
      'branches.view',
      'system.view',
      'system.backup',
    ].any(widget.authState.can);
    final canViewAccounting = const [
      'accounts.coa.view',
      'accounts.journal.view',
      'accounts.periods.view',
      'accounts.reconciliation.view',
      'accounts.recurring.view',
      'accounts.budgets.view',
      'accounts.credit_notes.view',
      'accounts.debit_notes.view',
      'accounts.writeoffs.view',
      'accounts.advances.view',
      'customers.view',
      'suppliers.view',
    ].any(widget.authState.can);
    final canViewMis = widget.authState.can('reports.view');
    // Destinations are grouped conceptually (Main / Sales / Inventory /
    // Purchasing / Finance & Management) to make a long, permission-gated
    // navigation list scannable. Each group only renders if at least one of
    // its destinations is visible to the current user, and each item's
    // destination/page are paired together so reordering can never drift
    // the two lists out of sync.
    final groups = <_NavGroup>[
      _NavGroup('Main', [
        _NavItem(
          const NavigationRailDestination(
            icon: Icon(Icons.dashboard_outlined),
            selectedIcon: Icon(Icons.dashboard),
            label: Text('Dashboard'),
          ),
          DashboardScreen(authState: widget.authState),
        ),
      ]),
      _NavGroup('Sales', [
        if (canViewSales)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.point_of_sale_outlined),
              selectedIcon: Icon(Icons.point_of_sale),
              label: Text('Sales'),
            ),
            PosScreen(authState: widget.authState),
          ),
        if (canViewWholesale)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.storefront_outlined),
              selectedIcon: Icon(Icons.storefront),
              label: Text('Wholesale'),
            ),
            WholesaleScreen(authState: widget.authState),
          ),
        if (canViewQuotations)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.request_quote_outlined),
              selectedIcon: Icon(Icons.request_quote),
              label: Text('Quotations'),
            ),
            QuotationsScreen(authState: widget.authState),
          ),
        if (canViewSalesOrders)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.assignment_outlined),
              selectedIcon: Icon(Icons.assignment),
              label: Text('Sales Orders'),
            ),
            SalesOrdersScreen(authState: widget.authState),
          ),
        if (canViewCashierShift)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.savings_outlined),
              selectedIcon: Icon(Icons.savings),
              label: Text('Cashier Shift'),
            ),
            CashierShiftScreen(authState: widget.authState),
          ),
        if (canViewCustomers)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.people_alt_outlined),
              selectedIcon: Icon(Icons.people_alt),
              label: Text('Customers'),
            ),
            CustomersScreen(authState: widget.authState),
          ),
      ]),
      _NavGroup('Inventory', [
        if (canViewProducts)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.medication_outlined),
              selectedIcon: Icon(Icons.medication),
              label: Text('Products'),
            ),
            ProductsScreen(authState: widget.authState),
          ),
        if (canViewCategories)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.category_outlined),
              selectedIcon: Icon(Icons.category),
              label: Text('Categories'),
            ),
            CatalogMastersScreen(
              authState: widget.authState,
              mode: CatalogMasterMode.categories,
            ),
          ),
        if (canViewManufacturers)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.factory_outlined),
              selectedIcon: Icon(Icons.factory),
              label: Text('Manufacturers'),
            ),
            CatalogMastersScreen(
              authState: widget.authState,
              mode: CatalogMasterMode.manufacturers,
            ),
          ),
        if (canViewInventory)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.inventory_2_outlined),
              selectedIcon: Icon(Icons.inventory_2),
              label: Text('Inventory'),
            ),
            InventoryScreen(authState: widget.authState),
          ),
        if (canViewGodowns)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.warehouse_outlined),
              selectedIcon: Icon(Icons.warehouse),
              label: Text('Godowns'),
            ),
            GodownsScreen(authState: widget.authState),
          ),
        if (canViewStockTransfers)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.compare_arrows_outlined),
              selectedIcon: Icon(Icons.compare_arrows),
              label: Text('Transfers'),
            ),
            StockTransfersScreen(authState: widget.authState),
          ),
      ]),
      _NavGroup('Purchasing', [
        if (canViewPurchasing)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.shopping_cart_outlined),
              selectedIcon: Icon(Icons.shopping_cart),
              label: Text('Purchasing'),
            ),
            PurchasingScreen(authState: widget.authState),
          ),
        if (canViewSuppliers)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.local_shipping_outlined),
              selectedIcon: Icon(Icons.local_shipping),
              label: Text('Suppliers'),
            ),
            SuppliersScreen(authState: widget.authState),
          ),
      ]),
      _NavGroup('Finance / Management', [
        if (canViewFinance)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.account_balance_wallet_outlined),
              selectedIcon: Icon(Icons.account_balance_wallet),
              label: Text('Finance'),
            ),
            FinanceScreen(authState: widget.authState),
          ),
        if (canViewAccounting)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.account_balance_outlined),
              selectedIcon: Icon(Icons.account_balance),
              label: Text('Accounts'),
            ),
            AccountsScreen(authState: widget.authState),
          ),
        if (canViewReports)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.analytics_outlined),
              selectedIcon: Icon(Icons.analytics),
              label: Text('Reports'),
            ),
            ReportsScreen(authState: widget.authState),
          ),
        if (canViewMis)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.insights_outlined),
              selectedIcon: Icon(Icons.insights),
              label: Text('Management / MIS'),
            ),
            MisScreen(authState: widget.authState),
          ),
        if (canViewPricing)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.sell_outlined),
              selectedIcon: Icon(Icons.sell),
              label: Text('Price Levels'),
            ),
            PriceLevelsScreen(authState: widget.authState),
          ),
        if (canViewPhase6)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.auto_awesome_outlined),
              selectedIcon: Icon(Icons.auto_awesome),
              label: Text('Business Automation'),
            ),
            Phase6Screen(authState: widget.authState),
          ),
        if (canViewUsers)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.manage_accounts_outlined),
              selectedIcon: Icon(Icons.manage_accounts),
              label: Text('Users'),
            ),
            UsersScreen(authState: widget.authState),
          ),
        if (canViewAdministration)
          _NavItem(
            const NavigationRailDestination(
              icon: Icon(Icons.admin_panel_settings_outlined),
              selectedIcon: Icon(Icons.admin_panel_settings),
              label: Text('Administration'),
            ),
            AdministrationScreen(authState: widget.authState),
          ),
      ]),
    ];
    final destinations = <NavigationRailDestination>[];
    final pages = <Widget>[];
    for (final group in groups) {
      if (group.items.isEmpty) continue;
      if (destinations.isNotEmpty) {
        destinations.add(
          NavigationRailDestination(
            disabled: true,
            icon: const SizedBox(height: 4),
            // A fixed width keeps a long group name (e.g. "Finance /
            // Management") from ever becoming the widest destination in the
            // rail - NavigationRail sizes itself to its widest destination,
            // and letting a header grow that would shift every screen to
            // its right by a few pixels at narrow window widths.
            label: SizedBox(
              width: 96,
              child: Text(
                group.label.toUpperCase(),
                textAlign: TextAlign.center,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontSize: 10,
                  fontWeight: FontWeight.w700,
                  letterSpacing: 0.4,
                ),
              ),
            ),
          ),
        );
        pages.add(const SizedBox.shrink());
      }
      for (final item in group.items) {
        destinations.add(item.destination);
        pages.add(item.page);
      }
    }
    destinations.add(
      const NavigationRailDestination(
        icon: Icon(Icons.account_circle_outlined),
        selectedIcon: Icon(Icons.account_circle),
        label: Text('My profile'),
      ),
    );
    pages.add(ProfileScreen(authState: widget.authState));
    if (_selected >= pages.length) _selected = 0;
    return LayoutBuilder(
      builder: (context, constraints) {
        final expanded = _expanded ?? constraints.maxWidth >= 1150;
        final user = widget.authState.currentUser!;
        return Scaffold(
          body: Row(
            children: [
              AppSidebar(
                destinations: destinations,
                selectedIndex: _selected,
                expanded: expanded,
                onSelected: (value) => setState(() {
                  _selected = value;
                  _visited.add(value);
                }),
                onToggle: () => setState(() => _expanded = !expanded),
                onLogout: widget.authState.logout,
              ),
              Expanded(
                child: Column(
                  children: [
                    Container(
                      height: 56,
                      padding: const EdgeInsets.symmetric(horizontal: 20),
                      decoration: const BoxDecoration(
                        color: Colors.white,
                        border: Border(
                          bottom: BorderSide(color: AppColors.border),
                        ),
                      ),
                      child: Row(
                        children: [
                          const Icon(
                            Icons.location_on_outlined,
                            size: 18,
                            color: AppColors.primary,
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              'Branch · ${user.branch.name}',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: Theme.of(context).textTheme.labelLarge,
                            ),
                          ),
                          const SizedBox(width: 12),
                          const Icon(Icons.account_circle_outlined, size: 20),
                          const SizedBox(width: 8),
                          Flexible(
                            child: Text(
                              user.fullName,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: Theme.of(context).textTheme.bodySmall,
                            ),
                          ),
                        ],
                      ),
                    ),
                    Expanded(
                      child: IndexedStack(
                        index: _selected,
                        children: [
                          for (var i = 0; i < pages.length; i++)
                            _visited.contains(i)
                                ? TickerMode(
                                    enabled: i == _selected,
                                    child: FocusScope(
                                      canRequestFocus: i == _selected,
                                      child: pages[i],
                                    ),
                                  )
                                : const SizedBox.shrink(),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

/// Pairs a nav destination with the page it opens so reordering destinations
/// into groups can never desynchronize the two lists by index.
class _NavItem {
  const _NavItem(this.destination, this.page);
  final NavigationRailDestination destination;
  final Widget page;
}

class _NavGroup {
  const _NavGroup(this.label, this.items);
  final String label;
  final List<_NavItem> items;
}
