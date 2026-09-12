import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../auth/auth_state.dart';
import 'accounts_models.dart';
import 'accounts_widgets.dart';

class ChartOfAccountsView extends StatefulWidget {
  const ChartOfAccountsView({required this.authState, super.key});
  final AuthState authState;
  @override
  State<ChartOfAccountsView> createState() => _ChartOfAccountsViewState();
}

class _ChartOfAccountsViewState extends State<ChartOfAccountsView> {
  var _loading = true;
  var _includeInactive = true;
  String? _error;
  List<AccountInfo> _accounts = [];
  List<AccountMappingInfo> _mappings = [];

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final values = await Future.wait([
        widget.authState.accounting('chart', query: {'includeInactive': '$_includeInactive'}),
        widget.authState.accounting('mappings'),
      ]);
      if (!mounted) return;
      setState(() {
        _accounts = (values[0] as List<dynamic>).map((x) => AccountInfo.fromJson(x as Map<String, dynamic>)).toList();
        _mappings = (values[1] as List<dynamic>).map((x) => AccountMappingInfo.fromJson(x as Map<String, dynamic>)).toList();
      });
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }

  @override
  Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(
      title: 'Chart of Accounts', subtitle: 'Global account hierarchy and semantic posting mappings',
      actions: [
        Row(children: [Checkbox(value: _includeInactive, onChanged: (v) { _includeInactive = v ?? true; _load(); }), const Text('Show inactive')]),
        IconButton(tooltip: 'Refresh', onPressed: _load, icon: const Icon(Icons.refresh)),
        if (widget.authState.can('accounts.coa.manage')) FilledButton.icon(key: const Key('coa_create'), onPressed: () => _edit(), icon: const Icon(Icons.add), label: const Text('New account')),
      ],
    ),
    Expanded(child: _body()),
  ]);

  Widget _body() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) return AccountsError(_error!, onRetry: _load);
    if (_accounts.isEmpty) return const Center(child: Text('No chart accounts found.'));
    final mappingByAccount = <String, List<String>>{};
    for (final mapping in _mappings) { (mappingByAccount[mapping.accountId] ??= []).add(mapping.key); }
    return horizontalTable(DataTable(
      columnSpacing: 28,
      columns: const [
        DataColumn(label: Text('Code')), DataColumn(label: Text('Account')), DataColumn(label: Text('Type')),
        DataColumn(label: Text('Normal')), DataColumn(label: Text('Kind')), DataColumn(label: Text('Semantic mapping')),
        DataColumn(label: Text('Status')), DataColumn(label: Text('Actions')),
      ],
      rows: _accounts.map((account) => DataRow(cells: [
        DataCell(Text(account.code)),
        DataCell(Padding(padding: EdgeInsets.only(left: 16.0 * _depth(account)), child: Row(mainAxisSize: MainAxisSize.min, children: [
          Icon(account.isPostingAccount ? Icons.subdirectory_arrow_right : Icons.folder_outlined, size: 17),
          const SizedBox(width: 7), Text(account.name, style: TextStyle(fontWeight: account.isPostingAccount ? FontWeight.normal : FontWeight.w600)),
        ]))),
        DataCell(Text(account.accountType)), DataCell(Text(account.normalBalance)),
        DataCell(Text(account.isPostingAccount ? 'Posting' : 'Control')),
        DataCell(SizedBox(width: 220, child: Text((mappingByAccount[account.id] ?? const []).join(', '), overflow: TextOverflow.ellipsis))),
        DataCell(Chip(label: Text(account.isActive ? 'Active' : 'Inactive'), visualDensity: VisualDensity.compact)),
        DataCell(widget.authState.can('accounts.coa.manage') ? Row(mainAxisSize: MainAxisSize.min, children: [
          IconButton(key: Key('coa_edit_${account.id}'), tooltip: 'Edit', onPressed: () => _edit(account), icon: const Icon(Icons.edit_outlined)),
          IconButton(tooltip: account.isActive ? 'Deactivate' : 'Activate', onPressed: () => _toggle(account), icon: Icon(account.isActive ? Icons.block : Icons.check_circle_outline)),
        ]) : const SizedBox.shrink()),
      ])).toList(),
    ));
  }

  int _depth(AccountInfo account) {
    var depth = 0;
    var parent = account.parentAccountId;
    final visited = <String>{};
    while (parent != null && visited.add(parent)) {
      depth++;
      final matches = _accounts.where((x) => x.id == parent);
      parent = matches.isEmpty ? null : matches.first.parentAccountId;
    }
    return depth.clamp(0, 3);
  }

  Future<void> _edit([AccountInfo? account]) async {
    final changed = await showDialog<bool>(context: context, builder: (_) => _AccountForm(authState: widget.authState, accounts: _accounts, account: account));
    if (changed == true) await _load();
  }

  Future<void> _toggle(AccountInfo account) async {
    final activate = !account.isActive;
    final confirmed = await showDialog<bool>(context: context, builder: (context) => AlertDialog(
      title: Text('${activate ? 'Activate' : 'Deactivate'} ${account.code}?'),
      content: Text(activate ? 'The account will become available for posting.' : 'Mapped accounts cannot be deactivated. Existing journal history remains unchanged.'),
      actions: [TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')), FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(activate ? 'Activate' : 'Deactivate'))],
    ));
    if (confirmed != true) return;
    try { await widget.authState.accounting('chart/${account.id}/${activate ? 'activate' : 'deactivate'}', method: 'POST'); await _load(); }
    on ApiException catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); }
  }
}

class _AccountForm extends StatefulWidget {
  const _AccountForm({required this.authState, required this.accounts, this.account});
  final AuthState authState;
  final List<AccountInfo> accounts;
  final AccountInfo? account;
  @override
  State<_AccountForm> createState() => _AccountFormState();
}

class _AccountFormState extends State<_AccountForm> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _code, _name, _description;
  String _type = 'Asset', _normal = 'Debit';
  String? _parent;
  bool _posting = true, _saving = false;
  String? _error;
  bool get editing => widget.account != null;
  @override
  void initState() {
    super.initState(); final a = widget.account;
    _code = TextEditingController(text: a?.code); _name = TextEditingController(text: a?.name); _description = TextEditingController(text: a?.description);
    _type = a?.accountType ?? 'Asset'; _normal = a?.normalBalance ?? 'Debit'; _parent = a?.parentAccountId; _posting = a?.isPostingAccount ?? true;
  }
  @override
  void dispose() { _code.dispose(); _name.dispose(); _description.dispose(); super.dispose(); }
  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(editing ? 'Edit account ${widget.account!.code}' : 'Create account'),
    content: SizedBox(width: 560, child: Form(key: _form, child: SingleChildScrollView(child: Column(mainAxisSize: MainAxisSize.min, children: [
      if (!editing) TextFormField(key: const Key('coa_code'), controller: _code, decoration: const InputDecoration(labelText: 'Account code'), validator: _required),
      const SizedBox(height: 12), TextFormField(key: const Key('coa_name'), controller: _name, decoration: const InputDecoration(labelText: 'Account name'), validator: _required),
      if (!editing) ...[
        const SizedBox(height: 12), Row(children: [
          Expanded(child: DropdownButtonFormField<String>(initialValue: _type, isExpanded: true, decoration: const InputDecoration(labelText: 'Account type'), items: const ['Asset','Liability','Equity','Income','CostOfSales','Expense'].map((x) => DropdownMenuItem(value: x, child: Text(x))).toList(), onChanged: (x) => setState(() => _type = x!))),
          const SizedBox(width: 12), Expanded(child: DropdownButtonFormField<String>(initialValue: _normal, isExpanded: true, decoration: const InputDecoration(labelText: 'Normal balance'), items: const ['Debit','Credit'].map((x) => DropdownMenuItem(value: x, child: Text(x))).toList(), onChanged: (x) => setState(() => _normal = x!))),
        ]),
        const SizedBox(height: 12), DropdownButtonFormField<String?>(initialValue: _parent, isExpanded: true, decoration: const InputDecoration(labelText: 'Parent account (optional)'), items: [const DropdownMenuItem<String?>(value: null, child: Text('No parent')), ...widget.accounts.where((x) => !x.isPostingAccount).map((x) => DropdownMenuItem(value: x.id, child: Text('${x.code} · ${x.name}')))], onChanged: (x) => setState(() => _parent = x)),
      ],
      SwitchListTile(contentPadding: EdgeInsets.zero, title: const Text('Posting account'), subtitle: const Text('Transactions can post directly only to posting accounts.'), value: _posting, onChanged: (x) => setState(() => _posting = x)),
      TextFormField(controller: _description, decoration: const InputDecoration(labelText: 'Description'), maxLines: 2),
      if (_error != null) Padding(padding: const EdgeInsets.only(top: 12), child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
    ])))),
    actions: [TextButton(onPressed: _saving ? null : () => Navigator.pop(context, false), child: const Text('Cancel')), FilledButton(key: const Key('coa_save'), onPressed: _saving ? null : _save, child: Text(_saving ? 'Saving…' : 'Save'))],
  );
  String? _required(String? value) => value == null || value.trim().isEmpty ? 'Required' : null;
  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    setState(() { _saving = true; _error = null; });
    try {
      await widget.authState.accounting(editing ? 'chart/${widget.account!.id}' : 'chart', method: editing ? 'PUT' : 'POST', body: editing
        ? {'name': _name.text.trim(), 'description': _description.text.trim(), 'isPostingAccount': _posting}
        : {'code': _code.text.trim(), 'name': _name.text.trim(), 'parentAccountId': _parent, 'accountType': _type, 'normalBalance': _normal, 'isPostingAccount': _posting, 'description': _description.text.trim()});
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _saving = false); }
  }
}
