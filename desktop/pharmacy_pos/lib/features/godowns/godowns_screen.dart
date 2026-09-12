import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

class GodownsScreen extends StatefulWidget {
  const GodownsScreen({required this.authState, super.key});
  final AuthState authState;
  @override
  State<GodownsScreen> createState() => _GodownsScreenState();
}

class _GodownsScreenState extends State<GodownsScreen> {
  final _search = TextEditingController();
  PagedGodowns? _godowns;
  List<InventoryLookup> _branches = const [];
  String? _branchFilter;
  bool _loading = true;
  String? _error;

  bool can(String permission) => widget.authState.can(permission);

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final options = await widget.authState.inventoryOptions();
      final godowns = await widget.authState.listGodowns(
        branchId: _branchFilter,
        search: _search.text,
      );
      if (mounted) {
        setState(() {
          _branches = options.branches;
          _godowns = godowns;
        });
      }
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(24, 22, 24, 12),
          child: Wrap(
            spacing: 12,
            runSpacing: 12,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              SizedBox(
                width: 200,
                child: Text(
                  'Godowns',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              SizedBox(
                width: 220,
                child: DropdownButtonFormField<String?>(
                  initialValue: _branchFilter,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Branch'),
                  items: [
                    const DropdownMenuItem<String?>(
                      value: null,
                      child: Text('All branches', overflow: TextOverflow.ellipsis),
                    ),
                    ..._branches.map(
                      (b) => DropdownMenuItem<String?>(
                        value: b.id,
                        child: Text(b.name, overflow: TextOverflow.ellipsis),
                      ),
                    ),
                  ],
                  onChanged: (v) {
                    setState(() => _branchFilter = v);
                    _load();
                  },
                ),
              ),
              SizedBox(
                width: 280,
                child: TextField(
                  controller: _search,
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search),
                    labelText: 'Search godowns',
                  ),
                  onSubmitted: (_) => _load(),
                ),
              ),
              IconButton.filledTonal(
                onPressed: _load,
                tooltip: 'Refresh',
                icon: const Icon(Icons.refresh),
              ),
              if (can('godowns.create'))
                FilledButton.icon(
                  key: const Key('add_godown'),
                  onPressed: () => _showForm(),
                  icon: const Icon(Icons.warehouse_outlined),
                  label: const Text('Add Godown'),
                ),
            ],
          ),
        ),
        Expanded(child: _body()),
      ],
    ),
  );

  Widget _body() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) return Center(child: Text(_error!));
    final items = _godowns?.items ?? const <GodownListItem>[];
    if (items.isEmpty) return const Center(child: Text('No godowns found'));
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          columns: const [
            DataColumn(label: Text('Branch')),
            DataColumn(label: Text('Code')),
            DataColumn(label: Text('Name')),
            DataColumn(label: Text('Description')),
            DataColumn(label: Text('Status')),
            DataColumn(label: Text('Actions')),
          ],
          rows: items
              .map(
                (godown) => DataRow(
                  cells: [
                    DataCell(Text(godown.branchName)),
                    DataCell(Text(godown.code)),
                    DataCell(Text(godown.name)),
                    DataCell(Text(godown.description ?? '-')),
                    DataCell(
                      Wrap(
                        spacing: 6,
                        children: [
                          Chip(
                            label: Text(
                              godown.isActive ? 'Active' : 'Inactive',
                            ),
                            visualDensity: VisualDensity.compact,
                          ),
                          if (godown.isDefault)
                            const Chip(
                              label: Text('Default'),
                              avatar: Icon(Icons.star, size: 16),
                              visualDensity: VisualDensity.compact,
                            ),
                        ],
                      ),
                    ),
                    DataCell(
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          if (can('godowns.update'))
                            IconButton(
                              tooltip: 'Edit',
                              onPressed: () => _showForm(godown: godown),
                              icon: const Icon(Icons.edit_outlined),
                            ),
                          if (can('godowns.manage'))
                            IconButton(
                              tooltip: 'Manage user access',
                              onPressed: () => _showUsers(godown),
                              icon: const Icon(Icons.people_outline),
                            ),
                          if (can('godowns.set_default') &&
                              !godown.isDefault &&
                              godown.isActive)
                            IconButton(
                              tooltip: 'Set as default',
                              onPressed: () => _setDefault(godown),
                              icon: const Icon(Icons.star_outline),
                            ),
                          if (can(
                            godown.isActive
                                ? 'godowns.deactivate'
                                : 'godowns.activate',
                          ))
                            IconButton(
                              tooltip: godown.isActive
                                  ? 'Deactivate'
                                  : 'Activate',
                              onPressed: () => _confirmStatus(godown),
                              icon: Icon(
                                godown.isActive
                                    ? Icons.block
                                    : Icons.check_circle_outline,
                              ),
                            ),
                        ],
                      ),
                    ),
                  ],
                ),
              )
              .toList(),
        ),
      ),
    );
  }

  Future<void> _showForm({GodownListItem? godown}) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => _GodownForm(
        authState: widget.authState,
        branches: _branches,
        defaultBranchId: _branchFilter,
        godown: godown,
      ),
    );
    if (ok == true) await _load();
  }

  Future<void> _showUsers(GodownListItem godown) async {
    await showDialog<void>(
      context: context,
      builder: (_) =>
          _GodownUsersDialog(authState: widget.authState, godown: godown),
    );
  }

  Future<void> _setDefault(GodownListItem godown) async {
    try {
      await widget.authState.setGodownDefault(godown.id);
      await _load();
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(e.message)));
      }
    }
  }

  Future<void> _confirmStatus(GodownListItem godown) async {
    final active = !godown.isActive;
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: Text(active ? 'Activate godown' : 'Deactivate godown'),
        content: Text(
          active
              ? 'This godown will become selectable for new transactions again.'
              : 'This godown will no longer be selectable for new transactions. Historical records remain readable.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            key: const Key('confirm_godown_status'),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Confirm'),
          ),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await widget.authState.setGodownActive(godown.id, active);
      await _load();
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }
}

class _GodownForm extends StatefulWidget {
  const _GodownForm({
    required this.authState,
    required this.branches,
    this.defaultBranchId,
    this.godown,
  });
  final AuthState authState;
  final List<InventoryLookup> branches;
  final String? defaultBranchId;
  final GodownListItem? godown;
  @override
  State<_GodownForm> createState() => _GodownFormState();
}

class _GodownFormState extends State<_GodownForm> {
  final _form = GlobalKey<FormState>();
  late final _code = TextEditingController(text: widget.godown?.code);
  late final _name = TextEditingController(text: widget.godown?.name);
  late final _description = TextEditingController(
    text: widget.godown?.description,
  );
  late String? _branchId = widget.godown?.branchId ?? widget.defaultBranchId;
  bool _isDefault = false;
  bool _isActive = true;
  String? _error;

  @override
  Widget build(BuildContext context) {
    final editing = widget.godown != null;
    return AlertDialog(
      title: Text(editing ? 'Edit Godown' : 'Add Godown'),
      content: SizedBox(
        width: 480,
        child: Form(
          key: _form,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                DropdownButtonFormField<String>(
                  initialValue: _branchId,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Branch'),
                  items: widget.branches
                      .map(
                        (b) => DropdownMenuItem<String>(
                          value: b.id,
                          child: Text(b.name),
                        ),
                      )
                      .toList(),
                  onChanged: editing
                      ? null
                      : (v) => setState(() => _branchId = v),
                  validator: (v) => v == null ? 'Required' : null,
                ),
                TextFormField(
                  key: const Key('godown_code'),
                  controller: _code,
                  decoration: const InputDecoration(labelText: 'Code'),
                  validator: _required,
                ),
                TextFormField(
                  key: const Key('godown_name'),
                  controller: _name,
                  decoration: const InputDecoration(labelText: 'Name'),
                  validator: _required,
                ),
                TextFormField(
                  controller: _description,
                  decoration: const InputDecoration(
                    labelText: 'Description / Notes',
                  ),
                  maxLines: 2,
                ),
                if (!editing)
                  SwitchListTile(
                    value: _isDefault,
                    onChanged: (v) => setState(() => _isDefault = v),
                    title: const Text('Make default godown for this branch'),
                  ),
                if (!editing)
                  SwitchListTile(
                    value: _isActive,
                    onChanged: (v) => setState(() => _isActive = v),
                    title: const Text('Active'),
                  ),
                if (_error != null)
                  Text(
                    _error!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context, false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          key: const Key('save_godown'),
          onPressed: _save,
          child: const Text('Save'),
        ),
      ],
    );
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    try {
      if (widget.godown == null) {
        await widget.authState.createGodown({
          'branchId': _branchId,
          'code': _code.text.trim(),
          'name': _name.text.trim(),
          'description': _empty(_description.text),
          'isDefault': _isDefault,
          'isActive': _isActive,
        });
      } else {
        await widget.authState.updateGodown(widget.godown!.id, {
          'code': _code.text.trim(),
          'name': _name.text.trim(),
          'description': _empty(_description.text),
        });
      }
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    }
  }
}

class _GodownUsersDialog extends StatefulWidget {
  const _GodownUsersDialog({required this.authState, required this.godown});
  final AuthState authState;
  final GodownListItem godown;
  @override
  State<_GodownUsersDialog> createState() => _GodownUsersDialogState();
}

class _GodownUsersDialogState extends State<_GodownUsersDialog> {
  List<UserGodownAssignment>? _assignments;
  List<UserListItem> _branchUsers = const [];
  String? _selectedUserId;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final assignments = await widget.authState.listGodownUsers(
      widget.godown.id,
    );
    final users = await widget.authState.listUsers(
      branchId: widget.godown.branchId,
      isActive: true,
    );
    if (mounted) {
      setState(() {
        _assignments = assignments;
        _branchUsers = users.items;
      });
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text('${widget.godown.name} — User Access'),
    content: SizedBox(
      width: 480,
      child: _assignments == null
          ? const Center(child: CircularProgressIndicator())
          : Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (_assignments!.isEmpty)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 12),
                    child: Text('No users are assigned to this godown yet.'),
                  )
                else
                  ..._assignments!.map(
                    (a) => ListTile(
                      dense: true,
                      title: Text(a.userFullName),
                      subtitle: a.isDefault ? const Text('Default') : null,
                      trailing: IconButton(
                        tooltip: 'Remove',
                        icon: const Icon(Icons.remove_circle_outline),
                        onPressed: () async {
                          await widget.authState.unassignUserGodown(
                            widget.godown.id,
                            a.userId,
                          );
                          await _load();
                        },
                      ),
                    ),
                  ),
                const Divider(),
                Row(
                  children: [
                    Expanded(
                      child: DropdownButtonFormField<String>(
                        initialValue: _selectedUserId,
                        isExpanded: true,
                        decoration: const InputDecoration(
                          labelText: 'Assign user',
                        ),
                        items: _branchUsers
                            .where(
                              (u) => _assignments!.every(
                                (a) => a.userId != u.id,
                              ),
                            )
                            .map(
                              (u) => DropdownMenuItem<String>(
                                value: u.id,
                                child: Text(u.fullName),
                              ),
                            )
                            .toList(),
                        onChanged: (v) => setState(() => _selectedUserId = v),
                      ),
                    ),
                    const SizedBox(width: 8),
                    FilledButton(
                      onPressed: _selectedUserId == null
                          ? null
                          : () async {
                              try {
                                await widget.authState.assignUserGodown(
                                  widget.godown.id,
                                  {
                                    'userId': _selectedUserId,
                                    'godownId': widget.godown.id,
                                    'isDefault': false,
                                  },
                                );
                                setState(() => _selectedUserId = null);
                                await _load();
                              } on ApiException catch (e) {
                                setState(() => _error = e.message);
                              }
                            },
                      child: const Text('Add'),
                    ),
                  ],
                ),
                if (_error != null)
                  Text(
                    _error!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
              ],
            ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('Close'),
      ),
    ],
  );
}

String? _required(String? value) =>
    value == null || value.trim().isEmpty ? 'Required' : null;
String? _empty(String value) => value.trim().isEmpty ? null : value.trim();
