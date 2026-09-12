import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../auth/auth_state.dart';
import 'accounts_models.dart';
import 'accounts_widgets.dart';
import 'statements_view.dart' show accountingScope;

class CashFlowView extends StatefulWidget {
  const CashFlowView({required this.authState, super.key});
  final AuthState authState;
  @override
  State<CashFlowView> createState() => _CashFlowViewState();
}

class _CashFlowViewState extends State<CashFlowView> {
  var _loading = true;
  String? _error;
  DateTime _from = DateTime(DateTime.now().year, DateTime.now().month, 1), _to = DateTime.now();
  Map<String, dynamic>? _data;

  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final data = await widget.authState.accounting('cash-flow', query: accountingScope(widget.authState, from: _from, to: DateTime(_to.year, _to.month, _to.day, 23, 59, 59), branchId: widget.authState.currentUser!.branch.id)) as Map<String, dynamic>;
      if (mounted) setState(() => _data = data);
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }

  @override
  Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(title: 'Cash Flow Statement', subtitle: 'Indirect method: operating, investing, and financing activities', actions: [
      OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _from); if (x != null) setState(() => _from = x); }, child: Text('From ${shortDate(_from)}')),
      const SizedBox(width: 8),
      OutlinedButton(onPressed: () async { final x = await pickAccountDate(context, _to); if (x != null) setState(() => _to = x); }, child: Text('To ${shortDate(_to)}')),
      const SizedBox(width: 8), FilledButton.tonal(onPressed: _load, child: const Text('Apply')),
    ]),
    Expanded(child: _loading ? const Center(child: CircularProgressIndicator()) : _error != null ? AccountsError(_error!, onRetry: _load) : _body()),
  ]);

  Widget _body() {
    final data = _data!;
    List<Map<String, dynamic>> lines(String key) => (data[key] as List<dynamic>? ?? []).cast<Map<String, dynamic>>();
    return ListView(padding: const EdgeInsets.all(20), children: [
      _Section('Operating Activities', [{'label': 'Net Profit', 'amount': data['netProfit']}, ...lines('operatingAdjustments')], amount(data['netCashFromOperating'])),
      _Section('Investing Activities', lines('investingActivities'), amount(data['netCashFromInvesting'])),
      _Section('Financing Activities', lines('financingActivities'), amount(data['netCashFromFinancing'])),
      const Divider(),
      _Total('Net Change in Cash', amount(data['netChangeInCash']), strong: true),
      _Total('Opening Cash', amount(data['openingCash'])),
      _Total('Closing Cash', amount(data['closingCash']), strong: true),
    ]);
  }
}

class _Section extends StatelessWidget {
  const _Section(this.title, this.rows, this.net);
  final String title;
  final List<Map<String, dynamic>> rows;
  final double net;
  @override
  Widget build(BuildContext context) => Card(child: Padding(padding: const EdgeInsets.all(14), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
    Text(title, style: Theme.of(context).textTheme.titleMedium), const Divider(),
    if (rows.isEmpty) const Text('No activity') else for (final x in rows) Padding(padding: const EdgeInsets.symmetric(vertical: 4), child: Row(children: [
      Expanded(child: Text('${x['label']}')), Text(money(amount(x['amount']))),
    ])),
    const Divider(), Row(children: [Expanded(child: Text('Net cash from $title', style: const TextStyle(fontWeight: FontWeight.bold))), Text(money(net), style: const TextStyle(fontWeight: FontWeight.bold))]),
  ])));
}

class _Total extends StatelessWidget {
  const _Total(this.label, this.value, {this.strong = false});
  final String label;
  final double value;
  final bool strong;
  @override
  Widget build(BuildContext context) => Padding(padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8), child: Row(children: [
    Expanded(child: Text(label, style: strong ? Theme.of(context).textTheme.titleMedium : null)), Text(money(value), style: strong ? Theme.of(context).textTheme.titleMedium : null),
  ]));
}
