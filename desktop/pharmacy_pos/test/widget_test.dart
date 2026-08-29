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
