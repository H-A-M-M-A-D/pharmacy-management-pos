import 'dart:async';

import 'package:flutter/material.dart';
import '../../ui/app_widgets.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../auth/auth_state.dart';

enum CatalogMasterMode { categories, manufacturers }

class CatalogMastersScreen extends StatefulWidget {
  const CatalogMastersScreen({
    required this.authState,
    required this.mode,
    super.key,
  });
  final AuthState authState;
  final CatalogMasterMode mode;
  @override
  State<CatalogMastersScreen> createState() => _CatalogMastersScreenState();
}

class _CatalogMastersScreenState extends State<CatalogMastersScreen> {
  final _search = TextEditingController();
  Timer? _debounce;
  List<CatalogLookup> _items = const [];
  bool _loading = true;
  bool? _active;
  String? _error;
  bool get _categories => widget.mode == CatalogMasterMode.categories;
  bool get _manage => widget.authState.can(
    _categories ? 'categories.manage' : 'manufacturers.manage',
  );
  String get _title => _categories ? 'Categories' : 'Manufacturers';
  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      _items = _categories
          ? await widget.authState.listCategories(
              search: _search.text,
              isActive: _active,
            )
          : await widget.authState.listManufacturers(
              search: _search.text,
              isActive: _active,
            );
    } on ApiException catch (error) {
      _error = error.message;
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _edit([CatalogLookup? item]) async {
    final changed = await showDialog<bool>(
      context: context,
      builder: (_) => _MasterDialog(
        authState: widget.authState,
        mode: widget.mode,
        item: item,
      ),
    );
    if (changed == true) _load();
  }

  Future<void> _toggle(CatalogLookup item) async {
    final active = !item.isActive;
    final approved = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('${active ? 'Activate' : 'Deactivate'} ${item.name}?'),
        content: Text(
          active
              ? 'This record will be available for product selection.'
              : 'Existing products retain this relationship, but it cannot be selected for new products.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: Text(active ? 'Activate' : 'Deactivate'),
          ),
        ],
      ),
    );
    if (approved != true) return;
    if (_categories) {
      await widget.authState.setCategoryActive(item.id, active);
    } else {
      await widget.authState.setManufacturerActive(item.id, active);
    }
    _load();
  }

  @override
  Widget build(BuildContext context) => SafeArea(
    child: Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 18,
            runSpacing: 12,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Text(_title, style: Theme.of(context).textTheme.headlineMedium),
              if (_manage)
                FilledButton.icon(
                  key: Key(_categories ? 'add_category' : 'add_manufacturer'),
                  onPressed: () => _edit(),
                  icon: const Icon(Icons.add),
                  label: Text(
                    'Add ${_categories ? 'Category' : 'Manufacturer'}',
                  ),
                ),
            ],
          ),
          const SizedBox(height: 18),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              SizedBox(
                width: 300,
                child: TextField(
                  controller: _search,
                  onChanged: (_) {
                    _debounce?.cancel();
                    _debounce = Timer(const Duration(milliseconds: 350), _load);
                  },
                  decoration: InputDecoration(
                    labelText: 'Search $_title',
                    prefixIcon: const Icon(Icons.search),
                  ),
                ),
              ),
              SizedBox(
                width: 170,
                child: DropdownButtonFormField<bool>(
                  initialValue: _active,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Status'),
                  items: const [
                    DropdownMenuItem(value: null, child: Text('All')),
                    DropdownMenuItem(value: true, child: Text('Active')),
                    DropdownMenuItem(value: false, child: Text('Inactive')),
                  ],
                  onChanged: (value) {
                    setState(() => _active = value);
                    _load();
                  },
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Expanded(
            child: _loading
                ? const AppLoadingState()
                : _error != null
                ? Center(child: Text(_error!))
                : _items.isEmpty
                ? AppEmptyState(title: 'No ${_title.toLowerCase()} found.')
                : ListView.separated(
                    itemCount: _items.length,
                    separatorBuilder: (_, _) => const Divider(height: 1),
                    itemBuilder: (_, index) {
                      final item = _items[index];
                      return ListTile(
                        title: Text(item.name),
                        subtitle: Text(item.isActive ? 'Active' : 'Inactive'),
                        trailing: _manage
                            ? Wrap(
                                children: [
                                  IconButton(
                                    tooltip: 'Edit',
                                    onPressed: () => _edit(item),
                                    icon: const Icon(Icons.edit_outlined),
                                  ),
                                  IconButton(
                                    tooltip: item.isActive
                                        ? 'Deactivate'
                                        : 'Activate',
                                    onPressed: () => _toggle(item),
                                    icon: Icon(
                                      item.isActive
                                          ? Icons.block
                                          : Icons.check_circle_outline,
                                    ),
                                  ),
                                ],
                              )
                            : null,
                      );
                    },
                  ),
          ),
        ],
      ),
    ),
  );
}

class _MasterDialog extends StatefulWidget {
  const _MasterDialog({required this.authState, required this.mode, this.item});
  final AuthState authState;
  final CatalogMasterMode mode;
  final CatalogLookup? item;
  @override
  State<_MasterDialog> createState() => _MasterDialogState();
}

class _MasterDialogState extends State<_MasterDialog> {
  final _form = GlobalKey<FormState>();
  late final _name = TextEditingController(text: widget.item?.name);
  late final _description = TextEditingController(
    text: widget.item is CategoryInfo
        ? (widget.item! as CategoryInfo).description
        : null,
  );
  late final _shortName = TextEditingController(
    text: widget.item is ManufacturerInfo
        ? (widget.item! as ManufacturerInfo).shortName
        : null,
  );
  late final _phone = TextEditingController(
    text: widget.item is ManufacturerInfo
        ? (widget.item! as ManufacturerInfo).phoneNumber
        : null,
  );
  late final _email = TextEditingController(
    text: widget.item is ManufacturerInfo
        ? (widget.item! as ManufacturerInfo).email
        : null,
  );
  late final _website = TextEditingController(
    text: widget.item is ManufacturerInfo
        ? (widget.item! as ManufacturerInfo).website
        : null,
  );
  bool _saving = false;
  String? _error;
  bool get _categories => widget.mode == CatalogMasterMode.categories;
  @override
  void dispose() {
    for (final value in [
      _name,
      _description,
      _shortName,
      _phone,
      _email,
      _website,
    ]) {
      value.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      if (_categories) {
        await widget.authState.saveCategory({
          'name': _name.text,
          'description': _description.text,
          'isActive': widget.item?.isActive ?? true,
        }, id: widget.item?.id);
      } else {
        await widget.authState.saveManufacturer({
          'name': _name.text,
          'shortName': _shortName.text,
          'phoneNumber': _phone.text,
          'email': _email.text,
          'website': _website.text,
          'isActive': widget.item?.isActive ?? true,
        }, id: widget.item?.id);
      }
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(
      '${widget.item == null ? 'Add' : 'Edit'} ${_categories ? 'Category' : 'Manufacturer'}',
    ),
    content: SizedBox(
      width: 480,
      child: Form(
        key: _form,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                key: Key(_categories ? 'category_name' : 'manufacturer_name'),
                controller: _name,
                validator: (value) =>
                    value?.trim().isEmpty != false ? 'Name is required' : null,
                decoration: const InputDecoration(labelText: 'Name'),
              ),
              const SizedBox(height: 12),
              if (_categories)
                TextFormField(
                  controller: _description,
                  maxLines: 3,
                  decoration: const InputDecoration(labelText: 'Description'),
                )
              else ...[
                TextFormField(
                  controller: _shortName,
                  decoration: const InputDecoration(labelText: 'Short name'),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _phone,
                  decoration: const InputDecoration(labelText: 'Contact phone'),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _email,
                  decoration: const InputDecoration(labelText: 'Email'),
                ),
                const SizedBox(height: 12),
                TextFormField(
                  controller: _website,
                  decoration: const InputDecoration(labelText: 'Website'),
                ),
              ],
              if (_error != null) ...[
                const SizedBox(height: 12),
                Text(
                  _error!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ],
            ],
          ),
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('Cancel'),
      ),
      FilledButton(
        key: Key(_categories ? 'save_category' : 'save_manufacturer'),
        onPressed: _saving ? null : _save,
        child: Text(_saving ? 'Saving...' : 'Save'),
      ),
    ],
  );
}
