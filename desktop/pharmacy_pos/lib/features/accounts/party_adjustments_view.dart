import 'package:flutter/material.dart';
import '../../core/api_client.dart';
import '../auth/auth_state.dart';
import 'accounts_models.dart';
import 'accounts_widgets.dart';

class PartyAdjustmentsView extends StatefulWidget {
  const PartyAdjustmentsView({required this.authState, super.key});
  final AuthState authState;
  @override
  State<PartyAdjustmentsView> createState() => _PartyAdjustmentsViewState();
}
class _AdjustmentType {
  const _AdjustmentType(this.path, this.label, this.permission, this.supplier, this.advance);
  final String path, label, permission;
  final bool supplier, advance;
}
const _types = [
  _AdjustmentType('credit-notes', 'Customer credit note', 'accounts.credit_notes', false, false),
  _AdjustmentType('debit-notes', 'Supplier debit note', 'accounts.debit_notes', true, false),
  _AdjustmentType('customer-writeoffs', 'Customer write-off', 'accounts.writeoffs', false, false),
  _AdjustmentType('supplier-writeoffs', 'Supplier write-off', 'accounts.writeoffs', true, false),
  _AdjustmentType('customer-advances', 'Customer advance', 'accounts.advances', false, true),
  _AdjustmentType('supplier-advances', 'Supplier advance', 'accounts.advances', true, true),
];
class _PartyAdjustmentsViewState extends State<PartyAdjustmentsView> {
  late _AdjustmentType _type;
  List<Map<String, dynamic>> _rows = [];
  bool _loading = true;
  String? _error;
  List<_AdjustmentType> get _available => _types.where((t) => widget.authState.can('${t.permission}.view')).toList();
  @override
  void initState() { super.initState(); _type = _available.first; _load(); }
  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final rows = await widget.authState.accounting(_type.path, query: {'branchId': widget.authState.currentUser!.branch.id});
      if (mounted) setState(() => _rows = (rows as List).cast<Map<String, dynamic>>());
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }
  Future<void> _create() async {
    final saved = await showDialog<bool>(context: context, builder: (_) => _AdjustmentForm(authState: widget.authState, type: _type));
    if (saved == true) await _load();
  }
  @override
  Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(title: 'Party Adjustments', subtitle: 'Posted notes, write-offs and advances', actions: [
      DropdownButton<_AdjustmentType>(key: const Key('adjustment_type'), value: _type, items: _available.map((t) => DropdownMenuItem(value: t, child: Text(t.label))).toList(),
        onChanged: (t) { if (t != null) { _type = t; _load(); } }),
      if (widget.authState.can('${_type.permission}.create')) FilledButton(key: const Key('adjustment_create'), onPressed: _create, child: const Text('New adjustment')),
    ]),
    Expanded(child: _loading ? const Center(child: CircularProgressIndicator()) : _error != null ? AccountsError(_error!, onRetry: _load) :
      _rows.isEmpty ? const Center(child: Text('No adjustments in this branch.')) : horizontalTable(DataTable(columns: const [
        DataColumn(label: Text('Number')), DataColumn(label: Text('Party')), DataColumn(label: Text('Amount')), DataColumn(label: Text('Reason / Remaining')),
      ], rows: _rows.map((r) => DataRow(cells: [
        DataCell(Text('${r['creditNoteNumber'] ?? r['debitNoteNumber'] ?? r['writeOffNumber'] ?? r['advanceNumber']}')),
        DataCell(Text('${r['customerName'] ?? r['supplierName']}')), DataCell(Text(money(amount(r['amount'])))),
        DataCell(Text(_type.advance ? money(amount(r['amountRemaining'])) : '${r['reason']}')),
      ])).toList()))),
  ]);
}
class _AdjustmentForm extends StatefulWidget {
  const _AdjustmentForm({required this.authState, required this.type});
  final AuthState authState;
  final _AdjustmentType type;
  @override
  State<_AdjustmentForm> createState() => _AdjustmentFormState();
}
class _AdjustmentFormState extends State<_AdjustmentForm> {
  final _amount = TextEditingController(), _reason = TextEditingController();
  List<(String, String)> _parties = [], _accounts = [];
  String? _partyId, _accountId, _error;
  bool _loading = true, _saving = false;
  @override
  void initState() { super.initState(); _load(); }
  Future<void> _load() async {
    try {
      if (widget.type.supplier) {
        final parties = await widget.authState.listSuppliers(isActive: true);
        _parties = parties.items.map((p) => (p.id, p.name)).toList();
      } else {
        final parties = await widget.authState.lookupCustomers();
        _parties = parties.map((p) => (p.id, p.name)).toList();
      }
      if (widget.type.advance) {
        final accounts = await widget.authState.listFinancialAccounts(branchId: widget.authState.currentUser!.branch.id);
        _accounts = accounts.where((a) => a.isActive).map((a) => (a.id, a.name)).toList();
      }
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }
  @override
  void dispose() { _amount.dispose(); _reason.dispose(); super.dispose(); }
  Future<void> _post() async {
    final value = double.tryParse(_amount.text);
    if (_partyId == null || value == null || !value.isFinite || value <= 0 || (widget.type.advance ? _accountId == null : _reason.text.trim().isEmpty)) {
      setState(() => _error = 'Select a party, enter a positive amount and ${widget.type.advance ? 'select a financial account' : 'provide a reason'}.'); return;
    }
    setState(() { _saving = true; _error = null; });
    final now = DateTime.now().toUtc().toIso8601String();
    final body = <String, dynamic>{widget.type.supplier ? 'supplierId' : 'customerId': _partyId,
      'branchId': widget.authState.currentUser!.branch.id, 'amount': value, 'notes': null};
    if (widget.type.advance) {
      body.addAll({'financialAccountId': _accountId, widget.type.supplier ? 'paidDateUtc' : 'receivedDateUtc': now, 'referenceNumber': _reason.text.trim()});
    } else {
      body.addAll({'reason': _reason.text.trim(), widget.type.path.endsWith('writeoffs') ? 'writeOffDateUtc' : 'issueDateUtc': now,
        widget.type.supplier ? 'appliedToGoodsReceiptId' : 'appliedToSaleId': null});
    }
    try {
      await widget.authState.accounting(widget.type.path, method: 'POST', body: body);
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _saving = false); }
  }
  @override
  Widget build(BuildContext context) => AlertDialog(title: Text(widget.type.label), content: SizedBox(width: 440,
    child: _loading ? const Center(child: CircularProgressIndicator()) : SingleChildScrollView(child: Column(mainAxisSize: MainAxisSize.min, children: [
      DropdownButtonFormField<String>(key: const Key('adjustment_party'), initialValue: _partyId, isExpanded: true, decoration: const InputDecoration(labelText: 'Party'),
        items: _parties.map((p) => DropdownMenuItem(value: p.$1, child: Text(p.$2))).toList(), onChanged: (id) => setState(() => _partyId = id)),
      TextField(key: const Key('adjustment_amount'), controller: _amount, decoration: const InputDecoration(labelText: 'Amount')),
      TextField(key: const Key('adjustment_reason'), controller: _reason, decoration: InputDecoration(labelText: widget.type.advance ? 'Reference' : 'Reason')),
      if (widget.type.advance) DropdownButtonFormField<String>(key: const Key('adjustment_account'), initialValue: _accountId, isExpanded: true, decoration: const InputDecoration(labelText: 'Financial account'),
        items: _accounts.map((a) => DropdownMenuItem(value: a.$1, child: Text(a.$2))).toList(), onChanged: (id) => setState(() => _accountId = id)),
      if (_error != null) Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
    ]))), actions: [TextButton(onPressed: _saving ? null : () => Navigator.pop(context, false), child: const Text('Cancel')),
      FilledButton(key: const Key('adjustment_post'), onPressed: _loading || _saving ? null : _post, child: Text(_saving ? 'Posting…' : 'Post adjustment'))]);
}
