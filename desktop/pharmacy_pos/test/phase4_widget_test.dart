import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pharmacy_pos/core/models.dart';
import 'package:pharmacy_pos/core/token_store.dart';
import 'package:pharmacy_pos/features/auth/auth_state.dart';
import 'package:pharmacy_pos/features/accounts/accounts_screen.dart';
import 'package:pharmacy_pos/features/accounts/periods_view.dart';
import 'package:pharmacy_pos/features/accounts/bank_reconciliation_view.dart';
import 'package:pharmacy_pos/features/accounts/budgets_view.dart';
import 'package:pharmacy_pos/features/accounts/party_adjustments_view.dart';
import 'package:pharmacy_pos/features/accounts/recurring_journals_view.dart';
import 'package:pharmacy_pos/main.dart';
import 'widget_test.dart' show FakeApi;

const _viewPermissions = {'accounts.journal.view', 'accounts.reconciliation.view', 'accounts.periods.view', 'accounts.recurring.view',
  'accounts.budgets.view', 'accounts.credit_notes.view'};
void main() {
  Future<(AuthState, _Phase4Api)> setup(WidgetTester tester, Set<String> permissions, Widget Function(AuthState) view) async {
    final api = _Phase4Api(permissions);
    final auth = AuthState(api, MemoryTokenStore());
    await auth.login('test', 'test');
    await tester.binding.setSurfaceSize(const Size(1500, 900));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(MaterialApp(home: Scaffold(body: view(auth))));
    await tester.pumpAndSettle();
    return (auth, api);
  }
  testWidgets('Phase4 permissions alone expose Accounts in the application shell', (tester) async {
    final api = _Phase4Api({'accounts.budgets.view'});
    final auth = AuthState(api, MemoryTokenStore());
    await auth.login('test', 'test');
    await tester.pumpWidget(PharmacyPOSApp(authState: auth));
    await tester.pumpAndSettle();
    expect(find.text('Accounts'), findsOneWidget);
  });
  testWidgets('Phase4 navigation hides unauthorized sections and mutation controls', (tester) async {
    await setup(tester, {'accounts.budgets.view'}, (auth) => AccountsScreen(authState: auth));
    expect(find.text('Budgets'), findsWidgets);
    expect(find.text('Accounting Periods'), findsNothing);
    expect(find.text('Party Adjustments'), findsNothing);
    expect(find.byKey(const Key('budget_create')), findsNothing);
  });
  for (final section in <String, String>{
    'Accounting Periods': 'periods', 'Bank Reconciliation': 'bank-reconciliations', 'Cash Book': 'cash-book', 'Bank Book': 'bank-book',
    'Day Book': 'day-book', 'Cash Flow': 'cash-flow', 'AR Reconciliation': 'reconciliation/ar', 'AP Reconciliation': 'reconciliation/ap',
    'Inventory Reconciliation': 'reconciliation/inventory', 'Cash/Bank Reconciliation': 'reconciliation/cash-bank',
    'Recurring Journals': 'recurring-journals', 'Budgets': 'budgets', 'Party Adjustments': 'credit-notes',
  }.entries) {
    testWidgets('${section.key} is reachable and loads API data', (tester) async {
      final (_, api) = await setup(tester, _viewPermissions, (auth) => AccountsScreen(authState: auth));
      await tester.scrollUntilVisible(find.widgetWithText(ListTile, section.key), 180, scrollable: find.byType(Scrollable).first);
      await tester.ensureVisible(find.widgetWithText(ListTile, section.key));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ListTile, section.key));
      await tester.pumpAndSettle();
      expect(api.calls, contains(section.value));
      expect(find.byType(CircularProgressIndicator), findsNothing);
      expect(tester.takeException(), isNull);
      if (section.value == 'periods') expect(find.text('January control'), findsOneWidget);
      if (section.value == 'budgets') expect(find.text('1010 Cash'), findsOneWidget);
      if (section.value == 'credit-notes') expect(find.text('CN-1'), findsOneWidget);
    });
  }
  testWidgets('period close and reopen send requests and reload changed status', (tester) async {
    final (_, api) = await setup(tester, {'accounts.periods.view', 'accounts.periods.close', 'accounts.periods.reopen'}, (a) => PeriodsView(authState: a));
    await tester.tap(find.text('Close').first); await tester.pumpAndSettle();
    await tester.tap(find.text('Close period')); await tester.pumpAndSettle();
    expect(api.period['status'], 'Closed'); expect(find.text('Closed'), findsOneWidget);
    await tester.tap(find.text('Reopen')); await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('reopen_reason')), 'Correcting posting');
    await tester.tap(find.widgetWithText(FilledButton, 'Reopen')); await tester.pumpAndSettle();
    expect(api.posts['periods/p1/reopen']?['reason'], 'Correcting posting');
    expect(api.period['status'], 'Open'); expect(find.text('Open'), findsOneWidget);
  });
  testWidgets('reconciliation finalize and reopen persist state and reason', (tester) async {
    final (_, api) = await setup(tester, {'accounts.reconciliation.view', 'accounts.reconciliation.manage'}, (a) => BankReconciliationView(authState: a));
    await tester.tap(find.text('Main bank')); await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('reconciliation_finalize'))); await tester.pumpAndSettle();
    expect(api.reconciliation['status'], 'Finalized');
    expect(api.posts['bank-reconciliations/r1/finalize']?['acknowledgeDifference'], false);
    await tester.tap(find.text('Main bank')); await tester.pumpAndSettle();
    await tester.tap(find.text('Reopen')); await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'Corrected statement');
    await tester.tap(find.widgetWithText(FilledButton, 'Reopen')); await tester.pumpAndSettle();
    expect(api.reconciliation['status'], 'InProgress');
    expect(api.posts['bank-reconciliations/r1/reopen']?['reason'], 'Corrected statement');
    expect(find.byKey(const Key('reconciliation_finalize')), findsOneWidget);
  });
  testWidgets('budget create and edit submit amounts and refresh results', (tester) async {
    final (_, api) = await setup(tester, {'accounts.budgets.view', 'accounts.budgets.manage'}, (a) => BudgetsView(authState: a));
    await tester.tap(find.byKey(const Key('budget_create'))); await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('budget_account'))); await tester.pumpAndSettle();
    await tester.tap(find.text('1010 Cash').last); await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('budget_amount')), '750');
    await tester.tap(find.byKey(const Key('budget_save'))); await tester.pumpAndSettle();
    expect(api.posts['budgets']?['budgetAmount'], 750);
    await tester.tap(find.byKey(const Key('budget_edit_b1'))); await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('budget_amount')), '900');
    await tester.tap(find.byKey(const Key('budget_save'))); await tester.pumpAndSettle();
    expect(api.budget['budgetAmount'], 900);
    expect(api.posts['budgets']?['chartOfAccountId'], 'account-cash');
    expect(api.calls.where((p) => p == 'budgets').length, greaterThanOrEqualTo(5));
  });
  testWidgets('party adjustment validates and posts selected party amount and reason', (tester) async {
    final (_, api) = await setup(tester, {'accounts.credit_notes.view', 'accounts.credit_notes.create'}, (a) => PartyAdjustmentsView(authState: a));
    await tester.tap(find.byKey(const Key('adjustment_create'))); await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('adjustment_post'))); await tester.pumpAndSettle();
    expect(api.posts['credit-notes'], isNull);
    await tester.tap(find.byKey(const Key('adjustment_party'))); await tester.pumpAndSettle();
    await tester.tap(find.text('Ali Customer').last); await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('adjustment_amount')), '125');
    await tester.enterText(find.byKey(const Key('adjustment_reason')), 'Billing correction');
    await tester.tap(find.byKey(const Key('adjustment_post'))); await tester.pumpAndSettle();
    expect(api.posts['credit-notes']?['customerId'], 'customer-1');
    expect(api.posts['credit-notes']?['amount'], 125);
    expect(api.posts['credit-notes']?['reason'], 'Billing correction');
    expect(find.text('CN-2'), findsOneWidget);
  });
  testWidgets('recurring generation submits action reloads schedule and reports result', (tester) async {
    final (_, api) = await setup(tester, {'accounts.recurring.view', 'accounts.recurring.manage'}, (a) => RecurringJournalsView(authState: a));
    await tester.tap(find.byKey(const Key('generate_due'))); await tester.pumpAndSettle();
    expect(api.posts, contains('recurring-journals/generate-due'));
    expect(find.text('1 entry generated.'), findsOneWidget);
    expect(find.text('2026-10-01'), findsOneWidget);
  });
  testWidgets('read-only periods reconciliation and recurring hide mutation actions', (tester) async {
    final (auth, _) = await setup(tester, _viewPermissions, (a) => PeriodsView(authState: a));
    expect(find.text('Close'), findsNothing); expect(find.text('Reopen'), findsNothing);
    await tester.pumpWidget(MaterialApp(home: Scaffold(body: RecurringJournalsView(authState: auth)))); await tester.pumpAndSettle();
    expect(find.byKey(const Key('generate_due')), findsNothing);
    await tester.pumpWidget(MaterialApp(home: Scaffold(body: BankReconciliationView(authState: auth)))); await tester.pumpAndSettle();
    await tester.tap(find.text('Main bank')); await tester.pumpAndSettle();
    expect(find.byKey(const Key('reconciliation_finalize')), findsNothing); expect(find.text('Reopen'), findsNothing);
  });
}

class _Phase4Api extends FakeApi {
  _Phase4Api(Set<String> permissions) : super(user: CurrentUser(id: 'u1', username: 'test', fullName: 'Test User',
    branch: const BranchInfo(id: 'branch-1', code: 'MAIN', name: 'Main'), roles: const [RoleInfo(id: 'role-1', name: 'Accountant')],
    permissions: permissions, mustChangePassword: false), loginError: false);
  final calls = <String>[];
  final posts = <String, Map<String, dynamic>>{};
  final period = <String, dynamic>{'id': 'p1', 'periodNumber': 1, 'name': 'January control', 'startDate': '2026-01-01', 'endDate': '2026-01-31', 'status': 'Open'};
  final reconciliation = <String, dynamic>{'id': 'r1', 'financialAccountName': 'Main bank', 'financialAccountId': 'bank-1', 'statementStartDate': '2026-01-01',
    'statementEndDate': '2026-01-31', 'statementClosingBalance': 1000, 'bookBalance': 1000, 'difference': 0, 'status': 'InProgress', 'lines': <dynamic>[]};
  final budget = <String, dynamic>{'id': 'b1', 'chartOfAccountId': 'account-cash', 'accountCode': '1010', 'accountName': 'Cash', 'budgetAmount': 500, 'branchId': 'branch-1'};
  final notes = <Map<String, dynamic>>[{'id': 'cn1', 'creditNoteNumber': 'CN-1', 'customerName': 'Ali Customer', 'amount': 100, 'reason': 'Correction'}];
  String nextRun = '2026-09-01';
  @override
  Future<dynamic> accounting(String token, String path, {String method = 'GET', Map<String, String>? query, Map<String, dynamic>? body}) async {
    calls.add(path);
    if (method == 'POST') {
      posts[path] = body ?? {};
      if (path == 'periods/p1/close') period['status'] = 'Closed';
      if (path == 'periods/p1/reopen') period['status'] = 'Open';
      if (path.endsWith('/finalize')) reconciliation['status'] = 'Finalized';
      if (path.endsWith('/reopen') && path.startsWith('bank-')) reconciliation['status'] = 'InProgress';
      if (path == 'budgets') budget.addAll(body!);
      if (path == 'credit-notes') notes.add({'id': 'cn2', 'creditNoteNumber': 'CN-2', 'customerName': 'Ali Customer', ...body!});
      if (path == 'recurring-journals/generate-due') { nextRun = '2026-10-01'; return {'generated': [{}], 'failures': []}; }
      return {};
    }
    if (path == 'periods') return [period];
    if (path.startsWith('periods/fiscal-years/')) return null;
    if (path == 'bank-reconciliations') return [reconciliation];
    if (path == 'bank-reconciliations/r1') return reconciliation;
    if (path == 'budgets') return [budget];
    if (path == 'credit-notes') return notes;
    if (path == 'recurring-journals') return [{'id': 't1', 'name': 'Office rent', 'frequency': 'Monthly', 'nextRunDate': nextRun, 'branchName': 'Main', 'isActive': true}];
    if (['cash-book', 'bank-book', 'day-book'].contains(path)) return {'openingBalance': 100, 'closingBalance': 225, 'totalReceipts': 125, 'totalPayments': 0, 'totalDebit': 125, 'totalCredit': 125, 'lines': {'items': <dynamic>[], 'totalCount': 0}};
    if (path == 'cash-flow') return {'netProfit': 125, 'openingCash': 100, 'closingCash': 225, 'netCashFromOperating': 125};
    if (path.startsWith('reconciliation/')) return {'totalSubledgerBalance': 125, 'totalGlBalance': 125, 'totalDifference': 0, 'mismatches': <dynamic>[], 'accounts': <dynamic>[]};
    return super.accounting(token, path, method: method, query: query, body: body);
  }
}
