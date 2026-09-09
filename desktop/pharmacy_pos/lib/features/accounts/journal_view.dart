import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../auth/auth_state.dart';
import 'accounts_models.dart';
import 'accounts_widgets.dart';

class JournalView extends StatefulWidget {
  const JournalView({required this.authState, super.key});
  final AuthState authState;
  @override
  State<JournalView> createState() => _JournalViewState();
}

class _JournalViewState extends State<JournalView> {
  final _search = TextEditingController();
  var _loading = true, _page = 1, _total = 0;
  String? _error, _source;
  DateTime? _from, _to;
  List<JournalListItem> _items = [];
  @override
  void initState() { super.initState(); _load(); }
  @override
  void dispose() { _search.dispose(); super.dispose(); }

  Future<void> _load({int? page}) async {
    setState(() { _loading = true; _error = null; _page = page ?? _page; });
    final query = <String, String>{'page': '$_page', 'pageSize': '25'};
    if (_search.text.trim().isNotEmpty) query['search'] = _search.text.trim();
    if (_source != null) query['sourceType'] = _source!;
    if (_from != null) query['fromUtc'] = DateTime(_from!.year, _from!.month, _from!.day).toUtc().toIso8601String();
    if (_to != null) query['toUtc'] = DateTime(_to!.year, _to!.month, _to!.day, 23, 59, 59).toUtc().toIso8601String();
    try {
      final data = await widget.authState.accounting('journal', query: query) as Map<String, dynamic>;
      if (mounted) setState(() {
        _items = (data['items'] as List<dynamic>? ?? []).map((x) => JournalListItem.fromJson(x as Map<String, dynamic>)).toList();
        _total = data['totalCount'] as int? ?? 0;
      });
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }

  @override
  Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(title: 'Journal / Vouchers', subtitle: 'Immutable journal history and manual journal vouchers', actions: [
      IconButton(tooltip: 'Refresh', onPressed: _load, icon: const Icon(Icons.refresh)),
      if (widget.authState.can('accounts.journal.post')) FilledButton.icon(key: const Key('manual_journal_create'), onPressed: _manual, icon: const Icon(Icons.post_add), label: const Text('Manual journal')),
    ]),
    Padding(padding: const EdgeInsets.symmetric(horizontal: 20), child: Wrap(spacing: 10, runSpacing: 10, crossAxisAlignment: WrapCrossAlignment.center, children: [
      SizedBox(width: 240, child: TextField(key: const Key('journal_search'), controller: _search, decoration: const InputDecoration(labelText: 'Reference or description', prefixIcon: Icon(Icons.search)), onSubmitted: (_) => _load(page: 1))),
      DropdownButton<String?>(value: _source, hint: const Text('All source types'), items: [const DropdownMenuItem<String?>(value: null, child: Text('All source types')), ..._sources.map((x) => DropdownMenuItem(value: x, child: Text(x)))], onChanged: (x) => setState(() => _source = x)),
      OutlinedButton.icon(onPressed: () async { final x = await pickAccountDate(context, _from ?? DateTime.now()); if (x != null) setState(() => _from = x); }, icon: const Icon(Icons.date_range), label: Text(_from == null ? 'From date' : shortDate(_from!))),
      OutlinedButton.icon(onPressed: () async { final x = await pickAccountDate(context, _to ?? DateTime.now()); if (x != null) setState(() => _to = x); }, icon: const Icon(Icons.event), label: Text(_to == null ? 'To date' : shortDate(_to!))),
      FilledButton.tonal(onPressed: () => _load(page: 1), child: const Text('Apply')),
    ])),
    const SizedBox(height: 8), Expanded(child: _body()),
  ]);

  Widget _body() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) return AccountsError(_error!, onRetry: _load);
    if (_items.isEmpty) return const Center(child: Text('No journal entries match these filters.'));
    return Column(children: [
      Expanded(child: horizontalTable(DataTable(columns: const [
        DataColumn(label: Text('Entry #')), DataColumn(label: Text('Date')), DataColumn(label: Text('Source')),
        DataColumn(label: Text('Reference')), DataColumn(label: Text('Description')), DataColumn(label: Text('Branch')),
        DataColumn(label: Text('Debit')), DataColumn(label: Text('Credit')), DataColumn(label: Text('Status')),
      ], rows: _items.map((x) => DataRow(onSelectChanged: (_) => _details(x), cells: [
        DataCell(Text(x.number)), DataCell(Text(shortDate(x.date))), DataCell(Text(x.sourceType)), DataCell(Text(x.reference ?? '-')),
        DataCell(SizedBox(width: 220, child: Text(x.description, overflow: TextOverflow.ellipsis))), DataCell(Text(x.branchName)),
        DataCell(Text(money(x.totalDebit))), DataCell(Text(money(x.totalDebit))), const DataCell(Text('Posted')),
      ])).toList()))),
      Padding(padding: const EdgeInsets.all(10), child: Row(mainAxisAlignment: MainAxisAlignment.end, children: [
        Text('${(_page - 1) * 25 + 1}–${(_page * 25).clamp(0, _total)} of $_total'), const SizedBox(width: 10),
        IconButton(onPressed: _page > 1 ? () => _load(page: _page - 1) : null, icon: const Icon(Icons.chevron_left)),
        IconButton(onPressed: _page * 25 < _total ? () => _load(page: _page + 1) : null, icon: const Icon(Icons.chevron_right)),
      ])),
    ]);
  }

  Future<void> _details(JournalListItem item) async {
    try {
      final data = await widget.authState.accounting('journal/${item.id}') as Map<String, dynamic>;
      if (!mounted) return;
      await showDialog<void>(context: context, builder: (_) => _JournalDetailsDialog(details: JournalDetails.fromJson(data)));
    } on ApiException catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); }
  }

  Future<void> _manual() async {
    try {
      final data = await widget.authState.accounting('chart', query: {'includeInactive': 'false'}) as List<dynamic>;
      final accounts = data.map((x) => AccountInfo.fromJson(x as Map<String, dynamic>)).where((x) => x.isPostingAccount && x.isActive).toList();
      if (!mounted) return;
      final posted = await showDialog<bool>(context: context, builder: (_) => _ManualJournalDialog(authState: widget.authState, accounts: accounts));
      if (posted == true) await _load(page: 1);
    } on ApiException catch (e) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message))); }
  }

  static const _sources = ['Sale','SalesReturn','Purchase','PurchaseReturn','CustomerPayment','SupplierPayment','Expense','OtherIncome','CashTransfer','StockWriteOff','StockAdjustment','OpeningBalance','ManualVoucher','CustomerAdjustment','SupplierAdjustment','FinancialAccountAdjustment','CashierDrawerEntry','CashierShiftVariance'];
}

class _JournalDetailsDialog extends StatelessWidget {
  const _JournalDetailsDialog({required this.details});
  final JournalDetails details;
  @override
  Widget build(BuildContext context) => AlertDialog(
    key: const Key('posted_journal_read_only'),
    title: Text('${details.number} · ${details.status}'),
    content: SizedBox(width: 850, child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text('${shortDate(details.date)}  ·  ${details.sourceType}  ·  ${details.branchName}'),
      Text('${details.reference ?? 'No reference'}  ·  Posted by ${details.postedBy}'), const SizedBox(height: 8), Text(details.description), const Divider(),
      SingleChildScrollView(scrollDirection: Axis.horizontal, child: DataTable(columns: const [
        DataColumn(label: Text('Account')), DataColumn(label: Text('Description')), DataColumn(label: Text('Party')),
        DataColumn(label: Text('Debit')), DataColumn(label: Text('Credit')),
      ], rows: details.lines.map((x) => DataRow(cells: [
        DataCell(Text('${x.code} · ${x.name}')), DataCell(Text(x.description ?? '-')), DataCell(Text(x.customerName ?? x.supplierName ?? '-')),
        DataCell(Text(money(x.debit))), DataCell(Text(money(x.credit))),
      ])).toList())),
      const Divider(), Align(alignment: Alignment.centerRight, child: Text('Debit ${money(details.totalDebit)}     Credit ${money(details.totalCredit)}', style: Theme.of(context).textTheme.titleMedium)),
      const SizedBox(height: 8), const Row(children: [Icon(Icons.lock_outline, size: 17), SizedBox(width: 6), Text('Posted journals are permanent and read-only.')]),
    ])),
    actions: [FilledButton(onPressed: () => Navigator.pop(context), child: const Text('Close'))],
  );
}

class _ManualJournalDialog extends StatefulWidget {
  const _ManualJournalDialog({required this.authState, required this.accounts});
  final AuthState authState;
  final List<AccountInfo> accounts;
  @override
  State<_ManualJournalDialog> createState() => _ManualJournalDialogState();
}

class _ManualJournalDialogState extends State<_ManualJournalDialog> {
  final _description = TextEditingController(), _reference = TextEditingController();
  final List<_JournalDraftLine> _lines = [_JournalDraftLine(), _JournalDraftLine()];
  var _date = DateTime.now(); bool _saving = false; String? _error;
  @override
  void dispose() { _description.dispose(); _reference.dispose(); for (final x in _lines) { x.dispose(); } super.dispose(); }
  double get debit => _lines.fold(0, (v, x) => v + (double.tryParse(x.debit.text) ?? 0));
  double get credit => _lines.fold(0, (v, x) => v + (double.tryParse(x.credit.text) ?? 0));
  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Post Manual Journal'),
    content: SizedBox(width: 900, child: SingleChildScrollView(child: Column(mainAxisSize: MainAxisSize.min, children: [
      Row(children: [Expanded(child: TextField(key: const Key('manual_journal_description'), controller: _description, decoration: const InputDecoration(labelText: 'Description *'))), const SizedBox(width: 12), Expanded(child: TextField(controller: _reference, decoration: const InputDecoration(labelText: 'Reference'))), const SizedBox(width: 12), OutlinedButton.icon(onPressed: () async { final x = await pickAccountDate(context, _date); if (x != null) setState(() => _date = x); }, icon: const Icon(Icons.event), label: Text(shortDate(_date)))]),
      const SizedBox(height: 14),
      for (var i = 0; i < _lines.length; i++) Padding(padding: const EdgeInsets.only(bottom: 8), child: Row(children: [
        Expanded(flex: 3, child: DropdownButtonFormField<String>(key: Key('manual_line_account_$i'), initialValue: _lines[i].accountId, decoration: const InputDecoration(labelText: 'Posting account'), isExpanded: true, items: widget.accounts.map((x) => DropdownMenuItem(value: x.id, child: Text('${x.code} · ${x.name}', overflow: TextOverflow.ellipsis))).toList(), onChanged: (x) => setState(() => _lines[i].accountId = x))),
        const SizedBox(width: 8), Expanded(child: TextField(key: Key('manual_line_debit_$i'), controller: _lines[i].debit, keyboardType: const TextInputType.numberWithOptions(decimal: true), decoration: const InputDecoration(labelText: 'Debit'), onChanged: (_) => setState(() {}))),
        const SizedBox(width: 8), Expanded(child: TextField(key: Key('manual_line_credit_$i'), controller: _lines[i].credit, keyboardType: const TextInputType.numberWithOptions(decimal: true), decoration: const InputDecoration(labelText: 'Credit'), onChanged: (_) => setState(() {}))),
        const SizedBox(width: 8), Expanded(flex: 2, child: TextField(controller: _lines[i].description, decoration: const InputDecoration(labelText: 'Line description'))),
        IconButton(onPressed: _lines.length > 2 ? () { setState(() { final removed = _lines.removeAt(i); removed.dispose(); }); } : null, icon: const Icon(Icons.remove_circle_outline)),
      ])),
      Align(alignment: Alignment.centerLeft, child: TextButton.icon(onPressed: () => setState(() => _lines.add(_JournalDraftLine())), icon: const Icon(Icons.add), label: const Text('Add line'))),
      Card(child: Padding(padding: const EdgeInsets.all(12), child: Row(mainAxisAlignment: MainAxisAlignment.end, children: [Text('Total debit: ${money(debit)}'), const SizedBox(width: 28), Text('Total credit: ${money(credit)}'), const SizedBox(width: 18), Icon(debit > 0 && (debit - credit).abs() < .005 ? Icons.check_circle : Icons.error_outline, color: debit > 0 && (debit - credit).abs() < .005 ? Colors.green : Theme.of(context).colorScheme.error)]))),
      if (_error != null) Padding(padding: const EdgeInsets.only(top: 10), child: Text(_error!, key: const Key('manual_journal_error'), style: TextStyle(color: Theme.of(context).colorScheme.error))),
    ]))),
    actions: [TextButton(onPressed: _saving ? null : () => Navigator.pop(context, false), child: const Text('Cancel')), FilledButton(key: const Key('manual_journal_post'), onPressed: _saving ? null : _post, child: Text(_saving ? 'Posting…' : 'Review & post'))],
  );

  Future<void> _post() async {
    final meaningful = _lines.where((x) => (double.tryParse(x.debit.text) ?? 0) > 0 || (double.tryParse(x.credit.text) ?? 0) > 0).toList();
    String? validation;
    if (_description.text.trim().isEmpty) {
      validation = 'Description is required.';
    } else if (meaningful.length < 2) {
      validation = 'At least two meaningful lines are required.';
    } else if (meaningful.any((x) => x.accountId == null)) {
      validation = 'Select an account for every line.';
    } else if (meaningful.any((x) {
      final d = double.tryParse(x.debit.text) ?? 0, c = double.tryParse(x.credit.text) ?? 0;
      return d < 0 || c < 0 || (d > 0 && c > 0);
    })) {
      validation = 'Each line must contain one non-negative debit or credit.';
    } else if (debit <= 0 || (debit - credit).abs() >= .005) {
      validation = 'Total debit must exactly equal total credit.';
    }
    if (validation != null) {
      setState(() => _error = validation);
      return;
    }
    final confirmed = await showDialog<bool>(context: context, builder: (context) => AlertDialog(title: const Text('Post this journal?'), content: Text('Debit ${money(debit)} and credit ${money(credit)}. This cannot be edited or deleted after posting.'), actions: [TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Back')), FilledButton(key: const Key('confirm_manual_journal'), onPressed: () => Navigator.pop(context, true), child: const Text('Post permanently'))]));
    if (confirmed != true) return;
    setState(() { _saving = true; _error = null; });
    try {
      await widget.authState.accounting('journal', method: 'POST', body: {
        'entryDateUtc': _date.toUtc().toIso8601String(), 'branchId': widget.authState.currentUser!.branch.id,
        'reference': _reference.text.trim(), 'description': _description.text.trim(),
        'lines': meaningful.map((x) => {'chartOfAccountId': x.accountId, 'debit': double.tryParse(x.debit.text) ?? 0, 'credit': double.tryParse(x.credit.text) ?? 0, 'description': x.description.text.trim()}).toList(),
      });
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _saving = false); }
  }
}

class _JournalDraftLine {
  String? accountId;
  final debit = TextEditingController(), credit = TextEditingController(), description = TextEditingController();
  void dispose() { debit.dispose(); credit.dispose(); description.dispose(); }
}
