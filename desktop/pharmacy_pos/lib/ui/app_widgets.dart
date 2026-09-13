import 'package:flutter/material.dart';
import 'app_theme.dart';

class AppPageHeader extends StatelessWidget {
  const AppPageHeader({
    required this.title,
    this.subtitle,
    this.actions = const [],
    super.key,
  });
  final String title;
  final String? subtitle;
  final List<Widget> actions;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(20, 20, 20, 16),
    child: Row(
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: Theme.of(context).textTheme.headlineSmall),
              if (subtitle != null) ...[
                const SizedBox(height: 4),
                Text(subtitle!, style: Theme.of(context).textTheme.bodySmall),
              ],
            ],
          ),
        ),
        if (actions.isNotEmpty)
          Flexible(
            child: Align(
              alignment: Alignment.centerRight,
              child: Wrap(spacing: 8, runSpacing: 8, children: actions),
            ),
          ),
      ],
    ),
  );
}

class AppEmptyState extends StatelessWidget {
  const AppEmptyState({
    required this.title,
    this.message = 'Adjust your filters or add a record to get started.',
    this.icon = Icons.inbox_outlined,
    this.action,
    super.key,
  });
  final String title, message;
  final IconData icon;
  final Widget? action;
  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 28, color: AppColors.muted),
          const SizedBox(height: 10),
          Text(
            title,
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.titleSmall,
          ),
          const SizedBox(height: 4),
          Text(
            message,
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodySmall,
          ),
          if (action != null) ...[const SizedBox(height: 12), action!],
        ],
      ),
    ),
  );
}

class AppLoadingState extends StatelessWidget {
  const AppLoadingState({super.key});
  @override
  Widget build(BuildContext context) => const Center(
    child: SizedBox(
      width: 24,
      height: 24,
      child: CircularProgressIndicator(strokeWidth: 2),
    ),
  );
}

class AppErrorState extends StatelessWidget {
  const AppErrorState(this.message, {this.onRetry, super.key});
  final String message;
  final VoidCallback? onRetry;
  @override
  Widget build(BuildContext context) => AppEmptyState(
    title: message,
    message:
        'Please retry. If the issue continues, contact your administrator.',
    icon: Icons.error_outline,
    action: onRetry == null
        ? null
        : OutlinedButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh),
            label: const Text('Retry'),
          ),
  );
}

class AppStatusChip extends StatelessWidget {
  const AppStatusChip(this.label, {super.key});
  final String label;
  @override
  Widget build(BuildContext context) {
    final value = label.toLowerCase();
    final color =
        [
          'expired',
          'cancelled',
          'overdue',
          'failed',
          'out of stock',
        ].any(value.contains)
        ? AppColors.dangerText
        : [
            'pending',
            'low stock',
            'near expiry',
            'submitted',
          ].any(value.contains)
        ? AppColors.warning
        : [
            'paid',
            'completed',
            'active',
            'in stock',
            'posted',
            'received',
          ].any((x) => value == x)
        ? AppColors.successText
        : AppColors.informationText;
    return Semantics(
      label: 'Status: $label',
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        decoration: BoxDecoration(
          color: color.withValues(alpha: .08),
          border: Border.all(color: color.withValues(alpha: .2)),
          borderRadius: BorderRadius.circular(5),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w600,
            color: color,
          ),
        ),
      ),
    );
  }
}

/// Retains Flutter's sorting, row selection and semantics, with shared desktop density.
class AppDataTable extends DataTable {
  AppDataTable({
    required super.columns,
    required super.rows,
    super.key,
    super.sortColumnIndex,
    super.sortAscending,
    super.onSelectAll,
    super.showCheckboxColumn,
    super.columnSpacing,
    super.horizontalMargin,
    super.headingRowHeight,
    super.dataRowMinHeight,
    super.dataRowMaxHeight,
    super.headingTextStyle,
    super.dataTextStyle,
    super.border,
    super.decoration,
    super.dividerThickness,
    super.headingRowColor,
    super.dataRowColor,
    super.showBottomBorder,
  });
}

class AppSectionCard extends StatelessWidget {
  const AppSectionCard({
    required this.child,
    this.padding = const EdgeInsets.all(16),
    super.key,
  });
  final Widget child;
  final EdgeInsetsGeometry padding;
  @override
  Widget build(BuildContext context) => Card(
    child: Padding(padding: padding, child: child),
  );
}

class AppFilterBar extends StatelessWidget {
  const AppFilterBar({required this.children, super.key});
  final List<Widget> children;
  @override
  Widget build(BuildContext context) => SizedBox(
    width: double.infinity,
    child: AppSectionCard(
      child: Wrap(
        spacing: 12,
        runSpacing: 12,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: children,
      ),
    ),
  );
}

class AppStatCard extends StatelessWidget {
  const AppStatCard({
    required this.label,
    required this.value,
    required this.icon,
    this.compact = false,
    super.key,
  });
  final String label, value;
  final IconData icon;
  final bool compact;
  @override
  Widget build(BuildContext context) => AppSectionCard(
    padding: const EdgeInsets.all(14),
    child: SizedBox(
      height: compact ? 62 : 82,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            children: [
              Icon(icon, size: 18, color: AppColors.muted),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  label,
                  style: Theme.of(context).textTheme.bodySmall,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
          Tooltip(
            message: value,
            child: Text(
              value,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: compact ? 17 : 23,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    ),
  );
}

class AppWorkflowStrip extends StatelessWidget {
  const AppWorkflowStrip({required this.steps, super.key});
  final List<String> steps;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 16),
    child: Wrap(
      spacing: 8,
      runSpacing: 8,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        for (var i = 0; i < steps.length; i++) ...[
          if (i > 0)
            const Icon(Icons.chevron_right, size: 16, color: AppColors.muted),
          Text(
            '${i + 1}. ${steps[i]}',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ],
    ),
  );
}

/// Compact desktop fields; falls back to one column for focused narrow dialogs.
class AppFormGrid extends StatelessWidget {
  const AppFormGrid({required this.children, super.key});
  final List<Widget> children;
  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) {
      final columns = constraints.maxWidth >= 560 ? 2 : 1;
      final width = (constraints.maxWidth - (columns - 1) * 12) / columns;
      return Wrap(
        spacing: 12,
        runSpacing: 12,
        children: [
          for (final field in children) SizedBox(width: width, child: field),
        ],
      );
    },
  );
}
