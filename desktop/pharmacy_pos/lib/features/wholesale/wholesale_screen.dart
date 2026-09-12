import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';
import '../pricing/price_source_label.dart';

class WholesaleScreen extends StatefulWidget {
  const WholesaleScreen({required this.authState, super.key});
  final AuthState authState;

  @override
  State<WholesaleScreen> createState() => _WholesaleScreenState();
}

class _WholesaleLine {
  _WholesaleLine({required this.product});
  final ProductListItem product;
  int quantity = 1;
  double discountPercent = 0;
  double? manualPrice;
  String? priceOverrideReason;
  ResolvedPrice? resolved;
}

class _WholesaleScreenState extends State<WholesaleScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs = TabController(length: 2, vsync: this);

  final _customerSearch = TextEditingController();
  final _productSearch = TextEditingController();
  final _poNumber = TextEditingController();
  List<CustomerLookup> _customerResults = [];
  List<ProductListItem> _productResults = [];
  CustomerLookup? _customer;
  CustomerDetails? _customerDetails;
  List<GodownLookup> _godowns = [];
  String? _godownId;
  final List<_WholesaleLine> _lines = [];
  bool _posting = false;
  String? _error;
  SaleDetails? _lastSale;
  PagedSales? _history;

  bool can(String permission) => widget.authState.can(permission);

  @override
  void initState() {
    super.initState();
    widget.authState.myGodowns().then((g) {
      if (mounted) setState(() => _godowns = g);
    });
    _loadHistory();
  }

  @override
  void dispose() {
    _tabs.dispose();
    _customerSearch.dispose();
    _productSearch.dispose();
    _poNumber.dispose();
    super.dispose();
  }

  Future<void> _loadHistory() async {
    final history = await widget.authState.listSales();
    if (mounted) {
      setState(
        () => _history = PagedSales(
          items: history.items.where((x) => x.saleType == 'Wholesale').toList(),
          totalCount: history.totalCount,
        ),
      );
    }
  }

  double get _grossTotal => _lines.fold(0, (sum, line) {
    final price =
        line.manualPrice ?? line.resolved?.price ?? line.product.retailPrice;
    final gross = price * line.quantity;
    return sum + gross - (gross * line.discountPercent / 100);
  });

  Future<void> _selectCustomer(CustomerLookup customer) async {
    setState(() {
      _customer = customer;
      _customerResults = [];
      _customerSearch.text = customer.name;
    });
    try {
      final details = await widget.authState.customerDetails(customer.id);
      if (mounted) setState(() => _customerDetails = details);
    } on ApiException {
      // Non-fatal: the invoice can still proceed with the lookup summary.
    }
    for (final line in _lines) {
      await _resolvePrice(line);
    }
  }

  Future<void> _resolvePrice(_WholesaleLine line) async {
    try {
      final query = <String, String>{
        'productId': line.product.id,
        'quantity': '${line.quantity}',
        'saleType': 'Wholesale',
      };
      if (_customer != null) query['customerId'] = _customer!.id;
      final data = await widget.authState.phase6('sale-price', query: query);
      if (mounted) {
        setState(
          () => line.resolved = ResolvedPrice.fromJson(
            data as Map<String, dynamic>,
          ),
        );
      }
    } on ApiException {
      // Preview only - fall back silently to the product's default retail price.
    }
  }

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(24, 22, 24, 0),
          child: Text(
            'Wholesale Sales',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
        ),
        TabBar(
          controller: _tabs,
          isScrollable: true,
          tabs: const [
            Tab(text: 'New Invoice'),
            Tab(text: 'Wholesale History'),
          ],
        ),
        Expanded(
          child: TabBarView(
            controller: _tabs,
            children: [_invoiceTab(), _historyTab()],
          ),
        ),
      ],
    ),
  );

  Widget _historyTab() {
    final items = _history?.items ?? const <SaleListItem>[];
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          columns: const [
            DataColumn(label: Text('Invoice')),
            DataColumn(label: Text('Customer')),
            DataColumn(label: Text('Net Total')),
            DataColumn(label: Text('Credit')),
            DataColumn(label: Text('Status')),
          ],
          rows: items
              .map(
                (s) => DataRow(
                  cells: [
                    DataCell(Text(s.invoiceNumber ?? '-')),
                    DataCell(Text(s.customerName ?? 'Walk-in')),
                    DataCell(Text(s.netTotal.toStringAsFixed(2))),
                    DataCell(Text(s.creditAmount.toStringAsFixed(2))),
                    DataCell(Text(s.status)),
                  ],
                ),
              )
              .toList(),
        ),
      ),
    );
  }

  Widget _invoiceTab() => SingleChildScrollView(
    padding: const EdgeInsets.all(24),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  TextField(
                    key: const Key('wholesale_customer_search'),
                    controller: _customerSearch,
                    decoration: const InputDecoration(
                      labelText: 'Customer (required for wholesale credit)',
                      prefixIcon: Icon(Icons.person_search),
                    ),
                    onChanged: (v) async {
                      if (v.trim().isEmpty) {
                        setState(() => _customerResults = []);
                        return;
                      }
                      final results = await widget.authState.lookupCustomers(
                        search: v,
                      );
                      if (mounted) setState(() => _customerResults = results);
                    },
                  ),
                  if (_customerResults.isNotEmpty)
                    Container(
                      constraints: const BoxConstraints(maxHeight: 160),
                      decoration: BoxDecoration(
                        border: Border.all(color: Colors.grey),
                      ),
                      child: ListView(
                        shrinkWrap: true,
                        children: _customerResults
                            .map(
                              (c) => ListTile(
                                dense: true,
                                title: Text(c.name),
                                subtitle: Text(c.customerCode),
                                onTap: () => _selectCustomer(c),
                              ),
                            )
                            .toList(),
                      ),
                    ),
                  if (_customerDetails != null) ...[
                    const SizedBox(height: 8),
                    Card(
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              'Price level: ${_customerDetails!.priceLevelName ?? 'Default retail'}',
                            ),
                            Text(
                              'Credit limit: ${_customerDetails!.creditLimit.toStringAsFixed(2)}',
                            ),
                            Text(
                              'Outstanding: ${_customerDetails!.outstandingBalance.toStringAsFixed(2)}',
                            ),
                            Text(
                              'Available credit: ${_customerDetails!.availableCredit.toStringAsFixed(2)}',
                            ),
                            if (!_customerDetails!.creditAllowed)
                              const Text(
                                'Credit sales are not allowed for this customer.',
                                style: TextStyle(color: Colors.red),
                              ),
                          ],
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: 16),
            SizedBox(
              width: 260,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  DropdownButtonFormField<String?>(
                    initialValue: _godownId,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Godown'),
                    items: [
                      const DropdownMenuItem(
                        value: null,
                        child: Text(
                          'Default',
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      ..._godowns.map(
                        (g) => DropdownMenuItem(
                          value: g.id,
                          child: Text(
                            g.name,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ),
                    ],
                    onChanged: (v) => setState(() => _godownId = v),
                  ),
                  TextField(
                    controller: _poNumber,
                    decoration: const InputDecoration(
                      labelText: 'Customer PO Number',
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        TextField(
          key: const Key('wholesale_product_search'),
          controller: _productSearch,
          decoration: const InputDecoration(
            labelText: 'Add product',
            prefixIcon: Icon(Icons.search),
          ),
          onChanged: (v) async {
            if (v.trim().isEmpty) {
              setState(() => _productResults = []);
              return;
            }
            final paged = await widget.authState.listProducts(search: v);
            if (mounted) setState(() => _productResults = paged.items);
          },
        ),
        if (_productResults.isNotEmpty)
          Container(
            constraints: const BoxConstraints(maxHeight: 200),
            decoration: BoxDecoration(border: Border.all(color: Colors.grey)),
            child: ListView(
              shrinkWrap: true,
              children: _productResults
                  .map(
                    (p) => ListTile(
                      dense: true,
                      title: Text(p.name),
                      subtitle: Text(
                        '${p.sku} - ${p.retailPrice.toStringAsFixed(2)}',
                      ),
                      onTap: () async {
                        final line = _WholesaleLine(product: p);
                        setState(() {
                          _lines.add(line);
                          _productResults = [];
                          _productSearch.clear();
                        });
                        await _resolvePrice(line);
                      },
                    ),
                  )
                  .toList(),
            ),
          ),
        const SizedBox(height: 12),
        if (_lines.isNotEmpty)
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: DataTable(
              columns: const [
                DataColumn(label: Text('Product')),
                DataColumn(label: Text('Qty')),
                DataColumn(label: Text('Resolved Price')),
                DataColumn(label: Text('Source')),
                DataColumn(label: Text('Override')),
                DataColumn(label: Text('Disc %')),
                DataColumn(label: Text('Net')),
                DataColumn(label: Text('')),
              ],
              rows: _lines.map((line) {
                final price =
                    line.manualPrice ??
                    line.resolved?.price ??
                    line.product.retailPrice;
                final gross = price * line.quantity;
                final net = gross - (gross * line.discountPercent / 100);
                return DataRow(
                  cells: [
                    DataCell(Text(line.product.name)),
                    DataCell(
                      SizedBox(
                        width: 60,
                        child: TextFormField(
                          initialValue: '${line.quantity}',
                          keyboardType: TextInputType.number,
                          onChanged: (v) async {
                            line.quantity = int.tryParse(v) ?? line.quantity;
                            await _resolvePrice(line);
                            setState(() {});
                          },
                        ),
                      ),
                    ),
                    DataCell(Text(price.toStringAsFixed(2))),
                    DataCell(
                      Text(
                        priceSourceLabel(
                          line.manualPrice != null
                              ? 'ManualOverride'
                              : line.resolved?.source,
                        ),
                      ),
                    ),
                    DataCell(
                      can('sales.price_override')
                          ? IconButton(
                              icon: const Icon(Icons.edit_outlined),
                              tooltip: 'Override price',
                              onPressed: () => _showOverrideDialog(line),
                            )
                          : const SizedBox.shrink(),
                    ),
                    DataCell(
                      SizedBox(
                        width: 60,
                        child: TextFormField(
                          initialValue: '${line.discountPercent}',
                          keyboardType: TextInputType.number,
                          onChanged: (v) => setState(
                            () => line.discountPercent =
                                double.tryParse(v) ?? line.discountPercent,
                          ),
                        ),
                      ),
                    ),
                    DataCell(Text(net.toStringAsFixed(2))),
                    DataCell(
                      IconButton(
                        icon: const Icon(Icons.delete_outline),
                        onPressed: () => setState(() => _lines.remove(line)),
                      ),
                    ),
                  ],
                );
              }).toList(),
            ),
          ),
        const SizedBox(height: 16),
        Align(
          alignment: Alignment.centerRight,
          child: Text(
            'Total: ${_grossTotal.toStringAsFixed(2)}',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
        ),
        const SizedBox(height: 12),
        if (_error != null)
          Text(
            _error!,
            style: TextStyle(color: Theme.of(context).colorScheme.error),
          ),
        FilledButton.icon(
          key: const Key('post_wholesale_sale'),
          onPressed: _posting || _lines.isEmpty ? null : _post,
          icon: const Icon(Icons.point_of_sale),
          label: const Text('Post Wholesale Invoice'),
        ),
        if (_lastSale != null) ...[
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Posted: ${_lastSale!.invoiceNumber}',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  Text('Net total: ${_lastSale!.netTotal.toStringAsFixed(2)}'),
                  Text(
                    'Credit amount: ${_lastSale!.creditAmount.toStringAsFixed(2)}',
                  ),
                  if (_lastSale!.dueDateUtc != null)
                    Text('Due date: ${_lastSale!.dueDateUtc}'),
                  for (final item in _lastSale!.items.where((item) => item.priceSource != 'Default')) Text('${item.productName}: ${priceSourceLabel(item.priceSource)}'),
                ],
              ),
            ),
          ),
        ],
      ],
    ),
  );

  Future<void> _showOverrideDialog(_WholesaleLine line) async {
    final priceController = TextEditingController(
      text:
          (line.manualPrice ?? line.resolved?.price ?? line.product.retailPrice)
              .toStringAsFixed(2),
    );
    final reasonController = TextEditingController(
      text: line.priceOverrideReason,
    );
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Override Selling Price'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              key: const Key('override_price_amount'),
              controller: priceController,
              decoration: const InputDecoration(labelText: 'Unit price'),
              keyboardType: TextInputType.number,
            ),
            TextField(
              key: const Key('override_price_reason'),
              controller: reasonController,
              decoration: const InputDecoration(labelText: 'Reason (required)'),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Apply'),
          ),
        ],
      ),
    );
    if (ok == true) {
      setState(() {
        line.manualPrice = double.tryParse(priceController.text);
        line.priceOverrideReason = reasonController.text.trim();
      });
    }
  }

  Future<void> _post() async {
    setState(() {
      _posting = true;
      _error = null;
    });
    try {
      final accounts = (await widget.authState.listFinancialAccounts(
        branchId: widget.authState.currentUser!.branch.id,
      )).where((x) => x.isActive && x.accountType == 'Cash').toList();
      final total = _grossTotal;
      List<Map<String, dynamic>> payments = [];
      if (accounts.isNotEmpty) {
        payments = [
          {
            'method': 1,
            'amountApplied': total,
            'tenderedAmount': total,
            'financialAccountId': accounts.first.id,
          },
        ];
      }
      final values = {
        'branchId': widget.authState.currentUser!.branch.id,
        'customerId': _customer?.id,
        'notes': null,
        'godownId': _godownId,
        'saleType': 'Wholesale',
        'customerPoNumber': _poNumber.text.trim().isEmpty
            ? null
            : _poNumber.text.trim(),
        'items': _lines
            .map(
              (l) => {
                'productId': l.product.id,
                'quantity': l.quantity,
                'discountPercent': l.discountPercent,
                if (l.manualPrice != null) 'unitPriceOverride': l.manualPrice,
                if (l.manualPrice != null)
                  'priceOverrideReason': l.priceOverrideReason,
              },
            )
            .toList(),
        'payments': payments,
      };
      final sale = await widget.authState.postSale(values);
      setState(() {
        _lastSale = sale;
        _lines.clear();
        _customer = null;
        _customerDetails = null;
        _customerSearch.clear();
        _poNumber.clear();
      });
      await _loadHistory();
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _posting = false);
    }
  }
}
