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
  late final TabController _tabs = TabController(length: 5, vsync: this);
  final _search = TextEditingController();
  PagedInventory? _inventory;
  PagedBatches? _batches;
  List<ExpiryItem> _expiry = [];
  PagedMovements? _movements;
  InventoryOptions? _options;
  PagedStockCountSessions? _stockCountSessions;
  List<GodownLookup> _godowns = [];
  String? _godownFilter;
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
      final canViewStockCount = can('inventory.stock_count.view');
      final godowns = await widget.authState.myGodowns();
      final results = await Future.wait([
        widget.authState.listInventory(search: _search.text),
        widget.authState.listBatches(
          search: _search.text,
          godownId: _godownFilter,
        ),
        widget.authState.listExpiry(days: 30, godownId: _godownFilter),
        widget.authState.listMovements(
          search: _search.text,
          godownId: _godownFilter,
        ),
        widget.authState.inventoryOptions(productSearch: _search.text),
        canViewStockCount
            ? widget.authState.listStockCountSessions()
            : Future.value(
                const PagedStockCountSessions(items: [], totalCount: 0),
              ),
      ]);
      if (!mounted) return;
      setState(() {
        _godowns = godowns;
        _inventory = results[0] as PagedInventory;
        _batches = results[1] as PagedBatches;
        _expiry = results[2] as List<ExpiryItem>;
        _movements = results[3] as PagedMovements;
        _options = results[4] as InventoryOptions;
        _stockCountSessions = results[5] as PagedStockCountSessions;
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
              if (_godowns.length > 1)
                SizedBox(
                  width: 200,
                  child: DropdownButtonFormField<String?>(
                    key: const Key('inventory_godown_filter'),
                    initialValue: _godownFilter,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Godown'),
                    items: [
                      const DropdownMenuItem<String?>(
                        value: null,
                        child: Text('All permitted godowns'),
                      ),
                      ..._godowns.map(
                        (g) => DropdownMenuItem<String?>(
                          value: g.id,
                          child: Text(g.name, overflow: TextOverflow.ellipsis),
                        ),
                      ),
                    ],
                    onChanged: (v) {
                      setState(() => _godownFilter = v);
                      _load();
                    },
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
              if (can('inventory.stock_count'))
                FilledButton.icon(
                  key: const Key('new_stock_taking'),
                  onPressed: () => _showNewStockCountSession(),
                  icon: const Icon(Icons.checklist_outlined),
                  label: const Text('New Stock Taking'),
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
            Tab(text: 'Stock Taking'),
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
        _StockCountSessionTable(
          items: _stockCountSessions?.items ?? const [],
          onOpen: _openStockCountSession,
        ),
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
    final options = _options;
    if (options == null) return;
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => _AdjustmentDialog(
        options: options,
        batches: _batches?.items ?? const [],
        authState: widget.authState,
      ),
    );
    if (ok == true) await _load();
  }

  Future<void> _showStockCount() async {
    final options = _options;
    if (options == null) return;
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => _StockCountDialog(
        options: options,
        batches: _batches?.items ?? const [],
        authState: widget.authState,
      ),
    );
    if (ok == true) await _load();
  }

  Future<void> _showNewStockCountSession() async {
    final options = _options;
    if (options == null) return;
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) =>
          _NewStockCountDialog(options: options, authState: widget.authState),
    );
    if (ok == true) await _load();
  }

  Future<void> _openStockCountSession(String id) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) =>
          _StockCountSessionDialog(sessionId: id, authState: widget.authState),
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
        DataColumn(label: Text('Godown')),
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
                DataCell(Text(x.godownName ?? '-')),
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
        DataColumn(label: Text('Godown')),
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
                DataCell(Text(x.godownName ?? '-')),
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
        DataColumn(label: Text('Godown')),
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
                DataCell(Text(x.godownName ?? '-')),
                DataCell(Text(x.movementType)),
                DataCell(Text('${x.quantity}')),
              ],
            ),
          )
          .toList(),
    ),
  );
}

class _StockCountSessionTable extends StatelessWidget {
  const _StockCountSessionTable({required this.items, required this.onOpen});
  final List<StockCountSessionSummary> items;
  final void Function(String id) onOpen;
  @override
  Widget build(BuildContext context) => _TableShell(
    empty: 'No stock count sessions found',
    isEmpty: items.isEmpty,
    child: DataTable(
      columns: const [
        DataColumn(label: Text('Count #')),
        DataColumn(label: Text('Branch')),
        DataColumn(label: Text('Godown')),
        DataColumn(label: Text('Date')),
        DataColumn(label: Text('Scope')),
        DataColumn(label: Text('Status')),
        DataColumn(label: Text('Counted')),
        DataColumn(label: Text('Variance')),
        DataColumn(label: Text('')),
      ],
      rows: items
          .map(
            (x) => DataRow(
              cells: [
                DataCell(Text(x.countNumber)),
                DataCell(Text(x.branchName)),
                DataCell(Text(x.godownName ?? 'Whole branch')),
                DataCell(Text(_date(x.countDate))),
                DataCell(Text(_scopeLabel(x.scope))),
                DataCell(_StatusChip(label: x.status)),
                DataCell(Text('${x.countedItems}/${x.totalItems}')),
                DataCell(Text('${x.varianceItems}')),
                DataCell(
                  TextButton(
                    key: Key('open_stock_count_${x.id}'),
                    onPressed: () => onOpen(x.id),
                    child: const Text('Open'),
                  ),
                ),
              ],
            ),
          )
          .toList(),
    ),
  );
}

class _NewStockCountDialog extends StatefulWidget {
  const _NewStockCountDialog({required this.options, required this.authState});
  final InventoryOptions options;
  final AuthState authState;
  @override
  State<_NewStockCountDialog> createState() => _NewStockCountDialogState();
}

class _NewStockCountDialogState extends State<_NewStockCountDialog> {
  final _form = GlobalKey<FormState>();
  final _notes = TextEditingController();
  String? _branchId;
  String _scope = 'Full';
  String? _categoryId;
  String? _godownId;
  List<GodownLookup> _godowns = [];
  String? _error;

  Future<void> _reloadGodowns(String? branchId) async {
    if (branchId == null) {
      setState(() {
        _godowns = [];
        _godownId = null;
      });
      return;
    }
    final godowns = await widget.authState.myGodowns(branchId: branchId);
    if (mounted) {
      setState(() {
        _godowns = godowns;
        _godownId = null;
      });
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('New Stock Taking'),
    content: SizedBox(
      width: 460,
      child: Form(
        key: _form,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              DropdownButtonFormField<String>(
                initialValue: _branchId,
                key: const Key('stock_count_branch'),
                decoration: const InputDecoration(labelText: 'Branch'),
                items: widget.options.branches
                    .map(
                      (x) => DropdownMenuItem(value: x.id, child: Text(x.name)),
                    )
                    .toList(),
                onChanged: (v) {
                  setState(() => _branchId = v);
                  _reloadGodowns(v);
                },
                validator: (v) => v == null ? 'Branch is required' : null,
              ),
              if (_godowns.isNotEmpty)
                DropdownButtonFormField<String?>(
                  initialValue: _godownId,
                  key: const Key('stock_count_godown'),
                  isExpanded: true,
                  decoration: const InputDecoration(
                    labelText: 'Godown (leave blank for the whole branch)',
                  ),
                  items: [
                    const DropdownMenuItem<String?>(
                      value: null,
                      child: Text(
                        'Whole branch (all godowns)',
                        overflow: TextOverflow.ellipsis,
                        maxLines: 1,
                      ),
                    ),
                    ..._godowns.map(
                      (g) => DropdownMenuItem<String?>(
                        value: g.id,
                        child: Text(
                          g.name,
                          overflow: TextOverflow.ellipsis,
                          maxLines: 1,
                        ),
                      ),
                    ),
                  ],
                  onChanged: (v) => setState(() => _godownId = v),
                ),
              DropdownButtonFormField<String>(
                initialValue: _scope,
                key: const Key('stock_count_scope'),
                decoration: const InputDecoration(labelText: 'Scope'),
                items: const [
                  DropdownMenuItem(value: 'Full', child: Text('Full Inventory')),
                  DropdownMenuItem(value: 'Category', child: Text('By Category')),
                ],
                onChanged: (v) => setState(() => _scope = v ?? 'Full'),
              ),
              if (_scope == 'Category')
                DropdownButtonFormField<String>(
                  initialValue: _categoryId,
                  key: const Key('stock_count_category'),
                  decoration: const InputDecoration(labelText: 'Category'),
                  items: widget.options.categories
                      .map(
                        (x) =>
                            DropdownMenuItem(value: x.id, child: Text(x.name)),
                      )
                      .toList(),
                  onChanged: (v) => setState(() => _categoryId = v),
                  validator: (v) =>
                      _scope == 'Category' && v == null
                      ? 'Category is required'
                      : null,
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
        key: const Key('save_stock_count_session'),
        onPressed: () async {
          if (!_form.currentState!.validate()) return;
          try {
            await widget.authState.createStockCountSession({
              'branchId': _branchId,
              'countDate': _date(DateTime.now()),
              'scope': _scope,
              'categoryId': _scope == 'Category' ? _categoryId : null,
              'notes': _notes.text.trim().isEmpty ? null : _notes.text.trim(),
              'godownId': _godownId,
            });
            if (context.mounted) Navigator.pop(context, true);
          } on ApiException catch (e) {
            setState(() => _error = e.message);
          }
        },
        child: const Text('Create'),
      ),
    ],
  );
}

class _StockCountSessionDialog extends StatefulWidget {
  const _StockCountSessionDialog({
    required this.sessionId,
    required this.authState,
  });
  final String sessionId;
  final AuthState authState;
  @override
  State<_StockCountSessionDialog> createState() =>
      _StockCountSessionDialogState();
}

class _StockCountSessionDialogState extends State<_StockCountSessionDialog> {
  StockCountSession? _session;
  final Map<String, TextEditingController> _controllers = {};
  bool _loading = true;
  bool _changed = false;
  String? _error;

  bool can(String p) => widget.authState.can(p);

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    for (final controller in _controllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final session = await widget.authState.getStockCountSession(
        widget.sessionId,
      );
      for (final item in session.items) {
        _controllers.putIfAbsent(
          item.id,
          () => TextEditingController(
            text: item.countedQuantity?.toString() ?? '',
          ),
        );
      }
      if (!mounted) return;
      setState(() => _session = session);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _start() async {
    try {
      await widget.authState.startStockCountSession(widget.sessionId);
      _changed = true;
      await _load();
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    }
  }

  Future<void> _saveCounts() async {
    final session = _session;
    if (session == null) return;
    final entries = <Map<String, dynamic>>[];
    for (final item in session.items) {
      final text = _controllers[item.id]?.text.trim() ?? '';
      if (text.isEmpty) continue;
      final qty = int.tryParse(text);
      if (qty == null || qty < 0) {
        setState(
          () => _error =
              'Enter a valid non-negative counted quantity for every line you fill in.',
        );
        return;
      }
      entries.add({'stockCountItemId': item.id, 'countedQuantity': qty});
    }
    if (entries.isEmpty) {
      setState(() => _error = 'Enter at least one counted quantity.');
      return;
    }
    try {
      await widget.authState.submitStockCountEntries(
        widget.sessionId,
        entries,
      );
      _changed = true;
      await _load();
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    }
  }

  Future<void> _finalize() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Finalize Stock Count'),
        content: const Text(
          'This posts stock adjustment movements for every counted line with a '
          'variance and cannot be undone. Continue?',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            key: const Key('confirm_finalize_stock_count'),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Finalize'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    try {
      await widget.authState.finalizeStockCountSession(widget.sessionId);
      _changed = true;
      await _load();
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    }
  }

  Future<void> _cancelSession() async {
    final reasonController = TextEditingController();
    final reason = await showDialog<String>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Cancel Stock Count'),
        content: TextField(
          key: const Key('cancel_stock_count_reason'),
          controller: reasonController,
          decoration: const InputDecoration(labelText: 'Cancellation reason'),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Back'),
          ),
          FilledButton(
            key: const Key('confirm_cancel_stock_count'),
            onPressed: () =>
                Navigator.pop(context, reasonController.text.trim()),
            child: const Text('Cancel Count'),
          ),
        ],
      ),
    );
    if (reason == null || reason.isEmpty) return;
    try {
      await widget.authState.cancelStockCountSession(widget.sessionId, reason);
      _changed = true;
      await _load();
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final session = _session;
    return AlertDialog(
      title: Text(
        session == null ? 'Stock Count' : 'Stock Count ${session.countNumber}',
      ),
      content: SizedBox(
        width: 720,
        height: 480,
        child: _loading
            ? const Center(child: CircularProgressIndicator())
            : session == null
            ? Center(child: Text(_error ?? 'Session not found'))
            : _content(session),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context, _changed),
          child: const Text('Close'),
        ),
        if (session?.status == 'Draft' && can('inventory.stock_count'))
          FilledButton(
            key: const Key('start_stock_count'),
            onPressed: _start,
            child: const Text('Start Counting'),
          ),
        if (session?.status == 'InProgress' && can('inventory.stock_count'))
          OutlinedButton(
            key: const Key('save_stock_count_entries'),
            onPressed: _saveCounts,
            child: const Text('Save Counts'),
          ),
        if (session?.status == 'InProgress' &&
            can('inventory.stock_count.finalize'))
          FilledButton(
            key: const Key('finalize_stock_count'),
            onPressed: _finalize,
            child: const Text('Finalize'),
          ),
        if ((session?.status == 'Draft' || session?.status == 'InProgress') &&
            can('inventory.stock_count'))
          TextButton(
            key: const Key('cancel_stock_count'),
            onPressed: _cancelSession,
            child: const Text('Cancel Count'),
          ),
      ],
    );
  }

  Widget _content(StockCountSession session) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    mainAxisSize: MainAxisSize.min,
    children: [
      Wrap(
        spacing: 16,
        runSpacing: 8,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          _StatusChip(label: session.status),
          Text('Branch: ${session.branchName}'),
          Text('Godown: ${session.godownName ?? 'Whole branch'}'),
          Text(
            'Scope: ${_scopeLabel(session.scope)}'
            '${session.categoryName != null ? ' (${session.categoryName})' : ''}',
          ),
          Text(
            'Items: ${session.countedItems}/${session.totalItems} counted, '
            '${session.varianceItems} with variance',
          ),
        ],
      ),
      if (_error != null)
        Padding(
          padding: const EdgeInsets.only(top: 8),
          child: Text(
            _error!,
            style: TextStyle(color: Theme.of(context).colorScheme.error),
          ),
        ),
      const SizedBox(height: 12),
      Expanded(
        child: SingleChildScrollView(
          child: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: DataTable(
              columns: const [
                DataColumn(label: Text('Product')),
                DataColumn(label: Text('Batch')),
                DataColumn(label: Text('Expiry')),
                DataColumn(label: Text('System Qty')),
                DataColumn(label: Text('Counted Qty')),
                DataColumn(label: Text('Variance')),
              ],
              rows: session.items.map((item) {
                final editable = session.status == 'InProgress';
                return DataRow(
                  cells: [
                    DataCell(Text(item.productName)),
                    DataCell(Text(item.batchNumber)),
                    DataCell(Text(_date(item.expiryDate))),
                    DataCell(Text('${item.systemQuantity}')),
                    DataCell(
                      editable
                          ? SizedBox(
                              width: 90,
                              child: TextField(
                                key: Key('count_entry_${item.id}'),
                                controller: _controllers[item.id],
                                keyboardType: TextInputType.number,
                                decoration: const InputDecoration(
                                  isDense: true,
                                ),
                              ),
                            )
                          : Text(item.countedQuantity?.toString() ?? '-'),
                    ),
                    DataCell(Text(item.variance == null ? '-' : '${item.variance}')),
                  ],
                );
              }).toList(),
            ),
          ),
        ),
      ),
    ],
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

const Map<String, String> _adjustmentReasonLabels = {
  'PhysicalCountCorrection': 'Physical Count Correction',
  'Damaged': 'Damaged',
  'Expired': 'Expired',
  'Broken': 'Broken',
  'Leakage': 'Leakage',
  'TheftOrLoss': 'Theft / Loss',
  'Missing': 'Missing',
  'DataEntryCorrection': 'Data Correction',
  'Other': 'Other',
};

BatchItem? _batchById(List<BatchItem> batches, String? id) {
  if (id == null) return null;
  for (final b in batches) {
    if (b.batchId == id) return b;
  }
  return null;
}

class _BatchPickerFields extends StatefulWidget {
  const _BatchPickerFields({
    required this.options,
    required this.batches,
    required this.onBatchChanged,
  });
  final InventoryOptions options;
  final List<BatchItem> batches;
  final void Function(BatchItem? batch) onBatchChanged;
  @override
  State<_BatchPickerFields> createState() => _BatchPickerFieldsState();
}

class _BatchPickerFieldsState extends State<_BatchPickerFields> {
  String? _branchId, _productId, _godownId, _batchId;

  List<BatchItem> get _branchAndProductFiltered => widget.batches
      .where(
        (b) =>
            (_branchId == null || b.branchId == _branchId) &&
            (_productId == null || b.productId == _productId),
      )
      .toList();

  List<BatchItem> get _filtered => _branchAndProductFiltered
      .where((b) => _godownId == null || b.godownId == _godownId)
      .toList();

  List<MapEntry<String, String>> get _availableGodowns {
    final byId = <String, String>{};
    for (final b in _branchAndProductFiltered) {
      if (b.godownId != null) byId[b.godownId!] = b.godownName ?? b.godownId!;
    }
    return byId.entries.toList();
  }

  @override
  Widget build(BuildContext context) => Column(
    mainAxisSize: MainAxisSize.min,
    children: [
      DropdownButtonFormField<String>(
        initialValue: _branchId,
        key: const Key('picker_branch'),
        decoration: const InputDecoration(labelText: 'Branch'),
        items: widget.options.branches
            .map((x) => DropdownMenuItem(value: x.id, child: Text(x.name)))
            .toList(),
        onChanged: (v) => setState(() {
          _branchId = v;
          _godownId = null;
          _batchId = null;
          widget.onBatchChanged(null);
        }),
        validator: (v) => v == null ? 'Branch is required' : null,
      ),
      DropdownButtonFormField<String>(
        initialValue: _productId,
        key: const Key('picker_product'),
        decoration: const InputDecoration(labelText: 'Product'),
        items: widget.options.products
            .map(
              (x) => DropdownMenuItem(
                value: x.id,
                child: Text('${x.name} (${x.sku})'),
              ),
            )
            .toList(),
        onChanged: (v) => setState(() {
          _productId = v;
          _godownId = null;
          _batchId = null;
          widget.onBatchChanged(null);
        }),
        validator: (v) => v == null ? 'Product is required' : null,
      ),
      if (_availableGodowns.isNotEmpty)
        DropdownButtonFormField<String?>(
          initialValue: _godownId,
          key: const Key('picker_godown'),
          decoration: const InputDecoration(labelText: 'Godown (optional filter)'),
          items: [
            const DropdownMenuItem<String?>(
              value: null,
              child: Text('Any godown'),
            ),
            ..._availableGodowns.map(
              (g) => DropdownMenuItem<String?>(
                value: g.key,
                child: Text(g.value),
              ),
            ),
          ],
          onChanged: (v) => setState(() {
            _godownId = v;
            _batchId = null;
            widget.onBatchChanged(null);
          }),
        ),
      DropdownButtonFormField<String>(
        initialValue: _batchId,
        key: const Key('picker_batch'),
        isExpanded: true,
        decoration: const InputDecoration(labelText: 'Batch'),
        items: _filtered
            .map(
              (x) => DropdownMenuItem(
                value: x.batchId,
                child: Text(
                  '${x.batchNumber} (qty ${x.quantityAvailable}) — ${x.godownName ?? 'No godown'}',
                  overflow: TextOverflow.ellipsis,
                  maxLines: 1,
                ),
              ),
            )
            .toList(),
        onChanged: (v) {
          setState(() => _batchId = v);
          widget.onBatchChanged(_batchById(_filtered, v));
        },
        validator: (v) => v == null ? 'Batch is required' : null,
      ),
    ],
  );
}

class _AdjustmentDialog extends StatefulWidget {
  const _AdjustmentDialog({
    required this.options,
    required this.batches,
    required this.authState,
  });
  final InventoryOptions options;
  final List<BatchItem> batches;
  final AuthState authState;
  @override
  State<_AdjustmentDialog> createState() => _AdjustmentDialogState();
}

class _AdjustmentDialogState extends State<_AdjustmentDialog> {
  final _form = GlobalKey<FormState>();
  final _qty = TextEditingController();
  final _notes = TextEditingController();
  BatchItem? _batch;
  bool _increase = true;
  String _reason = 'PhysicalCountCorrection';
  String? _error;

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Adjust Stock'),
    content: SizedBox(
      width: 480,
      child: Form(
        key: _form,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              _BatchPickerFields(
                options: widget.options,
                batches: widget.batches,
                onBatchChanged: (b) => setState(() => _batch = b),
              ),
              if (_batch != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Align(
                    alignment: Alignment.centerLeft,
                    child: Text('Current quantity: ${_batch!.quantityAvailable}'),
                  ),
                ),
              Row(
                children: [
                  Expanded(
                    child: RadioListTile<bool>(
                      key: const Key('adjust_direction_increase'),
                      contentPadding: EdgeInsets.zero,
                      title: const Text('Increase'),
                      value: true,
                      groupValue: _increase,
                      onChanged: (v) => setState(() => _increase = v ?? true),
                    ),
                  ),
                  Expanded(
                    child: RadioListTile<bool>(
                      key: const Key('adjust_direction_decrease'),
                      contentPadding: EdgeInsets.zero,
                      title: const Text('Decrease'),
                      value: false,
                      groupValue: _increase,
                      onChanged: (v) => setState(() => _increase = v ?? false),
                    ),
                  ),
                ],
              ),
              TextFormField(
                key: const Key('adjust_quantity'),
                controller: _qty,
                decoration: const InputDecoration(labelText: 'Quantity'),
                keyboardType: TextInputType.number,
                validator: _positiveInt,
              ),
              DropdownButtonFormField<String>(
                initialValue: _reason,
                key: const Key('adjust_reason'),
                decoration: const InputDecoration(labelText: 'Reason'),
                items: _adjustmentReasonLabels.entries
                    .map(
                      (e) =>
                          DropdownMenuItem(value: e.key, child: Text(e.value)),
                    )
                    .toList(),
                onChanged: (v) => setState(() => _reason = v ?? 'Other'),
              ),
              TextFormField(
                key: const Key('adjust_notes'),
                controller: _notes,
                decoration: const InputDecoration(labelText: 'Notes'),
                validator: (v) =>
                    _reason == 'Other' && (v == null || v.trim().isEmpty)
                    ? 'Notes are required when reason is Other'
                    : null,
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
        key: const Key('save_adjustment'),
        onPressed: () async {
          if (!_form.currentState!.validate()) return;
          final batch = _batch;
          if (batch == null) {
            setState(() => _error = 'Select a batch to adjust.');
            return;
          }
          try {
            await widget.authState.adjustStock({
              'branchId': batch.branchId,
              'productId': batch.productId,
              'productBatchId': batch.batchId,
              'quantity': int.parse(_qty.text),
              'reason': _reason,
              'notes': _notes.text.trim().isEmpty ? null : _notes.text.trim(),
            }, increase: _increase);
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

class _StockCountDialog extends StatefulWidget {
  const _StockCountDialog({
    required this.options,
    required this.batches,
    required this.authState,
  });
  final InventoryOptions options;
  final List<BatchItem> batches;
  final AuthState authState;
  @override
  State<_StockCountDialog> createState() => _StockCountDialogState();
}

class _StockCountDialogState extends State<_StockCountDialog> {
  final _form = GlobalKey<FormState>();
  final _qty = TextEditingController();
  final _notes = TextEditingController();
  BatchItem? _batch;
  String? _error;

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Stock Count'),
    content: SizedBox(
      width: 480,
      child: Form(
        key: _form,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              _BatchPickerFields(
                options: widget.options,
                batches: widget.batches,
                onBatchChanged: (b) => setState(() => _batch = b),
              ),
              if (_batch != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Align(
                    alignment: Alignment.centerLeft,
                    child: Text('System quantity: ${_batch!.quantityAvailable}'),
                  ),
                ),
              TextFormField(
                key: const Key('save_stock_count_quantity'),
                controller: _qty,
                decoration: const InputDecoration(
                  labelText: 'Physical Quantity',
                ),
                keyboardType: TextInputType.number,
                validator: _positiveInt,
              ),
              TextFormField(
                key: const Key('save_stock_count_notes'),
                controller: _notes,
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
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context, false),
        child: const Text('Cancel'),
      ),
      FilledButton(
        key: const Key('save_stock_count'),
        onPressed: () async {
          if (!_form.currentState!.validate()) return;
          final batch = _batch;
          if (batch == null) {
            setState(() => _error = 'Select a batch to count.');
            return;
          }
          try {
            await widget.authState.reconcileStockCount({
              'branchId': batch.branchId,
              'productId': batch.productId,
              'productBatchId': batch.batchId,
              'physicalQuantity': int.parse(_qty.text),
              'reason': 'PhysicalCountCorrection',
              'notes': _notes.text.trim(),
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
String _scopeLabel(String value) => switch (value) {
  'Full' => 'Full Inventory',
  'Category' => 'Category',
  'SelectedProducts' => 'Selected Products',
  'SelectedBatches' => 'Selected Batches',
  _ => value,
};
