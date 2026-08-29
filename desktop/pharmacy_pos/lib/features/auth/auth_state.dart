import 'package:flutter/foundation.dart';

import '../../core/api_client.dart';

enum AuthenticationStatus { unauthenticated, authenticating, authenticated }

class AuthState extends ChangeNotifier {
  AuthState(this._apiClient);

  final ApiClient _apiClient;

  AuthenticationStatus status = AuthenticationStatus.unauthenticated;
  LoginSession? session;
  String? errorMessage;

  Future<void> login(String username, String password) async {
    status = AuthenticationStatus.authenticating;
    errorMessage = null;
    notifyListeners();

    try {
      session = await _apiClient.login(username, password);
      status = AuthenticationStatus.authenticated;
    } on ApiException catch (error) {
      session = null;
      status = AuthenticationStatus.unauthenticated;
      errorMessage = error.message;
    }
    notifyListeners();
  }

  void logout() {
    session = null;
    errorMessage = null;
    status = AuthenticationStatus.unauthenticated;
    notifyListeners();
  }

  @override
  void dispose() {
    _apiClient.close();
    super.dispose();
  }
}
