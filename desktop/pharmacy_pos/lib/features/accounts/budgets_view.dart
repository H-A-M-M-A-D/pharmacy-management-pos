import 'package:flutter/material.dart';
import '../../core/api_client.dart';
import '../auth/auth_state.dart';
import 'accounts_models.dart';
import 'accounts_widgets.dart';

class BudgetsView extends StatefulWidget {
  const BudgetsView({required this.authState, super.key});
  final AuthState authState;
  @override
  State<BudgetsView> createState() => _BudgetsViewState();
}

class _BudgetsViewState extends State<BudgetsView> {
  int _year = DateTime.now().year;
  bool _loading = true;
  String? _error;
  List<Map<String, dynamic>> _budgets = [];
  @override
  void initState() { super.initState(); _load(); }
  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final data = await widget.authState.accounting('budgets', query: {'fiscalYear': '$_year', 'branchId': widget.authState.currentUser!.branch.id});
      if (mounted) setState(() => _budgets = (data as List).cast<Map<String, dynamic>>());
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }
  Future<void> _edit([Map<String, dynamic>? budget]) async {
    final saved = await showDialog<bool>(context: context, builder: (_) => _BudgetForm(authState: widget.authState, year: _year, budget: budget));
    if (saved == true) await _load();
  }
  @override
  Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(title: 'Budgets', subtitle: 'Fiscal year $_year', actions: [
      IconButton(tooltip: 'Previous year', onPressed: () { _year--; _load(); }, icon: const Icon(Icons.chevron_left)),
      IconButton(tooltip: 'Next year', onPressed: () { _year++; _load(); }, icon: const Icon(Icons.chevron_right)),
      IconButton(tooltip: 'Refresh', onPressed: _load, icon: const Icon(Icons.refresh)),
      if (widget.authState.can('accounts.budgets.manage')) FilledButton(key: const Key('budget_create'), onPressed: _edit, child: const Text('New budget')),
    ]),
    Expanded(child: _loading ? const Center(child: CircularProgressIndicator()) : _error != null ? AccountsError(_error!, onRetry: _load) :
      _budgets.isEmpty ? const Center(child: Text('No budgets for this fiscal year.')) : horizontalTable(DataTable(columns: const [
        DataColumn(label: Text('Account')), DataColumn(label: Text('Period')), DataColumn(label: Text('Budget')), DataColumn(label: Text('Actions')),
      ], rows: _budgets.map((b) => DataRow(cells: [
        DataCell(Text('${b['accountCode']} ${b['accountName']}')), DataCell(Text('${b['periodNumber'] ?? 'Annual'}')),
        DataCell(Text(money(amount(b['budgetAmount'])))), DataCell(widget.authState.can('accounts.budgets.manage') ?
          TextButton(key: Key('budget_edit_${b['id']}'), onPressed: () => _edit(b), child: const Text('Edit')) : const SizedBox.shrink()),
      ])).toList()))),
  ]);
}

class _BudgetForm extends StatefulWidget {
  const _BudgetForm({required this.authState, required this.year, this.budget});
  final AuthState authState;
  final int year;
  final Map<String, dynamic>? budget;
  @override
  State<_BudgetForm> createState() => _BudgetFormState();
}
class _BudgetFormState extends State<_BudgetForm> {
  final _amount = TextEditingController(), _period = TextEditingController(), _notes = TextEditingController();
  List<AccountInfo> _accounts = [];
  String? _accountId, _error;
  bool _loading = true, _saving = false;
  @override
  void initState() {
    super.initState();
    _accountId = widget.budget?['chartOfAccountId'] as String?;
    _amount.text = '${widget.budget?['budgetAmount'] ?? ''}';
    _period.text = '${widget.budget?['periodNumber'] ?? ''}';
    _notes.text = '${widget.budget?['notes'] ?? ''}';
    _load();
  }
  Future<void> _load() async {
    try {
      final data = await widget.authState.accounting('chart', query: {'includeInactive': 'false'}) as List;
      if (mounted) setState(() => _accounts = data.map((x) => AccountInfo.fromJson(x as Map<String, dynamic>)).where((x) => x.isActive && x.isPostingAccount).toList());
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }
  @override
  void dispose() { _amount.dispose(); _period.dispose(); _notes.dispose(); super.dispose(); }
  Future<void> _save() async {
    final value = double.tryParse(_amount.text), period = int.tryParse(_period.text);
    if (_accountId == null || value == null || !value.isFinite || value < 0 || (_period.text.isNotEmpty && (period == null || period < 1 || period > 12))) {
      setState(() => _error = 'Select an account, enter a non-negative budget and a period from 1 to 12 or leave it blank.'); return;
    }
    setState(() { _saving = true; _error = null; });
    try {
      await widget.authState.accounting('budgets', method: 'POST', body: {'fiscalYear': widget.year, 'periodNumber': period, 'chartOfAccountId': _accountId,
        'branchId': widget.budget?['branchId'] ?? widget.authState.currentUser!.branch.id, 'budgetAmount': value, 'notes': _notes.text.trim()});
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _saving = false); }
  }
  @override
  Widget build(BuildContext context) => AlertDialog(title: Text(widget.budget == null ? 'New budget' : 'Edit budget'), content: SizedBox(width: 440,
    child: _loading ? const Center(child: CircularProgressIndicator()) : SingleChildScrollView(child: Column(mainAxisSize: MainAxisSize.min, children: [
      DropdownButtonFormField<String>(key: const Key('budget_account'), initialValue: _accountId, isExpanded: true, decoration: const InputDecoration(labelText: 'Account'),
        items: _accounts.map((a) => DropdownMenuItem(value: a.id, child: Text('${a.code} ${a.name}'))).toList(), onChanged: widget.budget == null ? (id) => setState(() => _accountId = id) : null),
      TextField(key: const Key('budget_amount'), controller: _amount, decoration: const InputDecoration(labelText: 'Budget amount')),
      TextField(key: const Key('budget_period'), controller: _period, enabled: widget.budget == null, decoration: const InputDecoration(labelText: 'Period (blank for annual)')),
      TextField(controller: _notes, decoration: const InputDecoration(labelText: 'Notes')),
      if (_error != null) Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
    ]))), actions: [TextButton(onPressed: _saving ? null : () => Navigator.pop(context, false), child: const Text('Cancel')),
      FilledButton(key: const Key('budget_save'), onPressed: _loading || _saving ? null : _save, child: Text(_saving ? 'Saving…' : 'Save budget'))]);
}
