import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../auth/auth_state.dart';
import 'accounts_models.dart';
import 'accounts_widgets.dart';
import 'statements_view.dart' show accountingScope;

class _ControlReconciliationView extends StatefulWidget {
  const _ControlReconciliationView({required this.authState, required this.endpoint, required this.title, required this.subtitle, required this.partyLabel});
  final AuthState authState;
  final String endpoint, title, subtitle, partyLabel;
  @override
  State<_ControlReconciliationView> createState() => _ControlReconciliationViewState();
}

class _ControlReconciliationViewState extends State<_ControlReconciliationView> {
  var _loading = true;
  String? _error;
  DateTime _asOf = DateTime.now();
  Map<String, dynamic>? _data;

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final data = await widget.authState.accounting(widget.endpoint, query: accountingScope(widget.authState, asOf: DateTime(_asOf.year, _asOf.month, _asOf.day, 23, 59, 59), branchId: widget.authState.currentUser!.branch.id)) as Map<String, dynamic>;
      if (mounted) setState(() => _data = data);
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }

  @override
  Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(title: widget.title, subtitle: widget.subtitle, actions: [
      OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _asOf); if (x != null) { setState(() => _asOf = x); _load(); } }, child: Text('As of ${shortDate(_asOf)}')),
      IconButton(onPressed: _load, icon: const Icon(Icons.refresh)),
    ]),
    Expanded(child: _loading ? const Center(child: CircularProgressIndicator()) : _error != null ? AccountsError(_error!, onRetry: _load) : _body()),
  ]);

  Widget _body() {
    final data = _data!;
    final total = amount(data['totalSubledgerBalance']), gl = amount(data['totalGlBalance']), diff = amount(data['totalDifference']);
    final mismatches = (data['mismatches'] as List<dynamic>? ?? []).cast<Map<String, dynamic>>();
    return ListView(padding: const EdgeInsets.all(20), children: [
      Card(color: diff.abs() < .005 ? Colors.green.withValues(alpha: .08) : Theme.of(context).colorScheme.errorContainer, child: Padding(padding: const EdgeInsets.all(14), child: Row(children: [
        Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text('Operational ledger total: ${money(total)}'), Text('GL control account: ${money(gl)}')])),
        Text(diff.abs() < .005 ? 'RECONCILED' : 'DIFFERENCE: ${money(diff)}', style: const TextStyle(fontWeight: FontWeight.bold)),
      ]))),
      const SizedBox(height: 16),
      if (mismatches.isEmpty) const Text('No mismatches.') else horizontalTable(DataTable(columns: [
        DataColumn(label: Text(widget.partyLabel)), const DataColumn(label: Text('Ledger balance')), const DataColumn(label: Text('GL balance')), const DataColumn(label: Text('Difference')),
      ], rows: mismatches.map((x) => DataRow(cells: [
        DataCell(Text('${x['partyName']}')), DataCell(Text(money(amount(x['subledgerBalance'])))), DataCell(Text(money(amount(x['glBalance'])))),
        DataCell(Text(money(amount(x['difference'])), style: TextStyle(color: Theme.of(context).colorScheme.error))),
      ])).toList())),
    ]);
  }
}

class ArReconciliationView extends StatelessWidget {
  const ArReconciliationView({required this.authState, super.key});
  final AuthState authState;
  @override
  Widget build(BuildContext context) => _ControlReconciliationView(authState: authState, endpoint: 'reconciliation/ar', title: 'AR Control Reconciliation', subtitle: 'Customer ledger totals vs Accounts Receivable GL', partyLabel: 'Customer');
}

class ApReconciliationView extends StatelessWidget {
  const ApReconciliationView({required this.authState, super.key});
  final AuthState authState;
  @override
  Widget build(BuildContext context) => _ControlReconciliationView(authState: authState, endpoint: 'reconciliation/ap', title: 'AP Control Reconciliation', subtitle: 'Supplier ledger totals vs Accounts Payable GL', partyLabel: 'Supplier');
}

class InventoryReconciliationView extends StatefulWidget {
  const InventoryReconciliationView({required this.authState, super.key});
  final AuthState authState;
  @override
  State<InventoryReconciliationView> createState() => _InventoryReconciliationViewState();
}

class _InventoryReconciliationViewState extends State<InventoryReconciliationView> {
  var _loading = true;
  String? _error;
  DateTime _asOf = DateTime.now();
  Map<String, dynamic>? _data;
  @override
  void initState() { super.initState(); _load(); }
  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final data = await widget.authState.accounting('reconciliation/inventory', query: accountingScope(widget.authState, asOf: DateTime(_asOf.year, _asOf.month, _asOf.day, 23, 59, 59), branchId: widget.authState.currentUser!.branch.id)) as Map<String, dynamic>;
      if (mounted) setState(() => _data = data);
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }
  @override
  Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(title: 'Inventory Control Reconciliation', subtitle: 'Batch valuation vs Inventory GL control account', actions: [
      OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _asOf); if (x != null) { setState(() => _asOf = x); _load(); } }, child: Text('As of ${shortDate(_asOf)}')),
      IconButton(onPressed: _load, icon: const Icon(Icons.refresh)),
    ]),
    Expanded(child: _loading ? const Center(child: CircularProgressIndicator()) : _error != null ? AccountsError(_error!, onRetry: _load) : _body()),
  ]);
  Widget _body() {
    final data = _data!;
    final diff = amount(data['difference']);
    return Center(child: Card(child: Padding(padding: const EdgeInsets.all(24), child: Column(mainAxisSize: MainAxisSize.min, children: [
      Text('Inventory valuation (batches): ${money(amount(data['inventoryValuation']))}'), const SizedBox(height: 6),
      Text('Inventory GL balance: ${money(amount(data['glBalance']))}'), const SizedBox(height: 12),
      Text(diff.abs() < .005 ? 'RECONCILED' : 'DIFFERENCE: ${money(diff)}', style: TextStyle(fontWeight: FontWeight.bold, color: diff.abs() < .005 ? Colors.green : Theme.of(context).colorScheme.error)),
      const SizedBox(height: 12),
      const Text('Note: cross-branch stock transfers move physical inventory without a per-branch GL entry, so a branch-scoped figure may show a transient difference; the consolidated (all-branch) total is authoritative.', textAlign: TextAlign.center),
    ]))));
  }
}

class CashBankReconciliationView extends StatefulWidget {
  const CashBankReconciliationView({required this.authState, super.key});
  final AuthState authState;
  @override
  State<CashBankReconciliationView> createState() => _CashBankReconciliationViewState();
}

class _CashBankReconciliationViewState extends State<CashBankReconciliationView> {
  var _loading = true;
  String? _error;
  DateTime _asOf = DateTime.now();
  Map<String, dynamic>? _data;
  @override
  void initState() { super.initState(); _load(); }
  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final data = await widget.authState.accounting('reconciliation/cash-bank', query: accountingScope(widget.authState, asOf: DateTime(_asOf.year, _asOf.month, _asOf.day, 23, 59, 59), branchId: widget.authState.currentUser!.branch.id)) as Map<String, dynamic>;
      if (mounted) setState(() => _data = data);
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }
  @override
  Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(title: 'Cash/Bank Control Reconciliation', subtitle: 'Financial account balances vs Cash/Bank GL control accounts', actions: [
      OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _asOf); if (x != null) { setState(() => _asOf = x); _load(); } }, child: Text('As of ${shortDate(_asOf)}')),
      IconButton(onPressed: _load, icon: const Icon(Icons.refresh)),
    ]),
    Expanded(child: _loading ? const Center(child: CircularProgressIndicator()) : _error != null ? AccountsError(_error!, onRetry: _load) : _body()),
  ]);
  Widget _body() {
    final data = _data!;
    final accounts = (data['accounts'] as List<dynamic>? ?? []).cast<Map<String, dynamic>>();
    final cashDiff = amount(data['cashDifference']), bankDiff = amount(data['bankDifference']);
    return ListView(padding: const EdgeInsets.all(20), children: [
      Wrap(spacing: 12, runSpacing: 12, children: [
        _SummaryCard('Cash', amount(data['cashOperationalTotal']), amount(data['glCashBalance']), cashDiff),
        _SummaryCard('Bank', amount(data['bankOperationalTotal']), amount(data['glBankBalance']), bankDiff),
      ]),
      const SizedBox(height: 16),
      horizontalTable(DataTable(columns: const [DataColumn(label: Text('Account')), DataColumn(label: Text('Type')), DataColumn(label: Text('Operational balance'))],
        rows: accounts.map((x) => DataRow(cells: [DataCell(Text('${x['financialAccountName']}')), DataCell(Text(enumName(x['accountType']))), DataCell(Text(money(amount(x['operationalBalance']))))])).toList())),
      const SizedBox(height: 12),
      const Text('Individual accounts cannot be split back out of a single GL Cash/Bank control account, so comparison is against the aggregate total by account type.'),
    ]);
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard(this.label, this.operational, this.gl, this.diff);
  final String label;
  final double operational, gl, diff;
  @override
  Widget build(BuildContext context) => SizedBox(width: 280, child: Card(child: Padding(padding: const EdgeInsets.all(14), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
    Text(label, style: Theme.of(context).textTheme.titleMedium), const Divider(),
    Text('Operational: ${money(operational)}'), Text('GL: ${money(gl)}'),
    Text(diff.abs() < .005 ? 'Reconciled' : 'Difference: ${money(diff)}', style: TextStyle(fontWeight: FontWeight.bold, color: diff.abs() < .005 ? Colors.green : Theme.of(context).colorScheme.error)),
  ]))));
}
