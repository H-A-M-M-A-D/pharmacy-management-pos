import 'dart:async';
import 'package:flutter/material.dart';
import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

class ProductsScreen extends StatefulWidget {
  const ProductsScreen({required this.authState, super.key});
  final AuthState authState;
  @override
  State<ProductsScreen> createState() => _ProductsScreenState();
}

class _ProductsScreenState extends State<ProductsScreen> {
  final _search = TextEditingController();
  Timer? _debounce;
  ProductOptions? _options;
  List<ProductListItem> _items = const [];
  String? _category, _manufacturer, _error;
  bool? _active;
  bool _loading = true;
  int _page = 1, _total = 0;
  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      _options ??= await widget.authState.productOptions();
      final result = await widget.authState.listProducts(
        page: _page,
        search: _search.text,
        categoryId: _category,
        manufacturerId: _manufacturer,
        isActive: _active,
      );
      if (mounted) {
        setState(() {
          _items = result.items;
          _total = result.totalCount;
        });
      }
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _searchChanged(String _) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), () {
      _page = 1;
      _load();
    });
  }

  Future<void> _edit([ProductListItem? item]) async {
    ProductDetails? details;
    if (item != null) details = await widget.authState.productDetails(item.id);
    if (!mounted || _options == null) return;
    final changed = await showDialog<bool>(
      context: context,
      builder: (_) => ProductFormDialog(
        authState: widget.authState,
        options: _options!,
        product: details,
      ),
    );
    if (changed == true) _load();
  }

  Future<void> _toggle(ProductListItem item) async {
    final verb = item.isActive ? 'Deactivate' : 'Activate';
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('$verb ${item.name}?'),
        content: Text(
          item.isActive
              ? 'The product remains in historical data but is unavailable for normal operations.'
              : 'The product will become available for normal operations.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text(verb),
          ),
        ],
      ),
    );
    if (ok == true) {
      await widget.authState.setProductActive(item.id, !item.isActive);
      _load();
    }
  }

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(
                'Products',
                style: Theme.of(context).textTheme.headlineMedium,
              ),
              const Spacer(),
              if (widget.authState.can('products.create'))
                FilledButton.icon(
                  key: const Key('add_product'),
                  onPressed: () => _edit(),
                  icon: const Icon(Icons.add),
                  label: const Text('Add Product'),
                ),
            ],
          ),
          const SizedBox(height: 18),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              SizedBox(
                width: 300,
                child: TextField(
                  key: const Key('product_search'),
                  controller: _search,
                  onChanged: _searchChanged,
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search),
                    labelText: 'Search products',
                  ),
                ),
              ),
              _filter('Category', _category, _options?.categories ?? const [], (
                v,
              ) {
                setState(() => _category = v);
                _page = 1;
                _load();
              }),
              _filter(
                'Manufacturer',
                _manufacturer,
                _options?.manufacturers ?? const [],
                (v) {
                  setState(() => _manufacturer = v);
                  _page = 1;
                  _load();
                },
              ),
              SizedBox(
                width: 170,
                child: DropdownButtonFormField<bool>(
                  initialValue: _active,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Status'),
                  items: const [
                    DropdownMenuItem(value: null, child: Text('All')),
                    DropdownMenuItem(value: true, child: Text('Active')),
                    DropdownMenuItem(value: false, child: Text('Inactive')),
                  ],
                  onChanged: (v) {
                    setState(() => _active = v);
                    _page = 1;
                    _load();
                  },
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _error != null
                ? Center(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(_error!),
                        TextButton.icon(
                          onPressed: _load,
                          icon: const Icon(Icons.refresh),
                          label: const Text('Retry'),
                        ),
                      ],
                    ),
                  )
                : _items.isEmpty
                ? const Center(
                    child: Text('No products match the current filters.'),
                  )
                : SingleChildScrollView(
                    scrollDirection: Axis.horizontal,
                    child: SingleChildScrollView(
                      child: DataTable(
                        columns: const [
                          DataColumn(label: Text('Product')),
                          DataColumn(label: Text('SKU')),
                          DataColumn(label: Text('Barcode')),
                          DataColumn(label: Text('Generic')),
                          DataColumn(label: Text('Category')),
                          DataColumn(label: Text('Manufacturer')),
                          DataColumn(label: Text('Unit')),
                          DataColumn(label: Text('Retail Price')),
                          DataColumn(label: Text('Status')),
                          DataColumn(label: Text('Actions')),
                        ],
                        rows: _items
                            .map(
                              (x) => DataRow(
                                cells: [
                                  DataCell(
                                    SizedBox(
                                      width: 170,
                                      child: Tooltip(
                                        message: x.name,
                                        child: Text(
                                          x.name,
                                          overflow: TextOverflow.ellipsis,
                                        ),
                                      ),
                                    ),
                                  ),
                                  DataCell(Text(x.sku)),
                                  DataCell(Text(x.barcode ?? '-')),
                                  DataCell(
                                    SizedBox(
                                      width: 150,
                                      child: Text(
                                        x.genericName ?? '-',
                                        overflow: TextOverflow.ellipsis,
                                      ),
                                    ),
                                  ),
                                  DataCell(Text(x.category.name)),
                                  DataCell(Text(x.manufacturer?.name ?? '-')),
                                  DataCell(Text(x.unit)),
                                  DataCell(
                                    Text(
                                      'PKR ${x.retailPrice.toStringAsFixed(2)}',
                                    ),
                                  ),
                                  DataCell(
                                    Text(x.isActive ? 'Active' : 'Inactive'),
                                  ),
                                  DataCell(
                                    Row(
                                      children: [
                                        IconButton(
                                          tooltip:
                                              widget.authState.can(
                                                'products.update',
                                              )
                                              ? 'Edit'
                                              : 'View',
                                          onPressed: () => _edit(x),
                                          icon: Icon(
                                            widget.authState.can(
                                                  'products.update',
                                                )
                                                ? Icons.edit_outlined
                                                : Icons.visibility_outlined,
                                          ),
                                        ),
                                        if (widget.authState.can(
                                          x.isActive
                                              ? 'products.deactivate'
                                              : 'products.activate',
                                        ))
                                          IconButton(
                                            tooltip: x.isActive
                                                ? 'Deactivate'
                                                : 'Activate',
                                            onPressed: () => _toggle(x),
                                            icon: Icon(
                                              x.isActive
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
                  ),
          ),
          Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              Text('$_total products'),
              IconButton(
                onPressed: _page > 1
                    ? () {
                        setState(() => _page--);
                        _load();
                      }
                    : null,
                icon: const Icon(Icons.chevron_left),
              ),
              Text('Page $_page'),
              IconButton(
                onPressed: _page * 25 < _total
                    ? () {
                        setState(() => _page++);
                        _load();
                      }
                    : null,
                icon: const Icon(Icons.chevron_right),
              ),
            ],
          ),
        ],
      ),
    ),
  );
  Widget _filter(
    String label,
    String? value,
    List<CatalogLookup> items,
    ValueChanged<String?> changed,
  ) => SizedBox(
    width: 190,
    child: DropdownButtonFormField<String>(
      initialValue: value,
      isExpanded: true,
      decoration: InputDecoration(labelText: label),
      items: [
        const DropdownMenuItem(value: null, child: Text('All')),
        ...items.map((x) => DropdownMenuItem(value: x.id, child: Text(x.name))),
      ],
      onChanged: changed,
    ),
  );
}

class ProductFormDialog extends StatefulWidget {
  const ProductFormDialog({
    required this.authState,
    required this.options,
    this.product,
    super.key,
  });
  final AuthState authState;
  final ProductOptions options;
  final ProductDetails? product;
  @override
  State<ProductFormDialog> createState() => _ProductFormDialogState();
}

class _ProductFormDialogState extends State<ProductFormDialog> {
  final _form = GlobalKey<FormState>();
  late final Map<String, TextEditingController> c;
  String? category, manufacturer, unit;
  bool saving = false;
  String? error;
  bool get editable =>
      widget.product == null || widget.authState.can('products.update');
  @override
  void initState() {
    super.initState();
    final p = widget.product;
    c = {
      'name': TextEditingController(text: p?.name),
      'brandName': TextEditingController(text: p?.brandName),
      'genericName': TextEditingController(text: p?.genericName),
      'sku': TextEditingController(text: p?.sku),
      'barcode': TextEditingController(text: p?.barcode),
      'packSize': TextEditingController(text: '${p?.packSize ?? 1}'),
      'purchasePrice': TextEditingController(text: '${p?.purchasePrice ?? 0}'),
      'retailPrice': TextEditingController(text: '${p?.retailPrice ?? 0}'),
      'tradePrice': TextEditingController(text: p?.tradePrice?.toString()),
      'maximumDiscountPercent': TextEditingController(
        text: '${p?.maximumDiscountPercent ?? 0}',
      ),
      'reorderLevel': TextEditingController(text: '${p?.reorderLevel ?? 0}'),
    };
    category = p?.category.id;
    manufacturer = p?.manufacturer?.id;
    unit =
        p?.unit ??
        (widget.options.units.isEmpty ? null : widget.options.units.first);
  }

  @override
  void dispose() {
    for (final x in c.values) {
      x.dispose();
    }
    super.dispose();
  }

  String? required(String? v) => v?.trim().isEmpty != false ? 'Required' : null;
  String? number(String? v, {bool integer = false}) {
    final n = integer ? int.tryParse(v ?? '') : double.tryParse(v ?? '');
    return n == null || n < 0 ? 'Enter a non-negative number' : null;
  }

  Future<void> save() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      saving = true;
      error = null;
    });
    final values = {
      'name': c['name']!.text,
      'sku': c['sku']!.text,
      'barcode': c['barcode']!.text,
      'genericName': c['genericName']!.text,
      'brandName': c['brandName']!.text,
      'categoryId': category,
      'manufacturerId': manufacturer,
      'unit': unit,
      'packSize': int.parse(c['packSize']!.text),
      'purchasePrice': double.parse(c['purchasePrice']!.text),
      'retailPrice': double.parse(c['retailPrice']!.text),
      'tradePrice': double.tryParse(c['tradePrice']!.text),
      'maximumDiscountPercent': double.parse(c['maximumDiscountPercent']!.text),
      'reorderLevel': int.parse(c['reorderLevel']!.text),
      'isActive': widget.product?.isActive ?? true,
    };
    try {
      if (widget.product == null) {
        await widget.authState.createProduct(values);
      } else {
        await widget.authState.updateProduct(widget.product!.id, values);
      }
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) {
      setState(() => error = e.message);
    } finally {
      if (mounted) setState(() => saving = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(
      widget.product == null
          ? 'Add Product'
          : '${widget.authState.can('products.update') ? 'Edit' : 'View'} Product',
    ),
    content: SizedBox(
      width: 760,
      height: 560,
      child: Form(
        key: _form,
        child: SingleChildScrollView(
          child: Wrap(
            spacing: 14,
            runSpacing: 14,
            children: [
              section('Basic information'),
              field('Product Name', 'name', validator: required),
              field('Brand Name', 'brandName'),
              field('Generic Name', 'genericName'),
              section('Identifiers'),
              field(
                'SKU',
                'sku',
                enabled: widget.product == null,
                key: const Key('product_sku'),
              ),
              field('Barcode', 'barcode'),
              section('Classification'),
              drop(
                'Category',
                category,
                widget.options.categories,
                (v) => setState(() => category = v),
                requiredField: true,
              ),
              drop(
                'Manufacturer',
                manufacturer,
                widget.options.manufacturers,
                (v) => setState(() => manufacturer = v),
              ),
              section('Packaging'),
              dropText(
                'Unit',
                unit,
                widget.options.units,
                (v) => setState(() => unit = v),
              ),
              field(
                'Pack Size',
                'packSize',
                validator: (v) {
                  final n = int.tryParse(v ?? '');
                  return n == null || n < 1
                      ? 'Enter a positive whole number'
                      : null;
                },
              ),
              section('Pricing'),
              field('Purchase Price', 'purchasePrice', validator: number),
              field('Retail Price', 'retailPrice', validator: number),
              field(
                'Trade Price',
                'tradePrice',
                validator: (v) => v?.trim().isEmpty == true ? null : number(v),
              ),
              field(
                'Maximum Discount %',
                'maximumDiscountPercent',
                validator: (v) {
                  final n = double.tryParse(v ?? '');
                  return n == null || n < 0 || n > 100
                      ? 'Enter 0 to 100'
                      : null;
                },
              ),
              section('Stock policy'),
              field(
                'Reorder Level',
                'reorderLevel',
                validator: (v) => number(v, integer: true),
              ),
              if (error != null)
                SizedBox(
                  width: 730,
                  child: Text(
                    error!,
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
        onPressed: () => Navigator.pop(context),
        child: const Text('Cancel'),
      ),
      if (widget.product == null || widget.authState.can('products.update'))
        FilledButton(
          key: const Key('save_product'),
          onPressed: saving ? null : save,
          child: Text(saving ? 'Saving...' : 'Save Product'),
        ),
    ],
  );
  Widget section(String x) => SizedBox(
    width: 730,
    child: Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Text(
        x.toUpperCase(),
        style: Theme.of(context).textTheme.labelLarge,
      ),
    ),
  );
  Widget field(
    String label,
    String name, {
    String? Function(String?)? validator,
    bool enabled = true,
    Key? key,
  }) => SizedBox(
    width: 235,
    child: TextFormField(
      key: key,
      controller: c[name],
      enabled: enabled && editable,
      validator: validator,
      decoration: InputDecoration(labelText: label),
    ),
  );
  Widget drop(
    String label,
    String? value,
    List<CatalogLookup> items,
    ValueChanged<String?> change, {
    bool requiredField = false,
  }) => SizedBox(
    width: 235,
    child: DropdownButtonFormField<String>(
      initialValue: value,
      isExpanded: true,
      validator: requiredField ? ((v) => v == null ? 'Required' : null) : null,
      decoration: InputDecoration(labelText: label),
      items: [
        if (!requiredField)
          const DropdownMenuItem(value: null, child: Text('None')),
        ...items.map((x) => DropdownMenuItem(value: x.id, child: Text(x.name))),
      ],
      onChanged: editable ? change : null,
    ),
  );
  Widget dropText(
    String label,
    String? value,
    List<String> items,
    ValueChanged<String?> change,
  ) => SizedBox(
    width: 235,
    child: DropdownButtonFormField<String>(
      initialValue: value,
      validator: (v) => v == null ? 'Required' : null,
      decoration: InputDecoration(labelText: label),
      items: items
          .map((x) => DropdownMenuItem(value: x, child: Text(x)))
          .toList(),
      onChanged: editable ? change : null,
    ),
  );
}
