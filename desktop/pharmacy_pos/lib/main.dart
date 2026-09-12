import 'dart:async';

import 'package:flutter/material.dart';

import 'core/api_client.dart';
import 'core/token_store.dart';
import 'features/auth/auth_state.dart';
import 'features/auth/force_change_password_screen.dart';
import 'features/auth/login_screen.dart';
import 'features/shell/app_shell.dart';

void main() {
  // An unexpected exception anywhere in the widget tree must show a readable
  // message, never Flutter's default red/grey error box or a blank frame.
  ErrorWidget.builder = (details) => Material(
    color: const Color(0xFFF4F6F5),
    child: Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 40, color: Colors.redAccent),
            const SizedBox(height: 12),
            const Text(
              'Something went wrong displaying this screen.',
              style: TextStyle(fontWeight: FontWeight.bold),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 4),
            const Text(
              'Try going back or restarting the app. If this keeps happening, contact support.',
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    ),
  );

  runZonedGuarded(
    () {
      FlutterError.onError = (details) {
        FlutterError.presentError(details);
        debugPrint('Unhandled Flutter error: ${details.exceptionAsString()}');
      };
      runApp(const PharmacyPOSApp());
    },
    (error, stack) => debugPrint('Unhandled error: $error\n$stack'),
  );
}

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
