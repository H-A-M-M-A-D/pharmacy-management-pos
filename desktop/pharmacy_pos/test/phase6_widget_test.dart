import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pharmacy_pos/core/models.dart';
import 'package:pharmacy_pos/core/token_store.dart';
import 'package:pharmacy_pos/features/auth/auth_state.dart';
import 'package:pharmacy_pos/features/phase6/bulk_pricing_dialog.dart';
import 'package:pharmacy_pos/features/phase6/phase6_screen.dart';
import 'package:pharmacy_pos/features/phase6/pricing_rule_dialog.dart';
import 'package:pharmacy_pos/features/phase6/reorder_draft_dialog.dart';
import 'package:pharmacy_pos/features/phase6/phase6_suggestions_dialog.dart';
import 'package:pharmacy_pos/features/pricing/price_source_label.dart';
import 'package:pharmacy_pos/features/reports/reports_screen.dart';
import 'package:pharmacy_pos/features/sales/pos_screen.dart';
import 'widget_test.dart' show FakeApi;

const productId = '11111111-1111-1111-1111-111111111111';
const branchId = '22222222-2222-2222-2222-222222222222';
const supplierId = '33333333-3333-3333-3333-333333333333';
const suggestionId = '44444444-4444-4444-4444-444444444444';

class Phase6Api extends FakeApi {
  Phase6Api({Set<String>? grants}) : super(user: CurrentUser(id: 'manager', username: 'manager', fullName: 'Manager', branch: const BranchInfo(id: branchId, code: 'HQ', name: 'Main'), roles: const [RoleInfo(id: 'role', name: 'Manager')], permissions: grants ?? {'pricing.view', 'pricing.manage', 'pricing.suggest', 'sales.cost_view', 'inventory.reorder.view', 'purchase_orders.create', 'alerts.view', 'alerts.manage', 'automation.view', 'automation.run', 'reports.view'}, mustChangePassword: false), loginError: false);
  final calls = <({String path, String method, Map<String, dynamic>? body})>[];
  final rules = <Map<String, dynamic>>[];
  int runs = 0;
  int accepts = 0;
  static const reorder = <String, dynamic>{'productId': productId, 'product': 'Tablet', 'sku': 'TAB', 'branchId': branchId, 'branch': 'Main', 'godownId': null, 'godown': 'Main store', 'currentStock': 1, 'reorderLevel': 10, 'suggestedOrderQuantity': 19, 'preferredSupplierId': supplierId, 'preferredSupplier': 'Supplier', 'risk': 'Reorder'};
  @override
  Future<dynamic> phase6(String token, String path, {String method = 'GET', Map<String, String>? query, Map<String, dynamic>? body}) async {
    calls.add((path: path, method: method, body: body));
    if (path == 'sale-price') return {'price': 10, 'source': 'Promotion', 'priceLevelId': null, 'defaultRetailPrice': 12};
    if (path == 'pricing-options') return {'categories': [], 'manufacturers': [], 'products': [{'id': productId, 'name': 'Tablet'}], 'priceLevels': [], 'customers': [], 'branches': [{'id': branchId, 'name': 'Main'}], 'thresholds': {'roundingIncrement': 1}};
    if (path == 'bulk-pricing/preview') return {'previewId': suggestionId, 'expiresAtUtc': '2026-09-12T12:00:00Z', 'reason': body!['reason'], 'rows': [{'product': 'Tablet', 'oldPrice': 100, 'newPrice': 110}]};
    if (path == 'bulk-pricing/apply') return 1;
    if (path.startsWith('pricing-rules') && method != 'GET') { rules.clear(); rules.add({...body!, 'id': suggestionId}); return rules.single; }
    if (path == 'pricing-rules') return rules;
    if (path == 'reorder') return [reorder];
    if (path == 'reorder/suppliers') return [{'id': supplierId, 'name': 'Supplier'}];
    if (path == 'reorder/drafts') return {'purchaseOrderIds': ['po'], 'duplicateSuppressed': false};
    if (path == 'slow-stock') return [{'product': 'Tablet', 'status': 'Dead stock', 'branch': 'Main', 'godown': 'Main store', 'currentQuantity': 1, 'stockValue': 80, 'lastSaleDate': null, 'daysSinceLastSale': 200}];
    if (path == 'alerts') {
      return [
      {'id': 'ar', 'category': 'Receivables', 'severity': 'Critical', 'title': 'Customer overdue', 'description': 'Clinic outstanding 120000', 'dismissed': false},
      {'id': 'ap', 'category': 'Payables', 'severity': 'Warning', 'title': 'Supplier payment due', 'description': 'Supplier outstanding 150000', 'dismissed': false},
      {'id': 'dismissed', 'category': 'Inventory', 'severity': 'Info', 'title': 'Dismissed stock alert', 'description': 'Dismissed', 'dismissed': true},
    ];
    }
    if (path == 'automation') return [{'name': 'Reorder review', 'triggerType': 'StockBelowReorder', 'actionType': 'FlagForReview', 'isActive': true}];
    if (path == 'automation/run') { runs++; return {'alertsCreated': runs == 1 ? 2 : 0, 'reviewFlagsCreated': runs == 1 ? 1 : 0, 'draftPurchaseOrderIds': [], 'executedRules': runs == 1 ? 1 : 0, 'duplicateRulesSuppressed': runs == 1 ? 0 : 1}; }
    if (path == 'purchase-cost-suggestions') return accepts > 0 ? [] : [{'id': suggestionId, 'product': 'Tablet', 'oldCost': 80, 'newCost': 90, 'currentSellingPrice': 100, 'currentMarginPercent': 10, 'suggestedPrice': 115}];
    if (path.endsWith('/accept')) { accepts++; return 1; }
    if (path == 'expiry-discount-suggestions') return [{'product': 'Tablet', 'batch': 'LOT', 'expiryDate': '2026-09-20', 'quantity': 1, 'discountPercent': 20, 'finalPrice': 80, 'projectedMarginPercent': 0, 'belowCostGuardApplied': true}];
    return 0;
  }
}

Future<AuthState> state(Phase6Api api) async {
  final auth = AuthState(api, MemoryTokenStore());
  await auth.login('manager', 'password');
  return auth;
}
Future<void> mount(WidgetTester tester, Widget widget, {bool dialog = false}) async {
  tester.view.physicalSize = const Size(1400, 1400);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(MaterialApp(home: Scaffold(body: dialog ? Builder(builder: (context) => FilledButton(onPressed: () => showDialog<void>(context: context, builder: (_) => widget), child: const Text('Open'))) : widget)));
  if (dialog) await tester.tap(find.text('Open'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('bulk pricing previews first and requires explicit confirmation to apply', (tester) async {
    final api = Phase6Api();
    await mount(tester, BulkPricingDialog(authState: await state(api)), dialog: true);
    await tester.enterText(find.widgetWithText(TextFormField, 'Reason'), 'Supplier increase');
    await tester.tap(find.text('Preview')); await tester.pumpAndSettle();
    expect(find.text('100 → 110'), findsOneWidget);
    expect(api.calls.any((call) => call.path.endsWith('/apply')), isFalse);
    expect(tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Apply confirmed changes')).onPressed, isNull);
    await tester.tap(find.text('I confirm these price changes')); await tester.pump();
    await tester.tap(find.text('Apply confirmed changes')); await tester.pumpAndSettle();
    expect(api.calls.last.body!['confirmed'], true);
  });
  testWidgets('bulk pricing validates reason before requesting preview', (tester) async {
    final api = Phase6Api();
    await mount(tester, BulkPricingDialog(authState: await state(api)), dialog: true);
    await tester.tap(find.text('Preview')); await tester.pump();
    expect(find.text('Reason is required'), findsOneWidget);
    expect(api.calls.where((call) => call.path == 'bulk-pricing/preview'), isEmpty);
  });
  testWidgets('pricing rule create validates and sends complete adjustment contract', (tester) async {
    final api = Phase6Api();
    await mount(tester, PricingRuleDialog(authState: await state(api)), dialog: true);
    await tester.tap(find.text('Save')); await tester.pump();
    expect(find.text('Name is required'), findsOneWidget);
    await tester.enterText(find.widgetWithText(TextFormField, 'Name'), 'Clinic rule');
    await tester.enterText(find.widgetWithText(TextFormField, 'Priority'), '20');
    await tester.enterText(find.widgetWithText(TextFormField, 'Adjustment value'), '95');
    await tester.tap(find.text('Save')); await tester.pumpAndSettle();
    expect(api.rules.single['priority'], 20);
    expect(api.rules.single['adjustmentValue'], 95);
  });
  testWidgets('pricing rule edit can deactivate and preserve scope', (tester) async {
    final api = Phase6Api();
    await mount(tester, PricingRuleDialog(authState: await state(api), rule: {'id': suggestionId, 'name': 'Existing', 'priority': 2, 'adjustmentValue': 90, 'productId': productId, 'kind': 'Rule', 'adjustmentType': 'FixedPrice', 'isActive': true}), dialog: true);
    await tester.tap(find.text('Active')); await tester.pump();
    await tester.tap(find.text('Save')); await tester.pumpAndSettle();
    expect(api.calls.last.method, 'PUT');
    expect(api.calls.last.body!['isActive'], false);
    expect(api.calls.last.body!['productId'], productId);
  });
  testWidgets('promotion management uses the pricing form with promotion kind', (tester) async {
    final api = Phase6Api();
    await mount(tester, PricingRuleDialog(authState: await state(api), rule: {'id': suggestionId, 'name': 'Promotion', 'priority': 2, 'adjustmentValue': 10, 'kind': 'Promotion', 'adjustmentType': 'PercentageDiscount', 'isActive': true}), dialog: true);
    await tester.tap(find.text('Save')); await tester.pumpAndSettle();
    expect(api.calls.last.body!['kind'], 'Promotion');
    expect(api.calls.last.body!['adjustmentType'], 'PercentageDiscount');
  });
  testWidgets('reorder draft preserves suggestion and submits reviewed quantity only after confirmation', (tester) async {
    final api = Phase6Api();
    await mount(tester, ReorderDraftDialog(authState: await state(api), rows: [Phase6Api.reorder]), dialog: true);
    await tester.tap(find.text('Tablet')); await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, 'Final editable quantity'), '25');
    await tester.tap(find.text('I confirm the selected suppliers and quantities')); await tester.pump();
    await tester.tap(find.text('Create Draft POs')); await tester.pumpAndSettle();
    final line = (api.calls.last.body!['lines'] as List<dynamic>).single;
    expect(line['suggestedQuantity'], 19); expect(line['finalQuantity'], 25);
  });
  testWidgets('slow dead stock displays quantity value and last sale information', (tester) async {
    final api = Phase6Api();
    await mount(tester, Phase6Screen(authState: await state(api)));
    await tester.tap(find.text('Slow / dead stock')); await tester.pumpAndSettle();
    expect(find.text('Tablet · Dead stock'), findsOneWidget);
    expect(find.textContaining('Last sale never'), findsOneWidget);
  });
  testWidgets('AR AP alert categories and dismissed filter work', (tester) async {
    final api = Phase6Api();
    await mount(tester, Phase6Screen(authState: await state(api)));
    await tester.tap(find.text('Alerts')); await tester.pumpAndSettle();
    expect(find.textContaining('Customer overdue'), findsOneWidget);
    expect(find.textContaining('Supplier payment due'), findsOneWidget);
    expect(find.textContaining('Dismissed stock alert'), findsNothing);
    await tester.tap(find.text('All categories')); await tester.pumpAndSettle();
    await tester.tap(find.text('Receivables').last); await tester.pumpAndSettle();
    expect(find.textContaining('Supplier payment due'), findsNothing);
  });
  testWidgets('automation shows idempotency result after repeated run', (tester) async {
    final api = Phase6Api();
    await mount(tester, Phase6Screen(authState: await state(api)));
    await tester.tap(find.text('Automation')); await tester.pumpAndSettle();
    await tester.tap(find.text('Run automation')); await tester.pumpAndSettle();
    await tester.tap(find.text('Run automation')); await tester.pumpAndSettle();
    expect(find.textContaining('duplicates suppressed 1'), findsOneWidget);
  });
  testWidgets('purchase cost suggestion shows old new cost and accepts explicitly', (tester) async {
    final api = Phase6Api();
    await mount(tester, Phase6SuggestionsDialog(authState: await state(api), expiry: false), dialog: true);
    expect(find.textContaining('Old cost 80 → new cost 90'), findsOneWidget);
    await tester.tap(find.text('Review / accept')); await tester.pumpAndSettle();
    expect(api.accepts, 0);
    await tester.tap(find.text('Accept price')); await tester.pumpAndSettle();
    expect(api.accepts, 1);
  });
  testWidgets('expiry discount suggestion shows final price margin and cost guard', (tester) async {
    final api = Phase6Api();
    await mount(tester, Phase6SuggestionsDialog(authState: await state(api), expiry: true), dialog: true);
    expect(find.textContaining('final price 80'), findsOneWidget);
    expect(find.textContaining('Cost floor applied'), findsOneWidget);
    expect(find.text('Review / accept'), findsNothing);
  });
  testWidgets('Phase 6 report entries are available through existing reports screen', (tester) async {
    final api = Phase6Api();
    await mount(tester, ReportsScreen(authState: await state(api)));
    await tester.tap(find.text('Overview').first); await tester.pumpAndSettle();
    await tester.tap(find.text('Phase 6').last); await tester.pumpAndSettle();
    expect(find.text('Price Change History'), findsWidgets);
    await tester.tap(find.text('Price Change History').first); await tester.pumpAndSettle();
    expect(find.text('Automation Execution Log'), findsOneWidget);
    expect(find.text('Slow/Dead Stock Summary'), findsOneWidget);
  });
  test('price source labels cover retail rules promotions snapshots and overrides', () {
    expect(priceSourceLabel('Default'), 'Retail');
    expect(priceSourceLabel('PricingRule'), 'Pricing Rule');
    expect(priceSourceLabel('Promotion'), 'Promotion');
    expect(priceSourceLabel('DocumentSnapshot'), 'Document Snapshot');
    expect(priceSourceLabel('ManualOverride'), 'Manual Override');
  });
  testWidgets('POS displays promotional price source without cost or margin details', (tester) async {
    final api = Phase6Api(grants: {'sales.view', 'sales.create'});
    await mount(tester, PosScreen(authState: await state(api)));
    await tester.enterText(find.byKey(const Key('pos_search')), 'Panadol');
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pumpAndSettle();
    expect(find.text('Promotion'), findsOneWidget);
    expect(find.textContaining('margin'), findsNothing);
    expect(find.text('Total PKR 10.00'), findsOneWidget);
  });
  testWidgets('read only pricing users cannot create rules or bulk changes', (tester) async {
    final api = Phase6Api(grants: {'pricing.view'});
    await mount(tester, Phase6Screen(authState: await state(api)));
    expect(find.text('Create rule / promotion'), findsNothing);
    expect(find.text('Bulk pricing'), findsNothing);
  });
}
