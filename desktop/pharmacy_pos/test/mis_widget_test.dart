import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pharmacy_pos/core/models.dart';
import 'package:pharmacy_pos/core/token_store.dart';
import 'package:pharmacy_pos/features/auth/auth_state.dart';
import 'package:pharmacy_pos/features/reports/mis_screen.dart';
import 'widget_test.dart' show FakeApi, TestFixture;

class MisApi extends FakeApi {
  MisApi(Set<String> permissions)
    : super(
        user: CurrentUser(
          id: 'user-1',
          username: 'test',
          fullName: 'Test User',
          branch: const BranchInfo(
            id: 'branch-1',
            code: 'HQ',
            name: 'Head Office',
          ),
          roles: const [RoleInfo(id: 'role-1', name: 'Owner')],
          permissions: permissions,
          mustChangePassword: false,
        ),
        loginError: false,
      );
  final calls =
      <
        ({
          String path,
          DateTime from,
          DateTime to,
          String? branch,
          Map<String, String>? filters,
        })
      >[];
  dynamic rows = <String, dynamic>{
    'items': [
      {
        'key': 'product-1',
        'name': 'Panadol',
        'netSales': 900,
        'grossProfit': 300,
        'costOfGoodsSold': 600,
      },
    ],
    'total': 1,
  };
  Map<String, dynamic> snapshot = {
    'sales': {'netSales': 900, 'invoiceCount': 3},
    'profitability': {'grossProfit': 300},
    'inventory': {'inventoryValue': 5000},
    'comparison': [
      {
        'metric': 'Sales',
        'current': 900,
        'previous': 600,
        'absoluteChange': 300,
        'percentageChange': 50,
      },
    ],
    'trend': [
      {'name': '2026-09-01', 'netSales': 900, 'grossProfit': 300},
    ],
    'expiryExposure': [
      {'bucket': '0–30', 'quantity': 5, 'costValue': 50},
    ],
  };
  String? exported;
  @override
  Future<dynamic> report(
    String token,
    String path, {
    required DateTime fromUtc,
    required DateTime toUtc,
    String? branchId,
    String? option,
    Map<String, String>? filters,
  }) async {
    calls.add((
      path: path,
      from: fromUtc,
      to: toUtc,
      branch: branchId,
      filters: filters,
    ));
    if (path == 'management/filters') {
      return {
        'branches': [
          {'id': 'branch-1', 'name': 'Head Office'},
        ],
        'godowns': [
          {'id': 'godown-1', 'branchId': 'branch-1', 'name': 'Main Store'},
        ],
      };
    }
    return path.startsWith('management/') ? snapshot : rows;
  }

  @override
  Future<List<int>> exportReport(
    String token,
    String path, {
    required DateTime fromUtc,
    required DateTime toUtc,
    String? branchId,
    String? option,
    Map<String, String>? filters,
  }) async {
    exported = path;
    return [65, 44, 66, 10];
  }
}

const permissions = {
  'reports.view',
  'reports.sales',
  'reports.inventory',
  'reports.profitability',
  'reports.export',
  'sales.view',
};
Future<MisApi> load(
  WidgetTester tester, {
  Set<String> grants = permissions,
  Future<String> Function(List<int>)? save,
}) async {
  final api = MisApi(grants);
  final state = AuthState(api, MemoryTokenStore());
  await state.login('test', 'password');
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: MisScreen(authState: state, saveCsv: save),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return api;
}

Future<void> select(WidgetTester tester, String label, String choice) async {
  final dropdown = find.byWidgetPredicate(
    (w) =>
        w is DropdownButtonFormField<String> && w.decoration.labelText == label,
  );
  await tester.ensureVisible(dropdown);
  await tester.tap(dropdown);
  await tester.pumpAndSettle();
  await tester.ensureVisible(find.text(choice).last);
  await tester.tap(find.text(choice).last);
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('MIS navigation follows reporting permission', (tester) async {
    for (final grants in [
      <String>{},
      {'reports.view'},
    ]) {
      final fixture = TestFixture(permissions: grants);
      await fixture.state.login('test', 'password');
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      expect(
        find.text('Management / MIS'),
        grants.isEmpty ? findsNothing : findsOneWidget,
      );
    }
  });
  testWidgets('overview loads snapshot and comparison', (tester) async {
    final api = await load(tester);
    expect(api.calls.first.path, 'management/overview');
    expect(find.text('900.00'), findsWidgets);
    expect(find.text('Period comparison'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
  testWidgets('period change requests a one day range', (tester) async {
    final api = await load(tester);
    await select(tester, 'Comparison period', 'Today');
    expect(
      api.calls.last.to.difference(api.calls.last.from),
      const Duration(days: 1),
    );
    expect(api.calls.last.from.hour, 19);
  });
  testWidgets('branch selection reaches the server', (tester) async {
    final api = await load(tester);
    await select(tester, 'Branch', 'Head Office');
    expect(api.calls.last.branch, 'branch-1');
  });
  testWidgets('sales MIS requests and renders server rows', (tester) async {
    final api = await load(tester);
    await select(tester, 'MIS section', 'Sales');
    await select(tester, 'Report', 'Products');
    expect(api.calls.last.path, 'sales/products');
    expect(find.text('Panadol'), findsOneWidget);
  });
  testWidgets('inventory MIS uses movement stock position', (tester) async {
    final api = await load(tester);
    await select(tester, 'MIS section', 'Inventory');
    expect(api.calls.last.path, 'inventory/position');
    expect(find.text('Panadol'), findsOneWidget);
  });
  testWidgets('profitability section is gated', (tester) async {
    await load(tester, grants: {'reports.view', 'reports.sales'});
    final dropdown = find.byKey(const ValueKey('MIS section-Overview'));
    await tester.tap(dropdown);
    await tester.pumpAndSettle();
    expect(find.text('Profitability'), findsNothing);
  });
  testWidgets('cost and margin are hidden even from malformed fixture data', (
    tester,
  ) async {
    await load(tester, grants: {'reports.view', 'reports.sales'});
    await select(tester, 'MIS section', 'Sales');
    expect(find.text('gross Profit'), findsNothing);
    expect(find.text('cost Of Goods Sold'), findsNothing);
    expect(find.text('300.00'), findsNothing);
  });
  testWidgets('product drilldown preserves product filter', (tester) async {
    final api = await load(tester);
    await select(tester, 'MIS section', 'Sales');
    await select(tester, 'Report', 'Products');
    final detail = find.byTooltip('Open report detail');
    await tester.ensureVisible(detail);
    await tester.tap(detail);
    await tester.pumpAndSettle();
    expect(api.calls.last.path, 'sales/daily');
    expect(api.calls.last.filters?['productId'], 'product-1');
  });
  testWidgets('CSV exports selected report and saves returned bytes', (
    tester,
  ) async {
    List<int>? saved;
    final api = await load(
      tester,
      save: (bytes) async {
        saved = bytes;
        return 'test.csv';
      },
    );
    await tester.tap(find.byTooltip('Export MIS CSV'));
    await tester.pumpAndSettle();
    expect(api.exported, 'management/overview');
    expect(saved, [65, 44, 66, 10]);
    expect(find.text('CSV saved to test.csv'), findsOneWidget);
  });
  testWidgets('empty report renders an empty state', (tester) async {
    final api = await load(tester);
    api.rows = {'items': [], 'total': 0};
    await select(tester, 'MIS section', 'Sales');
    expect(find.text('No management data for this period.'), findsOneWidget);
  });
  for (final size in [
    const Size(800, 600),
    const Size(1024, 768),
    const Size(1440, 900),
  ]) {
    testWidgets('MIS supports desktop size $size', (tester) async {
      await tester.binding.setSurfaceSize(size);
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await load(tester);
      await select(tester, 'MIS section', 'Inventory');
      expect(tester.takeException(), isNull);
    });
  }
}
