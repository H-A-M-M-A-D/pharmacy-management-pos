class BranchInfo {
  const BranchInfo({required this.id, required this.code, required this.name});

  final String id;
  final String code;
  final String name;

  factory BranchInfo.fromJson(Map<String, dynamic> json) => BranchInfo(
    id: json['id'] as String,
    code: json['code'] as String? ?? '',
    name: json['name'] as String? ?? '',
  );
}

class RoleInfo {
  const RoleInfo({required this.id, required this.name, this.description});

  final String id;
  final String name;
  final String? description;

  factory RoleInfo.fromJson(Map<String, dynamic> json) => RoleInfo(
    id: json['id'] as String,
    name: json['name'] as String? ?? '',
    description: json['description'] as String?,
  );
}

class CurrentUser {
  const CurrentUser({
    required this.id,
    required this.username,
    required this.fullName,
    required this.branch,
    required this.roles,
    required this.permissions,
    required this.mustChangePassword,
    this.email,
    this.phoneNumber,
  });

  final String id;
  final String username;
  final String fullName;
  final String? email;
  final String? phoneNumber;
  final BranchInfo branch;
  final List<RoleInfo> roles;
  final Set<String> permissions;
  final bool mustChangePassword;

  bool can(String permission) => permissions.contains(permission);

  factory CurrentUser.fromJson(Map<String, dynamic> json) => CurrentUser(
    id: json['id'] as String,
    username: json['username'] as String? ?? '',
    fullName: json['fullName'] as String? ?? '',
    email: json['email'] as String?,
    phoneNumber: json['phoneNumber'] as String?,
    branch: BranchInfo.fromJson(json['branch'] as Map<String, dynamic>),
    roles: (json['roles'] as List<dynamic>? ?? [])
        .map((item) => RoleInfo.fromJson(item as Map<String, dynamic>))
        .toList(),
    permissions: (json['permissions'] as List<dynamic>? ?? [])
        .cast<String>()
        .toSet(),
    mustChangePassword: json['mustChangePassword'] as bool? ?? false,
  );
}

class LoginSession {
  const LoginSession({
    required this.accessToken,
    required this.expiresAtUtc,
    required this.user,
  });

  final String accessToken;
  final DateTime expiresAtUtc;
  final CurrentUser user;

  factory LoginSession.fromJson(Map<String, dynamic> json) => LoginSession(
    accessToken: json['accessToken'] as String,
    expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String).toUtc(),
    user: CurrentUser.fromJson(json['user'] as Map<String, dynamic>),
  );
}

class UserListItem {
  const UserListItem({
    required this.id,
    required this.fullName,
    required this.username,
    required this.branch,
    required this.role,
    required this.isActive,
    required this.mustChangePassword,
    this.lastLoginAtUtc,
  });

  final String id;
  final String fullName;
  final String username;
  final BranchInfo branch;
  final RoleInfo role;
  final bool isActive;
  final bool mustChangePassword;
  final DateTime? lastLoginAtUtc;

  factory UserListItem.fromJson(Map<String, dynamic> json) => UserListItem(
    id: json['id'] as String,
    fullName: json['fullName'] as String? ?? '',
    username: json['username'] as String? ?? '',
    branch: BranchInfo.fromJson(json['branch'] as Map<String, dynamic>),
    role: RoleInfo.fromJson(json['role'] as Map<String, dynamic>),
    isActive: json['isActive'] as bool? ?? false,
    mustChangePassword: json['mustChangePassword'] as bool? ?? false,
    lastLoginAtUtc: json['lastLoginAtUtc'] == null
        ? null
        : DateTime.parse(json['lastLoginAtUtc'] as String).toUtc(),
  );
}

class UserDetails {
  const UserDetails({
    required this.id,
    required this.fullName,
    required this.username,
    required this.branch,
    required this.roles,
    required this.isActive,
    required this.mustChangePassword,
    this.email,
    this.phoneNumber,
    this.lastLoginAtUtc,
  });

  final String id;
  final String fullName;
  final String username;
  final String? email;
  final String? phoneNumber;
  final BranchInfo branch;
  final List<RoleInfo> roles;
  final bool isActive;
  final bool mustChangePassword;
  final DateTime? lastLoginAtUtc;

  factory UserDetails.fromJson(Map<String, dynamic> json) => UserDetails(
    id: json['id'] as String,
    fullName: json['fullName'] as String? ?? '',
    username: json['username'] as String? ?? '',
    email: json['email'] as String?,
    phoneNumber: json['phoneNumber'] as String?,
    branch: BranchInfo.fromJson(json['branch'] as Map<String, dynamic>),
    roles: (json['roles'] as List<dynamic>? ?? [])
        .map((item) => RoleInfo.fromJson(item as Map<String, dynamic>))
        .toList(),
    isActive: json['isActive'] as bool? ?? false,
    mustChangePassword: json['mustChangePassword'] as bool? ?? false,
    lastLoginAtUtc: json['lastLoginAtUtc'] == null
        ? null
        : DateTime.parse(json['lastLoginAtUtc'] as String).toUtc(),
  );
}

class PagedUsers {
  const PagedUsers({required this.items, required this.totalCount});

  final List<UserListItem> items;
  final int totalCount;

  factory PagedUsers.fromJson(Map<String, dynamic> json) => PagedUsers(
    items: (json['items'] as List<dynamic>? ?? [])
        .map((item) => UserListItem.fromJson(item as Map<String, dynamic>))
        .toList(),
    totalCount: json['totalCount'] as int? ?? 0,
  );
}

class UserOptions {
  const UserOptions({required this.branches, required this.roles});

  final List<BranchInfo> branches;
  final List<RoleInfo> roles;

  factory UserOptions.fromJson(Map<String, dynamic> json) => UserOptions(
    branches: (json['branches'] as List<dynamic>? ?? [])
        .map((item) => BranchInfo.fromJson(item as Map<String, dynamic>))
        .toList(),
    roles: (json['roles'] as List<dynamic>? ?? [])
        .map((item) => RoleInfo.fromJson(item as Map<String, dynamic>))
        .toList(),
  );
}

class CatalogLookup {
  const CatalogLookup({
    required this.id,
    required this.name,
    required this.isActive,
  });
  final String id;
  final String name;
  final bool isActive;
  factory CatalogLookup.fromJson(Map<String, dynamic> json) => CatalogLookup(
    id: json['id'] as String,
    name: json['name'] as String? ?? '',
    isActive: json['isActive'] as bool? ?? false,
  );
}

class ProductListItem {
  const ProductListItem({
    required this.id,
    required this.name,
    required this.sku,
    required this.category,
    required this.unit,
    required this.retailPrice,
    required this.isActive,
    this.barcode,
    this.genericName,
    this.brandName,
    this.manufacturer,
  });
  final String id, name, sku, unit;
  final String? barcode, genericName, brandName;
  final CatalogLookup category;
  final CatalogLookup? manufacturer;
  final double retailPrice;
  final bool isActive;
  factory ProductListItem.fromJson(
    Map<String, dynamic> json,
  ) => ProductListItem(
    id: json['id'] as String,
    name: json['name'] as String? ?? '',
    sku: json['sku'] as String? ?? '',
    barcode: json['barcode'] as String?,
    genericName: json['genericName'] as String?,
    brandName: json['brandName'] as String?,
    category: CatalogLookup.fromJson(json['category'] as Map<String, dynamic>),
    manufacturer: json['manufacturer'] == null
        ? null
        : CatalogLookup.fromJson(json['manufacturer'] as Map<String, dynamic>),
    unit: json['unit'] as String? ?? '',
    retailPrice: (json['retailPrice'] as num?)?.toDouble() ?? 0,
    isActive: json['isActive'] as bool? ?? false,
  );
}

class ProductDetails extends ProductListItem {
  const ProductDetails({
    required super.id,
    required super.name,
    required super.sku,
    required super.category,
    required super.unit,
    required super.retailPrice,
    required super.isActive,
    required this.packSize,
    required this.purchasePrice,
    required this.maximumDiscountPercent,
    required this.reorderLevel,
    super.barcode,
    super.genericName,
    super.brandName,
    super.manufacturer,
    this.tradePrice,
  });
  final int packSize, reorderLevel;
  final double purchasePrice, maximumDiscountPercent;
  final double? tradePrice;
  factory ProductDetails.fromJson(Map<String, dynamic> json) => ProductDetails(
    id: json['id'] as String,
    name: json['name'] as String? ?? '',
    sku: json['sku'] as String? ?? '',
    barcode: json['barcode'] as String?,
    genericName: json['genericName'] as String?,
    brandName: json['brandName'] as String?,
    category: CatalogLookup.fromJson(json['category'] as Map<String, dynamic>),
    manufacturer: json['manufacturer'] == null
        ? null
        : CatalogLookup.fromJson(json['manufacturer'] as Map<String, dynamic>),
    unit: json['unit'] as String? ?? '',
    packSize: json['packSize'] as int? ?? 1,
    purchasePrice: (json['purchasePrice'] as num?)?.toDouble() ?? 0,
    retailPrice: (json['retailPrice'] as num?)?.toDouble() ?? 0,
    tradePrice: (json['tradePrice'] as num?)?.toDouble(),
    maximumDiscountPercent:
        (json['maximumDiscountPercent'] as num?)?.toDouble() ?? 0,
    reorderLevel: json['reorderLevel'] as int? ?? 0,
    isActive: json['isActive'] as bool? ?? false,
  );
}

class PagedProducts {
  const PagedProducts({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
  });
  final List<ProductListItem> items;
  final int page, pageSize, totalCount;
  factory PagedProducts.fromJson(Map<String, dynamic> json) => PagedProducts(
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => ProductListItem.fromJson(x as Map<String, dynamic>))
        .toList(),
    page: json['page'] as int? ?? 1,
    pageSize: json['pageSize'] as int? ?? 25,
    totalCount: json['totalCount'] as int? ?? 0,
  );
}

class CategoryInfo extends CatalogLookup {
  const CategoryInfo({
    required super.id,
    required super.name,
    required super.isActive,
    this.description,
  });
  final String? description;
  factory CategoryInfo.fromJson(Map<String, dynamic> json) => CategoryInfo(
    id: json['id'] as String,
    name: json['name'] as String? ?? '',
    description: json['description'] as String?,
    isActive: json['isActive'] as bool? ?? false,
  );
}

class ManufacturerInfo extends CatalogLookup {
  const ManufacturerInfo({
    required super.id,
    required super.name,
    required super.isActive,
    this.shortName,
    this.phoneNumber,
    this.email,
    this.website,
    this.country,
    this.address,
  });
  final String? shortName, phoneNumber, email, website, country, address;
  factory ManufacturerInfo.fromJson(Map<String, dynamic> json) =>
      ManufacturerInfo(
        id: json['id'] as String,
        name: json['name'] as String? ?? '',
        isActive: json['isActive'] as bool? ?? false,
        shortName: json['shortName'] as String?,
        phoneNumber: json['phoneNumber'] as String?,
        email: json['email'] as String?,
        website: json['website'] as String?,
        country: json['country'] as String?,
        address: json['address'] as String?,
      );
}

class ProductOptions {
  const ProductOptions({
    required this.categories,
    required this.manufacturers,
    required this.units,
  });
  final List<CatalogLookup> categories, manufacturers;
  final List<String> units;
  factory ProductOptions.fromJson(Map<String, dynamic> json) => ProductOptions(
    categories: (json['categories'] as List<dynamic>? ?? [])
        .map((x) => CatalogLookup.fromJson(x as Map<String, dynamic>))
        .toList(),
    manufacturers: (json['manufacturers'] as List<dynamic>? ?? [])
        .map((x) => CatalogLookup.fromJson(x as Map<String, dynamic>))
        .toList(),
    units: (json['units'] as List<dynamic>? ?? []).cast<String>(),
  );
}

class InventoryLookup {
  const InventoryLookup({required this.id, required this.name});
  final String id;
  final String name;
  factory InventoryLookup.fromJson(Map<String, dynamic> json) =>
      InventoryLookup(
        id: json['id'] as String,
        name: json['name'] as String? ?? '',
      );
}

class ProductLookup {
  const ProductLookup({
    required this.id,
    required this.name,
    required this.sku,
    required this.isActive,
    this.barcode,
    this.genericName,
  });
  final String id, name, sku;
  final String? barcode, genericName;
  final bool isActive;
  factory ProductLookup.fromJson(Map<String, dynamic> json) => ProductLookup(
    id: json['id'] as String,
    name: json['name'] as String? ?? '',
    sku: json['sku'] as String? ?? '',
    barcode: json['barcode'] as String?,
    genericName: json['genericName'] as String?,
    isActive: json['isActive'] as bool? ?? false,
  );
}

class InventoryOptions {
  const InventoryOptions({
    required this.branches,
    required this.categories,
    required this.manufacturers,
    required this.products,
    required this.suppliers,
  });
  final List<InventoryLookup> branches, categories, manufacturers, suppliers;
  final List<ProductLookup> products;
  factory InventoryOptions.fromJson(Map<String, dynamic> json) =>
      InventoryOptions(
        branches: _lookups(json['branches']),
        categories: _lookups(json['categories']),
        manufacturers: _lookups(json['manufacturers']),
        suppliers: _lookups(json['suppliers']),
        products: (json['products'] as List<dynamic>? ?? [])
            .map((x) => ProductLookup.fromJson(x as Map<String, dynamic>))
            .toList(),
      );
}

class InventoryItem {
  const InventoryItem({
    required this.productId,
    required this.productName,
    required this.sku,
    required this.category,
    required this.quantityInStock,
    required this.reorderLevel,
    required this.stockStatus,
    required this.activeBatchCount,
    required this.estimatedStockValue,
    this.genericName,
    this.manufacturer,
    this.nearestExpiryDate,
  });
  final String productId, productName, sku, category, stockStatus;
  final String? genericName, manufacturer;
  final int quantityInStock, reorderLevel, activeBatchCount;
  final DateTime? nearestExpiryDate;
  final double estimatedStockValue;
  factory InventoryItem.fromJson(Map<String, dynamic> json) => InventoryItem(
    productId: json['productId'] as String,
    productName: json['productName'] as String? ?? '',
    sku: json['sku'] as String? ?? '',
    genericName: json['genericName'] as String?,
    category: json['category'] as String? ?? '',
    manufacturer: json['manufacturer'] as String?,
    quantityInStock: json['quantityInStock'] as int? ?? 0,
    reorderLevel: json['reorderLevel'] as int? ?? 0,
    stockStatus: json['stockStatus'] as String? ?? '',
    activeBatchCount: json['activeBatchCount'] as int? ?? 0,
    nearestExpiryDate: _date(json['nearestExpiryDate']),
    estimatedStockValue: (json['estimatedStockValue'] as num?)?.toDouble() ?? 0,
  );
}

class PagedInventory {
  const PagedInventory({required this.items, required this.totalCount});
  final List<InventoryItem> items;
  final int totalCount;
  factory PagedInventory.fromJson(Map<String, dynamic> json) => PagedInventory(
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => InventoryItem.fromJson(x as Map<String, dynamic>))
        .toList(),
    totalCount: json['totalCount'] as int? ?? 0,
  );
}

class BatchItem {
  const BatchItem({
    required this.batchId,
    required this.productId,
    required this.productName,
    required this.sku,
    required this.batchNumber,
    required this.branchId,
    required this.branchName,
    required this.expiryDate,
    required this.quantityAvailable,
    required this.purchasePrice,
    required this.retailPrice,
    required this.estimatedStockValue,
    required this.state,
  });
  final String batchId,
      productId,
      productName,
      sku,
      batchNumber,
      branchId,
      branchName,
      state;
  final DateTime expiryDate;
  final int quantityAvailable;
  final double purchasePrice, retailPrice, estimatedStockValue;
  factory BatchItem.fromJson(Map<String, dynamic> json) => BatchItem(
    batchId: json['batchId'] as String,
    productId: json['productId'] as String,
    productName: json['productName'] as String? ?? '',
    sku: json['sku'] as String? ?? '',
    batchNumber: json['batchNumber'] as String? ?? '',
    branchId: json['branchId'] as String,
    branchName: json['branchName'] as String? ?? '',
    expiryDate: _date(json['expiryDate']) ?? DateTime.now(),
    quantityAvailable: json['quantityAvailable'] as int? ?? 0,
    purchasePrice: (json['purchasePrice'] as num?)?.toDouble() ?? 0,
    retailPrice: (json['retailPrice'] as num?)?.toDouble() ?? 0,
    estimatedStockValue: (json['estimatedStockValue'] as num?)?.toDouble() ?? 0,
    state: json['state'] as String? ?? '',
  );
}

class PagedBatches {
  const PagedBatches({required this.items, required this.totalCount});
  final List<BatchItem> items;
  final int totalCount;
  factory PagedBatches.fromJson(Map<String, dynamic> json) => PagedBatches(
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => BatchItem.fromJson(x as Map<String, dynamic>))
        .toList(),
    totalCount: json['totalCount'] as int? ?? 0,
  );
}

class ExpiryItem {
  const ExpiryItem({
    required this.batchId,
    required this.productName,
    required this.batchNumber,
    required this.expiryDate,
    required this.daysRemaining,
    required this.quantityAvailable,
    required this.estimatedStockValue,
  });
  final String batchId, productName, batchNumber;
  final DateTime expiryDate;
  final int daysRemaining, quantityAvailable;
  final double estimatedStockValue;
  factory ExpiryItem.fromJson(Map<String, dynamic> json) => ExpiryItem(
    batchId: json['batchId'] as String,
    productName: json['productName'] as String? ?? '',
    batchNumber: json['batchNumber'] as String? ?? '',
    expiryDate: _date(json['expiryDate']) ?? DateTime.now(),
    daysRemaining: json['daysRemaining'] as int? ?? 0,
    quantityAvailable: json['quantityAvailable'] as int? ?? 0,
    estimatedStockValue: (json['estimatedStockValue'] as num?)?.toDouble() ?? 0,
  );
}

class StockMovementItem {
  const StockMovementItem({
    required this.createdAt,
    required this.productName,
    required this.batchNumber,
    required this.branchName,
    required this.movementType,
    required this.quantity,
  });
  final DateTime createdAt;
  final String productName, batchNumber, branchName, movementType;
  final int quantity;
  factory StockMovementItem.fromJson(Map<String, dynamic> json) =>
      StockMovementItem(
        createdAt: DateTime.parse(json['createdAt'] as String).toLocal(),
        productName: json['productName'] as String? ?? '',
        batchNumber: json['batchNumber'] as String? ?? '',
        branchName: json['branchName'] as String? ?? '',
        movementType: json['movementType'] as String? ?? '',
        quantity: json['quantity'] as int? ?? 0,
      );
}

class PagedMovements {
  const PagedMovements({required this.items, required this.totalCount});
  final List<StockMovementItem> items;
  final int totalCount;
  factory PagedMovements.fromJson(Map<String, dynamic> json) => PagedMovements(
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => StockMovementItem.fromJson(x as Map<String, dynamic>))
        .toList(),
    totalCount: json['totalCount'] as int? ?? 0,
  );
}

class SupplierListItem {
  const SupplierListItem({
    required this.id,
    required this.name,
    required this.outstandingBalance,
    required this.isActive,
    this.shortName,
    this.contactPerson,
    this.phoneNumber,
    this.whatsApp,
    this.email,
    this.city,
    this.creditLimit,
  });
  final String id, name;
  final String? shortName, contactPerson, phoneNumber, whatsApp, email, city;
  final double? creditLimit;
  final double outstandingBalance;
  final bool isActive;
  factory SupplierListItem.fromJson(Map<String, dynamic> json) =>
      SupplierListItem(
        id: json['id'] as String,
        name: json['name'] as String? ?? '',
        shortName: json['shortName'] as String?,
        contactPerson: json['contactPerson'] as String?,
        phoneNumber: json['phoneNumber'] as String?,
        whatsApp: json['whatsApp'] as String?,
        email: json['email'] as String?,
        city: json['city'] as String?,
        creditLimit: (json['creditLimit'] as num?)?.toDouble(),
        outstandingBalance:
            (json['outstandingBalance'] as num?)?.toDouble() ?? 0,
        isActive: json['isActive'] as bool? ?? false,
      );
}

class PagedSuppliers {
  const PagedSuppliers({required this.items, required this.totalCount});
  final List<SupplierListItem> items;
  final int totalCount;
  factory PagedSuppliers.fromJson(Map<String, dynamic> json) => PagedSuppliers(
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => SupplierListItem.fromJson(x as Map<String, dynamic>))
        .toList(),
    totalCount: json['totalCount'] as int? ?? 0,
  );
}

class SupplierLedgerItem {
  const SupplierLedgerItem({
    required this.entryDate,
    required this.entryType,
    required this.amount,
    required this.runningBalance,
    required this.branchName,
    this.userName,
    this.notes,
  });
  final DateTime entryDate;
  final String entryType, branchName;
  final double amount, runningBalance;
  final String? userName, notes;
  factory SupplierLedgerItem.fromJson(Map<String, dynamic> json) =>
      SupplierLedgerItem(
        entryDate: _date(json['entryDate']) ?? DateTime.now(),
        entryType: json['entryType'] as String? ?? '',
        amount: (json['amount'] as num?)?.toDouble() ?? 0,
        runningBalance: (json['runningBalance'] as num?)?.toDouble() ?? 0,
        branchName: json['branchName'] as String? ?? '',
        userName: json['userName'] as String?,
        notes: json['notes'] as String?,
      );
}

class PagedSupplierLedger {
  const PagedSupplierLedger({required this.items, required this.totalCount});
  final List<SupplierLedgerItem> items;
  final int totalCount;
  factory PagedSupplierLedger.fromJson(Map<String, dynamic> json) =>
      PagedSupplierLedger(
        items: (json['items'] as List<dynamic>? ?? [])
            .map((x) => SupplierLedgerItem.fromJson(x as Map<String, dynamic>))
            .toList(),
        totalCount: json['totalCount'] as int? ?? 0,
      );
}

List<InventoryLookup> _lookups(dynamic json) => (json as List<dynamic>? ?? [])
    .map((x) => InventoryLookup.fromJson(x as Map<String, dynamic>))
    .toList();

DateTime? _date(dynamic value) =>
    value == null ? null : DateTime.parse(value as String);
