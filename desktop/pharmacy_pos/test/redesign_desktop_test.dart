import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'widget_test.dart' as fixtures;
import 'package:pharmacy_pos/ui/app_report_grid.dart';
import 'package:pharmacy_pos/ui/app_theme.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  testWidgets(
    'report grid keeps headers visible and sorts and pages loaded facts',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: buildAppTheme(),
          home: Scaffold(
            body: AppReportGrid(
              rows: List.generate(30, (i) => {'amount': i + 1}),
              columns: const ['amount'],
              label: (_) => 'Amount',
              display: (value) => '$value',
            ),
          ),
        ),
      );
      await tester.tap(find.text('Amount'));
      await tester.tap(find.text('Amount'));
      await tester.pump();
      expect(find.text('30'), findsOneWidget);
      await tester.tap(find.byTooltip('Next page'));
      await tester.pump();
      expect(find.text('Loaded rows 26–30 of 30'), findsOneWidget);
      expect(find.text('Amount'), findsOneWidget);
      expect(find.text('1'), findsOneWidget);
    },
  );
  setUpAll(() async {
    final font = FontLoader('Inter')
      ..addFont(rootBundle.load('assets/fonts/Inter.ttf'));
    await font.load();
    final icons = FontLoader('MaterialIcons')
      ..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'));
    await icons.load();
  });
  const permissions = {
    'reports.view',
    'reports.sales',
    'reports.inventory',
    'reports.financial',
    'reports.profitability',
    'sales.view',
    'sales.create',
    'sales.hold',
    'sales.discount',
    'customers.view',
    'customers.create',
    'suppliers.view',
    'suppliers.create',
    'inventory.view',
    'inventory.opening_stock',
    'inventory.adjust',
    'godowns.view',
    'stock_transfers.view',
    'purchases.view',
    'purchase_orders.view',
    'purchase_orders.create',
    'purchases.receive',
    'purchases.create',
    'products.view',
    'users.view',
    'accounts.coa.view',
    'accounts.journal.view',
    'system.view',
    'alerts.view',
    'inventory.reorder.view',
  };
  for (final size in [
    const Size(1366, 768),
    const Size(1440, 900),
    const Size(1920, 1080),
  ]) {
    testWidgets(
      'desktop redesign major modules fit ${size.width.toInt()}x${size.height.toInt()}',
      (tester) async {
        await tester.binding.setSurfaceSize(size);
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final fixture = fixtures.TestFixture(
          permissions: permissions,
          godownCount: 2,
        );
        await fixture.state.login('test', 'password');
        final capture = GlobalKey();
        await tester.pumpWidget(
          RepaintBoundary(key: capture, child: fixture.app),
        );
        await tester.pumpAndSettle();
        Future<void> snapshot(String name) async {
          expect(tester.takeException(), isNull, reason: '$name at $size');
          await tester.runAsync(() async {
            final boundary =
                capture.currentContext!.findRenderObject()!
                    as RenderRepaintBoundary;
            final image = await boundary.toImage(pixelRatio: 1);
            final bytes = await image.toByteData(
              format: ui.ImageByteFormat.png,
            );
            final file = File(
              '../../artifacts/ui-redesign/${size.width.toInt()}x${size.height.toInt()}-$name.png',
            );
            await file.parent.create(recursive: true);
            await file.writeAsBytes(bytes!.buffer.asUint8List());
            image.dispose();
          });
        }

        await snapshot('dashboard');
        for (final module in [
          'Sales',
          'Inventory',
          'Godowns',
          'Transfers',
          'Purchasing',
          'Customers',
          'Suppliers',
          'Accounts',
          'Reports',
          'Users',
          'Administration',
        ]) {
          final destination = find.descendant(
            of: find.byKey(const Key('app_sidebar_scroll')),
            matching: find.text(module),
          );
          await tester.drag(
            find.byKey(const Key('app_sidebar_scroll')),
            const Offset(0, 1500),
          );
          await tester.pumpAndSettle();
          await tester.scrollUntilVisible(
            destination,
            120,
            scrollable: find
                .descendant(
                  of: find.byKey(const Key('app_sidebar_scroll')),
                  matching: find.byType(Scrollable),
                )
                .first,
          );
          await tester.tap(destination);
          await tester.pumpAndSettle();
          if (module == 'Sales') {
            await tester.enterText(find.byKey(const Key('pos_search')), 'para');
            await tester.testTextInput.receiveAction(TextInputAction.done);
            await tester.pumpAndSettle();
          }
          await snapshot(module.toLowerCase().replaceAll(' ', '-'));
          if (module == 'Purchasing') {
            await tester.tap(find.byKey(const Key('direct_purchase')));
            await tester.pumpAndSettle();
            await snapshot('grn-dialog');
            await tester.tap(find.text('Close').last);
            await tester.pumpAndSettle();
          }
        }
        await tester.pumpWidget(const SizedBox.shrink());
        fixture.state.dispose();
      },
    );
  }
  testWidgets(
    'POS keyboard shortcuts focus search and customer without discarding a sale',
    (tester) async {
      final fixture = fixtures.TestFixture(permissions: permissions);
      await fixture.state.login('test', 'password');
      await tester.pumpWidget(fixture.app);
      await tester.pumpAndSettle();
      await tester.tap(
        find.descendant(
          of: find.byKey(const Key('app_sidebar_scroll')),
          matching: find.text('Sales'),
        ),
      );
      await tester.pumpAndSettle();
      await tester.sendKeyEvent(LogicalKeyboardKey.f4);
      await tester.pump();
      expect(
        tester
            .widget<TextField>(find.byKey(const Key('pos_customer_search')))
            .focusNode!
            .hasFocus,
        isTrue,
      );
      await tester.enterText(find.byKey(const Key('pos_search')), 'para');
      await tester.testTextInput.receiveAction(TextInputAction.done);
      await tester.pumpAndSettle();
      final quantity = find.byWidgetPredicate(
        (widget) => widget is InkWell && '${widget.key}'.contains('qty_'),
      );
      await tester.tap(quantity.first);
      await tester.pumpAndSettle();
      final editor = find.byWidgetPredicate(
        (widget) =>
            widget is TextFormField && '${widget.key}'.contains('edit_qty_'),
      );
      await tester.enterText(editor, '3');
      await tester.testTextInput.receiveAction(TextInputAction.done);
      await tester.pumpAndSettle();
      final total = tester
          .widget<Text>(find.byKey(const Key('cart_total')))
          .data;
      expect(total, 'Total PKR 36.00');
      Future<void> navigate(String label) async {
        final destination = find.descendant(
          of: find.byKey(const Key('app_sidebar_scroll')),
          matching: find.text(label),
        );
        await tester.drag(
          find.byKey(const Key('app_sidebar_scroll')),
          const Offset(0, 1500),
        );
        await tester.pumpAndSettle();
        await tester.scrollUntilVisible(
          destination,
          120,
          scrollable: find
              .descendant(
                of: find.byKey(const Key('app_sidebar_scroll')),
                matching: find.byType(Scrollable),
              )
              .first,
        );
        await tester.tap(destination);
        await tester.pumpAndSettle();
      }

      await navigate('Inventory');
      await navigate('Sales');
      expect(
        tester.widget<Text>(find.byKey(const Key('cart_total'))).data,
        total,
      );
      await tester.sendKeyEvent(LogicalKeyboardKey.f2);
      await tester.pump();
      expect(
        tester
            .widget<TextField>(find.byKey(const Key('pos_search')))
            .focusNode!
            .hasFocus,
        isTrue,
      );
    },
  );
}
