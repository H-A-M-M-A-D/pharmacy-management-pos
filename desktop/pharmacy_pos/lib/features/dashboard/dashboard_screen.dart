import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import '../../ui/app_widgets.dart';

import '../auth/auth_state.dart';
import '../../ui/app_theme.dart';
import '../reports/report_periods.dart';

/// The owner/manager landing screen. Every figure shown here is read from
/// endpoints the rest of the app already calls (the Phase 5 management
/// overview report and the Phase 6 alerts/reorder feeds) - this screen adds
/// no new backend calculations, it only arranges already-authorized data.
class DashboardScreen extends StatefulWidget {
  const DashboardScreen({required this.authState, super.key});

  final AuthState authState;

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  bool _loading = true;
  String? _error;
  int _request = 0;
  int _unavailable = 0;

  Map<String, dynamic>? _today;
  Map<String, dynamic>? _trend;
  List<Map<String, dynamic>> _recentSales = const [];
  List<Map<String, dynamic>> _alerts = const [];
  List<Map<String, dynamic>> _reorder = const [];
  List<Map<String, dynamic>> _purchaseOrders = const [];

  bool get _canReports => widget.authState.can('reports.view');
  bool get _canSales =>
      widget.authState.can('sales.view') ||
      widget.authState.can('sales.create');
  bool get _canAlerts => widget.authState.can('alerts.view');
  bool get _canReorder => widget.authState.can('inventory.reorder.view');
  bool get _canPurchaseOrders => widget.authState.can('purchase_orders.view');

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _load());
  }

  Future<T?> _safe<T>(bool allowed, Future<T> Function() call) async {
    if (!allowed) return null;
    try {
      return await call();
    } catch (_) {
      _unavailable++;
      return null;
    }
  }

  Future<void> _load() async {
    final request = ++_request;
    _unavailable = 0;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final todayRange = reportPeriod('Today');
      final trendRange = reportPeriod('30 Days');
      final results = await Future.wait([
        _safe(
          _canReports,
          () => widget.authState.report(
            'management/overview',
            fromUtc: todayRange.$1,
            toUtc: todayRange.$2,
          ),
        ),
        _safe(
          _canReports,
          () => widget.authState.report(
            'management/overview',
            fromUtc: trendRange.$1,
            toUtc: trendRange.$2,
          ),
        ),
        _safe(_canSales, () => widget.authState.listSales()),
        _safe(_canAlerts, () => widget.authState.phase6('alerts')),
        _safe(_canReorder, () => widget.authState.phase6('reorder')),
        _safe(_canPurchaseOrders, () => widget.authState.listPurchaseOrders()),
      ]);
      if (!mounted || request != _request) return;
      setState(() {
        _today = _asMap(results[0]);
        _trend = _asMap(results[1]);
        _recentSales =
            (results[2] as dynamic)?.items
                .map<Map<String, dynamic>>(
                  (sale) => <String, dynamic>{
                    'invoiceNumber': sale.invoiceNumber,
                    'holdNumber': sale.holdNumber,
                    'customerName': sale.customerName,
                    'netTotal': sale.netTotal,
                    'createdAt': sale.createdAt.toIso8601String(),
                  },
                )
                .toList() ??
            <Map<String, dynamic>>[];
        _alerts = _asMapList(results[3]);
        _reorder = _asMapList(results[4]);
        _purchaseOrders =
            (results[5] as dynamic)?.items
                .map<Map<String, dynamic>>(
                  (po) => <String, dynamic>{'status': po.status},
                )
                .toList() ??
            <Map<String, dynamic>>[];
      });
    } catch (_) {
      if (mounted && request == _request) {
        setState(() => _error = 'The dashboard could not be loaded right now.');
      }
    } finally {
      if (mounted && request == _request) setState(() => _loading = false);
    }
  }

  static Map<String, dynamic>? _asMap(dynamic value) =>
      value is Map ? Map<String, dynamic>.from(value) : null;
  static List<Map<String, dynamic>> _asMapList(dynamic value) => value is List
      ? value.whereType<Map>().map((e) => Map<String, dynamic>.from(e)).toList()
      : const [];

  Map<String, dynamic>? get _sales => _asMap(_today?['sales']);
  Map<String, dynamic>? get _profitability => _asMap(_today?['profitability']);
  Map<String, dynamic>? get _inventory => _asMap(_today?['inventory']);
  Map<String, dynamic>? get _finance => _asMap(_today?['finance']);
  Map<String, dynamic>? get _position => _asMap(_finance?['position']);
  Map<String, dynamic>? get _calendarSales => _asMap(_today?['calendarSales']);
  List<Map<String, dynamic>> get _expiryExposure =>
      _asMapList(_today?['expiryExposure']);
  List<Map<String, dynamic>> get _trendRows => _asMapList(_trend?['trend']);
  List<Map<String, dynamic>> get _topProducts =>
      _asMapList(_trend?['topProducts']);

  Map<String, dynamic>? _expiryBucket(String label) {
    for (final row in _expiryExposure) {
      if (row['bucket'] == label) return row;
    }
    return null;
  }

  int get _pendingPurchaseOrderCount => _purchaseOrders
      .where(
        (po) =>
            po['status'] == 'Submitted' || po['status'] == 'PartiallyReceived',
      )
      .length;

  @override
  Widget build(BuildContext context) {
    final user = widget.authState.currentUser!;
    final now = DateTime.now();
    return SafeArea(
      child: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          key: const Key('dashboard_scroll'),
          padding: const EdgeInsets.all(20),
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Dashboard',
                        style: Theme.of(context).textTheme.headlineMedium,
                      ),
                      const SizedBox(height: 4),
                      Text(
                        '${user.branch.name} · ${user.roles.map((r) => r.name).join(', ')}',
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                    ],
                  ),
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text(
                      '${_weekday(now.weekday)}, ${now.day} ${_month(now.month)} ${now.year}',
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'Welcome, ${user.fullName}',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                ),
                IconButton(
                  tooltip: 'Refresh dashboard',
                  onPressed: _loading ? null : _load,
                  icon: const Icon(Icons.refresh),
                ),
              ],
            ),
            const SizedBox(height: 20),
            if (_loading)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 80),
                child: AppLoadingState(),
              )
            else if (_error != null)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 60),
                child: Column(
                  children: [
                    Text(_error!),
                    const SizedBox(height: 12),
                    FilledButton(onPressed: _load, child: const Text('Retry')),
                  ],
                ),
              )
            else
              ..._body(context),
          ],
        ),
      ),
    );
  }

  List<Widget> _body(BuildContext context) => [
    if (_canReports && _sales != null) _primaryKpis(context),
    if (!_canReports)
      const _InfoCard(
        text:
            'Sales and financial figures require reporting permission and are hidden for this role.',
      ),
    if (_unavailable > 0)
      const _InfoCard(
        text:
            'Some dashboard feeds are unavailable. Refresh to retry; unavailable data is not a zero balance.',
      ),
    const SizedBox(height: 20),
    _secondaryCards(context),
    const SizedBox(height: 24),
    LayoutBuilder(
      builder: (context, bounds) {
        final chart = _trendRows.isEmpty
            ? const _InfoCard(
                text: 'No sales trend is available for this period.',
              )
            : _salesTrendCard(context);
        if (bounds.maxWidth < 1000) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              chart,
              if (_canAlerts) ...[
                const SizedBox(height: 16),
                _alertsCard(context),
              ],
            ],
          );
        }
        return Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(flex: 2, child: chart),
            if (_canAlerts) ...[
              const SizedBox(width: 16),
              Expanded(child: _alertsCard(context)),
            ],
          ],
        );
      },
    ),
    const SizedBox(height: 20),
    Wrap(
      spacing: 16,
      runSpacing: 16,
      children: [
        if (_position != null) _cashBankCard(context),
        if (_canReports) _topProductsCard(context),
      ],
    ),
    const SizedBox(height: 20),
    Wrap(
      spacing: 16,
      runSpacing: 16,
      children: [if (_canSales) _recentSalesCard(context)],
    ),
  ];

  Widget _primaryKpis(BuildContext context) {
    final cards = <_Kpi>[
      _Kpi(
        'Sales Today',
        _money(_calendarSales?['salesToday'] ?? _sales?['netSales']),
        Icons.point_of_sale_outlined,
      ),
      if (_profitability != null)
        _Kpi(
          'Gross Profit',
          _money(_profitability?['grossProfit']),
          Icons.trending_up,
        ),
      _Kpi(
        'Transactions Today',
        '${_num(_sales?['invoiceCount'])}',
        Icons.receipt_long_outlined,
      ),
      if (_position != null)
        _Kpi(
          'Receivables',
          _money(_position?['receivables']),
          Icons.arrow_downward,
        ),
      if (_position != null)
        _Kpi('Payables', _money(_position?['payables']), Icons.arrow_upward),
      if (_inventory != null)
        _Kpi(
          'Inventory Value',
          _money(_inventory?['inventoryValue']),
          Icons.inventory_2_outlined,
        ),
    ];
    return LayoutBuilder(
      builder: (context, bounds) {
        final count = ((bounds.maxWidth + 16) / 216).floor().clamp(
          1,
          cards.length,
        );
        final width = (bounds.maxWidth - (count - 1) * 16) / count;
        return Wrap(
          spacing: 16,
          runSpacing: 16,
          children: [
            for (final card in cards) SizedBox(width: width, child: card),
          ],
        );
      },
    );
  }

  Widget _secondaryCards(BuildContext context) {
    final cards = <Widget>[];
    if (_inventory != null) {
      cards.add(
        _Kpi(
          'Low Stock',
          '${_num(_inventory?['lowStockItems'])}',
          Icons.warning_amber_outlined,
          compact: true,
        ),
      );
      cards.add(
        _Kpi(
          'Out of Stock',
          '${_num(_inventory?['outOfStockItems'])}',
          Icons.remove_shopping_cart_outlined,
          compact: true,
        ),
      );
    }
    if (_today != null) {
      cards.add(
        _Kpi(
          'Near Expiry',
          '${_num(_expiryBucket('0–30')?['batches'])}',
          Icons.event_busy_outlined,
          compact: true,
        ),
      );
    }
    if (_canReorder || _canPurchaseOrders) {
      final parts = <String>[
        if (_canReorder) '${_reorder.length} reorder',
        if (_canPurchaseOrders) '$_pendingPurchaseOrderCount pending PO',
      ];
      cards.add(
        _Kpi(
          'Reorder / Pending POs',
          parts.join(' · '),
          Icons.local_shipping_outlined,
          compact: true,
        ),
      );
    }
    if (cards.isEmpty) return const SizedBox.shrink();
    return Wrap(spacing: 16, runSpacing: 16, children: cards);
  }

  Widget _salesTrendCard(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Sales Trend (30 days)',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 12),
          SizedBox(
            height: 220,
            child: LineChart(
              LineChartData(
                borderData: FlBorderData(show: false),
                minX: 0,
                maxX: _trendRows.length > 1
                    ? (_trendRows.length - 1).toDouble()
                    : 1,
                lineBarsData: [
                  LineChartBarData(
                    spots: [
                      for (var i = 0; i < _trendRows.length; i++)
                        FlSpot(
                          i.toDouble(),
                          (_trendRows[i]['netSales'] as num? ?? 0).toDouble(),
                        ),
                    ],
                    color: AppColors.primary,
                    barWidth: 2,
                    dotData: const FlDotData(show: false),
                  ),
                ],
                titlesData: FlTitlesData(
                  topTitles: const AxisTitles(
                    sideTitles: SideTitles(showTitles: false),
                  ),
                  rightTitles: const AxisTitles(
                    sideTitles: SideTitles(showTitles: false),
                  ),
                  bottomTitles: AxisTitles(
                    sideTitles: SideTitles(
                      showTitles: true,
                      reservedSize: 28,
                      interval: _trendRows.length > 6
                          ? (_trendRows.length / 5).ceilToDouble()
                          : 1,
                      getTitlesWidget: (v, meta) {
                        final i = v.toInt();
                        return Text(
                          i >= 0 && i < _trendRows.length
                              ? '${_trendRows[i]['name']}'
                              : '',
                          style: const TextStyle(fontSize: 10),
                        );
                      },
                    ),
                  ),
                  leftTitles: const AxisTitles(
                    sideTitles: SideTitles(showTitles: true, reservedSize: 55),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    ),
  );

  Widget _cashBankCard(BuildContext context) => SizedBox(
    width: 260,
    child: Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Cash / Bank', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 12),
            _summaryRow('Cash', _money(_position?['cash'])),
            _summaryRow('Bank', _money(_position?['bank'])),
          ],
        ),
      ),
    ),
  );

  Widget _summaryRow(String label, String value) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 4),
    child: Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [Text(label), Text(value)],
    ),
  );

  Widget _topProductsCard(BuildContext context) => SizedBox(
    width: 340,
    child: Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Top Selling Products (30 days)',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            if (_topProducts.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Text('No product sales in this period.'),
              )
            else
              for (final row in _topProducts.take(5))
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  dense: true,
                  title: Text('${row['name'] ?? 'Unknown product'}'),
                  trailing: Text(_money(row['netSales'])),
                ),
          ],
        ),
      ),
    ),
  );

  Widget _recentSalesCard(BuildContext context) => SizedBox(
    width: 340,
    child: Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Recent Sales',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            if (_recentSales.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Text('No sales recorded yet.'),
              )
            else
              for (final sale in _recentSalesSorted.take(6))
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  dense: true,
                  title: Text(
                    '${sale['invoiceNumber'] ?? sale['holdNumber'] ?? 'Sale'} · ${sale['customerName'] ?? 'Walk-in'}',
                  ),
                  trailing: Text(_money(sale['netTotal'])),
                ),
          ],
        ),
      ),
    ),
  );

  List<Map<String, dynamic>> get _recentSalesSorted {
    final sorted = [..._recentSales];
    sorted.sort((a, b) {
      final ad = DateTime.tryParse('${a['createdAt']}') ?? DateTime(0);
      final bd = DateTime.tryParse('${b['createdAt']}') ?? DateTime(0);
      return bd.compareTo(ad);
    });
    return sorted;
  }

  Widget _alertsCard(BuildContext context) => SizedBox(
    width: 340,
    child: Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Business Alerts',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            if (_alerts.where((a) => a['dismissed'] != true).isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Text('No active business alerts.'),
              )
            else
              for (final alert
                  in _alerts.where((a) => a['dismissed'] != true).take(5))
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  dense: true,
                  leading: Icon(
                    Icons.circle,
                    size: 10,
                    color: _severityColor(alert['severity'] as String?),
                  ),
                  title: Text('${alert['title'] ?? 'Alert'}'),
                  subtitle: alert['description'] != null
                      ? Text('${alert['description']}')
                      : null,
                ),
          ],
        ),
      ),
    ),
  );

  Color _severityColor(String? severity) => switch (severity) {
    'Critical' => AppColors.danger,
    'Warning' => AppColors.warning,
    _ => AppColors.muted,
  };

  static num _num(dynamic value) => value is num ? value : 0;
  static String _money(dynamic value) =>
      'PKR ${(value is num ? value : 0).toStringAsFixed(2)}';

  static const _weekdays = [
    'Monday',
    'Tuesday',
    'Wednesday',
    'Thursday',
    'Friday',
    'Saturday',
    'Sunday',
  ];
  static const _months = [
    'January',
    'February',
    'March',
    'April',
    'May',
    'June',
    'July',
    'August',
    'September',
    'October',
    'November',
    'December',
  ];
  static String _weekday(int day) => _weekdays[(day - 1).clamp(0, 6)];
  static String _month(int month) => _months[(month - 1).clamp(0, 11)];
}

class _Kpi extends StatelessWidget {
  const _Kpi(this.label, this.value, this.icon, {this.compact = false});
  final String label;
  final String value;
  final IconData icon;
  final bool compact;

  @override
  Widget build(BuildContext context) => SizedBox(
    width: compact ? 190 : 200,
    child: AppStatCard(
      label: label,
      value: value,
      icon: icon,
      compact: compact,
    ),
  );
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({required this.text});
  final String text;

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(padding: const EdgeInsets.all(16), child: Text(text)),
  );
}
