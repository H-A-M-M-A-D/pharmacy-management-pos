import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../auth/auth_state.dart';
import 'accounts_models.dart';
import 'accounts_widgets.dart';

Map<String, String> accountingScope(AuthState auth, {DateTime? from, DateTime? to, DateTime? asOf, String? branchId}) {
  final query = <String, String>{};
  if (from != null) query['fromUtc'] = from.toUtc().toIso8601String();
  if (to != null) query['toUtc'] = to.toUtc().toIso8601String();
  if (asOf != null) query['asOfUtc'] = asOf.toUtc().toIso8601String();
  if (branchId != null) query['branchId'] = branchId;
  return query;
}

bool canAllBranches(AuthState auth) => auth.currentUser!.roles.any((x) => x.name == 'Owner' || x.name == 'Manager');

class AccountsDashboard extends StatefulWidget {
  const AccountsDashboard({required this.authState, super.key});
  final AuthState authState;
  @override State<AccountsDashboard> createState() => _AccountsDashboardState();
}
class _AccountsDashboardState extends State<AccountsDashboard> {
  bool _loading = true; String? _error; Map<String, dynamic>? _profit, _balance;
  @override void initState() { super.initState(); _load(); }
  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    final now = DateTime.now(), from = DateTime(DateTime.now().year, DateTime.now().month, 1);
    final branch = widget.authState.currentUser!.branch.id;
    try {
      final data = await Future.wait([
        widget.authState.accounting('profit-loss', query: accountingScope(widget.authState, from: from, to: now, branchId: branch)),
        widget.authState.accounting('balance-sheet', query: accountingScope(widget.authState, asOf: now, branchId: branch)),
      ]);
      if (mounted) setState(() { _profit = data[0] as Map<String, dynamic>; _balance = data[1] as Map<String, dynamic>; });
    } on ApiException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _loading = false); }
  }
  double _sumRows(String key, bool Function(Map<String, dynamic>) match) =>
    ((_balance?[key] as List<dynamic>?) ?? []).whereType<Map<String, dynamic>>().where(match).fold(0, (v, x) => v + amount(x['amount']));
  @override Widget build(BuildContext context) => Column(children: [
    AccountsPageHeader(title: 'Accounts Dashboard', subtitle: 'Current branch balances and month-to-date journal results', actions: [IconButton(onPressed: _load, tooltip: 'Refresh', icon: const Icon(Icons.refresh))]),
    Expanded(child: _loading ? const Center(child: CircularProgressIndicator()) : _error != null ? AccountsError(_error!, onRetry: _load) : ListView(padding: const EdgeInsets.all(20), children: [
      Wrap(spacing: 12, runSpacing: 12, children: [
        _Metric('Cash & Bank', _sumRows('assets', (x) => '${x['accountName']}'.toLowerCase().contains('cash') || '${x['accountName']}'.toLowerCase().contains('bank')), Icons.account_balance_wallet_outlined),
        _Metric('Accounts Receivable', _sumRows('assets', (x) => '${x['accountName']}'.toLowerCase().contains('receivable')), Icons.people_outline),
        _Metric('Inventory', _sumRows('assets', (x) => '${x['accountName']}'.toLowerCase() == 'inventory'), Icons.inventory_2_outlined),
        _Metric('Accounts Payable', _sumRows('liabilities', (x) => '${x['accountName']}'.toLowerCase().contains('payable')), Icons.local_shipping_outlined),
        _Metric('Net Revenue', amount(_profit?['netRevenue']), Icons.trending_up),
        _Metric('Gross Profit', amount(_profit?['grossProfit']), Icons.show_chart),
        _Metric('Expenses', amount(_profit?['totalOperatingExpenses']), Icons.receipt_long_outlined),
        _Metric('Net Profit', amount(_profit?['netProfit']), Icons.savings_outlined),
      ]),
      const SizedBox(height: 20),
      if (_balance?['isBalanced'] != true) Card(color: Theme.of(context).colorScheme.errorContainer, child: const ListTile(leading: Icon(Icons.warning_amber), title: Text('Balance sheet is out of balance'), subtitle: Text('Review journal postings before relying on these figures.'))),
      const Card(child: ListTile(leading: Icon(Icons.info_outline), title: Text('Accounting source of truth'), subtitle: Text('Every figure on this dashboard comes from posted journal lines. Operational POS widgets are not used.'))),
    ])),
  ]);
}
class _Metric extends StatelessWidget {
  const _Metric(this.label, this.value, this.icon); final String label; final double value; final IconData icon;
  @override Widget build(BuildContext context) => SizedBox(width: 210, height: 112, child: Card(child: Padding(padding: const EdgeInsets.all(14), child: Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [Icon(icon, size: 20), Text(label), Text(money(value), style: Theme.of(context).textTheme.titleMedium)]))));
}

class GeneralLedgerView extends StatefulWidget {
  const GeneralLedgerView({required this.authState, super.key}); final AuthState authState;
  @override State<GeneralLedgerView> createState() => _GeneralLedgerViewState();
}
class _GeneralLedgerViewState extends State<GeneralLedgerView> {
  bool _loading = true; String? _error, _accountId, _source; List<AccountInfo> _accounts = []; Map<String, dynamic>? _ledger;
  DateTime _from = DateTime(DateTime.now().year, DateTime.now().month, 1), _to = DateTime.now(); int _page = 1;
  @override void initState() { super.initState(); _initialize(); }
  Future<void> _initialize() async {
    try { final data = await widget.authState.accounting('chart', query: {'includeInactive':'false'}) as List<dynamic>; _accounts = data.map((x) => AccountInfo.fromJson(x as Map<String,dynamic>)).where((x) => x.isPostingAccount).toList(); if (_accounts.isNotEmpty) _accountId = _accounts.first.id; await _load(); }
    on ApiException catch(e) { if(mounted) setState(() { _error=e.message; _loading=false; }); }
  }
  Future<void> _load({int page = 1}) async {
    if (_accountId == null) { if(mounted) setState(() => _loading=false); return; }
    setState(() { _loading=true; _error=null; _page=page; });
    final query = accountingScope(widget.authState, from: DateTime(_from.year,_from.month,_from.day), to: DateTime(_to.year,_to.month,_to.day,23,59,59), branchId: widget.authState.currentUser!.branch.id)
      ..addAll({'chartOfAccountId':_accountId!, 'page':'$_page','pageSize':'50'});
    if (_source != null) query['sourceType']=_source!;
    try { final data=await widget.authState.accounting('general-ledger',query:query) as Map<String,dynamic>; if(mounted)setState(()=>_ledger=data); }
    on ApiException catch(e){if(mounted)setState(()=>_error=e.message);} finally{if(mounted)setState(()=>_loading=false);}
  }
  @override Widget build(BuildContext context) => Column(children:[
    const AccountsPageHeader(title:'General Ledger',subtitle:'Server-filtered account activity with running balance'),
    Padding(padding:const EdgeInsets.symmetric(horizontal:20),child:Wrap(spacing:10,runSpacing:10,crossAxisAlignment:WrapCrossAlignment.center,children:[
      SizedBox(width:300,child:DropdownButtonFormField<String>(key:const Key('ledger_account'),initialValue:_accountId,isExpanded:true,decoration:const InputDecoration(labelText:'Account'),items:_accounts.map((x)=>DropdownMenuItem(value:x.id,child:Text('${x.code} · ${x.name}',overflow:TextOverflow.ellipsis))).toList(),onChanged:(x)=>setState(()=>_accountId=x))),
      OutlinedButton(onPressed:()async{final x=await pickAccountDate(context,_from);if(x!=null)setState(()=>_from=x);},child:Text('From ${shortDate(_from)}')),
      OutlinedButton(onPressed:()async{final x=await pickAccountDate(context,_to);if(x!=null)setState(()=>_to=x);},child:Text('To ${shortDate(_to)}')),
      DropdownButton<String?>(value:_source,hint:const Text('All sources'),items:[const DropdownMenuItem<String?>(value:null,child:Text('All sources')),for(final x in const ['Sale','SalesReturn','Purchase','PurchaseReturn','CustomerPayment','SupplierPayment','Expense','OtherIncome','CashTransfer','ManualVoucher'])DropdownMenuItem(value:x,child:Text(x))],onChanged:(x)=>setState(()=>_source=x)),
      FilledButton.icon(onPressed:_load,icon:const Icon(Icons.filter_alt_outlined),label:const Text('Apply')),
    ])),const SizedBox(height:8),Expanded(child:_ledgerBody()),
  ]);
  Widget _ledgerBody(){if(_loading)return const Center(child:CircularProgressIndicator());if(_error!=null)return AccountsError(_error!,onRetry:_load);if(_ledger==null)return const Center(child:Text('Select an account.'));final entries=_ledger!['entries'] as Map<String,dynamic>;final rows=(entries['items'] as List<dynamic>? ?? []).cast<Map<String,dynamic>>();return Column(children:[
    Padding(padding:const EdgeInsets.all(12),child:Wrap(spacing:10,children:[_Summary('Opening',amount(_ledger!['openingBalance'])),_Summary('Debit',amount(_ledger!['totalDebit'])),_Summary('Credit',amount(_ledger!['totalCredit'])),_Summary('Closing',amount(_ledger!['closingBalance']))])),
    Expanded(child:rows.isEmpty?const Center(child:Text('No ledger activity for this period.')):horizontalTable(DataTable(columns:const[DataColumn(label:Text('Date')),DataColumn(label:Text('Entry #')),DataColumn(label:Text('Source')),DataColumn(label:Text('Reference')),DataColumn(label:Text('Description')),DataColumn(label:Text('Debit')),DataColumn(label:Text('Credit')),DataColumn(label:Text('Running Balance'))],rows:rows.map((x)=>DataRow(cells:[DataCell(Text(shortDate(DateTime.parse(x['entryDateUtc'] as String).toLocal()))),DataCell(Text('${x['entryNumber']}')),DataCell(Text(enumName(x['sourceType']))),DataCell(Text('${x['reference']??'-'}')),DataCell(SizedBox(width:220,child:Text('${x['description']}',overflow:TextOverflow.ellipsis))),DataCell(Text(money(amount(x['debit'])))),DataCell(Text(money(amount(x['credit'])))),DataCell(Text(money(amount(x['runningBalance']))))])).toList()))),
    Padding(padding:const EdgeInsets.all(8),child:Row(mainAxisAlignment:MainAxisAlignment.end,children:[IconButton(onPressed:_page>1?()=>_load(page:_page-1):null,icon:const Icon(Icons.chevron_left)),Text('Page $_page'),IconButton(onPressed:_page*50<(entries['totalCount'] as int? ?? 0)?()=>_load(page:_page+1):null,icon:const Icon(Icons.chevron_right))])),
  ]);}
}
class _Summary extends StatelessWidget{const _Summary(this.label,this.value);final String label;final double value;@override Widget build(BuildContext context)=>Chip(label:Text('$label  ${money(value)}'));}

class TrialBalanceView extends StatefulWidget {const TrialBalanceView({required this.authState,super.key});final AuthState authState;@override State<TrialBalanceView> createState()=>_TrialBalanceViewState();}
class _TrialBalanceViewState extends State<TrialBalanceView>{bool _loading=true;String? _error;DateTime _asOf=DateTime.now();List<TrialBalanceRow> _rows=[];double _debit=0,_credit=0;@override void initState(){super.initState();_load();}Future<void>_load()async{setState((){_loading=true;_error=null;});try{final data=await widget.authState.accounting('trial-balance',query:accountingScope(widget.authState,asOf:DateTime(_asOf.year,_asOf.month,_asOf.day,23,59,59),branchId:widget.authState.currentUser!.branch.id))as Map<String,dynamic>;if(mounted)setState((){_rows=(data['rows']as List<dynamic>? ?? []).map((x)=>TrialBalanceRow.fromJson(x as Map<String,dynamic>)).toList();_debit=amount(data['totalDebit']);_credit=amount(data['totalCredit']);});}on ApiException catch(e){if(mounted)setState(()=>_error=e.message);}finally{if(mounted)setState(()=>_loading=false);}}@override Widget build(BuildContext context)=>Column(children:[AccountsPageHeader(title:'Trial Balance',subtitle:'Posted journal balances as of ${shortDate(_asOf)}',actions:[OutlinedButton.icon(onPressed:()async{final x=await pickAccountDate(context,_asOf);if(x!=null){setState(()=>_asOf=x);_load();}},icon:const Icon(Icons.event),label:const Text('As-of date')),IconButton(onPressed:_load,icon:const Icon(Icons.refresh))]),if(!_loading&&(_debit-_credit).abs()>=.005)Container(key:const Key('trial_balance_warning'),width:double.infinity,color:Theme.of(context).colorScheme.errorContainer,padding:const EdgeInsets.all(12),child:Text('OUT OF BALANCE by ${money((_debit-_credit).abs())}. Do not rely on this statement until corrected.',style:const TextStyle(fontWeight:FontWeight.bold))),Expanded(child:_loading?const Center(child:CircularProgressIndicator()):_error!=null?AccountsError(_error!,onRetry:_load):Column(children:[Expanded(child:_rows.isEmpty?const Center(child:Text('No posted balances as of this date.')):horizontalTable(DataTable(columns:const[DataColumn(label:Text('Account Code')),DataColumn(label:Text('Account Name')),DataColumn(label:Text('Type')),DataColumn(label:Text('Debit')),DataColumn(label:Text('Credit'))],rows:_rows.map((x)=>DataRow(cells:[DataCell(Text(x.code)),DataCell(Text(x.name)),DataCell(Text(x.accountType)),DataCell(Text(money(x.debit))),DataCell(Text(money(x.credit)))] )).toList()))),Container(key:const Key('trial_balance_totals'),padding:const EdgeInsets.all(14),child:Row(mainAxisAlignment:MainAxisAlignment.end,children:[Text('Total Debit  ${money(_debit)}',style:Theme.of(context).textTheme.titleMedium),const SizedBox(width:32),Text('Total Credit  ${money(_credit)}',style:Theme.of(context).textTheme.titleMedium)]))]))]);}

class ProfitLossView extends StatefulWidget{const ProfitLossView({required this.authState,super.key});final AuthState authState;@override State<ProfitLossView>createState()=>_ProfitLossViewState();}
class _ProfitLossViewState extends State<ProfitLossView>{DateTime _from=DateTime(DateTime.now().year,DateTime.now().month,1),_to=DateTime.now();bool _loading=true;String? _error;Map<String,dynamic>?_data;@override void initState(){super.initState();_load();}Future<void>_load()async{setState((){_loading=true;_error=null;});try{final d=await widget.authState.accounting('profit-loss',query:accountingScope(widget.authState,from:_from,to:DateTime(_to.year,_to.month,_to.day,23,59,59),branchId:widget.authState.currentUser!.branch.id))as Map<String,dynamic>;if(mounted)setState(()=>_data=d);}on ApiException catch(e){if(mounted)setState(()=>_error=e.message);}finally{if(mounted)setState(()=>_loading=false);}}@override Widget build(BuildContext context)=>Column(children:[AccountsPageHeader(title:'Profit & Loss',subtitle:'Revenue and expenses from posted journal classifications',actions:[OutlinedButton(onPressed:()async{final x=await pickAccountDate(context,_from);if(x!=null)setState(()=>_from=x);},child:Text('From ${shortDate(_from)}')),const SizedBox(width:8),OutlinedButton(onPressed:()async{final x=await pickAccountDate(context,_to);if(x!=null)setState(()=>_to=x);},child:Text('To ${shortDate(_to)}')),IconButton(onPressed:_load,icon:const Icon(Icons.refresh))]),Expanded(child:_loading?const Center(child:CircularProgressIndicator()):_error!=null?AccountsError(_error!,onRetry:_load):_ProfitBody(data:_data!))]);}
class _ProfitBody extends StatelessWidget{const _ProfitBody({required this.data});final Map<String,dynamic>data;List<StatementRow>rows(String key)=>(data[key]as List<dynamic>? ?? []).map((x)=>StatementRow.fromJson(x as Map<String,dynamic>)).toList();@override Widget build(BuildContext context)=>ListView(key:const Key('profit_loss_sections'),padding:const EdgeInsets.all(20),children:[_StatementSection('Revenue',rows('revenue')),_Total('Net Revenue',amount(data['netRevenue'])),_StatementSection('Cost of Goods Sold',rows('costOfGoodsSold')),_Total('Gross Profit',amount(data['grossProfit']),strong:true),_StatementSection('Operating Expenses',rows('operatingExpenses')),_Total(amount(data['netProfit'])>=0?'Net Profit':'Net Loss',amount(data['netProfit']),strong:true)]);}

class BalanceSheetView extends StatefulWidget{const BalanceSheetView({required this.authState,super.key});final AuthState authState;@override State<BalanceSheetView>createState()=>_BalanceSheetViewState();}
class _BalanceSheetViewState extends State<BalanceSheetView>{DateTime _asOf=DateTime.now();bool _loading=true;String?_error;Map<String,dynamic>?_data;@override void initState(){super.initState();_load();}Future<void>_load()async{setState((){_loading=true;_error=null;});try{final d=await widget.authState.accounting('balance-sheet',query:accountingScope(widget.authState,asOf:DateTime(_asOf.year,_asOf.month,_asOf.day,23,59,59),branchId:widget.authState.currentUser!.branch.id))as Map<String,dynamic>;if(mounted)setState(()=>_data=d);}on ApiException catch(e){if(mounted)setState(()=>_error=e.message);}finally{if(mounted)setState(()=>_loading=false);}}List<StatementRow>rows(String key)=>(_data?[key]as List<dynamic>? ?? []).map((x)=>StatementRow.fromJson(x as Map<String,dynamic>)).toList();@override Widget build(BuildContext context)=>Column(children:[AccountsPageHeader(title:'Balance Sheet',subtitle:'Accounting equation from all posted journal balances',actions:[OutlinedButton(onPressed:()async{final x=await pickAccountDate(context,_asOf);if(x!=null){setState(()=>_asOf=x);_load();}},child:Text('As of ${shortDate(_asOf)}')),IconButton(onPressed:_load,icon:const Icon(Icons.refresh))]),Expanded(child:_loading?const Center(child:CircularProgressIndicator()):_error!=null?AccountsError(_error!,onRetry:_load):ListView(key:const Key('balance_sheet_sections'),padding:const EdgeInsets.all(20),children:[if(_data?['isBalanced']!=true)Card(color:Theme.of(context).colorScheme.errorContainer,child:const ListTile(leading:Icon(Icons.warning_amber),title:Text('Assets do not equal liabilities plus equity'),subtitle:Text('This discrepancy is visible by design and must be investigated.'))),_StatementSection('Assets',rows('assets')),_Total('Total Assets',amount(_data?['totalAssets']),strong:true),_StatementSection('Liabilities',rows('liabilities')),_Total('Total Liabilities',amount(_data?['totalLiabilities'])),_StatementSection('Equity',rows('equity')),_Total('Current Period Earnings',amount(_data?['currentPeriodEarnings'])),_Total('Total Equity',amount(_data?['totalEquity']),strong:true),const Divider(),_Total('Liabilities + Equity',amount(_data?['totalLiabilities'])+amount(_data?['totalEquity']),strong:true)]))]);}

class _StatementSection extends StatelessWidget{const _StatementSection(this.title,this.rows);final String title;final List<StatementRow>rows;@override Widget build(BuildContext context)=>Card(child:Padding(padding:const EdgeInsets.all(14),child:Column(crossAxisAlignment:CrossAxisAlignment.start,children:[Text(title,style:Theme.of(context).textTheme.titleMedium),const Divider(),if(rows.isEmpty)const Text('No balance')else for(final x in rows)Padding(padding:const EdgeInsets.symmetric(vertical:5),child:Row(children:[SizedBox(width:90,child:Text(x.code)),Expanded(child:Text(x.name)),Text(money(x.amount))]))])));}
class _Total extends StatelessWidget{const _Total(this.label,this.value,{this.strong=false});final String label;final double value;final bool strong;@override Widget build(BuildContext context)=>Padding(padding:const EdgeInsets.symmetric(horizontal:12,vertical:8),child:Row(children:[Expanded(child:Text(label,style:strong?Theme.of(context).textTheme.titleMedium:null)),Text(money(value),style:strong?Theme.of(context).textTheme.titleMedium:null)]));}
