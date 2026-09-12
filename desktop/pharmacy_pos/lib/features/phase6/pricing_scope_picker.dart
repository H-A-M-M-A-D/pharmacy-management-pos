import 'package:flutter/material.dart';

class PricingScopePicker extends StatelessWidget {
  const PricingScopePicker({required this.label, required this.controller, required this.options, super.key});
  final String label;
  final TextEditingController controller;
  final List<dynamic> options;
  @override
  Widget build(BuildContext context) {
    final selected = controller.text.isEmpty ? null : controller.text;
    return DropdownButtonFormField<String>(isExpanded: true, initialValue: selected, decoration: InputDecoration(labelText: label), items: [
      const DropdownMenuItem<String>(value: null, child: Text('All / default')),
      if (selected != null && !options.any((o) => o['id'] == selected)) DropdownMenuItem(value: selected, child: const Text('Current selection')),
      for (final option in options) DropdownMenuItem(value: option['id'] as String, child: Text('${option['name']}', overflow: TextOverflow.ellipsis)),
    ], onChanged: (value) => controller.text = value ?? '');
  }
}
