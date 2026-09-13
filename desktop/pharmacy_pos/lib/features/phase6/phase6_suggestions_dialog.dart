import 'package:flutter/material.dart';
import '../../ui/app_widgets.dart';
import '../auth/auth_state.dart';

class Phase6SuggestionsDialog extends StatefulWidget {
  const Phase6SuggestionsDialog({
    required this.authState,
    required this.expiry,
    this.goodsReceiptId,
    super.key,
  });
  final AuthState authState;
  final bool expiry;
  final String? goodsReceiptId;
  @override
  State<Phase6SuggestionsDialog> createState() =>
      _Phase6SuggestionsDialogState();
}

class _Phase6SuggestionsDialogState extends State<Phase6SuggestionsDialog> {
  List<dynamic> _rows = [];
  bool _busy = true;
  String? _error;
  final _discount = TextEditingController(text: '10');
  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _discount.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final data = await widget.authState.phase6(
        widget.expiry
            ? 'expiry-discount-suggestions'
            : 'purchase-cost-suggestions',
        query: widget.expiry ? {'discountPercent': _discount.text} : null,
      );
      if (mounted) {
        setState(
          () => _rows = (data as List<dynamic>)
              .where(
                (row) =>
                    widget.goodsReceiptId == null ||
                    row['goodsReceiptId'] == widget.goodsReceiptId,
              )
              .toList(),
        );
      }
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _accept(Map<String, dynamic> row) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Confirm suggested price'),
        content: Text(
          '${row['product']}: ${row['currentSellingPrice']} → ${row['suggestedPrice']} (rounded up using the configured rule).',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Accept price'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    setState(() => _busy = true);
    try {
      await widget.authState.phase6(
        'purchase-cost-suggestions/${row['id']}/accept',
        method: 'POST',
        body: {'confirmed': true},
      );
      await _load();
    } catch (error) {
      if (mounted) {
        setState(() {
          _error = error.toString();
          _busy = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(
      widget.expiry
          ? 'Expiry discount suggestions'
          : 'Purchase-cost price suggestions',
    ),
    content: SizedBox(
      width: 700,
      height: 450,
      child: Column(
        children: [
          if (widget.expiry)
            Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: _discount,
                    decoration: const InputDecoration(
                      labelText: 'Proposed discount %',
                    ),
                  ),
                ),
                TextButton(
                  onPressed: _busy ? null : _load,
                  child: const Text('Preview discount'),
                ),
              ],
            ),
          const Text(
            'Suggestions require manager review. Selling prices are never changed automatically.',
          ),
          if (_error != null)
            Text(
              _error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          if (_busy) const LinearProgressIndicator(),
          Expanded(
            child: _rows.isEmpty
                ? AppEmptyState(title: 'No price suggestions.')
                : ListView(
                    children: [
                      for (final row in _rows)
                        ListTile(
                          title: Text('${row['product']}'),
                          subtitle: Text(
                            widget.expiry
                                ? 'Batch ${row['batch']} · expires ${row['expiryDate']} · qty ${row['quantity']}\nDiscount ${row['discountPercent']}% · final price ${row['finalPrice']} · projected margin ${row['projectedMarginPercent']}%${row['belowCostGuardApplied'] == true ? '\nCost floor applied' : ''}'
                                : 'Old cost ${row['oldCost']} → new cost ${row['newCost']}\nSelling price ${row['currentSellingPrice']} · margin ${row['currentMarginPercent']}% · suggested ${row['suggestedPrice']}',
                          ),
                          trailing:
                              !widget.expiry &&
                                  widget.authState.can('pricing.manage')
                              ? TextButton(
                                  onPressed: _busy
                                      ? null
                                      : () => _accept(
                                          row as Map<String, dynamic>,
                                        ),
                                  child: const Text('Review / accept'),
                                )
                              : null,
                        ),
                    ],
                  ),
          ),
        ],
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
