import 'dart:io';

import 'package:flutter/material.dart';

import '../auth/auth_state.dart';
import 'report_periods.dart';

class ReportsScreen extends StatefulWidget {
  const ReportsScreen({required this.authState, super.key});
  final AuthState authState;
  @override
  State<ReportsScreen> createState() => _ReportsScreenState();
}

class _ReportsScreenState extends State<ReportsScreen> {
  String _section = 'Overview';
  String _reportName = 'Overview';
  String _preset = 'Today';
  String? _branchId;
  DateTime _from = DateTime.now();
  DateTime _to = DateTime.now().add(const Duration(days: 1));
  dynamic _data;
  bool _loading = false;
  String? _error;

  List<String> get _sections => [
    if (widget.authState.can('reports.view')) 'Overview',
    if (widget.authState.can('reports.view')) 'Phase 6',
    if (widget.authState.can('reports.sales')) 'Sales',
    if (widget.authState.can('reports.purchases')) 'Purchases',
    if (widget.authState.can('reports.inventory')) 'Inventory',
    if (widget.authState.can('reports.inventory')) 'Transfers',
    if (widget.authState.can('reports.financial')) 'Financial',
    if (widget.authState.can('reports.profitability')) 'Profitability',
    if (widget.authState.can('quotations.view')) 'Quotations',
    if (widget.authState.can('sales_orders.view')) 'Sales Orders',
  ];

  @override
  void initState() {
    super.initState();
    if (!widget.authState.can('reports.view')) {
      _section = _sections.first;
      _reportName = _reports[_section]!.keys.first;
    }
    _applyPreset('Today');
    WidgetsBinding.instance.addPostFrameCallback((_) => _load());
  }

  void _applyPreset(String value) {
    _preset = value;
    final range = reportPeriod(value);
    _from = range.$1;
    _to = range.$2;
  }

  Future<void> _selectPreset(String value) async {
    if (value != 'Custom') {
      setState(() => _applyPreset(value));
      return;
    }
    final range = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 1)),
      initialDateRange: DateTimeRange(
        start: _from.add(const Duration(hours: 5)),
        end: _to.add(const Duration(hours: 5)).subtract(const Duration(days: 1)),
      ),
    );
    if (range != null) {
      setState(() {
        _preset = 'Custom';
        _from = DateTime.utc(range.start.year, range.start.month, range.start.day).subtract(const Duration(hours: 5));
        _to = DateTime.utc(
          range.end.year,
          range.end.month,
          range.end.day,
        ).add(const Duration(days: 1)).subtract(const Duration(hours: 5));
      });
    }
  }

  static const _reports = <String, Map<String, String>>{
    'Phase 6': {
      'Price Change History': 'phase6/price-history', 'Promotion Performance': 'phase6/promotion-performance',
      'Margin Exceptions': 'phase6/margin-exceptions', 'Low Margin Products': 'phase6/low-margin',
      'Reorder Suggestions': 'phase6/reorder', 'Stockout Risk': 'phase6/stockout-risk',
      'Slow/Dead Stock Summary': 'phase6/slow-dead-stock', 'Expiry Alert Summary': 'phase6/expiry-summary',
      'Automation Execution Log': 'phase6/automation-log', 'Alert Summary': 'phase6/alert-summary',
    },
    'Sales': {
      'Summary': 'sales/summary',
      'Daily Sales': 'sales/daily',
      'Products': 'sales/products',
      'Categories': 'sales/categories',
      'Cashiers': 'sales/cashiers',
      'Payments': 'sales/payments',
      'Discounts': 'sales/discounts',
      'Credit Sales': 'sales/credit',
      'By Customer': 'sales/by-customer',
      'Retail vs Wholesale': 'sales/retail-vs-wholesale',
      'By Price Level': 'sales/by-price-level',
      'Price Overrides': 'sales/price-overrides',
      'Below-Cost Sales': 'sales/below-cost',
      'Daily Wholesale Sales': 'sales/daily-wholesale',
    },
    'Purchases': {
      'Summary': 'purchases/summary',
      'By Supplier': 'purchases/suppliers',
      'By Product': 'purchases/products',
      'Purchase Returns': 'purchases/returns',
    },
    'Inventory': {
      'Current Stock': 'inventory/current',
      'Low Stock': 'inventory/current',
      'Out of Stock': 'inventory/current',
      'Expired': 'inventory/expiry',
      'Expiry 30 Days': 'inventory/expiry',
      'Expiry 60 Days': 'inventory/expiry',
      'Expiry 90 Days': 'inventory/expiry',
      'Expiry 180 Days': 'inventory/expiry',
      'Batch Stock': 'inventory/batches',
      'Stock Movements': 'inventory/movements',
      'Valuation': 'inventory/valuation',
      'Godown-wise Stock': 'inventory/godown-stock',
      'Godown-wise Movements': 'inventory/godown-movements',
      'Godown-wise Valuation': 'inventory/godown-valuation',
      'In-Transit Stock': 'inventory/in-transit',
      'Stock Count Variance': 'inventory/stock-count-variance',
    },
    'Transfers': {
      'Summary': 'transfers/summary',
      'Daily Transfers': 'transfers/daily',
      'Inter-Godown Detail': 'transfers/detail',
      'Discrepancies': 'transfers/discrepancy',
    },
    'Financial': {
      'Cash Position': 'financial/cash-position',
      'Expenses': 'financial/expenses',
      'Other Income': 'financial/other-income',
      'Customer Outstanding': 'financial/customer-outstanding',
      'Supplier Outstanding': 'financial/supplier-outstanding',
      'Account Ledger': 'financial/account-ledger',
      'Credit Limit Utilization': 'financial/credit-limit-utilization',
    },
    'Profitability': {
      'Gross Profit': 'profitability/summary',
      'Product Profitability': 'profitability/products',
      'By Customer': 'profitability/by-customer',
    },
    'Quotations': {'Summary': 'quotations/summary'},
    'Sales Orders': {
      'Summary': 'sales_orders/summary',
      'Open Orders': 'sales_orders/open',
    },
  };

  String get _path =>
      _section == 'Overview' ? 'overview' : _reports[_section]![_reportName]!;
  String? get _option => switch (_reportName) {
    'Low Stock' => 'low',
    'Out of Stock' => 'out',
    'Expired' => 'expired',
    'Expiry 30 Days' => '30',
    'Expiry 60 Days' => '60',
    'Expiry 90 Days' => '90',
    'Expiry 180 Days' => '180',
    _ => null,
  };

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final result = await widget.authState.report(
        _path,
        fromUtc: _from.toUtc(),
        toUtc: _to.toUtc(),
        branchId: _branchId,
        option: _option,
      );
      if (mounted) setState(() => _data = result);
    } catch (_) {
      if (mounted) setState(() => _error = 'The report could not be loaded.');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _export() async {
    try {
      final bytes = await widget.authState.exportReport(
        _path,
        fromUtc: _from.toUtc(),
        toUtc: _to.toUtc(),
        branchId: _branchId,
        option: _option,
      );
      final home = Platform.environment['USERPROFILE'];
      if (home == null) throw const FileSystemException();
      final directory = Directory('$home\\Downloads');
      final file = File(
        '${directory.path}\\pharmacy-${_section.toLowerCase()}-${DateTime.now().millisecondsSinceEpoch}.csv',
      );
      await file.writeAsBytes(bytes, flush: true);
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text('CSV saved to ${file.path}')));
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('The report export could not be completed.'),
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  'Reports & Analytics',
                  style: Theme.of(context).textTheme.headlineMedium,
                ),
              ),
              if (widget.authState.can('reports.export'))
                IconButton(
                  onPressed: _loading ? null : _export,
                  tooltip: 'Export CSV',
                  icon: const Icon(Icons.download_outlined),
                ),
            ],
          ),
          const SizedBox(height: 16),
          Wrap(
            spacing: 12,
            runSpacing: 10,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              DropdownButton<String>(
                value: _section,
                items: _sections
                    .map((x) => DropdownMenuItem(value: x, child: Text(x)))
                    .toList(),
                onChanged: (x) {
                  if (x != null) {
                    setState(() {
                      _section = x;
                      _reportName = x == 'Overview'
                          ? 'Overview'
                          : _reports[x]!.keys.first;
                    });
                  }
                },
              ),
              if (_section != 'Overview')
                DropdownButton<String>(
                  value: _reportName,
                  items: _reports[_section]!.keys
                      .map((x) => DropdownMenuItem(value: x, child: Text(x)))
                      .toList(),
                  onChanged: (x) {
                    if (x != null) setState(() => _reportName = x);
                  },
                ),
              DropdownButton<String>(
                value: _preset,
                items:
                    [
                          'Today',
                          'Yesterday',
                          'This Week',
                          'This Month',
                          '30 Days',
                          'Custom',
                        ]
                        .map((x) => DropdownMenuItem(value: x, child: Text(x)))
                        .toList(),
                onChanged: (x) {
                  if (x != null) _selectPreset(x);
                },
              ),
              if (_canSelectBranch)
                DropdownButton<String?>(
                  value: _branchId,
                  items: [
                    const DropdownMenuItem(
                      value: null,
                      child: Text('All permitted branches'),
                    ),
                    DropdownMenuItem(
                      value: widget.authState.currentUser!.branch.id,
                      child: Text(widget.authState.currentUser!.branch.name),
                    ),
                  ],
                  onChanged: (x) => setState(() => _branchId = x),
                ),
              FilledButton.icon(
                onPressed: _loading ? null : _load,
                icon: const Icon(Icons.filter_alt_outlined),
                label: const Text('Apply'),
              ),
            ],
          ),
          const SizedBox(height: 18),
          Expanded(child: _body()),
        ],
      ),
    ),
  );

  bool get _canSelectBranch => widget.authState.currentUser!.roles.any(
    (x) => x.name == 'Owner' || x.name == 'Manager',
  );

  Widget _body() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(
        child: Text(
          _error!,
          style: TextStyle(color: Theme.of(context).colorScheme.error),
        ),
      );
    }
    final data = _data;
    if (data == null) {
      return const Center(child: Text('Select a report and apply filters.'));
    }
    if (data is List && data.isEmpty) {
      return const Center(child: Text('No report data for this period.'));
    }
    if (data is Map<String, dynamic> &&
        data['items'] is List &&
        (data['items'] as List).isEmpty) {
      return const Center(child: Text('No report data for this period.'));
    }
    if (data is Map<String, dynamic> && _section == 'Overview') {
      return _overview(data);
    }
    final rows = data is List
        ? data
        : data is Map<String, dynamic> && data['items'] is List
        ? data['items'] as List
        : [data];
    return _table(rows.cast<dynamic>());
  }

  Widget _overview(Map<String, dynamic> data) {
    final metrics = <String, dynamic>{
      'Net sales': data['netSales'],
      'Gross profit': data['grossProfit'],
      'Inventory value': data['inventoryValue'],
      'Low stock': data['lowStockCount'],
      'Near expiry': data['nearExpiryCount'],
      'Customer outstanding': data['customerOutstanding'],
      'Supplier outstanding': data['supplierOutstanding'],
      'Expenses': data['expenses'],
      'Cash position': data['cashPosition'],
    };
    return ListView(
      children: [
        Wrap(
          spacing: 12,
          runSpacing: 12,
          children: metrics.entries
              .map(
                (x) => SizedBox(
                  width: 190,
                  height: 180,
                  child: Card(
                    child: Padding(
                      padding: const EdgeInsets.all(14),
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(x.key),
                          Text(
                            _metricDisplay(x.key, x.value),
                            style: Theme.of(context).textTheme.titleMedium,
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              )
              .toList(),
        ),
        const SizedBox(height: 22),
        if ((data['salesTrend'] as List<dynamic>? ?? []).isNotEmpty) ...[
          Text('Sales trend', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          _bars(data['salesTrend'] as List<dynamic>, 'date', 'netSales'),
          const SizedBox(height: 22),
        ],
        if ((data['paymentMethods'] as List<dynamic>? ?? []).isNotEmpty) ...[
          Text(
            'Sales by payment method',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 8),
          _bars(data['paymentMethods'] as List<dynamic>, 'method', 'amount'),
          const SizedBox(height: 22),
        ],
        Text(
          'Top products by net quantity sold',
          style: Theme.of(context).textTheme.titleMedium,
        ),
        const SizedBox(height: 8),
        _table((data['topProducts'] as List<dynamic>? ?? [])),
      ],
    );
  }

  Widget _bars(List<dynamic> values, String labelKey, String valueKey) {
    final rows = values.whereType<Map<String, dynamic>>().take(10).toList();
    final maximum = rows.fold<double>(0, (max, row) {
      final value = (row[valueKey] as num?)?.toDouble() ?? 0;
      return value > max ? value : max;
    });
    return Column(
      children: rows.map((row) {
        final value = (row[valueKey] as num?)?.toDouble() ?? 0;
        return Padding(
          padding: const EdgeInsets.symmetric(vertical: 4),
          child: Row(
            children: [
              SizedBox(
                width: 110,
                child: Text(
                  '${row[labelKey]}',
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              Expanded(
                child: LinearProgressIndicator(
                  value: maximum == 0 ? 0 : value / maximum,
                  minHeight: 10,
                ),
              ),
              const SizedBox(width: 10),
              SizedBox(
                width: 90,
                child: Text(
                  'Rs ${value.toStringAsFixed(2)}',
                  textAlign: TextAlign.end,
                ),
              ),
            ],
          ),
        );
      }).toList(),
    );
  }

  Widget _table(List<dynamic> values) {
    if (values.isEmpty) {
      return const Center(child: Text('No report data for this period.'));
    }
    final rows = values.whereType<Map<String, dynamic>>().toList();
    if (rows.isEmpty) {
      return const Center(child: Text('No report data for this period.'));
    }
    final columns = rows.first.keys
        .where((x) => !const {'id', 'productId', 'partyId'}.contains(x))
        .take(10)
        .toList();
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: SingleChildScrollView(
        child: DataTable(
          columns: columns
              .map((x) => DataColumn(label: Text(_label(x))))
              .toList(),
          rows: rows
              .map(
                (row) => DataRow(
                  cells: columns
                      .map((x) => DataCell(Text(_display(row[x]))))
                      .toList(),
                ),
              )
              .toList(),
        ),
      ),
    );
  }

  static String _display(dynamic value) => value is num
      ? (value is int ? '$value' : 'Rs ${value.toStringAsFixed(2)}')
      : '${value ?? ''}';
  static String _metricDisplay(String key, dynamic value) {
    if (key == 'Low stock' || key == 'Near expiry') return '${value ?? 0}';
    final amount = value is num ? value.toDouble() : 0;
    return 'Rs ${amount.toStringAsFixed(2)}';
  }

  static String _label(String value) => value
      .replaceAllMapped(RegExp(r'([A-Z])'), (x) => ' ${x.group(1)}')
      .trim()
      .split(' ')
      .map((x) => x.isEmpty ? x : '${x[0].toUpperCase()}${x.substring(1)}')
      .join(' ');
}
