import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

class UserFormScreen extends StatefulWidget {
  const UserFormScreen({
    required this.authState,
    required this.options,
    this.user,
    super.key,
  });

  final AuthState authState;
  final UserOptions options;
  final UserDetails? user;

  @override
  State<UserFormScreen> createState() => _UserFormScreenState();
}

class _UserFormScreenState extends State<UserFormScreen> {
  final _formKey = GlobalKey<FormState>();
  late final _fullName = TextEditingController(text: widget.user?.fullName);
  late final _username = TextEditingController(text: widget.user?.username);
  late final _email = TextEditingController(text: widget.user?.email);
  late final _phone = TextEditingController(text: widget.user?.phoneNumber);
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  late String? _branchId =
      widget.user?.branch.id ?? widget.options.branches.firstOrNull?.id;
  late String? _roleId =
      widget.user?.roles.firstOrNull?.id ??
      widget.options.roles.firstOrNull?.id;
  late bool _active = widget.user?.isActive ?? true;
  bool _saving = false;
  String? _error;

  bool get _editing => widget.user != null;
  bool get _canEdit => !_editing || widget.authState.can('users.update');

  @override
  void dispose() {
    _fullName.dispose();
    _username.dispose();
    _email.dispose();
    _phone.dispose();
    _password.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate() ||
        _branchId == null ||
        _roleId == null) {
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    final values = <String, dynamic>{
      'fullName': _fullName.text.trim(),
      'email': _email.text.trim().isEmpty ? null : _email.text.trim(),
      'phoneNumber': _phone.text.trim().isEmpty ? null : _phone.text.trim(),
      'branchId': _branchId,
      'roleId': _roleId,
      if (!_editing) 'username': _username.text.trim(),
      if (!_editing) 'temporaryPassword': _password.text,
      if (!_editing) 'isActive': _active,
    };
    try {
      if (_editing) {
        await widget.authState.updateUser(widget.user!.id, values);
      } else {
        await widget.authState.createUser(values);
      }
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (error) {
      if (mounted) {
        setState(() {
          _error = error.message;
          _saving = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(_editing ? 'Edit user' : 'Add user')),
    body: ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 760),
            child: Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  TextFormField(
                    key: const Key('user_full_name'),
                    controller: _fullName,
                    enabled: _canEdit,
                    decoration: const InputDecoration(labelText: 'Full name'),
                    validator: (value) => (value?.trim().length ?? 0) < 2
                        ? 'Full name is required'
                        : null,
                  ),
                  const SizedBox(height: 14),
                  TextFormField(
                    key: const Key('user_username'),
                    controller: _username,
                    enabled: !_editing,
                    decoration: const InputDecoration(labelText: 'Username'),
                    validator: (value) =>
                        !_editing &&
                            !RegExp(
                              r'^[A-Za-z0-9._-]{3,100}$',
                            ).hasMatch(value?.trim() ?? '')
                        ? 'Use 3+ letters, numbers, dots, underscores, or hyphens'
                        : null,
                  ),
                  const SizedBox(height: 14),
                  Row(
                    children: [
                      Expanded(
                        child: TextFormField(
                          controller: _email,
                          enabled: _canEdit,
                          decoration: const InputDecoration(
                            labelText: 'Email (optional)',
                          ),
                          keyboardType: TextInputType.emailAddress,
                        ),
                      ),
                      const SizedBox(width: 14),
                      Expanded(
                        child: TextFormField(
                          controller: _phone,
                          enabled: _canEdit,
                          decoration: const InputDecoration(
                            labelText: 'Phone (optional)',
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 14),
                  Row(
                    children: [
                      Expanded(
                        child: DropdownButtonFormField<String>(
                          initialValue: _branchId,
                          isExpanded: true,
                          decoration: const InputDecoration(
                            labelText: 'Branch',
                          ),
                          items: widget.options.branches
                              .map(
                                (branch) => DropdownMenuItem(
                                  value: branch.id,
                                  child: Text(branch.name),
                                ),
                              )
                              .toList(),
                          onChanged: _canEdit
                              ? (value) => setState(() => _branchId = value)
                              : null,
                          validator: (value) =>
                              value == null ? 'Branch is required' : null,
                        ),
                      ),
                      const SizedBox(width: 14),
                      Expanded(
                        child: DropdownButtonFormField<String>(
                          initialValue: _roleId,
                          isExpanded: true,
                          decoration: const InputDecoration(labelText: 'Role'),
                          items: widget.options.roles
                              .map(
                                (role) => DropdownMenuItem(
                                  value: role.id,
                                  child: Text(role.name),
                                ),
                              )
                              .toList(),
                          onChanged:
                              (!_editing ||
                                  widget.authState.can('roles.manage'))
                              ? (value) => setState(() => _roleId = value)
                              : null,
                          validator: (value) =>
                              value == null ? 'Role is required' : null,
                        ),
                      ),
                    ],
                  ),
                  if (!_editing) ...[
                    const SizedBox(height: 14),
                    Row(
                      children: [
                        Expanded(
                          child: TextFormField(
                            key: const Key('user_password'),
                            controller: _password,
                            obscureText: true,
                            decoration: const InputDecoration(
                              labelText: 'Temporary password',
                            ),
                            validator: _passwordError,
                          ),
                        ),
                        const SizedBox(width: 14),
                        Expanded(
                          child: TextFormField(
                            controller: _confirm,
                            obscureText: true,
                            decoration: const InputDecoration(
                              labelText: 'Confirm password',
                            ),
                            validator: (value) => value != _password.text
                                ? 'Passwords do not match'
                                : null,
                          ),
                        ),
                      ],
                    ),
                    SwitchListTile(
                      contentPadding: EdgeInsets.zero,
                      title: const Text('Active'),
                      value: _active,
                      onChanged: (value) => setState(() => _active = value),
                    ),
                  ],
                  if (_error != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 14),
                      child: Text(
                        _error!,
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.error,
                        ),
                      ),
                    ),
                  const SizedBox(height: 22),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: [
                      TextButton(
                        onPressed: _saving
                            ? null
                            : () => Navigator.pop(context),
                        child: const Text('Cancel'),
                      ),
                      const SizedBox(width: 10),
                      if (_canEdit)
                        FilledButton.icon(
                          key: const Key('save_user'),
                          onPressed: _saving ? null : _save,
                          icon: const Icon(Icons.save_outlined),
                          label: Text(
                            _editing ? 'Save changes' : 'Create user',
                          ),
                        ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    ),
  );

  static String? _passwordError(String? value) {
    final password = value ?? '';
    return password.length >= 10 &&
            RegExp('[A-Z]').hasMatch(password) &&
            RegExp('[a-z]').hasMatch(password) &&
            RegExp('[0-9]').hasMatch(password) &&
            RegExp(r'[^A-Za-z0-9]').hasMatch(password)
        ? null
        : 'Use 10+ characters with uppercase, lowercase, digit, and special character';
  }
}
