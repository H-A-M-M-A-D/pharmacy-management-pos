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
    );
    state = AuthState(api, MemoryTokenStore());
  }

  final bool loginError;
  final bool mustChangePassword;
  final Set<String> permissions;
  final bool adjustmentError;
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
  });

  CurrentUser user;
  final bool loginError;
  final bool adjustmentError;

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
    suppliers: const [],
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

  @override
  Future<CurrentUser> updateProfile(
    String token, {
    required String fullName,
    String? email,
    String? phoneNumber,
  }) async => user;

  @override
  void close() {}
}
