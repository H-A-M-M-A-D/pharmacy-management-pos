import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

class SuppliersScreen extends StatefulWidget {
  const SuppliersScreen({required this.authState, super.key});
  final AuthState authState;
  @override
  State<SuppliersScreen> createState() => _SuppliersScreenState();
}

class _SuppliersScreenState extends State<SuppliersScreen> {
  final _search = TextEditingController();
  PagedSuppliers? _suppliers;
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
      final suppliers = await widget.authState.listSuppliers(
        search: _search.text,
      );
      if (mounted) setState(() => _suppliers = suppliers);
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
                width: 240,
                child: Text(
                  'Suppliers',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              SizedBox(
                width: 320,
                child: TextField(
                  controller: _search,
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search),
                    labelText: 'Search suppliers',
                  ),
                  onSubmitted: (_) => _load(),
                ),
              ),
              IconButton.filledTonal(
                onPressed: _load,
                tooltip: 'Refresh',
                icon: const Icon(Icons.refresh),
              ),
              if (can('suppliers.create'))
                FilledButton.icon(
                  key: const Key('add_supplier'),
                  onPressed: () => _showForm(),
                  icon: const Icon(Icons.add_business_outlined),
                  label: const Text('Add Supplier'),
                ),
            ],
          ),
        ),
        Expanded(child: _body()),
      ],
    ),
  );

  Widget _body() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) return Center(child: Text(_error!));
    final items = _suppliers?.items ?? const <SupplierListItem>[];
    if (items.isEmpty) return const Center(child: Text('No suppliers found'));
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          columns: const [
            DataColumn(label: Text('Supplier')),
            DataColumn(label: Text('Contact')),
            DataColumn(label: Text('Phone')),
            DataColumn(label: Text('City')),
            DataColumn(label: Text('Credit Limit')),
            DataColumn(label: Text('Outstanding Balance')),
            DataColumn(label: Text('Status')),
            DataColumn(label: Text('Actions')),
          ],
          rows: items
              .map(
                (supplier) => DataRow(
                  cells: [
                    DataCell(Text(supplier.name)),
                    DataCell(Text(supplier.contactPerson ?? '-')),
                    DataCell(Text(supplier.phoneNumber ?? '-')),
                    DataCell(Text(supplier.city ?? '-')),
                    DataCell(
                      Text(
                        supplier.creditLimit == null
                            ? '-'
                            : _money(supplier.creditLimit!),
                      ),
                    ),
                    DataCell(Text(_balance(supplier.outstandingBalance))),
                    DataCell(
                      Chip(
                        label: Text(supplier.isActive ? 'Active' : 'Inactive'),
                        visualDensity: VisualDensity.compact,
                      ),
                    ),
                    DataCell(
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          IconButton(
                            tooltip: 'View ledger',
                            onPressed: can('suppliers.ledger.view')
                                ? () => _showLedger(supplier)
                                : null,
                            icon: const Icon(Icons.receipt_long_outlined),
                          ),
                          if (can('suppliers.update'))
                            IconButton(
                              tooltip: 'Edit',
                              onPressed: () => _showForm(supplier: supplier),
                              icon: const Icon(Icons.edit_outlined),
                            ),
                          if (can('suppliers.payment.create'))
                            IconButton(
                              tooltip: 'Record payment',
                              onPressed: () => _showPayment(supplier),
                              icon: const Icon(Icons.payments_outlined),
                            ),
                          if (can('suppliers.adjust_balance'))
                            IconButton(
                              tooltip: 'Adjust balance',
                              onPressed: () => _showAdjustment(supplier),
                              icon: const Icon(Icons.balance_outlined),
                            ),
                          if (can(
                            supplier.isActive
                                ? 'suppliers.deactivate'
                                : 'suppliers.activate',
                          ))
                            IconButton(
                              tooltip: supplier.isActive
                                  ? 'Deactivate'
                                  : 'Activate',
                              onPressed: () => _confirmStatus(supplier),
                              icon: Icon(
                                supplier.isActive
                                    ? Icons.block
                                    : Icons.check_circle_outline,
                              ),
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

  Future<void> _showForm({SupplierListItem? supplier}) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) =>
          _SupplierForm(authState: widget.authState, supplier: supplier),
    );
    if (ok == true) await _load();
  }

  Future<void> _showLedger(SupplierListItem supplier) async {
    await showDialog<void>(
      context: context,
      builder: (_) => _SupplierLedgerDialog(
        authState: widget.authState,
        supplier: supplier,
      ),
    );
  }

  Future<void> _showPayment(SupplierListItem supplier) async {
    final accounts = (await widget.authState.listFinancialAccounts(
      branchId: widget.authState.currentUser!.branch.id,
    )).where((x) => x.isActive).toList();
    if (!mounted) return;
    final account = await showDialog<FinancialAccountInfo>(
      context: context,
      builder: (context) => SimpleDialog(
        title: const Text('Payment source account'),
        children: accounts
            .map(
              (x) => SimpleDialogOption(
                onPressed: () => Navigator.pop(context, x),
                child: Text('${x.name} (${_money(x.currentBalance)})'),
              ),
            )
            .toList(),
      ),
    );
    if (account == null || !mounted) return;
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => _PaymentDialog(
        authState: widget.authState,
        supplier: supplier,
        account: account,
      ),
    );
    if (ok == true) await _load();
  }

  Future<void> _showAdjustment(SupplierListItem supplier) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) =>
          _AdjustmentDialog(authState: widget.authState, supplier: supplier),
    );
    if (ok == true) await _load();
  }

  Future<void> _confirmStatus(SupplierListItem supplier) async {
    final active = !supplier.isActive;
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: Text(active ? 'Activate supplier' : 'Deactivate supplier'),
        content: const Text(
          'Supplier will remain in historical records but will no longer appear in normal selection when inactive.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            key: const Key('confirm_supplier_status'),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Confirm'),
          ),
        ],
      ),
    );
    if (ok == true) {
      await widget.authState.setSupplierActive(supplier.id, active);
      await _load();
    }
  }
}

class _SupplierForm extends StatefulWidget {
  const _SupplierForm({required this.authState, this.supplier});
  final AuthState authState;
  final SupplierListItem? supplier;
  @override
  State<_SupplierForm> createState() => _SupplierFormState();
}

class _SupplierFormState extends State<_SupplierForm> {
  final _form = GlobalKey<FormState>();
  late final _name = TextEditingController(text: widget.supplier?.name);
  final _short = TextEditingController();
  late final _contact = TextEditingController(
    text: widget.supplier?.contactPerson,
  );
  late final _phone = TextEditingController(text: widget.supplier?.phoneNumber);
  final _alt = TextEditingController();
  late final _whatsApp = TextEditingController(text: widget.supplier?.whatsApp);
  late final _email = TextEditingController(text: widget.supplier?.email);
  final _address = TextEditingController();
  late final _city = TextEditingController(text: widget.supplier?.city);
  final _ntn = TextEditingController();
  final _strn = TextEditingController();
  final _opening = TextEditingController(text: '0');
  late final _credit = TextEditingController(
    text: widget.supplier?.creditLimit?.toString() ?? '',
  );
  final _days = TextEditingController();
  bool _active = true;
  String? _error;

  @override
  Widget build(BuildContext context) {
    final editing = widget.supplier != null;
    return AlertDialog(
      title: Text(editing ? 'Edit Supplier' : 'Add Supplier'),
      content: SizedBox(
        width: 560,
        child: Form(
          key: _form,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextFormField(
                  key: const Key('supplier_name'),
                  controller: _name,
                  decoration: const InputDecoration(labelText: 'Name'),
                  validator: _required,
                ),
                TextFormField(
                  controller: _short,
                  decoration: const InputDecoration(labelText: 'Short Name'),
                ),
                TextFormField(
                  controller: _contact,
                  decoration: const InputDecoration(
                    labelText: 'Contact Person',
                  ),
                ),
                TextFormField(
                  controller: _phone,
                  decoration: const InputDecoration(labelText: 'Phone'),
                ),
                TextFormField(
                  controller: _alt,
                  decoration: const InputDecoration(
                    labelText: 'Alternate Phone',
                  ),
                ),
                TextFormField(
                  controller: _whatsApp,
                  decoration: const InputDecoration(labelText: 'WhatsApp'),
                ),
                TextFormField(
                  controller: _email,
                  decoration: const InputDecoration(labelText: 'Email'),
                  validator: _emailRule,
                ),
                TextFormField(
                  controller: _address,
                  decoration: const InputDecoration(labelText: 'Address'),
                ),
                TextFormField(
                  controller: _city,
                  decoration: const InputDecoration(labelText: 'City'),
                ),
                TextFormField(
                  controller: _ntn,
                  decoration: const InputDecoration(labelText: 'NTN'),
                ),
                TextFormField(
                  controller: _strn,
                  decoration: const InputDecoration(labelText: 'STRN'),
                ),
                if (!editing)
                  TextFormField(
                    key: const Key('supplier_opening_balance'),
                    controller: _opening,
                    decoration: const InputDecoration(
                      labelText: 'Opening Balance',
                      helperText:
                          'Positive = payable. Negative = advance with supplier.',
                    ),
                    keyboardType: TextInputType.number,
                    validator: _decimal,
                  ),
                TextFormField(
                  controller: _credit,
                  decoration: const InputDecoration(labelText: 'Credit Limit'),
                  keyboardType: TextInputType.number,
                  validator: _optionalNonNegative,
                ),
                TextFormField(
                  controller: _days,
                  decoration: const InputDecoration(
                    labelText: 'Payment Terms Days',
                  ),
                  keyboardType: TextInputType.number,
                  validator: _optionalNonNegativeInt,
                ),
                SwitchListTile(
                  value: _active,
                  onChanged: editing
                      ? null
                      : (v) => setState(() => _active = v),
                  title: const Text('Active'),
                ),
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
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context, false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          key: const Key('save_supplier'),
          onPressed: _save,
          child: const Text('Save'),
        ),
      ],
    );
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    final values = {
      'name': _name.text.trim(),
      'shortName': _empty(_short.text),
      'contactPerson': _empty(_contact.text),
      'phoneNumber': _empty(_phone.text),
      'alternatePhone': _empty(_alt.text),
      'whatsApp': _empty(_whatsApp.text),
      'email': _empty(_email.text),
      'address': _empty(_address.text),
      'city': _empty(_city.text),
      'ntn': _empty(_ntn.text),
      'strn': _empty(_strn.text),
      'creditLimit': _credit.text.trim().isEmpty
          ? null
          : double.parse(_credit.text),
      'paymentTermsDays': _days.text.trim().isEmpty
          ? null
          : int.parse(_days.text),
      if (widget.supplier == null)
        'openingBalance': double.parse(_opening.text),
      if (widget.supplier == null) 'isActive': _active,
    };
    try {
      if (widget.supplier == null) {
        await widget.authState.createSupplier(values);
      } else {
        await widget.authState.updateSupplier(widget.supplier!.id, values);
      }
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    }
  }
}

class _SupplierLedgerDialog extends StatefulWidget {
  const _SupplierLedgerDialog({
    required this.authState,
    required this.supplier,
  });
  final AuthState authState;
  final SupplierListItem supplier;
  @override
  State<_SupplierLedgerDialog> createState() => _SupplierLedgerDialogState();
}

class _SupplierLedgerDialogState extends State<_SupplierLedgerDialog> {
  PagedSupplierLedger? _ledger;
  @override
  void initState() {
    super.initState();
    widget.authState.supplierLedger(widget.supplier.id).then((value) {
      if (mounted) setState(() => _ledger = value);
    });
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text('${widget.supplier.name} Ledger'),
    content: SizedBox(
      width: 720,
      child: _ledger == null
          ? const Center(child: CircularProgressIndicator())
          : DataTable(
              columns: const [
                DataColumn(label: Text('Date')),
                DataColumn(label: Text('Branch')),
                DataColumn(label: Text('Type')),
                DataColumn(label: Text('Amount')),
                DataColumn(label: Text('Balance')),
                DataColumn(label: Text('Notes')),
              ],
              rows: _ledger!.items
                  .map(
                    (x) => DataRow(
                      cells: [
                        DataCell(Text(_date(x.entryDate))),
                        DataCell(Text(x.branchName)),
                        DataCell(Text(x.entryType)),
                        DataCell(Text(_money(x.amount))),
                        DataCell(Text(_balance(x.runningBalance))),
                        DataCell(Text(x.notes ?? '-')),
                      ],
                    ),
                  )
                  .toList(),
            ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('Close'),
      ),
    ],
  );
}

class _PaymentDialog extends StatelessWidget {
  const _PaymentDialog({
    required this.authState,
    required this.supplier,
    required this.account,
  });
  final AuthState authState;
  final SupplierListItem supplier;
  final FinancialAccountInfo account;
  @override
  Widget build(BuildContext context) => _AmountDialog(
    title: 'Record Payment',
    keyName: 'save_supplier_payment',
    balance: supplier.outstandingBalance,
    helper: 'Payment reduces payable. Overpayment becomes advance.',
    onSave: (amount, note) => authState.recordSupplierPayment(supplier.id, {
      'branchId': authState.currentUser!.branch.id,
      'amount': amount,
      'paymentDate': _date(DateTime.now()),
      'paymentMethod': 'Cash',
      'financialAccountId': account.id,
      'notes': note,
    }),
  );
}

class _AdjustmentDialog extends StatelessWidget {
  const _AdjustmentDialog({required this.authState, required this.supplier});
  final AuthState authState;
  final SupplierListItem supplier;
  @override
  Widget build(BuildContext context) => _AmountDialog(
    title: 'Adjust Balance',
    keyName: 'save_supplier_adjustment',
    balance: supplier.outstandingBalance,
    helper: 'Debit increases payable. Credit decreases payable.',
    onSave: (amount, note) => authState.adjustSupplierBalance(supplier.id, {
      'branchId': authState.currentUser!.branch.id,
      'type': 'Debit',
      'amount': amount,
      'reason': note,
      'notes': note,
    }),
  );
}

class _AmountDialog extends StatefulWidget {
  const _AmountDialog({
    required this.title,
    required this.keyName,
    required this.balance,
    required this.helper,
    required this.onSave,
  });
  final String title, keyName, helper;
  final double balance;
  final Future<void> Function(double amount, String note) onSave;
  @override
  State<_AmountDialog> createState() => _AmountDialogState();
}

class _AmountDialogState extends State<_AmountDialog> {
  final _form = GlobalKey<FormState>();
  final _amount = TextEditingController();
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
          Text('Current outstanding: ${_balance(widget.balance)}'),
          Text(widget.helper),
          TextFormField(
            key: Key('${widget.keyName}_amount'),
            controller: _amount,
            decoration: const InputDecoration(labelText: 'Amount'),
            keyboardType: TextInputType.number,
            validator: _positiveDecimal,
          ),
          TextFormField(
            key: Key('${widget.keyName}_reason'),
            controller: _note,
            decoration: const InputDecoration(labelText: 'Reason / notes'),
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
            await widget.onSave(double.parse(_amount.text), _note.text);
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
String? _emailRule(String? value) =>
    value == null || value.trim().isEmpty || value.contains('@')
    ? null
    : 'Enter a valid email address';
String? _decimal(String? value) =>
    double.tryParse(value ?? '') == null ? 'Enter an amount' : null;
String? _optionalNonNegative(String? value) {
  if (value == null || value.trim().isEmpty) return null;
  final parsed = double.tryParse(value);
  return parsed == null || parsed < 0 ? 'Enter a non-negative amount' : null;
}

String? _optionalNonNegativeInt(String? value) {
  if (value == null || value.trim().isEmpty) return null;
  final parsed = int.tryParse(value);
  return parsed == null || parsed < 0 ? 'Enter a non-negative number' : null;
}

String? _positiveDecimal(String? value) {
  final parsed = double.tryParse(value ?? '');
  return parsed == null || parsed <= 0 ? 'Enter a positive amount' : null;
}

String? _empty(String value) => value.trim().isEmpty ? null : value.trim();
String _money(double value) => 'PKR ${value.toStringAsFixed(2)}';
String _balance(double value) =>
    value >= 0 ? '${_money(value)} payable' : '${_money(value.abs())} advance';
String _date(DateTime value) =>
    '${value.year.toString().padLeft(4, '0')}-${value.month.toString().padLeft(2, '0')}-${value.day.toString().padLeft(2, '0')}';
