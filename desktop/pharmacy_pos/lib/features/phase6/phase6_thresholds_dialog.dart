import 'package:flutter/material.dart';
import '../auth/auth_state.dart';

class Phase6ThresholdsDialog extends StatefulWidget {
  const Phase6ThresholdsDialog({required this.authState, super.key});
  final AuthState authState;
  @override
  State<Phase6ThresholdsDialog> createState() => _Phase6ThresholdsDialogState();
}

class _Phase6ThresholdsDialogState extends State<Phase6ThresholdsDialog> {
  final _form = GlobalKey<FormState>();
  final _fields = <String, TextEditingController>{};
  bool _busy = true;
  String? _error;
  static const _labels = {
    'expiryWarningDays': 'Expiry warning days',
    'slowMovingDays': 'Slow-moving days',
    'deadStockDays': 'Dead-stock days',
    'minimumMarginPercent': 'Minimum margin %',
    'defaultReorderCoverDays': 'Default reorder cover days',
    'roundingIncrement': 'Round up to increment',
  };
  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final data =
          await widget.authState.phase6('thresholds') as Map<String, dynamic>;
      if (mounted) {
        setState(() {
          for (final key in _labels.keys) {
            _fields[key] = TextEditingController(text: '${data[key]}');
          }
          _busy = false;
        });
      }
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
  void dispose() {
    for (final field in _fields.values) {
      field.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.authState.phase6(
        'thresholds',
        method: 'PUT',
        body: {
          for (final entry in _fields.entries)
            entry.key: entry.key.endsWith('Days')
                ? int.parse(entry.value.text)
                : num.parse(entry.value.text),
        },
      );
      if (mounted) Navigator.pop(context, true);
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
    title: const Text('Phase 6 thresholds'),
    content: SizedBox(
      width: 480,
      child: SingleChildScrollView(
        child: Form(
          key: _form,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (_error != null) Text(_error!),
              if (_busy) const LinearProgressIndicator(),
              for (final entry in _fields.entries)
                TextFormField(
                  controller: entry.value,
                  decoration: InputDecoration(labelText: _labels[entry.key]),
                  validator: (v) =>
                      (entry.key.endsWith('Days')
                              ? int.tryParse(v ?? '')
                              : num.tryParse(v ?? '')) ==
                          null
                      ? 'Enter a valid number'
                      : null,
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
        onPressed: _busy || _fields.isEmpty ? null : _save,
        child: const Text('Save thresholds'),
      ),
    ],
  );
}
