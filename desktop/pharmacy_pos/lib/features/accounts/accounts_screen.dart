import 'package:flutter/material.dart';

import '../auth/auth_state.dart';
import 'chart_of_accounts_view.dart';
import 'journal_view.dart';
import 'parties_view.dart';
import 'statements_view.dart';

class AccountsScreen extends StatefulWidget {
  const AccountsScreen({required this.authState, super.key});
  final AuthState authState;

  @override
  State<AccountsScreen> createState() => _AccountsScreenState();
}

class _AccountsScreenState extends State<AccountsScreen> {
  var _selected = 0;

  @override
  Widget build(BuildContext context) {
    final sections = <_AccountsSection>[
      if (widget.authState.can('accounts.journal.view'))
        _AccountsSection('Dashboard', Icons.space_dashboard_outlined, AccountsDashboard(authState: widget.authState)),
      if (widget.authState.can('accounts.coa.view'))
        _AccountsSection('Chart of Accounts', Icons.account_tree_outlined, ChartOfAccountsView(authState: widget.authState)),
      if (widget.authState.can('accounts.journal.view')) ...[
        _AccountsSection('Journal / Vouchers', Icons.menu_book_outlined, JournalView(authState: widget.authState)),
        _AccountsSection('General Ledger', Icons.receipt_long_outlined, GeneralLedgerView(authState: widget.authState)),
        _AccountsSection('Trial Balance', Icons.balance_outlined, TrialBalanceView(authState: widget.authState)),
        _AccountsSection('Profit & Loss', Icons.trending_up_outlined, ProfitLossView(authState: widget.authState)),
        _AccountsSection('Balance Sheet', Icons.account_balance_outlined, BalanceSheetView(authState: widget.authState)),
      ],
      if (widget.authState.can('customers.view'))
        _AccountsSection('Receivables', Icons.people_outline, ReceivablesView(authState: widget.authState)),
      if (widget.authState.can('suppliers.view'))
        _AccountsSection('Payables', Icons.local_shipping_outlined, PayablesView(authState: widget.authState)),
    ];
    if (_selected >= sections.length) _selected = 0;
    if (sections.isEmpty) return const Center(child: Text('No accounting features are available for this user.'));

    return SafeArea(
      child: LayoutBuilder(
        builder: (context, constraints) {
          final compact = constraints.maxWidth < 980;
          return Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(24, 18, 24, 8),
                child: Row(
                  children: [
                    Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Text('Accounts', style: Theme.of(context).textTheme.headlineMedium),
                      Text('Journal-backed financial control · ${widget.authState.currentUser!.branch.name}'),
                    ])),
                    if (compact)
                      DropdownButton<int>(
                        key: const Key('accounts_section_picker'), value: _selected,
                        items: [for (var i = 0; i < sections.length; i++) DropdownMenuItem(value: i, child: Text(sections[i].label))],
                        onChanged: (value) => setState(() => _selected = value ?? 0),
                      ),
                  ],
                ),
              ),
              const Divider(height: 1),
              Expanded(
                child: Row(
                  children: [
                    if (!compact) ...[
                      SizedBox(
                        width: 210,
                        child: ListView.builder(
                          padding: const EdgeInsets.all(10), itemCount: sections.length,
                          itemBuilder: (context, index) => ListTile(
                            key: Key('accounts_nav_$index'), dense: true, selected: index == _selected,
                            leading: Icon(sections[index].icon, size: 20), title: Text(sections[index].label),
                            onTap: () => setState(() => _selected = index),
                          ),
                        ),
                      ),
                      const VerticalDivider(width: 1),
                    ],
                    Expanded(child: KeyedSubtree(key: ValueKey(sections[_selected].label), child: sections[_selected].page)),
                  ],
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _AccountsSection {
  const _AccountsSection(this.label, this.icon, this.page);
  final String label;
  final IconData icon;
  final Widget page;
}
