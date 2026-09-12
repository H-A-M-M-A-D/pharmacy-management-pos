import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';
import '../pricing/price_source_label.dart';

class PosScreen extends StatefulWidget {
  const PosScreen({required this.authState, super.key});
  final AuthState authState;

  @override
  State<PosScreen> createState() => _PosScreenState();
}

class _PosScreenState extends State<PosScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs = TabController(length: 3, vsync: this);
  final _search = TextEditingController();
  final _customer = TextEditingController();
  final _phone = TextEditingController();
  final List<_CartLine> _cart = [];
  CustomerLookup? _selectedCustomer;
  List<CustomerLookup> _customerMatches = [];
  List<PosProduct> _products = [];
  List<GodownLookup> _godowns = [];
  String? _godownId;
  PagedSales? _history;
  PagedSales? _held;
  SaleDetails? _receipt;
  PagedSalesReturns? _returns;
  SalesReturnDetails? _returnReceipt;
  bool _loading = false;
  String? _error;

  bool can(String permission) => widget.authState.can(permission);
  double get _subtotal => _cart.fold(0, (sum, x) => sum + x.gross);
  double get _discount => _cart.fold(0, (sum, x) => sum + x.discountAmount);
  double get _total => _cart.fold(0, (sum, x) => sum + x.net);

  @override
  void initState() {
    super.initState();
    _loadHistory();
    _loadGodowns();
  }

  Future<void> _loadGodowns() async {
    try {
      final godowns = await widget.authState.myGodowns();
      if (!mounted) return;
      setState(() {
        _godowns = godowns;
        _godownId = godowns.isEmpty
            ? null
            : godowns
                  .firstWhere((g) => g.isDefault, orElse: () => godowns.first)
                  .id;
      });
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    }
  }

  @override
  void dispose() {
    _tabs.dispose();
    _search.dispose();
    _customer.dispose();
    _phone.dispose();
    super.dispose();
  }

  Future<void> _searchProducts() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final products = await widget.authState.searchPosProducts(
        query: _search.text,
        godownId: _godownId,
      );
      if (mounted) {
        setState(() => _products = products);
      }
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _loadHistory() async {
    if (!can('sales.view') &&
        !can('sales.hold') &&
        !can('sales.returns.view')) {
      return;
    }
    setState(() => _loading = true);
    try {
      final history = can('sales.view')
          ? await widget.authState.listSales()
          : null;
      final held = can('sales.hold')
          ? await widget.authState.listHeldSales()
          : null;
      final returns = can('sales.returns.view')
          ? await widget.authState.listSalesReturns()
          : null;
      final customers = can('customers.view')
          ? await widget.authState.lookupCustomers()
          : const <CustomerLookup>[];
      if (mounted) {
        setState(() {
          _history = history;
          _held = held;
          _returns = returns;
          _customerMatches = customers;
        });
      }
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _addProduct(PosProduct product) {
    final existing = _cart
        .where((x) => x.product.productId == product.productId)
        .firstOrNull;
    setState(() {
      if (existing == null) {
        _cart.add(_CartLine(product));
      } else if (existing.quantity < product.availableQuantity) {
        existing.quantity++;
      }
    });
    final line = _cart
        .where((x) => x.product.productId == product.productId)
        .firstOrNull;
    if (line != null) _resolvePrice(line);
  }

  Future<void> _resolvePrice(_CartLine line) async {
    final revision = ++line.priceRevision;
    try {
      final data = await widget.authState.phase6(
        'sale-price',
        query: {
          'productId': line.product.productId,
          'quantity': '${line.quantity}',
          'saleType': 'Retail',
          if (_selectedCustomer != null) 'customerId': _selectedCustomer!.id,
        },
      );
      if (mounted && revision == line.priceRevision && _cart.contains(line)) {
        setState(() {
          line.resolvedPrice = (data['price'] as num?)?.toDouble();
          line.priceSource = data['source'] as String? ?? 'Default';
        });
      }
    } on ApiException catch (error) {
      if (mounted && revision == line.priceRevision) {
        setState(() => _error = error.message);
      }
    }
  }

  void _changeQuantity(_CartLine line, int quantity) {
    setState(
      () => line.quantity = quantity.clamp(
        1,
        line.product.availableQuantity > 0 ? line.product.availableQuantity : 1,
      ),
    );
    _resolvePrice(line);
  }

  Map<String, dynamic> _saleBody({
    required List<Map<String, dynamic>> payments,
  }) => {
    'branchId': widget.authState.currentUser?.branch.id,
    'godownId': _godownId,
    'customerId': _selectedCustomer?.id,
    'customerName': _emptyToNull(_customer.text),
    'customerPhone': _emptyToNull(_phone.text),
    'items': _cart
        .map(
          (line) => {
            'productId': line.product.productId,
            'quantity': line.quantity,
            'discountPercent': line.discountPercent,
          },
        )
        .toList(),
    'payments': payments,
  };

  Future<void> _hold() async {
    if (_cart.isEmpty) {
      setState(() => _error = 'Add at least one product.');
      return;
    }
    setState(() => _loading = true);
    try {
      _receipt = await widget.authState.holdSale({
        'branchId': widget.authState.currentUser?.branch.id,
        'godownId': _godownId,
        'customerName': _emptyToNull(_customer.text),
        'customerPhone': _emptyToNull(_phone.text),
        'items': _cart
            .map(
              (line) => {
                'productId': line.product.productId,
                'quantity': line.quantity,
                'discountPercent': line.discountPercent,
              },
            )
            .toList(),
      });
      _cart.clear();
      await _loadHistory();
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _checkout() async {
    if (_cart.isEmpty) {
      setState(() => _error = 'Add at least one product.');
      return;
    }
    List<FinancialAccountInfo> accounts;
    try {
      accounts = await widget.authState.listFinancialAccounts(
        branchId: widget.authState.currentUser!.branch.id,
      );
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
      return;
    }
    if (!mounted) return;
    final payments = await showDialog<List<Map<String, dynamic>>>(
      context: context,
      builder: (_) => _PaymentDialog(
        total: _total,
        creditAllowed: can('sales.credit') && _selectedCustomer != null,
        customerName: _selectedCustomer?.name,
        accounts: accounts.where((x) => x.isActive).toList(),
      ),
    );
    if (payments == null) return;
    setState(() => _loading = true);
    try {
      final sale = await widget.authState.postSale(
        _saleBody(payments: payments),
      );
      if (mounted) {
        setState(() {
          _receipt = sale;
          _cart.clear();
          _products = [];
          _search.clear();
          _selectedCustomer = null;
          _customer.clear();
          _phone.clear();
        });
      }
      await _loadHistory();
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _showReceipt(SaleListItem sale, {bool reprint = false}) async {
    setState(() => _loading = true);
    try {
      final receipt = reprint
          ? await widget.authState.reprintSaleReceipt(sale.id)
          : await widget.authState.saleReceipt(sale.id);
      if (mounted) setState(() => _receipt = receipt);
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
                  'POS & Sales',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              IconButton.filledTonal(
                onPressed: _loadHistory,
                tooltip: 'Refresh',
                icon: const Icon(Icons.refresh),
              ),
            ],
          ),
        ),
        TabBar(
          controller: _tabs,
          tabs: const [
            Tab(text: 'POS'),
            Tab(text: 'Sales History'),
            Tab(text: 'Sales Returns'),
          ],
        ),
        if (_error != null)
          Padding(
            padding: const EdgeInsets.all(8),
            child: Text(
              _error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ),
        Expanded(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : TabBarView(
                  controller: _tabs,
                  children: [_pos(), _historyView(), _returnsView()],
                ),
        ),
      ],
    ),
  );

  Widget _godownSelector() {
    if (_godowns.isEmpty) {
      return Container(
        width: double.infinity,
        margin: const EdgeInsets.only(bottom: 12),
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.errorContainer,
          borderRadius: BorderRadius.circular(8),
        ),
        child: Text(
          'No godown is assigned to you for this branch. Ask an administrator to grant godown access before selling.',
          style: TextStyle(
            color: Theme.of(context).colorScheme.onErrorContainer,
          ),
        ),
      );
    }
    if (_godowns.length == 1) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: DropdownButtonFormField<String>(
        key: const Key('pos_godown'),
        initialValue: _godownId,
        decoration: const InputDecoration(labelText: 'Selling from godown'),
        items: _godowns
            .map((g) => DropdownMenuItem(value: g.id, child: Text(g.name)))
            .toList(),
        onChanged: (v) => setState(() {
          _godownId = v;
          _products = [];
        }),
      ),
    );
  }

  Widget _pos() => Row(
    children: [
      Expanded(
        flex: 3,
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            children: [
              _godownSelector(),
              TextField(
                key: const Key('pos_search'),
                controller: _search,
                decoration: InputDecoration(
                  labelText: 'Search or scan barcode',
                  suffixIcon: IconButton(
                    tooltip: 'Search',
                    onPressed: _searchProducts,
                    icon: const Icon(Icons.search),
                  ),
                ),
                onSubmitted: (_) async {
                  await _searchProducts();
                  if (_products.length == 1) _addProduct(_products.single);
                },
              ),
              const SizedBox(height: 12),
              Expanded(child: _productsTable()),
            ],
          ),
        ),
      ),
      const VerticalDivider(width: 1),
      Expanded(flex: 4, child: _cartPanel()),
    ],
  );

  Future<void> _startReturn(SaleListItem sale) async {
    setState(() => _loading = true);
    ReturnableSale returnable;
    try {
      returnable = await widget.authState.returnableSale(sale.id);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
      return;
    } finally {
      if (mounted) setState(() => _loading = false);
    }
    if (!mounted) return;
    final result = await showDialog<SalesReturnDetails>(
      context: context,
      builder: (_) => _SalesReturnDialog(
        authState: widget.authState,
        returnable: returnable,
      ),
    );
    if (result != null && mounted) {
      setState(() => _returnReceipt = result);
      await _loadHistory();
    }
  }

  Future<void> _showReturnReceipt(SalesReturnListItem item) async {
    setState(() => _loading = true);
    try {
      final receipt = await widget.authState.salesReturnReceipt(item.id);
      if (mounted) setState(() => _returnReceipt = receipt);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Widget _returnsView() {
    if (!can('sales.returns.view')) {
      return const Center(child: Text('Not permitted'));
    }
    final returns = _returns?.items ?? const <SalesReturnListItem>[];
    return Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Sales Returns', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 12),
          Expanded(
            child: returns.isEmpty
                ? const Center(child: Text('No returns found'))
                : SingleChildScrollView(
                    scrollDirection: Axis.horizontal,
                    child: DataTable(
                      columns: const [
                        DataColumn(label: Text('Return #')),
                        DataColumn(label: Text('Original Invoice')),
                        DataColumn(label: Text('Customer')),
                        DataColumn(label: Text('Items')),
                        DataColumn(label: Text('Refund')),
                        DataColumn(label: Text('Processed By')),
                        DataColumn(label: Text('Actions')),
                      ],
                      rows: returns
                          .map(
                            (item) => DataRow(
                              cells: [
                                DataCell(Text(item.returnNumber)),
                                DataCell(Text(item.originalInvoiceNumber)),
                                DataCell(Text(item.customerName ?? '-')),
                                DataCell(Text('${item.itemCount}')),
                                DataCell(Text(_money(item.refundAmount))),
                                DataCell(Text(item.processedByName)),
                                DataCell(
                                  IconButton(
                                    tooltip: 'Return receipt',
                                    onPressed: () => _showReturnReceipt(item),
                                    icon: const Icon(Icons.receipt_long),
                                  ),
                                ),
                              ],
                            ),
                          )
                          .toList(),
                    ),
                  ),
          ),
          if (_returnReceipt != null) _returnReceiptPanel(_returnReceipt!),
        ],
      ),
    );
  }

  Widget _returnReceiptPanel(SalesReturnDetails item) => Padding(
    padding: const EdgeInsets.only(top: 12),
    child: Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'SALES RETURN ${item.returnNumber}',
              key: const Key('return_receipt_preview'),
              style: Theme.of(context).textTheme.titleMedium,
            ),
            Text('Original ${item.originalInvoiceNumber}  ${item.branchName}'),
            Text('Refund ${_money(item.refundAmount)}  Reason ${item.reason}'),
          ],
        ),
      ),
    ),
  );
  Widget _productsTable() {
    if (_products.isEmpty) {
      return const Center(child: Text('No products loaded'));
    }
    return SingleChildScrollView(
      child: DataTable(
        columns: const [
          DataColumn(label: Text('Product')),
          DataColumn(label: Text('SKU')),
          DataColumn(label: Text('Stock')),
          DataColumn(label: Text('Price')),
          DataColumn(label: Text('')),
        ],
        rows: _products
            .map(
              (p) => DataRow(
                cells: [
                  DataCell(Text(p.name)),
                  DataCell(Text(p.sku)),
                  DataCell(Text('${p.availableQuantity}')),
                  DataCell(Text(_money(p.indicativeRetailPrice ?? 0))),
                  DataCell(
                    IconButton(
                      tooltip: 'Add',
                      onPressed: () => _addProduct(p),
                      icon: const Icon(Icons.add_shopping_cart),
                    ),
                  ),
                ],
              ),
            )
            .toList(),
      ),
    );
  }

  Widget _cartPanel() => Padding(
    padding: const EdgeInsets.all(20),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Cart', style: Theme.of(context).textTheme.titleLarge),
        _customerSelector(),
        const SizedBox(height: 12),
        Expanded(child: _cartTable()),
        const Divider(),
        Row(
          children: [
            Expanded(child: Text('Subtotal ${_money(_subtotal)}')),
            Expanded(child: Text('Discount ${_money(_discount)}')),
            Expanded(
              child: Text(
                'Total ${_money(_total)}',
                key: const Key('cart_total'),
              ),
            ),
          ],
        ),
        if (_selectedCustomer != null)
          Text(
            'Customer credit available ${_money(_selectedCustomer!.availableCredit)}',
            key: const Key('selected_customer_credit'),
          ),
        const SizedBox(height: 12),
        Row(
          children: [
            if (can('sales.hold'))
              FilledButton.tonalIcon(
                key: const Key('hold_sale'),
                onPressed: _hold,
                icon: const Icon(Icons.pause_circle_outline),
                label: const Text('Hold'),
              ),
            const Spacer(),
            FilledButton.icon(
              key: const Key('checkout_sale'),
              onPressed: _checkout,
              icon: const Icon(Icons.point_of_sale),
              label: const Text('Checkout'),
            ),
          ],
        ),
        if (_receipt != null) _receiptPanel(_receipt!),
      ],
    ),
  );

  Widget _cartTable() {
    if (_cart.isEmpty) return const Center(child: Text('Cart is empty'));
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: DataTable(
        columns: [
          const DataColumn(label: Text('Product')),
          const DataColumn(label: Text('Qty')),
          const DataColumn(label: Text('Price')),
          if (can('sales.discount'))
            const DataColumn(label: Text('Discount %')),
          const DataColumn(label: Text('Net')),
          const DataColumn(label: Text('')),
        ],
        rows: _cart
            .map(
              (line) => DataRow(
                cells: [
                  DataCell(Text(line.product.name)),
                  DataCell(
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        IconButton(
                          tooltip: 'Decrease',
                          onPressed: () =>
                              _changeQuantity(line, line.quantity - 1),
                          icon: const Icon(Icons.remove),
                        ),
                        Text('${line.quantity}'),
                        IconButton(
                          tooltip: 'Increase',
                          onPressed: () =>
                              _changeQuantity(line, line.quantity + 1),
                          icon: const Icon(Icons.add),
                        ),
                      ],
                    ),
                  ),
                  DataCell(
                    Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(_money(line.price)),
                        Text(
                          priceSourceLabel(line.priceSource),
                          style: Theme.of(context).textTheme.labelSmall,
                        ),
                      ],
                    ),
                  ),
                  if (can('sales.discount'))
                    DataCell(
                      SizedBox(
                        width: 90,
                        child: TextFormField(
                          key: const Key('line_discount'),
                          initialValue: '${line.discountPercent}',
                          keyboardType: TextInputType.number,
                          onChanged: (value) => setState(
                            () => line.discountPercent =
                                (double.tryParse(value) ?? 0)
                                    .clamp(
                                      0,
                                      line.product.maximumDiscountPercent,
                                    )
                                    .toDouble(),
                          ),
                        ),
                      ),
                    ),
                  DataCell(Text(_money(line.net))),
                  DataCell(
                    IconButton(
                      tooltip: 'Remove',
                      onPressed: () => setState(() => _cart.remove(line)),
                      icon: const Icon(Icons.delete_outline),
                    ),
                  ),
                ],
              ),
            )
            .toList(),
      ),
    );
  }

  Widget _customerSelector() => Column(
    children: [
      Row(
        children: [
          Expanded(
            child: TextField(
              key: const Key('pos_customer_search'),
              controller: _customer,
              decoration: InputDecoration(
                labelText: 'Customer',
                suffixIcon: IconButton(
                  tooltip: 'Search customers',
                  onPressed: _searchCustomers,
                  icon: const Icon(Icons.person_search_outlined),
                ),
              ),
              onSubmitted: (_) => _searchCustomers(),
              onChanged: (_) { setState(() => _selectedCustomer = null); for (final line in _cart) { _resolvePrice(line); } },
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: TextField(
              controller: _phone,
              decoration: const InputDecoration(labelText: 'Phone'),
            ),
          ),
        ],
      ),
      if (_customerMatches.isNotEmpty && _selectedCustomer == null)
        SizedBox(
          height: 42,
          child: ListView(
            scrollDirection: Axis.horizontal,
            children: _customerMatches.take(5).map((customer) {
              return Padding(
                padding: const EdgeInsets.only(right: 8),
                child: ActionChip(
                  key: Key('select_customer_${customer.customerCode}'),
                  avatar: const Icon(Icons.person_outline, size: 18),
                  label: Text('${customer.customerCode} ${customer.name}'),
                  onPressed: () { setState(() {
                    _selectedCustomer = customer;
                    _customer.text = customer.name;
                    _phone.text = customer.phoneNumber ?? '';
                  }); for (final line in _cart) { _resolvePrice(line); } },
                ),
              );
            }).toList(),
          ),
        ),
    ],
  );

  Future<void> _searchCustomers() async {
    if (!can('customers.view')) return;
    try {
      final customers = await widget.authState.lookupCustomers(
        search: _customer.text,
      );
      if (mounted) setState(() => _customerMatches = customers);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    }
  }

  Widget _historyView() => Padding(
    padding: const EdgeInsets.all(20),
    child: Column(
      children: [
        if (can('sales.hold')) _heldTable(),
        const SizedBox(height: 16),
        Expanded(child: _salesTable()),
        if (_returnReceipt != null) _returnReceiptPanel(_returnReceipt!),
      ],
    ),
  );

  Widget _heldTable() {
    final held = _held?.items ?? const <SaleListItem>[];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Held Sales', style: Theme.of(context).textTheme.titleMedium),
        if (held.isEmpty)
          const Text('No held sales')
        else
          Wrap(
            spacing: 8,
            children: held
                .map(
                  (x) => ActionChip(
                    label: Text(x.holdNumber ?? 'Held'),
                    onPressed: () {},
                  ),
                )
                .toList(),
          ),
      ],
    );
  }

  Widget _salesTable() {
    final sales = _history?.items ?? const <SaleListItem>[];
    if (sales.isEmpty) return const Center(child: Text('No sales found'));
    return SingleChildScrollView(
      child: DataTable(
        columnSpacing: 8,
        horizontalMargin: 8,
        columns: const [
          DataColumn(label: Text('Invoice')),
          DataColumn(label: Text('Cashier')),
          DataColumn(label: Text('Items')),
          DataColumn(label: Text('Return')),
          DataColumn(label: Text('Actions')),
          DataColumn(label: Text('Total')),
          DataColumn(label: Text('Credit')),
          DataColumn(label: Text('Payment')),
        ],
        rows: sales
            .map(
              (sale) => DataRow(
                cells: [
                  DataCell(Text(sale.invoiceNumber ?? sale.holdNumber ?? '-')),
                  DataCell(Text(sale.cashierName)),
                  DataCell(Text('${sale.itemCount}')),
                  DataCell(
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Tooltip(
                          message: _returnLabel(sale.returnState),
                          child: const Icon(
                            Icons.assignment_turned_in_outlined,
                          ),
                        ),
                        if (can('sales.returns.create') &&
                            sale.status == 'Posted' &&
                            sale.returnState != 'FullyReturned')
                          IconButton(
                            tooltip: 'Return items',
                            onPressed: () => _startReturn(sale),
                            icon: const Icon(Icons.assignment_return_outlined),
                          ),
                      ],
                    ),
                  ),
                  DataCell(
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        IconButton(
                          tooltip: 'Receipt',
                          onPressed: () => _showReceipt(sale),
                          icon: const Icon(Icons.receipt_long_outlined),
                        ),
                        if (can('sales.reprint'))
                          IconButton(
                            tooltip: 'Reprint receipt',
                            onPressed: () => _showReceipt(sale, reprint: true),
                            icon: const Icon(Icons.print_outlined),
                          ),
                      ],
                    ),
                  ),
                  DataCell(Text(_money(sale.netTotal))),
                  DataCell(Text(_money(sale.creditAmount))),
                  DataCell(Text(sale.paymentSummary)),
                ],
              ),
            )
            .toList(),
      ),
    );
  }

  Widget _receiptPanel(SaleDetails sale) => Padding(
    padding: const EdgeInsets.only(top: 12),
    child: Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Receipt ${sale.invoiceNumber ?? sale.holdNumber ?? ''}',
              key: const Key('receipt_preview'),
              style: Theme.of(context).textTheme.titleMedium,
            ),
            Text('${sale.branchName}  ${sale.cashierName}'),
            Text(
              'Total ${_money(sale.netTotal)}  Paid ${_money(sale.amountPaid)}  Change ${_money(sale.changeGiven)}',
            ),
              if (sale.creditAmount > 0)
                Text('Credit ${_money(sale.creditAmount)}'),
              for (final item in sale.items.where((item) => item.priceSource != 'Default')) Text('${item.productName}: ${priceSourceLabel(item.priceSource)}'),
          ],
        ),
      ),
    ),
  );
}

class _PaymentDialog extends StatefulWidget {
  const _PaymentDialog({
    required this.total,
    required this.creditAllowed,
    this.customerName,
    required this.accounts,
  });
  final double total;
  final bool creditAllowed;
  final String? customerName;
  final List<FinancialAccountInfo> accounts;

  @override
  State<_PaymentDialog> createState() => _PaymentDialogState();
}

class _PaymentDialogState extends State<_PaymentDialog> {
  final _cashApplied = TextEditingController();
  final _cashTendered = TextEditingController();
  final _cardApplied = TextEditingController();
  String? _error;
  String? _cashAccountId;
  String? _cardAccountId;

  double get _applied =>
      (double.tryParse(_cashApplied.text) ?? 0) +
      (double.tryParse(_cardApplied.text) ?? 0);
  double get _creditAmount =>
      (widget.total - _applied).clamp(0, widget.total).toDouble();

  @override
  void initState() {
    super.initState();
    final cash = widget.accounts.where((x) => x.accountType == 'Cash');
    final card = widget.accounts.where(
      (x) => x.accountType == 'CardSettlement',
    );
    _cashAccountId = cash.isEmpty ? null : cash.first.id;
    _cardAccountId = card.isEmpty ? null : card.first.id;
    if (widget.creditAllowed) {
      _cashApplied.text = '0.00';
      _cashTendered.text = '0.00';
    } else {
      _cashApplied.text = widget.total.toStringAsFixed(2);
      _cashTendered.text = widget.total.toStringAsFixed(2);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Payment'),
    content: SizedBox(
      width: 420,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text('Due ${_money(widget.total)}'),
          if (widget.creditAllowed)
            Text('Credit customer ${widget.customerName ?? ''}'),
          TextField(
            key: const Key('cash_applied'),
            controller: _cashApplied,
            decoration: const InputDecoration(labelText: 'Cash applied'),
            onChanged: (_) => setState(() {}),
          ),
          DropdownButtonFormField<String>(
            initialValue: _cashAccountId,
            decoration: const InputDecoration(labelText: 'Cash account'),
            items: widget.accounts
                .map((a) => DropdownMenuItem(value: a.id, child: Text(a.name)))
                .toList(),
            onChanged: (value) => setState(() => _cashAccountId = value),
          ),
          TextField(
            key: const Key('cash_tendered'),
            controller: _cashTendered,
            decoration: const InputDecoration(labelText: 'Cash tendered'),
          ),
          DropdownButtonFormField<String>(
            initialValue: _cardAccountId,
            decoration: const InputDecoration(
              labelText: 'Card settlement account',
            ),
            items: widget.accounts
                .map((a) => DropdownMenuItem(value: a.id, child: Text(a.name)))
                .toList(),
            onChanged: (value) => setState(() => _cardAccountId = value),
          ),
          TextField(
            key: const Key('card_applied'),
            controller: _cardApplied,
            decoration: const InputDecoration(labelText: 'Card amount'),
            onChanged: (_) => setState(() {}),
          ),
          if (widget.creditAllowed)
            Text('Credit amount ${_money(_creditAmount)}'),
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
        onPressed: () => Navigator.pop(context),
        child: const Text('Close'),
      ),
      FilledButton(
        key: const Key('confirm_payment'),
        onPressed: _confirm,
        child: const Text('Post Sale'),
      ),
    ],
  );

  void _confirm() {
    final cash = double.tryParse(_cashApplied.text) ?? 0;
    final tendered = double.tryParse(_cashTendered.text) ?? 0;
    final card = double.tryParse(_cardApplied.text) ?? 0;
    final applied = cash + card;
    if (cash < 0 || tendered < 0 || card < 0) {
      setState(() => _error = 'Payment amounts cannot be negative.');
      return;
    }
    if (widget.creditAllowed) {
      if (applied - widget.total > 0.009) {
        setState(() => _error = 'Payment total cannot exceed sale total.');
        return;
      }
    } else if ((applied - widget.total).abs() > 0.009) {
      setState(() => _error = 'Payment total must equal sale total.');
      return;
    }
    if (cash > 0 && tendered < cash) {
      setState(
        () => _error = 'Cash tendered cannot be less than cash applied.',
      );
      return;
    }
    if ((cash > 0 && _cashAccountId == null) ||
        (card > 0 && _cardAccountId == null)) {
      setState(() => _error = 'Select a financial account for each payment.');
      return;
    }
    final payments = <Map<String, dynamic>>[];
    if (cash > 0) {
      payments.add({
        'method': 1,
        'amountApplied': cash,
        'tenderedAmount': tendered,
        'financialAccountId': _cashAccountId,
      });
    }
    if (card > 0) {
      payments.add({
        'method': 2,
        'amountApplied': card,
        'financialAccountId': _cardAccountId,
      });
    }
    Navigator.pop(context, payments);
  }
}

class _CartLine {
  _CartLine(this.product);
  final PosProduct product;
  int quantity = 1;
  double discountPercent = 0;
  double? resolvedPrice;
  String priceSource = 'Default';
  int priceRevision = 0;
  double get price => resolvedPrice ?? product.indicativeRetailPrice ?? 0;
  double get gross => price * quantity;
  double get discountAmount => gross * discountPercent / 100;
  double get net => gross - discountAmount;
}

String? _emptyToNull(String value) =>
    value.trim().isEmpty ? null : value.trim();
String _money(double value) => 'PKR ${value.toStringAsFixed(2)}';

class _SalesReturnDialog extends StatefulWidget {
  const _SalesReturnDialog({required this.authState, required this.returnable});
  final AuthState authState;
  final ReturnableSale returnable;

  @override
  State<_SalesReturnDialog> createState() => _SalesReturnDialogState();
}

class _SalesReturnDialogState extends State<_SalesReturnDialog> {
  final Map<String, TextEditingController> _quantities = {};
  final Map<String, String> _dispositions = {};
  final _notes = TextEditingController();
  final _cash = TextEditingController();
  String _reason = 'CustomerReturn';
  String? _error;
  bool _posting = false;
  List<FinancialAccountInfo> _refundAccounts = [];
  String? _refundAccountId;

  @override
  void initState() {
    super.initState();
    for (final item in widget.returnable.items) {
      for (final allocation in item.allocations.where(
        (x) => x.remainingQuantity > 0,
      )) {
        _quantities[allocation.allocationId] = TextEditingController(
          text: item.allocations.length == 1
              ? '${allocation.remainingQuantity}'
              : '0',
        );
        _dispositions[allocation.allocationId] = allocation.isBatchDisposed
            ? 'NonResellable'
            : 'Restockable';
      }
    }
    _updateCash();
    _loadRefundAccounts();
  }

  Future<void> _loadRefundAccounts() async {
    try {
      final accounts = await widget.authState.listFinancialAccounts(
        branchId: widget.authState.currentUser!.branch.id,
      );
      final active = accounts.where((x) => x.isActive).toList();
      if (mounted) {
        setState(() {
          _refundAccounts = active;
          _refundAccountId =
              active.where((x) => x.accountType == 'Cash').firstOrNull?.id ??
              active.firstOrNull?.id;
        });
      }
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    }
  }

  @override
  void dispose() {
    for (final controller in _quantities.values) {
      controller.dispose();
    }
    _notes.dispose();
    _cash.dispose();
    super.dispose();
  }

  double get _refundTotal {
    var total = 0.0;
    for (final item in widget.returnable.items) {
      for (final allocation in item.allocations) {
        final qty =
            int.tryParse(_quantities[allocation.allocationId]?.text ?? '') ?? 0;
        if (qty <= 0) continue;
        total += allocation.remainingQuantity == qty
            ? allocation.refundRemaining
            : allocation.refundRemaining * qty / allocation.remainingQuantity;
      }
    }
    return double.parse(total.toStringAsFixed(2));
  }

  bool get _hasCustomerCredit => widget.returnable.customerId != null;
  double get _defaultCashRefund => _hasCustomerCredit ? 0 : _refundTotal;

  void _updateCash() => _cash.text = _defaultCashRefund.toStringAsFixed(2);

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Sales Return / Refund'),
    content: SizedBox(
      width: 900,
      child: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('Original Invoice ${widget.returnable.invoiceNumber}'),
            Text(
              'Customer ${widget.returnable.customerName ?? '-'}  Net ${_money(widget.returnable.netTotal)}',
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              initialValue: _reason,
              decoration: const InputDecoration(labelText: 'Reason'),
              items: const [
                'CustomerReturn',
                'WrongItem',
                'Damaged',
                'QualityIssue',
                'Other',
              ].map((x) => DropdownMenuItem(value: x, child: Text(x))).toList(),
              onChanged: (value) => setState(() => _reason = value ?? _reason),
            ),
            TextField(
              controller: _notes,
              decoration: const InputDecoration(labelText: 'Notes'),
            ),
            const SizedBox(height: 12),
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: DataTable(
                columns: const [
                  DataColumn(label: Text('Product')),
                  DataColumn(label: Text('Batch')),
                  DataColumn(label: Text('Remaining')),
                  DataColumn(label: Text('Return Qty')),
                  DataColumn(label: Text('Disposition')),
                  DataColumn(label: Text('Refund')),
                ],
                rows: [
                  for (final item in widget.returnable.items)
                    for (final allocation in item.allocations)
                      DataRow(
                        cells: [
                          DataCell(Text(item.productName)),
                          DataCell(
                            Text(
                              '${allocation.batchNumber}${allocation.isBatchExpired ? ' expired' : ''}',
                            ),
                          ),
                          DataCell(Text('${allocation.remainingQuantity}')),
                          DataCell(
                            SizedBox(
                              width: 80,
                              child: TextField(
                                key: Key(
                                  'return_qty_${allocation.allocationId}',
                                ),
                                controller:
                                    _quantities[allocation.allocationId],
                                keyboardType: TextInputType.number,
                                onChanged: (_) => setState(_updateCash),
                              ),
                            ),
                          ),
                          DataCell(
                            DropdownButton<String>(
                              value: _dispositions[allocation.allocationId],
                              items: [
                                if (!allocation.isBatchDisposed)
                                  const DropdownMenuItem(
                                    value: 'Restockable',
                                    child: Text('Restockable'),
                                  ),
                                const DropdownMenuItem(
                                  value: 'NonResellable',
                                  child: Text('Non-Resellable'),
                                ),
                              ],
                              onChanged: (value) => setState(
                                () => _dispositions[allocation.allocationId] =
                                    value ?? 'NonResellable',
                              ),
                            ),
                          ),
                          DataCell(Text(_money(allocation.refundRemaining))),
                        ],
                      ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            Text('Refund Total ${_money(_refundTotal)}'),
            if (_hasCustomerCredit)
              Text(
                'Credit sales reduce the customer ledger first. Enter only the cash refund portion.',
              ),
            TextField(
              key: const Key('return_cash_refund'),
              controller: _cash,
              decoration: const InputDecoration(labelText: 'Cash refund'),
            ),
            DropdownButtonFormField<String>(
              key: ValueKey(_refundAccountId),
              initialValue: _refundAccountId,
              decoration: const InputDecoration(
                labelText: 'Refund financial account',
              ),
              items: _refundAccounts
                  .map(
                    (a) => DropdownMenuItem(value: a.id, child: Text(a.name)),
                  )
                  .toList(),
              onChanged: (value) => setState(() => _refundAccountId = value),
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
        onPressed: _posting ? null : () => Navigator.pop(context),
        child: const Text('Close'),
      ),
      FilledButton.icon(
        key: const Key('post_return'),
        onPressed: _posting ? null : _post,
        icon: const Icon(Icons.assignment_return),
        label: const Text('Post Return'),
      ),
    ],
  );

  Future<void> _post() async {
    final allocations = <Map<String, dynamic>>[];
    for (final entry in _quantities.entries) {
      final qty = int.tryParse(entry.value.text) ?? 0;
      if (qty <= 0) continue;
      allocations.add({
        'originalAllocationId': entry.key,
        'quantity': qty,
        'disposition': _dispositions[entry.key] == 'Restockable' ? 1 : 2,
      });
    }
    if (allocations.isEmpty) {
      setState(() => _error = 'Select at least one return quantity.');
      return;
    }
    final cash = double.tryParse(_cash.text) ?? 0;
    if (cash < 0) {
      setState(() => _error = 'Cash refund cannot be negative.');
      return;
    }
    if (_hasCustomerCredit) {
      if (cash - _refundTotal > 0.009) {
        setState(() => _error = 'Cash refund cannot exceed return amount.');
        return;
      }
    } else if ((cash - _refundTotal).abs() > 0.009) {
      setState(() => _error = 'Cash refund must equal return amount.');
      return;
    }
    if (cash > 0 && _refundAccountId == null) {
      setState(
        () => _error = 'Select the financial account used for the refund.',
      );
      return;
    }
    if (_reason == 'Other' && _notes.text.trim().isEmpty) {
      setState(() => _error = 'Notes are required for Other.');
      return;
    }
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Confirm return'),
        content: const Text(
          'Posting this return will refund the customer and update inventory. It cannot be edited afterward.',
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
    );
    if (confirmed != true) return;
    setState(() => _posting = true);
    try {
      final result = await widget.authState.postSalesReturn(
        widget.returnable.saleId,
        {
          'reason': _reasonIndex(_reason),
          'notes': _emptyToNull(_notes.text),
          'allocations': allocations,
          'refundPayments': cash > 0
              ? [
                  {
                    'method': 1,
                    'amount': cash,
                    'financialAccountId': _refundAccountId,
                  },
                ]
              : <Map<String, dynamic>>[],
        },
      );
      if (mounted) Navigator.pop(context, result);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _posting = false);
    }
  }
}

int _reasonIndex(String reason) => switch (reason) {
  'CustomerReturn' => 1,
  'WrongItem' => 2,
  'Damaged' => 3,
  'QualityIssue' => 4,
  _ => 5,
};

String _returnLabel(String state) => switch (state) {
  'PartiallyReturned' => 'Partially Returned',
  'FullyReturned' => 'Fully Returned',
  _ => 'No Returns',
};
