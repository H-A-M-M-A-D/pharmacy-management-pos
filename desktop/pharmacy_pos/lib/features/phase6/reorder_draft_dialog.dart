import 'package:flutter/material.dart';
import '../auth/auth_state.dart';

class ReorderDraftDialog extends StatefulWidget {
  const ReorderDraftDialog({
    required this.authState,
    required this.rows,
    super.key,
  });
  final AuthState authState;
  final List<dynamic> rows;
  @override
  State<ReorderDraftDialog> createState() => _ReorderDraftDialogState();
}

class _ReorderDraftDialogState extends State<ReorderDraftDialog> {
  final _form = GlobalKey<FormState>();
  late final List<TextEditingController> _suppliers;
  late final List<TextEditingController> _quantities;
  final Set<int> _selected = {};
  bool _confirmed = false;
  bool _busy = false;
  String? _error;
  List<dynamic> _supplierOptions = [];
  @override
  void initState() {
    super.initState();
    _suppliers = [
      for (final row in widget.rows)
        TextEditingController(
          text: row['preferredSupplierId'] as String? ?? '',
        ),
    ];
    _quantities = [
      for (final row in widget.rows)
        TextEditingController(text: '${row['suggestedOrderQuantity']}'),
    ];
    _loadSuppliers();
  }

  Future<void> _loadSuppliers() async {
    setState(() => _busy = true);
    try { final suppliers = await widget.authState.phase6('reorder/suppliers') as List<dynamic>; if (mounted) setState(() => _supplierOptions = suppliers); }
    catch (error) { if (mounted) setState(() => _error = error.toString()); }
    finally { if (mounted) setState(() => _busy = false); }
  }

  @override
  void dispose() {
    for (final controller in [..._suppliers, ..._quantities]) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _create() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final result = await widget.authState.phase6(
        'reorder/drafts',
        method: 'POST',
        body: {
          'confirmed': _confirmed,
          'lines': [
            for (final i in _selected)
              {
                'productId': widget.rows[i]['productId'],
                'branchId': widget.rows[i]['branchId'],
                'godownId': widget.rows[i]['godownId'],
                'supplierId': _suppliers[i].text.trim(),
                'suggestedQuantity': widget.rows[i]['suggestedOrderQuantity'],
                'finalQuantity': int.parse(_quantities[i].text),
              },
          ],
        },
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            result['duplicateSuppressed'] == true
                ? 'Existing draft orders returned; duplicate suppressed.'
                : 'Draft purchase orders created. Review in Purchasing.',
          ),
        ),
      );
      Navigator.pop(context, true);
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Generate draft purchase orders'),
    content: SizedBox(
      width: 750,
      child: SingleChildScrollView(
        child: Form(
          key: _form,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text(
                'Review supplier and quantity. Orders are grouped by supplier, branch and godown and remain Draft.',
              ),
              if (_error != null)
                Text(
                  _error!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              for (var i = 0; i < widget.rows.length; i++)
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(8),
                    child: Column(
                      children: [
                        CheckboxListTile(
                          value: _selected.contains(i),
                          onChanged: _busy
                              ? null
                              : (v) => setState(() {
                                  if (v == true) {
                                    _selected.add(i);
                                  } else {
                                    _selected.remove(i);
                                  }
                                  _confirmed = false;
                                }),
                          title: Text('${widget.rows[i]['product']}'),
                          subtitle: Text(
                            '${widget.rows[i]['branch']} / ${widget.rows[i]['godown'] ?? 'Legacy'} · suggested ${widget.rows[i]['suggestedOrderQuantity']}',
                          ),
                        ),
                        if (_selected.contains(i)) ...[
                          DropdownButtonFormField<String>(
                            isExpanded: true,
                            initialValue: _supplierOptions.any((s) => s['id'] == _suppliers[i].text) ? _suppliers[i].text : null,
                            items: [for (final supplier in _supplierOptions) DropdownMenuItem(value: supplier['id'] as String, child: Text('${supplier['name']}'))],
                            onChanged: (value) { _suppliers[i].text = value ?? ''; setState(() => _confirmed = false); },
                            decoration: InputDecoration(
                              labelText: 'Supplier',
                              helperText:
                                  widget.rows[i]['preferredSupplier'] == null
                                  ? 'Select supplier explicitly; no purchase history is available.'
                                  : 'Last purchase supplier: ${widget.rows[i]['preferredSupplier']}',
                            ),
                            validator: (v) => v == null ? 'Select a supplier' : null,
                          ),
                          TextFormField(
                            controller: _quantities[i],
                            onChanged: (_) => setState(() => _confirmed = false),
                            decoration: const InputDecoration(
                              labelText: 'Final editable quantity',
                            ),
                            validator: (v) =>
                                int.tryParse(v ?? '') == null ||
                                    int.parse(v!) <= 0
                                ? 'Enter a positive quantity'
                                : null,
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
              CheckboxListTile(
                value: _confirmed,
                onChanged: _busy
                    ? null
                    : (v) => setState(() => _confirmed = v!),
                title: const Text(
                  'I confirm the selected suppliers and quantities',
                ),
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
        onPressed: _busy || !_confirmed || _selected.isEmpty ? null : _create,
        child: Text(_busy ? 'Creating…' : 'Create Draft POs'),
      ),
    ],
  );
}
