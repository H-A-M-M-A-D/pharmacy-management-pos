import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

class FinanceScreen extends StatefulWidget {
  const FinanceScreen({required this.authState, super.key});
  final AuthState authState;
  @override
  State<FinanceScreen> createState() => _FinanceScreenState();
}

class _FinanceScreenState extends State<FinanceScreen> {
  var _tab = 0;
  var _loading = true;
  String? _error;
  List<FinancialAccountInfo> _accounts = [];
  List<ExpenseInfo> _expenses = [];
  DailyCashPosition? _position;
  String get _branchId => widget.authState.currentUser!.branch.id;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final accounts = await widget.authState.listFinancialAccounts(
        branchId: _branchId,
      );
      final expenses = widget.authState.can('expenses.view')
          ? await widget.authState.listExpenses()
          : <ExpenseInfo>[];
      final position = widget.authState.can('finance.ledger.view')
          ? await widget.authState.dailyCashPosition(_branchId, DateTime.now())
          : null;
      if (mounted) {
        setState(() {
          _accounts = accounts;
          _expenses = expenses;
          _position = position;
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
        Padding(
          padding: const EdgeInsets.fromLTRB(24, 20, 24, 10),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Finance',
                      style: Theme.of(context).textTheme.headlineMedium,
                    ),
                    const Text(
                      'Operational cash accounts and immutable ledger',
                    ),
                  ],
                ),
              ),
              IconButton(
                tooltip: 'Refresh',
                onPressed: _load,
                icon: const Icon(Icons.refresh),
              ),
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 24),
          child: SegmentedButton<int>(
            segments: const [
              ButtonSegment(
                value: 0,
                icon: Icon(Icons.account_balance_wallet_outlined),
                label: Text('Accounts'),
              ),
              ButtonSegment(
                value: 1,
                icon: Icon(Icons.receipt_long_outlined),
                label: Text('Expenses'),
              ),
              ButtonSegment(
                value: 2,
                icon: Icon(Icons.today_outlined),
                label: Text('Cash position'),
              ),
            ],
            selected: {_tab},
            onSelectionChanged: (v) => setState(() => _tab = v.first),
          ),
        ),
        if (_error != null)
          Padding(
            padding: const EdgeInsets.all(12),
            child: Text(
              _error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ),
        Expanded(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : switch (_tab) {
                  0 => _accountsView(),
                  1 => _expensesView(),
                  _ => _positionView(),
                },
        ),
      ],
    ),
  );

  Widget _accountsView() => Padding(
    padding: const EdgeInsets.all(24),
    child: Column(
      children: [
        Row(
          children: [
            if (widget.authState.can('accounts.manage'))
              FilledButton.icon(
                onPressed: _addAccount,
                icon: const Icon(Icons.add),
                label: const Text('Account'),
              ),
            const SizedBox(width: 8),
            if (widget.authState.can('finance.income.create'))
              OutlinedButton.icon(
                onPressed: _otherIncome,
                icon: const Icon(Icons.arrow_downward),
                label: const Text('Other income'),
              ),
            const SizedBox(width: 8),
            if (widget.authState.can('finance.transfer'))
              OutlinedButton.icon(
                onPressed: _transfer,
                icon: const Icon(Icons.swap_horiz),
                label: const Text('Transfer'),
              ),
          ],
        ),
        const SizedBox(height: 16),
        Expanded(
          child: SingleChildScrollView(
            child: SizedBox(
              width: double.infinity,
              child: DataTable(
                columns: const [
                  DataColumn(label: Text('Account')),
                  DataColumn(label: Text('Actions')),
                  DataColumn(label: Text('Type')),
                  DataColumn(label: Text('Branch')),
                  DataColumn(label: Text('Balance')),
                  DataColumn(label: Text('Status')),
                ],
                rows: _accounts
                    .map(
                      (a) => DataRow(
                        cells: [
                          DataCell(Text(a.name)),
                          DataCell(
                            IconButton(
                              tooltip: 'View ledger',
                              onPressed:
                                  widget.authState.can('finance.ledger.view')
                                  ? () => _ledger(a)
                                  : null,
                              icon: const Icon(Icons.history),
                            ),
                          ),
                          DataCell(Text(a.accountType)),
                          DataCell(Text(a.branchName)),
                          DataCell(Text(_money(a.currentBalance))),
                          DataCell(Text(a.isActive ? 'Active' : 'Inactive')),
                        ],
                      ),
                    )
                    .toList(),
              ),
            ),
          ),
        ),
      ],
    ),
  );

  Widget _expensesView() => Padding(
    padding: const EdgeInsets.all(24),
    child: Column(
      children: [
        Align(
          alignment: Alignment.centerLeft,
          child: widget.authState.can('expenses.create')
              ? FilledButton.icon(
                  onPressed: _addExpense,
                  icon: const Icon(Icons.add),
                  label: const Text('Post expense'),
                )
              : const SizedBox.shrink(),
        ),
        const SizedBox(height: 16),
        Expanded(
          child: SingleChildScrollView(
            child: SizedBox(
              width: double.infinity,
              child: DataTable(
                columns: const [
                  DataColumn(label: Text('Expense #')),
                  DataColumn(label: Text('Date')),
                  DataColumn(label: Text('Category')),
                  DataColumn(label: Text('Payee')),
                  DataColumn(label: Text('Account')),
                  DataColumn(label: Text('Amount')),
                  DataColumn(label: Text('Created by')),
                ],
                rows: _expenses
                    .map(
                      (e) => DataRow(
                        cells: [
                          DataCell(Text(e.expenseNumber)),
                          DataCell(Text(_date(e.expenseDateUtc))),
                          DataCell(Text(e.categoryName)),
                          DataCell(Text(e.payee ?? '-')),
                          DataCell(Text(e.accountName)),
                          DataCell(Text(_money(e.amount))),
                          DataCell(Text(e.createdByName)),
                        ],
                      ),
                    )
                    .toList(),
              ),
            ),
          ),
        ),
      ],
    ),
  );

  Widget _positionView() {
    final p = _position;
    if (p == null) {
      return const Center(child: Text('Cash position permission is required.'));
    }
    return Padding(
      padding: const EdgeInsets.all(24),
      child: GridView.count(
        crossAxisCount: 2,
        childAspectRatio: 3,
        mainAxisSpacing: 12,
        crossAxisSpacing: 12,
        children: [
          _metric('Opening', p.openingBalance, Icons.first_page),
          _metric('Money in', p.moneyIn, Icons.south_west),
          _metric('Money out', p.moneyOut, Icons.north_east),
          _metric('Closing', p.closingBalance, Icons.account_balance_wallet),
        ],
      ),
    );
  }

  Widget _metric(String label, double value, IconData icon) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Row(
        children: [
          Icon(icon),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Text(label),
                FittedBox(
                  fit: BoxFit.scaleDown,
                  alignment: Alignment.centerLeft,
                  child: Text(
                    _money(value),
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    ),
  );

  Future<void> _addAccount() async {
    final name = TextEditingController(),
        opening = TextEditingController(text: '0.00'),
        notes = TextEditingController();
    String type = 'Cash';
    bool active = true;
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setLocal) => AlertDialog(
          title: const Text('Add financial account'),
          content: SizedBox(
            width: 460,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextField(
                  controller: name,
                  decoration: const InputDecoration(
                    labelText: 'Account name *',
                  ),
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField(
                  initialValue: type,
                  decoration: const InputDecoration(labelText: 'Account type'),
                  items:
                      [
                            'Cash',
                            'Bank',
                            'MobileWallet',
                            'CardSettlement',
                            'Other',
                          ]
                          .map(
                            (x) => DropdownMenuItem(value: x, child: Text(x)),
                          )
                          .toList(),
                  onChanged: (v) => setLocal(() => type = v!),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: opening,
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  decoration: const InputDecoration(
                    labelText: 'Opening balance',
                    helperText:
                        'Recorded once in the ledger and cannot be edited later.',
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: notes,
                  decoration: const InputDecoration(labelText: 'Notes'),
                ),
                SwitchListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('Active'),
                  value: active,
                  onChanged: (v) => setLocal(() => active = v),
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
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Create'),
            ),
          ],
        ),
      ),
    );
    if (ok != true) return;
    if (name.text.trim().isEmpty || double.tryParse(opening.text) == null) {
      _message('Enter a valid account name and opening balance.');
      return;
    }
    await _perform(
      () => widget.authState.createFinancialAccount({
        'branchId': _branchId,
        'name': name.text.trim(),
        'accountType': type,
        'openingBalance': double.parse(opening.text),
        'notes': notes.text.trim().isEmpty ? null : notes.text.trim(),
        'isActive': active,
      }),
    );
  }

  Future<void> _addExpense() async {
    final categories = await widget.authState.listExpenseCategories();
    if (!mounted) return;
    if (_accounts.where((a) => a.isActive).isEmpty || categories.isEmpty) {
      _message('An active account and expense category are required.');
      return;
    }
    var account = _accounts.firstWhere((a) => a.isActive),
        category = categories.first;
    final amount = TextEditingController(),
        description = TextEditingController(),
        payee = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setLocal) => AlertDialog(
          title: const Text('Post expense'),
          content: SizedBox(
            width: 480,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                DropdownButtonFormField(
                  initialValue: account.id,
                  decoration: InputDecoration(
                    labelText: 'Financial account',
                    helperText:
                        'Current balance: ${_money(account.currentBalance)}',
                  ),
                  items: _accounts
                      .where((a) => a.isActive)
                      .map(
                        (a) =>
                            DropdownMenuItem(value: a.id, child: Text(a.name)),
                      )
                      .toList(),
                  onChanged: (v) => setLocal(
                    () => account = _accounts.firstWhere((a) => a.id == v),
                  ),
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField(
                  initialValue: category.id,
                  decoration: const InputDecoration(labelText: 'Category'),
                  items: categories
                      .map(
                        (c) =>
                            DropdownMenuItem(value: c.id, child: Text(c.name)),
                      )
                      .toList(),
                  onChanged: (v) =>
                      category = categories.firstWhere((c) => c.id == v),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: amount,
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  decoration: const InputDecoration(labelText: 'Amount *'),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: description,
                  decoration: const InputDecoration(labelText: 'Description *'),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: payee,
                  decoration: const InputDecoration(labelText: 'Payee'),
                ),
                const SizedBox(height: 12),
                const Text(
                  'Posting reduces the selected account balance and cannot be edited afterward.',
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
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Post'),
            ),
          ],
        ),
      ),
    );
    if (ok != true) return;
    final value = double.tryParse(amount.text);
    if (value == null || value <= 0 || description.text.trim().isEmpty) {
      _message('Enter a positive amount and description.');
      return;
    }
    await _perform(
      () => widget.authState.postExpense({
        'branchId': _branchId,
        'expenseCategoryId': category.id,
        'financialAccountId': account.id,
        'expenseDateUtc': DateTime.now().toUtc().toIso8601String(),
        'amount': value,
        'description': description.text.trim(),
        'payee': payee.text.trim().isEmpty ? null : payee.text.trim(),
      }),
    );
  }

  Future<void> _otherIncome() async => _moneyDialog(
    'Post other income',
    'Income increases the selected account balance.',
    (account, amount, description) => widget.authState.postOtherIncome({
      'branchId': _branchId,
      'financialAccountId': account.id,
      'occurredAtUtc': DateTime.now().toUtc().toIso8601String(),
      'amount': amount,
      'description': description,
    }),
  );
  Future<void> _transfer() async {
    final active = _accounts.where((x) => x.isActive).toList();
    if (active.length < 2) {
      _message('Two active accounts are required.');
      return;
    }
    var source = active[0], destination = active[1];
    final amount = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setLocal) => AlertDialog(
          title: const Text('Transfer funds'),
          content: SizedBox(
            width: 450,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                DropdownButtonFormField(
                  initialValue: source.id,
                  decoration: InputDecoration(
                    labelText: 'Source account',
                    helperText: 'Available: ${_money(source.currentBalance)}',
                  ),
                  items: active
                      .map(
                        (a) =>
                            DropdownMenuItem(value: a.id, child: Text(a.name)),
                      )
                      .toList(),
                  onChanged: (v) => setLocal(
                    () => source = active.firstWhere((a) => a.id == v),
                  ),
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField(
                  initialValue: destination.id,
                  decoration: const InputDecoration(
                    labelText: 'Destination account',
                  ),
                  items: active
                      .map(
                        (a) =>
                            DropdownMenuItem(value: a.id, child: Text(a.name)),
                      )
                      .toList(),
                  onChanged: (v) => setLocal(
                    () => destination = active.firstWhere((a) => a.id == v),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: amount,
                  decoration: const InputDecoration(labelText: 'Amount *'),
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
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
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Transfer'),
            ),
          ],
        ),
      ),
    );
    if (ok != true) return;
    final value = double.tryParse(amount.text);
    if (source.id == destination.id || value == null || value <= 0) {
      _message('Select different accounts and enter a positive amount.');
      return;
    }
    await _perform(
      () => widget.authState.postFinancialTransfer({
        'branchId': _branchId,
        'sourceAccountId': source.id,
        'destinationAccountId': destination.id,
        'occurredAtUtc': DateTime.now().toUtc().toIso8601String(),
        'amount': value,
      }),
    );
  }

  Future<void> _moneyDialog(
    String title,
    String guidance,
    Future<void> Function(FinancialAccountInfo, double, String) submit,
  ) async {
    final active = _accounts.where((x) => x.isActive).toList();
    if (active.isEmpty) {
      _message('An active account is required.');
      return;
    }
    var account = active.first;
    final amount = TextEditingController(),
        description = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setLocal) => AlertDialog(
          title: Text(title),
          content: SizedBox(
            width: 450,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                DropdownButtonFormField(
                  initialValue: account.id,
                  decoration: const InputDecoration(labelText: 'Account'),
                  items: active
                      .map(
                        (a) =>
                            DropdownMenuItem(value: a.id, child: Text(a.name)),
                      )
                      .toList(),
                  onChanged: (v) => setLocal(
                    () => account = active.firstWhere((a) => a.id == v),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: amount,
                  decoration: const InputDecoration(labelText: 'Amount *'),
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: description,
                  decoration: const InputDecoration(labelText: 'Description *'),
                ),
                const SizedBox(height: 12),
                Text(guidance),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Post'),
            ),
          ],
        ),
      ),
    );
    if (ok != true) return;
    final value = double.tryParse(amount.text);
    if (value == null || value <= 0 || description.text.trim().isEmpty) {
      _message('Enter a positive amount and description.');
      return;
    }
    await _perform(() => submit(account, value, description.text.trim()));
  }

  Future<void> _ledger(FinancialAccountInfo account) async {
    try {
      final entries = await widget.authState.financialLedger(account.id);
      if (!mounted) return;
      await showDialog<void>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text('${account.name} ledger'),
          content: SizedBox(
            width: 850,
            height: 460,
            child: entries.isEmpty
                ? const Center(child: Text('No ledger entries'))
                : SingleChildScrollView(
                    child: DataTable(
                      columns: const [
                        DataColumn(label: Text('Date/time')),
                        DataColumn(label: Text('Type')),
                        DataColumn(label: Text('Description')),
                        DataColumn(label: Text('In')),
                        DataColumn(label: Text('Out')),
                        DataColumn(label: Text('Balance')),
                      ],
                      rows: entries
                          .map(
                            (e) => DataRow(
                              cells: [
                                DataCell(Text(_date(e.occurredAtUtc))),
                                DataCell(Text(e.entryType)),
                                DataCell(Text(e.description)),
                                DataCell(
                                  Text(e.amount > 0 ? _money(e.amount) : '-'),
                                ),
                                DataCell(
                                  Text(e.amount < 0 ? _money(-e.amount) : '-'),
                                ),
                                DataCell(Text(_money(e.runningBalance))),
                              ],
                            ),
                          )
                          .toList(),
                    ),
                  ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Close'),
            ),
          ],
        ),
      );
    } on ApiException catch (e) {
      _message(e.message);
    }
  }

  Future<void> _perform(Future<dynamic> Function() action) async {
    try {
      await action();
      await _load();
      if (mounted) _message('Posted successfully.');
    } on ApiException catch (e) {
      _message(e.message);
    }
  }

  void _message(String text) {
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));
    }
  }

  String _money(double value) => 'Rs. ${value.toStringAsFixed(2)}';
  String _date(DateTime value) =>
      '${value.year}-${value.month.toString().padLeft(2, '0')}-${value.day.toString().padLeft(2, '0')}';
}
