import 'package:flutter/material.dart';

import 'auth_state.dart';

class ForceChangePasswordScreen extends StatefulWidget {
  const ForceChangePasswordScreen({required this.authState, super.key});

  final AuthState authState;

  @override
  State<ForceChangePasswordScreen> createState() =>
      _ForceChangePasswordScreenState();
}

class _ForceChangePasswordScreenState extends State<ForceChangePasswordScreen> {
  final _formKey = GlobalKey<FormState>();
  final _current = TextEditingController();
  final _next = TextEditingController();
  final _confirm = TextEditingController();
  bool _loading = false;
  bool _obscure = true;

  @override
  void dispose() {
    _current.dispose();
    _next.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _loading = true);
    final success = await widget.authState.changePassword(
      _current.text,
      _next.text,
    );
    if (mounted) {
      setState(() => _loading = false);
      if (success && Navigator.of(context).canPop()) Navigator.pop(context);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: const Text('Change temporary password'),
      actions: [
        IconButton(
          tooltip: 'Sign out',
          onPressed: widget.authState.logout,
          icon: const Icon(Icons.logout),
        ),
      ],
    ),
    body: Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 520),
          child: Card(
            child: Padding(
              padding: const EdgeInsets.all(28),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      'Set a permanent password',
                      style: Theme.of(context).textTheme.titleLarge,
                    ),
                    const SizedBox(height: 8),
                    const Text(
                      'Use at least 10 characters with uppercase, lowercase, a digit, and a special character.',
                    ),
                    const SizedBox(height: 24),
                    _PasswordField(
                      key: const Key('current_password'),
                      controller: _current,
                      label: 'Current password',
                      obscure: _obscure,
                    ),
                    const SizedBox(height: 14),
                    _PasswordField(
                      key: const Key('new_password'),
                      controller: _next,
                      label: 'New password',
                      obscure: _obscure,
                      validator: _validatePassword,
                    ),
                    const SizedBox(height: 14),
                    _PasswordField(
                      key: const Key('confirm_password'),
                      controller: _confirm,
                      label: 'Confirm new password',
                      obscure: _obscure,
                      validator: (value) =>
                          value != _next.text ? 'Passwords do not match' : null,
                    ),
                    Align(
                      alignment: Alignment.centerRight,
                      child: TextButton.icon(
                        onPressed: () => setState(() => _obscure = !_obscure),
                        icon: Icon(
                          _obscure ? Icons.visibility : Icons.visibility_off,
                        ),
                        label: Text(
                          _obscure ? 'Show passwords' : 'Hide passwords',
                        ),
                      ),
                    ),
                    if (widget.authState.errorMessage != null)
                      Text(
                        widget.authState.errorMessage!,
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.error,
                        ),
                      ),
                    const SizedBox(height: 16),
                    FilledButton.icon(
                      key: const Key('change_password_submit'),
                      onPressed: _loading ? null : _submit,
                      icon: const Icon(Icons.password),
                      label: const Text('Change password'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    ),
  );

  String? _validatePassword(String? value) {
    final password = value ?? '';
    final valid =
        password.length >= 10 &&
        password.contains(RegExp('[A-Z]')) &&
        password.contains(RegExp('[a-z]')) &&
        password.contains(RegExp('[0-9]')) &&
        password.contains(RegExp(r'[^A-Za-z0-9]'));
    return valid ? null : 'Password does not meet the required rules';
  }
}

class _PasswordField extends StatelessWidget {
  const _PasswordField({
    required super.key,
    required this.controller,
    required this.label,
    required this.obscure,
    this.validator,
  });

  final TextEditingController controller;
  final String label;
  final bool obscure;
  final String? Function(String?)? validator;

  @override
  Widget build(BuildContext context) => TextFormField(
    controller: controller,
    obscureText: obscure,
    decoration: InputDecoration(labelText: label),
    validator:
        validator ??
        (value) => value?.isEmpty == true ? '$label is required' : null,
  );
}
