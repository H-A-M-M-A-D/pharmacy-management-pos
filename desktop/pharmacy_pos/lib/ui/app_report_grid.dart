import 'package:flutter/material.dart';
import 'app_theme.dart';

/// Presentation of loaded report rows only; never recalculates report facts.
class AppReportGrid extends StatefulWidget {
  const AppReportGrid({
    required this.rows,
    required this.columns,
    required this.label,
    required this.display,
    super.key,
  });
  final List<Map<String, dynamic>> rows;
  final List<String> columns;
  final String Function(String) label;
  final String Function(dynamic) display;
  @override
  State<AppReportGrid> createState() => _AppReportGridState();
}

class _AppReportGridState extends State<AppReportGrid> {
  int _page = 0;
  String? _sort;
  bool _ascending = true;
  static const _pageSize = 25;
  @override
  void didUpdateWidget(covariant AppReportGrid oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (!identical(oldWidget.rows, widget.rows)) _page = 0;
  }

  @override
  Widget build(BuildContext context) {
    final rows = [...widget.rows];
    if (_sort != null) {
      rows.sort((a, b) {
        final left = a[_sort], right = b[_sort];
        final value = left is num && right is num
            ? left.compareTo(right)
            : '${left ?? ''}'.compareTo('${right ?? ''}');
        return _ascending ? value : -value;
      });
    }
    final start = _page * _pageSize;
    final visible = rows.skip(start).take(_pageSize);
    return LayoutBuilder(
      builder: (context, bounds) {
        final width = (bounds.maxWidth / widget.columns.length).clamp(
          150.0,
          240.0,
        );
        return Container(
          decoration: BoxDecoration(
            color: Colors.white,
            border: Border.all(color: AppColors.border),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Column(
            children: [
              Expanded(
                child: SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: SizedBox(
                    width: width * widget.columns.length,
                    child: Column(
                      children: [
                        Container(
                          height: 42,
                          color: const Color(0xFFF0F4F2),
                          child: Row(
                            children: [
                              for (final column in widget.columns)
                                SizedBox(
                                  width: width,
                                  child: InkWell(
                                    onTap: () => setState(() {
                                      _ascending = _sort == column
                                          ? !_ascending
                                          : true;
                                      _sort = column;
                                      _page = 0;
                                    }),
                                    child: Padding(
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 12,
                                      ),
                                      child: Row(
                                        children: [
                                          Expanded(
                                            child: Text(
                                              widget.label(column),
                                              style: Theme.of(
                                                context,
                                              ).textTheme.labelSmall,
                                              overflow: TextOverflow.ellipsis,
                                            ),
                                          ),
                                          if (_sort == column)
                                            Icon(
                                              _ascending
                                                  ? Icons.arrow_upward
                                                  : Icons.arrow_downward,
                                              size: 14,
                                            ),
                                        ],
                                      ),
                                    ),
                                  ),
                                ),
                            ],
                          ),
                        ),
                        Expanded(
                          child: ListView(
                            children: [
                              for (final row in visible)
                                _ReportRow(
                                  child: Row(
                                    children: [
                                      for (final column in widget.columns)
                                        SizedBox(
                                          width: width,
                                          child: Padding(
                                            padding: const EdgeInsets.symmetric(
                                              horizontal: 12,
                                            ),
                                            child: Tooltip(
                                              message: widget.display(
                                                row[column],
                                              ),
                                              child: Text(
                                                widget.display(row[column]),
                                                maxLines: 2,
                                                overflow: TextOverflow.ellipsis,
                                                textAlign: row[column] is num
                                                    ? TextAlign.end
                                                    : TextAlign.start,
                                                style: Theme.of(context)
                                                    .textTheme
                                                    .bodySmall
                                                    ?.copyWith(
                                                      color: AppColors.text,
                                                    ),
                                              ),
                                            ),
                                          ),
                                        ),
                                    ],
                                  ),
                                ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
              SizedBox(
                height: 44,
                child: Row(
                  children: [
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        'Loaded rows ${rows.isEmpty ? 0 : start + 1}–${(start + _pageSize).clamp(0, rows.length)} of ${rows.length}',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ),
                    IconButton(
                      tooltip: 'Previous page',
                      onPressed: _page == 0
                          ? null
                          : () => setState(() => _page--),
                      icon: const Icon(Icons.chevron_left),
                    ),
                    IconButton(
                      tooltip: 'Next page',
                      onPressed: start + _pageSize >= rows.length
                          ? null
                          : () => setState(() => _page++),
                      icon: const Icon(Icons.chevron_right),
                    ),
                  ],
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _ReportRow extends StatefulWidget {
  const _ReportRow({required this.child});
  final Widget child;
  @override
  State<_ReportRow> createState() => _ReportRowState();
}

class _ReportRowState extends State<_ReportRow> {
  bool _hover = false;
  @override
  Widget build(BuildContext context) => MouseRegion(
    onEnter: (_) => setState(() => _hover = true),
    onExit: (_) => setState(() => _hover = false),
    child: Container(
      height: 44,
      decoration: BoxDecoration(
        color: _hover ? const Color(0xFFF5F8F6) : Colors.white,
        border: const Border(bottom: BorderSide(color: AppColors.border)),
      ),
      child: widget.child,
    ),
  );
}
