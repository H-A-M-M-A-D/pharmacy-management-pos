import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

class InventoryScreen extends StatefulWidget {
  const InventoryScreen({required this.authState, super.key});
  final AuthState authState;

  @override
  State<InventoryScreen> createState() => _InventoryScreenState();
}

class _InventoryScreenState extends State<InventoryScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs = TabController(length: 4, vsync: this);
  final _search = TextEditingController();
  PagedInventory? _inventory;
  PagedBatches? _batches;
  List<ExpiryItem> _expiry = [];
  PagedMovements? _movements;
  InventoryOptions? _options;
  bool _loading = true;
  String? _error;

  bool can(String p) => widget.authState.can(p);

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _tabs.dispose();
    _search.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        widget.authState.listInventory(search: _search.text),
        widget.authState.listBatches(search: _search.text),
        widget.authState.listExpiry(days: 30),
        widget.authState.listMovements(search: _search.text),
        widget.authState.inventoryOptions(productSearch: _search.text),
      ]);
      if (!mounted) return;
      setState(() {
        _inventory = results[0] as PagedInventory;
        _batches = results[1] as PagedBatches;
        _expiry = results[2] as List<ExpiryItem>;
        _movements = results[3] as PagedMovements;
        _options = results[4] as InventoryOptions;
      });
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
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
          child: Wrap(
            spacing: 12,
            runSpacing: 12,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              SizedBox(
                width: 280,
                child: Text(
                  'Inventory',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              SizedBox(
                width: 320,
                child: TextField(
                  controller: _search,
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search),
                    labelText: 'Search stock, batch, SKU',
                  ),
                  onSubmitted: (_) => _load(),
                ),
              ),
              IconButton.filledTonal(
                key: const Key('refresh_inventory'),
                tooltip: 'Refresh',
                onPressed: _load,
                icon: const Icon(Icons.refresh),
              ),
              if (can('inventory.opening_stock'))
                FilledButton.icon(
                  key: const Key('opening_stock'),
                  onPressed: () => _showOpeningStock(),
                  icon: const Icon(Icons.add_box_outlined),
                  label: const Text('Opening Stock'),
                ),
              if (can('inventory.adjust'))
                OutlinedButton.icon(
                  key: const Key('adjust_stock'),
                  onPressed: () => _showAdjustment(),
                  icon: const Icon(Icons.tune),
                  label: const Text('Adjust'),
                ),
              if (can('inventory.stock_count'))
                OutlinedButton.icon(
                  key: const Key('stock_count'),
                  onPressed: () => _showStockCount(),
                  icon: const Icon(Icons.fact_check_outlined),
                  label: const Text('Stock Count'),
                ),
            ],
          ),
        ),
        TabBar(
          controller: _tabs,
          tabs: const [
            Tab(text: 'Stock'),
            Tab(text: 'Batches'),
            Tab(text: 'Expiry'),
            Tab(text: 'Movements'),
          ],
        ),
        Expanded(child: _body()),
      ],
    ),
  );

  Widget _body() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(_error!),
            const SizedBox(height: 8),
            OutlinedButton(onPressed: _load, child: const Text('Retry')),
          ],
        ),
      );
    }
    return TabBarView(
      controller: _tabs,
      children: [
        _InventoryTable(items: _inventory?.items ?? const []),
        _BatchTable(
          items: _batches?.items ?? const [],
          canAdjust: can('inventory.adjust'),
        ),
        _ExpiryTable(
          items: _expiry,
          canDispose: can('inventory.expiry_manage'),
        ),
        _MovementTable(items: _movements?.items ?? const []),
      ],
    );
  }

  Future<void> _showOpeningStock() async {
    final options = _options;
    if (options == null) return;
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) =>
          _OpeningStockDialog(options: options, authState: widget.authState),
    );
    if (ok == true) await _load();
  }

  Future<void> _showAdjustment() async {
    final batch = _batches?.items.isNotEmpty == true
        ? _batches!.items.first
        : null;
    if (batch == null) return;
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) =>
          _AdjustmentDialog(batch: batch, authState: widget.authState),
    );
    if (ok == true) await _load();
  }

  Future<void> _showStockCount() async {
    final batch = _batches?.items.isNotEmpty == true
        ? _batches!.items.first
        : null;
    if (batch == null) return;
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) =>
          _StockCountDialog(batch: batch, authState: widget.authState),
    );
    if (ok == true) await _load();
  }
}

class _InventoryTable extends StatelessWidget {
  const _InventoryTable({required this.items});
  final List<InventoryItem> items;
  @override
  Widget build(BuildContext context) => _TableShell(
    empty: 'No inventory found',
    isEmpty: items.isEmpty,
    child: DataTable(
      columns: const [
        DataColumn(label: Text('Product')),
        DataColumn(label: Text('SKU')),
        DataColumn(label: Text('Quantity')),
        DataColumn(label: Text('Reorder')),
        DataColumn(label: Text('Nearest Expiry')),
        DataColumn(label: Text('Batches')),
        DataColumn(label: Text('Stock Value')),
        DataColumn(label: Text('Status')),
      ],
      rows: items
          .map(
            (x) => DataRow(
              cells: [
                DataCell(Text(x.productName)),
                DataCell(Text(x.sku)),
                DataCell(Text('${x.quantityInStock}')),
                DataCell(Text('${x.reorderLevel}')),
                DataCell(Text(_date(x.nearestExpiryDate))),
                DataCell(Text('${x.activeBatchCount}')),
                DataCell(Text(_money(x.estimatedStockValue))),
                DataCell(_StatusChip(label: _status(x.stockStatus))),
              ],
            ),
          )
          .toList(),
    ),
  );
}

class _BatchTable extends StatelessWidget {
  const _BatchTable({required this.items, required this.canAdjust});
  final List<BatchItem> items;
  final bool canAdjust;
  @override
  Widget build(BuildContext context) => _TableShell(
    empty: 'No batches found',
    isEmpty: items.isEmpty,
    child: DataTable(
      columns: const [
        DataColumn(label: Text('Product')),
        DataColumn(label: Text('Batch')),
        DataColumn(label: Text('Branch')),
        DataColumn(label: Text('Expiry')),
        DataColumn(label: Text('Qty')),
        DataColumn(label: Text('Purchase')),
        DataColumn(label: Text('Retail')),
        DataColumn(label: Text('Value')),
        DataColumn(label: Text('Status')),
      ],
      rows: items
          .map(
            (x) => DataRow(
              cells: [
                DataCell(Text(x.productName)),
                DataCell(Text(x.batchNumber)),
                DataCell(Text(x.branchName)),
                DataCell(Text(_date(x.expiryDate))),
                DataCell(Text('${x.quantityAvailable}')),
                DataCell(Text(_money(x.purchasePrice))),
                DataCell(Text(_money(x.retailPrice))),
                DataCell(Text(_money(x.estimatedStockValue))),
                DataCell(_StatusChip(label: x.state)),
              ],
            ),
          )
          .toList(),
    ),
  );
}

class _ExpiryTable extends StatelessWidget {
  const _ExpiryTable({required this.items, required this.canDispose});
  final List<ExpiryItem> items;
  final bool canDispose;
  @override
  Widget build(BuildContext context) => _TableShell(
    empty: 'No expiring stock found',
    isEmpty: items.isEmpty,
    child: DataTable(
      columns: const [
        DataColumn(label: Text('Product')),
        DataColumn(label: Text('Batch')),
        DataColumn(label: Text('Expiry Date')),
        DataColumn(label: Text('Days')),
        DataColumn(label: Text('Quantity')),
        DataColumn(label: Text('Value')),
      ],
      rows: items
          .map(
            (x) => DataRow(
              cells: [
                DataCell(Text(x.productName)),
                DataCell(Text(x.batchNumber)),
                DataCell(Text(_date(x.expiryDate))),
                DataCell(Text('${x.daysRemaining}')),
                DataCell(Text('${x.quantityAvailable}')),
                DataCell(Text(_money(x.estimatedStockValue))),
              ],
            ),
          )
          .toList(),
    ),
  );
}

class _MovementTable extends StatelessWidget {
  const _MovementTable({required this.items});
  final List<StockMovementItem> items;
  @override
  Widget build(BuildContext context) => _TableShell(
    empty: 'No stock movements found',
    isEmpty: items.isEmpty,
    child: DataTable(
      columns: const [
        DataColumn(label: Text('Date/Time')),
        DataColumn(label: Text('Product')),
        DataColumn(label: Text('Batch')),
        DataColumn(label: Text('Branch')),
        DataColumn(label: Text('Type')),
        DataColumn(label: Text('Quantity')),
      ],
      rows: items
          .map(
            (x) => DataRow(
              cells: [
                DataCell(Text('${x.createdAt.toLocal()}'.split('.').first)),
                DataCell(Text(x.productName)),
                DataCell(Text(x.batchNumber)),
                DataCell(Text(x.branchName)),
                DataCell(Text(x.movementType)),
                DataCell(Text('${x.quantity}')),
              ],
            ),
          )
          .toList(),
    ),
  );
}

class _TableShell extends StatelessWidget {
  const _TableShell({
    required this.child,
    required this.empty,
    required this.isEmpty,
  });
  final Widget child;
  final String empty;
  final bool isEmpty;
  @override
  Widget build(BuildContext context) => isEmpty
      ? Center(child: Text(empty))
      : SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: child,
          ),
        );
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.label});
  final String label;
  @override
  Widget build(BuildContext context) =>
      Chip(label: Text(label), visualDensity: VisualDensity.compact);
}

class _OpeningStockDialog extends StatefulWidget {
  const _OpeningStockDialog({required this.options, required this.authState});
  final InventoryOptions options;
  final AuthState authState;
  @override
  State<_OpeningStockDialog> createState() => _OpeningStockDialogState();
}

class _OpeningStockDialogState extends State<_OpeningStockDialog> {
  final _form = GlobalKey<FormState>();
  final _batch = TextEditingController();
  final _qty = TextEditingController();
  final _purchase = TextEditingController();
  final _retail = TextEditingController();
  final _notes = TextEditingController();
  String? _branchId, _productId;
  DateTime? _expiry;
  String? _error;
  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Opening Stock'),
    content: SizedBox(
      width: 520,
      child: Form(
        key: _form,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text(
                'Opening Stock is intended for initial/existing inventory entry, not normal purchasing.',
              ),
              DropdownButtonFormField<String>(
                initialValue: _branchId,
                key: const Key('opening_branch'),
                decoration: const InputDecoration(labelText: 'Branch'),
                items: widget.options.branches
                    .map(
                      (x) => DropdownMenuItem(value: x.id, child: Text(x.name)),
                    )
                    .toList(),
                onChanged: (v) => setState(() => _branchId = v),
                validator: (v) => v == null ? 'Branch is required' : null,
              ),
              DropdownButtonFormField<String>(
                initialValue: _productId,
                key: const Key('opening_product'),
                decoration: const InputDecoration(labelText: 'Product'),
                items: widget.options.products
                    .map(
                      (x) => DropdownMenuItem(
                        value: x.id,
                        child: Text('${x.name} (${x.sku})'),
                      ),
                    )
                    .toList(),
                onChanged: (v) => setState(() => _productId = v),
                validator: (v) => v == null ? 'Product is required' : null,
              ),
              TextFormField(
                key: const Key('opening_batch'),
                controller: _batch,
                decoration: const InputDecoration(labelText: 'Batch Number'),
                validator: _required,
              ),
              TextFormField(
                key: const Key('opening_quantity'),
                controller: _qty,
                decoration: const InputDecoration(labelText: 'Quantity'),
                keyboardType: TextInputType.number,
                validator: _positiveInt,
              ),
              TextFormField(
                controller: _purchase,
                decoration: const InputDecoration(labelText: 'Purchase Price'),
                keyboardType: TextInputType.number,
                validator: _nonNegative,
              ),
              TextFormField(
                controller: _retail,
                decoration: const InputDecoration(labelText: 'Retail Price'),
                keyboardType: TextInputType.number,
                validator: _nonNegative,
              ),
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(_expiry == null ? 'Expiry Date' : _date(_expiry)),
                trailing: const Icon(Icons.calendar_month),
                onTap: () async {
                  final picked = await showDatePicker(
                    context: context,
                    firstDate: DateTime.now(),
                    lastDate: DateTime(2100),
                    initialDate: DateTime.now().add(const Duration(days: 30)),
                  );
                  if (picked != null) setState(() => _expiry = picked);
                },
              ),
              TextFormField(
                controller: _notes,
                decoration: const InputDecoration(labelText: 'Notes'),
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
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context, false),
        child: const Text('Cancel'),
      ),
      FilledButton(
        key: const Key('save_opening_stock'),
        onPressed: () async {
          if (!_form.currentState!.validate() || _expiry == null) return;
          try {
            await widget.authState.addOpeningStock({
              'branchId': _branchId,
              'productId': _productId,
              'batchNumber': _batch.text.trim(),
              'expiryDate': _date(_expiry),
              'quantity': int.parse(_qty.text),
              'purchasePrice': double.parse(_purchase.text),
              'retailPrice': double.parse(_retail.text),
              'notes': _notes.text.trim().isEmpty ? null : _notes.text.trim(),
            });
            if (context.mounted) Navigator.pop(context, true);
          } on ApiException catch (e) {
            setState(() => _error = e.message);
          }
        },
        child: const Text('Save'),
      ),
    ],
  );
}

class _AdjustmentDialog extends StatelessWidget {
  const _AdjustmentDialog({required this.batch, required this.authState});
  final BatchItem batch;
  final AuthState authState;
  @override
  Widget build(BuildContext context) => _SimpleQuantityDialog(
    keyName: 'save_adjustment',
    title: 'Adjust Stock',
    current: batch.quantityAvailable,
    onSave: (qty, note) => authState.adjustStock({
      'branchId': batch.branchId,
      'productId': batch.productId,
      'productBatchId': batch.batchId,
      'quantity': qty,
      'reason': 'DataEntryCorrection',
      'notes': note,
    }, increase: true),
  );
}

class _StockCountDialog extends StatelessWidget {
  const _StockCountDialog({required this.batch, required this.authState});
  final BatchItem batch;
  final AuthState authState;
  @override
  Widget build(BuildContext context) => _SimpleQuantityDialog(
    keyName: 'save_stock_count',
    title: 'Stock Count',
    current: batch.quantityAvailable,
    quantityLabel: 'Physical Quantity',
    onSave: (qty, note) => authState.reconcileStockCount({
      'branchId': batch.branchId,
      'productId': batch.productId,
      'productBatchId': batch.batchId,
      'physicalQuantity': qty,
      'reason': 'PhysicalCountCorrection',
      'notes': note,
    }),
  );
}

class _SimpleQuantityDialog extends StatefulWidget {
  const _SimpleQuantityDialog({
    required this.keyName,
    required this.title,
    required this.current,
    required this.onSave,
    this.quantityLabel = 'Quantity',
  });
  final String keyName, title, quantityLabel;
  final int current;
  final Future<void> Function(int quantity, String note) onSave;
  @override
  State<_SimpleQuantityDialog> createState() => _SimpleQuantityDialogState();
}

class _SimpleQuantityDialogState extends State<_SimpleQuantityDialog> {
  final _form = GlobalKey<FormState>();
  final _qty = TextEditingController();
  final _note = TextEditingController();
  String? _error;
  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(widget.title),
    content: Form(
      key: _form,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text('Current quantity: ${widget.current}'),
          TextFormField(
            key: Key('${widget.keyName}_quantity'),
            controller: _qty,
            decoration: InputDecoration(labelText: widget.quantityLabel),
            keyboardType: TextInputType.number,
            validator: _positiveInt,
          ),
          TextFormField(
            controller: _note,
            decoration: const InputDecoration(labelText: 'Reason / note'),
            validator: _required,
          ),
          if (_error != null)
            Text(
              _error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
        ],
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context, false),
        child: const Text('Cancel'),
      ),
      FilledButton(
        key: Key(widget.keyName),
        onPressed: () async {
          if (!_form.currentState!.validate()) return;
          try {
            await widget.onSave(int.parse(_qty.text), _note.text);
            if (context.mounted) Navigator.pop(context, true);
          } on ApiException catch (e) {
            setState(() => _error = e.message);
          }
        },
        child: const Text('Save'),
      ),
    ],
  );
}

String? _required(String? value) =>
    value == null || value.trim().isEmpty ? 'Required' : null;
String? _positiveInt(String? value) {
  final parsed = int.tryParse(value ?? '');
  return parsed == null || parsed <= 0 ? 'Enter a positive quantity' : null;
}

String? _nonNegative(String? value) {
  final parsed = double.tryParse(value ?? '');
  return parsed == null || parsed < 0 ? 'Enter a non-negative amount' : null;
}

String _money(double value) => 'PKR ${value.toStringAsFixed(2)}';
String _date(DateTime? value) => value == null
    ? '-'
    : '${value.year.toString().padLeft(4, '0')}-${value.month.toString().padLeft(2, '0')}-${value.day.toString().padLeft(2, '0')}';
String _status(String value) => switch (value) {
  'LowStock' => 'Low Stock',
  'OutOfStock' => 'Out of Stock',
  _ => value.isEmpty ? 'Healthy' : value,
};
