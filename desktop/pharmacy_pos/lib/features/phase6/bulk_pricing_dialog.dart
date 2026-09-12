import 'package:flutter/material.dart';
import '../auth/auth_state.dart';
import 'pricing_scope_picker.dart';

class BulkPricingDialog extends StatefulWidget {
  const BulkPricingDialog({required this.authState, super.key});
  final AuthState authState;
  @override
  State<BulkPricingDialog> createState() => _BulkPricingDialogState();
}

class _BulkPricingDialogState extends State<BulkPricingDialog> {
  final _form = GlobalKey<FormState>();
  final _category = TextEditingController();
  final _manufacturer = TextEditingController();
  final _level = TextEditingController();
  final _products = TextEditingController();
  final _value = TextEditingController(text: '10');
  final _rounding = TextEditingController(text: '1');
  final _reason = TextEditingController();
  String _action = 'IncreasePercent';
  Map<String, dynamic>? _preview;
  String? _error;
  bool _busy = false;
  bool _confirmed = false;
  Map<String, dynamic> _options = {};
  String _productSearch = '';
  final Set<String> _selectedProducts = {};

  @override
  void initState() { super.initState(); _loadOptions(); }
  Future<void> _loadOptions() async {
    setState(() => _busy = true);
    try {
      final options = await widget.authState.phase6('pricing-options') as Map<String, dynamic>;
      if (mounted) setState(() { _options = options; _rounding.text = '${options['thresholds']['roundingIncrement']}'; });
    } catch (error) { if (mounted) setState(() => _error = error.toString()); }
    finally { if (mounted) setState(() => _busy = false); }
  }

  @override
  void dispose() {
    for (final controller in [
      _category,
      _manufacturer,
      _level,
      _products,
      _value,
      _rounding,
      _reason,
    ]) {
      controller.dispose();
    }
    super.dispose();
  }

  String? _optional(TextEditingController value) =>
      value.text.trim().isEmpty ? null : value.text.trim();

  Future<void> _submit() async {
    if (_preview == null && !_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      if (_preview == null) {
        final result = await widget.authState.phase6(
          'bulk-pricing/preview',
          method: 'POST',
          body: {
            'categoryId': _optional(_category),
            'manufacturerId': _optional(_manufacturer),
            'priceLevelId': _optional(_level),
            'productIds': _products.text.trim().isEmpty
                ? null
                : _products.text.split(',').map((s) => s.trim()).toList(),
            'action': _action,
            'value': num.parse(_value.text),
            'roundingIncrement': num.parse(_rounding.text),
            'reason': _reason.text.trim(),
          },
        );
        if (mounted) setState(() => _preview = result as Map<String, dynamic>);
      } else {
        await widget.authState.phase6(
          'bulk-pricing/apply',
          method: 'POST',
          body: {'previewId': _preview!['previewId'], 'confirmed': _confirmed},
        );
        if (mounted) Navigator.pop(context, true);
      }
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(_preview == null ? 'Bulk pricing' : 'Review price changes'),
    content: SizedBox(
      width: 650,
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (_error != null)
              Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            if (_preview == null)
              Form(
                key: _form,
                child: Column(
                  children: [
                    PricingScopePicker(label: 'Category', controller: _category, options: _options['categories'] as List<dynamic>? ?? []),
                    PricingScopePicker(label: 'Manufacturer', controller: _manufacturer, options: _options['manufacturers'] as List<dynamic>? ?? []),
                    PricingScopePicker(label: 'Price level (defaults to retail)', controller: _level, options: _options['priceLevels'] as List<dynamic>? ?? []),
                    TextField(decoration: const InputDecoration(labelText: 'Select explicit products (optional)'), onChanged: (v) => setState(() => _productSearch = v)),
                    SizedBox(height: 110, child: SingleChildScrollView(child: Wrap(spacing: 8, children: [for (final product in (_options['products'] as List<dynamic>? ?? []).where((p) => '${p['name']}'.toLowerCase().contains(_productSearch.toLowerCase()))) FilterChip(label: Text('${product['name']}'), selected: _selectedProducts.contains(product['id']), onSelected: (selected) => setState(() { if (selected) { _selectedProducts.add(product['id'] as String); } else { _selectedProducts.remove(product['id']); } _products.text = _selectedProducts.join(','); }))]))),
                    DropdownButtonFormField<String>(
                      initialValue: _action,
                      decoration: const InputDecoration(labelText: 'Action'),
                      items: const [
                        DropdownMenuItem(
                          value: 'IncreasePercent',
                          child: Text('Increase %'),
                        ),
                        DropdownMenuItem(
                          value: 'DecreasePercent',
                          child: Text('Decrease %'),
                        ),
                        DropdownMenuItem(
                          value: 'SetMargin',
                          child: Text('Set margin over cost %'),
                        ),
                        DropdownMenuItem(
                          value: 'SetMarkup',
                          child: Text('Set markup %'),
                        ),
                        DropdownMenuItem(
                          value: 'Round',
                          child: Text('Apply rounding'),
                        ),
                      ],
                      onChanged: (value) => setState(() => _action = value!),
                    ),
                    TextFormField(
                      controller: _value,
                      decoration: const InputDecoration(labelText: 'Value %'),
                      validator: (s) =>
                          num.tryParse(s ?? '') == null || num.parse(s!) < 0
                          ? 'Enter a nonnegative value'
                          : null,
                    ),
                    TextFormField(
                      controller: _rounding,
                      decoration: const InputDecoration(
                        labelText: 'Round up to increment',
                      ),
                      validator: (s) =>
                          num.tryParse(s ?? '') == null || num.parse(s!) <= 0
                          ? 'Enter a positive increment'
                          : null,
                    ),
                    TextFormField(
                      controller: _reason,
                      decoration: const InputDecoration(labelText: 'Reason'),
                      maxLength: 500,
                      validator: (s) => s == null || s.trim().isEmpty
                          ? 'Reason is required'
                          : null,
                    ),
                  ],
                ),
              ),
            if (_preview != null) ...[
              Text('Reason: ${_preview!['reason']}'),
              Text('Expires: ${_preview!['expiresAtUtc']}'),
              for (final row in _preview!['rows'] as List<dynamic>)
                ListTile(
                  title: Text('${row['product']}'),
                  subtitle: Text('${row['oldPrice']} → ${row['newPrice']}'),
                ),
              CheckboxListTile(
                value: _confirmed,
                onChanged: _busy
                    ? null
                    : (value) => setState(() => _confirmed = value!),
                title: const Text('I confirm these price changes'),
              ),
            ],
          ],
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: _busy ? null : () => Navigator.pop(context),
        child: const Text('Cancel'),
      ),
      if (_preview != null)
        TextButton(
          onPressed: _busy
              ? null
              : () => setState(() {
                  _preview = null;
                  _confirmed = false;
                }),
          child: const Text('Edit selection'),
        ),
      FilledButton(
        onPressed: _busy || _preview != null && !_confirmed ? null : _submit,
        child: Text(
          _busy
              ? 'Working…'
              : _preview == null
              ? 'Preview'
              : 'Apply confirmed changes',
        ),
      ),
    ],
  );
}
