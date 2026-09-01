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
    );
    state = AuthState(api, MemoryTokenStore());
  }

  final bool loginError;
  final bool mustChangePassword;
  final Set<String> permissions;
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
  FakeApi({required this.user, required this.loginError});

  CurrentUser user;
  final bool loginError;

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
