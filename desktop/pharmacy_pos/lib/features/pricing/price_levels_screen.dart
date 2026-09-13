import 'package:flutter/material.dart';
import '../../ui/app_widgets.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

class PriceLevelsScreen extends StatefulWidget {
  const PriceLevelsScreen({required this.authState, super.key});
  final AuthState authState;

  @override
  State<PriceLevelsScreen> createState() => _PriceLevelsScreenState();
}

class _PriceLevelsScreenState extends State<PriceLevelsScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs = TabController(length: 2, vsync: this);

  bool can(String permission) => widget.authState.can(permission);

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(24, 22, 24, 0),
          child: Text(
            'Price Levels & Product Pricing',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
        ),
        TabBar(
          controller: _tabs,
          isScrollable: true,
          tabs: const [
            Tab(text: 'Price Levels'),
            Tab(text: 'Product Pricing'),
          ],
        ),
        Expanded(
          child: TabBarView(
            controller: _tabs,
            children: [
              _LevelsTab(authState: widget.authState),
              _ProductPricingTab(authState: widget.authState),
            ],
          ),
        ),
      ],
    ),
  );
}

class _LevelsTab extends StatefulWidget {
  const _LevelsTab({required this.authState});
  final AuthState authState;

  @override
  State<_LevelsTab> createState() => _LevelsTabState();
}

class _LevelsTabState extends State<_LevelsTab> {
  List<PriceLevel> _levels = [];
  bool _loading = true;
  String? _error;

  bool can(String permission) => widget.authState.can(permission);

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
      final data = await widget.authState.pricing(
        '',
        query: const {'activeOnly': 'false'},
      );
      final levels = (data as List<dynamic>)
          .map((x) => PriceLevel.fromJson(x as Map<String, dynamic>))
          .toList();
      if (mounted) setState(() => _levels = levels);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const AppLoadingState();
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              IconButton.filledTonal(
                onPressed: _load,
                tooltip: 'Refresh',
                icon: const Icon(Icons.refresh),
              ),
              const Spacer(),
              if (can('pricing.manage'))
                FilledButton.icon(
                  key: const Key('add_price_level'),
                  onPressed: () => _showForm(),
                  icon: const Icon(Icons.add),
                  label: const Text('Add Price Level'),
                ),
            ],
          ),
          const SizedBox(height: 12),
          if (_error != null) Text(_error!),
          Expanded(
            child: SingleChildScrollView(
              child: SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: AppDataTable(
                  columns: const [
                    DataColumn(label: Text('Name')),
                    DataColumn(label: Text('Code')),
                    DataColumn(label: Text('Priority')),
                    DataColumn(label: Text('Default')),
                    DataColumn(label: Text('Status')),
                    DataColumn(label: Text('Actions')),
                  ],
                  rows: _levels
                      .map(
                        (level) => DataRow(
                          cells: [
                            DataCell(Text(level.name)),
                            DataCell(Text(level.code)),
                            DataCell(Text('${level.priority}')),
                            DataCell(
                              level.isDefault
                                  ? const Icon(Icons.star, size: 18)
                                  : const SizedBox.shrink(),
                            ),
                            DataCell(
                              AppStatusChip(
                                level.isActive ? 'Active' : 'Inactive',
                              ),
                            ),
                            DataCell(
                              can('pricing.manage')
                                  ? IconButton(
                                      tooltip: 'Edit',
                                      onPressed: () => _showForm(level: level),
                                      icon: const Icon(Icons.edit_outlined),
                                    )
                                  : const SizedBox.shrink(),
                            ),
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
  }

  Future<void> _showForm({PriceLevel? level}) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (_) =>
          _PriceLevelForm(authState: widget.authState, level: level),
    );
    if (ok == true) await _load();
  }
}

class _PriceLevelForm extends StatefulWidget {
  const _PriceLevelForm({required this.authState, this.level});
  final AuthState authState;
  final PriceLevel? level;

  @override
  State<_PriceLevelForm> createState() => _PriceLevelFormState();
}

class _PriceLevelFormState extends State<_PriceLevelForm> {
  final _form = GlobalKey<FormState>();
  late final _name = TextEditingController(text: widget.level?.name);
  late final _code = TextEditingController(text: widget.level?.code);
  late final _priority = TextEditingController(
    text: (widget.level?.priority ?? 0).toString(),
  );
  late bool _isDefault = widget.level?.isDefault ?? false;
  late bool _isActive = widget.level?.isActive ?? true;
  String? _error;

  @override
  void dispose() {
    _name.dispose();
    _code.dispose();
    _priority.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final editing = widget.level != null;
    return AlertDialog(
      title: Text(editing ? 'Edit Price Level' : 'Add Price Level'),
      content: SizedBox(
        width: 420,
        child: Form(
          key: _form,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                key: const Key('price_level_name'),
                controller: _name,
                decoration: const InputDecoration(labelText: 'Name'),
                validator: (v) =>
                    v == null || v.trim().isEmpty ? 'Required' : null,
              ),
              TextFormField(
                key: const Key('price_level_code'),
                controller: _code,
                decoration: const InputDecoration(labelText: 'Code'),
                validator: (v) =>
                    v == null || v.trim().isEmpty ? 'Required' : null,
              ),
              TextFormField(
                controller: _priority,
                decoration: const InputDecoration(
                  labelText: 'Priority (lower shows first)',
                ),
                keyboardType: TextInputType.number,
              ),
              SwitchListTile(
                key: const Key('price_level_default'),
                value: _isDefault,
                onChanged: (v) => setState(() => _isDefault = v),
                title: const Text('Default level'),
              ),
              SwitchListTile(
                value: _isActive,
                onChanged: (v) => setState(() => _isActive = v),
                title: const Text('Active'),
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
          onPressed: () => Navigator.pop(context, false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          key: const Key('save_price_level'),
          onPressed: _save,
          child: const Text('Save'),
        ),
      ],
    );
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    final body = {
      'name': _name.text.trim(),
      'code': _code.text.trim(),
      'priority': int.tryParse(_priority.text.trim()) ?? 0,
      'isDefault': _isDefault,
      'isActive': _isActive,
    };
    try {
      if (widget.level == null) {
        await widget.authState.pricing('', method: 'POST', body: body);
      } else {
        await widget.authState.pricing(
          widget.level!.id,
          method: 'PUT',
          body: body,
        );
      }
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    }
  }
}

class _ProductPricingTab extends StatefulWidget {
  const _ProductPricingTab({required this.authState});
  final AuthState authState;

  @override
  State<_ProductPricingTab> createState() => _ProductPricingTabState();
}

class _ProductPricingTabState extends State<_ProductPricingTab> {
  final _search = TextEditingController();
  List<ProductListItem> _results = [];
  ProductListItem? _selected;
  List<PriceLevel> _levels = [];
  List<ProductPriceLevelInfo> _prices = [];
  List<ProductPriceBreakInfo> _breaks = [];
  bool _loadingProduct = false;

  bool can(String permission) => widget.authState.can(permission);

  @override
  void initState() {
    super.initState();
    widget.authState
        .pricing('', query: const {'activeOnly': 'true'})
        .then((data) {
          if (!mounted) return;
          setState(() {
            _levels = (data as List<dynamic>)
                .map((x) => PriceLevel.fromJson(x as Map<String, dynamic>))
                .toList();
          });
        })
        .catchError((_) {});
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _searchProducts(String value) async {
    if (value.trim().isEmpty) {
      setState(() => _results = []);
      return;
    }
    final paged = await widget.authState.listProducts(search: value);
    if (mounted) setState(() => _results = paged.items);
  }

  Future<void> _selectProduct(ProductListItem product) async {
    setState(() {
      _selected = product;
      _results = [];
      _search.text = product.name;
      _loadingProduct = true;
    });
    try {
      final pricesData = await widget.authState.pricing(
        'product-prices',
        query: {'productId': product.id},
      );
      final breaksData = await widget.authState.pricing(
        'product-breaks',
        query: {'productId': product.id},
      );
      if (mounted) {
        setState(() {
          _prices = (pricesData as List<dynamic>)
              .map(
                (x) =>
                    ProductPriceLevelInfo.fromJson(x as Map<String, dynamic>),
              )
              .toList();
          _breaks = (breaksData as List<dynamic>)
              .map(
                (x) =>
                    ProductPriceBreakInfo.fromJson(x as Map<String, dynamic>),
              )
              .toList();
        });
      }
    } finally {
      if (mounted) setState(() => _loadingProduct = false);
    }
  }

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.all(24),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 420,
          child: TextField(
            key: const Key('product_pricing_search'),
            controller: _search,
            decoration: const InputDecoration(
              labelText: 'Search product',
              prefixIcon: Icon(Icons.search),
            ),
            onChanged: _searchProducts,
          ),
        ),
        if (_results.isNotEmpty)
          Container(
            width: 420,
            constraints: const BoxConstraints(maxHeight: 220),
            decoration: BoxDecoration(border: Border.all(color: Colors.grey)),
            child: ListView(
              shrinkWrap: true,
              children: _results
                  .map(
                    (p) => ListTile(
                      dense: true,
                      title: Text(p.name),
                      subtitle: Text(p.sku),
                      onTap: () => _selectProduct(p),
                    ),
                  )
                  .toList(),
            ),
          ),
        const SizedBox(height: 16),
        if (_selected == null)
          const Text('Search for a product to manage its pricing.')
        else if (_loadingProduct)
          const CircularProgressIndicator()
        else
          Expanded(
            child: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '${_selected!.name} (${_selected!.sku}) - default retail price ${_selected!.retailPrice.toStringAsFixed(2)}',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Text(
                        'Price-Level Prices',
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                      const SizedBox(width: 12),
                      if (can('pricing.manage'))
                        OutlinedButton.icon(
                          key: const Key('add_product_price'),
                          onPressed: () => _showPriceDialog(),
                          icon: const Icon(Icons.add),
                          label: const Text('Set Price'),
                        ),
                    ],
                  ),
                  SingleChildScrollView(
                    scrollDirection: Axis.horizontal,
                    child: AppDataTable(
                      columns: const [
                        DataColumn(label: Text('Level')),
                        DataColumn(label: Text('Price'), numeric: true),
                        DataColumn(label: Text('Status')),
                        DataColumn(label: Text('')),
                      ],
                      rows: _prices
                          .map(
                            (p) => DataRow(
                              cells: [
                                DataCell(Text(p.priceLevelName)),
                                DataCell(
                                  Text(p.sellingPrice.toStringAsFixed(2)),
                                ),
                                DataCell(
                                  Text(p.isActive ? 'Active' : 'Inactive'),
                                ),
                                DataCell(
                                  can('pricing.manage')
                                      ? IconButton(
                                          icon: const Icon(
                                            Icons.delete_outline,
                                          ),
                                          onPressed: () async {
                                            await widget.authState.pricing(
                                              'product-prices/${p.id}',
                                              method: 'DELETE',
                                            );
                                            await _selectProduct(_selected!);
                                          },
                                        )
                                      : const SizedBox.shrink(),
                                ),
                              ],
                            ),
                          )
                          .toList(),
                    ),
                  ),
                  const SizedBox(height: 20),
                  Row(
                    children: [
                      Text(
                        'Quantity Breaks',
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                      const SizedBox(width: 12),
                      if (can('pricing.manage'))
                        OutlinedButton.icon(
                          key: const Key('add_product_break'),
                          onPressed: () => _showBreakDialog(),
                          icon: const Icon(Icons.add),
                          label: const Text('Add Break'),
                        ),
                    ],
                  ),
                  SingleChildScrollView(
                    scrollDirection: Axis.horizontal,
                    child: AppDataTable(
                      columns: const [
                        DataColumn(label: Text('Level')),
                        DataColumn(label: Text('Min Qty'), numeric: true),
                        DataColumn(label: Text('Price'), numeric: true),
                        DataColumn(label: Text('')),
                      ],
                      rows: _breaks
                          .map(
                            (b) => DataRow(
                              cells: [
                                DataCell(Text(b.priceLevelName ?? 'Any level')),
                                DataCell(Text('${b.minimumQuantity}')),
                                DataCell(
                                  Text(b.sellingPrice.toStringAsFixed(2)),
                                ),
                                DataCell(
                                  can('pricing.manage')
                                      ? IconButton(
                                          icon: const Icon(
                                            Icons.delete_outline,
                                          ),
                                          onPressed: () async {
                                            await widget.authState.pricing(
                                              'product-breaks/${b.id}',
                                              method: 'DELETE',
                                            );
                                            await _selectProduct(_selected!);
                                          },
                                        )
                                      : const SizedBox.shrink(),
                                ),
                              ],
                            ),
                          )
                          .toList(),
                    ),
                  ),
                ],
              ),
            ),
          ),
      ],
    ),
  );

  Future<void> _showPriceDialog() async {
    String? levelId = _levels.isNotEmpty ? _levels.first.id : null;
    final priceController = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: const Text('Set Price-Level Price'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              DropdownButtonFormField<String>(
                initialValue: levelId,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'Price Level'),
                items: _levels
                    .map(
                      (l) => DropdownMenuItem(value: l.id, child: Text(l.name)),
                    )
                    .toList(),
                onChanged: (v) => setDialogState(() => levelId = v),
              ),
              TextField(
                key: const Key('product_price_amount'),
                controller: priceController,
                decoration: const InputDecoration(labelText: 'Selling Price'),
                keyboardType: TextInputType.number,
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              key: const Key('save_product_price'),
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Save'),
            ),
          ],
        ),
      ),
    );
    if (ok == true && levelId != null && _selected != null) {
      await widget.authState.pricing(
        'product-prices',
        method: 'POST',
        body: {
          'productId': _selected!.id,
          'priceLevelId': levelId,
          'sellingPrice': double.tryParse(priceController.text) ?? 0,
          'isActive': true,
        },
      );
      await _selectProduct(_selected!);
    }
  }

  Future<void> _showBreakDialog() async {
    String? levelId;
    final qtyController = TextEditingController();
    final priceController = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: const Text('Add Quantity Break'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              DropdownButtonFormField<String?>(
                initialValue: levelId,
                isExpanded: true,
                decoration: const InputDecoration(
                  labelText: 'Price Level (optional)',
                  helperText: 'Leave blank to apply regardless of level.',
                ),
                items: [
                  const DropdownMenuItem(value: null, child: Text('Any level')),
                  ..._levels.map(
                    (l) => DropdownMenuItem(value: l.id, child: Text(l.name)),
                  ),
                ],
                onChanged: (v) => setDialogState(() => levelId = v),
              ),
              TextField(
                key: const Key('product_break_quantity'),
                controller: qtyController,
                decoration: const InputDecoration(
                  labelText: 'Minimum Quantity',
                ),
                keyboardType: TextInputType.number,
              ),
              TextField(
                key: const Key('product_break_price'),
                controller: priceController,
                decoration: const InputDecoration(labelText: 'Selling Price'),
                keyboardType: TextInputType.number,
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              key: const Key('save_product_break'),
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Save'),
            ),
          ],
        ),
      ),
    );
    if (ok == true && _selected != null) {
      await widget.authState.pricing(
        'product-breaks',
        method: 'POST',
        body: {
          'productId': _selected!.id,
          'priceLevelId': levelId,
          'minimumQuantity': int.tryParse(qtyController.text) ?? 0,
          'sellingPrice': double.tryParse(priceController.text) ?? 0,
          'isActive': true,
        },
      );
      await _selectProduct(_selected!);
    }
  }
}
