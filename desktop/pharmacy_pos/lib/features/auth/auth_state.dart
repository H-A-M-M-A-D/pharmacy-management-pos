import 'package:flutter/foundation.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../../core/token_store.dart';

enum AuthenticationStatus {
  initializing,
  unauthenticated,
  authenticating,
  authenticated,
}

class AuthState extends ChangeNotifier {
  AuthState(this._api, this._tokenStore);

  final PharmacyApi _api;
  final TokenStore _tokenStore;

  AuthenticationStatus status = AuthenticationStatus.initializing;
  CurrentUser? currentUser;
  String? errorMessage;
  String? _token;

  bool get mustChangePassword => currentUser?.mustChangePassword ?? false;
  bool can(String permission) => currentUser?.can(permission) ?? false;

  Future<void> initialize() async {
    final token = await _tokenStore.read();
    if (token == null) {
      status = AuthenticationStatus.unauthenticated;
      notifyListeners();
      return;
    }

    try {
      _token = token;
      currentUser = await _api.me(token);
      status = AuthenticationStatus.authenticated;
    } on ApiException {
      await _tokenStore.clear();
      _token = null;
      currentUser = null;
      status = AuthenticationStatus.unauthenticated;
    }
    notifyListeners();
  }

  Future<bool> login(String username, String password) async {
    status = AuthenticationStatus.authenticating;
    errorMessage = null;
    notifyListeners();
    try {
      final session = await _api.login(username, password);
      _token = session.accessToken;
      currentUser = session.user;
      await _tokenStore.write(session.accessToken);
      status = AuthenticationStatus.authenticated;
      notifyListeners();
      return true;
    } on ApiException catch (error) {
      _token = null;
      currentUser = null;
      status = AuthenticationStatus.unauthenticated;
      errorMessage = error.message;
      notifyListeners();
      return false;
    }
  }

  Future<bool> changePassword(
    String currentPassword,
    String newPassword,
  ) async {
    errorMessage = null;
    try {
      final session = await _api.changePassword(
        _requiredToken,
        currentPassword,
        newPassword,
      );
      _token = session.accessToken;
      currentUser = session.user;
      await _tokenStore.write(session.accessToken);
      notifyListeners();
      return true;
    } on ApiException catch (error) {
      errorMessage = error.message;
      notifyListeners();
      return false;
    }
  }

  Future<bool> updateProfile({
    required String fullName,
    String? email,
    String? phoneNumber,
  }) async {
    errorMessage = null;
    try {
      currentUser = await _api.updateProfile(
        _requiredToken,
        fullName: fullName,
        email: email,
        phoneNumber: phoneNumber,
      );
      notifyListeners();
      return true;
    } on ApiException catch (error) {
      errorMessage = error.message;
      notifyListeners();
      return false;
    }
  }

  Future<PagedUsers> listUsers({
    String? search,
    String? roleId,
    String? branchId,
    bool? isActive,
  }) => _api.listUsers(
    _requiredToken,
    search: search,
    roleId: roleId,
    branchId: branchId,
    isActive: isActive,
  );

  Future<UserOptions> userOptions() => _api.userOptions(_requiredToken);
  Future<UserDetails> userDetails(String id) =>
      _api.userDetails(_requiredToken, id);
  Future<UserDetails> createUser(Map<String, dynamic> values) =>
      _api.createUser(_requiredToken, values);
  Future<UserDetails> updateUser(String id, Map<String, dynamic> values) =>
      _api.updateUser(_requiredToken, id, values);
  Future<void> setUserActive(String id, bool active) =>
      _api.setUserActive(_requiredToken, id, active);
  Future<void> resetPassword(String id, String password) =>
      _api.resetPassword(_requiredToken, id, password);

  Future<void> logout() async {
    await _tokenStore.clear();
    _token = null;
    currentUser = null;
    errorMessage = null;
    status = AuthenticationStatus.unauthenticated;
    notifyListeners();
  }

  String get _requiredToken {
    final token = _token;
    if (token == null) throw const ApiException('Session is unavailable.');
    return token;
  }

  @override
  void dispose() {
    _api.close();
    super.dispose();
  }
}
