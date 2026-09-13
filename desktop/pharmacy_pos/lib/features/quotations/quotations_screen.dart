import 'package:flutter/material.dart';
import '../../ui/app_widgets.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

class QuotationsScreen extends StatefulWidget {
  const QuotationsScreen({required this.authState, super.key});
  final AuthState authState;

  @override
  State<QuotationsScreen> createState() => _QuotationsScreenState();
}

class _QuotationsScreenState extends State<QuotationsScreen> {
  final _search = TextEditingController();
  String? _statusFilter;
  PagedQuotations? _quotations;
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
      final data = await widget.authState.salesQuotations('', query: query);
      final paged = PagedQuotations.fromJson(data as Map<String, dynamic>);
      if (mounted) setState(() => _quotations = paged);
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
        AppPageHeader(title: 'Quotations'),
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 12),
          child: AppFilterBar(
            children: [
              SizedBox(
                width: 260,
                child: TextField(
                  key: const Key('quotation_search'),
                  controller: _search,
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search),
                    labelText: 'Search quotations',
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
                      value: 'Sent',
                      child: Text(
                        'Sent',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    DropdownMenuItem(
                      value: 'Accepted',
                      child: Text(
                        'Accepted',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    DropdownMenuItem(
                      value: 'Rejected',
                      child: Text(
                        'Rejected',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    DropdownMenuItem(
                      value: 'Expired',
                      child: Text(
                        'Expired',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    DropdownMenuItem(
                      value: 'Converted',
                      child: Text(
                        'Converted',
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
              if (can('quotations.create'))
                FilledButton.icon(
                  key: const Key('add_quotation'),
                  onPressed: () => _showForm(),
                  icon: const Icon(Icons.add),
                  label: const Text('New Quotation'),
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
    final items = _quotations?.items ?? const <QuotationListItem>[];
    if (items.isEmpty) return AppEmptyState(title: 'No quotations found');
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: AppDataTable(
          columns: const [
            DataColumn(label: Text('Quotation #')),
            DataColumn(label: Text('Customer')),
            DataColumn(label: Text('Date')),
            DataColumn(label: Text('Valid Until')),
            DataColumn(label: Text('Status')),
            DataColumn(label: Text('Total'), numeric: true),
            DataColumn(label: Text('Created By')),
            DataColumn(label: Text('')),
          ],
          rows: items
              .map(
                (q) => DataRow(
                  cells: [
                    DataCell(Text(q.quotationNumber)),
                    DataCell(Text(q.customerName)),
                    DataCell(Text(_fmtDate(q.quotationDate))),
                    DataCell(
                      Text(
                        q.validUntil == null ? '-' : _fmtDate(q.validUntil!),
                      ),
                    ),
                    DataCell(AppStatusChip(q.status)),
                    DataCell(Text(q.netTotal.toStringAsFixed(2))),
                    DataCell(Text(q.createdByName)),
                    DataCell(
                      IconButton(
                        tooltip: 'Open',
                        icon: const Icon(Icons.open_in_new),
                        onPressed: () => _openDetails(q.id),
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
      builder: (_) => _QuotationForm(authState: widget.authState),
    );
    if (created == true) await _load();
  }

  Future<void> _openDetails(String id) async {
    final changed = await showDialog<bool>(
      context: context,
      builder: (_) =>
          _QuotationDetailsDialog(authState: widget.authState, id: id),
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

class _QuotationForm extends StatefulWidget {
  const _QuotationForm({required this.authState});
  final AuthState authState;

  @override
  State<_QuotationForm> createState() => _QuotationFormState();
}

class _QuotationFormState extends State<_QuotationForm> {
  final _customerSearch = TextEditingController();
  final _productSearch = TextEditingController();
  final _notes = TextEditingController();
  List<CustomerLookup> _customerResults = [];
  List<ProductListItem> _productResults = [];
  CustomerLookup? _customer;
  DateTime? _validUntil;
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

  double get _total => _lines.fold(0, (sum, line) {
    final gross = line.product.retailPrice * line.quantity;
    return sum + gross - (gross * line.discountPercent / 100);
  });

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('New Quotation'),
    content: SizedBox(
      width: 640,
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            TextField(
              key: const Key('quotation_customer_search'),
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
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    icon: const Icon(Icons.event),
                    label: Text(
                      _validUntil == null
                          ? 'Valid until (optional)'
                          : 'Valid until: ${_fmtDate(_validUntil!)}',
                    ),
                    onPressed: () async {
                      final picked = await showDatePicker(
                        context: context,
                        initialDate: DateTime.now().add(
                          const Duration(days: 7),
                        ),
                        firstDate: DateTime.now(),
                        lastDate: DateTime.now().add(const Duration(days: 365)),
                      );
                      if (picked != null) setState(() => _validUntil = picked);
                    },
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            TextField(
              key: const Key('quotation_product_search'),
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
            const SizedBox(height: 12),
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
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerRight,
              child: Text(
                'Estimated total (server resolves the final price): ${_total.toStringAsFixed(2)}',
                style: Theme.of(context).textTheme.titleMedium,
              ),
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
        key: const Key('save_quotation'),
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
      await widget.authState.salesQuotations(
        '',
        method: 'POST',
        body: {
          'customerId': _customer!.id,
          'quotationDate': DateTime.now().toIso8601String(),
          'validUntil': _validUntil?.toIso8601String(),
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

class _QuotationDetailsDialog extends StatefulWidget {
  const _QuotationDetailsDialog({required this.authState, required this.id});
  final AuthState authState;
  final String id;

  @override
  State<_QuotationDetailsDialog> createState() =>
      _QuotationDetailsDialogState();
}

class _QuotationDetailsDialogState extends State<_QuotationDetailsDialog> {
  QuotationDetails? _details;
  String? _error;
  bool _busy = false;
  bool _changed = false;

  bool can(String permission) => widget.authState.can(permission);

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final data = await widget.authState.salesQuotations(widget.id);
      if (mounted) {
        setState(
          () => _details = QuotationDetails.fromJson(
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

  @override
  Widget build(BuildContext context) {
    final details = _details;
    return AlertDialog(
      title: Text(details == null ? 'Quotation' : details.quotationNumber),
      content: SizedBox(
        width: 640,
        child: details == null
            ? const SizedBox(height: 120, child: AppLoadingState())
            : SingleChildScrollView(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('${details.customerName} (${details.customerCode})'),
                    Text('Status: ${details.status}'),
                    Text(
                      'Date: ${_fmtDate(details.quotationDate)}'
                      '${details.validUntil == null ? '' : '  ·  Valid until ${_fmtDate(details.validUntil!)}'}',
                    ),
                    if (details.priceLevelName != null)
                      Text('Price level: ${details.priceLevelName}'),
                    const SizedBox(height: 12),
                    SingleChildScrollView(
                      scrollDirection: Axis.horizontal,
                      child: AppDataTable(
                        columns: const [
                          DataColumn(label: Text('Product')),
                          DataColumn(label: Text('Qty'), numeric: true),
                          DataColumn(label: Text('Price'), numeric: true),
                          DataColumn(label: Text('Disc %')),
                          DataColumn(label: Text('Net')),
                        ],
                        rows: details.items
                            .map(
                              (i) => DataRow(
                                cells: [
                                  DataCell(Text(i.productName)),
                                  DataCell(Text('${i.quantity}')),
                                  DataCell(
                                    Text(i.unitPrice.toStringAsFixed(2)),
                                  ),
                                  DataCell(
                                    Text(i.discountPercent.toStringAsFixed(1)),
                                  ),
                                  DataCell(
                                    Text(i.netAmount.toStringAsFixed(2)),
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
                    if (details.convertedToSalesOrderNumber != null)
                      Text(
                        'Converted to order ${details.convertedToSalesOrderNumber}',
                      ),
                    if (details.convertedToSaleInvoiceNumber != null)
                      Text(
                        'Converted to sale ${details.convertedToSaleInvoiceNumber}',
                      ),
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
            can('quotations.send'))
          FilledButton(
            key: const Key('send_quotation'),
            onPressed: _busy
                ? null
                : () => _act(
                    () => widget.authState.salesQuotations(
                      '${widget.id}/send',
                      method: 'POST',
                    ),
                  ),
            child: const Text('Send'),
          ),
        if (details != null &&
            (details.status == 'Draft' || details.status == 'Sent') &&
            can('quotations.accept'))
          FilledButton(
            key: const Key('accept_quotation'),
            onPressed: _busy
                ? null
                : () => _act(
                    () => widget.authState.salesQuotations(
                      '${widget.id}/accept',
                      method: 'POST',
                    ),
                  ),
            child: const Text('Accept'),
          ),
        if (details != null &&
            (details.status == 'Draft' || details.status == 'Sent') &&
            can('quotations.accept'))
          OutlinedButton(
            key: const Key('reject_quotation'),
            onPressed: _busy
                ? null
                : () => _act(
                    () => widget.authState.salesQuotations(
                      '${widget.id}/reject',
                      method: 'POST',
                      body: const {'reason': null},
                    ),
                  ),
            child: const Text('Reject'),
          ),
        if (details != null &&
            details.status == 'Accepted' &&
            can('quotations.convert'))
          FilledButton(
            key: const Key('convert_quotation_order'),
            onPressed: _busy
                ? null
                : () => _act(
                    () => widget.authState.salesQuotations(
                      '${widget.id}/convert-to-order',
                      method: 'POST',
                      body: const {'expectedDeliveryDate': null},
                    ),
                  ),
            child: const Text('Convert to Order'),
          ),
        if (details != null &&
            !['Converted', 'Cancelled'].contains(details.status) &&
            can('quotations.cancel'))
          TextButton(
            key: const Key('cancel_quotation'),
            onPressed: _busy
                ? null
                : () => _act(
                    () => widget.authState.salesQuotations(
                      '${widget.id}/cancel',
                      method: 'POST',
                      body: const {
                        'reason': 'Cancelled from Quotations screen',
                      },
                    ),
                  ),
            child: const Text('Cancel'),
          ),
      ],
    );
  }
}
