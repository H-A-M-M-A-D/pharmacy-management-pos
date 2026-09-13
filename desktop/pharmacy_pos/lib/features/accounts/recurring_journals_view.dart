import 'package:flutter/material.dart';
import '../../ui/app_theme.dart';
import '../../ui/app_widgets.dart';

import '../../core/api_client.dart';
import '../auth/auth_state.dart';
import 'accounts_models.dart';
import 'accounts_widgets.dart';

class RecurringJournalsView extends StatefulWidget {
  const RecurringJournalsView({required this.authState, super.key});
  final AuthState authState;
  @override
  State<RecurringJournalsView> createState() => _RecurringJournalsViewState();
}

class _RecurringJournalsViewState extends State<RecurringJournalsView> {
  var _loading = true;
  String? _error;
  List<Map<String, dynamic>> _templates = [];
  List<AccountInfo> _accounts = [];

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final data = await Future.wait([
        widget.authState.accounting('recurring-journals', query: {'includeInactive': 'true'}),
        widget.authState.accounting('chart', query: {'includeInactive': 'false'}),
      ]);
      if (mounted) {
        setState(() {
          _templates = (data[0] as List<dynamic>).cast<Map<String, dynamic>>();
          _accounts = (data[1] as List<dynamic>).map((x) => AccountInfo.fromJson(x as Map<String, dynamic>)).where((x) => x.isPostingAccount && x.isActive).toList();
        });
      }
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }

  @override
  Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(title: 'Recurring Journals', subtitle: 'Templates for rent, subscriptions, and other repeating postings', actions: [
      IconButton(tooltip: 'Refresh', onPressed: _load, icon: const Icon(Icons.refresh)),
      if (widget.authState.can('accounts.recurring.manage')) ...[
        OutlinedButton.icon(key: const Key('generate_due'), onPressed: _generateDue, icon: const Icon(Icons.play_arrow), label: const Text('Generate due entries')),
        const SizedBox(width: 8),
        FilledButton.icon(key: const Key('recurring_create'), onPressed: _create, icon: const Icon(Icons.add), label: const Text('New template')),
      ],
    ]),
    Expanded(child: _body()),
  ]);

  Widget _body() {
    if (_loading) return const AppLoadingState();
    if (_error != null) return AccountsError(_error!, onRetry: _load);
    if (_templates.isEmpty) return AppEmptyState(title: 'No recurring journal templates yet.');
    return horizontalTable(AppDataTable(columns: const [
      DataColumn(label: Text('Name')), DataColumn(label: Text('Frequency')), DataColumn(label: Text('Next run')),
      DataColumn(label: Text('Branch')), DataColumn(label: Text('Status')), DataColumn(label: Text('Actions')),
    ], rows: _templates.map((t) => DataRow(cells: [
      DataCell(Text('${t['name']}')), DataCell(Text(enumName(t['frequency']))), DataCell(Text('${t['nextRunDate']}')),
      DataCell(Text('${t['branchName']}')), DataCell(AppStatusChip(t['isActive'] == true ? 'Active' : 'Inactive')),
      DataCell(widget.authState.can('accounts.recurring.manage') ? TextButton(onPressed: () => _toggle(t), child: Text(t['isActive'] == true ? 'Deactivate' : 'Activate')) : const SizedBox.shrink()),
    ])).toList()));
  }

  Future<void> _toggle(Map<String, dynamic> t) async {
    try { await widget.authState.accounting('recurring-journals/${t['id']}/${t['isActive'] == true ? 'deactivate' : 'activate'}', method: 'POST'); await _load(); }
    on ApiException catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); }
  }

  Future<void> _create() async {
    final ok = await showDialog<bool>(context: context, builder: (_) => _TemplateForm(authState: widget.authState, accounts: _accounts));
    if (ok == true) await _load();
  }

  Future<void> _generateDue() async {
    try {
      final result = await widget.authState.accounting('recurring-journals/generate-due', method: 'POST', body: {'asOfDate': null}) as Map<String, dynamic>;
      final generated = (result['generated'] as List<dynamic>? ?? []).length;
      final failures = (result['failures'] as List<dynamic>? ?? []);
      final message = failures.isEmpty
          ? '$generated entr${generated == 1 ? 'y' : 'ies'} generated.'
          : '$generated entr${generated == 1 ? 'y' : 'ies'} generated; ${failures.length} template${failures.length == 1 ? '' : 's'} failed and will be retried on the next run.';
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
      await _load();
    } on ApiException catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); }
  }
}

class _TemplateForm extends StatefulWidget {
  const _TemplateForm({required this.authState, required this.accounts});
  final AuthState authState;
  final List<AccountInfo> accounts;
  @override
  State<_TemplateForm> createState() => _TemplateFormState();
}

class _TemplateFormState extends State<_TemplateForm> {
  final _name = TextEditingController();
  final List<_RecurringLine> _lines = [_RecurringLine(), _RecurringLine()];
  var _frequency = 'Monthly', _start = DateTime.now(), _saving = false;
  String? _error;
  @override
  void dispose() { _name.dispose(); for (final x in _lines) { x.dispose(); } super.dispose(); }
  double get debit => _lines.fold(0, (v, x) => v + (double.tryParse(x.debit.text) ?? 0));
  double get credit => _lines.fold(0, (v, x) => v + (double.tryParse(x.credit.text) ?? 0));
  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('New recurring journal template'),
    content: SizedBox(width: 720, child: SingleChildScrollView(child: Column(mainAxisSize: MainAxisSize.min, children: [
      Row(children: [
        Expanded(flex: 2, child: TextField(key: const Key('recurring_name'), controller: _name, decoration: const InputDecoration(labelText: 'Name *'))),
        const SizedBox(width: 12),
        Expanded(child: DropdownButtonFormField<String>(initialValue: _frequency, decoration: const InputDecoration(labelText: 'Frequency'), items: const ['Weekly', 'Monthly', 'Quarterly', 'Yearly'].map((x) => DropdownMenuItem(value: x, child: Text(x))).toList(), onChanged: (x) => setState(() => _frequency = x!))),
        const SizedBox(width: 12),
        OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _start); if (x != null) setState(() => _start = x); }, child: Text('Starts ${shortDate(_start)}')),
      ]),
      const SizedBox(height: 12),
      for (var i = 0; i < _lines.length; i++) Padding(padding: const EdgeInsets.only(bottom: 8), child: Row(children: [
        Expanded(flex: 3, child: DropdownButtonFormField<String>(initialValue: _lines[i].accountId, decoration: const InputDecoration(labelText: 'Account'), isExpanded: true, items: widget.accounts.map((x) => DropdownMenuItem(value: x.id, child: Text('${x.code} · ${x.name}', overflow: TextOverflow.ellipsis))).toList(), onChanged: (x) => setState(() => _lines[i].accountId = x))),
        const SizedBox(width: 8), Expanded(child: TextField(controller: _lines[i].debit, keyboardType: const TextInputType.numberWithOptions(decimal: true), decoration: const InputDecoration(labelText: 'Debit'), onChanged: (_) => setState(() {}))),
        const SizedBox(width: 8), Expanded(child: TextField(controller: _lines[i].credit, keyboardType: const TextInputType.numberWithOptions(decimal: true), decoration: const InputDecoration(labelText: 'Credit'), onChanged: (_) => setState(() {}))),
        IconButton(onPressed: _lines.length > 2 ? () { setState(() { final removed = _lines.removeAt(i); removed.dispose(); }); } : null, icon: const Icon(Icons.remove_circle_outline)),
      ])),
      Align(alignment: Alignment.centerLeft, child: TextButton.icon(onPressed: () => setState(() => _lines.add(_RecurringLine())), icon: const Icon(Icons.add), label: const Text('Add line'))),
      Text('Debit ${money(debit)}   Credit ${money(credit)}', style: TextStyle(color: (debit - credit).abs() < .005 && debit > 0 ? AppColors.success : Theme.of(context).colorScheme.error)),
      if (_error != null) Padding(padding: const EdgeInsets.only(top: 10), child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
    ]))),
    actions: [TextButton(onPressed: _saving ? null : () => Navigator.pop(context, false), child: const Text('Cancel')), FilledButton(key: const Key('recurring_save'), onPressed: _saving ? null : _save, child: Text(_saving ? 'Saving…' : 'Create'))],
  );
  Future<void> _save() async {
    final meaningful = _lines.where((x) => (double.tryParse(x.debit.text) ?? 0) > 0 || (double.tryParse(x.credit.text) ?? 0) > 0).toList();
    if (_name.text.trim().isEmpty || meaningful.length < 2 || meaningful.any((x) => x.accountId == null) || debit <= 0 || (debit - credit).abs() >= .005) {
      setState(() => _error = 'Provide a name and at least two balanced, account-assigned lines.');
      return;
    }
    setState(() { _saving = true; _error = null; });
    try {
      await widget.authState.accounting('recurring-journals', method: 'POST', body: {
        'name': _name.text.trim(), 'description': null, 'frequency': _frequency, 'startDate': shortDate(_start), 'endDate': null,
        'branchId': widget.authState.currentUser!.branch.id,
        'lines': meaningful.map((x) => {'chartOfAccountId': x.accountId, 'debit': double.tryParse(x.debit.text) ?? 0, 'credit': double.tryParse(x.credit.text) ?? 0, 'description': null}).toList(),
      });
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _saving = false); }
  }
}

class _RecurringLine {
  String? accountId;
  final debit = TextEditingController(), credit = TextEditingController();
  void dispose() { debit.dispose(); credit.dispose(); }
}
