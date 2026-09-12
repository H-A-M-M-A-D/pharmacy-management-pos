import 'package:flutter/material.dart';
import '../auth/auth_state.dart';
import 'pricing_scope_picker.dart';

class PricingRuleDialog extends StatefulWidget {
  const PricingRuleDialog({required this.authState, this.rule, super.key});
  final AuthState authState;
  final Map<String, dynamic>? rule;
  @override
  State<PricingRuleDialog> createState() => _PricingRuleDialogState();
}

class _PricingRuleDialogState extends State<PricingRuleDialog> {
  final _form = GlobalKey<FormState>();
  late final Map<String, TextEditingController> _fields;
  late final Map<String, dynamic> _values;
  bool _busy = false;
  String? _error;
  Map<String, dynamic> _options = {};
  static const _scopeOptions = {'branchId': 'branches', 'customerId': 'customers', 'productId': 'products', 'categoryId': 'categories', 'manufacturerId': 'manufacturers', 'priceLevelId': 'priceLevels'};
  static const _labels = {
    'name': 'Name',
    'priority': 'Priority',
    'adjustmentValue': 'Adjustment value',
    'branchId': 'Branch ID',
    'customerId': 'Customer ID',
    'productId': 'Product ID',
    'categoryId': 'Category ID',
    'manufacturerId': 'Manufacturer ID',
    'priceLevelId': 'Price level ID',
    'minimumQuantity': 'Minimum quantity',
    'startsAtUtc': 'Starts at (ISO date/time)',
    'endsAtUtc': 'Ends at (ISO date/time)',
  };
  static const _choices = {
    'kind': ['Rule', 'Promotion'],
    'adjustmentType': ['FixedPrice', 'PercentageDiscount', 'FixedDiscount'],
    'customerType': ['Retail', 'Wholesale', 'Institutional'],
    'saleType': ['Retail', 'Wholesale'],
  };
  @override
  void initState() {
    super.initState();
    final rule = widget.rule ?? {};
    _fields = {
      for (final key in _labels.keys)
        key: TextEditingController(
          text:
              '${rule[key] ?? (key == 'priority' || key == 'adjustmentValue' ? 0 : '')}',
        ),
    };
    _values = {
      'kind': rule['kind'] ?? 'Rule',
      'adjustmentType': rule['adjustmentType'] ?? 'FixedPrice',
      'customerType': rule['customerType'],
      'saleType': rule['saleType'],
      'isActive': rule['isActive'] ?? true,
    };
    _loadOptions();
  }

  Future<void> _loadOptions() async {
    setState(() => _busy = true);
    try { final options = await widget.authState.phase6('pricing-options') as Map<String, dynamic>; if (mounted) setState(() => _options = options); }
    catch (error) { if (mounted) setState(() => _error = error.toString()); }
    finally { if (mounted) setState(() => _busy = false); }
  }

  @override
  void dispose() {
    for (final field in _fields.values) {
      field.dispose();
    }
    super.dispose();
  }

  String? _validate(String key, String? raw) {
    final value = raw?.trim() ?? '';
    if (key == 'name') return value.isEmpty ? 'Name is required' : null;
    if (key == 'priority' || key == 'minimumQuantity') {
      return value.isEmpty && key == 'minimumQuantity'
          ? null
          : int.tryParse(value) == null ||
                int.parse(value) < (key == 'minimumQuantity' ? 1 : 0)
          ? 'Enter a valid integer'
          : null;
    }
    if (key == 'adjustmentValue') {
      return num.tryParse(value) == null ||
              num.parse(value) < 0 ||
              _values['adjustmentType'] == 'PercentageDiscount' &&
                  num.parse(value) > 100
          ? 'Enter a valid adjustment'
          : null;
    }
    if (key.endsWith('Utc')) {
      return value.isEmpty || DateTime.tryParse(value) != null
          ? null
          : 'Enter a valid date/time';
    }
    return value.isEmpty ||
            RegExp(
              r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
            ).hasMatch(value)
        ? null
        : 'Enter a valid ID';
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    final start = DateTime.tryParse(_fields['startsAtUtc']!.text);
    final end = DateTime.tryParse(_fields['endsAtUtc']!.text);
    if (start != null && end != null && !end.isAfter(start)) {
      setState(() => _error = 'End must be after start');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final body = Map<String, dynamic>.from(_values);
      for (final entry in _fields.entries) {
        final value = entry.value.text.trim();
        body[entry.key] =
            entry.key == 'priority' || entry.key == 'minimumQuantity'
            ? int.tryParse(value)
            : entry.key == 'adjustmentValue'
            ? num.parse(value)
            : entry.key.endsWith('Utc')
            ? DateTime.tryParse(value)?.toUtc().toIso8601String()
            : value.isEmpty
            ? null
            : value;
      }
      await widget.authState.phase6(
        widget.rule == null
            ? 'pricing-rules'
            : 'pricing-rules/${widget.rule!['id']}',
        method: widget.rule == null ? 'POST' : 'PUT',
        body: body,
      );
      if (mounted) Navigator.pop(context, true);
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(
      widget.rule == null
          ? 'Create pricing rule / promotion'
          : 'Edit pricing rule / promotion',
    ),
    content: SizedBox(
      width: 600,
      child: SingleChildScrollView(
        child: Form(
          key: _form,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (_error != null)
                Text(
                  _error!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              for (final choice in _choices.entries)
                DropdownButtonFormField<String>(
                  initialValue: _values[choice.key] as String?,
                  isExpanded: true,
                  decoration: InputDecoration(labelText: choice.key),
                  items: [
                    if (choice.key == 'customerType' ||
                        choice.key == 'saleType')
                      const DropdownMenuItem<String>(
                        value: null,
                        child: Text('All'),
                      ),
                    for (final item in choice.value)
                      DropdownMenuItem(value: item, child: Text(item)),
                  ],
                  onChanged: (v) => setState(() => _values[choice.key] = v),
                ),
              for (final entry in _fields.entries)
                if (_scopeOptions.containsKey(entry.key)) PricingScopePicker(label: _labels[entry.key]!.replaceAll(' ID', ''), controller: entry.value, options: _options[_scopeOptions[entry.key]] as List<dynamic>? ?? [])
                else TextFormField(
                  controller: entry.value,
                  decoration: InputDecoration(labelText: _labels[entry.key]),
                  validator: (v) => _validate(entry.key, v),
                ),
              SwitchListTile(
                title: const Text('Active'),
                value: _values['isActive'] as bool,
                onChanged: (v) => setState(() => _values['isActive'] = v),
              ),
            ],
          ),
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: _busy ? null : () => Navigator.pop(context),
        child: const Text('Cancel'),
      ),
      FilledButton(
        onPressed: _busy ? null : _save,
        child: Text(_busy ? 'Saving…' : 'Save'),
      ),
    ],
  );
}
