import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

class CustomersScreen extends StatefulWidget {
  const CustomersScreen({required this.authState, super.key});
  final AuthState authState;

  @override
  State<CustomersScreen> createState() => _CustomersScreenState();
}

class _CustomersScreenState extends State<CustomersScreen> {
  final _search = TextEditingController();
  PagedCustomers? _customers;
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
      final customers = await widget.authState.listCustomers(
        search: _search.text,
      );
      if (mounted) setState(() => _customers = customers);
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
          child: Wrap(
            spacing: 12,
            runSpacing: 12,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              SizedBox(
                width: 240,
                child: Text(
                  'Customers',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              SizedBox(
                width: 320,
                child: TextField(
                  key: const Key('customer_search'),
                  controller: _search,
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search),
                    labelText: 'Search customers',
                  ),
                  onSubmitted: (_) => _load(),
                ),
              ),
              IconButton.filledTonal(
                onPressed: _load,
                tooltip: 'Refresh',
                icon: const Icon(Icons.refresh),
              ),
              if (can('customers.create'))
                FilledButton.icon(
                  key: const Key('add_customer'),
                  onPressed: () => _showForm(),
                  icon: const Icon(Icons.person_add_alt_1_outlined),
                  label: const Text('Add Customer'),
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
    final items = _customers?.items ?? const <CustomerListItem>[];
    if (items.isEmpty) return const Center(child: Text('No customers found'));
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          columns: const [
            DataColumn(label: Text('Code')),
            DataColumn(label: Text('Customer')),
            DataColumn(label: Text('Phone')),
            DataColumn(label: Text('City')),
            DataColumn(label: Text('Credit Limit')),
            DataColumn(label: Text('Outstanding')),
            DataColumn(label: Text('Advance')),
            DataColumn(label: Text('Status')),
            DataColumn(label: Text('Actions')),
          ],
          rows: items
              .map(
                (customer) => DataRow(
                  cells: [
                    DataCell(Text(customer.customerCode)),
                    DataCell(Text(customer.name)),
                    DataCell(Text(customer.phoneNumber ?? '-')),
                    DataCell(Text(customer.city ?? '-')),
                    DataCell(Text(_money(customer.creditLimit))),
                    DataCell(Text(_money(customer.outstandingBalance))),
                    DataCell(Text(_money(customer.advanceBalance))),
                    DataCell(
                      Chip(
                        label: Text(customer.isActive ? 'Active' : 'Inactive'),
                        visualDensity: VisualDensity.compact,
                      ),
                    ),
                    DataCell(
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          IconButton(
                            tooltip: 'View ledger',
                            onPressed: can('customers.ledger.view')
                                ? () => _showLedger(customer)
                                : null,
                            icon: const Icon(Icons.receipt_long_outlined),
                          ),
                          if (can('customers.update'))
                            IconButton(
                              tooltip: 'Edit',
                              onPressed: () => _showForm(customer: customer),
                              icon: const Icon(Icons.edit_outlined),
                            ),
                          if (can('customers.payment.create'))
                            IconButton(
                              tooltip: 'Record payment',
                              onPressed: () => _showPayment(customer),
                              icon: const Icon(Icons.payments_outlined),
                            ),
                          if (can('customers.adjust_balance'))
                            IconButton(
                              tooltip: 'Adjust balance',
                              onPressed: () => _showAdjustment(customer),
                              icon: const Icon(Icons.balance_outlined),
                            ),
                          if (can(
                            customer.isActive
                                ? 'customers.deactivate'
                                : 'customers.activate',
                          ))
                            IconButton(
                              tooltip: customer.isActive
                                  ? 'Deactivate'
                                  : 'Activate',
                              onPressed: () => _confirmStatus(customer),
                              icon: Icon(
                                customer.isActive
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

  Future<void> _showForm({CustomerListItem? customer}) async {
    CustomerDetails? details;
    if (customer != null) {
      try {
        details = await widget.authState.customerDetails(customer.id);
      } on ApiException catch (error) {
        if (mounted) setState(() => _error = error.message);
        return;
      }
    }
    if (!mounted) return;
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) =>
          _CustomerForm(authState: widget.authState, customer: details),
    );
    if (ok == true) await _load();
  }

  Future<void> _showLedger(CustomerListItem customer) async {
    await showDialog<void>(
      context: context,
      builder: (_) => _CustomerLedgerDialog(
        authState: widget.authState,
        customer: customer,
      ),
    );
  }

  Future<void> _showPayment(CustomerListItem customer) async {
    final accounts = (await widget.authState.listFinancialAccounts(
      branchId: widget.authState.currentUser!.branch.id,
    )).where((x) => x.isActive).toList();
    if (!mounted) return;
    final account = await showDialog<FinancialAccountInfo>(
      context: context,
      builder: (context) => SimpleDialog(
        title: const Text('Receiving financial account'),
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
      builder: (_) => _AmountDialog(
        title: 'Record Customer Payment',
        keyName: 'save_customer_payment',
        helper:
            'Payment reduces customer receivable. Overpayment becomes advance.',
        onSave: (amount, note) =>
            widget.authState.recordCustomerPayment(customer.id, {
              'branchId': widget.authState.currentUser!.branch.id,
              'amount': amount,
              'paymentDateUtc': DateTime.now().toUtc().toIso8601String(),
              'paymentMethod': 'Cash',
              'financialAccountId': account.id,
              'notes': note,
            }),
      ),
    );
    if (ok == true) await _load();
  }

  Future<void> _showAdjustment(CustomerListItem customer) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => _AmountDialog(
        title: 'Adjust Customer Balance',
        keyName: 'save_customer_adjustment',
        helper: 'Debit increases receivable. Credit decreases receivable.',
        onSave: (amount, note) =>
            widget.authState.adjustCustomerBalance(customer.id, {
              'branchId': widget.authState.currentUser!.branch.id,
              'type': 'Debit',
              'amount': amount,
              'reason': note,
              'notes': note,
            }),
      ),
    );
    if (ok == true) await _load();
  }

  Future<void> _confirmStatus(CustomerListItem customer) async {
    final active = !customer.isActive;
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: Text(active ? 'Activate customer' : 'Deactivate customer'),
        content: const Text(
          'Customer history and ledger remain permanent. Inactive customers cannot be selected for new credit sales.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            key: const Key('confirm_customer_status'),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Confirm'),
          ),
        ],
      ),
    );
    if (ok == true) {
      await widget.authState.setCustomerActive(customer.id, active);
      await _load();
    }
  }
}

class _CustomerForm extends StatefulWidget {
  const _CustomerForm({required this.authState, this.customer});
  final AuthState authState;
  final CustomerDetails? customer;

  @override
  State<_CustomerForm> createState() => _CustomerFormState();
}

class _CustomerFormState extends State<_CustomerForm> {
  final _form = GlobalKey<FormState>();
  late final _name = TextEditingController(text: widget.customer?.name);
  late final _phone = TextEditingController(text: widget.customer?.phoneNumber);
  late final _alternate = TextEditingController(
    text: widget.customer?.alternatePhone,
  );
  late final _email = TextEditingController(text: widget.customer?.email);
  late final _address = TextEditingController(text: widget.customer?.address);
  late final _city = TextEditingController(text: widget.customer?.city);
  late final _business = TextEditingController(
    text: widget.customer?.businessName,
  );
  late final _ntn = TextEditingController(text: widget.customer?.ntn);
  final _opening = TextEditingController(text: '0');
  late final _credit = TextEditingController(
    text: widget.customer?.creditLimit.toString() ?? '0',
  );
  bool _active = true;
  String? _error;

  @override
  void dispose() {
    _name.dispose();
    _phone.dispose();
    _alternate.dispose();
    _email.dispose();
    _address.dispose();
    _city.dispose();
    _business.dispose();
    _ntn.dispose();
    _opening.dispose();
    _credit.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final editing = widget.customer != null;
    return AlertDialog(
      title: Text(editing ? 'Edit Customer' : 'Add Customer'),
      content: SizedBox(
        width: 560,
        child: Form(
          key: _form,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextFormField(
                  key: const Key('customer_name'),
                  controller: _name,
                  decoration: const InputDecoration(labelText: 'Name'),
                  validator: _required,
                ),
                TextFormField(
                  controller: _phone,
                  decoration: const InputDecoration(labelText: 'Phone'),
                ),
                TextFormField(
                  controller: _alternate,
                  decoration: const InputDecoration(
                    labelText: 'Alternate Phone',
                  ),
                ),
                TextFormField(
                  controller: _email,
                  decoration: const InputDecoration(labelText: 'Email'),
                  validator: _emailRule,
                ),
                TextFormField(
                  controller: _business,
                  decoration: const InputDecoration(labelText: 'Business Name'),
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
                if (!editing)
                  TextFormField(
                    key: const Key('customer_opening_balance'),
                    controller: _opening,
                    decoration: const InputDecoration(
                      labelText: 'Opening Balance',
                      helperText:
                          'Positive = receivable. Negative = customer advance.',
                    ),
                    keyboardType: TextInputType.number,
                    validator: _decimal,
                  ),
                TextFormField(
                  key: const Key('customer_credit_limit'),
                  controller: _credit,
                  decoration: const InputDecoration(labelText: 'Credit Limit'),
                  keyboardType: TextInputType.number,
                  validator: _nonNegative,
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
          key: const Key('save_customer'),
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
      'phoneNumber': _empty(_phone.text),
      'alternatePhone': _empty(_alternate.text),
      'email': _empty(_email.text),
      'address': _empty(_address.text),
      'city': _empty(_city.text),
      'businessName': _empty(_business.text),
      'ntn': _empty(_ntn.text),
      'creditLimit': double.parse(_credit.text),
      if (widget.customer == null)
        'openingBalance': double.parse(_opening.text),
      if (widget.customer == null) 'isActive': _active,
    };
    try {
      if (widget.customer == null) {
        await widget.authState.createCustomer(values);
      } else {
        await widget.authState.updateCustomer(widget.customer!.id, values);
      }
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    }
  }
}

class _CustomerLedgerDialog extends StatefulWidget {
  const _CustomerLedgerDialog({
    required this.authState,
    required this.customer,
  });
  final AuthState authState;
  final CustomerListItem customer;

  @override
  State<_CustomerLedgerDialog> createState() => _CustomerLedgerDialogState();
}

class _CustomerLedgerDialogState extends State<_CustomerLedgerDialog> {
  PagedCustomerLedger? _ledger;

  @override
  void initState() {
    super.initState();
    widget.authState.customerLedger(widget.customer.id).then((value) {
      if (mounted) setState(() => _ledger = value);
    });
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text('${widget.customer.name} Ledger'),
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
                        DataCell(Text(_money(x.runningBalance))),
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

class _AmountDialog extends StatefulWidget {
  const _AmountDialog({
    required this.title,
    required this.keyName,
    required this.helper,
    required this.onSave,
  });
  final String title, keyName, helper;
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
  void dispose() {
    _amount.dispose();
    _note.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(widget.title),
    content: Form(
      key: _form,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
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
          } on ApiException catch (error) {
            setState(() => _error = error.message);
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
String? _nonNegative(String? value) {
  final parsed = double.tryParse(value ?? '');
  return parsed == null || parsed < 0 ? 'Enter a non-negative amount' : null;
}

String? _positiveDecimal(String? value) {
  final parsed = double.tryParse(value ?? '');
  return parsed == null || parsed <= 0 ? 'Enter a positive amount' : null;
}

String? _empty(String value) => value.trim().isEmpty ? null : value.trim();
String _money(double value) => 'PKR ${value.toStringAsFixed(2)}';
String _date(DateTime value) =>
    '${value.year.toString().padLeft(4, '0')}-${value.month.toString().padLeft(2, '0')}-${value.day.toString().padLeft(2, '0')}';
