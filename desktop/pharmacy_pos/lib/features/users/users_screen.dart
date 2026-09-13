import 'package:flutter/material.dart';
import '../../ui/app_widgets.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';
import 'user_form_screen.dart';

class UsersScreen extends StatefulWidget {
  const UsersScreen({required this.authState, super.key});

  final AuthState authState;

  @override
  State<UsersScreen> createState() => _UsersScreenState();
}

class _UsersScreenState extends State<UsersScreen> {
  final _search = TextEditingController();
  UserOptions? _options;
  List<UserListItem> _users = [];
  String? _roleId;
  String? _branchId;
  bool? _active;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load(initial: true);
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _load({bool initial = false}) async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      if (initial || _options == null) {
        _options = await widget.authState.userOptions();
      }
      final page = await widget.authState.listUsers(
        search: _search.text,
        roleId: _roleId,
        branchId: _branchId,
        isActive: _active,
      );
      _users = page.items;
    } on ApiException catch (error) {
      _error = error.message;
    }
    if (mounted) setState(() => _loading = false);
  }

  Future<void> _openForm([UserListItem? item]) async {
    UserDetails? details;
    if (item != null) {
      try {
        details = await widget.authState.userDetails(item.id);
      } on ApiException catch (error) {
        _show(error.message);
        return;
      }
    }
    if (!mounted || _options == null) return;
    final saved = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => UserFormScreen(
          authState: widget.authState,
          options: _options!,
          user: details,
        ),
      ),
    );
    if (saved == true) {
      _show(details == null ? 'User created.' : 'User updated.');
      await _load();
    }
  }

  Future<void> _toggle(UserListItem user) async {
    try {
      await widget.authState.setUserActive(user.id, !user.isActive);
      _show(user.isActive ? 'User deactivated.' : 'User activated.');
      await _load();
    } on ApiException catch (error) {
      _show(error.message);
    }
  }

  Future<void> _resetPassword(UserListItem user) async {
    final password = TextEditingController();
    final confirm = TextEditingController();
    final formKey = GlobalKey<FormState>();
    final accepted = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('Reset ${user.username} password'),
        content: SizedBox(
          width: 420,
          child: Form(
            key: formKey,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextFormField(
                  key: const Key('reset_password'),
                  controller: password,
                  obscureText: true,
                  decoration: const InputDecoration(
                    labelText: 'Temporary password',
                  ),
                  validator: _passwordError,
                ),
                const SizedBox(height: 14),
                TextFormField(
                  controller: confirm,
                  obscureText: true,
                  decoration: const InputDecoration(
                    labelText: 'Confirm password',
                  ),
                  validator: (value) =>
                      value != password.text ? 'Passwords do not match' : null,
                ),
              ],
            ),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () {
              if (formKey.currentState!.validate()) {
                Navigator.pop(context, true);
              }
            },
            child: const Text('Reset password'),
          ),
        ],
      ),
    );
    if (accepted == true) {
      try {
        await widget.authState.resetPassword(user.id, password.text);
        _show(
          'Temporary password set. A password change is required at next sign-in.',
        );
      } on ApiException catch (error) {
        _show(error.message);
      }
    }
    password.dispose();
    confirm.dispose();
  }

  void _show(String message) => ScaffoldMessenger.of(
    context,
  ).showSnackBar(SnackBar(content: Text(message)));

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  'Users',
                  style: Theme.of(context).textTheme.headlineMedium,
                ),
              ),
              if (widget.authState.can('users.create'))
                FilledButton.icon(
                  key: const Key('add_user'),
                  onPressed: _options == null ? null : () => _openForm(),
                  icon: const Icon(Icons.person_add_alt_1),
                  label: const Text('Add user'),
                ),
            ],
          ),
          const SizedBox(height: 20),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              SizedBox(
                width: 280,
                child: TextField(
                  controller: _search,
                  decoration: InputDecoration(
                    labelText: 'Search users',
                    prefixIcon: const Icon(Icons.search),
                    suffixIcon: IconButton(
                      tooltip: 'Search',
                      onPressed: _load,
                      icon: const Icon(Icons.arrow_forward),
                    ),
                  ),
                  onSubmitted: (_) => _load(),
                ),
              ),
              _Filter<String>(
                label: 'Role',
                value: _roleId,
                items:
                    _options?.roles
                        .map(
                          (role) => DropdownMenuItem(
                            value: role.id,
                            child: Text(role.name),
                          ),
                        )
                        .toList() ??
                    [],
                onChanged: (value) {
                  setState(() => _roleId = value);
                  _load();
                },
              ),
              _Filter<String>(
                label: 'Branch',
                value: _branchId,
                items:
                    _options?.branches
                        .map(
                          (branch) => DropdownMenuItem(
                            value: branch.id,
                            child: Text(branch.name),
                          ),
                        )
                        .toList() ??
                    [],
                onChanged: (value) {
                  setState(() => _branchId = value);
                  _load();
                },
              ),
              _Filter<bool>(
                label: 'Status',
                value: _active,
                items: const [
                  DropdownMenuItem(value: true, child: Text('Active')),
                  DropdownMenuItem(value: false, child: Text('Inactive')),
                ],
                onChanged: (value) {
                  setState(() => _active = value);
                  _load();
                },
              ),
            ],
          ),
          const SizedBox(height: 18),
          Expanded(
            child: _loading
                ? const AppLoadingState()
                : _error != null
                ? Center(child: Text(_error!))
                : _users.isEmpty
                ? const Center(
                    child: Text('No users match the current filters.'),
                  )
                : Card(
                    child: SingleChildScrollView(
                      scrollDirection: Axis.horizontal,
                      child: SingleChildScrollView(
                        child: AppDataTable(
                          columns: const [
                            DataColumn(label: Text('Name')),
                            DataColumn(label: Text('Username')),
                            DataColumn(label: Text('Branch')),
                            DataColumn(label: Text('Role')),
                            DataColumn(label: Text('Status')),
                            DataColumn(label: Text('Last login')),
                            DataColumn(label: Text('Actions')),
                          ],
                          rows: _users.map(_row).toList(),
                        ),
                      ),
                    ),
                  ),
          ),
        ],
      ),
    ),
  );

  DataRow _row(UserListItem user) => DataRow(
    cells: [
      DataCell(Text(user.fullName)),
      DataCell(Text(user.username)),
      DataCell(Text(user.branch.name)),
      DataCell(Text(user.role.name)),
      DataCell(Text(user.isActive ? 'Active' : 'Inactive')),
      DataCell(
        Text(
          user.lastLoginAtUtc?.toLocal().toString().split('.').first ?? 'Never',
        ),
      ),
      DataCell(
        Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            IconButton(
              tooltip: 'View or edit',
              onPressed: () => _openForm(user),
              icon: Icon(
                widget.authState.can('users.update')
                    ? Icons.edit_outlined
                    : Icons.visibility_outlined,
              ),
            ),
            if (widget.authState.can(
              user.isActive ? 'users.deactivate' : 'users.activate',
            ))
              IconButton(
                tooltip: user.isActive ? 'Deactivate' : 'Activate',
                onPressed: () => _toggle(user),
                icon: Icon(
                  user.isActive
                      ? Icons.person_off_outlined
                      : Icons.person_add_alt,
                ),
              ),
            if (widget.authState.can('users.reset_password'))
              IconButton(
                tooltip: 'Reset password',
                onPressed: () => _resetPassword(user),
                icon: const Icon(Icons.password),
              ),
          ],
        ),
      ),
    ],
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

class _Filter<T> extends StatelessWidget {
  const _Filter({
    required this.label,
    required this.value,
    required this.items,
    required this.onChanged,
  });

  final String label;
  final T? value;
  final List<DropdownMenuItem<T>> items;
  final ValueChanged<T?> onChanged;

  @override
  Widget build(BuildContext context) => SizedBox(
    width: 190,
    child: DropdownButtonFormField<T>(
      initialValue: value,
      isExpanded: true,
      decoration: InputDecoration(labelText: label),
      items: [
        DropdownMenuItem<T>(value: null, child: const Text('All')),
        ...items,
      ],
      onChanged: onChanged,
    ),
  );
}
