import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';
import '../phase6/phase6_suggestions_dialog.dart';

class PurchasingScreen extends StatefulWidget {
  const PurchasingScreen({required this.authState, super.key});
  final AuthState authState;
  @override
  State<PurchasingScreen> createState() => _PurchasingScreenState();
}

class _PurchasingScreenState extends State<PurchasingScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs = TabController(length: 3, vsync: this);
  PagedPurchaseOrders? _orders;
  PagedPurchases? _purchases;
  PagedPurchaseReturns? _returns;
  PurchaseReturnDetails? _returnNote;
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
    _tabs.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final orders = can('purchase_orders.view')
          ? await widget.authState.listPurchaseOrders()
          : null;
      final purchases = can('purchases.view')
          ? await widget.authState.listPurchases()
          : null;
      final returns = can('purchase_returns.view')
          ? await widget.authState.listPurchaseReturns()
          : null;
      if (mounted) {
        setState(() {
          _orders = orders;
          _purchases = purchases;
          _returns = returns;
        });
      }
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
        Padding(
          padding: const EdgeInsets.fromLTRB(24, 22, 24, 12),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  'Purchasing',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              IconButton.filledTonal(
                onPressed: _load,
                tooltip: 'Refresh',
                icon: const Icon(Icons.refresh),
              ),
              const SizedBox(width: 8),
              if (can('purchase_orders.create'))
                FilledButton.icon(
                  key: const Key('new_purchase_order'),
                  onPressed: _showPurchaseOrder,
                  icon: const Icon(Icons.note_add_outlined),
                  label: const Text('New PO'),
                ),
              const SizedBox(width: 8),
              if (can('purchases.receive') && can('purchases.create'))
                FilledButton.icon(
                  key: const Key('direct_purchase'),
                  onPressed: () => _showReceipt(),
                  icon: const Icon(Icons.add_shopping_cart),
                  label: const Text('Direct Purchase'),
                ),
            ],
          ),
        ),
        TabBar(
          controller: _tabs,
          tabs: const [
            Tab(text: 'Purchase Orders'),
            Tab(text: 'Purchase History'),
            Tab(text: 'Purchase Returns'),
          ],
        ),
        Expanded(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : _error != null
              ? Center(child: Text(_error!))
              : TabBarView(
                  controller: _tabs,
                  children: [
                    _ordersTable(),
                    _purchasesTable(),
                    _returnsTable(),
                  ],
                ),
        ),
      ],
    ),
  );

  Widget _ordersTable() {
    final orders = _orders?.items ?? const <PurchaseOrderListItem>[];
    if (!can('purchase_orders.view')) {
      return const Center(child: Text('Not available'));
    }
    if (orders.isEmpty) {
      return const Center(child: Text('No purchase orders found'));
    }
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          columns: const [
            DataColumn(label: Text('PO #')),
            DataColumn(label: Text('Supplier')),
            DataColumn(label: Text('Branch')),
            DataColumn(label: Text('Order Date')),
            DataColumn(label: Text('Items')),
            DataColumn(label: Text('Received')),
            DataColumn(label: Text('Status')),
            DataColumn(label: Text('Actions')),
          ],
          rows: orders
              .map(
                (order) => DataRow(
                  cells: [
                    DataCell(Text(order.orderNumber)),
                    DataCell(Text(order.supplierName)),
                    DataCell(Text(order.branchName)),
                    DataCell(Text(_date(order.orderDate))),
                    DataCell(Text('${order.itemCount}')),
                    DataCell(
                      Text(
                        '${order.receivedQuantity}/${order.orderedQuantity}',
                      ),
                    ),
                    DataCell(
                      Chip(
                        label: Text(order.status),
                        visualDensity: VisualDensity.compact,
                      ),
                    ),
                    DataCell(
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          if (can('purchase_orders.update') &&
                              order.status == 'Draft')
                            IconButton(
                              tooltip: 'Submit PO',
                              onPressed: () => _submit(order),
                              icon: const Icon(Icons.send_outlined),
                            ),
                          if (can('purchases.receive') &&
                              (order.status == 'Submitted' ||
                                  order.status == 'PartiallyReceived'))
                            IconButton(
                              tooltip: 'Receive goods',
                              onPressed: () => _showReceipt(order: order),
                              icon: const Icon(Icons.inventory_outlined),
                            ),
                          if (can('purchase_orders.cancel') &&
                              (order.status == 'Draft' ||
                                  order.status == 'Submitted'))
                            IconButton(
                              tooltip: 'Cancel PO',
                              onPressed: () => _cancel(order),
                              icon: const Icon(Icons.cancel_outlined),
                            ),
                        ],
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

  Widget _purchasesTable() {
    final purchases = _purchases?.items ?? const <PurchaseHistoryItem>[];
    if (!can('purchases.view')) {
      return const Center(child: Text('Not available'));
    }
    if (purchases.isEmpty) {
      return const Center(child: Text('No purchases found'));
    }
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          columns: const [
            DataColumn(label: Text('Action')),
            DataColumn(label: Text('GRN #')),
            DataColumn(label: Text('Invoice')),
            DataColumn(label: Text('Supplier')),
            DataColumn(label: Text('Branch')),
            DataColumn(label: Text('Date')),
            DataColumn(label: Text('Net')),
            DataColumn(label: Text('Status')),
            DataColumn(label: Text('Return')),
          ],
          rows: purchases
              .map(
                (purchase) => DataRow(
                  cells: [
                    DataCell(
                      can('purchase_returns.create') &&
                              purchase.status == 'Posted' &&
                              purchase.returnState != 'FullyReturned'
                          ? IconButton(
                              key: Key('return_purchase_${purchase.id}'),
                              tooltip: 'Return to supplier',
                              onPressed: () => _startReturn(purchase),
                              icon: const Icon(
                                Icons.assignment_return_outlined,
                              ),
                            )
                          : const SizedBox.shrink(),
                    ),
                    DataCell(Text(purchase.grnNumber)),
                    DataCell(Text(purchase.supplierInvoiceNumber ?? '-')),
                    DataCell(Text(purchase.supplierName)),
                    DataCell(Text(purchase.branchName)),
                    DataCell(Text(_date(purchase.receiptDate))),
                    DataCell(Text(_money(purchase.netTotal))),
                    DataCell(
                      Chip(
                        label: Text(purchase.status),
                        visualDensity: VisualDensity.compact,
                      ),
                    ),
                    DataCell(Text(_returnStateLabel(purchase.returnState))),
                  ],
                ),
              )
              .toList(),
        ),
      ),
    );
  }

  Widget _returnsTable() {
    final returns = _returns?.items ?? const <PurchaseReturnListItem>[];
    if (!can('purchase_returns.view')) {
      return const Center(child: Text('Not available'));
    }
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: returns.isEmpty
              ? const Center(child: Text('No purchase returns found'))
              : SingleChildScrollView(
                  padding: const EdgeInsets.all(24),
                  child: SingleChildScrollView(
                    scrollDirection: Axis.horizontal,
                    child: DataTable(
                      columns: const [
                        DataColumn(label: Text('Action')),
                        DataColumn(label: Text('Return #')),
                        DataColumn(label: Text('GRN')),
                        DataColumn(label: Text('Supplier')),
                        DataColumn(label: Text('Date')),
                        DataColumn(label: Text('Paid')),
                        DataColumn(label: Text('Bonus')),
                        DataColumn(label: Text('Credit')),
                        DataColumn(label: Text('Reason')),
                      ],
                      rows: returns
                          .map(
                            (item) => DataRow(
                              cells: [
                                DataCell(
                                  SizedBox(
                                    width: 96,
                                    child: Row(
                                      mainAxisSize: MainAxisSize.min,
                                      children: [
                                        IconButton(
                                          tooltip: 'View return note',
                                          onPressed: () =>
                                              _showReturnNote(item),
                                          icon: const Icon(Icons.receipt_long),
                                        ),
                                        if (can('purchase_returns.reprint'))
                                          IconButton(
                                            tooltip: 'Reprint return note',
                                            onPressed: () => _showReturnNote(
                                              item,
                                              reprint: true,
                                            ),
                                            icon: const Icon(
                                              Icons.print_outlined,
                                            ),
                                          ),
                                      ],
                                    ),
                                  ),
                                ),
                                DataCell(Text(item.returnNumber)),
                                DataCell(Text(item.originalGrnNumber)),
                                DataCell(Text(item.supplierName)),
                                DataCell(Text(_date(item.returnDateUtc))),
                                DataCell(Text('${item.paidQuantity}')),
                                DataCell(Text('${item.bonusQuantity}')),
                                DataCell(Text(_money(item.netSupplierCredit))),
                                DataCell(Text(item.reason)),
                              ],
                            ),
                          )
                          .toList(),
                    ),
                  ),
                ),
        ),
        if (_returnNote != null)
          SizedBox(width: 360, child: _returnNotePanel(_returnNote!)),
      ],
    );
  }

  Widget _returnNotePanel(PurchaseReturnDetails note) => Card(
    key: const Key('purchase_return_note_preview'),
    margin: const EdgeInsets.fromLTRB(0, 24, 24, 24),
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            'PURCHASE RETURN',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 8),
          Text(note.returnNumber),
          Text('Original GRN: ${note.originalGrnNumber}'),
          Text('Supplier: ${note.supplierName}'),
          Text('Credit: ${_money(note.netSupplierCredit)}'),
          const Divider(),
          ...note.items.map(
            (item) => Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(
                '${item.productName} ${item.batchNumber}: paid ${item.paidReturnQuantity}, bonus ${item.bonusReturnQuantity}',
              ),
            ),
          ),
        ],
      ),
    ),
  );

  Future<void> _startReturn(PurchaseHistoryItem purchase) async {
    try {
      final returnable = await widget.authState.returnablePurchase(purchase.id);
      if (!mounted) return;
      final result = await showDialog<PurchaseReturnDetails>(
        context: context,
        builder: (_) => _PurchaseReturnDialog(
          authState: widget.authState,
          receipt: returnable,
        ),
      );
      if (result != null && mounted) {
        setState(() {
          _returnNote = result;
          _tabs.index = 2;
        });
        await _load();
      }
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    }
  }

  Future<void> _showReturnNote(
    PurchaseReturnListItem item, {
    bool reprint = false,
  }) async {
    try {
      final note = reprint
          ? await widget.authState.reprintPurchaseReturnNote(item.id)
          : await widget.authState.purchaseReturnNote(item.id);
      if (mounted) setState(() => _returnNote = note);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    }
  }

  Future<void> _showPurchaseOrder() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => _PurchaseOrderDialog(authState: widget.authState),
    );
    if (ok == true) await _load();
  }

  Future<void> _showReceipt({PurchaseOrderListItem? order}) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => _ReceiptDialog(authState: widget.authState, order: order),
    );
    if (ok == true) await _load();
  }

  Future<void> _submit(PurchaseOrderListItem order) async {
    await widget.authState.submitPurchaseOrder(order.id);
    await _load();
  }

  Future<void> _cancel(PurchaseOrderListItem order) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Cancel purchase order'),
        content: const Text(
          'Only unreceived purchase orders can be cancelled.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Close'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Cancel PO'),
          ),
        ],
      ),
    );
    if (ok == true) {
      await widget.authState.cancelPurchaseOrder(order.id);
      await _load();
    }
  }
}

class _PurchaseReturnDialog extends StatefulWidget {
  const _PurchaseReturnDialog({required this.authState, required this.receipt});
  final AuthState authState;
  final ReturnablePurchase receipt;

  @override
  State<_PurchaseReturnDialog> createState() => _PurchaseReturnDialogState();
}

class _PurchaseReturnDialogState extends State<_PurchaseReturnDialog> {
  final _form = GlobalKey<FormState>();
  final _notes = TextEditingController();
  final _paid = <String, TextEditingController>{};
  final _bonus = <String, TextEditingController>{};
  String _reason = 'Damaged';
  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    for (final item in widget.receipt.items) {
      _paid[item.id] = TextEditingController(text: '0')
        ..addListener(() => setState(() {}));
      _bonus[item.id] = TextEditingController(text: '0')
        ..addListener(() => setState(() {}));
    }
  }

  @override
  void dispose() {
    _notes.dispose();
    for (final controller in [..._paid.values, ..._bonus.values]) {
      controller.dispose();
    }
    super.dispose();
  }

  double get _creditPreview => widget.receipt.items.fold(0, (sum, item) {
    final paid = int.tryParse(_paid[item.id]?.text ?? '') ?? 0;
    if (paid <= 0 || item.paidQuantityRemaining <= 0) return sum;
    final ratio = paid / item.paidQuantityRemaining;
    return sum + (item.netRemainingSupplierCredit * ratio);
  });

  int get _physicalPreview => widget.receipt.items.fold(
    0,
    (sum, item) =>
        sum +
        (int.tryParse(_paid[item.id]?.text ?? '') ?? 0) +
        (int.tryParse(_bonus[item.id]?.text ?? '') ?? 0),
  );

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Return to supplier'),
    content: SizedBox(
      width: 980,
      child: Form(
        key: _form,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '${widget.receipt.grnNumber} - ${widget.receipt.supplierName}',
              ),
              Text(
                'Supplier credit preview ${_money(_creditPreview)} for $_physicalPreview units',
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                key: const Key('purchase_return_reason'),
                initialValue: _reason,
                decoration: const InputDecoration(labelText: 'Reason'),
                items: const [
                  DropdownMenuItem(value: 'Damaged', child: Text('Damaged')),
                  DropdownMenuItem(value: 'Expired', child: Text('Expired')),
                  DropdownMenuItem(
                    value: 'SupplierRecall',
                    child: Text('Supplier recall'),
                  ),
                  DropdownMenuItem(
                    value: 'WrongItem',
                    child: Text('Wrong item'),
                  ),
                  DropdownMenuItem(
                    value: 'ExcessSupply',
                    child: Text('Excess supply'),
                  ),
                  DropdownMenuItem(value: 'Other', child: Text('Other')),
                ],
                onChanged: (value) =>
                    setState(() => _reason = value ?? _reason),
              ),
              TextFormField(
                key: const Key('purchase_return_notes'),
                controller: _notes,
                decoration: const InputDecoration(labelText: 'Notes'),
                validator: (value) =>
                    _reason == 'Other' && (value?.trim().isEmpty ?? true)
                    ? 'Notes are required for Other'
                    : null,
              ),
              const SizedBox(height: 12),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: DataTable(
                  columns: const [
                    DataColumn(label: Text('Product')),
                    DataColumn(label: Text('Batch')),
                    DataColumn(label: Text('Expiry')),
                    DataColumn(label: Text('Paid left')),
                    DataColumn(label: Text('Bonus left')),
                    DataColumn(label: Text('Stock')),
                    DataColumn(label: Text('Paid')),
                    DataColumn(label: Text('Bonus')),
                  ],
                  rows: widget.receipt.items.map(_returnRow).toList(),
                ),
              ),
              const SizedBox(height: 12),
              const Text(
                'Posting this Purchase Return will remove stock and reduce the supplier payable. It cannot be edited afterward.',
              ),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(
                    _error!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: _saving ? null : () => Navigator.pop(context),
        child: const Text('Close'),
      ),
      FilledButton(
        key: const Key('post_purchase_return'),
        onPressed: _saving ? null : _post,
        child: const Text('Post Return'),
      ),
    ],
  );

  DataRow _returnRow(ReturnablePurchaseItem item) => DataRow(
    cells: [
      DataCell(Text('${item.productName}\n${item.sku}')),
      DataCell(Text(item.batchNumber)),
      DataCell(
        Text(
          '${_date(item.expiryDate)}${item.isBatchExpired ? ' expired' : ''}${item.isBatchDisposed ? ' disposed' : ''}',
        ),
      ),
      DataCell(Text('${item.paidQuantityRemaining}')),
      DataCell(Text('${item.bonusQuantityRemaining}')),
      DataCell(Text('${item.currentBatchAvailable}')),
      DataCell(
        SizedBox(
          width: 90,
          child: TextFormField(
            key: Key('return_paid_${item.id}'),
            controller: _paid[item.id],
            decoration: const InputDecoration(isDense: true),
            keyboardType: TextInputType.number,
            validator: (value) => _returnQuantityError(item, value, true),
          ),
        ),
      ),
      DataCell(
        SizedBox(
          width: 90,
          child: TextFormField(
            key: Key('return_bonus_${item.id}'),
            controller: _bonus[item.id],
            decoration: const InputDecoration(isDense: true),
            keyboardType: TextInputType.number,
            validator: (value) => _returnQuantityError(item, value, false),
          ),
        ),
      ),
    ],
  );

  String? _returnQuantityError(
    ReturnablePurchaseItem item,
    String? value,
    bool paid,
  ) {
    final parsed = int.tryParse(value ?? '');
    if (parsed == null || parsed < 0) return '0+';
    final limit = paid
        ? item.paidQuantityRemaining
        : item.bonusQuantityRemaining;
    if (parsed > limit) return 'Too high';
    final sibling =
        int.tryParse(
          paid ? _bonus[item.id]?.text ?? '' : _paid[item.id]?.text ?? '',
        ) ??
        0;
    if (parsed + sibling > item.maxPhysicalReturnQuantity) return 'Stock';
    return null;
  }

  Future<void> _post() async {
    if (!_form.currentState!.validate()) return;
    final items = widget.receipt.items
        .map((item) {
          final paid = int.tryParse(_paid[item.id]?.text ?? '') ?? 0;
          final bonus = int.tryParse(_bonus[item.id]?.text ?? '') ?? 0;
          return {
            'originalGoodsReceiptItemId': item.id,
            'paidReturnQuantity': paid,
            'bonusReturnQuantity': bonus,
          };
        })
        .where(
          (item) =>
              (item['paidReturnQuantity'] as int) +
                  (item['bonusReturnQuantity'] as int) >
              0,
        )
        .toList();
    if (items.isEmpty) {
      setState(() => _error = 'Return at least one paid or bonus unit.');
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      final result = await widget.authState
          .postPurchaseReturn(widget.receipt.id, {
            'reason': _reasonIndex(_reason),
            'notes': _notes.text.trim().isEmpty ? null : _notes.text.trim(),
            'items': items,
          });
      if (mounted) Navigator.pop(context, result);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  static int _reasonIndex(String reason) => switch (reason) {
    'Damaged' => 1,
    'Expired' => 2,
    'SupplierRecall' => 3,
    'WrongItem' => 4,
    'ExcessSupply' => 5,
    _ => 6,
  };
}

class _PurchaseOrderDialog extends StatefulWidget {
  const _PurchaseOrderDialog({required this.authState});
  final AuthState authState;
  @override
  State<_PurchaseOrderDialog> createState() => _PurchaseOrderDialogState();
}

class _PurchaseOrderDialogState extends State<_PurchaseOrderDialog> {
  final _form = GlobalKey<FormState>();
  final _quantity = TextEditingController();
  final _price = TextEditingController(text: '0');
  InventoryOptions? _options;
  String? _branchId, _supplierId, _productId, _error;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _loadOptions();
  }

  Future<void> _loadOptions() async {
    final options = await widget.authState.inventoryOptions();
    if (mounted) {
      setState(() {
        _options = options;
        _branchId = options.branches.firstOrNull?.id;
        _supplierId = options.suppliers.firstOrNull?.id;
        _productId = options.products.firstOrNull?.id;
      });
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('New Purchase Order'),
    content: SizedBox(
      width: 560,
      child: _options == null
          ? const Center(child: CircularProgressIndicator())
          : Form(
              key: _form,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  _lookup(
                    'Branch',
                    _branchId,
                    _options!.branches,
                    (v) => _branchId = v,
                  ),
                  _lookup(
                    'Supplier',
                    _supplierId,
                    _options!.suppliers,
                    (v) => _supplierId = v,
                  ),
                  _lookup(
                    'Product',
                    _productId,
                    _options!.products,
                    (v) => _productId = v,
                  ),
                  TextFormField(
                    key: const Key('po_quantity'),
                    controller: _quantity,
                    decoration: const InputDecoration(labelText: 'Ordered Qty'),
                    keyboardType: TextInputType.number,
                    validator: _positiveInt,
                  ),
                  TextFormField(
                    controller: _price,
                    decoration: const InputDecoration(
                      labelText: 'Expected Price',
                    ),
                    keyboardType: TextInputType.number,
                  ),
                  if (_error != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Text(
                        _error!,
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.error,
                        ),
                      ),
                    ),
                ],
              ),
            ),
    ),
    actions: [
      TextButton(
        onPressed: _saving ? null : () => Navigator.pop(context, false),
        child: const Text('Close'),
      ),
      FilledButton(
        key: const Key('save_purchase_order'),
        onPressed: _saving ? null : _save,
        child: const Text('Save'),
      ),
    ],
  );

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    setState(() => _saving = true);
    try {
      await widget.authState.createPurchaseOrder({
        'branchId': _branchId,
        'supplierId': _supplierId,
        'orderDate': _iso(DateTime.now()),
        'items': [
          {
            'productId': _productId,
            'orderedQuantity': int.parse(_quantity.text),
            'expectedPurchasePrice': double.tryParse(_price.text) ?? 0,
          },
        ],
      });
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }
}

class _ReceiptDialog extends StatefulWidget {
  const _ReceiptDialog({required this.authState, this.order});
  final AuthState authState;
  final PurchaseOrderListItem? order;
  @override
  State<_ReceiptDialog> createState() => _ReceiptDialogState();
}

class _ReceiptDialogState extends State<_ReceiptDialog> {
  final _form = GlobalKey<FormState>();
  final _invoice = TextEditingController();
  final _batch = TextEditingController();
  final _paid = TextEditingController();
  final _bonus = TextEditingController(text: '0');
  final _purchasePrice = TextEditingController();
  final _retailPrice = TextEditingController();
  final _discount = TextEditingController(text: '0');
  final _tax = TextEditingController(text: '0');
  InventoryOptions? _options;
  List<GodownLookup> _godowns = [];
  String? _branchId, _supplierId, _productId, _orderItemId, _godownId, _error;
  bool _saving = false;

  double get _paidQty => double.tryParse(_paid.text) ?? 0;
  double get _price => double.tryParse(_purchasePrice.text) ?? 0;
  double get _discountPct => double.tryParse(_discount.text) ?? 0;
  double get _taxPct => double.tryParse(_tax.text) ?? 0;
  double get _net {
    final gross = _paidQty * _price;
    final discounted = gross - (gross * _discountPct / 100);
    return discounted + (discounted * _taxPct / 100);
  }

  @override
  void initState() {
    super.initState();
    _loadOptions();
    for (final c in [_paid, _purchasePrice, _discount, _tax]) {
      c.addListener(() => setState(() {}));
    }
  }

  Future<void> _loadOptions() async {
    final options = await widget.authState.inventoryOptions();
    if (mounted) {
      setState(() {
        _options = options;
        _branchId = widget.order?.branchId ?? options.branches.firstOrNull?.id;
        _supplierId = widget.order?.supplierId ?? options.suppliers.firstOrNull?.id;
        _productId = options.products.firstOrNull?.id;
      });
    }
    await _reloadGodowns();
  }

  Future<void> _reloadGodowns() async {
    if (_branchId == null) return;
    final godowns = await widget.authState.myGodowns(branchId: _branchId);
    if (!mounted) return;
    setState(() {
      _godowns = godowns;
      _godownId = widget.order?.godownId ?? (godowns.isEmpty
          ? null
          : godowns
                .firstWhere((g) => g.isDefault, orElse: () => godowns.first)
                .id);
    });
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(widget.order == null ? 'Direct Purchase' : 'Receive Goods'),
    content: SizedBox(
      width: 680,
      child: _options == null
          ? const Center(child: CircularProgressIndicator())
          : Form(
              key: _form,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    if (widget.order != null)
                      Align(
                        alignment: Alignment.centerLeft,
                        child: Text(
                          '${widget.order!.orderNumber}: ${widget.order!.receivedQuantity}/${widget.order!.orderedQuantity} received',
                        ),
                      ),
                    _lookup('Branch', _branchId, _options!.branches, (v) {
                      setState(() => _branchId = v);
                      _reloadGodowns();
                    }),
                    if (_godowns.length > 1)
                      _lookup(
                        'Godown',
                        _godownId,
                        _godowns,
                        (v) => setState(() => _godownId = v),
                      )
                    else if (_godowns.isEmpty)
                      const Padding(
                        padding: EdgeInsets.symmetric(vertical: 8),
                        child: Align(
                          alignment: Alignment.centerLeft,
                          child: Text(
                            'No godown is configured for this branch yet. Ask an administrator to set one up before receiving stock.',
                            style: TextStyle(color: Colors.orange),
                          ),
                        ),
                      ),
                    _lookup(
                      'Supplier',
                      _supplierId,
                      _options!.suppliers,
                      (v) => _supplierId = v,
                    ),
                    TextFormField(
                      controller: _invoice,
                      decoration: const InputDecoration(
                        labelText: 'Supplier Invoice #',
                      ),
                    ),
                    _lookup(
                      'Product',
                      _productId,
                      _options!.products,
                      (v) => _productId = v,
                    ),
                    TextFormField(
                      key: const Key('receipt_batch'),
                      controller: _batch,
                      decoration: const InputDecoration(
                        labelText: 'Batch Number',
                      ),
                      validator: _required,
                    ),
                    Row(
                      children: [
                        Expanded(
                          child: TextFormField(
                            key: const Key('receipt_paid'),
                            controller: _paid,
                            decoration: const InputDecoration(
                              labelText: 'Paid Qty',
                            ),
                            validator: _positiveInt,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: TextFormField(
                            key: const Key('receipt_bonus'),
                            controller: _bonus,
                            decoration: const InputDecoration(
                              labelText: 'Bonus Qty',
                            ),
                            validator: _nonNegativeInt,
                          ),
                        ),
                      ],
                    ),
                    Row(
                      children: [
                        Expanded(
                          child: TextFormField(
                            controller: _purchasePrice,
                            decoration: const InputDecoration(
                              labelText: 'Purchase Price',
                            ),
                            validator: _nonNegativeMoney,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: TextFormField(
                            controller: _retailPrice,
                            decoration: const InputDecoration(
                              labelText: 'Retail Price',
                            ),
                            validator: _nonNegativeMoney,
                          ),
                        ),
                      ],
                    ),
                    Row(
                      children: [
                        Expanded(
                          child: TextFormField(
                            controller: _discount,
                            decoration: const InputDecoration(
                              labelText: 'Discount %',
                            ),
                            validator: _percent,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: TextFormField(
                            controller: _tax,
                            decoration: const InputDecoration(
                              labelText: 'Tax %',
                            ),
                            validator: _percent,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 10),
                    Text(
                      'Inventory quantity = paid + bonus. Net payable = ${_money(_net)}',
                    ),
                    const Text(
                      'Posting updates inventory and supplier payable and cannot be edited afterward.',
                    ),
                    if (_error != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 8),
                        child: Text(
                          _error!,
                          style: TextStyle(
                            color: Theme.of(context).colorScheme.error,
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ),
    ),
    actions: [
      TextButton(
        onPressed: _saving ? null : () => Navigator.pop(context, false),
        child: const Text('Close'),
      ),
      FilledButton(
        key: const Key('post_purchase'),
        onPressed: _saving ? null : _save,
        child: const Text('Post'),
      ),
    ],
  );

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    setState(() => _saving = true);
    final body = {
      'branchId': _branchId,
      'godownId': _godownId,
      'supplierId': _supplierId,
      'purchaseOrderId': widget.order?.id,
      'supplierInvoiceNumber': _invoice.text.trim().isEmpty
          ? null
          : _invoice.text.trim(),
      'receiptDate': _iso(DateTime.now()),
      'items': [
        {
          'productId': _productId,
          'purchaseOrderItemId': _orderItemId,
          'batchNumber': _batch.text.trim(),
          'expiryDate': _iso(DateTime.now().add(const Duration(days: 365))),
          'purchasedQuantity': int.parse(_paid.text),
          'bonusQuantity': int.tryParse(_bonus.text) ?? 0,
          'purchasePrice': double.tryParse(_purchasePrice.text) ?? 0,
          'retailPrice': double.tryParse(_retailPrice.text) ?? 0,
          'discountPercent': double.tryParse(_discount.text) ?? 0,
          'taxPercent': double.tryParse(_tax.text) ?? 0,
        },
      ],
    };
      try {
        final receipt = widget.order == null ? await widget.authState.postDirectPurchase(body) : await widget.authState.postGoodsReceipt(body);
        if (mounted && widget.authState.can('pricing.suggest') && (widget.authState.can('sales.cost_view') || widget.authState.can('reports.profitability'))) {
          final navigatorContext = Navigator.of(context).context;
          ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: const Text('Purchase posted. Cost-based price suggestions are available for manager review.'), action: SnackBarAction(label: 'Review prices', onPressed: () => showDialog<void>(context: navigatorContext, builder: (_) => Phase6SuggestionsDialog(authState: widget.authState, expiry: false, goodsReceiptId: receipt.id)))));
        }
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }
}

Widget _lookup(
  String label,
  String? value,
  List<dynamic> items,
  ValueChanged<String?> onChanged,
) => DropdownButtonFormField<String>(
  initialValue: value,
  decoration: InputDecoration(labelText: label),
  items: items
      .map(
        (x) => DropdownMenuItem<String>(
          value: x.id as String,
          child: Text(x.name as String),
        ),
      )
      .toList(),
  onChanged: onChanged,
  validator: (v) => v == null ? 'Required' : null,
);

String? _required(String? value) =>
    value?.trim().isEmpty == false ? null : 'Required';
String? _positiveInt(String? value) =>
    (int.tryParse(value ?? '') ?? 0) > 0 ? null : 'Enter a positive number';
String? _nonNegativeInt(String? value) =>
    (int.tryParse(value ?? '') ?? -1) >= 0 ? null : 'Enter zero or more';
String? _nonNegativeMoney(String? value) =>
    (double.tryParse(value ?? '') ?? -1) >= 0 ? null : 'Enter zero or more';
String? _percent(String? value) {
  final parsed = double.tryParse(value ?? '');
  return parsed != null && parsed >= 0 && parsed <= 100
      ? null
      : 'Enter 0 to 100';
}

String _iso(DateTime value) => value.toIso8601String().substring(0, 10);
String _date(DateTime value) => _iso(value);
String _money(double value) => 'PKR ${value.toStringAsFixed(2)}';
String _returnStateLabel(String value) => switch (value) {
  'PartiallyReturned' => 'Partial',
  'FullyReturned' => 'Full',
  _ => 'None',
};
