import 'package:flutter/material.dart';
import '../../ui/app_widgets.dart';

import '../auth/auth_state.dart';

class AdministrationScreen extends StatefulWidget {
  const AdministrationScreen({required this.authState, super.key});
  final AuthState authState;
  @override
  State<AdministrationScreen> createState() => _AdministrationScreenState();
}

class _AdministrationScreenState extends State<AdministrationScreen> {
  String? _selected;
  dynamic _data;
  bool _loading = false;
  String? _error;
  final _auditSearch = TextEditingController();

  List<String> get _sections => [
    if (widget.authState.can('audit.view')) 'Audit',
    if (widget.authState.can('recycle_bin.view')) 'Recycle Bin',
    if (widget.authState.can('branches.view')) 'Branches',
    if (widget.authState.can('system.view')) 'Settings',
    if (widget.authState.can('system.backup')) 'Backup',
    if (widget.authState.can('system.view')) 'System Info',
  ];

  @override
  void initState() {
    super.initState();
    _selected = _sections.isEmpty ? null : _sections.first;
    WidgetsBinding.instance.addPostFrameCallback((_) => _load());
  }

  @override
  void dispose() {
    _auditSearch.dispose();
    super.dispose();
  }

  String get _path => switch (_selected) {
    'Audit' => Uri(
      path: 'audit',
      queryParameters: _auditSearch.text.trim().isEmpty
          ? null
          : {'search': _auditSearch.text.trim()},
    ).toString(),
    'Recycle Bin' => 'recycle-bin',
    'Branches' => 'branches',
    'Settings' => 'settings',
    'Backup' => 'backups',
    _ => 'system-info',
  };

  Future<void> _load() async {
    if (_selected == null) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final value = await widget.authState.administration(_path);
      if (mounted) {
        setState(() => _data = value);
      }
    } catch (_) {
      if (mounted) {
        setState(() => _error = 'Administration data could not be loaded.');
      }
    } finally {
      if (mounted) {
        setState(() => _loading = false);
      }
    }
  }

  Future<void> _backup() async {
    setState(() => _loading = true);
    try {
      await widget.authState.administration('backups', method: 'POST');
      await _load();
    } catch (_) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = 'Backup could not be created.';
        });
      }
    }
  }

  Future<void> _restore(Map<String, dynamic> item) async {
    await widget.authState.administration(
      'recycle-bin/${item['entityType']}/${item['id']}/restore',
      method: 'POST',
    );
    await _load();
  }

  Future<void> _editSettings() async {
    final current = _data as Map<String, dynamic>;
    final business = TextEditingController(
      text: '${current['businessName'] ?? ''}',
    );
    final footer = TextEditingController(
      text: '${current['receiptFooter'] ?? ''}',
    );
    final save = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Receipt settings'),
        content: SizedBox(
          width: 440,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: business,
                decoration: const InputDecoration(labelText: 'Business name'),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: footer,
                decoration: const InputDecoration(labelText: 'Receipt footer'),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Save'),
          ),
        ],
      ),
    );
    if (save == true) {
      await widget.authState.administration(
        'settings',
        method: 'PUT',
        body: {
          ...current,
          'businessName': business.text,
          'receiptFooter': footer.text,
          'expectedVersion': current['version'],
        },
      );
      await _load();
    }
  }

  Future<void> _editBranch([Map<String, dynamic>? item]) async {
    final code = TextEditingController(text: '${item?['code'] ?? ''}');
    final name = TextEditingController(text: '${item?['name'] ?? ''}');
    final city = TextEditingController(text: '${item?['city'] ?? ''}');
    final save = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(item == null ? 'Add branch' : 'Edit branch'),
        content: SizedBox(
          width: 440,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: code,
                decoration: const InputDecoration(labelText: 'Code'),
              ),
              TextField(
                controller: name,
                decoration: const InputDecoration(labelText: 'Name'),
              ),
              TextField(
                controller: city,
                decoration: const InputDecoration(labelText: 'City'),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Save'),
          ),
        ],
      ),
    );
    if (save == true) {
      await widget.authState.administration(
        item == null ? 'branches' : 'branches/${item['id']}',
        method: item == null ? 'POST' : 'PUT',
        body: {
          'code': code.text,
          'name': name.text,
          'city': city.text,
          'address': item?['address'],
          'phoneNumber': item?['phoneNumber'],
          'email': item?['email'],
          'isHeadOffice': item?['isHeadOffice'] ?? false,
        },
      );
      await _load();
    }
  }

  Future<void> _setBranchActive(Map<String, dynamic> item) async {
    final active = item['isActive'] == true;
    await widget.authState.administration(
      'branches/${item['id']}/${active ? 'deactivate' : 'activate'}',
      method: 'POST',
    );
    await _load();
  }

  Future<void> _auditDetails(Map<String, dynamic> item) async {
    final details = await widget.authState.administration(
      'audit/${item['id']}',
    );
    if (!mounted) return;
    await showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('${details['action']} ${details['entityType']}'),
        content: SizedBox(
          width: 620,
          child: SelectableText(
            'User: ${details['username']}\nBranch: ${details['branchName']}\nEntity: ${details['entityId']}\nCreated: ${details['createdAt']}\n\nOld values:\n${details['oldValues'] ?? '-'}\n\nNew values:\n${details['newValues'] ?? '-'}',
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Close'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(
                'Administration',
                style: Theme.of(context).textTheme.headlineMedium,
              ),
              const Spacer(),
              IconButton(
                tooltip: 'Refresh',
                onPressed: _loading ? null : _load,
                icon: const Icon(Icons.refresh),
              ),
            ],
          ),
          const SizedBox(height: 16),
          SegmentedButton<String>(
            segments: _sections
                .map((x) => ButtonSegment(value: x, label: Text(x)))
                .toList(),
            selected: {_selected ?? _sections.first},
            onSelectionChanged: (value) {
              setState(() => _selected = value.first);
              _load();
            },
          ),
          const SizedBox(height: 18),
          if (_selected == 'Audit')
            SizedBox(
              width: 420,
              child: TextField(
                controller: _auditSearch,
                onSubmitted: (_) => _load(),
                decoration: InputDecoration(
                  labelText: 'Search audit events',
                  prefixIcon: const Icon(Icons.search),
                  suffixIcon: IconButton(
                    tooltip: 'Search',
                    icon: const Icon(Icons.arrow_forward),
                    onPressed: _load,
                  ),
                ),
              ),
            ),
          if (_selected == 'Backup' && widget.authState.can('system.backup'))
            Align(
              alignment: Alignment.centerRight,
              child: FilledButton.icon(
                onPressed: _loading ? null : _backup,
                icon: const Icon(Icons.backup_outlined),
                label: const Text('Create backup'),
              ),
            ),
          if (_selected == 'Branches' &&
              widget.authState.can('branches.manage'))
            Align(
              alignment: Alignment.centerRight,
              child: FilledButton.icon(
                onPressed: _loading ? null : () => _editBranch(),
                icon: const Icon(Icons.add),
                label: const Text('Add branch'),
              ),
            ),
          if (_selected == 'Settings' &&
              widget.authState.can('system.settings.manage'))
            Align(
              alignment: Alignment.centerRight,
              child: FilledButton.icon(
                onPressed: _loading ? null : _editSettings,
                icon: const Icon(Icons.edit_outlined),
                label: const Text('Edit settings'),
              ),
            ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 12),
              child: Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
          Expanded(
            child: _loading
                ? const AppLoadingState()
                : _content(),
          ),
        ],
      ),
    ),
  );

  Widget _content() {
    if (_data == null) {
      return const SizedBox.shrink();
    }
    if (_selected == 'Audit') {
      final rows = _data['items'] as List<dynamic>? ?? [];
      return rows.isEmpty
          ? AppEmptyState(title: 'No records found')
          : ListView(
              children: rows.map((raw) {
                final x = raw as Map<String, dynamic>;
                return ListTile(
                  title: Text('${x['action']} - ${x['entityType']}'),
                  subtitle: Text(
                    '${x['createdAt']}  ${x['username']}  ${x['branchName']}',
                  ),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => _auditDetails(x),
                );
              }).toList(),
            );
    }
    if (_selected == 'Recycle Bin') {
      return ListView(
        children: (_data as List<dynamic>).map((raw) {
          final x = raw as Map<String, dynamic>;
          return ListTile(
            title: Text('${x['entityType']}: ${x['name']}'),
            subtitle: Text('Deleted ${x['deletedAtUtc']}'),
            trailing: widget.authState.can('recycle_bin.restore')
                ? IconButton(
                    tooltip: 'Restore',
                    icon: const Icon(Icons.restore),
                    onPressed: () => _restore(x),
                  )
                : null,
          );
        }).toList(),
      );
    }
    if (_selected == 'Branches') {
      return ListView(
        children: (_data as List<dynamic>).map((raw) {
          final x = raw as Map<String, dynamic>;
          return ListTile(
            title: Text('${x['code']}  ${x['name']}'),
            subtitle: Text('${x['city'] ?? ''}  ${x['email'] ?? ''}'),
            leading: Icon(
              x['isActive'] == true ? Icons.check_circle_outline : Icons.block,
            ),
            trailing: widget.authState.can('branches.manage')
                ? Wrap(
                    children: [
                      IconButton(
                        tooltip: 'Edit',
                        icon: const Icon(Icons.edit_outlined),
                        onPressed: () => _editBranch(x),
                      ),
                      IconButton(
                        tooltip: x['isActive'] == true
                            ? 'Deactivate'
                            : 'Activate',
                        icon: Icon(
                          x['isActive'] == true
                              ? Icons.pause_circle_outline
                              : Icons.play_circle_outline,
                        ),
                        onPressed: () => _setBranchActive(x),
                      ),
                    ],
                  )
                : null,
          );
        }).toList(),
      );
    }
    if (_selected == 'Backup') {
      return _table(_data as List<dynamic>, const [
        'createdAt',
        'fileName',
        'sizeBytes',
        'status',
        'completedAtUtc',
      ]);
    }
    final map = _data as Map<String, dynamic>;
    if (_selected == 'System Info') {
      final user = widget.authState.currentUser;
      final clientFacts = <String, String>{
        'Current user': user == null ? '-' : '${user.fullName} (${user.username})',
        'Current branch': user?.branch.name ?? '-',
        'Server URL': widget.authState.serverUri?.toString() ?? '-',
      };
      return ListView(
        children: [
          ...clientFacts.entries.map(
            (x) => ListTile(title: Text(x.key), subtitle: Text(x.value)),
          ),
          const Divider(),
          ...map.entries.map(
            (x) => ListTile(
              title: Text(_label(x.key)),
              subtitle: Text('${x.value ?? ''}'),
            ),
          ),
        ],
      );
    }
    return ListView(
      children: map.entries
          .map(
            (x) => ListTile(
              title: Text(_label(x.key)),
              subtitle: Text('${x.value ?? ''}'),
            ),
          )
          .toList(),
    );
  }

  Widget _table(List<dynamic> rows, List<String> columns) => rows.isEmpty
      ? AppEmptyState(title: 'No records found')
      : SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: AppDataTable(
            columns: columns
                .map((x) => DataColumn(label: Text(_label(x))))
                .toList(),
            rows: rows.map((raw) {
              final row = raw as Map<String, dynamic>;
              return DataRow(
                cells: columns
                    .map((x) => DataCell(Text('${row[x] ?? ''}')))
                    .toList(),
              );
            }).toList(),
          ),
        );
  String _label(String value) => value
      .replaceAllMapped(RegExp(r'([A-Z])'), (m) => ' ${m[1]}')
      .trim()
      .split(' ')
      .map((x) => x.isEmpty ? x : '${x[0].toUpperCase()}${x.substring(1)}')
      .join(' ');
}
