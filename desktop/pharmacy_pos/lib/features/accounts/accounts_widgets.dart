import 'package:flutter/material.dart';
import '../../ui/app_widgets.dart';

String money(num value) => 'PKR ${value.toStringAsFixed(2)}';
String shortDate(DateTime value) => '${value.year}-${value.month.toString().padLeft(2, '0')}-${value.day.toString().padLeft(2, '0')}';

class AccountsPageHeader extends StatelessWidget {
  const AccountsPageHeader({required this.title, required this.subtitle, this.actions = const [], super.key});
  final String title, subtitle;
  final List<Widget> actions;
  @override
  Widget build(BuildContext context) => AppPageHeader(title: title, subtitle: subtitle, actions: actions);
}

class AccountsError extends StatelessWidget {
  const AccountsError(this.message, {this.onRetry, super.key});
  final String message;
  final VoidCallback? onRetry;
  @override
  Widget build(BuildContext context) => AppErrorState(message, onRetry: onRetry);
}

Future<DateTime?> pickAccountDate(BuildContext context, DateTime current) => showDatePicker(
  context: context, initialDate: current, firstDate: DateTime(2000), lastDate: DateTime(2100),
);

Widget horizontalTable(DataTable table) => SingleChildScrollView(
  padding: const EdgeInsets.fromLTRB(20, 8, 20, 20),
  child: SingleChildScrollView(scrollDirection: Axis.horizontal, child: table),
);
