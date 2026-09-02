import 'package:flutter/material.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

class PosScreen extends StatefulWidget {
  const PosScreen({required this.authState, super.key});
  final AuthState authState;

  @override
  State<PosScreen> createState() => _PosScreenState();
}

class _PosScreenState extends State<PosScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs = TabController(length: 2, vsync: this);
  final _search = TextEditingController();
  final _customer = TextEditingController();
  final _phone = TextEditingController();
  final List<_CartLine> _cart = [];
  List<PosProduct> _products = [];
  PagedSales? _history;
  PagedSales? _held;
  SaleDetails? _receipt;
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
    if (!can('sales.view') && !can('sales.hold')) return;
    setState(() => _loading = true);
    try {
      final history = can('sales.view')
          ? await widget.authState.listSales()
          : null;
      final held = can('sales.hold')
          ? await widget.authState.listHeldSales()
          : null;
      if (mounted) {
        setState(() {
          _history = history;
          _held = held;
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
  }

  Map<String, dynamic> _saleBody({
    required List<Map<String, dynamic>> payments,
  }) => {
    'branchId': widget.authState.currentUser?.branch.id,
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
    final payments = await showDialog<List<Map<String, dynamic>>>(
      context: context,
      builder: (_) => _PaymentDialog(total: _total),
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
                  children: [_pos(), _historyView()],
                ),
        ),
      ],
    ),
  );

  Widget _pos() => Row(
    children: [
      Expanded(
        flex: 3,
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            children: [
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
        Row(
          children: [
            Expanded(
              child: TextField(
                controller: _customer,
                decoration: const InputDecoration(labelText: 'Customer'),
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
                          onPressed: () => setState(
                            () => line.quantity = (line.quantity - 1).clamp(
                              1,
                              999,
                            ),
                          ),
                          icon: const Icon(Icons.remove),
                        ),
                        Text('${line.quantity}'),
                        IconButton(
                          tooltip: 'Increase',
                          onPressed: () => setState(() => line.quantity++),
                          icon: const Icon(Icons.add),
                        ),
                      ],
                    ),
                  ),
                  DataCell(Text(_money(line.price))),
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

  Widget _historyView() => Padding(
    padding: const EdgeInsets.all(20),
    child: Column(
      children: [
        if (can('sales.hold')) _heldTable(),
        const SizedBox(height: 16),
        Expanded(child: _salesTable()),
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
        columns: const [
          DataColumn(label: Text('Invoice')),
          DataColumn(label: Text('Cashier')),
          DataColumn(label: Text('Items')),
          DataColumn(label: Text('Total')),
          DataColumn(label: Text('Payment')),
          DataColumn(label: Text('Actions')),
        ],
        rows: sales
            .map(
              (sale) => DataRow(
                cells: [
                  DataCell(Text(sale.invoiceNumber ?? sale.holdNumber ?? '-')),
                  DataCell(Text(sale.cashierName)),
                  DataCell(Text('${sale.itemCount}')),
                  DataCell(Text(_money(sale.netTotal))),
                  DataCell(Text(sale.paymentSummary)),
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
          ],
        ),
      ),
    ),
  );
}

class _PaymentDialog extends StatefulWidget {
  const _PaymentDialog({required this.total});
  final double total;

  @override
  State<_PaymentDialog> createState() => _PaymentDialogState();
}

class _PaymentDialogState extends State<_PaymentDialog> {
  final _cashApplied = TextEditingController();
  final _cashTendered = TextEditingController();
  final _cardApplied = TextEditingController();
  String? _error;

  @override
  void initState() {
    super.initState();
    _cashApplied.text = widget.total.toStringAsFixed(2);
    _cashTendered.text = widget.total.toStringAsFixed(2);
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
          TextField(
            key: const Key('cash_applied'),
            controller: _cashApplied,
            decoration: const InputDecoration(labelText: 'Cash applied'),
          ),
          TextField(
            key: const Key('cash_tendered'),
            controller: _cashTendered,
            decoration: const InputDecoration(labelText: 'Cash tendered'),
          ),
          TextField(
            key: const Key('card_applied'),
            controller: _cardApplied,
            decoration: const InputDecoration(labelText: 'Card amount'),
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
    if ((applied - widget.total).abs() > 0.009) {
      setState(() => _error = 'Payment total must equal sale total.');
      return;
    }
    if (cash > 0 && tendered < cash) {
      setState(
        () => _error = 'Cash tendered cannot be less than cash applied.',
      );
      return;
    }
    final payments = <Map<String, dynamic>>[];
    if (cash > 0) {
      payments.add({
        'method': 1,
        'amountApplied': cash,
        'tenderedAmount': tendered,
      });
    }
    if (card > 0) {
      payments.add({'method': 2, 'amountApplied': card});
    }
    Navigator.pop(context, payments);
  }
}

class _CartLine {
  _CartLine(this.product);
  final PosProduct product;
  int quantity = 1;
  double discountPercent = 0;
  double get price => product.indicativeRetailPrice ?? 0;
  double get gross => price * quantity;
  double get discountAmount => gross * discountPercent / 100;
  double get net => gross - discountAmount;
}

String? _emptyToNull(String value) =>
    value.trim().isEmpty ? null : value.trim();
String _money(double value) => 'PKR ${value.toStringAsFixed(2)}';
