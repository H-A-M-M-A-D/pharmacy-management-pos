import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';
import 'accounts_models.dart';
import 'accounts_widgets.dart';

class BankReconciliationView extends StatefulWidget {
  const BankReconciliationView({required this.authState, super.key});
  final AuthState authState;
  @override
  State<BankReconciliationView> createState() => _BankReconciliationViewState();
}

class _BankReconciliationViewState extends State<BankReconciliationView> {
  var _loading = true;
  String? _error;
  List<Map<String, dynamic>> _reconciliations = [];
  List<Map<String, dynamic>> _accounts = [];

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final results = await Future.wait([
        widget.authState.accounting('bank-reconciliations'),
        widget.authState.listFinancialAccounts(branchId: widget.authState.currentUser!.branch.id),
      ]);
      if (mounted) {
        setState(() {
          _reconciliations = (results[0] as List<dynamic>).cast<Map<String, dynamic>>();
          _accounts = (results[1] as List<FinancialAccountInfo>).map((a) => {'id': a.id, 'name': a.name}).toList();
        });
      }
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }

  @override
  Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(title: 'Bank Reconciliation', subtitle: 'Match ledger activity against bank/cash statements', actions: [
      IconButton(tooltip: 'Refresh', onPressed: _load, icon: const Icon(Icons.refresh)),
      if (widget.authState.can('accounts.reconciliation.manage')) FilledButton.icon(key: const Key('reconciliation_start'), onPressed: _start, icon: const Icon(Icons.add), label: const Text('Start reconciliation')),
    ]),
    Expanded(child: _body()),
  ]);

  Widget _body() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) return AccountsError(_error!, onRetry: _load);
    if (_reconciliations.isEmpty) return const Center(child: Text('No bank reconciliations yet.'));
    return horizontalTable(DataTable(columns: const [
      DataColumn(label: Text('Account')), DataColumn(label: Text('Statement period')), DataColumn(label: Text('Statement closing')),
      DataColumn(label: Text('Book balance')), DataColumn(label: Text('Difference')), DataColumn(label: Text('Status')),
    ], rows: _reconciliations.map((r) => DataRow(onSelectChanged: (_) => _openDetail(r), cells: [
      DataCell(Text('${r['financialAccountName']}')), DataCell(Text('${r['statementStartDate']} – ${r['statementEndDate']}')),
      DataCell(Text(money(amount(r['statementClosingBalance'])))), DataCell(Text(money(amount(r['bookBalance'])))),
      DataCell(Text(money(amount(r['difference'])), style: TextStyle(color: amount(r['difference']).abs() < .005 ? Colors.green : Theme.of(context).colorScheme.error))),
      DataCell(Chip(label: Text(enumName(r['status'])), visualDensity: VisualDensity.compact)),
    ])).toList()));
  }

  Future<void> _openDetail(Map<String, dynamic> r) async {
    final changed = await showDialog<bool>(context: context, builder: (_) => _ReconciliationDetail(authState: widget.authState, id: '${r['id']}'));
    if (changed == true) await _load();
  }

  Future<void> _start() async {
    final ok = await showDialog<bool>(context: context, builder: (_) => _StartDialog(authState: widget.authState, accounts: _accounts));
    if (ok == true) await _load();
  }
}

class _StartDialog extends StatefulWidget {
  const _StartDialog({required this.authState, required this.accounts});
  final AuthState authState;
  final List<Map<String, dynamic>> accounts;
  @override
  State<_StartDialog> createState() => _StartDialogState();
}

class _StartDialogState extends State<_StartDialog> {
  String? _accountId;
  var _start = DateTime(DateTime.now().year, DateTime.now().month, 1), _end = DateTime.now();
  final _opening = TextEditingController(text: '0'), _closing = TextEditingController(text: '0');
  var _saving = false;
  String? _error;
  @override
  void dispose() { _opening.dispose(); _closing.dispose(); super.dispose(); }
  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Start bank reconciliation'),
    content: SizedBox(width: 420, child: Column(mainAxisSize: MainAxisSize.min, children: [
      DropdownButtonFormField<String>(isExpanded: true, decoration: const InputDecoration(labelText: 'Financial account *'), items: widget.accounts.map((x) => DropdownMenuItem(value: '${x['id']}', child: Text('${x['name']}'))).toList(), onChanged: (x) => setState(() => _accountId = x)),
      const SizedBox(height: 12),
      Row(children: [
        Expanded(child: OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _start); if (x != null) setState(() => _start = x); }, child: Text('From ${shortDate(_start)}'))),
        const SizedBox(width: 12),
        Expanded(child: OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _end); if (x != null) setState(() => _end = x); }, child: Text('To ${shortDate(_end)}'))),
      ]),
      const SizedBox(height: 12),
      Row(children: [
        Expanded(child: TextField(controller: _opening, keyboardType: const TextInputType.numberWithOptions(decimal: true), decoration: const InputDecoration(labelText: 'Statement opening'))),
        const SizedBox(width: 12),
        Expanded(child: TextField(controller: _closing, keyboardType: const TextInputType.numberWithOptions(decimal: true), decoration: const InputDecoration(labelText: 'Statement closing'))),
      ]),
      if (_error != null) Padding(padding: const EdgeInsets.only(top: 10), child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
    ])),
    actions: [TextButton(onPressed: _saving ? null : () => Navigator.pop(context, false), child: const Text('Cancel')), FilledButton(onPressed: _saving ? null : _save, child: Text(_saving ? 'Starting…' : 'Start'))],
  );
  Future<void> _save() async {
    if (_accountId == null) { setState(() => _error = 'Select a financial account.'); return; }
    setState(() { _saving = true; _error = null; });
    try {
      await widget.authState.accounting('bank-reconciliations', method: 'POST', body: {
        'financialAccountId': _accountId, 'statementStartDate': shortDate(_start), 'statementEndDate': shortDate(_end),
        'statementOpeningBalance': double.tryParse(_opening.text) ?? 0, 'statementClosingBalance': double.tryParse(_closing.text) ?? 0, 'notes': null,
      });
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _saving = false); }
  }
}

class _ReconciliationDetail extends StatefulWidget {
  const _ReconciliationDetail({required this.authState, required this.id});
  final AuthState authState;
  final String id;
  @override
  State<_ReconciliationDetail> createState() => _ReconciliationDetailState();
}

class _ReconciliationDetailState extends State<_ReconciliationDetail> {
  var _loading = true;
  String? _error;
  Map<String, dynamic>? _data;
  final Set<String> _selected = {};

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final data = await widget.authState.accounting('bank-reconciliations/${widget.id}') as Map<String, dynamic>;
      if (mounted) setState(() { _data = data; _selected.clear(); });
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }

  @override
  Widget build(BuildContext context) {
    final inProgress = _data != null && enumName(_data!['status']) == 'InProgress';
    return AlertDialog(
      title: Text('${_data?['financialAccountName'] ?? ''} reconciliation'),
      content: SizedBox(width: 780, height: 520, child: _loading ? const Center(child: CircularProgressIndicator()) : _error != null ? AccountsError(_error!, onRetry: _load) : _body(inProgress)),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Close')),
        if (inProgress && widget.authState.can('accounts.reconciliation.manage')) ...[
          if (_selected.isNotEmpty) TextButton(onPressed: () => _match(false), child: const Text('Unmatch selected')),
          if (_selected.isNotEmpty) TextButton(onPressed: () => _match(true), child: const Text('Match selected')),
          FilledButton(key: const Key('reconciliation_finalize'), onPressed: _finalize, child: const Text('Finalize')),
        ],
        if (!inProgress && widget.authState.can('accounts.reconciliation.manage')) TextButton(onPressed: _reopen, child: const Text('Reopen')),
      ],
    );
  }

  Widget _body(bool inProgress) {
    final data = _data!;
    final lines = (data['lines'] as List<dynamic>? ?? []).cast<Map<String, dynamic>>();
    final totalCandidateCount = (data['totalCandidateCount'] as num?)?.toInt() ?? lines.length;
    final difference = amount(data['difference']);
    return Column(children: [
      Wrap(spacing: 16, children: [
        Text('Statement closing: ${money(amount(data['statementClosingBalance']))}'),
        Text('Book balance: ${money(amount(data['bookBalance']))}'),
        Text('Difference: ${money(difference)}', style: TextStyle(color: difference.abs() < .005 ? Colors.green : Theme.of(context).colorScheme.error, fontWeight: FontWeight.bold)),
        Text('Matched total: ${money(amount(data['matchedTotal']))}'),
      ]),
      if (totalCandidateCount > lines.length)
        Padding(padding: const EdgeInsets.only(top: 6), child: Text(
          'Showing the most recent ${lines.length} of $totalCandidateCount candidate entries. Totals include all candidates.',
          style: TextStyle(color: Theme.of(context).colorScheme.error, fontSize: 12))),
      const SizedBox(height: 10),
      Expanded(child: lines.isEmpty ? const Center(child: Text('No ledger activity for this account.')) : ListView.builder(itemCount: lines.length, itemBuilder: (context, i) {
        final line = lines[i];
        final id = '${line['financialLedgerEntryId']}';
        final matched = line['isMatched'] == true;
        return CheckboxListTile(
          dense: true, value: matched || _selected.contains(id),
          enabled: inProgress,
          onChanged: inProgress ? (v) => setState(() { if (v == true) { _selected.add(id); } else { _selected.remove(id); } }) : null,
          title: Text('${line['description']}  ·  ${money(amount(line['amount']))}'),
          subtitle: Text('${line['occurredAtUtc']}  ·  ${line['referenceType']}${matched ? '  ·  matched' : ''}'),
        );
      })),
    ]);
  }

  Future<void> _match(bool match) async {
    try {
      await widget.authState.accounting('bank-reconciliations/${widget.id}/${match ? 'match' : 'unmatch'}', method: 'POST', body: {'financialLedgerEntryIds': _selected.toList()});
      await _load();
    } on ApiException catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); }
  }

  Future<void> _finalize() async {
    final difference = amount(_data?['difference']);
    var acknowledge = false;
    if (difference.abs() >= .005) {
      acknowledge = await showDialog<bool>(context: context, builder: (context) => AlertDialog(
        title: const Text('Unresolved difference'),
        content: Text('There is a difference of ${money(difference)} between the statement and the book balance. Finalize anyway and record it as an outstanding reconciling item?'),
        actions: [TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')), FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Finalize anyway'))],
      )) ?? false;
      if (!acknowledge) return;
    }
    try {
      await widget.authState.accounting('bank-reconciliations/${widget.id}/finalize', method: 'POST', body: {'notes': null, 'acknowledgeDifference': acknowledge});
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); }
  }

  Future<void> _reopen() async {
    final controller = TextEditingController();
    final reason = await showDialog<String>(context: context, builder: (context) => AlertDialog(
      title: const Text('Reopen reconciliation'),
      content: TextField(controller: controller, decoration: const InputDecoration(labelText: 'Reason *'), autofocus: true),
      actions: [TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancel')), FilledButton(onPressed: () => Navigator.pop(context, controller.text.trim()), child: const Text('Reopen'))],
    ));
    if (reason == null || reason.isEmpty) return;
    try { await widget.authState.accounting('bank-reconciliations/${widget.id}/reopen', method: 'POST', body: {'reason': reason}); await _load(); }
    on ApiException catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); }
  }
}
