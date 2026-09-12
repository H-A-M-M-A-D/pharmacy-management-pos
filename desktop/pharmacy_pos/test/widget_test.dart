import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pharmacy_pos/core/api_client.dart';
import 'package:pharmacy_pos/core/models.dart';
import 'package:pharmacy_pos/core/token_store.dart';
import 'package:pharmacy_pos/features/auth/auth_state.dart';
import 'package:pharmacy_pos/main.dart';

void main() {
  testWidgets('login validates required fields', (tester) async {
    final fixture = TestFixture();
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('login_submit')));
    await tester.pump();

    expect(find.text('Username is required'), findsOneWidget);
    expect(find.text('Password is required'), findsOneWidget);
  });

  testWidgets('successful login navigates to dashboard', (tester) async {
    final fixture = TestFixture();
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);

    expect(find.text('Dashboard'), findsWidgets);
    expect(find.text('Welcome, Test User'), findsOneWidget);
  });

  for (final size in [
    const Size(800, 600),
    const Size(1024, 768),
    const Size(1366, 768),
    const Size(1920, 1080),
  ]) {
    testWidgets('login and dashboard shell support desktop size $size', (
      tester,
    ) async {
      await tester.binding.setSurfaceSize(size);
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final fixture = TestFixture();
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      expect(find.text('Dashboard'), findsWidgets);
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets('login displays safe error', (tester) async {
    final fixture = TestFixture(loginError: true);
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);

    expect(find.text('Invalid username or password.'), findsOneWidget);
  });

  testWidgets('first login is restricted to password change', (tester) async {
    final fixture = TestFixture(mustChangePassword: true);
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);

    expect(find.text('Set a permanent password'), findsOneWidget);
    expect(find.text('Dashboard'), findsNothing);
  });

  testWidgets('user navigation follows users.view permission', (tester) async {
    final denied = TestFixture();
    await tester.pumpWidget(denied.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Users'), findsNothing);

    final allowed = TestFixture(permissions: {'users.view'});
    await tester.pumpWidget(allowed.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Users'), findsOneWidget);
  });

  testWidgets('administration navigation follows system permissions', (
    tester,
  ) async {
    final denied = TestFixture();
    await tester.pumpWidget(denied.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Administration'), findsNothing);

    final allowed = TestFixture(permissions: {'system.view'});
    await tester.pumpWidget(allowed.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Administration'), findsOneWidget);
    await tester.tap(find.text('Administration'));
    await tester.pumpAndSettle();
    expect(find.text('System Info'), findsOneWidget);
  });

  testWidgets(
    'sidebar scrolls to reach lower navigation items at 800x600 without overflow',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(800, 600));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final fixture = TestFixture(
        permissions: {
          'users.view',
          'products.view',
          'categories.view',
          'manufacturers.view',
          'inventory.view',
          'godowns.view',
          'stock_transfers.view',
          'suppliers.view',
          'customers.view',
          'purchases.view',
          'sales.view',
          'quotations.view',
          'sales_orders.view',
          'sales.wholesale',
          'pricing.view',
          'cashier_shift.view',
          'accounts.view',
          'reports.view',
          'system.view',
          'accounts.journal.view',
        },
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);

      // With every permission-gated section enabled, the destination list is
      // far taller than the 800x600 window - this must not overflow.
      expect(tester.takeException(), isNull);

      // Scroll the rail down (mouse-wheel drag) and confirm a destination
      // near the bottom of the list is reachable and tappable.
      await tester.drag(find.byType(NavigationRail), const Offset(0, -3000));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Administration'));
      await tester.pumpAndSettle();
      expect(find.text('System Info'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets('user list renders API results', (tester) async {
    final fixture = TestFixture(permissions: {'users.view'});
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);

    await tester.tap(find.text('Users'));
    await tester.pumpAndSettle();
    expect(find.text('Second User'), findsOneWidget);
    expect(find.text('cashier'), findsOneWidget);
  });

  testWidgets('add user form validates required values', (tester) async {
    final fixture = TestFixture(permissions: {'users.view', 'users.create'});
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Users'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('add_user')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('save_user')));
    await tester.pump();

    expect(find.text('Full name is required'), findsOneWidget);
    expect(
      find.text('Use 3+ letters, numbers, dots, underscores, or hyphens'),
      findsOneWidget,
    );
  });

  testWidgets('products navigation follows products.view permission', (
    tester,
  ) async {
    final fixture = TestFixture(permissions: {'products.view'});
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Products'), findsOneWidget);
  });

  testWidgets('product list renders catalog data', (tester) async {
    final fixture = TestFixture(permissions: {'products.view'});
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Products'));
    await tester.pumpAndSettle();
    expect(find.text('Panadol Extra'), findsOneWidget);
    expect(find.text('PKR 25.00'), findsOneWidget);
  });

  testWidgets('add product validates required fields', (tester) async {
    final fixture = TestFixture(
      permissions: {'products.view', 'products.create'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Products'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('add_product')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('product_sku')), '');
    await tester.tap(find.byKey(const Key('save_product')));
    await tester.pump();
    expect(find.text('Required'), findsWidgets);
  });

  testWidgets('category and manufacturer navigation is permission aware', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'categories.view', 'manufacturers.view'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Categories'), findsOneWidget);
    expect(find.text('Manufacturers'), findsOneWidget);
  });

  testWidgets('category form validates name', (tester) async {
    final fixture = TestFixture(
      permissions: {'categories.view', 'categories.manage'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Categories'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('add_category')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('save_category')));
    await tester.pump();
    expect(find.text('Name is required'), findsOneWidget);
  });

  testWidgets('manufacturer form validates name', (tester) async {
    final fixture = TestFixture(
      permissions: {'manufacturers.view', 'manufacturers.manage'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Manufacturers'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('add_manufacturer')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('save_manufacturer')));
    await tester.pump();
    expect(find.text('Name is required'), findsOneWidget);
  });

  testWidgets('inventory navigation follows inventory.view permission', (
    tester,
  ) async {
    final denied = TestFixture();
    await tester.pumpWidget(denied.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Inventory'), findsNothing);

    final allowed = TestFixture(permissions: {'inventory.view'});
    await tester.pumpWidget(allowed.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Inventory'), findsOneWidget);
  });

  testWidgets('inventory stock list renders status labels', (tester) async {
    final fixture = TestFixture(permissions: {'inventory.view'});
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Inventory'));
    await tester.pumpAndSettle();
    expect(find.text('Panadol Extra'), findsOneWidget);
    expect(find.text('Low Stock'), findsOneWidget);
    expect(find.text('PKR 80.00'), findsOneWidget);
  });

  testWidgets('opening stock form validates required fields', (tester) async {
    final fixture = TestFixture(
      permissions: {'inventory.view', 'inventory.opening_stock'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Inventory'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('opening_stock')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('save_opening_stock')));
    await tester.pump();
    expect(find.text('Branch is required'), findsOneWidget);
    expect(find.text('Product is required'), findsOneWidget);
    expect(find.text('Enter a positive quantity'), findsOneWidget);
  });

  testWidgets('adjustment dialog displays insufficient stock error', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'inventory.view', 'inventory.adjust'},
      adjustmentError: true,
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Inventory'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('adjust_stock')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('picker_branch')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Head Office').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('picker_product')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Panadol Extra (MED-001)').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('picker_batch')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('B-001 (qty 10) — Main Store').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('adjust_direction_decrease')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('adjust_quantity')), '99');
    await tester.enterText(find.byKey(const Key('adjust_notes')), 'count');
    await tester.tap(find.byKey(const Key('save_adjustment')));
    await tester.pumpAndSettle();
    expect(find.text('Insufficient stock in selected batch.'), findsOneWidget);
  });

  testWidgets(
    'adjustment dialog requires branch product batch quantity and reason',
    (tester) async {
      final fixture = TestFixture(
        permissions: {'inventory.view', 'inventory.adjust'},
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Inventory'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('adjust_stock')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('save_adjustment')));
      await tester.pump();
      expect(find.text('Branch is required'), findsOneWidget);
      expect(find.text('Product is required'), findsOneWidget);
      expect(find.text('Batch is required'), findsOneWidget);
      expect(find.text('Enter a positive quantity'), findsOneWidget);
    },
  );

  testWidgets('adjustment dialog requires notes when reason is other', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'inventory.view', 'inventory.adjust'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Inventory'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('adjust_stock')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('picker_branch')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Head Office').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('picker_product')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Panadol Extra (MED-001)').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('picker_batch')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('B-001 (qty 10) — Main Store').last);
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('adjust_quantity')), '2');
    await tester.tap(find.byKey(const Key('adjust_reason')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Other').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('save_adjustment')));
    await tester.pump();
    expect(
      find.text('Notes are required when reason is Other'),
      findsOneWidget,
    );
  });

  testWidgets('stock movements are read only', (tester) async {
    final fixture = TestFixture(
      permissions: {'inventory.view', 'inventory.movements.view'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Inventory'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Movements'));
    await tester.pumpAndSettle();
    expect(find.text('OpeningStock'), findsOneWidget);
    expect(find.text('Edit'), findsNothing);
    expect(find.text('Delete'), findsNothing);
  });

  testWidgets('stock taking session can be started counted and finalized', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {
        'inventory.view',
        'inventory.stock_count',
        'inventory.stock_count.view',
        'inventory.stock_count.finalize',
      },
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Inventory'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Stock Taking'));
    await tester.pumpAndSettle();
    expect(find.text('SC-2026-000001'), findsOneWidget);

    await tester.ensureVisible(
      find.byKey(const Key('open_stock_count_session-1')),
    );
    await tester.tap(find.byKey(const Key('open_stock_count_session-1')));
    await tester.pumpAndSettle();
    expect(find.text('Start Counting'), findsOneWidget);

    await tester.tap(find.byKey(const Key('start_stock_count')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('count_entry_line-1')), findsOneWidget);

    await tester.enterText(find.byKey(const Key('count_entry_line-1')), '12');
    await tester.tap(find.byKey(const Key('save_stock_count_entries')));
    await tester.pumpAndSettle();
    expect(find.text('2'), findsWidgets);

    await tester.tap(find.byKey(const Key('finalize_stock_count')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_finalize_stock_count')));
    await tester.pumpAndSettle();

    expect(find.widgetWithText(Chip, 'Completed'), findsOneWidget);
  });

  testWidgets('cashier shift can be opened, adjusted, closed and reconciled', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {
        'cashier_shift.open',
        'cashier_shift.view',
        'cashier_shift.close',
        'cashier_shift.reconcile',
        'cashier_shift.drawer_adjust',
      },
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Cashier Shift'));
    await tester.pumpAndSettle();
    expect(find.text('You do not have an open cashier shift.'), findsOneWidget);

    await tester.tap(find.byKey(const Key('open_cashier_shift')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('open_shift_cash')), '500');
    await tester.tap(find.byKey(const Key('save_open_shift')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('close_cashier_shift')), findsOneWidget);

    await tester.ensureVisible(find.byKey(const Key('cashier_shift_cash_in')));
    await tester.tap(find.byKey(const Key('cashier_shift_cash_in')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('drawer_entry_amount')), '50');
    await tester.enterText(
      find.byKey(const Key('drawer_entry_reason')),
      'float top-up',
    );
    await tester.tap(find.byKey(const Key('save_drawer_entry')));
    await tester.pumpAndSettle();
    expect(find.textContaining('float top-up'), findsOneWidget);

    await tester.ensureVisible(find.byKey(const Key('close_cashier_shift')));
    await tester.tap(find.byKey(const Key('close_cashier_shift')));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('close_shift_actual_cash')),
      '100',
    );
    await tester.tap(find.byKey(const Key('save_close_shift')));
    await tester.pumpAndSettle();

    // A closed shift is no longer "my open shift" (matches the real backend's
    // GetOpenShiftForCashierAsync, which only ever returns open shifts), so
    // the My Shift tab correctly drops back to its empty state here.
    // Reconciliation happens from Shift History > View, per the production flow.
    expect(find.text('You do not have an open cashier shift.'), findsOneWidget);

    await tester.tap(find.text('Shift History'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(
      find.byKey(const Key('open_cashier_shift_shift-1')),
    );
    await tester.tap(find.byKey(const Key('open_cashier_shift_shift-1')));
    await tester.pumpAndSettle();

    await tester.ensureVisible(
      find.byKey(const Key('reconcile_cashier_shift')),
    );
    await tester.tap(find.byKey(const Key('reconcile_cashier_shift')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_reconcile_shift')));
    await tester.pumpAndSettle();
    expect(find.widgetWithText(Chip, 'Reconciled'), findsOneWidget);
  });

  testWidgets('godowns navigation follows godowns.view permission', (
    tester,
  ) async {
    final denied = TestFixture();
    await tester.pumpWidget(denied.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Godowns'), findsNothing);

    final allowed = TestFixture(permissions: {'godowns.view'});
    await tester.pumpWidget(allowed.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Godowns'), findsOneWidget);
  });

  testWidgets('godown list renders branch, status and default badge', (
    tester,
  ) async {
    final fixture = TestFixture(permissions: {'godowns.view'}, godownCount: 2);
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Godowns'));
    await tester.pumpAndSettle();

    expect(find.text('Head Office'), findsWidgets);
    expect(find.text('Main Store'), findsOneWidget);
    expect(find.text('Annex Store'), findsOneWidget);
    expect(find.text('Active'), findsNWidgets(2));
    expect(find.text('Default'), findsOneWidget);
  });

  testWidgets('add godown validates required fields and creates a godown', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'godowns.view', 'godowns.create'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Godowns'));
    await tester.pumpAndSettle();

    // Pre-select the branch filter so the Add Godown form's branch field
    // (disabled for editing but required on create) is satisfied by
    // inheriting `defaultBranchId` without needing to drive that dropdown too.
    await tester.tap(
      find.widgetWithText(DropdownButtonFormField<String?>, 'Branch'),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Head Office').last);
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('add_godown')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('godown_code')), findsOneWidget);

    await tester.tap(find.byKey(const Key('save_godown')));
    await tester.pump();
    expect(find.text('Required'), findsWidgets);

    await tester.enterText(find.byKey(const Key('godown_code')), 'COLD');
    await tester.enterText(
      find.byKey(const Key('godown_name')),
      'Cold Storage',
    );
    await tester.tap(find.byKey(const Key('save_godown')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('godown_code')), findsNothing);
    expect(find.text('Cold Storage'), findsOneWidget);
  });

  testWidgets('edit godown updates name and description', (tester) async {
    final fixture = TestFixture(
      permissions: {'godowns.view', 'godowns.update'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Godowns'));
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.byTooltip('Edit'));
    await tester.tap(find.byTooltip('Edit'));
    await tester.pumpAndSettle();
    expect(find.text('Edit Godown'), findsOneWidget);

    await tester.enterText(
      find.byKey(const Key('godown_name')),
      'Main Store Renamed',
    );
    await tester.tap(find.byKey(const Key('save_godown')));
    await tester.pumpAndSettle();

    expect(find.text('Main Store Renamed'), findsOneWidget);
  });

  testWidgets('deactivate and reactivate a godown via confirm dialog', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'godowns.view', 'godowns.deactivate', 'godowns.activate'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Godowns'));
    await tester.pumpAndSettle();

    expect(find.text('Active'), findsOneWidget);

    await tester.ensureVisible(find.byTooltip('Deactivate'));
    await tester.tap(find.byTooltip('Deactivate'));
    await tester.pumpAndSettle();
    expect(find.text('Deactivate godown'), findsOneWidget);
    await tester.tap(find.byKey(const Key('confirm_godown_status')));
    await tester.pumpAndSettle();
    expect(find.text('Inactive'), findsOneWidget);

    await tester.ensureVisible(find.byTooltip('Activate'));
    await tester.tap(find.byTooltip('Activate'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_godown_status')));
    await tester.pumpAndSettle();
    expect(find.text('Active'), findsOneWidget);
  });

  testWidgets('set default godown moves the default to the selected godown', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'godowns.view', 'godowns.set_default'},
      godownCount: 2,
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Godowns'));
    await tester.pumpAndSettle();

    expect(fixture.api.defaultGodownFor('branch-1')?.name, 'Main Store');

    await tester.ensureVisible(find.byTooltip('Set as default'));
    await tester.tap(find.byTooltip('Set as default'));
    await tester.pumpAndSettle();

    expect(fixture.api.defaultGodownFor('branch-1')?.name, 'Annex Store');
  });

  testWidgets('assign and unassign a user to a godown', (tester) async {
    final fixture = TestFixture(
      permissions: {'godowns.view', 'godowns.manage'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Godowns'));
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.byTooltip('Manage user access'));
    await tester.tap(find.byTooltip('Manage user access'));
    await tester.pumpAndSettle();
    expect(
      find.text('No users are assigned to this godown yet.'),
      findsOneWidget,
    );

    await tester.tap(
      find.widgetWithText(DropdownButtonFormField<String>, 'Assign user'),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Second User').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();

    expect(find.text('Second User'), findsOneWidget);
    expect(
      find.text('No users are assigned to this godown yet.'),
      findsNothing,
    );

    await tester.tap(find.byIcon(Icons.remove_circle_outline));
    await tester.pumpAndSettle();

    expect(
      find.text('No users are assigned to this godown yet.'),
      findsOneWidget,
    );
  });

  testWidgets('suppliers navigation follows suppliers.view permission', (
    tester,
  ) async {
    final fixture = TestFixture(permissions: {'suppliers.view'});
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Suppliers'), findsOneWidget);
  });

  testWidgets('supplier list renders balances semantically', (tester) async {
    final fixture = TestFixture(permissions: {'suppliers.view'});
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Suppliers'));
    await tester.pumpAndSettle();
    expect(find.text('ABC Pharma'), findsOneWidget);
    expect(find.text('PKR 10000.00 payable'), findsOneWidget);
  });

  testWidgets('add supplier validates and shows opening balance guidance', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'suppliers.view', 'suppliers.create'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Suppliers'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('add_supplier')));
    await tester.pumpAndSettle();
    expect(
      find.text('Positive = payable. Negative = advance with supplier.'),
      findsOneWidget,
    );
    await tester.tap(find.byKey(const Key('save_supplier')));
    await tester.pump();
    expect(find.text('Required'), findsOneWidget);
  });

  testWidgets('supplier duplicate error is displayed safely', (tester) async {
    final fixture = TestFixture(
      permissions: {'suppliers.view', 'suppliers.create'},
      supplierError: true,
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Suppliers'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('add_supplier')));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('supplier_name')),
      'ABC Pharma',
    );
    await tester.tap(find.byKey(const Key('save_supplier')));
    await tester.pumpAndSettle();
    expect(
      find.text('A supplier with this name already exists.'),
      findsOneWidget,
    );
  });

  testWidgets('supplier ledger payment and adjustment validation', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {
        'suppliers.view',
        'suppliers.ledger.view',
        'suppliers.payment.create',
        'suppliers.adjust_balance',
      },
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Suppliers'));
    await tester.pumpAndSettle();
    await tester.drag(
      find.byType(SingleChildScrollView).last,
      const Offset(-1000, 0),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('View ledger'));
    await tester.pumpAndSettle();
    expect(find.text('OpeningBalance'), findsOneWidget);
    await tester.tap(find.text('Close'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Record payment'));
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Cash Counter'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('save_supplier_payment')));
    await tester.pump();
    expect(find.text('Enter a positive amount'), findsOneWidget);
    expect(find.text('Required'), findsOneWidget);
  });

  testWidgets('purchasing navigation follows purchasing permissions', (
    tester,
  ) async {
    final fixture = TestFixture(permissions: {'purchases.view'});
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Purchasing'), findsOneWidget);
  });

  testWidgets('purchase order form validates required quantity', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {
        'purchases.view',
        'purchase_orders.view',
        'purchase_orders.create',
      },
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Purchasing'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('new_purchase_order')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('save_purchase_order')));
    await tester.pump();
    expect(find.text('Enter a positive number'), findsOneWidget);
  });

  testWidgets('direct purchase shows bonus quantity totals', (tester) async {
    final fixture = TestFixture(
      permissions: {'purchases.view', 'purchases.create', 'purchases.receive'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Purchasing'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('direct_purchase')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('receipt_batch')), 'B-100');
    await tester.enterText(find.byKey(const Key('receipt_paid')), '100');
    await tester.enterText(find.byKey(const Key('receipt_bonus')), '10');
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Purchase Price'),
      '50',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Retail Price'),
      '60',
    );
    await tester.pump();
    expect(
      find.textContaining('Inventory quantity = paid + bonus'),
      findsOneWidget,
    );
    expect(find.textContaining('PKR 5000.00'), findsOneWidget);
  });

  testWidgets(
    'direct purchase with a single godown auto-selects it on the receipt payload',
    (tester) async {
      final fixture = TestFixture(
        permissions: {
          'purchases.view',
          'purchases.create',
          'purchases.receive',
        },
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Purchasing'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('direct_purchase')));
      await tester.pumpAndSettle();

      expect(
        find.widgetWithText(DropdownButtonFormField<String>, 'Godown'),
        findsNothing,
      );

      await tester.enterText(find.byKey(const Key('receipt_batch')), 'B-100');
      await tester.enterText(find.byKey(const Key('receipt_paid')), '10');
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Purchase Price'),
        '50',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Retail Price'),
        '60',
      );
      await tester.tap(find.byKey(const Key('post_purchase')));
      await tester.pumpAndSettle();

      expect(fixture.api.lastDirectPurchaseBody?['godownId'], 'godown-1');
    },
  );

  testWidgets(
    'direct purchase godown selection propagates to the receipt payload',
    (tester) async {
      final fixture = TestFixture(
        permissions: {
          'purchases.view',
          'purchases.create',
          'purchases.receive',
        },
        godownCount: 2,
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Purchasing'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('direct_purchase')));
      await tester.pumpAndSettle();

      final godownDropdown = find.widgetWithText(
        DropdownButtonFormField<String>,
        'Godown',
      );
      expect(godownDropdown, findsOneWidget);
      await tester.tap(godownDropdown);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Annex Store').last);
      await tester.pumpAndSettle();

      await tester.enterText(find.byKey(const Key('receipt_batch')), 'B-200');
      await tester.enterText(find.byKey(const Key('receipt_paid')), '10');
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Purchase Price'),
        '50',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Retail Price'),
        '60',
      );
      await tester.tap(find.byKey(const Key('post_purchase')));
      await tester.pumpAndSettle();

      expect(fixture.api.lastDirectPurchaseBody?['godownId'], 'godown-2');
    },
  );

  testWidgets('receiving against a purchase order posts the selected godown', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {
        'purchases.view',
        'purchase_orders.view',
        'purchases.receive',
      },
      godownCount: 2,
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Purchasing'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.byTooltip('Receive goods'));
    await tester.tap(find.byTooltip('Receive goods'));
    await tester.pumpAndSettle();

    final godownDropdown = find.widgetWithText(
      DropdownButtonFormField<String>,
      'Godown',
    );
    expect(godownDropdown, findsOneWidget);
    await tester.tap(godownDropdown);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Annex Store').last);
    await tester.pumpAndSettle();

    await tester.enterText(find.byKey(const Key('receipt_batch')), 'B-300');
    await tester.enterText(find.byKey(const Key('receipt_paid')), '10');
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Purchase Price'),
      '50',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Retail Price'),
      '60',
    );
    await tester.tap(find.byKey(const Key('post_purchase')));
    await tester.pumpAndSettle();

    expect(fixture.api.lastGoodsReceiptBody?['godownId'], 'godown-2');
  });

  testWidgets('direct purchase duplicate invoice error is safe', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'purchases.view', 'purchases.create', 'purchases.receive'},
      purchaseError: true,
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Purchasing'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('direct_purchase')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('receipt_batch')), 'B-100');
    await tester.enterText(find.byKey(const Key('receipt_paid')), '10');
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Purchase Price'),
      '50',
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Retail Price'),
      '60',
    );
    await tester.tap(find.byKey(const Key('post_purchase')));
    await tester.pumpAndSettle();
    expect(
      find.text('This supplier invoice has already been recorded.'),
      findsOneWidget,
    );
  });

  testWidgets('purchase return action follows purchase return permission', (
    tester,
  ) async {
    final denied = TestFixture(permissions: {'purchases.view'});
    await tester.pumpWidget(denied.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Purchasing'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Purchase History'));
    await tester.pumpAndSettle();
    expect(find.byTooltip('Return to supplier'), findsNothing);

    final allowed = TestFixture(
      permissions: {'purchases.view', 'purchase_returns.create'},
    );
    await tester.pumpWidget(allowed.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Purchasing'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Purchase History'));
    await tester.pumpAndSettle();
    expect(find.byTooltip('Return to supplier'), findsOneWidget);
  });

  testWidgets('purchase return workflow posts paid and bonus quantities', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {
        'purchases.view',
        'purchase_returns.view',
        'purchase_returns.create',
      },
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Purchasing'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Purchase History'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Return to supplier'));
    await tester.pumpAndSettle();
    expect(find.text('Return to supplier'), findsOneWidget);
    expect(find.text('B-100'), findsOneWidget);
    await tester.enterText(
      find.byKey(const Key('return_paid_grn-item-1')),
      '3',
    );
    await tester.enterText(
      find.byKey(const Key('return_bonus_grn-item-1')),
      '2',
    );
    await tester.tap(find.byKey(const Key('post_purchase_return')));
    await tester.pumpAndSettle();

    final postedItems = fixture.api.lastPurchaseReturnBody!['items'] as List;
    expect(postedItems.single['originalGoodsReceiptItemId'], 'grn-item-1');
    expect(postedItems.single['paidReturnQuantity'], 3);
    expect(postedItems.single['bonusReturnQuantity'], 2);
    expect(
      find.byKey(const Key('purchase_return_note_preview')),
      findsOneWidget,
    );
    expect(find.textContaining('PR-2026-000001'), findsWidgets);
  });

  testWidgets('purchase returns tab renders history and note', (tester) async {
    final fixture = TestFixture(
      permissions: {'purchases.view', 'purchase_returns.view'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Purchasing'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Purchase Returns'));
    await tester.pumpAndSettle();
    expect(find.text('PR-2026-000001'), findsOneWidget);
    await tester.tap(find.byTooltip('View return note'));
    await tester.pumpAndSettle();
    expect(
      find.byKey(const Key('purchase_return_note_preview')),
      findsOneWidget,
    );
    expect(find.text('PURCHASE RETURN'), findsOneWidget);
  });

  testWidgets('sales navigation follows sales permissions', (tester) async {
    final denied = TestFixture();
    await tester.pumpWidget(denied.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Sales'), findsNothing);

    final allowed = TestFixture(permissions: {'sales.view', 'sales.create'});
    await tester.pumpWidget(allowed.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Sales'), findsOneWidget);
  });

  testWidgets('pos search adds product and checkout shows receipt', (
    tester,
  ) async {
    final fixture = TestFixture(permissions: {'sales.view', 'sales.create'});
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Sales'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('pos_search')), 'Panadol');
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pumpAndSettle();
    expect(find.text('Total PKR 12.00'), findsOneWidget);
    await tester.tap(find.byKey(const Key('checkout_sale')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_payment')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('receipt_preview')), findsOneWidget);
    expect(find.textContaining('INV-2026-000001'), findsOneWidget);
  });

  for (final size in [
    const Size(800, 600),
    const Size(1024, 768),
    const Size(1366, 768),
    const Size(1920, 1080),
  ]) {
    testWidgets('POS supports desktop size $size', (tester) async {
      await tester.binding.setSurfaceSize(size);
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final fixture = TestFixture(permissions: {'sales.view', 'sales.create'});
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Sales'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('pos_search')), 'Panadol');
      await tester.testTextInput.receiveAction(TextInputAction.done);
      await tester.pumpAndSettle();
      expect(find.text('Total PKR 12.00'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets(
    'pos with a single godown auto-selects it for search, hold and checkout',
    (tester) async {
      final fixture = TestFixture(
        permissions: {'sales.view', 'sales.create', 'sales.hold'},
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Sales'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('pos_godown')), findsNothing);

      await tester.enterText(find.byKey(const Key('pos_search')), 'Panadol');
      await tester.testTextInput.receiveAction(TextInputAction.done);
      await tester.pumpAndSettle();
      expect(fixture.api.lastSearchPosProductsGodownId, 'godown-1');

      await tester.tap(find.byKey(const Key('hold_sale')));
      await tester.pumpAndSettle();
      expect(fixture.api.lastHoldSaleBody?['godownId'], 'godown-1');
    },
  );

  testWidgets(
    'pos godown selection propagates to product search and sale payloads',
    (tester) async {
      final fixture = TestFixture(
        permissions: {'sales.view', 'sales.create'},
        godownCount: 2,
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Sales'));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('pos_godown')), findsOneWidget);

      await tester.tap(find.byKey(const Key('pos_godown')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Annex Store').last);
      await tester.pumpAndSettle();

      await tester.enterText(find.byKey(const Key('pos_search')), 'Panadol');
      await tester.testTextInput.receiveAction(TextInputAction.done);
      await tester.pumpAndSettle();
      expect(fixture.api.lastSearchPosProductsGodownId, 'godown-2');

      await tester.tap(find.byKey(const Key('checkout_sale')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm_payment')));
      await tester.pumpAndSettle();

      expect(fixture.api.lastPostSaleBody?['godownId'], 'godown-2');
    },
  );

  testWidgets('pos shows a warning when the user has no godown access', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'sales.view', 'sales.create'},
      godownCount: 0,
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Sales'));
    await tester.pumpAndSettle();

    expect(
      find.textContaining('No godown is assigned to you for this branch'),
      findsOneWidget,
    );
  });

  testWidgets('sales discount input follows sales.discount permission', (
    tester,
  ) async {
    final denied = TestFixture(permissions: {'sales.view', 'sales.create'});
    await tester.pumpWidget(denied.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Sales'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('pos_search')), 'Panadol');
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('line_discount')), findsNothing);

    final allowed = TestFixture(
      permissions: {'sales.view', 'sales.create', 'sales.discount'},
    );
    await tester.pumpWidget(allowed.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Sales'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('pos_search')), 'Panadol');
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('line_discount')), findsOneWidget);
  });

  testWidgets('sales return workflow posts original allocation refund', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {
        'sales.view',
        'sales.returns.view',
        'sales.returns.create',
        'sales.returns.refund',
      },
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Sales'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sales History'));
    await tester.pumpAndSettle();
    expect(find.byTooltip('Return items'), findsOneWidget);
    await tester.ensureVisible(find.byTooltip('Return items'));
    await tester.tap(find.byTooltip('Return items'));
    await tester.pumpAndSettle();
    expect(find.text('Sales Return / Refund'), findsOneWidget);
    expect(find.text('B-001'), findsOneWidget);
    expect(find.text('Refund Total PKR 12.00'), findsOneWidget);
    await tester.tap(find.byKey(const Key('post_return')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Post'));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('return_receipt_preview')), findsOneWidget);
    expect(find.textContaining('RET-2026-000001'), findsOneWidget);
  });

  testWidgets('sales return history follows returns view permission', (
    tester,
  ) async {
    final denied = TestFixture(permissions: {'sales.view'});
    await tester.pumpWidget(denied.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Sales'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sales Returns'));
    await tester.pumpAndSettle();
    expect(find.text('Not permitted'), findsOneWidget);

    final allowed = TestFixture(
      permissions: {'sales.view', 'sales.returns.view'},
    );
    await tester.pumpWidget(allowed.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Sales'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Sales Returns'));
    await tester.pumpAndSettle();
    expect(find.text('RET-2026-000001'), findsOneWidget);
  });

  testWidgets('finance navigation follows finance permissions', (tester) async {
    final denied = TestFixture();
    await tester.pumpWidget(denied.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Finance'), findsNothing);

    final allowed = TestFixture(permissions: {'accounts.view'});
    await tester.pumpWidget(allowed.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Finance'), findsOneWidget);
  });

  testWidgets('accounts and daily cash position render ledger-backed values', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'accounts.view', 'finance.ledger.view'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Finance'));
    await tester.pumpAndSettle();
    expect(find.text('Cash Counter'), findsOneWidget);
    expect(find.text('Rs. 8000.00'), findsOneWidget);
    await tester.tap(find.text('Cash position'));
    await tester.pumpAndSettle();
    expect(find.text('Opening'), findsOneWidget);
    expect(find.text('Closing'), findsOneWidget);
  });

  testWidgets('add account displays immutable opening balance guidance', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'accounts.view', 'accounts.manage'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Finance'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Account'));
    await tester.pumpAndSettle();
    expect(
      find.text('Recorded once in the ledger and cannot be edited later.'),
      findsOneWidget,
    );
  });

  testWidgets('financial ledger is read only', (tester) async {
    final fixture = TestFixture(
      permissions: {'accounts.view', 'finance.ledger.view'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Finance'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.byTooltip('View ledger'));
    await tester.tap(find.byTooltip('View ledger'));
    await tester.pumpAndSettle();
    expect(find.text('Opening balance'), findsOneWidget);
    expect(find.text('Edit'), findsNothing);
    expect(find.text('Delete'), findsNothing);
  });

  testWidgets('reports navigation follows reports.view permission', (
    tester,
  ) async {
    final denied = TestFixture();
    await tester.pumpWidget(denied.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Reports'), findsNothing);

    final allowed = TestFixture(permissions: {'reports.view'});
    await tester.pumpWidget(allowed.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Reports'), findsOneWidget);
  });

  testWidgets('reports overview renders real API summary cards', (
    tester,
  ) async {
    final fixture = TestFixture(permissions: {'reports.view'});
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Reports'));
    await tester.pumpAndSettle();
    expect(find.text('Reports & Analytics'), findsOneWidget);
    expect(find.text('Net sales'), findsOneWidget);
    expect(find.text('Rs 900.00'), findsOneWidget);
  });

  testWidgets('profitability and export controls are permission aware', (
    tester,
  ) async {
    final fixture = TestFixture(permissions: {'reports.view', 'reports.sales'});
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Reports'));
    await tester.pumpAndSettle();
    expect(find.text('Profitability'), findsNothing);
    expect(find.byTooltip('Export CSV'), findsNothing);
  });

  testWidgets('report date presets apply without automatic query churn', (
    tester,
  ) async {
    final fixture = TestFixture(permissions: {'reports.view'});
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Reports'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Today').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('This Month').last);
    await tester.pumpAndSettle();
    expect(find.text('Apply'), findsOneWidget);
  });

  testWidgets(
    'transfers report section exposes the new stock transfer reports',
    (tester) async {
      final fixture = TestFixture(
        permissions: {'reports.view', 'reports.inventory'},
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Reports'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Overview'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Transfers').last);
      await tester.pumpAndSettle();
      expect(find.text('Summary'), findsOneWidget);

      await tester.tap(find.text('Apply'));
      await tester.pumpAndSettle();
      expect(find.text('No report data for this period.'), findsOneWidget);

      await tester.tap(find.text('Summary'));
      await tester.pumpAndSettle();
      expect(find.text('Daily Transfers'), findsOneWidget);
      expect(find.text('Inter-Godown Detail'), findsOneWidget);
      expect(find.text('Discrepancies'), findsOneWidget);
    },
  );

  testWidgets(
    'inventory report section exposes the new godown-scoped reports',
    (tester) async {
      final fixture = TestFixture(
        permissions: {'reports.view', 'reports.inventory'},
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Reports'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Overview'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Inventory').last);
      await tester.pumpAndSettle();
      expect(find.text('Current Stock'), findsOneWidget);

      await tester.tap(find.text('Current Stock'));
      await tester.pumpAndSettle();
      // The dropdown menu is a lazily-built scrollable; scroll the newest
      // entries into view before asserting on them.
      final menuScrollable = find.descendant(
        of: find.byType(Scrollbar),
        matching: find.byType(Scrollable),
      );
      await tester.dragUntilVisible(
        find.text('Stock Count Variance'),
        menuScrollable,
        const Offset(0, -50),
      );
      expect(find.text('Godown-wise Stock'), findsOneWidget);
      expect(find.text('Godown-wise Movements'), findsOneWidget);
      expect(find.text('Godown-wise Valuation'), findsOneWidget);
      expect(find.text('In-Transit Stock'), findsOneWidget);
      expect(find.text('Stock Count Variance'), findsOneWidget);
    },
  );

  testWidgets('reports show an empty state', (tester) async {
    final fixture = TestFixture(permissions: {'reports.sales'});
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Reports'));
    await tester.pumpAndSettle();
    expect(find.text('No report data for this period.'), findsOneWidget);
  });

  testWidgets('reports show a safe error state', (tester) async {
    final fixture = TestFixture(
      permissions: {'reports.view'},
      reportError: true,
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Reports'));
    await tester.pumpAndSettle();
    expect(find.text('The report could not be loaded.'), findsOneWidget);
  });

  testWidgets('reports show a loading state', (tester) async {
    final fixture = TestFixture(
      permissions: {'reports.view'},
      reportDelay: const Duration(seconds: 1),
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Reports'));
    await tester.pump();
    await tester.pump();
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    await tester.pumpAndSettle();
  });

  testWidgets('accounts navigation follows accounting permissions', (
    tester,
  ) async {
    final denied = TestFixture();
    await tester.pumpWidget(denied.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Accounts'), findsNothing);

    final allowed = TestFixture(permissions: {'accounts.coa.view'});
    await tester.pumpWidget(allowed.app);
    await tester.pumpAndSettle();
    await _login(tester);
    expect(find.text('Accounts'), findsOneWidget);
  });

  testWidgets(
    'chart of accounts renders accounts and validates new account form',
    (tester) async {
      final fixture = TestFixture(
        permissions: {'accounts.coa.view', 'accounts.coa.manage'},
      );
      await _openAccounts(tester, fixture);

      expect(find.text('Cash'), findsWidgets);
      expect(find.text('Sales Revenue'), findsOneWidget);

      await tester.tap(find.byKey(const Key('coa_create')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('coa_save')));
      await tester.pump();
      expect(find.text('Required'), findsWidgets);
    },
  );

  testWidgets('journal list renders posted entries', (tester) async {
    final fixture = TestFixture(permissions: {'accounts.journal.view'});
    await _openAccountsTab(tester, fixture, 'Journal / Vouchers');
    expect(find.text('JE-2026-000001'), findsOneWidget);
    expect(find.text('Sale'), findsOneWidget);
  });

  testWidgets('posted journal detail is read only', (tester) async {
    final fixture = TestFixture(permissions: {'accounts.journal.view'});
    await _openAccountsTab(tester, fixture, 'Journal / Vouchers');
    expect(find.byKey(const Key('manual_journal_create')), findsNothing);

    await tester.tap(find.text('JE-2026-000001'));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('posted_journal_read_only')), findsOneWidget);
    expect(
      find.text(
        'Posted journals are permanent and read-only. Corrections are made by reversal.',
      ),
      findsOneWidget,
    );
    expect(find.byKey(const Key('journal_reverse')), findsNothing);
  });

  testWidgets('manual journal requires total debit to equal total credit', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'accounts.journal.view', 'accounts.journal.post'},
    );
    await _openAccountsTab(tester, fixture, 'Journal / Vouchers');

    await tester.tap(find.byKey(const Key('manual_journal_create')));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.byKey(const Key('manual_journal_description')),
      'Test entry',
    );
    await tester.tap(find.byKey(const Key('manual_line_account_0')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('1010 · Cash').last);
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('manual_line_debit_0')), '100');

    await tester.tap(find.byKey(const Key('manual_line_account_1')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('4010 · Sales Revenue').last);
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('manual_line_credit_1')), '50');
    await tester.pump();

    await tester.tap(find.byKey(const Key('manual_journal_post')));
    await tester.pump();
    expect(
      find.text('Total debit must exactly equal total credit.'),
      findsOneWidget,
    );
  });

  testWidgets('general ledger loads account activity with running balance', (
    tester,
  ) async {
    final fixture = TestFixture(permissions: {'accounts.journal.view'});
    await _openAccountsTab(tester, fixture, 'General Ledger');
    expect(find.text('PKR 500.00'), findsWidgets);
  });

  testWidgets('trial balance shows balanced totals with no warning', (
    tester,
  ) async {
    final fixture = TestFixture(permissions: {'accounts.journal.view'});
    await _openAccountsTab(tester, fixture, 'Trial Balance');

    expect(find.text('Total Debit  PKR 500.00'), findsOneWidget);
    expect(find.text('Total Credit  PKR 500.00'), findsOneWidget);
    expect(find.byKey(const Key('trial_balance_warning')), findsNothing);
  });

  testWidgets('profit and loss renders revenue through net profit', (
    tester,
  ) async {
    final fixture = TestFixture(permissions: {'accounts.journal.view'});
    await _openAccountsTab(tester, fixture, 'Profit & Loss');
    expect(find.byKey(const Key('profit_loss_sections')), findsOneWidget);
    expect(find.text('Net Profit'), findsOneWidget);
  });

  testWidgets('balance sheet renders the accounting equation as balanced', (
    tester,
  ) async {
    final fixture = TestFixture(permissions: {'accounts.journal.view'});
    await _openAccountsTab(tester, fixture, 'Balance Sheet');

    expect(find.text('Total Assets'), findsOneWidget);
    expect(
      find.text('Assets do not equal liabilities plus equity'),
      findsNothing,
    );
  });

  testWidgets('receivables and payables render outstanding balances', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'customers.view', 'suppliers.view'},
    );
    await _openAccounts(tester, fixture);

    expect(find.text('Ali Customer'), findsOneWidget);
    expect(find.text('PKR 250.00'), findsOneWidget);

    await tester.tap(find.text('Payables'));
    await tester.pumpAndSettle();
    expect(find.text('ABC Pharma'), findsOneWidget);
    expect(find.text('PKR 10000.00'), findsOneWidget);
  });

  testWidgets(
    'stock transfers navigation follows stock_transfers.view permission',
    (tester) async {
      final denied = TestFixture();
      await tester.pumpWidget(denied.app);
      await tester.pumpAndSettle();
      await _login(tester);
      expect(find.text('Transfers'), findsNothing);

      final allowed = TestFixture(permissions: {'stock_transfers.view'});
      await tester.pumpWidget(allowed.app);
      await tester.pumpAndSettle();
      await _login(tester);
      expect(find.text('Transfers'), findsOneWidget);
    },
  );

  testWidgets('stock transfer list starts empty with no transfers', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'stock_transfers.view'},
      godownCount: 2,
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Transfers'));
    await tester.pumpAndSettle();

    expect(find.text('No transfers found'), findsOneWidget);
    expect(find.byKey(const Key('new_stock_transfer')), findsNothing);
  });

  testWidgets(
    'create transfer requires a different source and destination godown',
    (tester) async {
      final fixture = TestFixture(
        permissions: {'stock_transfers.view', 'stock_transfers.create'},
        godownCount: 2,
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Transfers'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('new_stock_transfer')));
      await tester.pumpAndSettle();
      expect(find.text('New Stock Transfer'), findsOneWidget);

      // Source and destination both default to the branch's default godown.
      await tester.tap(find.byKey(const Key('save_draft_transfer')));
      await tester.pump();
      expect(
        find.text('Source and destination godown must be different.'),
        findsWidgets,
      );
    },
  );

  testWidgets(
    'create, request, approve, dispatch and receive a transfer end to end',
    (tester) async {
      final fixture = TestFixture(
        permissions: {
          'stock_transfers.view',
          'stock_transfers.create',
          'stock_transfers.request',
          'stock_transfers.approve',
          'stock_transfers.dispatch',
          'stock_transfers.receive',
        },
        godownCount: 2,
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Transfers'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('new_stock_transfer')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('transfer_dest_godown')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Annex Store').last);
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('search_transferable_batches')));
      await tester.pumpAndSettle();
      expect(find.text('Panadol Extra (B-001)'), findsOneWidget);

      await tester.tap(find.byIcon(Icons.add_circle_outline));
      await tester.pumpAndSettle();
      expect(find.text('Quantity to request'), findsOneWidget);
      await tester.enterText(find.byKey(const Key('line_quantity')), '10');
      await tester.tap(find.byKey(const Key('confirm_line_quantity')));
      await tester.pumpAndSettle();
      expect(find.text('Qty: 10'), findsOneWidget);

      await tester.tap(find.byKey(const Key('save_request_transfer')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('save_request_transfer')), findsNothing);
      expect(find.widgetWithText(Chip, 'Requested'), findsOneWidget);

      await tester.tap(find.byKey(const ValueKey('transfer_row_transfer-1')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('transfer_action_approve')));
      await tester.pumpAndSettle();
      expect(find.text('Approve transfer'), findsOneWidget);
      await tester.tap(find.byKey(const Key('confirm_approve')));
      await tester.pumpAndSettle();
      expect(find.widgetWithText(Chip, 'Approved'), findsOneWidget);

      await tester.tap(find.byKey(const Key('transfer_action_dispatch')));
      await tester.pumpAndSettle();
      expect(find.text('Dispatch transfer'), findsOneWidget);
      await tester.tap(find.byKey(const Key('confirm_dispatch')));
      await tester.pumpAndSettle();
      expect(find.widgetWithText(Chip, 'Dispatched'), findsOneWidget);

      await tester.tap(find.byKey(const Key('transfer_action_receive')));
      await tester.pumpAndSettle();
      expect(find.text('Receive transfer'), findsOneWidget);
      await tester.tap(find.byKey(const Key('confirm_receive')));
      await tester.pumpAndSettle();
      expect(find.widgetWithText(Chip, 'Received'), findsOneWidget);

      await tester.tap(find.text('Close'));
      await tester.pumpAndSettle();
      expect(find.text('No transfers found'), findsNothing);
    },
  );

  testWidgets(
    'a user without approve permission cannot see the approve action',
    (tester) async {
      final fixture = TestFixture(
        permissions: {
          'stock_transfers.view',
          'stock_transfers.create',
          'stock_transfers.request',
        },
        godownCount: 2,
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Transfers'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('new_stock_transfer')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('transfer_dest_godown')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Annex Store').last);
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('search_transferable_batches')));
      await tester.pumpAndSettle();
      await tester.tap(find.byIcon(Icons.add_circle_outline));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('line_quantity')), '5');
      await tester.tap(find.byKey(const Key('confirm_line_quantity')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('save_request_transfer')));
      await tester.pumpAndSettle();

      expect(find.widgetWithText(Chip, 'Requested'), findsOneWidget);

      await tester.tap(find.byKey(const ValueKey('transfer_row_transfer-1')));
      await tester.pumpAndSettle();
      expect(
        find.descendant(
          of: find.byType(AlertDialog),
          matching: find.widgetWithText(Chip, 'Requested'),
        ),
        findsOneWidget,
      );
      expect(find.byKey(const Key('transfer_action_approve')), findsNothing);
    },
  );

  testWidgets('cancel is available for a draft transfer', (tester) async {
    final fixture = TestFixture(
      permissions: {
        'stock_transfers.view',
        'stock_transfers.create',
        'stock_transfers.cancel',
      },
      godownCount: 2,
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Transfers'));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('new_stock_transfer')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('transfer_dest_godown')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Annex Store').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('search_transferable_batches')));
    await tester.pumpAndSettle();
    await tester.tap(find.byIcon(Icons.add_circle_outline));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('line_quantity')), '5');
    await tester.tap(find.byKey(const Key('confirm_line_quantity')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('save_draft_transfer')));
    await tester.pumpAndSettle();

    expect(find.widgetWithText(Chip, 'Draft'), findsOneWidget);
    await tester.tap(find.byKey(const ValueKey('transfer_row_transfer-1')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('transfer_action_cancel')));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('reason_field')),
      'no longer needed',
    );
    await tester.tap(find.byKey(const Key('confirm_reason')));
    await tester.pumpAndSettle();
    expect(find.widgetWithText(Chip, 'Cancelled'), findsOneWidget);
  });

  testWidgets('edit action is visible on a draft transfer', (tester) async {
    final fixture = TestFixture(
      permissions: {'stock_transfers.view', 'stock_transfers.create'},
      godownCount: 2,
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Transfers'));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('new_stock_transfer')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('transfer_dest_godown')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Annex Store').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('search_transferable_batches')));
    await tester.pumpAndSettle();
    await tester.tap(find.byIcon(Icons.add_circle_outline));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('line_quantity')), '5');
    await tester.tap(find.byKey(const Key('confirm_line_quantity')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('save_draft_transfer')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const ValueKey('transfer_row_transfer-1')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('transfer_action_edit')), findsOneWidget);
  });

  testWidgets('edit action is absent once a transfer has been requested', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {
        'stock_transfers.view',
        'stock_transfers.create',
        'stock_transfers.request',
      },
      godownCount: 2,
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Transfers'));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('new_stock_transfer')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('transfer_dest_godown')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Annex Store').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('search_transferable_batches')));
    await tester.pumpAndSettle();
    await tester.tap(find.byIcon(Icons.add_circle_outline));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('line_quantity')), '5');
    await tester.tap(find.byKey(const Key('confirm_line_quantity')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('save_request_transfer')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const ValueKey('transfer_row_transfer-1')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('transfer_action_edit')), findsNothing);
  });

  testWidgets('editing a draft transfer updates header fields', (tester) async {
    final fixture = TestFixture(
      permissions: {'stock_transfers.view', 'stock_transfers.create'},
      godownCount: 2,
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Transfers'));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('new_stock_transfer')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('transfer_dest_godown')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Annex Store').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('search_transferable_batches')));
    await tester.pumpAndSettle();
    await tester.tap(find.byIcon(Icons.add_circle_outline));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('line_quantity')), '5');
    await tester.tap(find.byKey(const Key('confirm_line_quantity')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('save_draft_transfer')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const ValueKey('transfer_row_transfer-1')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('transfer_action_edit')));
    await tester.pumpAndSettle();
    expect(find.text('Edit Stock Transfer'), findsOneWidget);

    await tester.enterText(
      find.byKey(const Key('transfer_notes')),
      'updated header note',
    );
    await tester.tap(find.byKey(const Key('save_transfer_edit')));
    await tester.pumpAndSettle();

    expect(find.text('Notes: updated header note'), findsOneWidget);
  });

  testWidgets('editing a draft transfer updates batch lines and quantities', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {'stock_transfers.view', 'stock_transfers.create'},
      godownCount: 2,
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Transfers'));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('new_stock_transfer')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('transfer_dest_godown')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Annex Store').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('search_transferable_batches')));
    await tester.pumpAndSettle();
    await tester.tap(find.byIcon(Icons.add_circle_outline));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('line_quantity')), '5');
    await tester.tap(find.byKey(const Key('confirm_line_quantity')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('save_draft_transfer')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const ValueKey('transfer_row_transfer-1')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('transfer_action_edit')));
    await tester.pumpAndSettle();

    // The existing line is pre-filled from the transfer being edited.
    expect(find.text('Qty: 5'), findsOneWidget);

    // Re-adding the same batch with a new quantity updates the existing line in place.
    await tester.tap(find.byKey(const Key('search_transferable_batches')));
    await tester.pumpAndSettle();
    await tester.tap(find.byIcon(Icons.add_circle_outline));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('line_quantity')), '12');
    await tester.tap(find.byKey(const Key('confirm_line_quantity')));
    await tester.pumpAndSettle();

    expect(find.text('Qty: 12'), findsOneWidget);
    expect(find.text('Qty: 5'), findsNothing);
  });

  testWidgets(
    'changing the source godown while editing invalidates the batch selection',
    (tester) async {
      final fixture = TestFixture(
        permissions: {'stock_transfers.view', 'stock_transfers.create'},
        godownCount: 2,
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Transfers'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('new_stock_transfer')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('transfer_dest_godown')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Annex Store').last);
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('search_transferable_batches')));
      await tester.pumpAndSettle();
      await tester.tap(find.byIcon(Icons.add_circle_outline));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('line_quantity')), '5');
      await tester.tap(find.byKey(const Key('confirm_line_quantity')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('save_draft_transfer')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const ValueKey('transfer_row_transfer-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('transfer_action_edit')));
      await tester.pumpAndSettle();
      expect(find.text('Panadol Extra (B-001)'), findsOneWidget);

      await tester.tap(find.byKey(const Key('transfer_source_godown')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Annex Store').last);
      await tester.pumpAndSettle();

      expect(find.text('Panadol Extra (B-001)'), findsNothing);
      expect(find.text('No items added yet'), findsOneWidget);
    },
  );

  testWidgets(
    'editing a draft transfer sends the updated payload to the backend',
    (tester) async {
      final fixture = TestFixture(
        permissions: {'stock_transfers.view', 'stock_transfers.create'},
        godownCount: 2,
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Transfers'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('new_stock_transfer')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('transfer_dest_godown')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Annex Store').last);
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('search_transferable_batches')));
      await tester.pumpAndSettle();
      await tester.tap(find.byIcon(Icons.add_circle_outline));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('line_quantity')), '5');
      await tester.tap(find.byKey(const Key('confirm_line_quantity')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('save_draft_transfer')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const ValueKey('transfer_row_transfer-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('transfer_action_edit')));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.byKey(const Key('transfer_notes')),
        'payload check',
      );
      await tester.tap(find.byKey(const Key('search_transferable_batches')));
      await tester.pumpAndSettle();
      await tester.tap(find.byIcon(Icons.add_circle_outline));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('line_quantity')), '9');
      await tester.tap(find.byKey(const Key('confirm_line_quantity')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('save_transfer_edit')));
      await tester.pumpAndSettle();

      final body = fixture.api.lastStockTransferUpdateBody!;
      expect(body['sourceGodownId'], 'godown-1');
      expect(body['destinationGodownId'], 'godown-2');
      expect(body['notes'], 'payload check');
      final items = body['items'] as List<dynamic>;
      expect(items, hasLength(1));
      expect((items.single as Map<String, dynamic>)['quantityRequested'], 9);
    },
  );

  testWidgets(
    'per-item and header notes entered while receiving are visible in the transfer detail',
    (tester) async {
      final fixture = TestFixture(
        permissions: {
          'stock_transfers.view',
          'stock_transfers.create',
          'stock_transfers.request',
          'stock_transfers.approve',
          'stock_transfers.dispatch',
          'stock_transfers.receive',
        },
        godownCount: 2,
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Transfers'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('new_stock_transfer')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('transfer_dest_godown')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Annex Store').last);
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('search_transferable_batches')));
      await tester.pumpAndSettle();
      await tester.tap(find.byIcon(Icons.add_circle_outline));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('line_quantity')), '10');
      await tester.tap(find.byKey(const Key('confirm_line_quantity')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('save_request_transfer')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const ValueKey('transfer_row_transfer-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('transfer_action_approve')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm_approve')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('transfer_action_dispatch')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm_dispatch')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('transfer_action_receive')));
      await tester.pumpAndSettle();
      const itemId = 'transfer-item-1-batch-1';
      await tester.enterText(find.byKey(const Key('receive_qty_$itemId')), '7');
      await tester.enterText(
        find.byKey(const Key('receive_notes_$itemId')),
        'carton damaged',
      );
      await tester.enterText(
        find.byKey(const Key('receive_header_notes')),
        'dock note',
      );
      await tester.tap(find.byKey(const Key('confirm_receive')));
      await tester.pumpAndSettle();

      expect(find.widgetWithText(Chip, 'PartiallyReceived'), findsOneWidget);
      expect(find.textContaining('carton damaged'), findsOneWidget);
      expect(find.text('Notes: dock note'), findsOneWidget);
    },
  );

  testWidgets(
    'inventory batches tab shows a godown filter and column when multiple godowns exist',
    (tester) async {
      final fixture = TestFixture(
        permissions: {'inventory.view'},
        godownCount: 2,
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Inventory'));
      await tester.pumpAndSettle();
      await tester.tap(
        find.descendant(
          of: find.byType(TabBar),
          matching: find.text('Batches'),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Godown'), findsWidgets);
      expect(find.text('Main Store'), findsOneWidget);

      await tester.tap(find.byKey(const Key('inventory_godown_filter')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Annex Store').last);
      await tester.pumpAndSettle();

      expect(find.text('No batches found'), findsOneWidget);
    },
  );

  testWidgets(
    'new stock taking dialog offers a godown scope and defaults to whole branch',
    (tester) async {
      final fixture = TestFixture(
        permissions: {'inventory.view', 'inventory.stock_count'},
        godownCount: 2,
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Inventory'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('new_stock_taking')));
      await tester.pumpAndSettle();

      await tester.tap(
        find.widgetWithText(DropdownButtonFormField<String>, 'Branch'),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Head Office').last);
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('stock_count_godown')), findsOneWidget);
      expect(find.text('Whole branch (all godowns)'), findsOneWidget);

      await tester.tap(find.byKey(const Key('stock_count_godown')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Annex Store').last);
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('save_stock_count_session')));
      await tester.pumpAndSettle();

      expect(
        fixture.api.lastCreateStockCountSessionBody?['godownId'],
        'godown-2',
      );
    },
  );

  testWidgets(
    'quotations, sales orders, wholesale and price levels navigation follow permissions',
    (tester) async {
      final denied = TestFixture();
      await tester.pumpWidget(denied.app);
      await tester.pumpAndSettle();
      await _login(tester);
      expect(find.text('Quotations'), findsNothing);
      expect(find.text('Sales Orders'), findsNothing);
      expect(find.text('Wholesale'), findsNothing);
      expect(find.text('Price Levels'), findsNothing);

      final allowed = TestFixture(
        permissions: {
          'quotations.view',
          'sales_orders.view',
          'sales.wholesale',
          'pricing.view',
        },
      );
      await tester.pumpWidget(allowed.app);
      await tester.pumpAndSettle();
      await _login(tester);
      expect(find.text('Quotations'), findsOneWidget);
      expect(find.text('Sales Orders'), findsOneWidget);
      expect(find.text('Wholesale'), findsOneWidget);
      expect(find.text('Price Levels'), findsOneWidget);
    },
  );

  testWidgets('creating a quotation and moving it through send and accept', (
    tester,
  ) async {
    final fixture = TestFixture(
      permissions: {
        'quotations.view',
        'quotations.create',
        'quotations.send',
        'quotations.accept',
      },
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Quotations'));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('add_quotation')));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('quotation_customer_search')),
      'Ali',
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Ali Customer').last);
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('quotation_product_search')),
      'Panadol',
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Panadol Extra').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('save_quotation')));
    await tester.pumpAndSettle();

    expect(fixture.api.lastQuotationBody?['customerId'], 'customer-1');
    expect(find.text('QT-2026-000001'), findsOneWidget);
    expect(find.text('Draft'), findsOneWidget);

    await tester.ensureVisible(find.byTooltip('Open'));
    await tester.tap(find.byTooltip('Open'));
    await tester.pumpAndSettle();
    expect(find.text('Status: Draft'), findsOneWidget);

    await tester.tap(find.byKey(const Key('send_quotation')));
    await tester.pumpAndSettle();
    expect(find.text('Status: Sent'), findsOneWidget);

    await tester.tap(find.byKey(const Key('accept_quotation')));
    await tester.pumpAndSettle();
    expect(find.text('Status: Accepted'), findsOneWidget);
  });

  testWidgets(
    'creating a sales order, confirming and partially fulfilling it',
    (tester) async {
      final fixture = TestFixture(
        permissions: {
          'sales_orders.view',
          'sales_orders.create',
          'sales_orders.confirm',
          'sales_orders.fulfill',
        },
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Sales Orders'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('add_sales_order')));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const Key('sales_order_customer_search')),
        'Ali',
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Ali Customer').last);
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const Key('sales_order_product_search')),
        'Panadol',
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Panadol Extra').last);
      await tester.pumpAndSettle();
      await tester.enterText(find.widgetWithText(TextFormField, 'Qty'), '10');
      await tester.tap(find.byKey(const Key('save_sales_order')));
      await tester.pumpAndSettle();

      expect(find.text('SO-2026-000001'), findsOneWidget);
      expect(find.text('Draft'), findsOneWidget);

      await tester.ensureVisible(find.byTooltip('Open'));
      await tester.tap(find.byTooltip('Open'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm_sales_order')));
      await tester.pumpAndSettle();
      expect(find.text('Status: Confirmed'), findsOneWidget);

      await tester.enterText(
        find.byKey(const Key('fulfill_qty_sales-order-item-product-1')),
        '4',
      );
      await tester.tap(find.byKey(const Key('fulfill_sales_order')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Record fully on customer credit'));
      await tester.pumpAndSettle();

      expect(fixture.api.lastFulfillSalesOrderBody?['items'], isNotNull);
      expect(find.text('Status: PartiallyFulfilled'), findsOneWidget);
    },
  );

  testWidgets('price levels screen creates a new level', (tester) async {
    final fixture = TestFixture(
      permissions: {'pricing.view', 'pricing.manage'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Price Levels'));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('add_price_level')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('price_level_name')), 'Trade');
    await tester.enterText(find.byKey(const Key('price_level_code')), 'TRADE');
    await tester.tap(find.byKey(const Key('save_price_level')));
    await tester.pumpAndSettle();

    expect(fixture.api.lastPriceLevelBody?['name'], 'Trade');
    expect(find.text('Trade'), findsOneWidget);
  });

  testWidgets('customer form exposes commercial fields', (tester) async {
    final fixture = TestFixture(
      permissions: {'customers.view', 'customers.create'},
    );
    await tester.pumpWidget(fixture.app);
    await tester.pumpAndSettle();
    await _login(tester);
    await tester.tap(find.text('Customers'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('add_customer')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('customer_type')), findsOneWidget);
    expect(find.byKey(const Key('customer_price_level')), findsOneWidget);
    expect(find.byKey(const Key('customer_credit_allowed')), findsOneWidget);
    expect(find.byKey(const Key('customer_credit_days')), findsOneWidget);
  });

  testWidgets(
    'reports show quotation and sales order sections when permitted',
    (tester) async {
      final fixture = TestFixture(
        permissions: {'reports.view', 'quotations.view', 'sales_orders.view'},
      );
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await _login(tester);
      await tester.tap(find.text('Reports'));
      await tester.pumpAndSettle();

      await tester.tap(find.byType(DropdownButton<String>).first);
      await tester.pumpAndSettle();
      expect(find.text('Quotations').last, findsOneWidget);
      expect(find.text('Sales Orders').last, findsOneWidget);
    },
  );
}

Future<void> _login(WidgetTester tester) async {
  await tester.enterText(find.byKey(const Key('login_username')), 'test');
  await tester.enterText(
    find.byKey(const Key('login_password')),
    'StrongPass1!',
  );
  await tester.tap(find.byKey(const Key('login_submit')));
  await tester.pumpAndSettle();
}

Future<void> _openAccounts(WidgetTester tester, TestFixture fixture) async {
  tester.view.physicalSize = const Size(1600, 1000);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(fixture.app);
  await tester.pumpAndSettle();
  await _login(tester);
  await tester.tap(find.text('Accounts'));
  await tester.pumpAndSettle();
}

Future<void> _openAccountsTab(
  WidgetTester tester,
  TestFixture fixture,
  String tab,
) async {
  await _openAccounts(tester, fixture);
  if (tab != 'Dashboard') {
    await tester.tap(find.text(tab));
    await tester.pumpAndSettle();
  }
}

class TestFixture {
  TestFixture({
    this.loginError = false,
    this.mustChangePassword = false,
    this.permissions = const {},
    this.adjustmentError = false,
    this.supplierError = false,
    this.purchaseError = false,
    this.reportError = false,
    this.reportDelay = Duration.zero,
    this.godownCount = 1,
  }) {
    api = FakeApi(
      user: CurrentUser(
        id: 'user-1',
        username: 'test',
        fullName: 'Test User',
        branch: branch,
        roles: [role],
        permissions: permissions,
        mustChangePassword: mustChangePassword,
      ),
      loginError: loginError,
      adjustmentError: adjustmentError,
      supplierError: supplierError,
      purchaseError: purchaseError,
      reportError: reportError,
      reportDelay: reportDelay,
      godownCount: godownCount,
    );
    state = AuthState(api, MemoryTokenStore());
  }

  final bool loginError;
  final bool mustChangePassword;
  final Set<String> permissions;
  final bool adjustmentError;
  final bool supplierError;
  final bool purchaseError;
  final bool reportError;
  final Duration reportDelay;
  final int godownCount;
  final branch = const BranchInfo(
    id: 'branch-1',
    code: 'HQ',
    name: 'Head Office',
  );
  final role = const RoleInfo(id: 'role-1', name: 'Cashier');
  late final FakeApi api;
  late final AuthState state;

  Widget get app => PharmacyPOSApp(key: UniqueKey(), authState: state);
}

class _FakeTransferItem {
  _FakeTransferItem({
    required this.id,
    required this.productId,
    required this.productName,
    required this.sku,
    required this.sourceProductBatchId,
    required this.batchNumber,
    required this.expiryDate,
    required this.unitCost,
    required this.requested,
  });
  final String id,
      productId,
      productName,
      sku,
      sourceProductBatchId,
      batchNumber;
  final DateTime expiryDate;
  final double unitCost;
  final int requested;
  int approved = 0, dispatched = 0, received = 0;
  String? destinationProductBatchId;
  String? notes;
}

class _FakeTransfer {
  _FakeTransfer({
    required this.id,
    required this.transferNumber,
    required this.sourceBranchId,
    required this.sourceBranchName,
    required this.sourceGodownId,
    required this.sourceGodownName,
    required this.destBranchId,
    required this.destBranchName,
    required this.destGodownId,
    required this.destGodownName,
    required this.transferDate,
    this.notes,
  });
  final String id, transferNumber;
  String sourceBranchId, sourceBranchName, sourceGodownId, sourceGodownName;
  String destBranchId, destBranchName, destGodownId, destGodownName;
  DateTime transferDate;
  String? notes;
  String status = 'Draft';
  String? createdBy,
      requestedBy,
      approvedBy,
      dispatchedBy,
      receivedBy,
      cancelledBy,
      cancellationReason;
  DateTime? requestedAt, approvedAt, dispatchedAt, receivedAt, cancelledAt;
  final List<_FakeTransferItem> items = [];
}

final List<TransferableBatch> _fakeTransferableBatches = [
  TransferableBatch(
    productBatchId: 'batch-1',
    productId: 'product-1',
    productName: 'Panadol Extra',
    sku: 'MED-001',
    batchNumber: 'B-001',
    expiryDate: DateTime(2027, 6, 30),
    quantityAvailable: 50,
    purchasePrice: 8,
    retailPrice: 12,
  ),
];

Map<String, dynamic> _transferListJson(_FakeTransfer t) => {
  'id': t.id,
  'transferNumber': t.transferNumber,
  'transferDate': t.transferDate.toIso8601String().substring(0, 10),
  'status': t.status,
  'sourceBranchId': t.sourceBranchId,
  'sourceBranchName': t.sourceBranchName,
  'sourceGodownId': t.sourceGodownId,
  'sourceGodownName': t.sourceGodownName,
  'destinationBranchId': t.destBranchId,
  'destinationBranchName': t.destBranchName,
  'destinationGodownId': t.destGodownId,
  'destinationGodownName': t.destGodownName,
  'quantityRequested': t.items.fold<int>(0, (a, i) => a + i.requested),
  'quantityApproved': t.items.fold<int>(0, (a, i) => a + i.approved),
  'quantityDispatched': t.items.fold<int>(0, (a, i) => a + i.dispatched),
  'quantityReceived': t.items.fold<int>(0, (a, i) => a + i.received),
  'quantityInTransit': t.items.fold<int>(
    0,
    (a, i) => a + i.dispatched - i.received,
  ),
  'requestedBy': t.requestedBy,
  'createdAt': DateTime.now().toIso8601String(),
};

Map<String, dynamic> _transferItemJson(_FakeTransferItem i) => {
  'id': i.id,
  'productId': i.productId,
  'productName': i.productName,
  'sku': i.sku,
  'sourceProductBatchId': i.sourceProductBatchId,
  'batchNumber': i.batchNumber,
  'expiryDate': i.expiryDate.toIso8601String().substring(0, 10),
  'unitCostSnapshot': i.unitCost,
  'destinationProductBatchId': i.destinationProductBatchId,
  'quantityRequested': i.requested,
  'quantityApproved': i.approved,
  'quantityDispatched': i.dispatched,
  'quantityReceived': i.received,
  'quantityInTransit': i.dispatched - i.received,
  'notes': i.notes,
};

Map<String, dynamic> _transferDetailJson(_FakeTransfer t) => {
  ..._transferListJson(t),
  'notes': t.notes,
  'createdBy': t.createdBy,
  'requestedAtUtc': t.requestedAt?.toIso8601String(),
  'approvedBy': t.approvedBy,
  'approvedAtUtc': t.approvedAt?.toIso8601String(),
  'dispatchedBy': t.dispatchedBy,
  'dispatchedAtUtc': t.dispatchedAt?.toIso8601String(),
  'receivedBy': t.receivedBy,
  'receivedAtUtc': t.receivedAt?.toIso8601String(),
  'cancelledBy': t.cancelledBy,
  'cancelledAtUtc': t.cancelledAt?.toIso8601String(),
  'cancellationReason': t.cancellationReason,
  'items': t.items.map(_transferItemJson).toList(),
};

class FakeApi implements PharmacyApi {
  FakeApi({
    required this.user,
    required this.loginError,
    this.adjustmentError = false,
    this.supplierError = false,
    this.purchaseError = false,
    this.reportError = false,
    this.reportDelay = Duration.zero,
    int godownCount = 1,
  }) {
    _godowns = List.generate(
      godownCount,
      (i) => GodownListItem(
        id: 'godown-${i + 1}',
        branchId: user.branch.id,
        branchName: user.branch.name,
        code: i == 0 ? 'MAIN' : 'ANNEX',
        name: i == 0 ? 'Main Store' : 'Annex Store',
        description: i == 0 ? 'Primary warehouse' : null,
        isDefault: i == 0,
        isActive: true,
      ),
    );
    _stockCountSession = StockCountSession(
      id: 'session-1',
      countNumber: 'SC-2026-000001',
      branchId: user.branch.id,
      branchName: user.branch.name,
      countDate: DateTime(2026, 9, 9),
      status: 'Draft',
      scope: 'Full',
      createdBy: user.fullName,
      totalItems: 1,
      countedItems: 0,
      varianceItems: 0,
      items: [
        StockCountLine(
          id: 'line-1',
          productId: 'product-1',
          productName: 'Panadol Extra',
          sku: 'MED-001',
          productBatchId: 'batch-1',
          batchNumber: 'B-001',
          expiryDate: _lineExpiry,
          systemQuantity: 10,
          unitCostSnapshot: 8,
        ),
      ],
    );
  }

  CurrentUser user;
  final bool loginError;
  final bool adjustmentError;
  final bool supplierError;
  final bool purchaseError;
  final bool reportError;
  final Duration reportDelay;
  Map<String, dynamic>? lastPurchaseReturnBody;
  Map<String, dynamic>? lastGoodsReceiptBody;
  Map<String, dynamic>? lastCreateStockCountSessionBody;
  Map<String, dynamic>? lastDirectPurchaseBody;
  Map<String, dynamic>? lastHoldSaleBody;
  Map<String, dynamic>? lastPostSaleBody;
  String? lastSearchPosProductsGodownId;
  late final List<GodownListItem> _godowns;
  final List<UserGodownAssignment> _godownAssignments = [];

  GodownListItem _copyGodown(
    GodownListItem g, {
    String? code,
    String? name,
    String? description,
    bool? isDefault,
    bool? isActive,
  }) => GodownListItem(
    id: g.id,
    branchId: g.branchId,
    branchName: g.branchName,
    code: code ?? g.code,
    name: name ?? g.name,
    description: description ?? g.description,
    isDefault: isDefault ?? g.isDefault,
    isActive: isActive ?? g.isActive,
  );

  GodownListItem? defaultGodownFor(String branchId) =>
      _godowns.where((g) => g.branchId == branchId && g.isDefault).firstOrNull;

  @override
  Future<LoginSession> login(String username, String password) async {
    if (loginError) {
      throw const ApiException(
        'Invalid username or password.',
        statusCode: 401,
      );
    }
    return LoginSession(
      accessToken: 'token',
      expiresAtUtc: DateTime.utc(2026, 8, 30, 13),
      user: user,
    );
  }

  @override
  Future<CurrentUser> me(String token) async => user;

  @override
  Future<LoginSession> changePassword(
    String token,
    String currentPassword,
    String newPassword,
  ) async {
    user = CurrentUser(
      id: user.id,
      username: user.username,
      fullName: user.fullName,
      branch: user.branch,
      roles: user.roles,
      permissions: user.permissions,
      mustChangePassword: false,
    );
    return LoginSession(
      accessToken: 'new-token',
      expiresAtUtc: DateTime.utc(2026, 8, 30, 13),
      user: user,
    );
  }

  @override
  Future<PagedUsers> listUsers(
    String token, {
    String? search,
    String? roleId,
    String? branchId,
    bool? isActive,
  }) async => PagedUsers(
    items: [
      UserListItem(
        id: 'user-2',
        fullName: 'Second User',
        username: 'cashier',
        branch: user.branch,
        role: user.roles.first,
        isActive: true,
        mustChangePassword: false,
      ),
    ],
    totalCount: 1,
  );

  @override
  Future<UserOptions> userOptions(String token) async =>
      UserOptions(branches: [user.branch], roles: user.roles);

  @override
  Future<UserDetails> userDetails(String token, String id) async => UserDetails(
    id: id,
    fullName: 'Second User',
    username: 'cashier',
    branch: user.branch,
    roles: user.roles,
    isActive: true,
    mustChangePassword: false,
  );

  @override
  Future<UserDetails> createUser(String token, Map<String, dynamic> values) =>
      userDetails(token, 'created');

  @override
  Future<UserDetails> updateUser(
    String token,
    String id,
    Map<String, dynamic> values,
  ) => userDetails(token, id);

  @override
  Future<void> setUserActive(String token, String id, bool active) async {}

  @override
  Future<void> resetPassword(String token, String id, String password) async {}

  CustomerListItem get customer => const CustomerListItem(
    id: 'customer-1',
    customerCode: 'CUS-000001',
    name: 'Ali Customer',
    phoneNumber: '03001234567',
    email: 'ali@example.com',
    city: 'Lahore',
    businessName: null,
    creditLimit: 5000,
    outstandingBalance: 250,
    advanceBalance: 0,
    isActive: true,
  );

  CustomerDetails get customerDetailsData => CustomerDetails(
    id: customer.id,
    customerCode: customer.customerCode,
    name: customer.name,
    phoneNumber: customer.phoneNumber,
    alternatePhone: null,
    email: customer.email,
    address: 'Main Road',
    city: customer.city,
    businessName: customer.businessName,
    ntn: null,
    openingBalance: 0,
    creditLimit: customer.creditLimit,
    isActive: customer.isActive,
    outstandingBalance: customer.outstandingBalance,
    advanceBalance: customer.advanceBalance,
    totalPayments: 0,
    createdAt: DateTime(2026, 9, 4),
    updatedAt: DateTime(2026, 9, 4),
  );

  @override
  Future<PagedCustomers> listCustomers(
    String token, {
    String? search,
    bool? isActive,
  }) async => PagedCustomers(items: [customer], totalCount: 1);

  @override
  Future<List<CustomerLookup>> lookupCustomers(
    String token, {
    String? search,
  }) async => [
    CustomerLookup(
      id: customer.id,
      customerCode: customer.customerCode,
      name: customer.name,
      phoneNumber: customer.phoneNumber,
      creditLimit: customer.creditLimit,
      outstandingBalance: customer.outstandingBalance,
      availableCredit: customer.creditLimit - customer.outstandingBalance,
      isActive: customer.isActive,
    ),
  ];

  @override
  Future<CustomerDetails> customerDetails(String token, String id) async =>
      customerDetailsData;

  @override
  Future<CustomerListItem> createCustomer(
    String token,
    Map<String, dynamic> values,
  ) async => customer;

  @override
  Future<CustomerListItem> updateCustomer(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => customer;

  @override
  Future<void> setCustomerActive(String token, String id, bool active) async {}

  @override
  Future<PagedCustomerLedger> customerLedger(
    String token,
    String id, {
    DateTime? from,
    DateTime? to,
    String? branchId,
  }) async => PagedCustomerLedger(
    items: [
      CustomerLedgerItem(
        entryDate: DateTime(2026, 9, 4),
        entryType: 'OpeningBalance',
        amount: 250,
        runningBalance: 250,
        branchName: user.branch.name,
        notes: 'Opening',
      ),
    ],
    totalCount: 1,
  );

  @override
  Future<void> recordCustomerPayment(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async {}

  @override
  Future<void> adjustCustomerBalance(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async {}

  PosProduct get posProduct => const PosProduct(
    productId: 'product-1',
    name: 'Panadol Extra',
    sku: 'MED-001',
    barcode: '12345',
    genericName: 'Paracetamol',
    unit: 'Tablet',
    availableQuantity: 10,
    indicativeRetailPrice: 12,
    maximumDiscountPercent: 5,
    isActive: true,
  );

  SaleDetails get sale => SaleDetails(
    id: 'sale-1',
    invoiceNumber: 'INV-2026-000001',
    status: 'Posted',
    createdAt: DateTime(2026, 9, 3, 10),
    postedAtUtc: DateTime(2026, 9, 3, 10),
    branchName: user.branch.name,
    cashierName: user.fullName,
    subtotal: 12,
    discountTotal: 0,
    taxTotal: 0,
    netTotal: 12,
    amountPaid: 12,
    creditAmount: 0,
    changeGiven: 0,
    items: [
      SaleItemDetail(
        productName: 'Panadol Extra',
        sku: 'MED-001',
        requestedQuantity: 1,
        discountPercent: 0,
        grossAmount: 12,
        discountAmount: 0,
        netAmount: 12,
        allocations: [
          SaleAllocation(
            batchNumber: 'B-001',
            expiryDate: DateTime(2027, 9, 3),
            quantity: 1,
            unitRetailPriceSnapshot: 12,
            unitSalePriceSnapshot: 12,
            netAmount: 12,
          ),
        ],
      ),
    ],
    payments: const [
      SalePaymentDetail(method: 'Cash', amountApplied: 12, tenderedAmount: 12),
    ],
  );

  @override
  Future<List<PosProduct>> searchPosProducts(
    String token, {
    String? query,
    String? godownId,
  }) async {
    lastSearchPosProductsGodownId = godownId;
    return [posProduct];
  }

  @override
  Future<SaleDetails> holdSale(
    String token,
    Map<String, dynamic> values,
  ) async {
    lastHoldSaleBody = values;
    return SaleDetails(
      id: 'hold-1',
      holdNumber: 'HOLD-2026-000001',
      status: 'Held',
      createdAt: DateTime(2026, 9, 3, 10),
      branchName: user.branch.name,
      cashierName: user.fullName,
      subtotal: 0,
      discountTotal: 0,
      taxTotal: 0,
      netTotal: 0,
      amountPaid: 0,
      creditAmount: 0,
      changeGiven: 0,
      items: const [],
      payments: const [],
    );
  }

  @override
  Future<SaleDetails> postSale(
    String token,
    Map<String, dynamic> values,
  ) async {
    lastPostSaleBody = values;
    return sale;
  }

  @override
  Future<SaleDetails> postHeldSale(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => sale;

  @override
  Future<void> cancelHeldSale(String token, String id) async {}

  @override
  Future<PagedSales> listHeldSales(String token, {String? search}) async =>
      const PagedSales(items: [], totalCount: 0);

  @override
  Future<PagedSales> listSales(String token, {String? search}) async =>
      PagedSales(
        items: [
          SaleListItem(
            id: 'sale-1',
            invoiceNumber: 'INV-2026-000001',
            status: 'Posted',
            createdAt: DateTime(2026, 9, 3, 10),
            postedAtUtc: DateTime(2026, 9, 3, 10),
            branchName: user.branch.name,
            cashierName: user.fullName,
            itemCount: 1,
            netTotal: 12,
            amountPaid: 12,
            creditAmount: 0,
            changeGiven: 0,
            paymentSummary: 'Cash:12',
            returnState: 'NotReturned',
          ),
        ],
        totalCount: 1,
      );

  @override
  Future<SaleDetails> saleDetails(String token, String id) async => sale;

  @override
  Future<SaleDetails> saleReceipt(String token, String id) async => sale;

  @override
  Future<SaleDetails> reprintSaleReceipt(String token, String id) async => sale;
  CatalogLookup get category =>
      const CatalogLookup(id: 'category-1', name: 'Tablets', isActive: true);
  CatalogLookup get manufacturer => const CatalogLookup(
    id: 'manufacturer-1',
    name: 'Acme Pharma',
    isActive: true,
  );
  ProductDetails get product => ProductDetails(
    id: 'product-1',
    name: 'Panadol Extra',
    sku: 'MED-001',
    category: category,
    manufacturer: manufacturer,
    unit: 'Box',
    packSize: 10,
    purchasePrice: 20,
    retailPrice: 25,
    maximumDiscountPercent: 5,
    reorderLevel: 10,
    isActive: true,
    genericName: 'Paracetamol',
  );

  @override
  Future<PagedProducts> listProducts(
    String token, {
    int page = 1,
    String? search,
    String? categoryId,
    String? manufacturerId,
    bool? isActive,
  }) async =>
      PagedProducts(items: [product], page: page, pageSize: 25, totalCount: 1);
  @override
  Future<ProductOptions> productOptions(String token) async => ProductOptions(
    categories: [category],
    manufacturers: [manufacturer],
    units: const ['Box', 'Piece'],
  );
  @override
  Future<ProductDetails> productDetails(String token, String id) async =>
      product;
  @override
  Future<ProductDetails> createProduct(
    String token,
    Map<String, dynamic> values,
  ) async => product;
  @override
  Future<ProductDetails> updateProduct(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => product;
  @override
  Future<void> setProductActive(String token, String id, bool active) async {}
  @override
  Future<List<CategoryInfo>> listCategories(
    String token, {
    String? search,
    bool? isActive,
  }) async => [
    const CategoryInfo(id: 'category-1', name: 'Tablets', isActive: true),
  ];
  @override
  Future<CategoryInfo> saveCategory(
    String token,
    Map<String, dynamic> values, {
    String? id,
  }) async =>
      const CategoryInfo(id: 'category-1', name: 'Tablets', isActive: true);
  @override
  Future<void> setCategoryActive(String token, String id, bool active) async {}
  @override
  Future<List<ManufacturerInfo>> listManufacturers(
    String token, {
    String? search,
    bool? isActive,
  }) async => [
    const ManufacturerInfo(
      id: 'manufacturer-1',
      name: 'Acme Pharma',
      isActive: true,
    ),
  ];
  @override
  Future<ManufacturerInfo> saveManufacturer(
    String token,
    Map<String, dynamic> values, {
    String? id,
  }) async => const ManufacturerInfo(
    id: 'manufacturer-1',
    name: 'Acme Pharma',
    isActive: true,
  );
  @override
  Future<void> setManufacturerActive(
    String token,
    String id,
    bool active,
  ) async {}

  InventoryItem get stock => InventoryItem(
    productId: 'product-1',
    productName: 'Panadol Extra',
    sku: 'MED-001',
    category: 'Tablets',
    quantityInStock: 10,
    reorderLevel: 10,
    stockStatus: 'LowStock',
    activeBatchCount: 1,
    nearestExpiryDate: DateTime(2026, 10, 1),
    estimatedStockValue: 80,
    genericName: 'Paracetamol',
    manufacturer: 'Acme Pharma',
  );

  BatchItem get batch => BatchItem(
    batchId: 'batch-1',
    productId: 'product-1',
    productName: 'Panadol Extra',
    sku: 'MED-001',
    batchNumber: 'B-001',
    branchId: user.branch.id,
    branchName: user.branch.name,
    expiryDate: DateTime(2026, 10, 1),
    quantityAvailable: 10,
    purchasePrice: 8,
    retailPrice: 12,
    estimatedStockValue: 80,
    state: 'NearExpiry',
    godownId: _godowns.firstOrNull?.id,
    godownCode: _godowns.firstOrNull?.code,
    godownName: _godowns.firstOrNull?.name,
  );

  @override
  Future<PagedInventory> listInventory(String token, {String? search}) async =>
      PagedInventory(items: [stock], totalCount: 1);

  @override
  Future<InventoryOptions> inventoryOptions(
    String token, {
    String? productSearch,
  }) async => InventoryOptions(
    branches: [InventoryLookup(id: user.branch.id, name: user.branch.name)],
    categories: const [InventoryLookup(id: 'category-1', name: 'Tablets')],
    manufacturers: const [
      InventoryLookup(id: 'manufacturer-1', name: 'Acme Pharma'),
    ],
    products: const [
      ProductLookup(
        id: 'product-1',
        name: 'Panadol Extra',
        sku: 'MED-001',
        isActive: true,
      ),
    ],
    suppliers: const [InventoryLookup(id: 'supplier-1', name: 'ABC Pharma')],
  );

  @override
  Future<void> addOpeningStock(
    String token,
    Map<String, dynamic> values,
  ) async {}

  @override
  Future<void> adjustStock(
    String token,
    Map<String, dynamic> values, {
    required bool increase,
  }) async {
    if (adjustmentError) {
      throw const ApiException('Insufficient stock in selected batch.');
    }
  }

  @override
  Future<void> reconcileStockCount(
    String token,
    Map<String, dynamic> values,
  ) async {}

  @override
  Future<List<ExpiryItem>> listExpiry(
    String token, {
    int? days,
    String? godownId,
  }) async {
    if (godownId != null && godownId != _godowns.firstOrNull?.id) return [];
    return [
      ExpiryItem(
        batchId: 'batch-1',
        productName: 'Panadol Extra',
        batchNumber: 'B-001',
        expiryDate: DateTime(2026, 10, 1),
        daysRemaining: 29,
        quantityAvailable: 10,
        estimatedStockValue: 80,
        godownId: _godowns.firstOrNull?.id,
        godownName: _godowns.firstOrNull?.name,
      ),
    ];
  }

  @override
  Future<PagedBatches> listBatches(
    String token, {
    String? search,
    String? godownId,
  }) async {
    if (godownId != null && godownId != batch.godownId) {
      return const PagedBatches(items: [], totalCount: 0);
    }
    return PagedBatches(items: [batch], totalCount: 1);
  }

  @override
  Future<PagedMovements> listMovements(
    String token, {
    String? search,
    String? godownId,
  }) async {
    if (godownId != null && godownId != _godowns.firstOrNull?.id) {
      return const PagedMovements(items: [], totalCount: 0);
    }
    return PagedMovements(
      items: [
        StockMovementItem(
          createdAt: DateTime(2026, 9, 1, 10),
          productName: 'Panadol Extra',
          batchNumber: 'B-001',
          branchName: user.branch.name,
          movementType: 'OpeningStock',
          quantity: 10,
          godownId: _godowns.firstOrNull?.id,
          godownName: _godowns.firstOrNull?.name,
        ),
      ],
      totalCount: 1,
    );
  }

  static final DateTime _lineExpiry = DateTime(2026, 10, 1);

  late StockCountSession _stockCountSession;

  @override
  Future<PagedStockCountSessions> listStockCountSessions(
    String token, {
    String? branchId,
    String? status,
  }) async => PagedStockCountSessions(
    items: [
      StockCountSessionSummary(
        id: _stockCountSession.id,
        countNumber: _stockCountSession.countNumber,
        branchId: _stockCountSession.branchId,
        branchName: _stockCountSession.branchName,
        countDate: _stockCountSession.countDate,
        status: _stockCountSession.status,
        scope: _stockCountSession.scope,
        totalItems: _stockCountSession.totalItems,
        countedItems: _stockCountSession.countedItems,
        varianceItems: _stockCountSession.varianceItems,
        createdBy: _stockCountSession.createdBy,
        createdAt: DateTime(2026, 9, 9),
      ),
    ],
    totalCount: 1,
  );

  @override
  Future<StockCountSession> getStockCountSession(
    String token,
    String id,
  ) async => _stockCountSession;

  @override
  Future<StockCountSession> createStockCountSession(
    String token,
    Map<String, dynamic> values,
  ) async {
    lastCreateStockCountSessionBody = values;
    return _stockCountSession;
  }

  @override
  Future<StockCountSession> startStockCountSession(
    String token,
    String id,
  ) async {
    _stockCountSession = StockCountSession(
      id: _stockCountSession.id,
      countNumber: _stockCountSession.countNumber,
      branchId: _stockCountSession.branchId,
      branchName: _stockCountSession.branchName,
      countDate: _stockCountSession.countDate,
      status: 'InProgress',
      scope: _stockCountSession.scope,
      createdBy: _stockCountSession.createdBy,
      startedBy: user.fullName,
      totalItems: _stockCountSession.totalItems,
      countedItems: _stockCountSession.countedItems,
      varianceItems: _stockCountSession.varianceItems,
      items: _stockCountSession.items,
    );
    return _stockCountSession;
  }

  @override
  Future<StockCountSession> submitStockCountEntries(
    String token,
    String id,
    List<Map<String, dynamic>> entries,
  ) async {
    final counted = entries.isNotEmpty
        ? entries.first['countedQuantity'] as int
        : 10;
    final line = StockCountLine(
      id: 'line-1',
      productId: 'product-1',
      productName: 'Panadol Extra',
      sku: 'MED-001',
      productBatchId: 'batch-1',
      batchNumber: 'B-001',
      expiryDate: _lineExpiry,
      systemQuantity: 10,
      unitCostSnapshot: 8,
      countedQuantity: counted,
      variance: counted - 10,
    );
    _stockCountSession = StockCountSession(
      id: _stockCountSession.id,
      countNumber: _stockCountSession.countNumber,
      branchId: _stockCountSession.branchId,
      branchName: _stockCountSession.branchName,
      countDate: _stockCountSession.countDate,
      status: _stockCountSession.status,
      scope: _stockCountSession.scope,
      createdBy: _stockCountSession.createdBy,
      startedBy: _stockCountSession.startedBy,
      totalItems: _stockCountSession.totalItems,
      countedItems: 1,
      varianceItems: line.variance != 0 ? 1 : 0,
      items: [line],
    );
    return _stockCountSession;
  }

  @override
  Future<StockCountSession> finalizeStockCountSession(
    String token,
    String id,
  ) async {
    _stockCountSession = StockCountSession(
      id: _stockCountSession.id,
      countNumber: _stockCountSession.countNumber,
      branchId: _stockCountSession.branchId,
      branchName: _stockCountSession.branchName,
      countDate: _stockCountSession.countDate,
      status: 'Completed',
      scope: _stockCountSession.scope,
      createdBy: _stockCountSession.createdBy,
      startedBy: _stockCountSession.startedBy,
      completedBy: user.fullName,
      totalItems: _stockCountSession.totalItems,
      countedItems: _stockCountSession.countedItems,
      varianceItems: _stockCountSession.varianceItems,
      items: _stockCountSession.items,
    );
    return _stockCountSession;
  }

  @override
  Future<StockCountSession> cancelStockCountSession(
    String token,
    String id,
    String reason,
  ) async {
    _stockCountSession = StockCountSession(
      id: _stockCountSession.id,
      countNumber: _stockCountSession.countNumber,
      branchId: _stockCountSession.branchId,
      branchName: _stockCountSession.branchName,
      countDate: _stockCountSession.countDate,
      status: 'Cancelled',
      scope: _stockCountSession.scope,
      createdBy: _stockCountSession.createdBy,
      totalItems: _stockCountSession.totalItems,
      countedItems: _stockCountSession.countedItems,
      varianceItems: _stockCountSession.varianceItems,
      items: _stockCountSession.items,
    );
    return _stockCountSession;
  }

  CashierShift? _cashierShift;

  @override
  Future<CashierShift?> myOpenCashierShift(String token) async =>
      _cashierShift?.status == 'Open' ? _cashierShift : null;

  @override
  Future<CashierShift> openCashierShift(
    String token,
    Map<String, dynamic> values,
  ) async {
    _cashierShift = CashierShift(
      id: 'shift-1',
      branchId: user.branch.id,
      branchName: user.branch.name,
      cashierUserId: user.id,
      cashierName: user.fullName,
      terminalName: values['terminalName'] as String?,
      openingCash: (values['openingCash'] as num).toDouble(),
      openedAtUtc: DateTime(2026, 9, 9, 9),
      openingNotes: values['openingNotes'] as String?,
      status: 'Open',
      totalSales: 0,
      totalRefunds: 0,
      cashSales: 0,
      cashRefunds: 0,
      customerCashReceived: 0,
      cashPaidOut: 0,
      manualCashIn: 0,
      manualCashOut: 0,
      paymentBreakdown: const [],
      drawerEntries: const [],
    );
    return _cashierShift!;
  }

  @override
  Future<CashierShift> addCashierShiftDrawerEntry(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async {
    final current = _cashierShift!;
    final entryType = values['entryType'] as String;
    final amount = (values['amount'] as num).toDouble();
    final entries = [
      ...current.drawerEntries,
      CashierShiftDrawerEntry(
        id: 'entry-${current.drawerEntries.length + 1}',
        entryType: entryType,
        amount: amount,
        reason: values['reason'] as String,
        createdBy: user.fullName,
        createdAtUtc: DateTime(2026, 9, 9, 10),
      ),
    ];
    _cashierShift = CashierShift(
      id: current.id,
      branchId: current.branchId,
      branchName: current.branchName,
      cashierUserId: current.cashierUserId,
      cashierName: current.cashierName,
      terminalName: current.terminalName,
      openingCash: current.openingCash,
      openedAtUtc: current.openedAtUtc,
      openingNotes: current.openingNotes,
      status: current.status,
      totalSales: current.totalSales,
      totalRefunds: current.totalRefunds,
      cashSales: current.cashSales,
      cashRefunds: current.cashRefunds,
      customerCashReceived: current.customerCashReceived,
      cashPaidOut: current.cashPaidOut,
      manualCashIn: current.manualCashIn + (entryType == 'CashIn' ? amount : 0),
      manualCashOut:
          current.manualCashOut + (entryType == 'CashOut' ? amount : 0),
      paymentBreakdown: current.paymentBreakdown,
      drawerEntries: entries,
    );
    return _cashierShift!;
  }

  @override
  Future<CashierShift> closeCashierShift(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async {
    final current = _cashierShift!;
    final actual = (values['actualCountedCash'] as num).toDouble();
    const expected = 100.0;
    _cashierShift = CashierShift(
      id: current.id,
      branchId: current.branchId,
      branchName: current.branchName,
      cashierUserId: current.cashierUserId,
      cashierName: current.cashierName,
      terminalName: current.terminalName,
      openingCash: current.openingCash,
      openedAtUtc: current.openedAtUtc,
      openingNotes: current.openingNotes,
      status: 'Closed',
      closedAtUtc: DateTime(2026, 9, 9, 17),
      expectedCash: expected,
      actualCountedCash: actual,
      cashVariance: actual - expected,
      closingNotes: values['closingNotes'] as String?,
      totalSales: current.totalSales,
      totalRefunds: current.totalRefunds,
      cashSales: current.cashSales,
      cashRefunds: current.cashRefunds,
      customerCashReceived: current.customerCashReceived,
      cashPaidOut: current.cashPaidOut,
      manualCashIn: current.manualCashIn,
      manualCashOut: current.manualCashOut,
      paymentBreakdown: const [
        CashierShiftPaymentSummary(
          paymentMethod: 'Cash',
          salesAmount: 100,
          refundsAmount: 0,
        ),
      ],
      drawerEntries: current.drawerEntries,
    );
    return _cashierShift!;
  }

  @override
  Future<CashierShift> reconcileCashierShift(
    String token,
    String id,
    String? notes,
  ) async {
    final current = _cashierShift!;
    _cashierShift = CashierShift(
      id: current.id,
      branchId: current.branchId,
      branchName: current.branchName,
      cashierUserId: current.cashierUserId,
      cashierName: current.cashierName,
      terminalName: current.terminalName,
      openingCash: current.openingCash,
      openedAtUtc: current.openedAtUtc,
      openingNotes: current.openingNotes,
      status: 'Reconciled',
      closedAtUtc: current.closedAtUtc,
      expectedCash: current.expectedCash,
      actualCountedCash: current.actualCountedCash,
      cashVariance: current.cashVariance,
      closingNotes: current.closingNotes,
      reconciledBy: user.fullName,
      reconciledAtUtc: DateTime(2026, 9, 9, 18),
      reconciliationNotes: notes,
      totalSales: current.totalSales,
      totalRefunds: current.totalRefunds,
      cashSales: current.cashSales,
      cashRefunds: current.cashRefunds,
      customerCashReceived: current.customerCashReceived,
      cashPaidOut: current.cashPaidOut,
      manualCashIn: current.manualCashIn,
      manualCashOut: current.manualCashOut,
      paymentBreakdown: current.paymentBreakdown,
      drawerEntries: current.drawerEntries,
    );
    return _cashierShift!;
  }

  @override
  Future<CashierShift> cashierShiftDetails(String token, String id) async =>
      _cashierShift!;

  @override
  Future<PagedCashierShifts> listCashierShifts(
    String token, {
    String? status,
  }) async => PagedCashierShifts(
    items: _cashierShift == null
        ? []
        : [
            CashierShiftListItem(
              id: _cashierShift!.id,
              branchId: _cashierShift!.branchId,
              branchName: _cashierShift!.branchName,
              cashierUserId: _cashierShift!.cashierUserId,
              cashierName: _cashierShift!.cashierName,
              terminalName: _cashierShift!.terminalName,
              openingCash: _cashierShift!.openingCash,
              openedAtUtc: _cashierShift!.openedAtUtc,
              status: _cashierShift!.status,
              closedAtUtc: _cashierShift!.closedAtUtc,
              expectedCash: _cashierShift!.expectedCash,
              actualCountedCash: _cashierShift!.actualCountedCash,
              cashVariance: _cashierShift!.cashVariance,
            ),
          ],
    totalCount: _cashierShift == null ? 0 : 1,
  );

  @override
  Future<DailyClosingSummary> dailyCashierClosingSummary(
    String token,
    String branchId,
    DateTime date,
  ) async => DailyClosingSummary(
    branchId: branchId,
    branchName: user.branch.name,
    date: date,
    shiftCount: _cashierShift == null ? 0 : 1,
    openShiftCount: 0,
    totalOpeningCash: _cashierShift?.openingCash ?? 0,
    totalExpectedCash: _cashierShift?.expectedCash ?? 0,
    totalActualCash: _cashierShift?.actualCountedCash ?? 0,
    totalVariance: _cashierShift?.cashVariance ?? 0,
    paymentBreakdown: _cashierShift?.paymentBreakdown ?? const [],
  );

  GodownLookup _lookupFor(GodownListItem g) => GodownLookup(
    id: g.id,
    branchId: g.branchId,
    code: g.code,
    name: g.name,
    isDefault: g.isDefault,
    isActive: g.isActive,
  );

  @override
  Future<PagedGodowns> listGodowns(
    String token, {
    String? branchId,
    String? search,
    bool? isActive,
  }) async {
    final items = _godowns
        .where((g) => branchId == null || g.branchId == branchId)
        .where((g) => isActive == null || g.isActive == isActive)
        .where(
          (g) =>
              search == null ||
              search.isEmpty ||
              g.name.toLowerCase().contains(search.toLowerCase()) ||
              g.code.toLowerCase().contains(search.toLowerCase()),
        )
        .toList();
    return PagedGodowns(items: items, totalCount: items.length);
  }

  @override
  Future<List<GodownLookup>> lookupGodowns(
    String token, {
    String? branchId,
    bool activeOnly = true,
  }) async => _godowns
      .where((g) => branchId == null || g.branchId == branchId)
      .where((g) => !activeOnly || g.isActive)
      .map(_lookupFor)
      .toList();

  @override
  Future<List<GodownLookup>> myGodowns(
    String token, {
    String? branchId,
  }) async => lookupGodowns(token, branchId: branchId);

  @override
  Future<GodownListItem> createGodown(
    String token,
    Map<String, dynamic> values,
  ) async {
    final created = GodownListItem(
      id: 'godown-${_godowns.length + 1}',
      branchId: values['branchId'] as String? ?? user.branch.id,
      branchName: user.branch.name,
      code: values['code'] as String? ?? '',
      name: values['name'] as String? ?? '',
      description: values['description'] as String?,
      isDefault: values['isDefault'] as bool? ?? false,
      isActive: values['isActive'] as bool? ?? true,
    );
    _godowns.add(created);
    return created;
  }

  @override
  Future<GodownListItem> updateGodown(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async {
    final index = _godowns.indexWhere((g) => g.id == id);
    if (index == -1) throw const ApiException('Godown was not found.');
    final updated = _copyGodown(
      _godowns[index],
      code: values['code'] as String?,
      name: values['name'] as String?,
      description: values['description'] as String?,
    );
    _godowns[index] = updated;
    return updated;
  }

  @override
  Future<void> setGodownActive(String token, String id, bool active) async {
    final index = _godowns.indexWhere((g) => g.id == id);
    if (index == -1) return;
    _godowns[index] = _copyGodown(_godowns[index], isActive: active);
  }

  @override
  Future<GodownListItem> setGodownDefault(String token, String id) async {
    final index = _godowns.indexWhere((g) => g.id == id);
    if (index == -1) throw const ApiException('Godown was not found.');
    final branchId = _godowns[index].branchId;
    for (var i = 0; i < _godowns.length; i++) {
      if (_godowns[i].branchId == branchId) {
        _godowns[i] = _copyGodown(_godowns[i], isDefault: _godowns[i].id == id);
      }
    }
    return _godowns[index];
  }

  @override
  Future<List<UserGodownAssignment>> listGodownUsers(
    String token,
    String id,
  ) async => _godownAssignments.where((a) => a.godownId == id).toList();

  @override
  Future<void> assignUserGodown(
    String token,
    String godownId,
    Map<String, dynamic> values,
  ) async {
    final userId = values['userId'] as String;
    _godownAssignments.removeWhere(
      (a) => a.userId == userId && a.godownId == godownId,
    );
    _godownAssignments.add(
      UserGodownAssignment(
        userId: userId,
        userFullName: 'Second User',
        godownId: godownId,
        godownName: _godowns.firstWhere((g) => g.id == godownId).name,
        isDefault: values['isDefault'] as bool? ?? false,
      ),
    );
  }

  @override
  Future<void> unassignUserGodown(
    String token,
    String godownId,
    String userId,
  ) async {
    _godownAssignments.removeWhere(
      (a) => a.userId == userId && a.godownId == godownId,
    );
  }

  @override
  Future<void> setUserDefaultGodown(
    String token,
    String godownId,
    String userId,
  ) async {
    for (var i = 0; i < _godownAssignments.length; i++) {
      final a = _godownAssignments[i];
      if (a.userId == userId) {
        _godownAssignments[i] = UserGodownAssignment(
          userId: a.userId,
          userFullName: a.userFullName,
          godownId: a.godownId,
          godownName: a.godownName,
          isDefault: a.godownId == godownId,
        );
      }
    }
  }

  SupplierListItem get supplier => const SupplierListItem(
    id: 'supplier-1',
    name: 'ABC Pharma',
    shortName: 'ABC',
    contactPerson: 'Ali',
    phoneNumber: '+923001234567',
    city: 'Lahore',
    creditLimit: 20000,
    outstandingBalance: 10000,
    isActive: true,
  );

  @override
  Future<PagedSuppliers> listSuppliers(
    String token, {
    String? search,
    bool? isActive,
  }) async => PagedSuppliers(items: [supplier], totalCount: 1);

  @override
  Future<SupplierListItem> createSupplier(
    String token,
    Map<String, dynamic> values,
  ) async {
    if (supplierError) {
      throw const ApiException('A supplier with this name already exists.');
    }
    return supplier;
  }

  @override
  Future<SupplierListItem> updateSupplier(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => supplier;

  @override
  Future<void> setSupplierActive(String token, String id, bool active) async {}

  @override
  Future<PagedSupplierLedger> supplierLedger(
    String token,
    String id, {
    DateTime? from,
    DateTime? to,
    String? branchId,
  }) async => PagedSupplierLedger(
    items: [
      SupplierLedgerItem(
        entryDate: DateTime(2026, 9, 2),
        entryType: 'OpeningBalance',
        amount: 10000,
        runningBalance: 10000,
        branchName: user.branch.name,
        notes: 'Opening balance',
      ),
    ],
    totalCount: 1,
  );

  @override
  Future<void> recordSupplierPayment(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async {}

  @override
  Future<void> adjustSupplierBalance(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async {}

  PurchaseOrderListItem get purchaseOrder => PurchaseOrderListItem(
    id: 'po-1',
    orderNumber: 'PO-2026-000001',
    orderDate: DateTime(2026, 9, 2),
    supplierId: 'supplier-1',
    supplierName: 'ABC Pharma',
    branchId: user.branch.id,
    branchName: user.branch.name,
    itemCount: 1,
    orderedQuantity: 100,
    receivedQuantity: 60,
    status: 'PartiallyReceived',
  );

  PurchaseDetails get purchase => PurchaseDetails(
    id: 'grn-1',
    grnNumber: 'GRN-2026-000001',
    supplierInvoiceNumber: 'INV-001',
    receiptDate: DateTime(2026, 9, 2),
    supplierName: 'ABC Pharma',
    branchName: user.branch.name,
    subtotal: 5000,
    discountTotal: 0,
    taxTotal: 0,
    netTotal: 5000,
    status: 'Posted',
    items: [
      PurchaseItem(
        productName: 'Panadol Extra',
        sku: 'MED-001',
        batchNumber: 'B-100',
        expiryDate: DateTime(2027, 9, 2),
        purchasedQuantity: 100,
        bonusQuantity: 10,
        inventoryQuantity: 110,
        purchasePrice: 50,
        netLineAmount: 5000,
      ),
    ],
  );

  ReturnablePurchase get returnablePurchaseData => ReturnablePurchase(
    id: 'grn-1',
    grnNumber: 'GRN-2026-000001',
    supplierInvoiceNumber: 'INV-001',
    receiptDate: DateTime(2026, 9, 2),
    supplierId: 'supplier-1',
    supplierName: 'ABC Pharma',
    branchId: user.branch.id,
    branchName: user.branch.name,
    netTotal: 5000,
    returnState: 'NoReturns',
    items: [
      ReturnablePurchaseItem(
        id: 'grn-item-1',
        productId: 'product-1',
        productName: 'Panadol Extra',
        sku: 'MED-001',
        productBatchId: 'batch-1',
        batchNumber: 'B-100',
        expiryDate: DateTime(2027, 9, 2),
        purchasedQuantity: 100,
        bonusQuantity: 10,
        paidQuantityReturned: 0,
        bonusQuantityReturned: 0,
        paidQuantityRemaining: 100,
        bonusQuantityRemaining: 10,
        currentBatchAvailable: 110,
        maxPhysicalReturnQuantity: 110,
        purchasePrice: 50,
        grossRemainingCredit: 5000,
        discountRemaining: 0,
        taxRemaining: 0,
        netRemainingSupplierCredit: 5000,
        isBatchExpired: false,
        isBatchDisposed: false,
      ),
    ],
  );

  PurchaseReturnDetails get purchaseReturn => PurchaseReturnDetails(
    id: 'purchase-return-1',
    returnNumber: 'PR-2026-000001',
    originalGoodsReceiptId: 'grn-1',
    originalGrnNumber: 'GRN-2026-000001',
    supplierInvoiceNumber: 'INV-001',
    supplierName: 'ABC Pharma',
    branchName: user.branch.name,
    processedByName: user.fullName,
    returnDateUtc: DateTime(2026, 9, 4, 10),
    reason: 'Damaged',
    grossReturnAmount: 150,
    discountAdjustment: 0,
    taxAdjustment: 0,
    netSupplierCredit: 150,
    status: 'Posted',
    items: [
      PurchaseReturnItemDetail(
        id: 'purchase-return-item-1',
        originalGoodsReceiptItemId: 'grn-item-1',
        productName: 'Panadol Extra',
        sku: 'MED-001',
        batchNumber: 'B-100',
        expiryDate: DateTime(2027, 9, 2),
        paidReturnQuantity: 3,
        bonusReturnQuantity: 2,
        totalPhysicalQuantity: 5,
        purchasePriceSnapshot: 50,
        netSupplierCredit: 150,
      ),
    ],
  );

  @override
  Future<PagedPurchaseOrders> listPurchaseOrders(
    String token, {
    String? search,
  }) async => PagedPurchaseOrders(items: [purchaseOrder], totalCount: 1);

  @override
  Future<PurchaseOrderDetails> purchaseOrderDetails(
    String token,
    String id,
  ) async => PurchaseOrderDetails(
    id: id,
    orderNumber: purchaseOrder.orderNumber,
    orderDate: purchaseOrder.orderDate,
    supplierId: purchaseOrder.supplierId,
    supplierName: purchaseOrder.supplierName,
    branchId: purchaseOrder.branchId,
    branchName: purchaseOrder.branchName,
    itemCount: 1,
    orderedQuantity: 100,
    receivedQuantity: 60,
    status: 'PartiallyReceived',
    items: const [
      PurchaseOrderItem(
        id: 'po-item-1',
        productId: 'product-1',
        productName: 'Panadol Extra',
        sku: 'MED-001',
        orderedQuantity: 100,
        receivedQuantity: 60,
        remainingQuantity: 40,
        expectedPurchasePrice: 50,
      ),
    ],
  );
  @override
  Future<PurchaseOrderDetails> createPurchaseOrder(
    String token,
    Map<String, dynamic> values,
  ) async => PurchaseOrderDetails(
    id: 'po-new',
    orderNumber: 'PO-2026-000002',
    orderDate: DateTime(2026, 9, 2),
    supplierId: 'supplier-1',
    supplierName: 'ABC Pharma',
    branchId: user.branch.id,
    branchName: user.branch.name,
    itemCount: 1,
    orderedQuantity: 100,
    receivedQuantity: 0,
    status: 'Draft',
    items: const [],
  );

  @override
  Future<PurchaseOrderDetails> submitPurchaseOrder(
    String token,
    String id,
  ) async => PurchaseOrderDetails(
    id: id,
    orderNumber: purchaseOrder.orderNumber,
    orderDate: purchaseOrder.orderDate,
    supplierId: purchaseOrder.supplierId,
    supplierName: purchaseOrder.supplierName,
    branchId: purchaseOrder.branchId,
    branchName: purchaseOrder.branchName,
    itemCount: purchaseOrder.itemCount,
    orderedQuantity: purchaseOrder.orderedQuantity,
    receivedQuantity: purchaseOrder.receivedQuantity,
    status: 'Submitted',
    items: const [],
  );

  @override
  Future<PurchaseOrderDetails> cancelPurchaseOrder(
    String token,
    String id,
  ) async => submitPurchaseOrder(token, id);

  @override
  Future<PagedPurchases> listPurchases(String token, {String? search}) async =>
      PagedPurchases(items: [purchase], totalCount: 1);

  @override
  Future<PurchaseDetails> purchaseDetails(String token, String id) async =>
      purchase;

  @override
  Future<PurchaseDetails> postGoodsReceipt(
    String token,
    Map<String, dynamic> values,
  ) async {
    lastGoodsReceiptBody = values;
    return purchase;
  }

  @override
  Future<PurchaseDetails> postDirectPurchase(
    String token,
    Map<String, dynamic> values,
  ) async {
    lastDirectPurchaseBody = values;
    if (purchaseError) {
      throw const ApiException(
        'This supplier invoice has already been recorded.',
      );
    }
    return purchase;
  }

  @override
  Future<ReturnablePurchase> returnablePurchase(
    String token,
    String receiptId,
  ) async => returnablePurchaseData;

  @override
  Future<PurchaseReturnDetails> postPurchaseReturn(
    String token,
    String receiptId,
    Map<String, dynamic> values,
  ) async {
    lastPurchaseReturnBody = values;
    return purchaseReturn;
  }

  @override
  Future<PagedPurchaseReturns> listPurchaseReturns(
    String token, {
    String? search,
  }) async => PagedPurchaseReturns(
    items: [
      PurchaseReturnListItem(
        id: 'purchase-return-1',
        returnNumber: 'PR-2026-000001',
        originalGrnNumber: 'GRN-2026-000001',
        supplierInvoiceNumber: 'INV-001',
        returnDateUtc: DateTime(2026, 9, 4, 10),
        supplierName: 'ABC Pharma',
        branchName: user.branch.name,
        paidQuantity: 3,
        bonusQuantity: 2,
        totalPhysicalQuantity: 5,
        netSupplierCredit: 150,
        status: 'Posted',
        reason: 'Damaged',
        processedByName: user.fullName,
      ),
    ],
    totalCount: 1,
  );

  @override
  Future<PurchaseReturnDetails> purchaseReturnDetails(
    String token,
    String id,
  ) async => purchaseReturn;

  @override
  Future<PurchaseReturnDetails> purchaseReturnNote(
    String token,
    String id,
  ) async => purchaseReturn;

  @override
  Future<PurchaseReturnDetails> reprintPurchaseReturnNote(
    String token,
    String id,
  ) async => purchaseReturn;

  @override
  Future<CurrentUser> updateProfile(
    String token, {
    required String fullName,
    String? email,
    String? phoneNumber,
  }) async => user;
  ReturnableSale get returnableSaleData => ReturnableSale(
    saleId: 'sale-1',
    invoiceNumber: 'INV-2026-000001',
    postedAtUtc: DateTime(2026, 9, 3, 10),
    branchName: user.branch.name,
    cashierName: user.fullName,
    customerName: 'Walk-in',
    customerId: null,
    customerCode: null,
    netTotal: 12,
    amountPaid: 12,
    creditAmount: 0,
    returnState: 'NotReturned',
    originalPayments: const [
      SalePaymentDetail(method: 'Cash', amountApplied: 12),
    ],
    items: [
      ReturnableSaleItem(
        saleItemId: 'item-1',
        productName: 'Panadol Extra',
        sku: 'MED-001',
        soldQuantity: 1,
        alreadyReturnedQuantity: 0,
        remainingQuantity: 1,
        originalNetAmount: 12,
        remainingRefundAmount: 12,
        allocations: [
          ReturnableAllocation(
            allocationId: 'allocation-1',
            productBatchId: 'batch-1',
            batchNumber: 'B-001',
            expiryDate: DateTime(2027, 9, 3),
            originalQuantity: 1,
            alreadyReturnedQuantity: 0,
            remainingQuantity: 1,
            unitSalePriceSnapshot: 12,
            refundRemaining: 12,
            isBatchDisposed: false,
            isBatchExpired: false,
          ),
        ],
      ),
    ],
  );

  SalesReturnDetails get salesReturn => SalesReturnDetails(
    id: 'return-1',
    returnNumber: 'RET-2026-000001',
    originalInvoiceNumber: 'INV-2026-000001',
    returnDateUtc: DateTime(2026, 9, 3, 11),
    branchName: user.branch.name,
    processedByName: user.fullName,
    reason: 'CustomerReturn',
    refundAmount: 12,
    customerCreditReductionAmount: 0,
    cashRefundAmount: 12,
    customerName: 'Walk-in',
    items: const [
      SalesReturnItemDetail(
        productName: 'Panadol Extra',
        sku: 'MED-001',
        quantity: 1,
        refundAmount: 12,
        allocations: [
          SalesReturnAllocationDetail(
            batchNumber: 'B-001',
            quantity: 1,
            disposition: 'Restockable',
            refundAmount: 12,
          ),
        ],
      ),
    ],
    refundPayments: const [
      SalesRefundPaymentDetail(method: 'Cash', amount: 12),
    ],
  );

  @override
  Future<ReturnableSale> returnableSale(String token, String saleId) async =>
      returnableSaleData;

  @override
  Future<SalesReturnDetails> postSalesReturn(
    String token,
    String saleId,
    Map<String, dynamic> values,
  ) async => salesReturn;

  @override
  Future<PagedSalesReturns> listSalesReturns(
    String token, {
    String? search,
  }) async => PagedSalesReturns(
    items: [
      SalesReturnListItem(
        id: 'return-1',
        returnNumber: 'RET-2026-000001',
        originalInvoiceNumber: 'INV-2026-000001',
        returnDateUtc: DateTime(2026, 9, 3, 11),
        branchName: user.branch.name,
        processedByName: user.fullName,
        customerName: 'Walk-in',
        itemCount: 1,
        refundAmount: 12,
        status: 'Posted',
        reason: 'CustomerReturn',
      ),
    ],
    totalCount: 1,
  );

  @override
  Future<SalesReturnDetails> salesReturnDetails(
    String token,
    String id,
  ) async => salesReturn;

  @override
  Future<SalesReturnDetails> salesReturnReceipt(
    String token,
    String id,
  ) async => salesReturn;

  @override
  Future<SalesReturnDetails> reprintSalesReturnReceipt(
    String token,
    String id,
  ) async => salesReturn;

  @override
  Future<List<FinancialAccountInfo>> listFinancialAccounts(
    String token, {
    String? branchId,
  }) async => [
    const FinancialAccountInfo(
      id: 'account-1',
      branchId: 'branch-1',
      branchName: 'Head Office',
      name: 'Cash Counter',
      accountType: 'Cash',
      openingBalance: 10000,
      currentBalance: 8000,
      isActive: true,
    ),
  ];

  @override
  Future<FinancialAccountInfo> createFinancialAccount(
    String token,
    Map<String, dynamic> values,
  ) async => (await listFinancialAccounts(token)).first;

  @override
  Future<List<ExpenseCategoryInfo>> listExpenseCategories(String token) async =>
      const [
        ExpenseCategoryInfo(id: 'category-1', name: 'Rent', isActive: true),
      ];

  @override
  Future<List<ExpenseInfo>> listExpenses(String token) async => [
    ExpenseInfo(
      id: 'expense-1',
      expenseNumber: 'EXP-2026-000001',
      branchName: 'Head Office',
      categoryName: 'Rent',
      accountName: 'Cash Counter',
      expenseDateUtc: DateTime(2026, 9, 7),
      amount: 2000,
      description: 'Shop rent',
      createdByName: 'Test User',
    ),
  ];

  @override
  Future<ExpenseInfo> postExpense(
    String token,
    Map<String, dynamic> values,
  ) async => (await listExpenses(token)).first;

  @override
  Future<void> reverseExpense(String token, String id, String reason) async {}

  @override
  Future<void> postOtherIncome(
    String token,
    Map<String, dynamic> values,
  ) async {}

  @override
  Future<void> reverseOtherIncome(
    String token,
    String id,
    String reason,
  ) async {}

  @override
  Future<void> postFinancialTransfer(
    String token,
    Map<String, dynamic> values,
  ) async {}

  @override
  Future<List<FinancialLedgerItem>> financialLedger(
    String token,
    String accountId,
  ) async => [
    FinancialLedgerItem(
      id: 'ledger-1',
      occurredAtUtc: DateTime(2026, 9, 7),
      entryType: 'OpeningBalance',
      description: 'Opening balance',
      amount: 10000,
      runningBalance: 10000,
      createdByName: 'Test User',
    ),
  ];

  @override
  Future<DailyCashPosition> dailyCashPosition(
    String token,
    String branchId,
    DateTime date, {
    String? accountId,
  }) async => const DailyCashPosition(
    openingBalance: 10000,
    moneyIn: 1000,
    moneyOut: 3000,
    closingBalance: 8000,
  );

  @override
  Future<dynamic> report(
    String token,
    String path, {
    required DateTime fromUtc,
    required DateTime toUtc,
    String? branchId,
    String? option,
    Map<String, String>? filters,
  }) async {
    if (reportDelay != Duration.zero) await Future<void>.delayed(reportDelay);
    if (reportError) throw const ApiException('Internal report failure');
    return path == 'overview'
        ? <String, dynamic>{
            'netSales': 900,
            'grossProfit': 300,
            'inventoryValue': 5000,
            'lowStockCount': 2,
            'nearExpiryCount': 1,
            'customerOutstanding': 1000,
            'supplierOutstanding': 2000,
            'expenses': 100,
            'cashPosition': 8000,
            'topProducts': <dynamic>[],
          }
        : <dynamic>[];
  }

  @override
  Future<List<int>> exportReport(
    String token,
    String path, {
    required DateTime fromUtc,
    required DateTime toUtc,
    String? branchId,
    String? option,
    Map<String, String>? filters,
  }) async => <int>[65, 44, 66, 10];

  @override
  Future<dynamic> administration(
    String token,
    String path, {
    String method = 'GET',
    Map<String, dynamic>? body,
  }) async {
    if (path == 'audit') {
      return <String, dynamic>{'items': <dynamic>[], 'totalCount': 0};
    }
    if (path == 'recycle-bin' || path == 'branches' || path == 'backups') {
      return <dynamic>[];
    }
    if (path == 'settings') {
      return <String, dynamic>{
        'businessName': 'Pharmacy',
        'timeZone': 'Asia/Karachi',
        'currency': 'PKR',
        'version': 0,
        'showCustomerPhone': false,
      };
    }
    return <String, dynamic>{
      'status': 'healthy',
      'databaseProvider': 'PostgreSQL',
      'databaseVersion': '17',
      'latestMigration': 'CompleteSystemAdministration',
      'canConnect': true,
      'timeZone': 'Asia/Karachi',
      'currency': 'PKR',
    };
  }

  Map<String, dynamic> get cashAccount => <String, dynamic>{
    'id': 'account-cash',
    'code': '1010',
    'name': 'Cash',
    'parentAccountId': null,
    'parentAccountName': null,
    'accountType': 'Asset',
    'normalBalance': 'Debit',
    'isPostingAccount': true,
    'isActive': true,
    'description': null,
  };

  Map<String, dynamic> get salesAccount => <String, dynamic>{
    'id': 'account-sales',
    'code': '4010',
    'name': 'Sales Revenue',
    'parentAccountId': null,
    'parentAccountName': null,
    'accountType': 'Income',
    'normalBalance': 'Credit',
    'isPostingAccount': true,
    'isActive': true,
    'description': null,
  };

  Map<String, dynamic> get journalLineOne => <String, dynamic>{
    'id': 'line-1',
    'chartOfAccountId': 'account-cash',
    'accountCode': '1010',
    'accountName': 'Cash',
    'debit': 500,
    'credit': 0,
    'branchId': user.branch.id,
    'customerName': null,
    'supplierName': null,
    'description': 'Cash received',
  };

  Map<String, dynamic> get journalLineTwo => <String, dynamic>{
    'id': 'line-2',
    'chartOfAccountId': 'account-sales',
    'accountCode': '4010',
    'accountName': 'Sales Revenue',
    'debit': 0,
    'credit': 500,
    'branchId': user.branch.id,
    'customerName': null,
    'supplierName': null,
    'description': 'Sale',
  };

  Map<String, dynamic> journalListItem({
    String id = 'journal-1',
    String entryNumber = 'JE-2026-000001',
  }) => <String, dynamic>{
    'id': id,
    'entryNumber': entryNumber,
    'entryDateUtc': '2026-09-01T00:00:00.000Z',
    'sourceType': 'Sale',
    'reference': 'INV-2026-000001',
    'description': 'Sale posting',
    'branchId': user.branch.id,
    'branchName': user.branch.name,
    'postedBy': user.fullName,
    'totalDebit': 500,
  };

  Map<String, dynamic> journalDetails({
    String id = 'journal-1',
    String entryNumber = 'JE-2026-000001',
  }) => <String, dynamic>{
    'id': id,
    'entryNumber': entryNumber,
    'entryDateUtc': '2026-09-01T00:00:00.000Z',
    'sourceType': 'Sale',
    'sourceId': null,
    'reference': 'INV-2026-000001',
    'description': 'Sale posting',
    'branchId': user.branch.id,
    'branchName': user.branch.name,
    'postedBy': user.fullName,
    'postedAtUtc': '2026-09-01T00:05:00.000Z',
    'status': 'Posted',
    'totalDebit': 500,
    'totalCredit': 500,
    'lines': [journalLineOne, journalLineTwo],
  };

  final List<_FakeTransfer> _transfers = [];
  int _transferSeq = 0;
  Map<String, dynamic>? lastStockTransferBody;
  Map<String, dynamic>? lastStockTransferUpdateBody;

  @override
  Future<dynamic> stockTransfers(
    String token,
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  }) async {
    if (path.isEmpty && method == 'GET') {
      var items = _transfers.toList();
      final status = query?['status'];
      if (status != null && status.isNotEmpty) {
        items = items.where((t) => t.status == status).toList();
      }
      final transferNumber = query?['transferNumber'];
      if (transferNumber != null && transferNumber.isNotEmpty) {
        items = items
            .where((t) => t.transferNumber.contains(transferNumber))
            .toList();
      }
      return <String, dynamic>{
        'items': items.map(_transferListJson).toList(),
        'page': 1,
        'pageSize': 100,
        'totalCount': items.length,
      };
    }
    if (path.isEmpty && method == 'POST') {
      lastStockTransferBody = body;
      _transferSeq++;
      final requestItems = (body!['items'] as List<dynamic>).map((raw) {
        final m = raw as Map<String, dynamic>;
        final batch = _fakeTransferableBatches.firstWhere(
          (b) => b.productBatchId == m['productBatchId'],
        );
        return _FakeTransferItem(
          id: 'transfer-item-$_transferSeq-${m['productBatchId']}',
          productId: batch.productId,
          productName: batch.productName,
          sku: batch.sku,
          sourceProductBatchId: batch.productBatchId,
          batchNumber: batch.batchNumber,
          expiryDate: batch.expiryDate,
          unitCost: batch.purchasePrice,
          requested: m['quantityRequested'] as int,
        );
      }).toList();
      String godownName(String id) =>
          _godowns.firstWhere((g) => g.id == id).name;
      final transfer = _FakeTransfer(
        id: 'transfer-$_transferSeq',
        transferNumber: 'TRF-2026-${_transferSeq.toString().padLeft(6, '0')}',
        sourceBranchId: body['sourceBranchId'] as String,
        sourceBranchName: user.branch.name,
        sourceGodownId: body['sourceGodownId'] as String,
        sourceGodownName: godownName(body['sourceGodownId'] as String),
        destBranchId: body['destinationBranchId'] as String,
        destBranchName: user.branch.name,
        destGodownId: body['destinationGodownId'] as String,
        destGodownName: godownName(body['destinationGodownId'] as String),
        transferDate:
            DateTime.tryParse(body['transferDate'] as String? ?? '') ??
            DateTime.now(),
        notes: body['notes'] as String?,
      )..createdBy = user.fullName;
      transfer.items.addAll(requestItems);
      _transfers.add(transfer);
      return _transferDetailJson(transfer);
    }
    if (path == 'transferable-batches') {
      return _fakeTransferableBatches
          .map(
            (b) => <String, dynamic>{
              'productBatchId': b.productBatchId,
              'productId': b.productId,
              'productName': b.productName,
              'sku': b.sku,
              'batchNumber': b.batchNumber,
              'expiryDate': b.expiryDate.toIso8601String().substring(0, 10),
              'quantityAvailable': b.quantityAvailable,
              'purchasePrice': b.purchasePrice,
              'retailPrice': b.retailPrice,
            },
          )
          .toList();
    }
    final match = RegExp(r'^([^/]+)(?:/(.+))?$').firstMatch(path);
    final id = match?.group(1);
    final action = match?.group(2);
    final transfer = _transfers.firstWhere(
      (t) => t.id == id,
      orElse: () => throw const ApiException('Stock transfer was not found.'),
    );
    if (action == null && method == 'GET') return _transferDetailJson(transfer);
    if (action == null && method == 'PUT') {
      lastStockTransferUpdateBody = body;
      final requestItems = (body!['items'] as List<dynamic>).map((raw) {
        final m = raw as Map<String, dynamic>;
        final batch = _fakeTransferableBatches.firstWhere(
          (b) => b.productBatchId == m['productBatchId'],
        );
        return _FakeTransferItem(
          id: 'transfer-item-${transfer.id}-${m['productBatchId']}',
          productId: batch.productId,
          productName: batch.productName,
          sku: batch.sku,
          sourceProductBatchId: batch.productBatchId,
          batchNumber: batch.batchNumber,
          expiryDate: batch.expiryDate,
          unitCost: batch.purchasePrice,
          requested: m['quantityRequested'] as int,
        );
      }).toList();
      String godownName(String id) =>
          _godowns.firstWhere((g) => g.id == id).name;
      transfer.sourceBranchId = body['sourceBranchId'] as String;
      transfer.sourceBranchName = user.branch.name;
      transfer.sourceGodownId = body['sourceGodownId'] as String;
      transfer.sourceGodownName = godownName(body['sourceGodownId'] as String);
      transfer.destBranchId = body['destinationBranchId'] as String;
      transfer.destBranchName = user.branch.name;
      transfer.destGodownId = body['destinationGodownId'] as String;
      transfer.destGodownName = godownName(
        body['destinationGodownId'] as String,
      );
      transfer.transferDate =
          DateTime.tryParse(body['transferDate'] as String? ?? '') ??
          transfer.transferDate;
      transfer.notes = body['notes'] as String?;
      transfer.items
        ..clear()
        ..addAll(requestItems);
      return _transferDetailJson(transfer);
    }
    if (action == 'request') {
      transfer.status = 'Requested';
      transfer.requestedBy = user.fullName;
      transfer.requestedAt = DateTime.now();
      return _transferDetailJson(transfer);
    }
    if (action == 'approve') {
      final overrides = <String, int>{
        for (final raw in (body?['items'] as List<dynamic>? ?? <dynamic>[]))
          (raw as Map<String, dynamic>)['stockTransferItemId'] as String:
              raw['quantityApproved'] as int,
      };
      for (final i in transfer.items) {
        i.approved = overrides[i.id] ?? i.requested;
      }
      transfer.status = 'Approved';
      transfer.approvedBy = user.fullName;
      transfer.approvedAt = DateTime.now();
      return _transferDetailJson(transfer);
    }
    if (action == 'dispatch') {
      final overrides = <String, int>{
        for (final raw in (body?['items'] as List<dynamic>? ?? <dynamic>[]))
          (raw as Map<String, dynamic>)['stockTransferItemId'] as String:
              raw['quantityDispatched'] as int,
      };
      for (final i in transfer.items) {
        if (i.approved > 0) i.dispatched = overrides[i.id] ?? i.approved;
      }
      transfer.status = 'Dispatched';
      transfer.dispatchedBy = user.fullName;
      transfer.dispatchedAt = DateTime.now();
      return _transferDetailJson(transfer);
    }
    if (action == 'receive') {
      for (final raw in (body?['items'] as List<dynamic>? ?? <dynamic>[])) {
        final m = raw as Map<String, dynamic>;
        final i = transfer.items.firstWhere(
          (x) => x.id == m['stockTransferItemId'],
        );
        i.received += m['quantityReceived'] as int;
        i.destinationProductBatchId ??= 'dest-${i.sourceProductBatchId}';
        final itemNote = m['notes'] as String?;
        if (itemNote != null && itemNote.trim().isNotEmpty) {
          i.notes = i.notes == null || i.notes!.isEmpty
              ? itemNote.trim()
              : '${i.notes}\n${itemNote.trim()}';
        }
      }
      final headerNote = body?['notes'] as String?;
      if (headerNote != null && headerNote.trim().isNotEmpty) {
        transfer.notes = transfer.notes == null || transfer.notes!.isEmpty
            ? headerNote.trim()
            : '${transfer.notes}\n${headerNote.trim()}';
      }
      final totalDispatched = transfer.items.fold<int>(
        0,
        (a, i) => a + i.dispatched,
      );
      final totalReceived = transfer.items.fold<int>(
        0,
        (a, i) => a + i.received,
      );
      transfer.status = totalReceived >= totalDispatched
          ? 'Received'
          : 'PartiallyReceived';
      transfer.receivedBy = user.fullName;
      transfer.receivedAt = DateTime.now();
      return _transferDetailJson(transfer);
    }
    if (action == 'cancel') {
      transfer.status = 'Cancelled';
      transfer.cancelledBy = user.fullName;
      transfer.cancelledAt = DateTime.now();
      transfer.cancellationReason = body?['reason'] as String?;
      return _transferDetailJson(transfer);
    }
    if (action == 'resolve-discrepancy') {
      transfer.status = 'Received';
      transfer.receivedBy = user.fullName;
      transfer.receivedAt = DateTime.now();
      return _transferDetailJson(transfer);
    }
    return _transferDetailJson(transfer);
  }

  final List<Map<String, dynamic>> _priceLevels = [];
  int _priceLevelSeq = 0;
  Map<String, dynamic>? lastPriceLevelBody;

  @override
  Future<dynamic> pricing(
    String token,
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  }) async {
    if (path.isEmpty && method == 'GET') {
      return _priceLevels.map((x) => Map<String, dynamic>.from(x)).toList();
    }
    if (path.isEmpty && method == 'POST') {
      _priceLevelSeq++;
      lastPriceLevelBody = body;
      final level = <String, dynamic>{
        'id': 'price-level-$_priceLevelSeq',
        'name': body!['name'],
        'code': body['code'],
        'priority': body['priority'] ?? 0,
        'isDefault': body['isDefault'] ?? false,
        'isActive': body['isActive'] ?? true,
        'branchId': null,
        'branchName': null,
      };
      _priceLevels.add(level);
      return level;
    }
    if (path == 'product-prices') {
      if (method == 'GET') return <Map<String, dynamic>>[];
      return <String, dynamic>{
        'id': 'product-price-1',
        'productId': body?['productId'],
        'productName': product.name,
        'sku': product.sku,
        'priceLevelId': body?['priceLevelId'],
        'priceLevelName': 'Wholesale',
        'sellingPrice': body?['sellingPrice'] ?? 0,
        'isActive': true,
      };
    }
    if (path == 'product-breaks') {
      if (method == 'GET') return <Map<String, dynamic>>[];
      return <String, dynamic>{
        'id': 'product-break-1',
        'productId': body?['productId'],
        'productName': product.name,
        'sku': product.sku,
        'priceLevelId': body?['priceLevelId'],
        'priceLevelName': null,
        'minimumQuantity': body?['minimumQuantity'] ?? 0,
        'sellingPrice': body?['sellingPrice'] ?? 0,
        'isActive': true,
      };
    }
    if (path == 'resolve') {
      return <String, dynamic>{
        'price': null,
        'source': 'Default',
        'priceLevelId': null,
        'fallbackPrice': product.retailPrice,
      };
    }
    final index = _priceLevels.indexWhere((x) => x['id'] == path);
    if (index != -1 && method == 'PUT') {
      _priceLevels[index] = {..._priceLevels[index], ...?body};
      return _priceLevels[index];
    }
    return null;
  }

  @override
  Future<dynamic> phase6(
    String token,
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  }) async => path == 'sale-price'
      ? <String, dynamic>{
          'price': null,
          'source': 'Default',
          'priceLevelId': null,
          'defaultRetailPrice': product.retailPrice,
        }
      : <dynamic>[];

  final List<Map<String, dynamic>> _quotations = [];
  int _quotationSeq = 0;
  Map<String, dynamic>? lastQuotationBody;

  Map<String, dynamic> _quotationJson(Map<String, dynamic> q) => {
    ...q,
    'items': (q['items'] as List<dynamic>).map((raw) {
      final m = raw as Map<String, dynamic>;
      final qty = m['quantity'] as int;
      final unitPrice = product.retailPrice;
      final gross = unitPrice * qty;
      return {
        'id': 'quotation-item-${m['productId']}',
        'productId': m['productId'],
        'productName': product.name,
        'sku': product.sku,
        'quantity': qty,
        'unitPrice': unitPrice,
        'discountPercent': m['discountPercent'] ?? 0,
        'grossAmount': gross,
        'discountAmount': 0,
        'netAmount': gross,
      };
    }).toList(),
  };

  @override
  Future<dynamic> salesQuotations(
    String token,
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  }) async {
    if (path.isEmpty && method == 'GET') {
      return <String, dynamic>{
        'items': _quotations
            .map(
              (q) => {
                'id': q['id'],
                'quotationNumber': q['quotationNumber'],
                'customerId': q['customerId'],
                'customerName': customer.name,
                'quotationDate': q['quotationDate'],
                'validUntil': q['validUntil'],
                'status': q['status'],
                'netTotal': q['items'] == null
                    ? 0
                    : (q['items'] as List<dynamic>).fold<double>(
                        0,
                        (sum, i) =>
                            sum +
                            product.retailPrice *
                                ((i as Map<String, dynamic>)['quantity']
                                    as int),
                      ),
                'createdByName': user.fullName,
              },
            )
            .toList(),
        'page': 1,
        'pageSize': 50,
        'totalCount': _quotations.length,
      };
    }
    if (path.isEmpty && method == 'POST') {
      _quotationSeq++;
      lastQuotationBody = body;
      final quotation = <String, dynamic>{
        'id': 'quotation-$_quotationSeq',
        'quotationNumber':
            'QT-2026-${_quotationSeq.toString().padLeft(6, '0')}',
        'branchId': user.branch.id,
        'branchName': user.branch.name,
        'godownId': null,
        'godownName': null,
        'customerId': body!['customerId'],
        'customerCode': customer.customerCode,
        'customerName': customer.name,
        'priceLevelId': null,
        'priceLevelName': null,
        'quotationDate': body['quotationDate'],
        'validUntil': body['validUntil'],
        'status': 'Draft',
        'notes': body['notes'],
        'createdByName': user.fullName,
        'approvedByName': null,
        'convertedToSalesOrderId': null,
        'convertedToSalesOrderNumber': null,
        'convertedToSaleId': null,
        'convertedToSaleInvoiceNumber': null,
        'sentAtUtc': null,
        'respondedAtUtc': null,
        'cancelledAtUtc': null,
        'cancellationReason': null,
        'createdAt': DateTime.now().toIso8601String(),
        'items': body['items'],
      };
      _quotations.add(quotation);
      return _quotationJson(quotation);
    }
    final segments = path.split('/');
    final id = segments.first;
    final quotation = _quotations.firstWhere((q) => q['id'] == id);
    if (segments.length == 1 && method == 'GET') {
      return _quotationJson(
        Map<String, dynamic>.from(quotation)
          ..['subtotal'] = 0
          ..['discountTotal'] = 0
          ..['netTotal'] = (quotation['items'] as List<dynamic>).fold<double>(
            0,
            (sum, i) =>
                sum +
                product.retailPrice *
                    ((i as Map<String, dynamic>)['quantity'] as int),
          ),
      );
    }
    if (segments.length == 2 && segments[1] == 'send') {
      quotation['status'] = 'Sent';
    } else if (segments.length == 2 && segments[1] == 'accept') {
      quotation['status'] = 'Accepted';
    } else if (segments.length == 2 && segments[1] == 'reject') {
      quotation['status'] = 'Rejected';
    } else if (segments.length == 2 && segments[1] == 'cancel') {
      quotation['status'] = 'Cancelled';
      quotation['cancellationReason'] = body?['reason'];
    } else if (segments.length == 2 && segments[1] == 'convert-to-order') {
      quotation['status'] = 'Converted';
      _salesOrderSeq++;
      final order = <String, dynamic>{
        'id': 'sales-order-$_salesOrderSeq',
        'orderNumber': 'SO-2026-${_salesOrderSeq.toString().padLeft(6, '0')}',
        'branchId': user.branch.id,
        'branchName': user.branch.name,
        'godownId': null,
        'godownName': null,
        'customerId': quotation['customerId'],
        'customerCode': customer.customerCode,
        'customerName': customer.name,
        'priceLevelId': null,
        'priceLevelName': null,
        'quotationId': quotation['id'],
        'quotationNumber': quotation['quotationNumber'],
        'orderDate': DateTime.now().toIso8601String(),
        'expectedDeliveryDate': null,
        'status': 'Draft',
        'notes': null,
        'createdByName': user.fullName,
        'confirmedByName': null,
        'confirmedAtUtc': null,
        'cancelledAtUtc': null,
        'cancellationReason': null,
        'createdAt': DateTime.now().toIso8601String(),
        'items': quotation['items'],
        'linkedSales': <dynamic>[],
      };
      _salesOrders.add(order);
      quotation['convertedToSalesOrderId'] = order['id'];
      quotation['convertedToSalesOrderNumber'] = order['orderNumber'];
      return _salesOrderJson(order);
    } else if (segments.length == 2 && segments[1] == 'convert-to-sale') {
      quotation['status'] = 'Converted';
      quotation['convertedToSaleId'] = 'sale-from-quotation';
      quotation['convertedToSaleInvoiceNumber'] = 'INV-2026-000001';
      return <String, dynamic>{
        'id': 'sale-from-quotation',
        'invoiceNumber': 'INV-2026-000001',
      };
    }
    return _quotationJson(quotation);
  }

  final List<Map<String, dynamic>> _salesOrders = [];
  int _salesOrderSeq = 0;
  Map<String, dynamic>? lastSalesOrderBody;
  Map<String, dynamic>? lastFulfillSalesOrderBody;

  Map<String, dynamic> _salesOrderJson(Map<String, dynamic> o) => {
    ...o,
    'subtotal': 0,
    'discountTotal': 0,
    'netTotal': (o['items'] as List<dynamic>).fold<double>(
      0,
      (sum, i) =>
          sum +
          product.retailPrice *
              ((i as Map<String, dynamic>)['quantity'] as int),
    ),
    'items': (o['items'] as List<dynamic>).map((raw) {
      final m = raw as Map<String, dynamic>;
      final qty = m['quantity'] as int;
      return {
        'id': 'sales-order-item-${m['productId']}',
        'productId': m['productId'],
        'productName': product.name,
        'sku': product.sku,
        'orderedQuantity': qty,
        'fulfilledQuantity': m['fulfilledQuantity'] ?? 0,
        'unitPrice': product.retailPrice,
        'discountPercent': m['discountPercent'] ?? 0,
        'grossAmount': product.retailPrice * qty,
        'discountAmount': 0,
        'netAmount': product.retailPrice * qty,
      };
    }).toList(),
  };

  @override
  Future<dynamic> salesOrders(
    String token,
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  }) async {
    if (path.isEmpty && method == 'GET') {
      return <String, dynamic>{
        'items': _salesOrders
            .map(
              (o) => {
                'id': o['id'],
                'orderNumber': o['orderNumber'],
                'customerId': o['customerId'],
                'customerName': customer.name,
                'orderDate': o['orderDate'],
                'status': o['status'],
                'netTotal': (o['items'] as List<dynamic>).fold<double>(
                  0,
                  (sum, i) =>
                      sum +
                      product.retailPrice *
                          ((i as Map<String, dynamic>)['quantity'] as int),
                ),
                'orderedQuantity': (o['items'] as List<dynamic>).fold<int>(
                  0,
                  (sum, i) =>
                      sum + ((i as Map<String, dynamic>)['quantity'] as int),
                ),
                'fulfilledQuantity': 0,
              },
            )
            .toList(),
        'page': 1,
        'pageSize': 50,
        'totalCount': _salesOrders.length,
      };
    }
    if (path.isEmpty && method == 'POST') {
      _salesOrderSeq++;
      lastSalesOrderBody = body;
      final order = <String, dynamic>{
        'id': 'sales-order-$_salesOrderSeq',
        'orderNumber': 'SO-2026-${_salesOrderSeq.toString().padLeft(6, '0')}',
        'branchId': user.branch.id,
        'branchName': user.branch.name,
        'godownId': body!['godownId'],
        'godownName': null,
        'customerId': body['customerId'],
        'customerCode': customer.customerCode,
        'customerName': customer.name,
        'priceLevelId': null,
        'priceLevelName': null,
        'quotationId': null,
        'quotationNumber': null,
        'orderDate': body['orderDate'],
        'expectedDeliveryDate': body['expectedDeliveryDate'],
        'status': 'Draft',
        'notes': body['notes'],
        'createdByName': user.fullName,
        'confirmedByName': null,
        'confirmedAtUtc': null,
        'cancelledAtUtc': null,
        'cancellationReason': null,
        'createdAt': DateTime.now().toIso8601String(),
        'items': body['items'],
        'linkedSales': <dynamic>[],
      };
      _salesOrders.add(order);
      return _salesOrderJson(order);
    }
    final segments = path.split('/');
    final id = segments.first;
    final order = _salesOrders.firstWhere((o) => o['id'] == id);
    if (segments.length == 1 && method == 'GET') {
      return _salesOrderJson(order);
    }
    if (segments.length == 2 && segments[1] == 'confirm') {
      order['status'] = 'Confirmed';
      order['confirmedByName'] = user.fullName;
      order['confirmedAtUtc'] = DateTime.now().toIso8601String();
    } else if (segments.length == 2 && segments[1] == 'cancel') {
      order['status'] = 'Cancelled';
      order['cancellationReason'] = body?['reason'];
    } else if (segments.length == 2 && segments[1] == 'fulfill') {
      lastFulfillSalesOrderBody = body;
      final items = (order['items'] as List<dynamic>)
          .cast<Map<String, dynamic>>();
      for (final requested in (body?['items'] as List<dynamic>? ?? [])) {
        final requestedMap = requested as Map<String, dynamic>;
        final match = items.firstWhere(
          (x) => x['productId'] == requestedMap['productId'],
        );
        match['fulfilledQuantity'] =
            ((match['fulfilledQuantity'] as int?) ?? 0) +
            (requestedMap['quantity'] as int);
      }
      final fullyFulfilled = items.every(
        (x) =>
            ((x['fulfilledQuantity'] as int?) ?? 0) >= (x['quantity'] as int),
      );
      order['status'] = fullyFulfilled ? 'Fulfilled' : 'PartiallyFulfilled';
      return <String, dynamic>{
        'id': 'sale-from-order',
        'invoiceNumber': 'INV-2026-000002',
      };
    }
    return _salesOrderJson(order);
  }

  @override
  Future<dynamic> accounting(
    String token,
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  }) async {
    if (path == 'chart' && method == 'GET') {
      return <Map<String, dynamic>>[cashAccount, salesAccount];
    }
    if (path == 'chart' && method == 'POST') {
      return <String, dynamic>{
        ...cashAccount,
        'id': 'account-new',
        'balance': 0,
      };
    }
    if (path.startsWith('chart/') && path.endsWith('/activate')) return null;
    if (path.startsWith('chart/') && path.endsWith('/deactivate')) return null;
    if (path.startsWith('chart/') && method == 'PUT') {
      return <String, dynamic>{...cashAccount, 'balance': 0};
    }
    if (path == 'mappings') {
      return <Map<String, dynamic>>[
        <String, dynamic>{
          'mappingKey': 'Cash',
          'chartOfAccountId': 'account-cash',
          'chartOfAccountCode': '1010',
          'chartOfAccountName': 'Cash',
        },
      ];
    }
    if (path == 'journal' && method == 'GET') {
      return <String, dynamic>{
        'items': [journalListItem()],
        'page': 1,
        'pageSize': 25,
        'totalCount': 1,
      };
    }
    if (path == 'journal' && method == 'POST') {
      return journalDetails(
        id: 'journal-manual',
        entryNumber: 'JE-2026-000002',
      );
    }
    if (path.startsWith('journal/')) return journalDetails();
    if (path == 'trial-balance') {
      return <String, dynamic>{
        'asOfUtc': '2026-09-10T00:00:00.000Z',
        'rows': [
          <String, dynamic>{
            'chartOfAccountId': 'account-cash',
            'accountCode': '1010',
            'accountName': 'Cash',
            'accountType': 'Asset',
            'normalBalance': 'Debit',
            'debit': 500,
            'credit': 0,
          },
          <String, dynamic>{
            'chartOfAccountId': 'account-sales',
            'accountCode': '4010',
            'accountName': 'Sales Revenue',
            'accountType': 'Income',
            'normalBalance': 'Credit',
            'debit': 0,
            'credit': 500,
          },
        ],
        'totalDebit': 500,
        'totalCredit': 500,
      };
    }
    if (path == 'general-ledger') {
      return <String, dynamic>{
        'chartOfAccountId': 'account-cash',
        'accountCode': '1010',
        'accountName': 'Cash',
        'normalBalance': 'Debit',
        'openingBalance': 0,
        'totalDebit': 500,
        'totalCredit': 0,
        'closingBalance': 500,
        'entries': <String, dynamic>{
          'items': [
            <String, dynamic>{
              'journalEntryId': 'journal-1',
              'entryNumber': 'JE-2026-000001',
              'entryDateUtc': '2026-09-01T00:00:00.000Z',
              'sourceType': 'Sale',
              'reference': 'INV-2026-000001',
              'description': 'Cash received',
              'debit': 500,
              'credit': 0,
              'runningBalance': 500,
            },
          ],
          'page': 1,
          'pageSize': 50,
          'totalCount': 1,
        },
      };
    }
    if (path == 'profit-loss') {
      return <String, dynamic>{
        'fromUtc': '2026-09-01T00:00:00.000Z',
        'toUtc': '2026-09-10T00:00:00.000Z',
        'revenue': [
          <String, dynamic>{
            'accountCode': '4010',
            'accountName': 'Sales Revenue',
            'amount': 500,
          },
        ],
        'netRevenue': 500,
        'costOfGoodsSold': <dynamic>[],
        'totalCostOfGoodsSold': 0,
        'grossProfit': 500,
        'operatingExpenses': <dynamic>[],
        'totalOperatingExpenses': 0,
        'netProfit': 500,
      };
    }
    if (path == 'balance-sheet') {
      return <String, dynamic>{
        'asOfUtc': '2026-09-10T00:00:00.000Z',
        'assets': [
          <String, dynamic>{
            'accountCode': '1010',
            'accountName': 'Cash',
            'amount': 500,
          },
        ],
        'totalAssets': 500,
        'liabilities': <dynamic>[],
        'totalLiabilities': 0,
        'equity': <dynamic>[],
        'accountEquity': 0,
        'currentPeriodEarnings': 500,
        'totalEquity': 500,
        'isBalanced': true,
      };
    }
    return null;
  }

  @override
  void close() {}
}
