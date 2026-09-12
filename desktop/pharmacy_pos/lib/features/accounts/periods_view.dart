import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../auth/auth_state.dart';
import 'accounts_models.dart';
import 'accounts_widgets.dart';

class PeriodsView extends StatefulWidget {
  const PeriodsView({required this.authState, super.key});
  final AuthState authState;
  @override
  State<PeriodsView> createState() => _PeriodsViewState();
}

class _PeriodsViewState extends State<PeriodsView> {
  var _loading = true, _year = DateTime.now().year;
  String? _error;
  List<Map<String, dynamic>> _periods = [];
  Map<String, dynamic>? _fiscalYearClose;

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final results = await Future.wait([
        widget.authState.accounting('periods', query: {'fiscalYear': '$_year'}),
        widget.authState.accounting('periods/fiscal-years/$_year'),
      ]);
      if (mounted) {
        setState(() {
          _periods = (results[0] as List<dynamic>).cast<Map<String, dynamic>>();
          _fiscalYearClose = results[1] as Map<String, dynamic>?;
        });
      }
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }

  bool get _allClosed => _periods.isNotEmpty && _periods.every((x) => enumName(x['status']) == 'Closed');
  bool get _yearClosed => enumName(_fiscalYearClose?['status']) == 'Closed';

  @override
  Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(title: 'Accounting Periods', subtitle: 'Fiscal year $_year · period locking gates all posting', actions: [
      IconButton(onPressed: () => setState(() { _year--; _load(); }), icon: const Icon(Icons.chevron_left)),
      Text('$_year'), IconButton(onPressed: () => setState(() { _year++; _load(); }), icon: const Icon(Icons.chevron_right)),
      IconButton(tooltip: 'Refresh', onPressed: _load, icon: const Icon(Icons.refresh)),
      if (widget.authState.can('accounts.periods.manage')) FilledButton.icon(key: const Key('period_create'), onPressed: _create, icon: const Icon(Icons.add), label: const Text('New period')),
    ]),
    if (_yearClosed) Container(width: double.infinity, color: Theme.of(context).colorScheme.errorContainer, padding: const EdgeInsets.all(12),
      child: Row(children: [const Expanded(child: Text('Fiscal year is CLOSED. Reopen the fiscal year before any of its periods can be reopened.')),
        if (widget.authState.can('accounts.periods.reopen')) TextButton(onPressed: _reopenYear, child: const Text('Reopen year'))])),
    Expanded(child: _body()),
    if (widget.authState.can('accounts.periods.close') && !_yearClosed) Padding(padding: const EdgeInsets.all(16), child: Align(alignment: Alignment.centerRight,
      child: OutlinedButton.icon(onPressed: _allClosed ? _closeYear : null, icon: const Icon(Icons.event_busy), label: Text(_allClosed ? 'Close fiscal year $_year' : 'Close all periods to enable year close')))),
  ]);

  Widget _body() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) return AccountsError(_error!, onRetry: _load);
    if (_periods.isEmpty) return const Center(child: Text('No periods defined for this fiscal year.'));
    return horizontalTable(DataTable(columns: const [
      DataColumn(label: Text('#')), DataColumn(label: Text('Name')), DataColumn(label: Text('Start')), DataColumn(label: Text('End')),
      DataColumn(label: Text('Status')), DataColumn(label: Text('Actions')),
    ], rows: _periods.map((p) => DataRow(cells: [
      DataCell(Text('${p['periodNumber']}')), DataCell(Text('${p['name']}')), DataCell(Text('${p['startDate']}')), DataCell(Text('${p['endDate']}')),
      DataCell(_statusChip(enumName(p['status']))),
      DataCell(Row(mainAxisSize: MainAxisSize.min, children: _actions(p))),
    ])).toList()));
  }

  Widget _statusChip(String status) => Chip(label: Text(status), visualDensity: VisualDensity.compact,
    backgroundColor: status == 'Closed' ? Colors.red.withValues(alpha: .15) : status == 'SoftClosed' ? Colors.orange.withValues(alpha: .15) : Colors.green.withValues(alpha: .15));

  List<Widget> _actions(Map<String, dynamic> p) {
    final status = enumName(p['status']);
    final canClose = widget.authState.can('accounts.periods.close');
    final canReopen = widget.authState.can('accounts.periods.reopen');
    return [
      if (canClose && status == 'Open') TextButton(onPressed: () => _softClose(p), child: const Text('Soft-close')),
      if (canClose && status != 'Closed') TextButton(onPressed: () => _close(p), child: const Text('Close')),
      if (canReopen && status != 'Open') TextButton(onPressed: () => _reopen(p), child: const Text('Reopen')),
    ];
  }

  Future<void> _create() async {
    final ok = await showDialog<bool>(context: context, builder: (_) => _PeriodForm(authState: widget.authState, defaultYear: _year));
    if (ok == true) await _load();
  }

  Future<void> _softClose(Map<String, dynamic> p) async {
    try { await widget.authState.accounting('periods/${p['id']}/soft-close', method: 'POST', body: {'notes': null}); await _load(); }
    on ApiException catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); }
  }

  Future<void> _close(Map<String, dynamic> p) async {
    final confirmed = await showDialog<bool>(context: context, builder: (context) => AlertDialog(
      title: Text('Close ${p['name']}?'),
      content: const Text('Once closed, no further postings will be accepted into this period without explicit reopening.'),
      actions: [TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')), FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Close period'))],
    ));
    if (confirmed != true) return;
    try { await widget.authState.accounting('periods/${p['id']}/close', method: 'POST', body: {'notes': null}); await _load(); }
    on ApiException catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); }
  }

  Future<void> _reopen(Map<String, dynamic> p) async {
    final reason = await _promptReason(context, 'Reopen ${p['name']}?');
    if (reason == null) return;
    try { await widget.authState.accounting('periods/${p['id']}/reopen', method: 'POST', body: {'reason': reason}); await _load(); }
    on ApiException catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); }
  }

  Future<void> _closeYear() async {
    final confirmed = await showDialog<bool>(context: context, builder: (context) => AlertDialog(
      title: Text('Close fiscal year $_year?'),
      content: const Text('This records the year\'s final P&L figures and permanently prevents ordinary posting into any of its periods. This does not post a closing journal — earnings are always derived live from posted history.'),
      actions: [TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')), FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Close year'))],
    ));
    if (confirmed != true) return;
    try { await widget.authState.accounting('periods/fiscal-years/close', method: 'POST', body: {'fiscalYear': _year, 'notes': null}); await _load(); }
    on ApiException catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); }
  }

  Future<void> _reopenYear() async {
    final reason = await _promptReason(context, 'Reopen fiscal year $_year?');
    if (reason == null) return;
    try { await widget.authState.accounting('periods/fiscal-years/$_year/reopen', method: 'POST', body: {'reason': reason}); await _load(); }
    on ApiException catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); }
  }
}

Future<String?> _promptReason(BuildContext context, String title) async {
  final controller = TextEditingController();
  return showDialog<String>(context: context, builder: (context) => AlertDialog(
    title: Text(title),
    content: TextField(key: const Key('reopen_reason'), controller: controller, decoration: const InputDecoration(labelText: 'Reason *'), autofocus: true),
    actions: [TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancel')),
      FilledButton(onPressed: () => controller.text.trim().isEmpty ? null : Navigator.pop(context, controller.text.trim()), child: const Text('Reopen'))],
  ));
}

class _PeriodForm extends StatefulWidget {
  const _PeriodForm({required this.authState, required this.defaultYear});
  final AuthState authState;
  final int defaultYear;
  @override
  State<_PeriodForm> createState() => _PeriodFormState();
}

class _PeriodFormState extends State<_PeriodForm> {
  final _name = TextEditingController();
  late var _year = widget.defaultYear;
  var _periodNumber = 1;
  var _start = DateTime.now(), _end = DateTime.now();
  var _saving = false;
  String? _error;
  @override
  void dispose() { _name.dispose(); super.dispose(); }
  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('New accounting period'),
    content: SizedBox(width: 420, child: Column(mainAxisSize: MainAxisSize.min, children: [
      TextField(key: const Key('period_name'), controller: _name, decoration: const InputDecoration(labelText: 'Name *')),
      const SizedBox(height: 12),
      Row(children: [
        Expanded(child: TextFormField(initialValue: '$_year', decoration: const InputDecoration(labelText: 'Fiscal year'), keyboardType: TextInputType.number, onChanged: (x) => _year = int.tryParse(x) ?? _year)),
        const SizedBox(width: 12),
        Expanded(child: TextFormField(initialValue: '$_periodNumber', decoration: const InputDecoration(labelText: 'Period # (1-12)'), keyboardType: TextInputType.number, onChanged: (x) => _periodNumber = int.tryParse(x) ?? _periodNumber)),
      ]),
      const SizedBox(height: 12),
      Row(children: [
        Expanded(child: OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _start); if (x != null) setState(() => _start = x); }, child: Text('Start ${shortDate(_start)}'))),
        const SizedBox(width: 12),
        Expanded(child: OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _end); if (x != null) setState(() => _end = x); }, child: Text('End ${shortDate(_end)}'))),
      ]),
      if (_error != null) Padding(padding: const EdgeInsets.only(top: 10), child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
    ])),
    actions: [TextButton(onPressed: _saving ? null : () => Navigator.pop(context, false), child: const Text('Cancel')), FilledButton(key: const Key('period_save'), onPressed: _saving ? null : _save, child: Text(_saving ? 'Saving…' : 'Create'))],
  );
  Future<void> _save() async {
    if (_name.text.trim().isEmpty) { setState(() => _error = 'Name is required.'); return; }
    setState(() { _saving = true; _error = null; });
    try {
      await widget.authState.accounting('periods', method: 'POST', body: {
        'fiscalYear': _year, 'periodNumber': _periodNumber, 'name': _name.text.trim(),
        'startDate': shortDate(_start), 'endDate': shortDate(_end), 'notes': null,
      });
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _saving = false); }
  }
}
