import 'package:flutter/material.dart';

import '../auth/auth_state.dart';
import '../auth/force_change_password_screen.dart';

class ProfileScreen extends StatefulWidget {
  const ProfileScreen({required this.authState, super.key});

  final AuthState authState;

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  late final _fullName = TextEditingController(
    text: widget.authState.currentUser?.fullName,
  );
  late final _email = TextEditingController(
    text: widget.authState.currentUser?.email,
  );
  late final _phone = TextEditingController(
    text: widget.authState.currentUser?.phoneNumber,
  );
  bool _saving = false;

  @override
  void dispose() {
    _fullName.dispose();
    _email.dispose();
    _phone.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_fullName.text.trim().length < 2) {
      _message('Full name is required.');
      return;
    }
    setState(() => _saving = true);
    final success = await widget.authState.updateProfile(
      fullName: _fullName.text.trim(),
      email: _email.text,
      phoneNumber: _phone.text,
    );
    if (mounted) {
      setState(() => _saving = false);
      _message(
        success
            ? 'Profile updated.'
            : widget.authState.errorMessage ?? 'Update failed.',
      );
    }
  }

  void _message(String message) => ScaffoldMessenger.of(
    context,
  ).showSnackBar(SnackBar(content: Text(message)));

  @override
  Widget build(BuildContext context) {
    final user = widget.authState.currentUser!;
    final editable = widget.authState.can('profile.update');
    return SafeArea(
      child: ListView(
        padding: const EdgeInsets.all(28),
        children: [
          Text('My profile', style: Theme.of(context).textTheme.headlineMedium),
          const SizedBox(height: 24),
          ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 720),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextFormField(
                  initialValue: user.username,
                  enabled: false,
                  decoration: const InputDecoration(labelText: 'Username'),
                ),
                const SizedBox(height: 14),
                TextFormField(
                  initialValue: user.branch.name,
                  enabled: false,
                  decoration: const InputDecoration(labelText: 'Branch'),
                ),
                const SizedBox(height: 14),
                TextFormField(
                  initialValue: user.roles.map((role) => role.name).join(', '),
                  enabled: false,
                  decoration: const InputDecoration(labelText: 'Roles'),
                ),
                const SizedBox(height: 14),
                TextField(
                  controller: _fullName,
                  enabled: editable,
                  decoration: const InputDecoration(labelText: 'Full name'),
                ),
                const SizedBox(height: 14),
                TextField(
                  controller: _email,
                  enabled: editable,
                  decoration: const InputDecoration(labelText: 'Email'),
                ),
                const SizedBox(height: 14),
                TextField(
                  controller: _phone,
                  enabled: editable,
                  decoration: const InputDecoration(labelText: 'Phone'),
                ),
                const SizedBox(height: 20),
                Wrap(
                  spacing: 12,
                  children: [
                    if (editable)
                      FilledButton.icon(
                        onPressed: _saving ? null : _save,
                        icon: const Icon(Icons.save_outlined),
                        label: const Text('Save profile'),
                      ),
                    if (widget.authState.can('profile.change_password'))
                      OutlinedButton.icon(
                        onPressed: () => Navigator.of(context).push(
                          MaterialPageRoute<void>(
                            builder: (_) => ForceChangePasswordScreen(
                              authState: widget.authState,
                            ),
                          ),
                        ),
                        icon: const Icon(Icons.password),
                        label: const Text('Change password'),
                      ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
