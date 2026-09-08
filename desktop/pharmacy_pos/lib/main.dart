import 'package:flutter/material.dart';

import 'core/api_client.dart';
import 'core/token_store.dart';
import 'features/auth/auth_state.dart';
import 'features/auth/force_change_password_screen.dart';
import 'features/auth/login_screen.dart';
import 'features/shell/app_shell.dart';

void main() => runApp(const PharmacyPOSApp());

class PharmacyPOSApp extends StatefulWidget {
  const PharmacyPOSApp({super.key, this.authState});

  final AuthState? authState;

  @override
  State<PharmacyPOSApp> createState() => _PharmacyPOSAppState();
}

class _PharmacyPOSAppState extends State<PharmacyPOSApp> {
  late final ApiClient _apiClient = ApiClient(onUnauthorized: _expireSession);
  late final AuthState _authState =
      widget.authState ?? AuthState(_apiClient, const SecureTokenStore());
  late final bool _ownsAuthState = widget.authState == null;

  void _expireSession() => _authState.expireSession();

  @override
  void initState() {
    super.initState();
    if (_authState.status == AuthenticationStatus.initializing) {
      _authState.initialize();
    }
  }

  @override
  void dispose() {
    if (_ownsAuthState) _authState.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: _authState,
    builder: (context, _) => MaterialApp(
      title: 'Pharmacy Management System',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF176B5B)),
        scaffoldBackgroundColor: const Color(0xFFF4F6F5),
        inputDecorationTheme: const InputDecorationTheme(
          border: OutlineInputBorder(),
          isDense: true,
        ),
        cardTheme: const CardThemeData(
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.all(Radius.circular(6)),
            side: BorderSide(color: Color(0xFFD8DEDB)),
          ),
        ),
        useMaterial3: true,
      ),
      home: switch (_authState.status) {
        AuthenticationStatus.initializing => const Scaffold(
          body: Center(child: CircularProgressIndicator()),
        ),
        AuthenticationStatus.unauthenticated ||
        AuthenticationStatus.authenticating => LoginScreen(
          authState: _authState,
        ),
        AuthenticationStatus.authenticated =>
          _authState.mustChangePassword
              ? ForceChangePasswordScreen(authState: _authState)
              : AppShell(authState: _authState),
      },
    ),
  );
}
