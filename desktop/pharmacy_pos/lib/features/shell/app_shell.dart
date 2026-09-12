import 'package:flutter/material.dart';

import '../auth/auth_state.dart';
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
    final destinations = <NavigationRailDestination>[
      const NavigationRailDestination(
        icon: Icon(Icons.dashboard_outlined),
        selectedIcon: Icon(Icons.dashboard),
        label: Text('Dashboard'),
      ),
      if (canViewUsers)
        const NavigationRailDestination(
          icon: Icon(Icons.manage_accounts_outlined),
          selectedIcon: Icon(Icons.manage_accounts),
          label: Text('Users'),
        ),
      if (canViewProducts)
        const NavigationRailDestination(
          icon: Icon(Icons.medication_outlined),
          selectedIcon: Icon(Icons.medication),
          label: Text('Products'),
        ),
      if (canViewCategories)
        const NavigationRailDestination(
          icon: Icon(Icons.category_outlined),
          selectedIcon: Icon(Icons.category),
          label: Text('Categories'),
        ),
      if (canViewManufacturers)
        const NavigationRailDestination(
          icon: Icon(Icons.factory_outlined),
          selectedIcon: Icon(Icons.factory),
          label: Text('Manufacturers'),
        ),
      if (canViewInventory)
        const NavigationRailDestination(
          icon: Icon(Icons.inventory_2_outlined),
          selectedIcon: Icon(Icons.inventory_2),
          label: Text('Inventory'),
        ),
      if (canViewGodowns)
        const NavigationRailDestination(
          icon: Icon(Icons.warehouse_outlined),
          selectedIcon: Icon(Icons.warehouse),
          label: Text('Godowns'),
        ),
      if (canViewStockTransfers)
        const NavigationRailDestination(
          icon: Icon(Icons.compare_arrows_outlined),
          selectedIcon: Icon(Icons.compare_arrows),
          label: Text('Transfers'),
        ),
      if (canViewSuppliers)
        const NavigationRailDestination(
          icon: Icon(Icons.local_shipping_outlined),
          selectedIcon: Icon(Icons.local_shipping),
          label: Text('Suppliers'),
        ),
      if (canViewCustomers)
        const NavigationRailDestination(
          icon: Icon(Icons.people_alt_outlined),
          selectedIcon: Icon(Icons.people_alt),
          label: Text('Customers'),
        ),
      if (canViewPurchasing)
        const NavigationRailDestination(
          icon: Icon(Icons.shopping_cart_outlined),
          selectedIcon: Icon(Icons.shopping_cart),
          label: Text('Purchasing'),
        ),
      if (canViewSales)
        const NavigationRailDestination(
          icon: Icon(Icons.point_of_sale_outlined),
          selectedIcon: Icon(Icons.point_of_sale),
          label: Text('Sales'),
        ),
      if (canViewQuotations)
        const NavigationRailDestination(
          icon: Icon(Icons.request_quote_outlined),
          selectedIcon: Icon(Icons.request_quote),
          label: Text('Quotations'),
        ),
      if (canViewSalesOrders)
        const NavigationRailDestination(
          icon: Icon(Icons.assignment_outlined),
          selectedIcon: Icon(Icons.assignment),
          label: Text('Sales Orders'),
        ),
      if (canViewWholesale)
        const NavigationRailDestination(
          icon: Icon(Icons.storefront_outlined),
          selectedIcon: Icon(Icons.storefront),
          label: Text('Wholesale'),
        ),
      if (canViewPricing)
        const NavigationRailDestination(
          icon: Icon(Icons.sell_outlined),
          selectedIcon: Icon(Icons.sell),
          label: Text('Price Levels'),
        ),
      if (canViewPhase6)
        const NavigationRailDestination(
          icon: Icon(Icons.auto_awesome_outlined),
          selectedIcon: Icon(Icons.auto_awesome),
          label: Text('Business Automation'),
        ),
      if (canViewCashierShift)
        const NavigationRailDestination(
          icon: Icon(Icons.savings_outlined),
          selectedIcon: Icon(Icons.savings),
          label: Text('Cashier Shift'),
        ),
      if (canViewFinance)
        const NavigationRailDestination(
          icon: Icon(Icons.account_balance_wallet_outlined),
          selectedIcon: Icon(Icons.account_balance_wallet),
          label: Text('Finance'),
        ),
      if (canViewReports)
        const NavigationRailDestination(
          icon: Icon(Icons.analytics_outlined),
          selectedIcon: Icon(Icons.analytics),
          label: Text('Reports'),
        ),
      if (widget.authState.can('reports.view'))
        const NavigationRailDestination(
          icon: Icon(Icons.insights_outlined),
          selectedIcon: Icon(Icons.insights),
          label: Text('Management / MIS'),
        ),
      if (canViewAccounting)
        const NavigationRailDestination(
          icon: Icon(Icons.account_balance_outlined),
          selectedIcon: Icon(Icons.account_balance),
          label: Text('Accounts'),
        ),
      if (canViewAdministration)
        const NavigationRailDestination(
          icon: Icon(Icons.admin_panel_settings_outlined),
          selectedIcon: Icon(Icons.admin_panel_settings),
          label: Text('Administration'),
        ),
      const NavigationRailDestination(
        icon: Icon(Icons.account_circle_outlined),
        selectedIcon: Icon(Icons.account_circle),
        label: Text('My profile'),
      ),
    ];
    final pages = <Widget>[
      _Dashboard(authState: widget.authState),
      if (canViewUsers) UsersScreen(authState: widget.authState),
      if (canViewProducts) ProductsScreen(authState: widget.authState),
      if (canViewCategories)
        CatalogMastersScreen(
          authState: widget.authState,
          mode: CatalogMasterMode.categories,
        ),
      if (canViewManufacturers)
        CatalogMastersScreen(
          authState: widget.authState,
          mode: CatalogMasterMode.manufacturers,
        ),
      if (canViewInventory) InventoryScreen(authState: widget.authState),
      if (canViewGodowns) GodownsScreen(authState: widget.authState),
      if (canViewStockTransfers)
        StockTransfersScreen(authState: widget.authState),
      if (canViewSuppliers) SuppliersScreen(authState: widget.authState),
      if (canViewCustomers) CustomersScreen(authState: widget.authState),
      if (canViewPurchasing) PurchasingScreen(authState: widget.authState),
      if (canViewSales) PosScreen(authState: widget.authState),
      if (canViewQuotations) QuotationsScreen(authState: widget.authState),
      if (canViewSalesOrders) SalesOrdersScreen(authState: widget.authState),
      if (canViewWholesale) WholesaleScreen(authState: widget.authState),
      if (canViewPricing) PriceLevelsScreen(authState: widget.authState),
      if (canViewPhase6) Phase6Screen(authState: widget.authState),
      if (canViewCashierShift) CashierShiftScreen(authState: widget.authState),
      if (canViewFinance) FinanceScreen(authState: widget.authState),
      if (canViewReports) ReportsScreen(authState: widget.authState),
      if (widget.authState.can('reports.view'))
        MisScreen(authState: widget.authState),
      if (canViewAccounting) AccountsScreen(authState: widget.authState),
      if (canViewAdministration)
        AdministrationScreen(authState: widget.authState),
      ProfileScreen(authState: widget.authState),
    ];
    if (_selected >= pages.length) _selected = 0;

    return Scaffold(
      body: Row(
        children: [
          NavigationRail(
            minWidth: 76,
            labelType: NavigationRailLabelType.all,
            selectedIndex: _selected,
            onDestinationSelected: (value) => setState(() => _selected = value),
            leading: Padding(
              padding: const EdgeInsets.symmetric(vertical: 18),
              child: Icon(
                Icons.local_pharmacy,
                color: Theme.of(context).colorScheme.primary,
                size: 32,
              ),
            ),
            trailing: Expanded(
              child: Align(
                alignment: Alignment.bottomCenter,
                child: Padding(
                  padding: const EdgeInsets.only(bottom: 14),
                  child: IconButton(
                    tooltip: 'Sign out',
                    onPressed: widget.authState.logout,
                    icon: const Icon(Icons.logout),
                  ),
                ),
              ),
            ),
            destinations: destinations,
          ),
          const VerticalDivider(width: 1),
          Expanded(child: pages[_selected]),
        ],
      ),
    );
  }
}

class _Dashboard extends StatelessWidget {
  const _Dashboard({required this.authState});

  final AuthState authState;

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Padding(
      padding: const EdgeInsets.all(28),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Dashboard', style: Theme.of(context).textTheme.headlineMedium),
          const SizedBox(height: 6),
          Text(
            '${authState.currentUser!.branch.name} · ${authState.currentUser!.roles.map((role) => role.name).join(', ')}',
          ),
          const SizedBox(height: 36),
          Text(
            'Welcome, ${authState.currentUser!.fullName}',
            style: Theme.of(context).textTheme.titleLarge,
          ),
        ],
      ),
    ),
  );
}
