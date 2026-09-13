import 'package:flutter/material.dart';
import '../../ui/app_widgets.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

class SalesOrdersScreen extends StatefulWidget {
  const SalesOrdersScreen({required this.authState, super.key});
  final AuthState authState;

  @override
  State<SalesOrdersScreen> createState() => _SalesOrdersScreenState();
}

class _SalesOrdersScreenState extends State<SalesOrdersScreen> {
  final _search = TextEditingController();
  String? _statusFilter;
  PagedSalesOrders? _orders;
  bool _loading = true;
  String? _error;

  bool can(String permission) => widget.authState.can(permission);

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final query = <String, String>{'page': '1', 'pageSize': '50'};
      if (_search.text.trim().isNotEmpty) query['search'] = _search.text.trim();
      if (_statusFilter != null) query['status'] = _statusFilter!;
      final data = await widget.authState.salesOrders('', query: query);
      final paged = PagedSalesOrders.fromJson(data as Map<String, dynamic>);
      if (mounted) setState(() => _orders = paged);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Column(
      children: [
        AppPageHeader(title: 'Sales Orders'),
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 12),
          child: AppFilterBar(
            children: [
              SizedBox(
                width: 260,
                child: TextField(
                  key: const Key('sales_order_search'),
                  controller: _search,
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search),
                    labelText: 'Search orders',
                  ),
                  onSubmitted: (_) => _load(),
                ),
              ),
              SizedBox(
                width: 180,
                child: DropdownButtonFormField<String?>(
                  initialValue: _statusFilter,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Status'),
                  items: const [
                    DropdownMenuItem(
                      value: null,
                      child: Text(
                        'All',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    DropdownMenuItem(
                      value: 'Draft',
                      child: Text(
                        'Draft',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    DropdownMenuItem(
                      value: 'Confirmed',
                      child: Text(
                        'Confirmed',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    DropdownMenuItem(
                      value: 'PartiallyFulfilled',
                      child: Text(
                        'Partially Fulfilled',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    DropdownMenuItem(
                      value: 'Fulfilled',
                      child: Text(
                        'Fulfilled',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    DropdownMenuItem(
                      value: 'Cancelled',
                      child: Text(
                        'Cancelled',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ],
                  onChanged: (v) {
                    setState(() => _statusFilter = v);
                    _load();
                  },
                ),
              ),
              IconButton.filledTonal(
                onPressed: _load,
                tooltip: 'Refresh',
                icon: const Icon(Icons.refresh),
              ),
              if (can('sales_orders.create'))
                FilledButton.icon(
                  key: const Key('add_sales_order'),
                  onPressed: () => _showForm(),
                  icon: const Icon(Icons.add),
                  label: const Text('New Order'),
                ),
            ],
          ),
        ),
        Expanded(child: _body()),
      ],
    ),
  );

  Widget _body() {
    if (_loading) return const AppLoadingState();
    if (_error != null) return AppErrorState(_error!, onRetry: _load);
    final items = _orders?.items ?? const <SalesOrderListItem>[];
    if (items.isEmpty) return AppEmptyState(title: 'No sales orders found');
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: AppDataTable(
          columns: const [
            DataColumn(label: Text('Order #')),
            DataColumn(label: Text('Customer')),
            DataColumn(label: Text('Date')),
            DataColumn(label: Text('Status')),
            DataColumn(label: Text('Total'), numeric: true),
            DataColumn(label: Text('Ordered')),
            DataColumn(label: Text('Fulfilled')),
            DataColumn(label: Text('Remaining')),
            DataColumn(label: Text('')),
          ],
          rows: items
              .map(
                (o) => DataRow(
                  cells: [
                    DataCell(Text(o.orderNumber)),
                    DataCell(Text(o.customerName)),
                    DataCell(Text(_fmtDate(o.orderDate))),
                    DataCell(AppStatusChip(o.status)),
                    DataCell(Text(o.netTotal.toStringAsFixed(2))),
                    DataCell(Text('${o.orderedQuantity}')),
                    DataCell(Text('${o.fulfilledQuantity}')),
                    DataCell(
                      Text('${o.orderedQuantity - o.fulfilledQuantity}'),
                    ),
                    DataCell(
                      IconButton(
                        tooltip: 'Open',
                        icon: const Icon(Icons.open_in_new),
                        onPressed: () => _openDetails(o.id),
                      ),
                    ),
                  ],
                ),
              )
              .toList(),
        ),
      ),
    );
  }

  Future<void> _showForm() async {
    final created = await showDialog<bool>(
      context: context,
      builder: (_) => _SalesOrderForm(authState: widget.authState),
    );
    if (created == true) await _load();
  }

  Future<void> _openDetails(String id) async {
    final changed = await showDialog<bool>(
      context: context,
      builder: (_) =>
          _SalesOrderDetailsDialog(authState: widget.authState, id: id),
    );
    if (changed == true) await _load();
  }
}

String _fmtDate(DateTime value) =>
    '${value.year.toString().padLeft(4, '0')}-${value.month.toString().padLeft(2, '0')}-${value.day.toString().padLeft(2, '0')}';

class _LineDraft {
  _LineDraft({required this.product});
  final ProductListItem product;
  int quantity = 1;
  double discountPercent = 0;
}

class _SalesOrderForm extends StatefulWidget {
  const _SalesOrderForm({required this.authState});
  final AuthState authState;

  @override
  State<_SalesOrderForm> createState() => _SalesOrderFormState();
}

class _SalesOrderFormState extends State<_SalesOrderForm> {
  final _customerSearch = TextEditingController();
  final _productSearch = TextEditingController();
  final _notes = TextEditingController();
  List<CustomerLookup> _customerResults = [];
  List<ProductListItem> _productResults = [];
  CustomerLookup? _customer;
  DateTime? _expectedDelivery;
  final List<_LineDraft> _lines = [];
  String? _error;
  bool _saving = false;

  @override
  void dispose() {
    _customerSearch.dispose();
    _productSearch.dispose();
    _notes.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('New Sales Order'),
    content: SizedBox(
      width: 640,
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            TextField(
              key: const Key('sales_order_customer_search'),
              controller: _customerSearch,
              decoration: InputDecoration(
                labelText: 'Customer',
                prefixIcon: const Icon(Icons.person_search),
                suffixText: _customer?.name,
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
                          onTap: () => setState(() {
                            _customer = c;
                            _customerResults = [];
                            _customerSearch.text = c.name;
                          }),
                        ),
                      )
                      .toList(),
                ),
              ),
            OutlinedButton.icon(
              icon: const Icon(Icons.event),
              label: Text(
                _expectedDelivery == null
                    ? 'Expected delivery (optional)'
                    : 'Expected: ${_fmtDate(_expectedDelivery!)}',
              ),
              onPressed: () async {
                final picked = await showDatePicker(
                  context: context,
                  initialDate: DateTime.now().add(const Duration(days: 3)),
                  firstDate: DateTime.now(),
                  lastDate: DateTime.now().add(const Duration(days: 365)),
                );
                if (picked != null) setState(() => _expectedDelivery = picked);
              },
            ),
            const SizedBox(height: 12),
            TextField(
              key: const Key('sales_order_product_search'),
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
                constraints: const BoxConstraints(maxHeight: 160),
                decoration: BoxDecoration(
                  border: Border.all(color: Colors.grey),
                ),
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
                          onTap: () => setState(() {
                            _lines.add(_LineDraft(product: p));
                            _productResults = [];
                            _productSearch.clear();
                          }),
                        ),
                      )
                      .toList(),
                ),
              ),
            ..._lines.map(
              (line) => Padding(
                padding: const EdgeInsets.symmetric(vertical: 4),
                child: Row(
                  children: [
                    Expanded(flex: 3, child: Text(line.product.name)),
                    SizedBox(
                      width: 70,
                      child: TextFormField(
                        initialValue: '${line.quantity}',
                        decoration: const InputDecoration(labelText: 'Qty'),
                        keyboardType: TextInputType.number,
                        onChanged: (v) =>
                            line.quantity = int.tryParse(v) ?? line.quantity,
                      ),
                    ),
                    const SizedBox(width: 8),
                    SizedBox(
                      width: 70,
                      child: TextFormField(
                        initialValue: '${line.discountPercent}',
                        decoration: const InputDecoration(labelText: 'Disc %'),
                        keyboardType: TextInputType.number,
                        onChanged: (v) => line.discountPercent =
                            double.tryParse(v) ?? line.discountPercent,
                      ),
                    ),
                    IconButton(
                      icon: const Icon(Icons.delete_outline),
                      onPressed: () => setState(() => _lines.remove(line)),
                    ),
                  ],
                ),
              ),
            ),
            TextFormField(
              controller: _notes,
              decoration: const InputDecoration(labelText: 'Notes'),
              maxLines: 2,
            ),
            if (_error != null)
              Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
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
      FilledButton(
        key: const Key('save_sales_order'),
        onPressed: _saving ? null : _save,
        child: const Text('Save Draft'),
      ),
    ],
  );

  Future<void> _save() async {
    if (_customer == null) {
      setState(() => _error = 'Select a customer.');
      return;
    }
    if (_lines.isEmpty) {
      setState(() => _error = 'Add at least one product.');
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await widget.authState.salesOrders(
        '',
        method: 'POST',
        body: {
          'customerId': _customer!.id,
          'orderDate': DateTime.now().toIso8601String(),
          'expectedDeliveryDate': _expectedDelivery?.toIso8601String(),
          'notes': _notes.text.trim().isEmpty ? null : _notes.text.trim(),
          'items': _lines
              .map(
                (l) => {
                  'productId': l.product.id,
                  'quantity': l.quantity,
                  'discountPercent': l.discountPercent,
                },
              )
              .toList(),
        },
      );
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }
}

class _SalesOrderDetailsDialog extends StatefulWidget {
  const _SalesOrderDetailsDialog({required this.authState, required this.id});
  final AuthState authState;
  final String id;

  @override
  State<_SalesOrderDetailsDialog> createState() =>
      _SalesOrderDetailsDialogState();
}

class _SalesOrderDetailsDialogState extends State<_SalesOrderDetailsDialog> {
  SalesOrderDetails? _details;
  String? _error;
  bool _busy = false;
  bool _changed = false;
  final Map<String, int> _fulfillQty = {};

  bool can(String permission) => widget.authState.can(permission);

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final data = await widget.authState.salesOrders(widget.id);
      if (mounted) {
        setState(
          () => _details = SalesOrderDetails.fromJson(
            data as Map<String, dynamic>,
          ),
        );
      }
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    }
  }

  Future<void> _act(Future<void> Function() action) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await action();
      _changed = true;
      await _load();
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _fulfill() async {
    final details = _details;
    if (details == null) return;
    final items = details.items
        .where((i) => (_fulfillQty[i.id] ?? 0) > 0)
        .map((i) => {'productId': i.productId, 'quantity': _fulfillQty[i.id]})
        .toList();
    if (items.isEmpty) {
      setState(
        () => _error = 'Enter a quantity to fulfill for at least one line.',
      );
      return;
    }
    final accounts = (await widget.authState.listFinancialAccounts(
      branchId: widget.authState.currentUser!.branch.id,
    )).where((x) => x.isActive).toList();
    if (!mounted) return;
    final amount = details.items.fold<double>(
      0,
      (sum, i) => sum + ((_fulfillQty[i.id] ?? 0) * i.unitPrice),
    );
    final account = await showDialog<FinancialAccountInfo?>(
      context: context,
      builder: (context) => SimpleDialog(
        title: const Text('Payment account'),
        children: [
          SimpleDialogOption(
            onPressed: () =>
                Navigator.pop<FinancialAccountInfo?>(context, null),
            child: const Text('Record fully on customer credit'),
          ),
          ...accounts.map(
            (x) => SimpleDialogOption(
              onPressed: () => Navigator.pop(context, x),
              child: Text('${x.name} (${x.currentBalance.toStringAsFixed(2)})'),
            ),
          ),
        ],
      ),
    );
    if (!mounted) return;
    await _act(
      () => widget.authState.salesOrders(
        '${widget.id}/fulfill',
        method: 'POST',
        body: {
          'items': items,
          'payments': account == null
              ? <Map<String, dynamic>>[]
              : [
                  {
                    'method': 'Cash',
                    'amountApplied': amount,
                    'financialAccountId': account.id,
                  },
                ],
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final details = _details;
    return AlertDialog(
      title: Text(details == null ? 'Sales Order' : details.orderNumber),
      content: SizedBox(
        width: 680,
        child: details == null
            ? const SizedBox(height: 120, child: AppLoadingState())
            : SingleChildScrollView(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('${details.customerName} (${details.customerCode})'),
                    Text('Status: ${details.status}'),
                    Text(
                      'Date: ${_fmtDate(details.orderDate)}'
                      '${details.expectedDeliveryDate == null ? '' : '  ·  Expected ${_fmtDate(details.expectedDeliveryDate!)}'}',
                    ),
                    if (details.quotationNumber != null)
                      Text('From quotation: ${details.quotationNumber}'),
                    const SizedBox(height: 12),
                    SingleChildScrollView(
                      scrollDirection: Axis.horizontal,
                      child: AppDataTable(
                        columns: [
                          const DataColumn(label: Text('Product')),
                          const DataColumn(label: Text('Ordered')),
                          const DataColumn(label: Text('Fulfilled')),
                          const DataColumn(label: Text('Remaining')),
                          const DataColumn(label: Text('Price'), numeric: true),
                          if (details.status == 'Confirmed' ||
                              details.status == 'PartiallyFulfilled')
                            const DataColumn(label: Text('Fulfill now')),
                        ],
                        rows: details.items
                            .map(
                              (i) => DataRow(
                                cells: [
                                  DataCell(Text(i.productName)),
                                  DataCell(Text('${i.orderedQuantity}')),
                                  DataCell(Text('${i.fulfilledQuantity}')),
                                  DataCell(Text('${i.remainingQuantity}')),
                                  DataCell(
                                    Text(i.unitPrice.toStringAsFixed(2)),
                                  ),
                                  if (details.status == 'Confirmed' ||
                                      details.status == 'PartiallyFulfilled')
                                    DataCell(
                                      SizedBox(
                                        width: 70,
                                        child: TextFormField(
                                          key: Key('fulfill_qty_${i.id}'),
                                          decoration: const InputDecoration(
                                            hintText: '0',
                                          ),
                                          keyboardType: TextInputType.number,
                                          onChanged: (v) => _fulfillQty[i.id] =
                                              int.tryParse(v) ?? 0,
                                        ),
                                      ),
                                    ),
                                ],
                              ),
                            )
                            .toList(),
                      ),
                    ),
                    Align(
                      alignment: Alignment.centerRight,
                      child: Text(
                        'Total: ${details.netTotal.toStringAsFixed(2)}',
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                    ),
                    if (details.linkedSales.isNotEmpty) ...[
                      const SizedBox(height: 8),
                      Text(
                        'Linked invoices',
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                      ...details.linkedSales.map(
                        (s) => Text(
                          '${s.invoiceNumber ?? s.saleId} - ${s.netTotal.toStringAsFixed(2)}',
                        ),
                      ),
                    ],
                    if (details.cancellationReason != null)
                      Text('Cancelled: ${details.cancellationReason}'),
                    if (_error != null)
                      Text(
                        _error!,
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.error,
                        ),
                      ),
                  ],
                ),
              ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context, _changed),
          child: const Text('Close'),
        ),
        if (details != null &&
            details.status == 'Draft' &&
            can('sales_orders.confirm'))
          FilledButton(
            key: const Key('confirm_sales_order'),
            onPressed: _busy
                ? null
                : () => _act(
                    () => widget.authState.salesOrders(
                      '${widget.id}/confirm',
                      method: 'POST',
                    ),
                  ),
            child: const Text('Confirm'),
          ),
        if (details != null &&
            (details.status == 'Confirmed' ||
                details.status == 'PartiallyFulfilled') &&
            can('sales_orders.fulfill'))
          FilledButton(
            key: const Key('fulfill_sales_order'),
            onPressed: _busy ? null : _fulfill,
            child: const Text('Fulfill'),
          ),
        if (details != null &&
            !['Fulfilled', 'Cancelled'].contains(details.status) &&
            can('sales_orders.cancel'))
          TextButton(
            key: const Key('cancel_sales_order'),
            onPressed: _busy
                ? null
                : () => _act(
                    () => widget.authState.salesOrders(
                      '${widget.id}/cancel',
                      method: 'POST',
                      body: const {
                        'reason': 'Cancelled from Sales Orders screen',
                      },
                    ),
                  ),
            child: const Text('Cancel'),
          ),
      ],
    );
  }
}
