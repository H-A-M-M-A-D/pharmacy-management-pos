import 'package:flutter/material.dart';
import '../../ui/app_widgets.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

class CashierShiftScreen extends StatefulWidget {
  const CashierShiftScreen({required this.authState, super.key});
  final AuthState authState;

  @override
  State<CashierShiftScreen> createState() => _CashierShiftScreenState();
}

class _CashierShiftScreenState extends State<CashierShiftScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs = TabController(length: 2, vsync: this);
  CashierShift? _myShift;
  PagedCashierShifts? _history;
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
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final canViewHistory = can('cashier_shift.view');
      final results = await Future.wait([
        widget.authState.myOpenCashierShift(),
        canViewHistory
            ? widget.authState.listCashierShifts()
            : Future.value(const PagedCashierShifts(items: [], totalCount: 0)),
      ]);
      if (!mounted) return;
      setState(() {
        _myShift = results[0] as CashierShift?;
        _history = results[1] as PagedCashierShifts;
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
          child: Row(
            children: [
              Text('Cashier Shift', style: Theme.of(context).textTheme.headlineSmall),
              const Spacer(),
              IconButton.filledTonal(
                key: const Key('refresh_cashier_shift'),
                tooltip: 'Refresh',
                onPressed: _load,
                icon: const Icon(Icons.refresh),
              ),
            ],
          ),
        ),
        TabBar(
          controller: _tabs,
          tabs: const [Tab(text: 'My Shift'), Tab(text: 'Shift History')],
        ),
        Expanded(child: _body()),
      ],
    ),
  );

  Widget _body() {
    if (_loading) return const AppLoadingState();
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
        _MyShiftTab(shift: _myShift, authState: widget.authState, onChanged: _load),
        _ShiftHistoryTab(
          history: _history?.items ?? const [],
          authState: widget.authState,
          onChanged: _load,
        ),
      ],
    );
  }
}

class _MyShiftTab extends StatelessWidget {
  const _MyShiftTab({
    required this.shift,
    required this.authState,
    required this.onChanged,
  });
  final CashierShift? shift;
  final AuthState authState;
  final VoidCallback onChanged;

  bool can(String p) => authState.can(p);

  @override
  Widget build(BuildContext context) {
    if (shift == null) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('You do not have an open cashier shift.'),
            const SizedBox(height: 12),
            if (can('cashier_shift.open'))
              FilledButton.icon(
                key: const Key('open_cashier_shift'),
                onPressed: () async {
                  final ok = await showDialog<bool>(
                    context: context,
                    builder: (_) => _OpenShiftDialog(authState: authState),
                  );
                  if (ok == true) onChanged();
                },
                icon: const Icon(Icons.point_of_sale),
                label: const Text('Open Shift'),
              ),
          ],
        ),
      );
    }
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: _ShiftDetailCard(
        shift: shift!,
        authState: authState,
        onChanged: onChanged,
      ),
    );
  }
}

class _ShiftDetailCard extends StatelessWidget {
  const _ShiftDetailCard({
    required this.shift,
    required this.authState,
    required this.onChanged,
  });
  final CashierShift shift;
  final AuthState authState;
  final VoidCallback onChanged;

  bool can(String p) => authState.can(p);

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 16,
            runSpacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              _StatusChip(label: shift.status),
              Text('${shift.branchName} · ${shift.cashierName}'),
              if (shift.terminalName != null) Text('Terminal: ${shift.terminalName}'),
              Text('Opened: ${_dateTime(shift.openedAtUtc)}'),
            ],
          ),
          const Divider(height: 32),
          Wrap(
            spacing: 24,
            runSpacing: 12,
            children: [
              _Figure('Opening Cash', shift.openingCash),
              _Figure('Total Sales', shift.totalSales),
              _Figure('Total Refunds', shift.totalRefunds),
              _Figure('Cash Sales', shift.cashSales),
              _Figure('Cash Refunds', shift.cashRefunds),
              _Figure('Customer Cash Received', shift.customerCashReceived),
              _Figure('Cash Paid Out', shift.cashPaidOut),
              _Figure('Manual Cash In', shift.manualCashIn),
              _Figure('Manual Cash Out', shift.manualCashOut),
              if (shift.expectedCash != null) _Figure('Expected Cash', shift.expectedCash!),
              if (shift.actualCountedCash != null)
                _Figure('Actual Counted Cash', shift.actualCountedCash!),
              if (shift.cashVariance != null)
                _Figure(
                  'Variance',
                  shift.cashVariance!,
                  emphasize: shift.cashVariance != 0,
                ),
            ],
          ),
          if (shift.paymentBreakdown.isNotEmpty) ...[
            const SizedBox(height: 20),
            Text('Payment Method Breakdown', style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: 8),
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: AppDataTable(
                columns: const [
                  DataColumn(label: Text('Method')),
                  DataColumn(label: Text('Sales')),
                  DataColumn(label: Text('Refunds')),
                ],
                rows: shift.paymentBreakdown
                    .map(
                      (x) => DataRow(
                        cells: [
                          DataCell(Text(x.paymentMethod)),
                          DataCell(Text(_money(x.salesAmount))),
                          DataCell(Text(_money(x.refundsAmount))),
                        ],
                      ),
                    )
                    .toList(),
              ),
            ),
          ],
          if (shift.drawerEntries.isNotEmpty) ...[
            const SizedBox(height: 20),
            Text('Manual Drawer Entries', style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: 8),
            ...shift.drawerEntries.map(
              (x) => ListTile(
                dense: true,
                contentPadding: EdgeInsets.zero,
                leading: Icon(
                  x.entryType == 'CashIn' ? Icons.add_circle_outline : Icons.remove_circle_outline,
                ),
                title: Text('${x.entryType == 'CashIn' ? '+' : '-'}${_money(x.amount)}  ${x.reason}'),
                subtitle: Text('${x.createdBy} · ${_dateTime(x.createdAtUtc)}'),
              ),
            ),
          ],
          if (shift.closingNotes != null) ...[
            const SizedBox(height: 12),
            Text('Closing notes: ${shift.closingNotes}'),
          ],
          if (shift.reconciledBy != null) ...[
            const SizedBox(height: 12),
            Text('Reconciled by ${shift.reconciledBy} at ${_dateTime(shift.reconciledAtUtc!)}'),
            if (shift.reconciliationNotes != null) Text(shift.reconciliationNotes!),
          ],
          const SizedBox(height: 20),
          Wrap(
            spacing: 12,
            children: [
              if (shift.status == 'Open' && can('cashier_shift.drawer_adjust')) ...[
                OutlinedButton.icon(
                  key: const Key('cashier_shift_cash_in'),
                  onPressed: () => _drawerEntry(context, 'CashIn'),
                  icon: const Icon(Icons.add),
                  label: const Text('Cash In'),
                ),
                OutlinedButton.icon(
                  key: const Key('cashier_shift_cash_out'),
                  onPressed: () => _drawerEntry(context, 'CashOut'),
                  icon: const Icon(Icons.remove),
                  label: const Text('Cash Out'),
                ),
              ],
              if (shift.status == 'Open' && can('cashier_shift.close'))
                FilledButton.icon(
                  key: const Key('close_cashier_shift'),
                  onPressed: () async {
                    final ok = await showDialog<bool>(
                      context: context,
                      builder: (_) => _CloseShiftDialog(shift: shift, authState: authState),
                    );
                    if (ok == true) onChanged();
                  },
                  icon: const Icon(Icons.lock_outline),
                  label: const Text('Close Shift'),
                ),
              if (shift.status == 'Closed' && can('cashier_shift.reconcile'))
                FilledButton.icon(
                  key: const Key('reconcile_cashier_shift'),
                  onPressed: () async {
                    final ok = await showDialog<bool>(
                      context: context,
                      builder: (_) => _ReconcileShiftDialog(shift: shift, authState: authState),
                    );
                    if (ok == true) onChanged();
                  },
                  icon: const Icon(Icons.fact_check_outlined),
                  label: const Text('Reconcile'),
                ),
            ],
          ),
        ],
      ),
    ),
  );

  Future<void> _drawerEntry(BuildContext context, String entryType) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => _DrawerEntryDialog(
        shiftId: shift.id,
        entryType: entryType,
        authState: authState,
      ),
    );
    if (ok == true) onChanged();
  }
}

class _Figure extends StatelessWidget {
  const _Figure(this.label, this.value, {this.emphasize = false});
  final String label;
  final double value;
  final bool emphasize;
  @override
  Widget build(BuildContext context) => SizedBox(
    width: 170,
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: Theme.of(context).textTheme.bodySmall),
        Text(
          _money(value),
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
            color: emphasize ? Theme.of(context).colorScheme.error : null,
          ),
        ),
      ],
    ),
  );
}

class _OpenShiftDialog extends StatefulWidget {
  const _OpenShiftDialog({required this.authState});
  final AuthState authState;
  @override
  State<_OpenShiftDialog> createState() => _OpenShiftDialogState();
}

class _OpenShiftDialogState extends State<_OpenShiftDialog> {
  final _form = GlobalKey<FormState>();
  final _openingCash = TextEditingController(text: '0');
  final _terminal = TextEditingController();
  final _notes = TextEditingController();
  String? _error;

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Open Cashier Shift'),
    content: SizedBox(
      width: 420,
      child: Form(
        key: _form,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('Branch: ${widget.authState.currentUser!.branch.name}'),
            const SizedBox(height: 8),
            TextFormField(
              key: const Key('open_shift_cash'),
              controller: _openingCash,
              decoration: const InputDecoration(labelText: 'Opening Cash'),
              keyboardType: TextInputType.number,
              validator: _nonNegative,
            ),
            TextFormField(
              controller: _terminal,
              decoration: const InputDecoration(labelText: 'Terminal (optional)'),
            ),
            TextFormField(
              controller: _notes,
              decoration: const InputDecoration(labelText: 'Notes (optional)'),
            ),
            if (_error != null)
              Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
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
        key: const Key('save_open_shift'),
        onPressed: () async {
          if (!_form.currentState!.validate()) return;
          try {
            await widget.authState.openCashierShift({
              'branchId': widget.authState.currentUser!.branch.id,
              'openingCash': double.parse(_openingCash.text),
              'terminalName': _terminal.text.trim().isEmpty ? null : _terminal.text.trim(),
              'openingNotes': _notes.text.trim().isEmpty ? null : _notes.text.trim(),
            });
            if (context.mounted) Navigator.pop(context, true);
          } on ApiException catch (e) {
            setState(() => _error = e.message);
          }
        },
        child: const Text('Open Shift'),
      ),
    ],
  );
}

class _DrawerEntryDialog extends StatefulWidget {
  const _DrawerEntryDialog({
    required this.shiftId,
    required this.entryType,
    required this.authState,
  });
  final String shiftId, entryType;
  final AuthState authState;
  @override
  State<_DrawerEntryDialog> createState() => _DrawerEntryDialogState();
}

class _DrawerEntryDialogState extends State<_DrawerEntryDialog> {
  final _form = GlobalKey<FormState>();
  final _amount = TextEditingController();
  final _reason = TextEditingController();
  String? _error;

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(widget.entryType == 'CashIn' ? 'Add Cash to Drawer' : 'Remove Cash from Drawer'),
    content: Form(
      key: _form,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          TextFormField(
            key: const Key('drawer_entry_amount'),
            controller: _amount,
            decoration: const InputDecoration(labelText: 'Amount'),
            keyboardType: TextInputType.number,
            validator: _positiveDouble,
          ),
          TextFormField(
            key: const Key('drawer_entry_reason'),
            controller: _reason,
            decoration: const InputDecoration(labelText: 'Reason'),
            validator: _required,
          ),
          if (_error != null)
            Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
        ],
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context, false),
        child: const Text('Cancel'),
      ),
      FilledButton(
        key: const Key('save_drawer_entry'),
        onPressed: () async {
          if (!_form.currentState!.validate()) return;
          try {
            await widget.authState.addCashierShiftDrawerEntry(widget.shiftId, {
              'entryType': widget.entryType,
              'amount': double.parse(_amount.text),
              'reason': _reason.text.trim(),
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

class _CloseShiftDialog extends StatefulWidget {
  const _CloseShiftDialog({required this.shift, required this.authState});
  final CashierShift shift;
  final AuthState authState;
  @override
  State<_CloseShiftDialog> createState() => _CloseShiftDialogState();
}

class _CloseShiftDialogState extends State<_CloseShiftDialog> {
  final _form = GlobalKey<FormState>();
  final _actual = TextEditingController();
  final _notes = TextEditingController();
  String? _error;

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Close Cashier Shift'),
    content: SizedBox(
      width: 420,
      child: Form(
        key: _form,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (widget.shift.expectedCash != null)
              Text('Expected cash: ${_money(widget.shift.expectedCash!)}'),
            const SizedBox(height: 8),
            TextFormField(
              key: const Key('close_shift_actual_cash'),
              controller: _actual,
              decoration: const InputDecoration(labelText: 'Actual Counted Cash'),
              keyboardType: TextInputType.number,
              validator: _nonNegative,
            ),
            TextFormField(
              controller: _notes,
              decoration: const InputDecoration(labelText: 'Closing Notes (optional)'),
            ),
            if (_error != null)
              Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
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
        key: const Key('save_close_shift'),
        onPressed: () async {
          if (!_form.currentState!.validate()) return;
          try {
            await widget.authState.closeCashierShift(widget.shift.id, {
              'actualCountedCash': double.parse(_actual.text),
              'closingNotes': _notes.text.trim().isEmpty ? null : _notes.text.trim(),
            });
            if (context.mounted) Navigator.pop(context, true);
          } on ApiException catch (e) {
            setState(() => _error = e.message);
          }
        },
        child: const Text('Close Shift'),
      ),
    ],
  );
}

class _ReconcileShiftDialog extends StatefulWidget {
  const _ReconcileShiftDialog({required this.shift, required this.authState});
  final CashierShift shift;
  final AuthState authState;
  @override
  State<_ReconcileShiftDialog> createState() => _ReconcileShiftDialogState();
}

class _ReconcileShiftDialogState extends State<_ReconcileShiftDialog> {
  final _notes = TextEditingController();
  String? _error;

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Reconcile Shift'),
    content: SizedBox(
      width: 420,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (widget.shift.cashVariance != null)
            Text('Cash variance: ${_money(widget.shift.cashVariance!)}'),
          const SizedBox(height: 8),
          TextField(
            key: const Key('reconcile_notes'),
            controller: _notes,
            decoration: const InputDecoration(labelText: 'Reconciliation Notes (optional)'),
          ),
          if (_error != null)
            Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
        ],
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context, false),
        child: const Text('Cancel'),
      ),
      FilledButton(
        key: const Key('confirm_reconcile_shift'),
        onPressed: () async {
          try {
            await widget.authState.reconcileCashierShift(
              widget.shift.id,
              _notes.text.trim().isEmpty ? null : _notes.text.trim(),
            );
            if (context.mounted) Navigator.pop(context, true);
          } on ApiException catch (e) {
            setState(() => _error = e.message);
          }
        },
        child: const Text('Confirm Reconciled'),
      ),
    ],
  );
}

class _ShiftHistoryTab extends StatelessWidget {
  const _ShiftHistoryTab({
    required this.history,
    required this.authState,
    required this.onChanged,
  });
  final List<CashierShiftListItem> history;
  final AuthState authState;
  final VoidCallback onChanged;

  @override
  Widget build(BuildContext context) {
    if (!authState.can('cashier_shift.view')) {
      return AppEmptyState(title: 'You do not have permission to view shift history.');
    }
    if (history.isEmpty) {
      return AppEmptyState(title: 'No cashier shifts found');
    }
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: AppDataTable(
          columns: const [
            DataColumn(label: Text('Cashier')),
            DataColumn(label: Text('Branch')),
            DataColumn(label: Text('Terminal')),
            DataColumn(label: Text('Opened')),
            DataColumn(label: Text('Status')),
            DataColumn(label: Text('Expected')),
            DataColumn(label: Text('Actual')),
            DataColumn(label: Text('Variance')),
            DataColumn(label: Text('')),
          ],
          rows: history
              .map(
                (x) => DataRow(
                  cells: [
                    DataCell(Text(x.cashierName)),
                    DataCell(Text(x.branchName)),
                    DataCell(Text(x.terminalName ?? '-')),
                    DataCell(Text(_dateTime(x.openedAtUtc))),
                    DataCell(_StatusChip(label: x.status)),
                    DataCell(Text(x.expectedCash == null ? '-' : _money(x.expectedCash!))),
                    DataCell(Text(x.actualCountedCash == null ? '-' : _money(x.actualCountedCash!))),
                    DataCell(Text(x.cashVariance == null ? '-' : _money(x.cashVariance!))),
                    DataCell(
                      TextButton(
                        key: Key('open_cashier_shift_${x.id}'),
                        onPressed: () async {
                          final full = await authState.cashierShiftDetails(x.id);
                          if (!context.mounted) return;
                          final ok = await showDialog<bool>(
                            context: context,
                            builder: (_) => Dialog(
                              child: SizedBox(
                                width: 640,
                                child: SingleChildScrollView(
                                  padding: const EdgeInsets.all(16),
                                  child: _ShiftDetailCard(
                                    shift: full,
                                    authState: authState,
                                    onChanged: () => Navigator.pop(context, true),
                                  ),
                                ),
                              ),
                            ),
                          );
                          if (ok == true) onChanged();
                        },
                        child: const Text('View'),
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
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.label});
  final String label;
  @override
  Widget build(BuildContext context) =>
      AppStatusChip(label);
}

String? _required(String? value) =>
    value == null || value.trim().isEmpty ? 'Required' : null;
String? _nonNegative(String? value) {
  final parsed = double.tryParse(value ?? '');
  return parsed == null || parsed < 0 ? 'Enter a non-negative amount' : null;
}

String? _positiveDouble(String? value) {
  final parsed = double.tryParse(value ?? '');
  return parsed == null || parsed <= 0 ? 'Enter a positive amount' : null;
}

String _money(double value) => 'PKR ${value.toStringAsFixed(2)}';
String _dateTime(DateTime value) => '${value.toLocal()}'.split('.').first;
