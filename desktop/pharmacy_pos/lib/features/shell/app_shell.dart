import 'package:flutter/material.dart';

import '../auth/auth_state.dart';
import '../profile/profile_screen.dart';
import '../catalog/products_screen.dart';
import '../catalog/catalog_masters_screen.dart';
import '../users/users_screen.dart';

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
