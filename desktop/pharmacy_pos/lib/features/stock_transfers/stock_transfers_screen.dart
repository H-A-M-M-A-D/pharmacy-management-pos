import 'package:flutter/material.dart';
import '../../ui/app_theme.dart';
import '../../ui/app_widgets.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

class StockTransfersScreen extends StatefulWidget {
  const StockTransfersScreen({required this.authState, super.key});
  final AuthState authState;
  @override
  State<StockTransfersScreen> createState() => _StockTransfersScreenState();
}

class _StockTransfersScreenState extends State<StockTransfersScreen> {
  final _search = TextEditingController();
  PagedStockTransfers? _transfers;
  InventoryOptions? _options;
  String? _statusFilter;
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
      final options = await widget.authState.inventoryOptions();
      final query = <String, String>{'page': '1', 'pageSize': '100'};
      if (_statusFilter != null) query['status'] = _statusFilter!;
      if (_search.text.trim().isNotEmpty) {
        query['transferNumber'] = _search.text.trim();
      }
      final raw =
          await widget.authState.stockTransfers('', query: query)
              as Map<String, dynamic>;
      if (mounted) {
        setState(() {
          _options = options;
          _transfers = PagedStockTransfers.fromJson(raw);
        });
      }
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
        AppPageHeader(title: 'Stock Transfers'),
        Padding(padding: const EdgeInsets.fromLTRB(20, 0, 20, 12), child: AppFilterBar(children: [
              SizedBox(
                width: 200,
                child: DropdownButtonFormField<String?>(
                  initialValue: _statusFilter,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Status'),
                  items: [
                    const DropdownMenuItem<String?>(
                      value: null,
                      child: Text('All statuses'),
                    ),
                    ...const [
                      'Draft',
                      'Requested',
                      'Approved',
                      'Dispatched',
                      'PartiallyReceived',
                      'Received',
                      'Cancelled',
                    ].map(
                      (s) => DropdownMenuItem<String?>(
                        value: s,
                        child: Text(s),
                      ),
                    ),
                  ],
                  onChanged: (v) {
                    setState(() => _statusFilter = v);
                    _load();
                  },
                ),
              ),
              SizedBox(
                width: 220,
                child: TextField(
                  controller: _search,
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search),
                    labelText: 'Transfer #',
                  ),
                  onSubmitted: (_) => _load(),
                ),
              ),
              IconButton.filledTonal(
                onPressed: _load,
                tooltip: 'Refresh',
                icon: const Icon(Icons.refresh),
              ),
              if (can('stock_transfers.create'))
                FilledButton.icon(
                  key: const Key('new_stock_transfer'),
                  onPressed: _options == null ? null : _openCreate,
                  icon: const Icon(Icons.compare_arrows),
                  label: const Text('New Transfer'),
                ),
            ])),
        Expanded(child: _body()),
      ],
    ),
  );

  Widget _body() {
    if (_loading) return const AppLoadingState();
    if (_error != null) return AppErrorState(_error!, onRetry: _load);
    final items = _transfers?.items ?? const <StockTransferListItem>[];
    if (items.isEmpty) return AppEmptyState(title: 'No transfers found');
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: AppDataTable(
          columns: const [
            DataColumn(label: Text('Transfer #')),
            DataColumn(label: Text('Date')),
            DataColumn(label: Text('From')),
            DataColumn(label: Text('To')),
            DataColumn(label: Text('Status')),
            DataColumn(label: Text('Requested'), numeric: true),
            DataColumn(label: Text('Dispatched'), numeric: true),
            DataColumn(label: Text('Received'), numeric: true),
            DataColumn(label: Text('In Transit'), numeric: true),
            DataColumn(label: Text('Requested By'), numeric: true),
          ],
          rows: items
              .map(
                (t) => DataRow(
                  key: ValueKey('transfer_row_${t.id}'),
                  onSelectChanged: (_) => _openDetail(t),
                  cells: [
                    DataCell(
                      Text(
                        t.transferNumber,
                        key: ValueKey('transfer_row_${t.id}'),
                      ),
                    ),
                    DataCell(Text(_shortDate(t.transferDate))),
                    DataCell(Text('${t.sourceBranchName} / ${t.sourceGodownName}')),
                    DataCell(
                      Text(
                        '${t.destinationBranchName} / ${t.destinationGodownName}',
                      ),
                    ),
                    DataCell(_StatusChip(t.status)),
                    DataCell(Text('${t.quantityRequested}')),
                    DataCell(Text('${t.quantityDispatched}')),
                    DataCell(Text('${t.quantityReceived}')),
                    DataCell(Text('${t.quantityInTransit}')),
                    DataCell(Text(t.requestedBy ?? '-')),
                  ],
                ),
              )
              .toList(),
        ),
      ),
    );
  }

  Future<void> _openCreate() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) =>
          _CreateTransferDialog(authState: widget.authState, options: _options!),
    );
    if (ok == true) await _load();
  }

  Future<void> _openDetail(StockTransferListItem item) async {
    final changed = await showDialog<bool>(
      context: context,
      builder: (_) => _TransferDetailDialog(
        authState: widget.authState,
        transferId: item.id,
        options: _options!,
      ),
    );
    if (changed == true) await _load();
  }
}

String _shortDate(DateTime d) =>
    '${d.year}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

class _StatusChip extends StatelessWidget {
  const _StatusChip(this.status);
  final String status;
  @override
  Widget build(BuildContext context) =>
      AppStatusChip(status);
}

class _CreateTransferDialog extends StatefulWidget {
  const _CreateTransferDialog({
    required this.authState,
    required this.options,
    this.existingTransfer,
  });
  final AuthState authState;
  final InventoryOptions options;
  final StockTransferDetails? existingTransfer;
  bool get isEditing => existingTransfer != null;
  @override
  State<_CreateTransferDialog> createState() => _CreateTransferDialogState();
}

class _DraftLine {
  _DraftLine({required this.batch, required this.quantity});
  final TransferableBatch batch;
  int quantity;
}

class _CreateTransferDialogState extends State<_CreateTransferDialog> {
  final _notes = TextEditingController();
  final _batchSearch = TextEditingController();
  String? _sourceBranchId, _sourceGodownId, _destBranchId, _destGodownId;
  List<GodownLookup> _sourceGodowns = const [], _destGodowns = const [];
  List<TransferableBatch> _searchResults = const [];
  final List<_DraftLine> _lines = [];
  late DateTime _transferDate;
  String? _error;
  bool _saving = false;
  bool _searching = false;

  @override
  void initState() {
    super.initState();
    final existing = widget.existingTransfer;
    _transferDate = existing?.transferDate ?? DateTime.now();
    if (existing != null) {
      _sourceBranchId = existing.sourceBranchId;
      _destBranchId = existing.destinationBranchId;
      _notes.text = existing.notes ?? '';
      _lines.addAll(
        existing.items.map(
          (i) => _DraftLine(
            batch: TransferableBatch(
              productBatchId: i.sourceProductBatchId,
              productId: i.productId,
              productName: i.productName,
              sku: i.sku,
              batchNumber: i.batchNumber,
              expiryDate: i.expiryDate,
              quantityAvailable: i.quantityRequested,
              purchasePrice: i.unitCostSnapshot,
              retailPrice: i.unitCostSnapshot,
            ),
            quantity: i.quantityRequested,
          ),
        ),
      );
    } else {
      _sourceBranchId = widget.options.branches.firstOrNull?.id;
      _destBranchId = widget.options.branches.firstOrNull?.id;
    }
    _reloadGodowns(
      source: true,
      preselect: existing?.sourceGodownId,
      clearLines: existing == null,
    );
    _reloadGodowns(source: false, preselect: existing?.destinationGodownId);
  }

  @override
  void dispose() {
    _notes.dispose();
    _batchSearch.dispose();
    super.dispose();
  }

  Future<void> _reloadGodowns({
    required bool source,
    String? preselect,
    bool clearLines = true,
  }) async {
    final branchId = source ? _sourceBranchId : _destBranchId;
    if (branchId == null) return;
    final godowns = await widget.authState.myGodowns(branchId: branchId);
    if (!mounted) return;
    setState(() {
      if (source) {
        _sourceGodowns = godowns;
        _sourceGodownId = preselect ??
            (godowns.isEmpty
                ? null
                : godowns.firstWhere((g) => g.isDefault, orElse: () => godowns.first).id);
        if (clearLines) {
          _lines.clear();
          _searchResults = const [];
        }
      } else {
        _destGodowns = godowns;
        _destGodownId = preselect ??
            (godowns.isEmpty
                ? null
                : godowns.firstWhere((g) => g.isDefault, orElse: () => godowns.first).id);
      }
    });
  }

  Future<void> _searchBatches() async {
    if (_sourceBranchId == null || _sourceGodownId == null) return;
    setState(() => _searching = true);
    try {
      final query = <String, String>{
        'branchId': _sourceBranchId!,
        'godownId': _sourceGodownId!,
      };
      if (_batchSearch.text.trim().isNotEmpty) {
        query['search'] = _batchSearch.text.trim();
      }
      final raw =
          await widget.authState.stockTransfers(
                'transferable-batches',
                query: query,
              )
              as List<dynamic>;
      if (mounted) {
        setState(
          () => _searchResults = raw
              .map((x) => TransferableBatch.fromJson(x as Map<String, dynamic>))
              .toList(),
        );
      }
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _searching = false);
    }
  }

  Future<void> _addLine(TransferableBatch batch) async {
    final qty = await _promptQuantity(
      context,
      title: 'Quantity to request',
      max: batch.quantityAvailable,
    );
    if (qty == null || qty <= 0) return;
    setState(() {
      final existing = _lines.indexWhere(
        (l) => l.batch.productBatchId == batch.productBatchId,
      );
      if (existing >= 0) {
        _lines[existing].quantity = qty;
      } else {
        _lines.add(_DraftLine(batch: batch, quantity: qty));
      }
    });
  }

  bool get _sameGodown =>
      _sourceGodownId != null && _sourceGodownId == _destGodownId;

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(widget.isEditing ? 'Edit Stock Transfer' : 'New Stock Transfer'),
    content: SizedBox(
      width: 720,
      child: SingleChildScrollView(
        child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
            const AppWorkflowStrip(steps: ['Source godown', 'Stock & quantities', 'Destination', 'Review']),
          Row(
            children: [
              Expanded(
                child: DropdownButtonFormField<String>(
                  key: const Key('transfer_source_branch'),
                  initialValue: _sourceBranchId,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'From Branch'),
                  items: widget.options.branches
                      .map((b) => DropdownMenuItem(value: b.id, child: Text(b.name)))
                      .toList(),
                  onChanged: (v) {
                    setState(() => _sourceBranchId = v);
                    _reloadGodowns(source: true);
                  },
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: DropdownButtonFormField<String>(
                  key: const Key('transfer_source_godown'),
                  initialValue: _sourceGodownId,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'From Godown'),
                  items: _sourceGodowns
                      .map((g) => DropdownMenuItem(value: g.id, child: Text(g.name)))
                      .toList(),
                  onChanged: (v) {
                    setState(() {
                      _sourceGodownId = v;
                      _lines.clear();
                      _searchResults = const [];
                    });
                  },
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: DropdownButtonFormField<String>(
                  key: const Key('transfer_dest_branch'),
                  initialValue: _destBranchId,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'To Branch'),
                  items: widget.options.branches
                      .map((b) => DropdownMenuItem(value: b.id, child: Text(b.name)))
                      .toList(),
                  onChanged: (v) {
                    setState(() => _destBranchId = v);
                    _reloadGodowns(source: false);
                  },
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: DropdownButtonFormField<String>(
                  key: const Key('transfer_dest_godown'),
                  initialValue: _destGodownId,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'To Godown'),
                  items: _destGodowns
                      .map((g) => DropdownMenuItem(value: g.id, child: Text(g.name)))
                      .toList(),
                  onChanged: (v) => setState(() => _destGodownId = v),
                ),
              ),
            ],
          ),
          if (_sameGodown)
            const Padding(
              padding: EdgeInsets.only(top: 6),
              child: Text(
                'Source and destination godown must be different.',
                style: TextStyle(color: AppColors.warning),
              ),
            ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: TextField(
                  key: const Key('transfer_batch_search'),
                  controller: _batchSearch,
                  decoration: const InputDecoration(
                    labelText: 'Search product/batch at source godown',
                    prefixIcon: Icon(Icons.search),
                  ),
                  onSubmitted: (_) => _searchBatches(),
                ),
              ),
              IconButton(
                key: const Key('search_transferable_batches'),
                onPressed: _searching ? null : _searchBatches,
                icon: const Icon(Icons.search),
              ),
            ],
          ),
          if (_searchResults.isNotEmpty)
            SizedBox(
              height: 120,
              child: ListView(
                children: _searchResults
                    .map(
                      (b) => ListTile(
                        dense: true,
                        title: Text('${b.productName} (${b.batchNumber})'),
                        subtitle: Text(
                          'Available: ${b.quantityAvailable} · Expiry: ${_shortDate(b.expiryDate)}',
                        ),
                        trailing: IconButton(
                          icon: const Icon(Icons.add_circle_outline),
                          onPressed: () => _addLine(b),
                        ),
                      ),
                    )
                    .toList(),
              ),
            ),
          const Divider(),
          SizedBox(
            height: 160,
            child: _lines.isEmpty
                ? AppEmptyState(title: 'No items added yet')
                : ListView(
                    children: _lines
                        .map(
                          (l) => ListTile(
                            dense: true,
                            title: Text(
                              '${l.batch.productName} (${l.batch.batchNumber})',
                            ),
                            subtitle: Text('Qty: ${l.quantity}'),
                            trailing: IconButton(
                              icon: const Icon(Icons.remove_circle_outline),
                              onPressed: () => setState(() => _lines.remove(l)),
                            ),
                          ),
                        )
                        .toList(),
                  ),
          ),
          TextField(
            key: const Key('transfer_notes'),
            controller: _notes,
            decoration: const InputDecoration(labelText: 'Notes'),
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
        ],
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: _saving ? null : () => Navigator.pop(context, false),
        child: const Text('Cancel'),
      ),
      if (!widget.isEditing)
        OutlinedButton(
          key: const Key('save_draft_transfer'),
          onPressed: _saving ? null : () => _save(request: false),
          child: const Text('Save Draft'),
        ),
      if (!widget.isEditing)
        FilledButton(
          key: const Key('save_request_transfer'),
          onPressed: _saving ? null : () => _save(request: true),
          child: const Text('Save & Request'),
        ),
      if (widget.isEditing)
        FilledButton(
          key: const Key('save_transfer_edit'),
          onPressed: _saving ? null : () => _save(request: false),
          child: const Text('Save Changes'),
        ),
    ],
  );

  Future<void> _save({required bool request}) async {
    if (_sourceBranchId == null ||
        _sourceGodownId == null ||
        _destBranchId == null ||
        _destGodownId == null) {
      setState(() => _error = 'Source and destination godowns are required.');
      return;
    }
    if (_sameGodown) {
      setState(() => _error = 'Source and destination godown must be different.');
      return;
    }
    if (_lines.isEmpty) {
      setState(() => _error = 'Add at least one item.');
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      final body = {
        'sourceBranchId': _sourceBranchId,
        'sourceGodownId': _sourceGodownId,
        'destinationBranchId': _destBranchId,
        'destinationGodownId': _destGodownId,
        'transferDate': _transferDate.toIso8601String().substring(0, 10),
        'notes': _notes.text.trim().isEmpty ? null : _notes.text.trim(),
        'items': _lines
            .map(
              (l) => {
                'productId': l.batch.productId,
                'productBatchId': l.batch.productBatchId,
                'quantityRequested': l.quantity,
              },
            )
            .toList(),
      };
      if (widget.isEditing) {
        await widget.authState.stockTransfers(
          widget.existingTransfer!.id,
          method: 'PUT',
          body: body,
        );
      } else {
        final raw =
            await widget.authState.stockTransfers('', method: 'POST', body: body)
                as Map<String, dynamic>;
        if (request) {
          final id = raw['id'] as String;
          await widget.authState.stockTransfers('$id/request', method: 'POST');
        }
      }
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }
}

class _TransferDetailDialog extends StatefulWidget {
  const _TransferDetailDialog({
    required this.authState,
    required this.transferId,
    required this.options,
  });
  final AuthState authState;
  final String transferId;
  final InventoryOptions options;
  @override
  State<_TransferDetailDialog> createState() => _TransferDetailDialogState();
}

class _TransferDetailDialogState extends State<_TransferDetailDialog> {
  StockTransferDetails? _transfer;
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
      final raw =
          await widget.authState.stockTransfers(widget.transferId)
              as Map<String, dynamic>;
      if (mounted) setState(() => _transfer = StockTransferDetails.fromJson(raw));
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = _transfer;
    return AlertDialog(
      title: Text(t == null ? 'Transfer' : t.transferNumber),
      content: SizedBox(
        width: 760,
        child: t == null
            ? (_error != null ? Text(_error!) : const AppLoadingState())
            : SingleChildScrollView(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _StatusChip(t.status),
                    const SizedBox(height: 8),
                    Text(
                      '${t.sourceBranchName} / ${t.sourceGodownName}  →  ${t.destinationBranchName} / ${t.destinationGodownName}',
                    ),
                    Text('Transfer date: ${_shortDate(t.transferDate)}'),
                    if (t.notes != null && t.notes!.isNotEmpty) Text('Notes: ${t.notes}'),
                    const SizedBox(height: 12),
                    Text('Timeline', style: Theme.of(context).textTheme.titleSmall),
                    _timelineRow('Created', t.createdBy, t.createdAt),
                    if (t.requestedAtUtc != null)
                      _timelineRow('Requested', t.requestedBy, t.requestedAtUtc),
                    if (t.approvedAtUtc != null)
                      _timelineRow('Approved', t.approvedBy, t.approvedAtUtc),
                    if (t.dispatchedAtUtc != null)
                      _timelineRow('Dispatched', t.dispatchedBy, t.dispatchedAtUtc),
                    if (t.receivedAtUtc != null)
                      _timelineRow('Received', t.receivedBy, t.receivedAtUtc),
                    if (t.cancelledAtUtc != null)
                      _timelineRow(
                        'Cancelled${t.cancellationReason != null ? ' (${t.cancellationReason})' : ''}',
                        t.cancelledBy,
                        t.cancelledAtUtc,
                      ),
                    const SizedBox(height: 12),
                    Text('Items', style: Theme.of(context).textTheme.titleSmall),
                    SingleChildScrollView(
                      scrollDirection: Axis.horizontal,
                      child: AppDataTable(
                        columns: const [
                          DataColumn(label: Text('Product')),
                          DataColumn(label: Text('Batch')),
                          DataColumn(label: Text('Req')),
                          DataColumn(label: Text('App')),
                          DataColumn(label: Text('Disp')),
                          DataColumn(label: Text('Recv')),
                          DataColumn(label: Text('In Transit'), numeric: true),
                          DataColumn(label: Text('Notes')),
                        ],
                        rows: t.items
                            .map(
                              (i) => DataRow(
                                cells: [
                                  DataCell(Text(i.productName)),
                                  DataCell(Text(i.batchNumber)),
                                  DataCell(Text('${i.quantityRequested}')),
                                  DataCell(Text('${i.quantityApproved}')),
                                  DataCell(Text('${i.quantityDispatched}')),
                                  DataCell(Text('${i.quantityReceived}')),
                                  DataCell(Text('${i.quantityInTransit}')),
                                  DataCell(
                                    ConstrainedBox(
                                      constraints: const BoxConstraints(maxWidth: 200),
                                      child: Text(
                                        i.notes ?? '-',
                                        overflow: TextOverflow.ellipsis,
                                        maxLines: 2,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            )
                            .toList(),
                      ),
                    ),
                    if (_error != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 8),
                        child: Text(
                          _error!,
                          style: TextStyle(color: Theme.of(context).colorScheme.error),
                        ),
                      ),
                  ],
                ),
              ),
      ),
      actions: [
        ..._actionButtons(t),
        TextButton(
          onPressed: () => Navigator.pop(context, _changed),
          child: const Text('Close'),
        ),
      ],
    );
  }

  Widget _timelineRow(String label, String? by, DateTime? at) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 2),
    child: Text('$label${by != null ? ' by $by' : ''}${at != null ? ' — ${_shortDate(at)}' : ''}'),
  );

  List<Widget> _actionButtons(StockTransferDetails? t) {
    if (t == null || _busy) return const [];
    final buttons = <Widget>[];
    if (t.status == 'Draft' && can('stock_transfers.create')) {
      buttons.add(
        OutlinedButton(
          key: const Key('transfer_action_edit'),
          onPressed: () => _openEdit(t),
          child: const Text('Edit'),
        ),
      );
    }
    if (t.status == 'Draft' && can('stock_transfers.request')) {
      buttons.add(
        FilledButton(
          key: const Key('transfer_action_request'),
          onPressed: () => _run(() => widget.authState.stockTransfers('${t.id}/request', method: 'POST')),
          child: const Text('Request'),
        ),
      );
    }
    if (t.status == 'Requested' && can('stock_transfers.approve')) {
      buttons.add(
        FilledButton(
          key: const Key('transfer_action_approve'),
          onPressed: () => _openApprove(t),
          child: const Text('Approve'),
        ),
      );
    }
    if (t.status == 'Approved' && can('stock_transfers.dispatch')) {
      buttons.add(
        FilledButton(
          key: const Key('transfer_action_dispatch'),
          onPressed: () => _openDispatch(t),
          child: const Text('Dispatch'),
        ),
      );
    }
    if ((t.status == 'Dispatched' || t.status == 'PartiallyReceived') &&
        can('stock_transfers.receive')) {
      buttons.add(
        FilledButton(
          key: const Key('transfer_action_receive'),
          onPressed: () => _openReceive(t),
          child: const Text('Receive'),
        ),
      );
    }
    if ((t.status == 'Dispatched' || t.status == 'PartiallyReceived') &&
        t.items.any((i) => i.quantityInTransit > 0) &&
        can('stock_transfers.cancel')) {
      buttons.add(
        OutlinedButton(
          key: const Key('transfer_action_resolve_discrepancy'),
          onPressed: () => _openReason(
            title: 'Resolve discrepancy',
            body:
                'The remaining in-transit quantity will be written off as a loss. This cannot be undone.',
            onConfirm: (reason) => widget.authState.stockTransfers(
              '${t.id}/resolve-discrepancy',
              method: 'POST',
              body: {'reason': reason},
            ),
          ),
          child: const Text('Resolve Discrepancy'),
        ),
      );
    }
    if ((t.status == 'Draft' || t.status == 'Requested' || t.status == 'Approved') &&
        can('stock_transfers.cancel')) {
      buttons.add(
        OutlinedButton(
          key: const Key('transfer_action_cancel'),
          onPressed: () => _openReason(
            title: 'Cancel transfer',
            body: 'This transfer will be cancelled. No stock has moved yet.',
            onConfirm: (reason) => widget.authState.stockTransfers(
              '${t.id}/cancel',
              method: 'POST',
              body: {'reason': reason},
            ),
          ),
          child: const Text('Cancel Transfer'),
        ),
      );
    }
    return buttons;
  }

  Future<void> _run(Future<dynamic> Function() action) async {
    setState(() => _busy = true);
    try {
      await action();
      _changed = true;
      await _load();
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _openReason({
    required String title,
    required String body,
    required Future<dynamic> Function(String reason) onConfirm,
  }) async {
    final controller = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: Text(title),
        content: SizedBox(
          width: 420,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(body),
              const SizedBox(height: 12),
              TextField(
                key: const Key('reason_field'),
                controller: controller,
                decoration: const InputDecoration(labelText: 'Reason'),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Back'),
          ),
          FilledButton(
            key: const Key('confirm_reason'),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Confirm'),
          ),
        ],
      ),
    );
    if (ok == true) {
      await _run(() => onConfirm(controller.text.trim()));
    }
  }

  Future<void> _openEdit(StockTransferDetails t) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => _CreateTransferDialog(
        authState: widget.authState,
        options: widget.options,
        existingTransfer: t,
      ),
    );
    if (ok == true) {
      _changed = true;
      await _load();
    }
  }

  Future<void> _openApprove(StockTransferDetails t) async {
    final controllers = {
      for (final i in t.items)
        i.id: TextEditingController(text: '${i.quantityRequested}'),
    };
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Approve transfer'),
        content: SizedBox(
          width: 480,
          child: ListView(
            shrinkWrap: true,
            children: t.items
                .map(
                  (i) => Padding(
                    padding: const EdgeInsets.symmetric(vertical: 4),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text('${i.productName} (${i.batchNumber}) — requested ${i.quantityRequested}'),
                        ),
                        SizedBox(
                          width: 90,
                          child: TextField(
                            key: Key('approve_qty_${i.id}'),
                            controller: controllers[i.id],
                            keyboardType: TextInputType.number,
                            decoration: const InputDecoration(labelText: 'Approved'),
                          ),
                        ),
                      ],
                    ),
                  ),
                )
                .toList(),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Back'),
          ),
          FilledButton(
            key: const Key('confirm_approve'),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Approve'),
          ),
        ],
      ),
    );
    if (ok != true) return;
    await _run(
      () => widget.authState.stockTransfers(
        '${t.id}/approve',
        method: 'POST',
        body: {
          'items': t.items
              .map(
                (i) => {
                  'stockTransferItemId': i.id,
                  'quantityApproved': int.tryParse(controllers[i.id]!.text) ?? 0,
                },
              )
              .toList(),
        },
      ),
    );
  }

  Future<void> _openDispatch(StockTransferDetails t) async {
    final controllers = {
      for (final i in t.items)
        i.id: TextEditingController(text: '${i.quantityApproved}'),
    };
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Dispatch transfer'),
        content: SizedBox(
          width: 480,
          child: ListView(
            shrinkWrap: true,
            children: t.items
                .where((i) => i.quantityApproved > 0)
                .map(
                  (i) => Padding(
                    padding: const EdgeInsets.symmetric(vertical: 4),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text('${i.productName} (${i.batchNumber}) — approved ${i.quantityApproved}'),
                        ),
                        SizedBox(
                          width: 90,
                          child: TextField(
                            key: Key('dispatch_qty_${i.id}'),
                            controller: controllers[i.id],
                            keyboardType: TextInputType.number,
                            decoration: const InputDecoration(labelText: 'Dispatched'),
                          ),
                        ),
                      ],
                    ),
                  ),
                )
                .toList(),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Back'),
          ),
          FilledButton(
            key: const Key('confirm_dispatch'),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Dispatch'),
          ),
        ],
      ),
    );
    if (ok != true) return;
    await _run(
      () => widget.authState.stockTransfers(
        '${t.id}/dispatch',
        method: 'POST',
        body: {
          'items': t.items
              .where((i) => i.quantityApproved > 0)
              .map(
                (i) => {
                  'stockTransferItemId': i.id,
                  'quantityDispatched': int.tryParse(controllers[i.id]!.text) ?? 0,
                },
              )
              .toList(),
        },
      ),
    );
  }

  Future<void> _openReceive(StockTransferDetails t) async {
    final pending = t.items.where((i) => i.quantityInTransit > 0).toList();
    final controllers = {
      for (final i in pending)
        i.id: TextEditingController(text: '${i.quantityInTransit}'),
    };
    final noteControllers = {
      for (final i in pending) i.id: TextEditingController(),
    };
    final headerNotes = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Receive transfer'),
        content: SizedBox(
          width: 480,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              ListView(
                shrinkWrap: true,
                children: pending
                    .map(
                      (i) => Padding(
                        padding: const EdgeInsets.symmetric(vertical: 4),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    '${i.productName} (${i.batchNumber}) — in transit ${i.quantityInTransit}',
                                  ),
                                ),
                                SizedBox(
                                  width: 90,
                                  child: TextField(
                                    key: Key('receive_qty_${i.id}'),
                                    controller: controllers[i.id],
                                    keyboardType: TextInputType.number,
                                    decoration: const InputDecoration(labelText: 'Received'),
                                  ),
                                ),
                              ],
                            ),
                            TextField(
                              key: Key('receive_notes_${i.id}'),
                              controller: noteControllers[i.id],
                              decoration: const InputDecoration(
                                labelText: 'Note (optional, e.g. shortage reason)',
                              ),
                            ),
                          ],
                        ),
                      ),
                    )
                    .toList(),
              ),
              const SizedBox(height: 8),
              TextField(
                key: const Key('receive_header_notes'),
                controller: headerNotes,
                decoration: const InputDecoration(labelText: 'Notes (optional)'),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Back'),
          ),
          FilledButton(
            key: const Key('confirm_receive'),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Receive'),
          ),
        ],
      ),
    );
    if (ok != true) return;
    await _run(
      () => widget.authState.stockTransfers(
        '${t.id}/receive',
        method: 'POST',
        body: {
          'items': pending
              .map(
                (i) => {
                  'stockTransferItemId': i.id,
                  'quantityReceived': int.tryParse(controllers[i.id]!.text) ?? 0,
                  'notes': noteControllers[i.id]!.text.trim().isEmpty
                      ? null
                      : noteControllers[i.id]!.text.trim(),
                },
              )
              .toList(),
          'notes': headerNotes.text.trim().isEmpty ? null : headerNotes.text.trim(),
        },
      ),
    );
  }
}

Future<int?> _promptQuantity(
  BuildContext context, {
  required String title,
  required int max,
}) {
  final controller = TextEditingController(text: '1');
  return showDialog<int>(
    context: context,
    builder: (_) => AlertDialog(
      title: Text(title),
      content: SizedBox(
        width: 320,
        child: TextField(
          key: const Key('line_quantity'),
          controller: controller,
          keyboardType: TextInputType.number,
          decoration: InputDecoration(labelText: 'Quantity (max $max)'),
          autofocus: true,
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Cancel'),
        ),
        FilledButton(
          key: const Key('confirm_line_quantity'),
          onPressed: () => Navigator.pop(context, int.tryParse(controller.text)),
          child: const Text('Add'),
        ),
      ],
    ),
  );
}
