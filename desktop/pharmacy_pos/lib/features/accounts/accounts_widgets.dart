import 'package:flutter/material.dart';

String money(num value) => 'PKR ${value.toStringAsFixed(2)}';
String shortDate(DateTime value) => '${value.year}-${value.month.toString().padLeft(2, '0')}-${value.day.toString().padLeft(2, '0')}';

class AccountsPageHeader extends StatelessWidget {
  const AccountsPageHeader({required this.title, required this.subtitle, this.actions = const [], super.key});
  final String title, subtitle;
  final List<Widget> actions;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(20, 18, 20, 12),
    child: Row(children: [
      Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(title, style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 2), Text(subtitle, style: Theme.of(context).textTheme.bodySmall),
      ])),
      ...actions,
    ]),
  );
}

class AccountsError extends StatelessWidget {
  const AccountsError(this.message, {this.onRetry, super.key});
  final String message;
  final VoidCallback? onRetry;
  @override
  Widget build(BuildContext context) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
    Icon(Icons.error_outline, color: Theme.of(context).colorScheme.error, size: 36),
    const SizedBox(height: 8), Text(message, textAlign: TextAlign.center),
    if (onRetry != null) ...[const SizedBox(height: 10), OutlinedButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('Retry'))],
  ]));
}

Future<DateTime?> pickAccountDate(BuildContext context, DateTime current) => showDatePicker(
  context: context, initialDate: current, firstDate: DateTime(2000), lastDate: DateTime(2100),
);

Widget horizontalTable(DataTable table) => SingleChildScrollView(
  padding: const EdgeInsets.fromLTRB(20, 8, 20, 20),
  child: SingleChildScrollView(scrollDirection: Axis.horizontal, child: table),
);
