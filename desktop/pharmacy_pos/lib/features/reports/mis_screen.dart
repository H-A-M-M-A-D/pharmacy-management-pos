import 'dart:convert';
import 'dart:io';
import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import '../../ui/app_theme.dart';
import '../../ui/app_widgets.dart';
import '../auth/auth_state.dart';
import 'report_periods.dart';

class MisScreen extends StatefulWidget {
  const MisScreen({required this.authState, this.saveCsv, super.key});
  final AuthState authState;
  final Future<String> Function(List<int> bytes)? saveCsv;
  @override
  State<MisScreen> createState() => _MisScreenState();
}

class _MisScreenState extends State<MisScreen> {
  String _section = 'Overview';
  String _path = 'management/overview';
  String _period = 'This Month';
  String? _branch;
  String? _godown;
  String? _product;
  int _page = 1;
  int _request = 0;
  dynamic _data;
  bool _loading = true;
  String? _error;
  DateTime? _customFrom, _customTo;
  final _search = TextEditingController();
  final _advanced = <String, String>{};
  Map<String, dynamic> _lookups = {};
  String _abcBasis = 'sales';
  List<Map<String, dynamic>> _branches = [], _godowns = [];

  static const _reports = <String, Map<String, String>>{
    'Overview': {
      'Overview': 'management/overview',
      'Daily Business Summary': 'management/daily',
      'Monthly MIS Summary': 'management/monthly',
      'Period Comparison': 'management/comparison',
    },
    'Sales': {
      'Invoices': 'sales/daily',
      'Daily Summary': 'sales/daily-summary',
      'Monthly Summary': 'sales/monthly',
      'Trend': 'sales/trend',
      'Products': 'sales/products',
      'Categories': 'sales/categories',
      'Manufacturers': 'sales/manufacturers',
      'Customers': 'sales/by-customer',
      'Cashiers': 'sales/cashiers',
      'Retail vs Wholesale': 'sales/retail-vs-wholesale',
      'Price Levels': 'sales/by-price-level',
      'Payments': 'sales/payments',
      'Discounts': 'sales/discounts',
      'Returns': 'sales/return-analysis',
      'Invoice Metrics': 'sales/invoice-metrics',
      'Hour of Day': 'sales/hourly',
    },
    'Profitability': {
      'Summary': 'profitability/summary',
      'Products': 'profitability/products',
      'Categories': 'profitability/categories',
      'Manufacturers': 'profitability/manufacturers',
      'Customers': 'profitability/by-customer',
      'Invoices': 'profitability/invoices',
      'Branches': 'profitability/branches',
      'Godowns': 'profitability/godowns',
      'Below Cost': 'sales/below-cost',
      'Discount Impact': 'profitability/discount-impact',
    },
    'Inventory': {
      'Stock Position': 'inventory/position',
      'Batches': 'inventory/batch-position',
      'Valuation': 'inventory/valuation',
      'Inventory Aging': 'inventory/aging',
      'Expiry Exposure': 'inventory/expiry-risk',
      'Product Performance': 'inventory/performance',
      'Fast Moving': 'inventory/fast-moving',
      'Slow Moving': 'inventory/slow-moving',
      'Dead Stock': 'inventory/dead-stock',
      'ABC Analysis': 'inventory/abc',
      'Stock Movements': 'inventory/movements',
      'Movement Summary': 'inventory/movement-summary',
      'Stock Adjustments': 'inventory/stock-adjustments',
      'Inventory Turnover': 'inventory/turnover',
      'In Transit': 'inventory/in-transit',
      'Count Variance': 'inventory/stock-count-variance',
    },
    'Purchasing': {
      'Summary': 'purchases/summary',
      'Trend': 'purchases/trend',
      'Suppliers': 'purchases/by-supplier',
      'Products': 'purchases/by-product',
      'Categories': 'purchases/by-category',
      'Manufacturers': 'purchases/by-manufacturer',
      'Branches': 'purchases/by-branch',
      'Godowns': 'purchases/by-godown',
      'Returns': 'purchases/returns',
      'Price History': 'purchases/price-history',
      'Price Comparison': 'purchases/price-comparison',
      'Last Purchase Rate': 'purchases/last-rate',
      'Price Variance': 'purchases/price-variance',
      'Pending Purchase Orders': 'purchases/pending-po',
      'PO vs GRN': 'purchases/po-vs-grn',
      'Supplier Performance': 'purchases/supplier-performance',
    },
    'Customers': {
      'Outstanding': 'financial/customer-outstanding',
      'Aging': 'financial/customer-aging',
      'Credit Utilization': 'financial/credit-limit-utilization',
      'Payment History': 'financial/customer-payments',
      'Statement': 'financial/customer-statement',
      'Performance': 'financial/customer-performance',
      'Sales': 'sales/by-customer',
      'Profitability': 'profitability/by-customer',
    },
    'Suppliers': {
      'Payables': 'financial/supplier-outstanding',
      'Payment History': 'financial/supplier-payments',
      'Statement': 'financial/supplier-statement',
      'Aging': 'financial/supplier-aging',
      'Purchases': 'purchases/by-supplier',
      'Returns': 'purchases/returns',
    },
    'Finance': {
      'Financial Summary': 'management/financial',
      'Working Capital': 'management/working-capital',
      'Budget vs Actual': 'management/budget',
      'Expenses': 'financial/expenses',
      'Cash Position': 'financial/cash-position',
    },
    'Branches': {
      'Branch MIS': 'management/branches',
      'Sales': 'sales/branches',
      'Profitability': 'profitability/branches',
      'Purchases': 'purchases/by-branch',
    },
    'Godowns': {
      'Godown MIS': 'management/godowns',
      'Stock': 'inventory/position',
      'Sales': 'sales/godowns',
      'Profitability': 'profitability/godowns',
      'Transfers': 'transfers/summary',
      'Transfer Detail': 'transfers/detail',
    },
  };

  bool _allowed(String path) {
    final permission = switch (path.split('/').first) {
      'sales' => 'reports.sales',
      'profitability' => 'reports.profitability',
      'inventory' || 'transfers' => 'reports.inventory',
      'purchases' => 'reports.purchases',
      'financial' => 'reports.financial',
      _ => 'reports.view',
    };
    if (!widget.authState.can(permission)) {
      return false;
    }
    final canCost =
        widget.authState.can('reports.profitability') ||
        widget.authState.can('sales.cost_view');
    if (path.startsWith('purchases/') && !canCost) return false;
    if (path == 'inventory/turnover' && !canCost) return false;
    if (path == 'financial/customer-aging' &&
        !widget.authState.can('accounts.aging.receivables.view')) {
      return false;
    }
    if (path == 'financial/supplier-aging' &&
        !widget.authState.can('accounts.aging.payables.view')) {
      return false;
    }
    if (path == 'management/budget' &&
        !widget.authState.can('accounts.budgets.view')) {
      return false;
    }
    if (path == 'management/godowns') {
      return widget.authState.can('reports.inventory');
    }
    if (path == 'management/branches') {
      return [
        'reports.sales',
        'reports.purchases',
        'reports.inventory',
        'reports.financial',
        'reports.profitability',
        'accounts.journal.view',
      ].every(widget.authState.can);
    }
    if (path == 'sales/below-cost' &&
        !widget.authState.can('reports.profitability')) {
      return false;
    }
    if (path.startsWith('management/') &&
        [
          'financial',
          'working-capital',
          'budget',
        ].contains(path.split('/').last)) {
      return widget.authState.can('reports.financial') &&
          widget.authState.can('reports.profitability') &&
          widget.authState.can('accounts.journal.view');
    }
    return true;
  }

  Map<String, String> _choices(String section) => Map.fromEntries(
    _reports[section]!.entries.where((e) => _allowed(e.value)),
  );
  List<String> get _sections =>
      _reports.keys.where((s) => _choices(s).isNotEmpty).toList();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      await _load();
      try {
        final options = await widget.authState.report(
          'management/filters',
          fromUtc: _range.$1.toUtc(),
          toUtc: _range.$2.toUtc(),
        );
        if (mounted && options is Map) {
          setState(() {
            _lookups = Map<String, dynamic>.from(options);
            _branches = (options['branches'] as List? ?? [])
                .cast<Map<String, dynamic>>();
            _godowns = (options['godowns'] as List? ?? [])
                .cast<Map<String, dynamic>>();
          });
        }
      } catch (_) {
        /* Overview remains usable if filter lookup fails. */
      }
    });
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  (DateTime, DateTime) get _range {
    final range = reportPeriod(_period);
    return _period == 'Custom'
        ? (_customFrom ?? range.$1, _customTo ?? range.$2)
        : range;
  }

  String? get _option => _path == 'inventory/abc'
      ? _abcBasis
      : _period == 'This Month'
      ? 'month'
      : _period == 'This Year'
      ? 'year'
      : null;
  Map<String, String> get _filters => {
    ..._advanced,
    'page': '$_page',
    'pageSize': '50',
    'godownId': ?_godown,
    'productId': ?_product,
    if (_search.text.isNotEmpty) 'search': _search.text,
  };

  Future<void> _editFilters() async {
    final draft = Map<String, String>.from(_advanced);
    var basis = _abcBasis;
    final fields = <String, String>{
      if ([
        'Sales',
        'Profitability',
        'Inventory',
        'Purchasing',
        'Godowns',
      ].contains(_section)) ...{
        'productId': 'Products',
        'categoryId': 'Categories',
        'manufacturerId': 'Manufacturers',
      },
      if (['Sales', 'Profitability', 'Customers'].contains(_section))
        'customerId': 'Customers',
      if (['Purchasing', 'Suppliers', 'Inventory'].contains(_section))
        'supplierId': 'Suppliers',
      if (['Sales', 'Profitability'].contains(_section)) ...{
        'userId': 'Users',
        'priceLevelId': 'PriceLevels',
      },
    };
    final applied = await showDialog<bool>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, update) => AlertDialog(
          title: const Text('Report filters'),
          content: SizedBox(
            width: 460,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  for (final field in fields.entries)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: DropdownButtonFormField<String>(
                        initialValue: draft[field.key],
                        isExpanded: true,
                        decoration: InputDecoration(
                          labelText: _label(field.key.replaceAll('Id', '')),
                        ),
                        items: [
                          const DropdownMenuItem<String>(
                            value: null,
                            child: Text('All'),
                          ),
                          for (final row
                              in (_lookups[field.value[0].toLowerCase() +
                                          field.value.substring(1)]
                                      as List? ??
                                  []))
                            DropdownMenuItem(
                              value: '${row['id']}',
                              child: Text(
                                '${row['name']}',
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                        ],
                        onChanged: (value) {
                          if (value == null) {
                            draft.remove(field.key);
                          } else {
                            draft[field.key] = value;
                          }
                        },
                      ),
                    ),
                  if (['Sales', 'Profitability'].contains(_section))
                    DropdownButtonFormField<String>(
                      initialValue: draft['saleType'],
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Sale type'),
                      items: const [
                        DropdownMenuItem<String>(
                          value: null,
                          child: Text('All'),
                        ),
                        DropdownMenuItem(
                          value: 'Retail',
                          child: Text('Retail'),
                        ),
                        DropdownMenuItem(
                          value: 'Wholesale',
                          child: Text('Wholesale'),
                        ),
                      ],
                      onChanged: (value) {
                        if (value == null) {
                          draft.remove('saleType');
                        } else {
                          draft['saleType'] = value;
                        }
                      },
                    ),
                  if (_path == 'inventory/position')
                    DropdownButtonFormField<String>(
                      initialValue: draft['status'],
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'Stock status',
                      ),
                      items: const [
                        DropdownMenuItem<String>(
                          value: null,
                          child: Text('All'),
                        ),
                        DropdownMenuItem(
                          value: 'low',
                          child: Text('Low stock'),
                        ),
                        DropdownMenuItem(
                          value: 'out',
                          child: Text('Out of stock'),
                        ),
                        DropdownMenuItem(
                          value: 'reorder',
                          child: Text('Reorder'),
                        ),
                      ],
                      onChanged: (value) {
                        if (value == null) {
                          draft.remove('status');
                        } else {
                          draft['status'] = value;
                        }
                      },
                    ),
                  if (_path == 'inventory/batch-position')
                    DropdownButtonFormField<String>(
                      initialValue: draft['status'],
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'Expiry status',
                      ),
                      items: const [
                        DropdownMenuItem<String>(
                          value: null,
                          child: Text('All'),
                        ),
                        DropdownMenuItem(
                          value: 'near',
                          child: Text('Within 30 days'),
                        ),
                        DropdownMenuItem(
                          value: 'expired',
                          child: Text('Expired'),
                        ),
                      ],
                      onChanged: (value) {
                        if (value == null) {
                          draft.remove('status');
                        } else {
                          draft['status'] = value;
                        }
                      },
                    ),
                  if (_path == 'inventory/abc') ...[
                    DropdownButtonFormField<String>(
                      initialValue: basis,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'ABC basis'),
                      items: [
                        const DropdownMenuItem(
                          value: 'sales',
                          child: Text('Sales value'),
                        ),
                        if (widget.authState.can('reports.profitability'))
                          const DropdownMenuItem(
                            value: 'profit',
                            child: Text('Gross profit'),
                          ),
                        if (widget.authState.can('reports.profitability') ||
                            widget.authState.can('sales.cost_view'))
                          const DropdownMenuItem(
                            value: 'inventory',
                            child: Text('Inventory value'),
                          ),
                      ],
                      onChanged: (value) =>
                          update(() => basis = value ?? 'sales'),
                    ),
                    for (final field in {
                      'abcA': 'A cumulative threshold %',
                      'abcB': 'B cumulative threshold %',
                    }.entries)
                      TextFormField(
                        initialValue:
                            draft[field.key] ??
                            (field.key == 'abcA' ? '80' : '95'),
                        decoration: InputDecoration(labelText: field.value),
                        keyboardType: TextInputType.number,
                        onChanged: (value) => draft[field.key] = value,
                      ),
                  ],
                  if (_section == 'Inventory')
                    for (final field in {
                      'fastMovingQuantity': 'Fast moving quantity',
                      'slowMovingDays': 'Slow moving days',
                      'deadStockDays': 'Dead stock days',
                    }.entries)
                      TextFormField(
                        initialValue:
                            draft[field.key] ??
                            (field.key == 'fastMovingQuantity'
                                ? '100'
                                : field.key == 'slowMovingDays'
                                ? '90'
                                : '180'),
                        decoration: InputDecoration(labelText: field.value),
                        keyboardType: TextInputType.number,
                        onChanged: (value) => draft[field.key] = value,
                      ),
                  const SizedBox(height: 12),
                  const Text(
                    'Selectors show up to 200 matches. Search report rows to narrow large lists.',
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Cancel'),
            ),
            TextButton(
              onPressed: () {
                draft.clear();
                Navigator.pop(context, true);
              },
              child: const Text('Clear'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Apply'),
            ),
          ],
        ),
      ),
    );
    if (applied == true && mounted) {
      setState(() {
        _advanced
          ..clear()
          ..addAll(draft);
        _abcBasis = basis;
        _page = 1;
      });
      await _load();
    }
  }

  Future<void> _load() async {
    final request = ++_request;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final data = await widget.authState.report(
        _path,
        fromUtc: _range.$1.toUtc(),
        toUtc: _range.$2.toUtc(),
        branchId: _branch,
        option: _option,
        filters: _filters,
      );
      if (mounted && request == _request) setState(() => _data = data);
    } catch (_) {
      if (mounted && request == _request) {
        setState(() => _error = 'The management report could not be loaded.');
      }
    } finally {
      if (mounted && request == _request) setState(() => _loading = false);
    }
  }

  Future<void> _periodChanged(String value) async {
    if (value == 'Custom') {
      final range = await showDateRangePicker(
        context: context,
        firstDate: DateTime(2020),
        lastDate: DateTime.now(),
        helpText: 'Pakistan business dates',
      );
      if (range == null || !mounted) return;
      _customFrom = DateTime.utc(
        range.start.year,
        range.start.month,
        range.start.day,
      ).subtract(const Duration(hours: 5));
      _customTo = DateTime.utc(
        range.end.year,
        range.end.month,
        range.end.day,
      ).add(const Duration(days: 1)).subtract(const Duration(hours: 5));
    }
    setState(() {
      _period = value;
      _page = 1;
    });
    await _load();
  }

  Future<void> _export() async {
    try {
      final bytes = await widget.authState.exportReport(
        _path,
        fromUtc: _range.$1.toUtc(),
        toUtc: _range.$2.toUtc(),
        branchId: _branch,
        option: _option,
        filters: _filters,
      );
      String path;
      if (widget.saveCsv != null) {
        path = await widget.saveCsv!(bytes);
      } else {
        final home = Platform.environment['USERPROFILE'];
        if (home == null) throw const FileSystemException();
        final file = File(
          '$home/Downloads/pharmacy-mis-${DateTime.now().millisecondsSinceEpoch}.csv',
        );
        await file.writeAsBytes(bytes, flush: true);
        path = file.path;
      }
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text('CSV saved to $path')));
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('CSV export could not be completed.')),
        );
      }
    }
  }

  Future<void> _drill(Map<String, dynamic> row) async {
    try {
      if (row['chartOfAccountId'] != null &&
          widget.authState.can('accounts.journal.view')) {
        final ledger = await widget.authState.accounting(
          'general-ledger',
          query: {
            'chartOfAccountId': '${row['chartOfAccountId']}',
            'fromUtc': _range.$1.toUtc().toIso8601String(),
            'toUtc': _range.$2
                .toUtc()
                .subtract(const Duration(microseconds: 1))
                .toIso8601String(),
            'branchId': ?_branch,
            'page': '1',
            'pageSize': '50',
          },
        );
        if (!mounted || ledger is! Map) return;
        await showDialog<void>(
          context: context,
          builder: (context) => AlertDialog(
            title: Text('${row['accountName'] ?? 'Account activity'}'),
            content: SizedBox(
              width: 650,
              height: 400,
              child: SingleChildScrollView(
                child: _structured(Map<String, dynamic>.from(ledger)),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('Close'),
              ),
            ],
          ),
        );
      } else if ((_path == 'sales/daily' ||
              _path == 'profitability/invoices' ||
              _path == 'sales/return-analysis') &&
          widget.authState.can('sales.view')) {
        final sale = await widget.authState.saleDetails(
          '${row['id'] ?? row['key']}',
        );
        if (!mounted) return;
        await showDialog<void>(
          context: context,
          builder: (context) => AlertDialog(
            title: Text(sale.invoiceNumber ?? 'Sale detail'),
            content: SizedBox(
              width: 650,
              child: SingleChildScrollView(
                child: _table(
                  sale.items
                      .map(
                        (i) => <String, dynamic>{
                          'product': i.productName,
                          'quantity': i.requestedQuantity,
                          'discount': i.discountAmount,
                          'net': i.netAmount,
                        },
                      )
                      .toList(),
                  drill: false,
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('Close'),
              ),
            ],
          ),
        );
      } else if (_path.contains('customer') &&
          ![
            'sales/daily',
            'financial/customer-statement',
            'financial/customer-payments',
          ].contains(_path)) {
        final id = row['customerId'] ?? row['partyId'] ?? row['key'];
        if (id == null || id == 'walk-in') return;
        setState(() {
          _advanced['customerId'] = '$id';
          _page = 1;
          if (widget.authState.can('reports.financial')) {
            _section = 'Customers';
            _path = 'financial/customer-statement';
          } else {
            _section = 'Sales';
            _path = 'sales/daily';
          }
        });
        await _load();
      } else if (_path.contains('supplier') &&
          ![
            'financial/supplier-statement',
            'financial/supplier-payments',
          ].contains(_path)) {
        final id = row['supplierId'] ?? row['partyId'] ?? row['key'];
        if (id == null) return;
        setState(() {
          _advanced['supplierId'] = '$id';
          _page = 1;
          if (widget.authState.can('reports.financial')) {
            _section = 'Suppliers';
            _path = 'financial/supplier-statement';
          } else {
            _section = 'Purchasing';
            _path = 'purchases/price-history';
          }
        });
        await _load();
      } else if (_path.contains('branch') ||
          _path == 'management/godowns' ||
          _path == 'sales/godowns') {
        setState(() {
          _branch =
              row['branchId']?.toString() ??
              (_path.contains('branch') ? row['key']?.toString() : _branch);
          _godown = row['godownId']?.toString();
          _page = 1;
          if (_path.contains('godown')) {
            _godown = '${row['godownId'] ?? row['key']}';
            _section = 'Inventory';
            _path = 'inventory/position';
          } else {
            _section = 'Sales';
            _path = 'sales/daily';
          }
        });
        await _load();
      } else if (row['batchId'] != null &&
          widget.authState.can('reports.inventory')) {
        setState(() {
          _advanced['batchId'] = '${row['batchId']}';
          _product = '${row['productId']}';
          _section = 'Inventory';
          _path = 'inventory/movements';
          _page = 1;
        });
        await _load();
      } else if (row['productId'] != null ||
          ((_path.endsWith('/products') || _path == 'purchases/by-product') &&
              row['key'] != null)) {
        setState(() {
          _product = '${row['productId'] ?? row['key']}';
          _branch = row['branchId']?.toString() ?? _branch;
          _godown = row['godownId']?.toString() ?? _godown;
          _path = _section == 'Purchasing'
              ? 'purchases/price-history'
              : _section == 'Inventory' || _section == 'Godowns'
              ? 'inventory/batch-position'
              : 'sales/daily';
          _page = 1;
        });
        await _load();
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Detail could not be loaded.')),
        );
      }
    }
  }

  bool _canDrill(Map<String, dynamic> row) {
    if (row['chartOfAccountId'] != null) {
      return widget.authState.can('accounts.journal.view');
    }
    if ([
      'sales/daily',
      'profitability/invoices',
      'sales/return-analysis',
    ].contains(_path)) {
      return widget.authState.can('sales.view');
    }
    if (_path.contains('customer') &&
        ![
          'financial/customer-statement',
          'financial/customer-payments',
        ].contains(_path)) {
      return widget.authState.can('reports.financial') ||
          widget.authState.can('reports.sales');
    }
    if (_path.contains('supplier') &&
        ![
          'financial/supplier-statement',
          'financial/supplier-payments',
        ].contains(_path)) {
      return widget.authState.can('reports.financial') ||
          widget.authState.can('reports.purchases');
    }
    if (_path.contains('branch')) return widget.authState.can('reports.sales');
    if (['management/godowns', 'sales/godowns'].contains(_path)) {
      return widget.authState.can('reports.inventory');
    }
    return row['productId'] != null ||
        _path.endsWith('/products') ||
        _path == 'purchases/by-product';
  }

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  'Management / MIS',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              if (widget.authState.can('reports.export'))
                IconButton(
                  tooltip: 'Export MIS CSV',
                  onPressed: _loading ? null : _export,
                  icon: const Icon(Icons.download_outlined),
                ),
            ],
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 12,
            runSpacing: 10,
            children: [
              _dropdown('MIS section', _section, _sections, (value) {
                setState(() {
                  _section = value;
                  _path = _choices(value).values.first;
                  _page = 1;
                  _product = null;
                  _advanced.clear();
                });
                _load();
              }),
              SizedBox(
                width: 220,
                child: DropdownButtonFormField<String>(
                  key: ValueKey(_section),
                  initialValue: _choices(_section).values.contains(_path)
                      ? _path
                      : null,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Report'),
                  items: _choices(_section).entries
                      .map(
                        (e) => DropdownMenuItem(
                          value: e.value,
                          child: Text(e.key, overflow: TextOverflow.ellipsis),
                        ),
                      )
                      .toList(),
                  onChanged: (value) {
                    if (value != null) {
                      setState(() {
                        _path = value;
                        _advanced.remove('status');
                        _page = 1;
                      });
                      _load();
                    }
                  },
                ),
              ),
              _dropdown('Comparison period', _period, const [
                'Today',
                'This Week',
                'This Month',
                'This Year',
                'Custom',
              ], _periodChanged),
              SizedBox(
                width: 200,
                child: DropdownButtonFormField<String>(
                  key: ValueKey('branch-$_branch'),
                  initialValue: _branch,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Branch'),
                  items: [
                    const DropdownMenuItem<String>(
                      value: null,
                      child: Text('Authorized branches'),
                    ),
                    ..._branches.map(
                      (b) => DropdownMenuItem(
                        value: '${b['id']}',
                        child: Text(
                          '${b['name']}',
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ),
                  ],
                  onChanged: (value) {
                    setState(() {
                      _branch = value;
                      _godown = null;
                      _page = 1;
                    });
                    _load();
                  },
                ),
              ),
              SizedBox(
                width: 200,
                child: DropdownButtonFormField<String>(
                  key: ValueKey('godown-$_branch-$_godown'),
                  initialValue: _godown,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Godown'),
                  items: [
                    const DropdownMenuItem<String>(
                      value: null,
                      child: Text('Authorized godowns'),
                    ),
                    ..._godowns
                        .where(
                          (g) => _branch == null || g['branchId'] == _branch,
                        )
                        .map(
                          (g) => DropdownMenuItem(
                            value: '${g['id']}',
                            child: Text(
                              '${g['name']}',
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ),
                  ],
                  onChanged: (value) {
                    setState(() {
                      _godown = value;
                      _page = 1;
                    });
                    _load();
                  },
                ),
              ),
              SizedBox(
                width: 190,
                child: TextField(
                  controller: _search,
                  decoration: const InputDecoration(labelText: 'Search'),
                  onSubmitted: (_) {
                    _page = 1;
                    _load();
                  },
                ),
              ),
              IconButton(
                tooltip: 'Filter MIS report',
                onPressed: _loading ? null : _editFilters,
                icon: const Icon(Icons.filter_alt_outlined),
              ),
              IconButton(
                tooltip: 'Refresh MIS',
                onPressed: _loading ? null : _load,
                icon: const Icon(Icons.refresh),
              ),
              if (_product != null)
                ActionChip(
                  label: const Text('Clear product drilldown'),
                  onPressed: () {
                    setState(() {
                      _product = null;
                      _page = 1;
                    });
                    _load();
                  },
                ),
            ],
          ),
          const SizedBox(height: 8),
          const Text(
            'Pakistan business dates • selected-period flows • stock and balances at period end',
            style: TextStyle(fontSize: 12),
          ),
          const SizedBox(height: 12),
          Expanded(
            child: _loading
                ? const AppLoadingState()
                : _error != null
                ? Center(child: Text(_error!))
                : _content(),
          ),
          if (_data is Map && (_data as Map)['total'] is num)
            Row(
              children: [
                Text('Page $_page • ${(_data as Map)['total']} rows'),
                const Spacer(),
                TextButton(
                  onPressed: _page > 1 && !_loading
                      ? () {
                          _page--;
                          _load();
                        }
                      : null,
                  child: const Text('Previous'),
                ),
                TextButton(
                  onPressed:
                      _page * 50 < ((_data as Map)['total'] as num) && !_loading
                      ? () {
                          _page++;
                          _load();
                        }
                      : null,
                  child: const Text('Next'),
                ),
              ],
            ),
        ],
      ),
    ),
  );

  Widget _dropdown(
    String label,
    String value,
    List<String> choices,
    ValueChanged<String> changed,
  ) => SizedBox(
    width: 190,
    child: DropdownButtonFormField<String>(
      key: ValueKey('$label-$value'),
      initialValue: value,
      isExpanded: true,
      decoration: InputDecoration(labelText: label),
      items: choices
          .map(
            (v) => DropdownMenuItem(
              value: v,
              child: Text(v, overflow: TextOverflow.ellipsis),
            ),
          )
          .toList(),
      onChanged: (v) {
        if (v != null) changed(v);
      },
    ),
  );

  Widget _content() {
    final data = _data;
    if (data == null) {
      return AppEmptyState(title: 'No management data for this period.');
    }
    if (data is Map && data.containsKey('comparison')) {
      return _overview(Map<String, dynamic>.from(data));
    }
    if (data is Map &&
        data.values.any((v) => v is Map || v is List) &&
        data['items'] is! List) {
      return ListView(children: [_structured(Map<String, dynamic>.from(data))]);
    }
    final rows = data is List
        ? data
        : data is Map && data['items'] is List
        ? data['items'] as List
        : [data];
    if (rows.isEmpty) {
      return AppEmptyState(title: 'No management data for this period.');
    }
    return SingleChildScrollView(
      child: _table(
        rows.whereType<Map>().map((r) => Map<String, dynamic>.from(r)).toList(),
      ),
    );
  }

  Widget _structured(Map<String, dynamic> data) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      for (final entry in data.entries.where((e) => _visible(e.key))) ...[
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 8),
          child: Text(
            _label(entry.key),
            style: Theme.of(context).textTheme.titleMedium,
          ),
        ),
        if (entry.value is Map)
          _structured(Map<String, dynamic>.from(entry.value as Map))
        else if (entry.value is List)
          _table(
            (entry.value as List)
                .whereType<Map>()
                .map((r) => Map<String, dynamic>.from(r))
                .toList(),
          )
        else
          Text(_value(entry.value)),
      ],
    ],
  );

  String _label(String key) =>
      key.replaceAllMapped(RegExp(r'([a-z])([A-Z])'), (m) => '${m[1]} ${m[2]}');
  String _value(dynamic value) => value is num
      ? value.toStringAsFixed(2)
      : value is Map || value is List
      ? jsonEncode(value)
      : '${value ?? '—'}';
  bool _visible(String key) {
    if ([
          'grossProfit',
          'grossMarginPercent',
          'netProfit',
          'marginPercent',
        ].contains(key) &&
        !widget.authState.can('reports.profitability')) {
      return false;
    }
    if ([
          'costOfGoodsSold',
          'stockValue',
          'inventoryValue',
          'costValue',
          'purchaseCost',
          'purchaseValue',
          'potentialLoss',
        ].contains(key) &&
        !widget.authState.can('reports.profitability') &&
        !widget.authState.can('sales.cost_view')) {
      return false;
    }
    return true;
  }

  Widget _table(List<Map<String, dynamic>> rows, {bool drill = true}) {
    if (rows.isEmpty) return const Text('No management data for this period.');
    final keys = rows.first.keys
        .where((k) => _visible(k) && k != 'id' && k != 'key')
        .toList();
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: AppDataTable(
        columns: [
          for (final k in keys) DataColumn(label: Text(_label(k))),
          if (drill) const DataColumn(label: Text('Detail')),
        ],
        rows: rows
            .map(
              (row) => DataRow(
                cells: [
                  for (final k in keys)
                    DataCell(
                      ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 350),
                        child: Text(
                          _value(row[k]),
                          maxLines: 3,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ),
                  if (drill)
                    DataCell(
                      IconButton(
                        tooltip: 'Open report detail',
                        onPressed: _canDrill(row) ? () => _drill(row) : null,
                        icon: const Icon(Icons.open_in_new),
                      ),
                    ),
                ],
              ),
            )
            .toList(),
      ),
    );
  }

  Widget _overview(Map<String, dynamic> data) {
    final sales = (data['sales'] as Map?) ?? {};
    final profit = (data['profitability'] as Map?) ?? {};
    final purchases = (data['purchases'] as Map?) ?? {};
    final inventory = (data['inventory'] as Map?) ?? {};
    final finance = (data['finance'] as Map?) ?? {};
    final position = (finance['position'] as Map?) ?? {};
    final metrics = <String, dynamic>{
      ...(data['calendarSales'] as Map? ?? {}).cast<String, dynamic>(),
      ...(data['calendarPurchases'] as Map? ?? {}).cast<String, dynamic>(),
      ...sales.cast<String, dynamic>(),
      ...profit.cast<String, dynamic>(),
      ...purchases.cast<String, dynamic>(),
      ...inventory.cast<String, dynamic>(),
      ...position.cast<String, dynamic>(),
      if (finance['expenses'] != null) 'expenses': finance['expenses'],
      ...(Map<String, dynamic>.from(finance)..removeWhere((k, v) => v is! num)),
    };
    metrics.removeWhere((k, v) => !(_visible(k)) || v is! num || k == 'key');
    final comparisons = (data['comparison'] as List? ?? [])
        .whereType<Map>()
        .map((r) => Map<String, dynamic>.from(r))
        .toList();
    final trend = (data['trend'] as List? ?? [])
        .whereType<Map>()
        .map((r) => Map<String, dynamic>.from(r))
        .toList();
    final expiry = (data['expiryExposure'] as List? ?? [])
        .whereType<Map>()
        .map((r) => Map<String, dynamic>.from(r))
        .toList();
    return ListView(
      children: [
        Wrap(
          spacing: 10,
          runSpacing: 10,
          children: metrics.entries
              .map(
                (e) => SizedBox(
                  width: 190,
                  height: 108,
                  child: Card(
                    child: InkWell(
                      onTap:
                          e.key == 'netSales' &&
                              widget.authState.can('reports.sales')
                          ? () {
                              setState(() {
                                _section = 'Sales';
                                _path = 'sales/daily';
                                _page = 1;
                              });
                              _load();
                            }
                          : null,
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text(_label(e.key)),
                            Text(
                              _value(e.value),
                              style: Theme.of(context).textTheme.titleLarge,
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              )
              .toList(),
        ),
        if (metrics.isEmpty)
          const Text('No management data for the granted report permissions.'),
        const SizedBox(height: 16),
        Text(
          'Period comparison',
          style: Theme.of(context).textTheme.titleMedium,
        ),
        _table(comparisons, drill: false),
        if (trend.isNotEmpty) _lineChart('Sales Trend', trend, 'netSales'),
        if (trend.isNotEmpty && widget.authState.can('reports.profitability'))
          _lineChart('Gross Profit Trend', trend, 'grossProfit'),
        if (sales['netSales'] != null && purchases['netPurchaseValue'] != null)
          _barChart('Sales vs Purchases', [
            {'bucket': 'Sales', 'amount': sales['netSales']},
            {'bucket': 'Purchases', 'amount': purchases['netPurchaseValue']},
          ], 'amount'),
        if (position.isNotEmpty)
          _barChart('Receivables vs Payables', [
            {'bucket': 'Receivables', 'amount': position['receivables']},
            {'bucket': 'Payables', 'amount': position['payables']},
          ], 'amount'),
        if (expiry.isNotEmpty)
          _barChart(
            'Expiry Exposure',
            expiry,
            _visible('costValue') ? 'costValue' : 'quantity',
          ),
        for (final key in [
          'topProducts',
          'topCustomers',
          'topSuppliers',
          'branchPerformance',
        ])
          if (data[key] is List && (data[key] as List).isNotEmpty) ...[
            const SizedBox(height: 16),
            Text(_label(key), style: Theme.of(context).textTheme.titleMedium),
            _table(
              (data[key] as List)
                  .whereType<Map>()
                  .map((r) => Map<String, dynamic>.from(r))
                  .toList(),
            ),
          ],
        if (data['cashActivity'] is Map)
          _structured({'cashActivity': data['cashActivity']}),
      ],
    );
  }

  Widget _lineChart(
    String title,
    List<Map<String, dynamic>> rows,
    String metric,
  ) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 16),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 12),
        SizedBox(
          height: 230,
          child: LineChart(
            LineChartData(
              minX: 0,
              maxX: rows.length > 1 ? (rows.length - 1).toDouble() : 1,
              lineBarsData: [
                LineChartBarData(
                  spots: [
                    for (var i = 0; i < rows.length; i++)
                      FlSpot(
                        i.toDouble(),
                        (rows[i][metric] as num? ?? 0).toDouble(),
                      ),
                  ],
                  color: AppColors.primary,
                  barWidth: 2,
                  dotData: const FlDotData(show: false),
                ),
              ],
              lineTouchData: LineTouchData(
                touchTooltipData: LineTouchTooltipData(
                  fitInsideHorizontally: true,
                  fitInsideVertically: true,
                  getTooltipItems: (spots) => spots
                      .map(
                        (s) => LineTooltipItem(
                          '${rows[s.spotIndex]['name']}\n${s.y.toStringAsFixed(2)}',
                          const TextStyle(color: Colors.white),
                        ),
                      )
                      .toList(),
                ),
              ),
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
                    reservedSize: 32,
                    interval: rows.length > 6
                        ? (rows.length / 5).ceilToDouble()
                        : 1,
                    getTitlesWidget: (v, meta) {
                      final i = v.toInt();
                      return Text(
                        i >= 0 && i < rows.length ? '${rows[i]['name']}' : '',
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
  );

  Widget _barChart(
    String title,
    List<Map<String, dynamic>> rows,
    String metric,
  ) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 16),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 12),
        SizedBox(
          height: 220,
          child: BarChart(
            BarChartData(
              barGroups: [
                for (var i = 0; i < rows.length; i++)
                  BarChartGroupData(
                    x: i,
                    barRods: [
                      BarChartRodData(
                        toY: (rows[i][metric] as num? ?? 0).toDouble(),
                        color: AppColors.primary,
                        width: 24,
                      ),
                    ],
                  ),
              ],
              barTouchData: BarTouchData(
                touchTooltipData: BarTouchTooltipData(
                  getTooltipItem: (group, groupIndex, rod, rodIndex) =>
                      BarTooltipItem(
                        '${rows[groupIndex]['bucket']}\n${rod.toY.toStringAsFixed(2)}',
                        const TextStyle(color: Colors.white),
                      ),
                ),
              ),
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
                    reservedSize: 32,
                    getTitlesWidget: (v, meta) => Text(
                      '${rows[v.toInt().clamp(0, rows.length - 1)]['bucket']}',
                      style: const TextStyle(fontSize: 10),
                    ),
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
  );
}
