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
    await tester.tap(find.text('B-001 (qty 10)').last);
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
    await tester.tap(find.text('B-001 (qty 10)').last);
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

    await tester.ensureVisible(find.byKey(const Key('open_stock_count_session-1')));
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
    await tester.enterText(find.byKey(const Key('drawer_entry_reason')), 'float top-up');
    await tester.tap(find.byKey(const Key('save_drawer_entry')));
    await tester.pumpAndSettle();
    expect(find.textContaining('float top-up'), findsOneWidget);

    await tester.ensureVisible(find.byKey(const Key('close_cashier_shift')));
    await tester.tap(find.byKey(const Key('close_cashier_shift')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('close_shift_actual_cash')), '100');
    await tester.tap(find.byKey(const Key('save_close_shift')));
    await tester.pumpAndSettle();

    // A closed shift is no longer "my open shift" (matches the real backend's
    // GetOpenShiftForCashierAsync, which only ever returns open shifts), so
    // the My Shift tab correctly drops back to its empty state here.
    // Reconciliation happens from Shift History > View, per the production flow.
    expect(find.text('You do not have an open cashier shift.'), findsOneWidget);

    await tester.tap(find.text('Shift History'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.byKey(const Key('open_cashier_shift_shift-1')));
    await tester.tap(find.byKey(const Key('open_cashier_shift_shift-1')));
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.byKey(const Key('reconcile_cashier_shift')));
    await tester.tap(find.byKey(const Key('reconcile_cashier_shift')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_reconcile_shift')));
    await tester.pumpAndSettle();
    expect(find.widgetWithText(Chip, 'Reconciled'), findsOneWidget);
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
      find.text('Posted journals are permanent and read-only.'),
      findsOneWidget,
    );
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
    await tester.enterText(
      find.byKey(const Key('manual_line_debit_0')),
      '100',
    );

    await tester.tap(find.byKey(const Key('manual_line_account_1')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('4010 · Sales Revenue').last);
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('manual_line_credit_1')),
      '50',
    );
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

class FakeApi implements PharmacyApi {
  FakeApi({
    required this.user,
    required this.loginError,
    this.adjustmentError = false,
    this.supplierError = false,
    this.purchaseError = false,
    this.reportError = false,
    this.reportDelay = Duration.zero,
  }) {
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
  }) async =>
      PagedCustomerLedger(
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
  }) async => [posProduct];

  @override
  Future<SaleDetails> holdSale(
    String token,
    Map<String, dynamic> values,
  ) async => SaleDetails(
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

  @override
  Future<SaleDetails> postSale(
    String token,
    Map<String, dynamic> values,
  ) async => sale;

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
  Future<List<ExpiryItem>> listExpiry(String token, {int? days}) async => [
    ExpiryItem(
      batchId: 'batch-1',
      productName: 'Panadol Extra',
      batchNumber: 'B-001',
      expiryDate: DateTime(2026, 10, 1),
      daysRemaining: 29,
      quantityAvailable: 10,
      estimatedStockValue: 80,
    ),
  ];

  @override
  Future<PagedBatches> listBatches(String token, {String? search}) async =>
      PagedBatches(items: [batch], totalCount: 1);

  @override
  Future<PagedMovements> listMovements(String token, {String? search}) async =>
      PagedMovements(
        items: [
          StockMovementItem(
            createdAt: DateTime(2026, 9, 1, 10),
            productName: 'Panadol Extra',
            batchNumber: 'B-001',
            branchName: user.branch.name,
            movementType: 'OpeningStock',
            quantity: 10,
          ),
        ],
        totalCount: 1,
      );

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
  ) async => _stockCountSession;

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
      manualCashOut: current.manualCashOut + (entryType == 'CashOut' ? amount : 0),
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
        CashierShiftPaymentSummary(paymentMethod: 'Cash', salesAmount: 100, refundsAmount: 0),
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
  Future<CashierShift> cashierShiftDetails(String token, String id) async => _cashierShift!;

  @override
  Future<PagedCashierShifts> listCashierShifts(String token, {String? status}) async =>
      PagedCashierShifts(
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
  }) async =>
      PagedSupplierLedger(
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
  ) async => purchase;

  @override
  Future<PurchaseDetails> postDirectPurchase(
    String token,
    Map<String, dynamic> values,
  ) async {
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
  Future<void> postOtherIncome(
    String token,
    Map<String, dynamic> values,
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
      return <String, dynamic>{...cashAccount, 'id': 'account-new', 'balance': 0};
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
      return journalDetails(id: 'journal-manual', entryNumber: 'JE-2026-000002');
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
          <String, dynamic>{'accountCode': '4010', 'accountName': 'Sales Revenue', 'amount': 500},
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
          <String, dynamic>{'accountCode': '1010', 'accountName': 'Cash', 'amount': 500},
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
