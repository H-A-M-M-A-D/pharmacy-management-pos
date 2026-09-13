import 'package:flutter/material.dart';
import '../../ui/app_widgets.dart';

import '../../core/api_client.dart';
import '../auth/auth_state.dart';
import 'accounts_models.dart';
import 'accounts_widgets.dart';
import 'statements_view.dart' show accountingScope;

class _MovementBookView extends StatefulWidget {
  const _MovementBookView({required this.authState, required this.endpoint, required this.title, required this.subtitle});
  final AuthState authState;
  final String endpoint, title, subtitle;
  @override
  State<_MovementBookView> createState() => _MovementBookViewState();
}

class _MovementBookViewState extends State<_MovementBookView> {
  var _loading = true, _page = 1;
  String? _error;
  DateTime _from = DateTime(DateTime.now().year, DateTime.now().month, 1), _to = DateTime.now();
  Map<String, dynamic>? _data;

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load({int page = 1}) async {
    setState(() { _loading = true; _error = null; _page = page; });
    final query = accountingScope(widget.authState, from: _from, to: DateTime(_to.year, _to.month, _to.day, 23, 59, 59), branchId: widget.authState.currentUser!.branch.id)
      ..addAll({'page': '$_page', 'pageSize': '50'});
    try {
      final data = await widget.authState.accounting(widget.endpoint, query: query) as Map<String, dynamic>;
      if (mounted) setState(() => _data = data);
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }

  @override
  Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(title: widget.title, subtitle: widget.subtitle, actions: [
      OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _from); if (x != null) setState(() => _from = x); }, child: Text('From ${shortDate(_from)}')),
      const SizedBox(width: 8),
      OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _to); if (x != null) setState(() => _to = x); }, child: Text('To ${shortDate(_to)}')),
      const SizedBox(width: 8),
      FilledButton.tonal(onPressed: () => _load(page: 1), child: const Text('Apply')),
    ]),
    Expanded(child: _body()),
  ]);

  Widget _body() {
    if (_loading) return const AppLoadingState();
    if (_error != null) return AccountsError(_error!, onRetry: _load);
    final lines = ((_data?['lines'] as Map<String, dynamic>?)?['items'] as List<dynamic>? ?? []).cast<Map<String, dynamic>>();
    final total = (_data?['lines'] as Map<String, dynamic>?)?['totalCount'] as int? ?? 0;
    return Column(children: [
      Padding(padding: const EdgeInsets.all(12), child: Wrap(spacing: 10, children: [
        Chip(label: Text('Opening  ${money(amount(_data?['openingBalance']))}')),
        Chip(label: Text('Receipts  ${money(amount(_data?['totalReceipts']))}')),
        Chip(label: Text('Payments  ${money(amount(_data?['totalPayments']))}')),
        Chip(label: Text('Closing  ${money(amount(_data?['closingBalance']))}')),
      ])),
      Expanded(child: lines.isEmpty ? AppEmptyState(title: 'No activity for this period.') : horizontalTable(AppDataTable(columns: const [
        DataColumn(label: Text('Date')), DataColumn(label: Text('Reference')), DataColumn(label: Text('Description')), DataColumn(label: Text('Source')),
        DataColumn(label: Text('Receipt')), DataColumn(label: Text('Payment')), DataColumn(label: Text('Balance'), numeric: true), DataColumn(label: Text('Posted by')),
      ], rows: lines.map((x) => DataRow(cells: [
        DataCell(Text(shortDate(DateTime.parse(x['dateUtc'] as String).toLocal()))), DataCell(Text('${x['reference']}')),
        DataCell(SizedBox(width: 200, child: Text('${x['description']}', overflow: TextOverflow.ellipsis))), DataCell(Text(enumName(x['sourceType']))),
        DataCell(Text(amount(x['receipt']) > 0 ? money(amount(x['receipt'])) : '-')), DataCell(Text(amount(x['payment']) > 0 ? money(amount(x['payment'])) : '-')),
        DataCell(Text(money(amount(x['runningBalance'])))), DataCell(Text('${x['postedBy']}')),
      ])).toList()))),
      Padding(padding: const EdgeInsets.all(8), child: Row(mainAxisAlignment: MainAxisAlignment.end, children: [
        IconButton(onPressed: _page > 1 ? () => _load(page: _page - 1) : null, icon: const Icon(Icons.chevron_left)),
        Text('Page $_page'), IconButton(onPressed: _page * 50 < total ? () => _load(page: _page + 1) : null, icon: const Icon(Icons.chevron_right)),
      ])),
    ]);
  }
}

class CashBookView extends StatelessWidget {
  const CashBookView({required this.authState, super.key});
  final AuthState authState;
  @override
  Widget build(BuildContext context) => _MovementBookView(authState: authState, endpoint: 'cash-book', title: 'Cash Book', subtitle: 'GL cash control account activity with running balance');
}

class BankBookView extends StatelessWidget {
  const BankBookView({required this.authState, super.key});
  final AuthState authState;
  @override
  Widget build(BuildContext context) => _MovementBookView(authState: authState, endpoint: 'bank-book', title: 'Bank Book', subtitle: 'GL bank control account activity with running balance');
}

class DayBookView extends StatefulWidget {
  const DayBookView({required this.authState, super.key});
  final AuthState authState;
  @override
  State<DayBookView> createState() => _DayBookViewState();
}

class _DayBookViewState extends State<DayBookView> {
  var _loading = true, _page = 1;
  String? _error;
  DateTime _from = DateTime.now(), _to = DateTime.now();
  Map<String, dynamic>? _data;
  @override
  void initState() { super.initState(); _load(); }
  Future<void> _load({int page = 1}) async {
    setState(() { _loading = true; _error = null; _page = page; });
    final query = accountingScope(widget.authState, from: _from, to: DateTime(_to.year, _to.month, _to.day, 23, 59, 59), branchId: widget.authState.currentUser!.branch.id)
      ..addAll({'page': '$_page', 'pageSize': '50'});
    try {
      final data = await widget.authState.accounting('day-book', query: query) as Map<String, dynamic>;
      if (mounted) setState(() => _data = data);
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }
  @override
  Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(title: 'Day Book', subtitle: 'All posted financial events, chronologically', actions: [
      OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _from); if (x != null) setState(() => _from = x); }, child: Text('From ${shortDate(_from)}')),
      const SizedBox(width: 8),
      OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _to); if (x != null) setState(() => _to = x); }, child: Text('To ${shortDate(_to)}')),
      const SizedBox(width: 8), FilledButton.tonal(onPressed: () => _load(page: 1), child: const Text('Apply')),
    ]),
    Expanded(child: _body()),
  ]);
  Widget _body() {
    if (_loading) return const AppLoadingState();
    if (_error != null) return AccountsError(_error!, onRetry: _load);
    final lines = ((_data?['lines'] as Map<String, dynamic>?)?['items'] as List<dynamic>? ?? []).cast<Map<String, dynamic>>();
    final total = (_data?['lines'] as Map<String, dynamic>?)?['totalCount'] as int? ?? 0;
    return Column(children: [
      Padding(padding: const EdgeInsets.all(12), child: Wrap(spacing: 10, children: [Chip(label: Text('Total debit  ${money(amount(_data?['totalDebit']))}')), Chip(label: Text('Total credit  ${money(amount(_data?['totalCredit']))}'))])),
      Expanded(child: lines.isEmpty ? AppEmptyState(title: 'No posted entries for this period.') : horizontalTable(AppDataTable(columns: const [
        DataColumn(label: Text('Date')), DataColumn(label: Text('Entry #')), DataColumn(label: Text('Source')), DataColumn(label: Text('Reference')),
        DataColumn(label: Text('Narration')), DataColumn(label: Text('Debit'), numeric: true), DataColumn(label: Text('Credit'), numeric: true), DataColumn(label: Text('Posted by')), DataColumn(label: Text('Branch')),
      ], rows: lines.map((x) => DataRow(cells: [
        DataCell(Text(shortDate(DateTime.parse(x['dateUtc'] as String).toLocal()))), DataCell(Text('${x['entryNumber']}')), DataCell(Text(enumName(x['sourceType']))),
        DataCell(Text('${x['reference'] ?? '-'}')), DataCell(SizedBox(width: 200, child: Text('${x['description']}', overflow: TextOverflow.ellipsis))),
        DataCell(Text(money(amount(x['totalDebit'])))), DataCell(Text(money(amount(x['totalCredit'])))), DataCell(Text('${x['postedBy']}')), DataCell(Text('${x['branchName']}')),
      ])).toList()))),
      Padding(padding: const EdgeInsets.all(8), child: Row(mainAxisAlignment: MainAxisAlignment.end, children: [
        IconButton(onPressed: _page > 1 ? () => _load(page: _page - 1) : null, icon: const Icon(Icons.chevron_left)),
        Text('Page $_page'), IconButton(onPressed: _page * 50 < total ? () => _load(page: _page + 1) : null, icon: const Icon(Icons.chevron_right)),
      ])),
    ]);
  }
}
