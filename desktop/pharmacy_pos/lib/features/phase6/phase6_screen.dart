import 'package:flutter/material.dart';
import '../../ui/app_widgets.dart';

import '../auth/auth_state.dart';
import 'bulk_pricing_dialog.dart';
import 'pricing_rule_dialog.dart';
import 'reorder_draft_dialog.dart';
import 'phase6_suggestions_dialog.dart';
import 'phase6_thresholds_dialog.dart';

class Phase6Screen extends StatefulWidget {
  const Phase6Screen({required this.authState, super.key});

  final AuthState authState;

  @override
  State<Phase6Screen> createState() => _Phase6ScreenState();
}

class _Phase6ScreenState extends State<Phase6Screen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;
  bool _loading = true;
  String? _error;
  List<dynamic> _rules = const [];
  List<dynamic> _reorder = const [];
  List<dynamic> _alerts = const [];
  List<dynamic> _automation = const [];
  List<dynamic> _slow = const [];
  String? _automationResult;
  final _minimumStockValue = TextEditingController(text: '0');

  bool can(String permission) => widget.authState.can(permission);

  @override
  void initState() {
    super.initState();
    _tabs = TabController(
      length: [
        'pricing.view',
        'inventory.reorder.view',
        'alerts.view',
        'alerts.view',
        'automation.view',
      ].where(can).length,
      vsync: this,
    );
    _load();
  }

  @override
  void dispose() {
    _tabs.dispose();
    _minimumStockValue.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait<dynamic>([
        can('pricing.view')
            ? widget.authState.phase6(
                'pricing-rules',
                query: {'activeOnly': 'false'},
              )
            : Future<dynamic>.value(const []),
        can('inventory.reorder.view')
            ? widget.authState.phase6('reorder')
            : Future<dynamic>.value(const []),
        can('alerts.view')
            ? widget.authState.phase6('alerts')
            : Future<dynamic>.value(const []),
        can('automation.view')
            ? widget.authState.phase6('automation')
            : Future<dynamic>.value(const []),
        can('alerts.view')
            ? widget.authState.phase6(
                'slow-stock',
                query: {'minimumStockValue': _minimumStockValue.text},
              )
            : Future<dynamic>.value(const []),
      ]);
      if (!mounted) return;
      setState(() {
        _rules = results[0] as List<dynamic>;
        _reorder = results[1] as List<dynamic>;
        _alerts = results[2] as List<dynamic>;
        _automation = results[3] as List<dynamic>;
        _slow = results[4] as List<dynamic>;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = error.toString();
      });
    }
  }

  Future<void> _refreshAlerts() async {
    await widget.authState.phase6('alerts/refresh', method: 'POST');
    await _load();
  }

  Future<void> _runAutomation() async {
    try {
      final result = await widget.authState.phase6(
        'automation/run',
        method: 'POST',
      );
      if (mounted) {
        setState(
          () => _automationResult =
              'Executed ${result['executedRules']} · alerts ${result['alertsCreated']} · review flags ${result['reviewFlagsCreated']} · draft POs ${(result['draftPurchaseOrderIds'] as List<dynamic>).length} · duplicates suppressed ${result['duplicateRulesSuppressed']}',
        );
      }
      await _load();
    } catch (error) {
      if (mounted) setState(() => _error = error.toString());
    }
  }

  Future<void> _dismiss(String id) async {
    await widget.authState.phase6('alerts/$id/dismiss', method: 'POST');
    await _load();
  }

  Future<void> _editRule(Map<String, dynamic>? rule) async {
    final changed = await showDialog<bool>(
      context: context,
      builder: (_) =>
          PricingRuleDialog(authState: widget.authState, rule: rule),
    );
    if (changed == true) await _load();
  }

  @override
  Widget build(BuildContext context) {
    final tabs = <Widget>[
      if (can('pricing.view')) const Tab(text: 'Pricing rules'),
      if (can('inventory.reorder.view')) const Tab(text: 'Reorder'),
      if (can('alerts.view')) const Tab(text: 'Alerts'),
      if (can('alerts.view')) const Tab(text: 'Slow / dead stock'),
      if (can('automation.view')) const Tab(text: 'Automation'),
    ];
    final views = <Widget>[
      if (can('pricing.view'))
        _PricingTab(
          rules: _rules,
          onEdit: can('pricing.manage') ? _editRule : null,
        ),
      if (can('inventory.reorder.view'))
        Column(
          children: [
            if (can('purchase_orders.create'))
              Align(
                alignment: Alignment.centerRight,
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: FilledButton(
                    onPressed: _reorder.isEmpty
                        ? null
                        : () async {
                            final changed = await showDialog<bool>(
                              context: context,
                              builder: (_) => ReorderDraftDialog(
                                authState: widget.authState,
                                rows: _reorder,
                              ),
                            );
                            if (changed == true) await _load();
                          },
                    child: const Text('Generate Draft POs'),
                  ),
                ),
              ),
            Expanded(child: _ReorderTab(rows: _reorder)),
          ],
        ),
      if (can('alerts.view'))
        _AlertsTab(
          alerts: _alerts,
          canManage: can('alerts.manage'),
          onRefresh: can('alerts.manage') ? _refreshAlerts : null,
          onDismiss: _dismiss,
        ),
      if (can('alerts.view'))
        Column(
          children: [
            Padding(
              padding: const EdgeInsets.all(12),
              child: Row(
                children: [
                  SizedBox(
                    width: 240,
                    child: TextField(
                      controller: _minimumStockValue,
                      decoration: const InputDecoration(
                        labelText: 'Minimum stock value',
                      ),
                    ),
                  ),
                  TextButton(
                    onPressed: _load,
                    child: const Text('Filter stock'),
                  ),
                ],
              ),
            ),
            Expanded(
              child: _DataList(
                empty: 'No slow or dead stock matches the thresholds.',
                rows: _slow,
                icon: Icons.hourglass_empty,
                title: (row) => '${row['product']} · ${row['status']}',
                detail: (row) =>
                    '${row['branch']} / ${row['godown']} · qty ${row['currentQuantity']} · value ${row['stockValue']}\nLast sale ${row['lastSaleDate'] ?? 'never'} · days since sale or receipt ${row['daysSinceLastSale']}',
              ),
            ),
          ],
        ),
      if (can('automation.view'))
        Column(
          children: [
            if (_automationResult != null)
              Padding(
                padding: const EdgeInsets.all(12),
                child: Text(_automationResult!),
              ),
            Expanded(
              child: _AutomationTab(
                rows: _automation,
                canRun: can('automation.run'),
                onRun: can('automation.run') ? _runAutomation : null,
              ),
            ),
          ],
        ),
    ];
    return Scaffold(
      appBar: AppBar(
        title: const Text('Business automation'),
        actions: [
          if (can('pricing.suggest') &&
              (can('sales.cost_view') || can('reports.profitability')))
            PopupMenuButton<bool>(
              tooltip: 'Price suggestions',
              onSelected: (expiry) => showDialog<void>(
                context: context,
                builder: (_) => Phase6SuggestionsDialog(
                  authState: widget.authState,
                  expiry: expiry,
                ),
              ),
              itemBuilder: (_) => const [
                PopupMenuItem(
                  value: false,
                  child: Text('Purchase-cost suggestions'),
                ),
                PopupMenuItem(
                  value: true,
                  child: Text('Expiry discount suggestions'),
                ),
              ],
            ),
          if (can('system.settings.manage') && can('alerts.view'))
            IconButton(
              tooltip: 'Phase 6 thresholds',
              icon: const Icon(Icons.tune),
              onPressed: () async {
                final changed = await showDialog<bool>(
                  context: context,
                  builder: (_) =>
                      Phase6ThresholdsDialog(authState: widget.authState),
                );
                if (changed == true) await _load();
              },
            ),
          if (can('pricing.manage'))
            TextButton(
              onPressed: () async {
                final changed = await showDialog<bool>(
                  context: context,
                  builder: (_) =>
                      BulkPricingDialog(authState: widget.authState),
                );
                if (changed == true) await _load();
              },
              child: const Text('Bulk pricing'),
            ),
          IconButton(
            tooltip: 'Refresh',
            onPressed: _loading ? null : _load,
            icon: const Icon(Icons.refresh),
          ),
        ],
        bottom: tabs.isEmpty
            ? null
            : TabBar(controller: _tabs, tabs: tabs, isScrollable: true),
      ),
      body: _loading
          ? const AppLoadingState()
          : _error != null
          ? _ErrorState(message: _error!, onRetry: _load)
          : views.isEmpty
          ? AppEmptyState(title: 'No Phase 6 permissions assigned.')
          : TabBarView(controller: _tabs, children: views),
    );
  }
}

class _PricingTab extends StatefulWidget {
  const _PricingTab({required this.rules, this.onEdit});
  final List<dynamic> rules;
  final Future<void> Function(Map<String, dynamic>?)? onEdit;

  @override
  State<_PricingTab> createState() => _PricingTabState();
}

class _PricingTabState extends State<_PricingTab> {
  String _kind = 'All';
  Future<void> Function(Map<String, dynamic>?)? get onEdit => widget.onEdit;
  List<dynamic> get rules => widget.rules.where((rule) => _kind == 'All' || rule['kind'] == _kind).toList();

  @override
  Widget build(BuildContext context) => Column(
    children: [
      Padding(padding: const EdgeInsets.all(12), child: SegmentedButton<String>(segments: const [ButtonSegment(value: 'All', label: Text('All pricing')), ButtonSegment(value: 'Rule', label: Text('Rules')), ButtonSegment(value: 'Promotion', label: Text('Promotions'))], selected: {_kind}, onSelectionChanged: (v) => setState(() => _kind = v.single))),
      if (onEdit != null)
        Align(
          alignment: Alignment.centerRight,
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: FilledButton(
              onPressed: () => onEdit!(null),
              child: const Text('Create rule / promotion'),
            ),
          ),
        ),
      Expanded(
        child: _DataList(
          empty:
              'No pricing rules configured. Existing price levels remain active.',
          rows: rules,
          icon: Icons.sell_outlined,
          title: (row) => '${row['name']} · ${row['kind']}',
          detail: (row) =>
              'Priority ${row['priority']} · ${row['adjustmentType']} ${row['adjustmentValue']} · ${row['isActive'] == true ? 'Active' : 'Inactive'}',
          trailing: onEdit == null
              ? null
              : (row) => IconButton(
                  tooltip: 'Edit rule / promotion',
                  onPressed: () => onEdit!(row),
                  icon: const Icon(Icons.edit),
                ),
        ),
      ),
    ],
  );
}

class _ReorderTab extends StatelessWidget {
  const _ReorderTab({required this.rows});
  final List<dynamic> rows;

  @override
  Widget build(BuildContext context) => _DataList(
    empty: 'No products currently need replenishment.',
    rows: rows,
    icon: Icons.inventory_2_outlined,
    title: (row) => '${row['product']} · ${row['sku']}',
    detail: (row) =>
        '${row['risk']} · stock ${row['currentStock']} · reorder ${row['reorderLevel']} · suggested ${row['suggestedOrderQuantity']}',
  );
}

class _AlertsTab extends StatefulWidget {
  const _AlertsTab({
    required this.alerts,
    required this.canManage,
    required this.onRefresh,
    required this.onDismiss,
  });
  final List<dynamic> alerts;
  final bool canManage;
  final Future<void> Function()? onRefresh;
  final Future<void> Function(String id) onDismiss;

  @override
  State<_AlertsTab> createState() => _AlertsTabState();
}

class _AlertsTabState extends State<_AlertsTab> {
  String? _category;
  String? _severity;
  bool _showDismissed = false;
  String _search = '';
  bool get canManage => widget.canManage;
  Future<void> Function()? get onRefresh => widget.onRefresh;
  Future<void> Function(String) get onDismiss => widget.onDismiss;
  List<dynamic> get alerts => widget.alerts
      .where(
        (row) =>
            (_category == null || row['category'] == _category) &&
            (_severity == null || row['severity'] == _severity) &&
            (_showDismissed || row['dismissed'] != true) &&
            '${row['title']} ${row['description']} ${row['sourceType']} ${row['branchId']} ${row['godownId']}'
                .toLowerCase()
                .contains(_search.toLowerCase()),
      )
      .toList();

  @override
  Widget build(BuildContext context) => Column(
    children: [
      Padding(
        padding: const EdgeInsets.all(12),
        child: Wrap(
          spacing: 12,
          runSpacing: 8,
          children: [
            SizedBox(
              width: 180,
              child: DropdownButtonFormField<String>(
                isExpanded: true,
                initialValue: _category,
                decoration: const InputDecoration(labelText: 'Alert category'),
                items: [
                  const DropdownMenuItem<String>(
                    value: null,
                    child: Text('All categories'),
                  ),
                  for (final category in [
                    'Inventory',
                    'Expiry',
                    'Pricing',
                    'Receivables',
                    'Payables',
                    'Purchasing',
                    'System',
                  ])
                    DropdownMenuItem(value: category, child: Text(category)),
                ],
                onChanged: (v) => setState(() => _category = v),
              ),
            ),
            SizedBox(
              width: 180,
              child: DropdownButtonFormField<String>(
                isExpanded: true,
                initialValue: _severity,
                decoration: const InputDecoration(labelText: 'Alert severity'),
                items: [
                  const DropdownMenuItem<String>(
                    value: null,
                    child: Text('All severities'),
                  ),
                  for (final severity in ['Info', 'Warning', 'Critical'])
                    DropdownMenuItem(value: severity, child: Text(severity)),
                ],
                onChanged: (v) => setState(() => _severity = v),
              ),
            ),
            SizedBox(
              width: 280,
              child: TextField(
                decoration: const InputDecoration(
                  labelText: 'Search alerts / location',
                ),
                onChanged: (v) => setState(() => _search = v),
              ),
            ),
            SizedBox(
              width: 220,
              child: CheckboxListTile(
                value: _showDismissed,
                onChanged: (v) => setState(() => _showDismissed = v!),
                title: const Text('Show dismissed'),
              ),
            ),
          ],
        ),
      ),
      if (canManage)
        Align(
          alignment: Alignment.centerRight,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
            child: FilledButton.icon(
              onPressed: onRefresh,
              icon: const Icon(Icons.sync),
              label: const Text('Refresh alerts'),
            ),
          ),
        ),
      Expanded(
        child: _DataList(
          empty: 'No active business alerts.',
          rows: alerts,
          icon: Icons.notifications_none,
          title: (row) => '${row['severity']} · ${row['title']}',
          detail: (row) => row['description'] as String? ?? '',
          trailing: canManage
              ? (row) => IconButton(
                  tooltip: 'Dismiss',
                  icon: const Icon(Icons.done),
                  onPressed: () => onDismiss(row['id'] as String),
                )
              : null,
        ),
      ),
    ],
  );
}

class _AutomationTab extends StatelessWidget {
  const _AutomationTab({
    required this.rows,
    required this.canRun,
    required this.onRun,
  });
  final List<dynamic> rows;
  final bool canRun;
  final Future<void> Function()? onRun;

  @override
  Widget build(BuildContext context) => Column(
    children: [
      if (canRun)
        Align(
          alignment: Alignment.centerRight,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
            child: FilledButton.icon(
              onPressed: onRun,
              icon: const Icon(Icons.play_arrow),
              label: const Text('Run automation'),
            ),
          ),
        ),
      Expanded(
        child: _DataList(
          empty: 'No automation rules configured.',
          rows: rows,
          icon: Icons.auto_awesome_outlined,
          title: (row) => '${row['name']} · ${row['triggerType']}',
          detail: (row) =>
              '${row['actionType']} · ${row['isActive'] == true ? 'Active' : 'Inactive'}',
        ),
      ),
    ],
  );
}

class _DataList extends StatelessWidget {
  const _DataList({
    required this.empty,
    required this.rows,
    required this.icon,
    required this.title,
    required this.detail,
    this.trailing,
  });
  final String empty;
  final List<dynamic> rows;
  final IconData icon;
  final String Function(Map<String, dynamic>) title;
  final String Function(Map<String, dynamic>) detail;
  final Widget Function(Map<String, dynamic>)? trailing;

  @override
  Widget build(BuildContext context) {
    if (rows.isEmpty) return Center(child: Text(empty));
    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: rows.length,
      separatorBuilder: (_, _) => const Divider(height: 1),
      itemBuilder: (context, index) {
        final row = rows[index] as Map<String, dynamic>;
        return ListTile(
          leading: Icon(icon),
          title: Text(title(row)),
          subtitle: Text(detail(row)),
          trailing: trailing?.call(row),
        );
      },
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
    child: Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(message, textAlign: TextAlign.center),
        const SizedBox(height: 12),
        OutlinedButton(onPressed: onRetry, child: const Text('Retry')),
      ],
    ),
  );
}
