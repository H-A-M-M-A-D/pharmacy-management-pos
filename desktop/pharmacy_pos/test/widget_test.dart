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
    await tester.enterText(
      find.byKey(const Key('save_adjustment_quantity')),
      '99',
    );
    await tester.enterText(find.byType(TextFormField).last, 'count');
    await tester.tap(find.byKey(const Key('save_adjustment')));
    await tester.pumpAndSettle();
    expect(find.text('Insufficient stock in selected batch.'), findsOneWidget);
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

class TestFixture {
  TestFixture({
    this.loginError = false,
    this.mustChangePassword = false,
    this.permissions = const {},
    this.adjustmentError = false,
    this.supplierError = false,
    this.purchaseError = false,
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
    );
    state = AuthState(api, MemoryTokenStore());
  }

  final bool loginError;
  final bool mustChangePassword;
  final Set<String> permissions;
  final bool adjustmentError;
  final bool supplierError;
  final bool purchaseError;
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
  });

  CurrentUser user;
  final bool loginError;
  final bool adjustmentError;
  final bool supplierError;
  final bool purchaseError;

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
  Future<PagedSupplierLedger> supplierLedger(String token, String id) async =>
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
    netTotal: 12,
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
  void close() {}
}
