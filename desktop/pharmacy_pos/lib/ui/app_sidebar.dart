import 'package:flutter/material.dart';
import 'app_theme.dart';

class AppSidebar extends StatelessWidget {
  const AppSidebar({
    required this.destinations,
    required this.selectedIndex,
    required this.expanded,
    required this.onSelected,
    required this.onToggle,
    required this.onLogout,
    super.key,
  });
  final List<NavigationRailDestination> destinations;
  final int selectedIndex;
  final bool expanded;
  final ValueChanged<int> onSelected;
  final VoidCallback onToggle, onLogout;
  @override
  Widget build(BuildContext context) => AnimatedContainer(
    duration: const Duration(milliseconds: 120),
    width: expanded ? 232 : 104,
    color: AppColors.sidebar,
    child: Column(
      children: [
        SizedBox(
          height: 64,
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(
                Icons.local_pharmacy_outlined,
                color: Color(0xFF8FD4B6),
                size: 28,
              ),
              if (expanded) ...[
                const SizedBox(width: 10),
                const Text(
                  'PHARMACY POS',
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ],
          ),
        ),
        Expanded(
          child: ListView(
            key: const Key('app_sidebar_scroll'),
            padding: const EdgeInsets.symmetric(horizontal: 8),
            children: [
              for (var i = 0; i < destinations.length; i++)
                if (destinations[i].disabled)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 10),
                    child: DefaultTextStyle.merge(
                      style: const TextStyle(
                        color: Color(0xFF9CB5AA),
                        fontSize: 10,
                      ),
                      child: destinations[i].label,
                    ),
                  )
                else
                  _item(context, i),
            ],
          ),
        ),
        const Divider(color: Color(0xFF294439)),
        Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            IconButton(
              tooltip: expanded ? 'Collapse sidebar' : 'Expand sidebar',
              onPressed: onToggle,
              icon: Icon(
                expanded ? Icons.chevron_left : Icons.chevron_right,
                color: Colors.white,
              ),
            ),
            IconButton(
              tooltip: 'Sign out',
              onPressed: onLogout,
              icon: const Icon(Icons.logout, color: Colors.white, size: 20),
            ),
          ],
        ),
      ],
    ),
  );
  Widget _item(BuildContext context, int i) {
    final selected = i == selectedIndex;
    final label = DefaultTextStyle(
      style: Theme.of(context).textTheme.bodyMedium!.copyWith(
        color: selected ? Colors.white : const Color(0xFFCCD9D2),
        fontSize: expanded ? 13 : 11,
        fontWeight: selected ? FontWeight.w600 : FontWeight.w400,
      ),
      child: destinations[i].label,
    );
    final content = expanded
        ? Row(
            children: [
              destinations[i].icon,
              const SizedBox(width: 12),
              Expanded(child: label),
            ],
          )
        : Column(
            children: [destinations[i].icon, const SizedBox(height: 3), label],
          );
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Semantics(
        selected: selected,
        button: true,
        child: Material(
          color: selected ? const Color(0xFF214A3B) : Colors.transparent,
          borderRadius: BorderRadius.circular(8),
          child: InkWell(
            borderRadius: BorderRadius.circular(8),
            onTap: () => onSelected(i),
            hoverColor: const Color(0xFF193E31),
            child: Padding(
              padding: EdgeInsets.symmetric(
                horizontal: expanded ? 12 : 4,
                vertical: expanded ? 10 : 7,
              ),
              child: IconTheme(
                data: IconThemeData(
                  color: selected
                      ? const Color(0xFFA4E1C4)
                      : const Color(0xFFABC0B5),
                  size: 20,
                ),
                child: content,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
