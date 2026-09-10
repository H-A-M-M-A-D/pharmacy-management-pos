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
    this.godownId,
    this.godownCode,
    this.godownName,
  });
  final String batchId,
      productId,
      productName,
      sku,
      batchNumber,
      branchId,
      branchName,
      state;
  final String? godownId, godownCode, godownName;
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
    godownId: json['godownId'] as String?,
    godownCode: json['godownCode'] as String?,
    godownName: json['godownName'] as String?,
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
    this.godownId,
    this.godownName,
  });
  final String batchId, productName, batchNumber;
  final String? godownId, godownName;
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
    godownId: json['godownId'] as String?,
    godownName: json['godownName'] as String?,
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
    this.godownId,
    this.godownName,
  });
  final DateTime createdAt;
  final String productName, batchNumber, branchName, movementType;
  final String? godownId, godownName;
  final int quantity;
  factory StockMovementItem.fromJson(Map<String, dynamic> json) =>
      StockMovementItem(
        createdAt: DateTime.parse(json['createdAt'] as String).toLocal(),
        productName: json['productName'] as String? ?? '',
        batchNumber: json['batchNumber'] as String? ?? '',
        branchName: json['branchName'] as String? ?? '',
        movementType: json['movementType'] as String? ?? '',
        quantity: json['quantity'] as int? ?? 0,
        godownId: json['godownId'] as String?,
        godownName: json['godownName'] as String?,
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

class StockCountLine {
  const StockCountLine({
    required this.id,
    required this.productId,
    required this.productName,
    required this.sku,
    required this.productBatchId,
    required this.batchNumber,
    required this.expiryDate,
    required this.systemQuantity,
    required this.unitCostSnapshot,
    this.countedQuantity,
    this.variance,
    this.varianceValue,
    this.reason,
    this.notes,
    this.countedBy,
    this.countedAtUtc,
  });
  final String id, productId, productName, sku, productBatchId, batchNumber;
  final DateTime expiryDate;
  final int systemQuantity;
  final int? countedQuantity, variance;
  final double unitCostSnapshot;
  final double? varianceValue;
  final String? reason, notes, countedBy;
  final DateTime? countedAtUtc;
  factory StockCountLine.fromJson(Map<String, dynamic> json) => StockCountLine(
    id: json['id'] as String,
    productId: json['productId'] as String,
    productName: json['productName'] as String? ?? '',
    sku: json['sku'] as String? ?? '',
    productBatchId: json['productBatchId'] as String,
    batchNumber: json['batchNumber'] as String? ?? '',
    expiryDate: _date(json['expiryDate']) ?? DateTime.now(),
    systemQuantity: json['systemQuantity'] as int? ?? 0,
    countedQuantity: json['countedQuantity'] as int?,
    variance: json['variance'] as int?,
    unitCostSnapshot: (json['unitCostSnapshot'] as num?)?.toDouble() ?? 0,
    varianceValue: (json['varianceValue'] as num?)?.toDouble(),
    reason: json['reason'] as String?,
    notes: json['notes'] as String?,
    countedBy: json['countedBy'] as String?,
    countedAtUtc: _date(json['countedAtUtc']),
  );
}

class StockCountSession {
  const StockCountSession({
    required this.id,
    required this.countNumber,
    required this.branchId,
    required this.branchName,
    required this.countDate,
    required this.status,
    required this.scope,
    required this.createdBy,
    required this.totalItems,
    required this.countedItems,
    required this.varianceItems,
    required this.items,
    this.categoryId,
    this.categoryName,
    this.notes,
    this.startedBy,
    this.startedAtUtc,
    this.completedBy,
    this.completedAtUtc,
    this.cancelledBy,
    this.cancelledAtUtc,
    this.godownId,
    this.godownName,
  });
  final String id, countNumber, branchId, branchName, status, scope, createdBy;
  final DateTime countDate;
  final String? categoryId, categoryName, notes, startedBy, completedBy, cancelledBy;
  final String? godownId, godownName;
  final DateTime? startedAtUtc, completedAtUtc, cancelledAtUtc;
  final int totalItems, countedItems, varianceItems;
  final List<StockCountLine> items;
  factory StockCountSession.fromJson(Map<String, dynamic> json) =>
      StockCountSession(
        id: json['id'] as String,
        countNumber: json['countNumber'] as String? ?? '',
        branchId: json['branchId'] as String,
        branchName: json['branchName'] as String? ?? '',
        countDate: _date(json['countDate']) ?? DateTime.now(),
        status: json['status'] as String? ?? '',
        scope: json['scope'] as String? ?? '',
        categoryId: json['categoryId'] as String?,
        categoryName: json['categoryName'] as String?,
        notes: json['notes'] as String?,
        createdBy: json['createdBy'] as String? ?? '',
        startedBy: json['startedBy'] as String?,
        startedAtUtc: _date(json['startedAtUtc']),
        completedBy: json['completedBy'] as String?,
        completedAtUtc: _date(json['completedAtUtc']),
        cancelledBy: json['cancelledBy'] as String?,
        cancelledAtUtc: _date(json['cancelledAtUtc']),
        totalItems: json['totalItems'] as int? ?? 0,
        countedItems: json['countedItems'] as int? ?? 0,
        varianceItems: json['varianceItems'] as int? ?? 0,
        items: (json['items'] as List<dynamic>? ?? [])
            .map((x) => StockCountLine.fromJson(x as Map<String, dynamic>))
            .toList(),
        godownId: json['godownId'] as String?,
        godownName: json['godownName'] as String?,
      );
}

class StockCountSessionSummary {
  const StockCountSessionSummary({
    required this.id,
    required this.countNumber,
    required this.branchId,
    required this.branchName,
    required this.countDate,
    required this.status,
    required this.scope,
    required this.totalItems,
    required this.countedItems,
    required this.varianceItems,
    required this.createdBy,
    required this.createdAt,
    this.categoryName,
    this.completedAtUtc,
    this.godownId,
    this.godownName,
  });
  final String id, countNumber, branchId, branchName, status, scope, createdBy;
  final DateTime countDate, createdAt;
  final String? categoryName;
  final String? godownId, godownName;
  final DateTime? completedAtUtc;
  final int totalItems, countedItems, varianceItems;
  factory StockCountSessionSummary.fromJson(Map<String, dynamic> json) =>
      StockCountSessionSummary(
        id: json['id'] as String,
        countNumber: json['countNumber'] as String? ?? '',
        branchId: json['branchId'] as String,
        branchName: json['branchName'] as String? ?? '',
        countDate: _date(json['countDate']) ?? DateTime.now(),
        status: json['status'] as String? ?? '',
        scope: json['scope'] as String? ?? '',
        categoryName: json['categoryName'] as String?,
        totalItems: json['totalItems'] as int? ?? 0,
        countedItems: json['countedItems'] as int? ?? 0,
        varianceItems: json['varianceItems'] as int? ?? 0,
        createdBy: json['createdBy'] as String? ?? '',
        createdAt: DateTime.parse(json['createdAt'] as String).toLocal(),
        completedAtUtc: _date(json['completedAtUtc']),
        godownId: json['godownId'] as String?,
        godownName: json['godownName'] as String?,
      );
}

class CashierShiftPaymentSummary {
  const CashierShiftPaymentSummary({
    required this.paymentMethod,
    required this.salesAmount,
    required this.refundsAmount,
  });
  final String paymentMethod;
  final double salesAmount, refundsAmount;
  factory CashierShiftPaymentSummary.fromJson(Map<String, dynamic> json) =>
      CashierShiftPaymentSummary(
        paymentMethod: json['paymentMethod'] as String? ?? '',
        salesAmount: (json['salesAmount'] as num?)?.toDouble() ?? 0,
        refundsAmount: (json['refundsAmount'] as num?)?.toDouble() ?? 0,
      );
}

class CashierShiftDrawerEntry {
  const CashierShiftDrawerEntry({
    required this.id,
    required this.entryType,
    required this.amount,
    required this.reason,
    required this.createdBy,
    required this.createdAtUtc,
  });
  final String id, entryType, reason, createdBy;
  final double amount;
  final DateTime createdAtUtc;
  factory CashierShiftDrawerEntry.fromJson(Map<String, dynamic> json) =>
      CashierShiftDrawerEntry(
        id: json['id'] as String,
        entryType: json['entryType'] as String? ?? '',
        amount: (json['amount'] as num?)?.toDouble() ?? 0,
        reason: json['reason'] as String? ?? '',
        createdBy: json['createdBy'] as String? ?? '',
        createdAtUtc: DateTime.parse(json['createdAtUtc'] as String).toLocal(),
      );
}

class CashierShift {
  const CashierShift({
    required this.id,
    required this.branchId,
    required this.branchName,
    required this.cashierUserId,
    required this.cashierName,
    required this.openingCash,
    required this.openedAtUtc,
    required this.status,
    required this.totalSales,
    required this.totalRefunds,
    required this.cashSales,
    required this.cashRefunds,
    required this.customerCashReceived,
    required this.cashPaidOut,
    required this.manualCashIn,
    required this.manualCashOut,
    required this.paymentBreakdown,
    required this.drawerEntries,
    this.terminalName,
    this.openingNotes,
    this.closedAtUtc,
    this.expectedCash,
    this.actualCountedCash,
    this.cashVariance,
    this.closingNotes,
    this.reconciledBy,
    this.reconciledAtUtc,
    this.reconciliationNotes,
  });
  final String id, branchId, branchName, cashierUserId, cashierName, status;
  final String? terminalName, openingNotes, closingNotes, reconciledBy, reconciliationNotes;
  final double openingCash, totalSales, totalRefunds, cashSales, cashRefunds,
      customerCashReceived, cashPaidOut, manualCashIn, manualCashOut;
  final double? expectedCash, actualCountedCash, cashVariance;
  final DateTime openedAtUtc;
  final DateTime? closedAtUtc, reconciledAtUtc;
  final List<CashierShiftPaymentSummary> paymentBreakdown;
  final List<CashierShiftDrawerEntry> drawerEntries;
  factory CashierShift.fromJson(Map<String, dynamic> json) => CashierShift(
    id: json['id'] as String,
    branchId: json['branchId'] as String,
    branchName: json['branchName'] as String? ?? '',
    cashierUserId: json['cashierUserId'] as String,
    cashierName: json['cashierName'] as String? ?? '',
    terminalName: json['terminalName'] as String?,
    openingCash: (json['openingCash'] as num?)?.toDouble() ?? 0,
    openedAtUtc: DateTime.parse(json['openedAtUtc'] as String).toLocal(),
    openingNotes: json['openingNotes'] as String?,
    status: json['status'] as String? ?? '',
    closedAtUtc: _date(json['closedAtUtc']),
    expectedCash: (json['expectedCash'] as num?)?.toDouble(),
    actualCountedCash: (json['actualCountedCash'] as num?)?.toDouble(),
    cashVariance: (json['cashVariance'] as num?)?.toDouble(),
    closingNotes: json['closingNotes'] as String?,
    reconciledBy: json['reconciledBy'] as String?,
    reconciledAtUtc: _date(json['reconciledAtUtc']),
    reconciliationNotes: json['reconciliationNotes'] as String?,
    totalSales: (json['totalSales'] as num?)?.toDouble() ?? 0,
    totalRefunds: (json['totalRefunds'] as num?)?.toDouble() ?? 0,
    cashSales: (json['cashSales'] as num?)?.toDouble() ?? 0,
    cashRefunds: (json['cashRefunds'] as num?)?.toDouble() ?? 0,
    customerCashReceived: (json['customerCashReceived'] as num?)?.toDouble() ?? 0,
    cashPaidOut: (json['cashPaidOut'] as num?)?.toDouble() ?? 0,
    manualCashIn: (json['manualCashIn'] as num?)?.toDouble() ?? 0,
    manualCashOut: (json['manualCashOut'] as num?)?.toDouble() ?? 0,
    paymentBreakdown: (json['paymentBreakdown'] as List<dynamic>? ?? [])
        .map((x) => CashierShiftPaymentSummary.fromJson(x as Map<String, dynamic>))
        .toList(),
    drawerEntries: (json['drawerEntries'] as List<dynamic>? ?? [])
        .map((x) => CashierShiftDrawerEntry.fromJson(x as Map<String, dynamic>))
        .toList(),
  );
}

class CashierShiftListItem {
  const CashierShiftListItem({
    required this.id,
    required this.branchId,
    required this.branchName,
    required this.cashierUserId,
    required this.cashierName,
    required this.openingCash,
    required this.openedAtUtc,
    required this.status,
    this.terminalName,
    this.closedAtUtc,
    this.expectedCash,
    this.actualCountedCash,
    this.cashVariance,
  });
  final String id, branchId, branchName, cashierUserId, cashierName, status;
  final String? terminalName;
  final double openingCash;
  final double? expectedCash, actualCountedCash, cashVariance;
  final DateTime openedAtUtc;
  final DateTime? closedAtUtc;
  factory CashierShiftListItem.fromJson(Map<String, dynamic> json) =>
      CashierShiftListItem(
        id: json['id'] as String,
        branchId: json['branchId'] as String,
        branchName: json['branchName'] as String? ?? '',
        cashierUserId: json['cashierUserId'] as String,
        cashierName: json['cashierName'] as String? ?? '',
        terminalName: json['terminalName'] as String?,
        openingCash: (json['openingCash'] as num?)?.toDouble() ?? 0,
        openedAtUtc: DateTime.parse(json['openedAtUtc'] as String).toLocal(),
        status: json['status'] as String? ?? '',
        closedAtUtc: _date(json['closedAtUtc']),
        expectedCash: (json['expectedCash'] as num?)?.toDouble(),
        actualCountedCash: (json['actualCountedCash'] as num?)?.toDouble(),
        cashVariance: (json['cashVariance'] as num?)?.toDouble(),
      );
}

class PagedCashierShifts {
  const PagedCashierShifts({required this.items, required this.totalCount});
  final List<CashierShiftListItem> items;
  final int totalCount;
  factory PagedCashierShifts.fromJson(Map<String, dynamic> json) =>
      PagedCashierShifts(
        items: (json['items'] as List<dynamic>? ?? [])
            .map((x) => CashierShiftListItem.fromJson(x as Map<String, dynamic>))
            .toList(),
        totalCount: json['totalCount'] as int? ?? 0,
      );
}

class DailyClosingSummary {
  const DailyClosingSummary({
    required this.branchId,
    required this.branchName,
    required this.date,
    required this.shiftCount,
    required this.openShiftCount,
    required this.totalOpeningCash,
    required this.totalExpectedCash,
    required this.totalActualCash,
    required this.totalVariance,
    required this.paymentBreakdown,
  });
  final String branchId, branchName;
  final DateTime date;
  final int shiftCount, openShiftCount;
  final double totalOpeningCash, totalExpectedCash, totalActualCash, totalVariance;
  final List<CashierShiftPaymentSummary> paymentBreakdown;
  factory DailyClosingSummary.fromJson(Map<String, dynamic> json) =>
      DailyClosingSummary(
        branchId: json['branchId'] as String,
        branchName: json['branchName'] as String? ?? '',
        date: _date(json['date']) ?? DateTime.now(),
        shiftCount: json['shiftCount'] as int? ?? 0,
        openShiftCount: json['openShiftCount'] as int? ?? 0,
        totalOpeningCash: (json['totalOpeningCash'] as num?)?.toDouble() ?? 0,
        totalExpectedCash: (json['totalExpectedCash'] as num?)?.toDouble() ?? 0,
        totalActualCash: (json['totalActualCash'] as num?)?.toDouble() ?? 0,
        totalVariance: (json['totalVariance'] as num?)?.toDouble() ?? 0,
        paymentBreakdown: (json['paymentBreakdown'] as List<dynamic>? ?? [])
            .map((x) => CashierShiftPaymentSummary.fromJson(x as Map<String, dynamic>))
            .toList(),
      );
}

class PagedStockCountSessions {
  const PagedStockCountSessions({required this.items, required this.totalCount});
  final List<StockCountSessionSummary> items;
  final int totalCount;
  factory PagedStockCountSessions.fromJson(Map<String, dynamic> json) =>
      PagedStockCountSessions(
        items: (json['items'] as List<dynamic>? ?? [])
            .map(
              (x) =>
                  StockCountSessionSummary.fromJson(x as Map<String, dynamic>),
            )
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

class GodownLookup {
  const GodownLookup({
    required this.id,
    required this.branchId,
    required this.code,
    required this.name,
    required this.isDefault,
    required this.isActive,
  });
  final String id, branchId, code, name;
  final bool isDefault, isActive;
  factory GodownLookup.fromJson(Map<String, dynamic> json) => GodownLookup(
    id: json['id'] as String,
    branchId: json['branchId'] as String,
    code: json['code'] as String? ?? '',
    name: json['name'] as String? ?? '',
    isDefault: json['isDefault'] as bool? ?? false,
    isActive: json['isActive'] as bool? ?? false,
  );
}

class GodownListItem {
  const GodownListItem({
    required this.id,
    required this.branchId,
    required this.branchName,
    required this.code,
    required this.name,
    required this.isDefault,
    required this.isActive,
    this.description,
  });
  final String id, branchId, branchName, code, name;
  final String? description;
  final bool isDefault, isActive;
  factory GodownListItem.fromJson(Map<String, dynamic> json) =>
      GodownListItem(
        id: json['id'] as String,
        branchId: json['branchId'] as String,
        branchName: json['branchName'] as String? ?? '',
        code: json['code'] as String? ?? '',
        name: json['name'] as String? ?? '',
        description: json['description'] as String?,
        isDefault: json['isDefault'] as bool? ?? false,
        isActive: json['isActive'] as bool? ?? false,
      );
}

class PagedGodowns {
  const PagedGodowns({required this.items, required this.totalCount});
  final List<GodownListItem> items;
  final int totalCount;
  factory PagedGodowns.fromJson(Map<String, dynamic> json) => PagedGodowns(
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => GodownListItem.fromJson(x as Map<String, dynamic>))
        .toList(),
    totalCount: json['totalCount'] as int? ?? 0,
  );
}

class UserGodownAssignment {
  const UserGodownAssignment({
    required this.userId,
    required this.userFullName,
    required this.godownId,
    required this.godownName,
    required this.isDefault,
  });
  final String userId, userFullName, godownId, godownName;
  final bool isDefault;
  factory UserGodownAssignment.fromJson(Map<String, dynamic> json) =>
      UserGodownAssignment(
        userId: json['userId'] as String,
        userFullName: json['userFullName'] as String? ?? '',
        godownId: json['godownId'] as String,
        godownName: json['godownName'] as String? ?? '',
        isDefault: json['isDefault'] as bool? ?? false,
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

class CustomerListItem {
  const CustomerListItem({
    required this.id,
    required this.customerCode,
    required this.name,
    required this.creditLimit,
    required this.outstandingBalance,
    required this.advanceBalance,
    required this.isActive,
    this.phoneNumber,
    this.email,
    this.city,
    this.businessName,
  });
  final String id, customerCode, name;
  final String? phoneNumber, email, city, businessName;
  final double creditLimit, outstandingBalance, advanceBalance;
  final bool isActive;
  factory CustomerListItem.fromJson(Map<String, dynamic> json) =>
      CustomerListItem(
        id: json['id'] as String,
        customerCode: json['customerCode'] as String? ?? '',
        name: json['name'] as String? ?? '',
        phoneNumber: json['phoneNumber'] as String?,
        email: json['email'] as String?,
        city: json['city'] as String?,
        businessName: json['businessName'] as String?,
        creditLimit: (json['creditLimit'] as num?)?.toDouble() ?? 0,
        outstandingBalance:
            (json['outstandingBalance'] as num?)?.toDouble() ?? 0,
        advanceBalance: (json['advanceBalance'] as num?)?.toDouble() ?? 0,
        isActive: json['isActive'] as bool? ?? false,
      );
}

class CustomerDetails {
  const CustomerDetails({
    required this.id,
    required this.customerCode,
    required this.name,
    required this.openingBalance,
    required this.creditLimit,
    required this.isActive,
    required this.outstandingBalance,
    required this.advanceBalance,
    required this.totalPayments,
    required this.createdAt,
    required this.updatedAt,
    this.phoneNumber,
    this.alternatePhone,
    this.email,
    this.address,
    this.city,
    this.businessName,
    this.ntn,
    this.lastPaymentAtUtc,
  });
  final String id, customerCode, name;
  final String? phoneNumber,
      alternatePhone,
      email,
      address,
      city,
      businessName,
      ntn;
  final double openingBalance,
      creditLimit,
      outstandingBalance,
      advanceBalance,
      totalPayments;
  final bool isActive;
  final DateTime createdAt, updatedAt;
  final DateTime? lastPaymentAtUtc;

  factory CustomerDetails.fromJson(Map<String, dynamic> json) =>
      CustomerDetails(
        id: json['id'] as String,
        customerCode: json['customerCode'] as String? ?? '',
        name: json['name'] as String? ?? '',
        phoneNumber: json['phoneNumber'] as String?,
        alternatePhone: json['alternatePhone'] as String?,
        email: json['email'] as String?,
        address: json['address'] as String?,
        city: json['city'] as String?,
        businessName: json['businessName'] as String?,
        ntn: json['ntn'] as String?,
        openingBalance: (json['openingBalance'] as num?)?.toDouble() ?? 0,
        creditLimit: (json['creditLimit'] as num?)?.toDouble() ?? 0,
        isActive: json['isActive'] as bool? ?? false,
        outstandingBalance:
            (json['outstandingBalance'] as num?)?.toDouble() ?? 0,
        advanceBalance: (json['advanceBalance'] as num?)?.toDouble() ?? 0,
        totalPayments: (json['totalPayments'] as num?)?.toDouble() ?? 0,
        lastPaymentAtUtc: _date(json['lastPaymentAtUtc']),
        createdAt: _date(json['createdAt']) ?? DateTime.now(),
        updatedAt: _date(json['updatedAt']) ?? DateTime.now(),
      );
}

class CustomerLookup {
  const CustomerLookup({
    required this.id,
    required this.customerCode,
    required this.name,
    required this.creditLimit,
    required this.outstandingBalance,
    required this.availableCredit,
    required this.isActive,
    this.phoneNumber,
  });
  final String id, customerCode, name;
  final String? phoneNumber;
  final double creditLimit, outstandingBalance, availableCredit;
  final bool isActive;
  factory CustomerLookup.fromJson(Map<String, dynamic> json) => CustomerLookup(
    id: json['id'] as String,
    customerCode: json['customerCode'] as String? ?? '',
    name: json['name'] as String? ?? '',
    phoneNumber: json['phoneNumber'] as String?,
    creditLimit: (json['creditLimit'] as num?)?.toDouble() ?? 0,
    outstandingBalance: (json['outstandingBalance'] as num?)?.toDouble() ?? 0,
    availableCredit: (json['availableCredit'] as num?)?.toDouble() ?? 0,
    isActive: json['isActive'] as bool? ?? false,
  );
}

class PagedCustomers {
  const PagedCustomers({required this.items, required this.totalCount});
  final List<CustomerListItem> items;
  final int totalCount;
  factory PagedCustomers.fromJson(Map<String, dynamic> json) => PagedCustomers(
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => CustomerListItem.fromJson(x as Map<String, dynamic>))
        .toList(),
    totalCount: json['totalCount'] as int? ?? 0,
  );
}

class CustomerLedgerItem {
  const CustomerLedgerItem({
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
  factory CustomerLedgerItem.fromJson(Map<String, dynamic> json) =>
      CustomerLedgerItem(
        entryDate: _date(json['entryDate']) ?? DateTime.now(),
        entryType: json['entryType'] as String? ?? '',
        amount: (json['amount'] as num?)?.toDouble() ?? 0,
        runningBalance: (json['runningBalance'] as num?)?.toDouble() ?? 0,
        branchName: json['branchName'] as String? ?? '',
        userName: json['userName'] as String?,
        notes: json['notes'] as String?,
      );
}

class PagedCustomerLedger {
  const PagedCustomerLedger({required this.items, required this.totalCount});
  final List<CustomerLedgerItem> items;
  final int totalCount;
  factory PagedCustomerLedger.fromJson(Map<String, dynamic> json) =>
      PagedCustomerLedger(
        items: (json['items'] as List<dynamic>? ?? [])
            .map((x) => CustomerLedgerItem.fromJson(x as Map<String, dynamic>))
            .toList(),
        totalCount: json['totalCount'] as int? ?? 0,
      );
}

class PurchaseOrderItem {
  const PurchaseOrderItem({
    required this.id,
    required this.productId,
    required this.productName,
    required this.sku,
    required this.orderedQuantity,
    required this.receivedQuantity,
    required this.remainingQuantity,
    this.expectedPurchasePrice,
  });
  final String id, productId, productName, sku;
  final int orderedQuantity, receivedQuantity, remainingQuantity;
  final double? expectedPurchasePrice;
  factory PurchaseOrderItem.fromJson(Map<String, dynamic> json) =>
      PurchaseOrderItem(
        id: json['id'] as String,
        productId: json['productId'] as String,
        productName: json['productName'] as String? ?? '',
        sku: json['sku'] as String? ?? '',
        orderedQuantity: json['orderedQuantity'] as int? ?? 0,
        receivedQuantity: json['receivedQuantity'] as int? ?? 0,
        remainingQuantity: json['remainingQuantity'] as int? ?? 0,
        expectedPurchasePrice: (json['expectedPurchasePrice'] as num?)
            ?.toDouble(),
      );
}

class PurchaseOrderListItem {
  const PurchaseOrderListItem({
    required this.id,
    required this.orderNumber,
    required this.orderDate,
    required this.supplierId,
    required this.supplierName,
    required this.branchId,
    required this.branchName,
    required this.itemCount,
    required this.orderedQuantity,
    required this.receivedQuantity,
    required this.status,
    this.expectedDate,
  });
  final String id,
      orderNumber,
      supplierId,
      supplierName,
      branchId,
      branchName,
      status;
  final DateTime orderDate;
  final DateTime? expectedDate;
  final int itemCount, orderedQuantity, receivedQuantity;
  factory PurchaseOrderListItem.fromJson(Map<String, dynamic> json) =>
      PurchaseOrderListItem(
        id: json['id'] as String,
        orderNumber: json['orderNumber'] as String? ?? '',
        orderDate: _date(json['orderDate']) ?? DateTime.now(),
        expectedDate: _date(json['expectedDate']),
        supplierId: json['supplierId'] as String? ?? '',
        supplierName: json['supplierName'] as String? ?? '',
        branchId: json['branchId'] as String? ?? '',
        branchName: json['branchName'] as String? ?? '',
        itemCount: json['itemCount'] as int? ?? 0,
        orderedQuantity: json['orderedQuantity'] as int? ?? 0,
        receivedQuantity: json['receivedQuantity'] as int? ?? 0,
        status: json['status'] as String? ?? '',
      );
}

class PurchaseOrderDetails extends PurchaseOrderListItem {
  const PurchaseOrderDetails({
    required super.id,
    required super.orderNumber,
    required super.orderDate,
    required super.supplierId,
    required super.supplierName,
    required super.branchId,
    required super.branchName,
    required super.itemCount,
    required super.orderedQuantity,
    required super.receivedQuantity,
    required super.status,
    required this.items,
    super.expectedDate,
  });
  final List<PurchaseOrderItem> items;
  factory PurchaseOrderDetails.fromJson(Map<String, dynamic> json) {
    final items = (json['items'] as List<dynamic>? ?? [])
        .map((x) => PurchaseOrderItem.fromJson(x as Map<String, dynamic>))
        .toList();
    return PurchaseOrderDetails(
      id: json['id'] as String,
      orderNumber: json['orderNumber'] as String? ?? '',
      orderDate: _date(json['orderDate']) ?? DateTime.now(),
      expectedDate: _date(json['expectedDate']),
      supplierId: json['supplierId'] as String? ?? '',
      supplierName: json['supplierName'] as String? ?? '',
      branchId: json['branchId'] as String? ?? '',
      branchName: json['branchName'] as String? ?? '',
      itemCount: items.length,
      orderedQuantity: items.fold(0, (sum, x) => sum + x.orderedQuantity),
      receivedQuantity: items.fold(0, (sum, x) => sum + x.receivedQuantity),
      status: json['status'] as String? ?? '',
      items: items,
    );
  }
}

class PagedPurchaseOrders {
  const PagedPurchaseOrders({required this.items, required this.totalCount});
  final List<PurchaseOrderListItem> items;
  final int totalCount;
  factory PagedPurchaseOrders.fromJson(Map<String, dynamic> json) =>
      PagedPurchaseOrders(
        items: (json['items'] as List<dynamic>? ?? [])
            .map(
              (x) => PurchaseOrderListItem.fromJson(x as Map<String, dynamic>),
            )
            .toList(),
        totalCount: json['totalCount'] as int? ?? 0,
      );
}

class PurchaseItem {
  const PurchaseItem({
    required this.productName,
    required this.sku,
    required this.batchNumber,
    required this.expiryDate,
    required this.purchasedQuantity,
    required this.bonusQuantity,
    required this.inventoryQuantity,
    required this.purchasePrice,
    required this.netLineAmount,
  });
  final String productName, sku, batchNumber;
  final DateTime expiryDate;
  final int purchasedQuantity, bonusQuantity, inventoryQuantity;
  final double purchasePrice, netLineAmount;
  factory PurchaseItem.fromJson(Map<String, dynamic> json) => PurchaseItem(
    productName: json['productName'] as String? ?? '',
    sku: json['sku'] as String? ?? '',
    batchNumber: json['batchNumber'] as String? ?? '',
    expiryDate: _date(json['expiryDate']) ?? DateTime.now(),
    purchasedQuantity: json['purchasedQuantity'] as int? ?? 0,
    bonusQuantity: json['bonusQuantity'] as int? ?? 0,
    inventoryQuantity: json['inventoryQuantity'] as int? ?? 0,
    purchasePrice: (json['purchasePrice'] as num?)?.toDouble() ?? 0,
    netLineAmount: (json['netLineAmount'] as num?)?.toDouble() ?? 0,
  );
}

class PurchaseHistoryItem {
  const PurchaseHistoryItem({
    required this.id,
    required this.grnNumber,
    required this.receiptDate,
    required this.supplierName,
    required this.branchName,
    required this.netTotal,
    required this.status,
    this.returnState = 'NoReturns',
    this.supplierInvoiceNumber,
  });
  final String id, grnNumber, supplierName, branchName, status, returnState;
  final String? supplierInvoiceNumber;
  final DateTime receiptDate;
  final double netTotal;
  factory PurchaseHistoryItem.fromJson(Map<String, dynamic> json) =>
      PurchaseHistoryItem(
        id: json['id'] as String,
        grnNumber: json['grnNumber'] as String? ?? '',
        supplierInvoiceNumber: json['supplierInvoiceNumber'] as String?,
        receiptDate: _date(json['receiptDate']) ?? DateTime.now(),
        supplierName: json['supplierName'] as String? ?? '',
        branchName: json['branchName'] as String? ?? '',
        netTotal: (json['netTotal'] as num?)?.toDouble() ?? 0,
        status: json['status'] as String? ?? '',
        returnState: _enumName(json['returnState'] ?? 'NoReturns'),
      );
}

class PurchaseDetails extends PurchaseHistoryItem {
  const PurchaseDetails({
    required super.id,
    required super.grnNumber,
    required super.receiptDate,
    required super.supplierName,
    required super.branchName,
    required super.netTotal,
    required super.status,
    required this.subtotal,
    required this.discountTotal,
    required this.taxTotal,
    required this.items,
    super.supplierInvoiceNumber,
  });
  final double subtotal, discountTotal, taxTotal;
  final List<PurchaseItem> items;
  factory PurchaseDetails.fromJson(Map<String, dynamic> json) =>
      PurchaseDetails(
        id: json['id'] as String,
        grnNumber: json['grnNumber'] as String? ?? '',
        supplierInvoiceNumber: json['supplierInvoiceNumber'] as String?,
        receiptDate: _date(json['receiptDate']) ?? DateTime.now(),
        supplierName: json['supplierName'] as String? ?? '',
        branchName: json['branchName'] as String? ?? '',
        subtotal: (json['subtotal'] as num?)?.toDouble() ?? 0,
        discountTotal: (json['discountTotal'] as num?)?.toDouble() ?? 0,
        taxTotal: (json['taxTotal'] as num?)?.toDouble() ?? 0,
        netTotal: (json['netTotal'] as num?)?.toDouble() ?? 0,
        status: json['status'] as String? ?? '',
        items: (json['items'] as List<dynamic>? ?? [])
            .map((x) => PurchaseItem.fromJson(x as Map<String, dynamic>))
            .toList(),
      );
}

class PagedPurchases {
  const PagedPurchases({required this.items, required this.totalCount});
  final List<PurchaseHistoryItem> items;
  final int totalCount;
  factory PagedPurchases.fromJson(Map<String, dynamic> json) => PagedPurchases(
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => PurchaseHistoryItem.fromJson(x as Map<String, dynamic>))
        .toList(),
    totalCount: json['totalCount'] as int? ?? 0,
  );
}

class ReturnablePurchase {
  const ReturnablePurchase({
    required this.id,
    required this.grnNumber,
    required this.receiptDate,
    required this.supplierId,
    required this.supplierName,
    required this.branchId,
    required this.branchName,
    required this.netTotal,
    required this.returnState,
    required this.items,
    this.supplierInvoiceNumber,
  });
  final String id,
      grnNumber,
      supplierId,
      supplierName,
      branchId,
      branchName,
      returnState;
  final String? supplierInvoiceNumber;
  final DateTime receiptDate;
  final double netTotal;
  final List<ReturnablePurchaseItem> items;
  factory ReturnablePurchase.fromJson(Map<String, dynamic> json) =>
      ReturnablePurchase(
        id: json['id'] as String? ?? '',
        grnNumber: json['grnNumber'] as String? ?? '',
        supplierInvoiceNumber: json['supplierInvoiceNumber'] as String?,
        receiptDate: _date(json['receiptDate']) ?? DateTime.now(),
        supplierId: json['supplierId'] as String? ?? '',
        supplierName: json['supplierName'] as String? ?? '',
        branchId: json['branchId'] as String? ?? '',
        branchName: json['branchName'] as String? ?? '',
        netTotal: (json['netTotal'] as num?)?.toDouble() ?? 0,
        returnState: _enumName(json['returnState'] ?? 'NoReturns'),
        items: (json['items'] as List<dynamic>? ?? [])
            .map(
              (x) => ReturnablePurchaseItem.fromJson(x as Map<String, dynamic>),
            )
            .toList(),
      );
}

class ReturnablePurchaseItem {
  const ReturnablePurchaseItem({
    required this.id,
    required this.productId,
    required this.productName,
    required this.sku,
    required this.productBatchId,
    required this.batchNumber,
    required this.expiryDate,
    required this.purchasedQuantity,
    required this.bonusQuantity,
    required this.paidQuantityReturned,
    required this.bonusQuantityReturned,
    required this.paidQuantityRemaining,
    required this.bonusQuantityRemaining,
    required this.currentBatchAvailable,
    required this.maxPhysicalReturnQuantity,
    required this.purchasePrice,
    required this.grossRemainingCredit,
    required this.discountRemaining,
    required this.taxRemaining,
    required this.netRemainingSupplierCredit,
    required this.isBatchExpired,
    required this.isBatchDisposed,
  });
  final String id, productId, productName, sku, productBatchId, batchNumber;
  final DateTime expiryDate;
  final int purchasedQuantity,
      bonusQuantity,
      paidQuantityReturned,
      bonusQuantityReturned,
      paidQuantityRemaining,
      bonusQuantityRemaining,
      currentBatchAvailable,
      maxPhysicalReturnQuantity;
  final double purchasePrice,
      grossRemainingCredit,
      discountRemaining,
      taxRemaining,
      netRemainingSupplierCredit;
  final bool isBatchExpired, isBatchDisposed;
  factory ReturnablePurchaseItem.fromJson(Map<String, dynamic> json) =>
      ReturnablePurchaseItem(
        id: json['id'] as String? ?? '',
        productId: json['productId'] as String? ?? '',
        productName: json['productName'] as String? ?? '',
        sku: json['sku'] as String? ?? '',
        productBatchId: json['productBatchId'] as String? ?? '',
        batchNumber: json['batchNumber'] as String? ?? '',
        expiryDate: _date(json['expiryDate']) ?? DateTime.now(),
        purchasedQuantity: json['purchasedQuantity'] as int? ?? 0,
        bonusQuantity: json['bonusQuantity'] as int? ?? 0,
        paidQuantityReturned: json['paidQuantityReturned'] as int? ?? 0,
        bonusQuantityReturned: json['bonusQuantityReturned'] as int? ?? 0,
        paidQuantityRemaining: json['paidQuantityRemaining'] as int? ?? 0,
        bonusQuantityRemaining: json['bonusQuantityRemaining'] as int? ?? 0,
        currentBatchAvailable: json['currentBatchAvailable'] as int? ?? 0,
        maxPhysicalReturnQuantity:
            json['maxPhysicalReturnQuantity'] as int? ?? 0,
        purchasePrice: (json['purchasePrice'] as num?)?.toDouble() ?? 0,
        grossRemainingCredit:
            (json['grossRemainingCredit'] as num?)?.toDouble() ?? 0,
        discountRemaining: (json['discountRemaining'] as num?)?.toDouble() ?? 0,
        taxRemaining: (json['taxRemaining'] as num?)?.toDouble() ?? 0,
        netRemainingSupplierCredit:
            (json['netRemainingSupplierCredit'] as num?)?.toDouble() ?? 0,
        isBatchExpired: json['isBatchExpired'] as bool? ?? false,
        isBatchDisposed: json['isBatchDisposed'] as bool? ?? false,
      );
}

class PurchaseReturnListItem {
  const PurchaseReturnListItem({
    required this.id,
    required this.returnNumber,
    required this.originalGrnNumber,
    required this.returnDateUtc,
    required this.supplierName,
    required this.branchName,
    required this.paidQuantity,
    required this.bonusQuantity,
    required this.totalPhysicalQuantity,
    required this.netSupplierCredit,
    required this.status,
    required this.reason,
    required this.processedByName,
    this.supplierInvoiceNumber,
  });
  final String id,
      returnNumber,
      originalGrnNumber,
      supplierName,
      branchName,
      status,
      reason,
      processedByName;
  final String? supplierInvoiceNumber;
  final DateTime returnDateUtc;
  final int paidQuantity, bonusQuantity, totalPhysicalQuantity;
  final double netSupplierCredit;
  factory PurchaseReturnListItem.fromJson(Map<String, dynamic> json) =>
      PurchaseReturnListItem(
        id: json['id'] as String? ?? '',
        returnNumber: json['returnNumber'] as String? ?? '',
        originalGrnNumber: json['originalGrnNumber'] as String? ?? '',
        supplierInvoiceNumber: json['supplierInvoiceNumber'] as String?,
        returnDateUtc: DateTime.parse(
          json['returnDateUtc'] as String,
        ).toLocal(),
        supplierName: json['supplierName'] as String? ?? '',
        branchName: json['branchName'] as String? ?? '',
        paidQuantity: json['paidQuantity'] as int? ?? 0,
        bonusQuantity: json['bonusQuantity'] as int? ?? 0,
        totalPhysicalQuantity: json['totalPhysicalQuantity'] as int? ?? 0,
        netSupplierCredit: (json['netSupplierCredit'] as num?)?.toDouble() ?? 0,
        status: _enumName(json['status']),
        reason: _enumName(json['reason']),
        processedByName: json['processedByName'] as String? ?? '',
      );
}

class PagedPurchaseReturns {
  const PagedPurchaseReturns({required this.items, required this.totalCount});
  final List<PurchaseReturnListItem> items;
  final int totalCount;
  factory PagedPurchaseReturns.fromJson(Map<String, dynamic> json) =>
      PagedPurchaseReturns(
        items: (json['items'] as List<dynamic>? ?? [])
            .map(
              (x) => PurchaseReturnListItem.fromJson(x as Map<String, dynamic>),
            )
            .toList(),
        totalCount: json['totalCount'] as int? ?? 0,
      );
}

class PurchaseReturnDetails {
  const PurchaseReturnDetails({
    required this.id,
    required this.returnNumber,
    required this.originalGoodsReceiptId,
    required this.originalGrnNumber,
    required this.supplierName,
    required this.branchName,
    required this.processedByName,
    required this.returnDateUtc,
    required this.reason,
    required this.grossReturnAmount,
    required this.discountAdjustment,
    required this.taxAdjustment,
    required this.netSupplierCredit,
    required this.status,
    required this.items,
    this.supplierInvoiceNumber,
    this.notes,
  });
  final String id,
      returnNumber,
      originalGoodsReceiptId,
      originalGrnNumber,
      supplierName,
      branchName,
      processedByName,
      reason,
      status;
  final String? supplierInvoiceNumber, notes;
  final DateTime returnDateUtc;
  final double grossReturnAmount,
      discountAdjustment,
      taxAdjustment,
      netSupplierCredit;
  final List<PurchaseReturnItemDetail> items;
  factory PurchaseReturnDetails.fromJson(
    Map<String, dynamic> json,
  ) => PurchaseReturnDetails(
    id: json['id'] as String? ?? '',
    returnNumber: json['returnNumber'] as String? ?? '',
    originalGoodsReceiptId: json['originalGoodsReceiptId'] as String? ?? '',
    originalGrnNumber: json['originalGrnNumber'] as String? ?? '',
    supplierInvoiceNumber: json['supplierInvoiceNumber'] as String?,
    supplierName: json['supplierName'] as String? ?? '',
    branchName: json['branchName'] as String? ?? '',
    processedByName: json['processedByName'] as String? ?? '',
    returnDateUtc: DateTime.parse(json['returnDateUtc'] as String).toLocal(),
    reason: _enumName(json['reason']),
    notes: json['notes'] as String?,
    grossReturnAmount: (json['grossReturnAmount'] as num?)?.toDouble() ?? 0,
    discountAdjustment: (json['discountAdjustment'] as num?)?.toDouble() ?? 0,
    taxAdjustment: (json['taxAdjustment'] as num?)?.toDouble() ?? 0,
    netSupplierCredit: (json['netSupplierCredit'] as num?)?.toDouble() ?? 0,
    status: _enumName(json['status']),
    items: (json['items'] as List<dynamic>? ?? [])
        .map(
          (x) => PurchaseReturnItemDetail.fromJson(x as Map<String, dynamic>),
        )
        .toList(),
  );
}

class PurchaseReturnItemDetail {
  const PurchaseReturnItemDetail({
    required this.id,
    required this.originalGoodsReceiptItemId,
    required this.productName,
    required this.sku,
    required this.batchNumber,
    required this.expiryDate,
    required this.paidReturnQuantity,
    required this.bonusReturnQuantity,
    required this.totalPhysicalQuantity,
    required this.purchasePriceSnapshot,
    required this.netSupplierCredit,
  });
  final String id, originalGoodsReceiptItemId, productName, sku, batchNumber;
  final DateTime expiryDate;
  final int paidReturnQuantity, bonusReturnQuantity, totalPhysicalQuantity;
  final double purchasePriceSnapshot, netSupplierCredit;
  factory PurchaseReturnItemDetail.fromJson(Map<String, dynamic> json) =>
      PurchaseReturnItemDetail(
        id: json['id'] as String? ?? '',
        originalGoodsReceiptItemId:
            json['originalGoodsReceiptItemId'] as String? ?? '',
        productName: json['productName'] as String? ?? '',
        sku: json['sku'] as String? ?? '',
        batchNumber: json['batchNumber'] as String? ?? '',
        expiryDate: _date(json['expiryDate']) ?? DateTime.now(),
        paidReturnQuantity: json['paidReturnQuantity'] as int? ?? 0,
        bonusReturnQuantity: json['bonusReturnQuantity'] as int? ?? 0,
        totalPhysicalQuantity: json['totalPhysicalQuantity'] as int? ?? 0,
        purchasePriceSnapshot:
            (json['purchasePriceSnapshot'] as num?)?.toDouble() ?? 0,
        netSupplierCredit: (json['netSupplierCredit'] as num?)?.toDouble() ?? 0,
      );
}

List<InventoryLookup> _lookups(dynamic json) => (json as List<dynamic>? ?? [])
    .map((x) => InventoryLookup.fromJson(x as Map<String, dynamic>))
    .toList();

DateTime? _date(dynamic value) =>
    value == null ? null : DateTime.parse(value as String);

class PosProduct {
  const PosProduct({
    required this.productId,
    required this.name,
    required this.sku,
    required this.unit,
    required this.availableQuantity,
    required this.maximumDiscountPercent,
    required this.isActive,
    this.barcode,
    this.genericName,
    this.brandName,
    this.nearestExpiryDate,
    this.indicativeRetailPrice,
  });

  final String productId, name, sku, unit;
  final String? barcode, genericName, brandName;
  final int availableQuantity;
  final DateTime? nearestExpiryDate;
  final double? indicativeRetailPrice;
  final double maximumDiscountPercent;
  final bool isActive;

  factory PosProduct.fromJson(Map<String, dynamic> json) => PosProduct(
    productId: json['productId'] as String,
    name: json['name'] as String? ?? '',
    sku: json['sku'] as String? ?? '',
    barcode: json['barcode'] as String?,
    genericName: json['genericName'] as String?,
    brandName: json['brandName'] as String?,
    unit: json['unit'] as String? ?? '',
    availableQuantity: json['availableQuantity'] as int? ?? 0,
    nearestExpiryDate: _date(json['nearestExpiryDate']),
    indicativeRetailPrice: (json['indicativeRetailPrice'] as num?)?.toDouble(),
    maximumDiscountPercent:
        (json['maximumDiscountPercent'] as num?)?.toDouble() ?? 0,
    isActive: json['isActive'] as bool? ?? false,
  );
}

class SaleListItem {
  const SaleListItem({
    required this.id,
    required this.status,
    required this.createdAt,
    required this.branchName,
    required this.cashierName,
    required this.itemCount,
    required this.netTotal,
    required this.amountPaid,
    required this.creditAmount,
    required this.changeGiven,
    required this.paymentSummary,
    required this.returnState,
    this.invoiceNumber,
    this.holdNumber,
    this.postedAtUtc,
    this.customerName,
    this.customerPhone,
  });

  final String id, status, branchName, cashierName, paymentSummary, returnState;
  final String? invoiceNumber, holdNumber, customerName, customerPhone;
  final DateTime createdAt;
  final DateTime? postedAtUtc;
  final int itemCount;
  final double netTotal, amountPaid, creditAmount, changeGiven;

  factory SaleListItem.fromJson(Map<String, dynamic> json) => SaleListItem(
    id: json['id'] as String? ?? '',
    invoiceNumber: json['invoiceNumber'] as String?,
    holdNumber: json['holdNumber'] as String?,
    status: json['status'] as String? ?? 'Posted',
    createdAt: DateTime.parse(
      (json['createdAt'] ?? json['postedAtUtc']) as String,
    ).toLocal(),
    postedAtUtc: json['postedAtUtc'] == null
        ? null
        : DateTime.parse(json['postedAtUtc'] as String).toLocal(),
    branchName: json['branchName'] as String? ?? '',
    cashierName: json['cashierName'] as String? ?? '',
    customerName: json['customerName'] as String?,
    customerPhone: json['customerPhone'] as String?,
    itemCount: json['itemCount'] as int? ?? 0,
    netTotal: (json['netTotal'] as num?)?.toDouble() ?? 0,
    amountPaid: (json['amountPaid'] as num?)?.toDouble() ?? 0,
    creditAmount: (json['creditAmount'] as num?)?.toDouble() ?? 0,
    changeGiven: (json['changeGiven'] as num?)?.toDouble() ?? 0,
    paymentSummary: json['paymentSummary'] as String? ?? '',
    returnState: _enumName(json['returnState'] ?? 'NotReturned'),
  );
}

class PagedSales {
  const PagedSales({required this.items, required this.totalCount});
  final List<SaleListItem> items;
  final int totalCount;
  factory PagedSales.fromJson(Map<String, dynamic> json) => PagedSales(
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => SaleListItem.fromJson(x as Map<String, dynamic>))
        .toList(),
    totalCount: json['totalCount'] as int? ?? 0,
  );
}

class SaleAllocation {
  const SaleAllocation({
    this.id = '',
    this.productBatchId = '',
    required this.batchNumber,
    required this.expiryDate,
    required this.quantity,
    required this.unitRetailPriceSnapshot,
    required this.unitSalePriceSnapshot,
    required this.netAmount,
  });
  final String id, productBatchId, batchNumber;
  final DateTime expiryDate;
  final int quantity;
  final double unitRetailPriceSnapshot, unitSalePriceSnapshot, netAmount;
  factory SaleAllocation.fromJson(Map<String, dynamic> json) => SaleAllocation(
    id: json['id'] as String? ?? '',
    productBatchId: json['productBatchId'] as String? ?? '',
    batchNumber: json['batchNumber'] as String? ?? '',
    expiryDate: _date(json['expiryDate']) ?? DateTime.now(),
    quantity: json['quantity'] as int? ?? 0,
    unitRetailPriceSnapshot:
        (json['unitRetailPriceSnapshot'] as num?)?.toDouble() ?? 0,
    unitSalePriceSnapshot:
        (json['unitSalePriceSnapshot'] as num?)?.toDouble() ?? 0,
    netAmount: (json['netAmount'] as num?)?.toDouble() ?? 0,
  );
}

class SaleItemDetail {
  const SaleItemDetail({
    required this.productName,
    required this.sku,
    required this.requestedQuantity,
    required this.discountPercent,
    required this.grossAmount,
    required this.discountAmount,
    required this.netAmount,
    required this.allocations,
  });
  final String productName, sku;
  final int requestedQuantity;
  final double discountPercent, grossAmount, discountAmount, netAmount;
  final List<SaleAllocation> allocations;
  factory SaleItemDetail.fromJson(Map<String, dynamic> json) => SaleItemDetail(
    productName: json['productName'] as String? ?? '',
    sku: json['sku'] as String? ?? '',
    requestedQuantity: json['requestedQuantity'] as int? ?? 0,
    discountPercent: (json['discountPercent'] as num?)?.toDouble() ?? 0,
    grossAmount: (json['grossAmount'] as num?)?.toDouble() ?? 0,
    discountAmount: (json['discountAmount'] as num?)?.toDouble() ?? 0,
    netAmount: (json['netAmount'] as num?)?.toDouble() ?? 0,
    allocations: (json['allocations'] as List<dynamic>? ?? [])
        .map((x) => SaleAllocation.fromJson(x as Map<String, dynamic>))
        .toList(),
  );
}

class SalePaymentDetail {
  const SalePaymentDetail({
    required this.method,
    required this.amountApplied,
    this.tenderedAmount,
    this.referenceNumber,
  });
  final String method;
  final double amountApplied;
  final double? tenderedAmount;
  final String? referenceNumber;
  factory SalePaymentDetail.fromJson(Map<String, dynamic> json) =>
      SalePaymentDetail(
        method: json['method'] as String? ?? '',
        amountApplied: (json['amountApplied'] as num?)?.toDouble() ?? 0,
        tenderedAmount: (json['tenderedAmount'] as num?)?.toDouble(),
        referenceNumber: json['referenceNumber'] as String?,
      );
}

class SaleDetails {
  const SaleDetails({
    required this.id,
    required this.status,
    required this.createdAt,
    required this.branchName,
    required this.cashierName,
    required this.subtotal,
    required this.discountTotal,
    required this.taxTotal,
    required this.netTotal,
    required this.amountPaid,
    required this.creditAmount,
    required this.changeGiven,
    required this.items,
    required this.payments,
    this.invoiceNumber,
    this.holdNumber,
    this.postedAtUtc,
    this.customerName,
    this.customerPhone,
    this.customerId,
    this.customerCode,
  });
  final String id, status, branchName, cashierName;
  final String? invoiceNumber, holdNumber, customerName, customerPhone;
  final String? customerId, customerCode;
  final DateTime createdAt;
  final DateTime? postedAtUtc;
  final double subtotal,
      discountTotal,
      taxTotal,
      netTotal,
      amountPaid,
      creditAmount,
      changeGiven;
  final List<SaleItemDetail> items;
  final List<SalePaymentDetail> payments;
  factory SaleDetails.fromJson(Map<String, dynamic> json) => SaleDetails(
    id: json['id'] as String? ?? '',
    invoiceNumber: json['invoiceNumber'] as String?,
    holdNumber: json['holdNumber'] as String?,
    status: json['status'] as String? ?? 'Posted',
    createdAt: DateTime.parse(
      (json['createdAt'] ?? json['postedAtUtc']) as String,
    ).toLocal(),
    postedAtUtc: json['postedAtUtc'] == null
        ? null
        : DateTime.parse(json['postedAtUtc'] as String).toLocal(),
    branchName: json['branchName'] as String? ?? '',
    cashierName: json['cashierName'] as String? ?? '',
    customerName: json['customerName'] as String?,
    customerPhone: json['customerPhone'] as String?,
    customerId: json['customerId'] as String?,
    customerCode: json['customerCode'] as String?,
    subtotal: (json['subtotal'] as num?)?.toDouble() ?? 0,
    discountTotal: (json['discountTotal'] as num?)?.toDouble() ?? 0,
    taxTotal: (json['taxTotal'] as num?)?.toDouble() ?? 0,
    netTotal: (json['netTotal'] as num?)?.toDouble() ?? 0,
    amountPaid: (json['amountPaid'] as num?)?.toDouble() ?? 0,
    creditAmount: (json['creditAmount'] as num?)?.toDouble() ?? 0,
    changeGiven: (json['changeGiven'] as num?)?.toDouble() ?? 0,
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => SaleItemDetail.fromJson(x as Map<String, dynamic>))
        .toList(),
    payments: (json['payments'] as List<dynamic>? ?? [])
        .map((x) => SalePaymentDetail.fromJson(x as Map<String, dynamic>))
        .toList(),
  );
}

class ReturnableSale {
  const ReturnableSale({
    required this.saleId,
    required this.invoiceNumber,
    required this.postedAtUtc,
    required this.branchName,
    required this.cashierName,
    required this.netTotal,
    required this.amountPaid,
    required this.creditAmount,
    required this.returnState,
    required this.items,
    required this.originalPayments,
    this.customerName,
    this.customerPhone,
    this.customerId,
    this.customerCode,
  });
  final String saleId, invoiceNumber, branchName, cashierName, returnState;
  final DateTime postedAtUtc;
  final String? customerName, customerPhone, customerId, customerCode;
  final double netTotal, amountPaid, creditAmount;
  final List<ReturnableSaleItem> items;
  final List<SalePaymentDetail> originalPayments;
  factory ReturnableSale.fromJson(Map<String, dynamic> json) => ReturnableSale(
    saleId: json['saleId'] as String? ?? '',
    invoiceNumber: json['invoiceNumber'] as String? ?? '',
    postedAtUtc: DateTime.parse(json['postedAtUtc'] as String).toLocal(),
    branchName: json['branchName'] as String? ?? '',
    cashierName: json['cashierName'] as String? ?? '',
    customerName: json['customerName'] as String?,
    customerPhone: json['customerPhone'] as String?,
    customerId: json['customerId'] as String?,
    customerCode: json['customerCode'] as String?,
    netTotal: (json['netTotal'] as num?)?.toDouble() ?? 0,
    amountPaid: (json['amountPaid'] as num?)?.toDouble() ?? 0,
    creditAmount: (json['creditAmount'] as num?)?.toDouble() ?? 0,
    returnState: _enumName(json['returnState']),
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => ReturnableSaleItem.fromJson(x as Map<String, dynamic>))
        .toList(),
    originalPayments: (json['originalPayments'] as List<dynamic>? ?? [])
        .map((x) => SalePaymentDetail.fromJson(x as Map<String, dynamic>))
        .toList(),
  );
}

class ReturnableSaleItem {
  const ReturnableSaleItem({
    required this.saleItemId,
    required this.productName,
    required this.sku,
    required this.soldQuantity,
    required this.alreadyReturnedQuantity,
    required this.remainingQuantity,
    required this.originalNetAmount,
    required this.remainingRefundAmount,
    required this.allocations,
  });
  final String saleItemId, productName, sku;
  final int soldQuantity, alreadyReturnedQuantity, remainingQuantity;
  final double originalNetAmount, remainingRefundAmount;
  final List<ReturnableAllocation> allocations;
  factory ReturnableSaleItem.fromJson(Map<String, dynamic> json) =>
      ReturnableSaleItem(
        saleItemId: json['saleItemId'] as String? ?? '',
        productName: json['productName'] as String? ?? '',
        sku: json['sku'] as String? ?? '',
        soldQuantity: json['soldQuantity'] as int? ?? 0,
        alreadyReturnedQuantity: json['alreadyReturnedQuantity'] as int? ?? 0,
        remainingQuantity: json['remainingQuantity'] as int? ?? 0,
        originalNetAmount: (json['originalNetAmount'] as num?)?.toDouble() ?? 0,
        remainingRefundAmount:
            (json['remainingRefundAmount'] as num?)?.toDouble() ?? 0,
        allocations: (json['allocations'] as List<dynamic>? ?? [])
            .map(
              (x) => ReturnableAllocation.fromJson(x as Map<String, dynamic>),
            )
            .toList(),
      );
}

class ReturnableAllocation {
  const ReturnableAllocation({
    required this.allocationId,
    required this.productBatchId,
    required this.batchNumber,
    required this.expiryDate,
    required this.originalQuantity,
    required this.alreadyReturnedQuantity,
    required this.remainingQuantity,
    required this.unitSalePriceSnapshot,
    required this.refundRemaining,
    required this.isBatchDisposed,
    required this.isBatchExpired,
  });
  final String allocationId, productBatchId, batchNumber;
  final DateTime expiryDate;
  final int originalQuantity, alreadyReturnedQuantity, remainingQuantity;
  final double unitSalePriceSnapshot, refundRemaining;
  final bool isBatchDisposed, isBatchExpired;
  factory ReturnableAllocation.fromJson(Map<String, dynamic> json) =>
      ReturnableAllocation(
        allocationId: json['allocationId'] as String? ?? '',
        productBatchId: json['productBatchId'] as String? ?? '',
        batchNumber: json['batchNumber'] as String? ?? '',
        expiryDate: _date(json['expiryDate']) ?? DateTime.now(),
        originalQuantity: json['originalQuantity'] as int? ?? 0,
        alreadyReturnedQuantity: json['alreadyReturnedQuantity'] as int? ?? 0,
        remainingQuantity: json['remainingQuantity'] as int? ?? 0,
        unitSalePriceSnapshot:
            (json['unitSalePriceSnapshot'] as num?)?.toDouble() ?? 0,
        refundRemaining: (json['refundRemaining'] as num?)?.toDouble() ?? 0,
        isBatchDisposed: json['isBatchDisposed'] as bool? ?? false,
        isBatchExpired: json['isBatchExpired'] as bool? ?? false,
      );
}

class SalesReturnListItem {
  const SalesReturnListItem({
    required this.id,
    required this.returnNumber,
    required this.originalInvoiceNumber,
    required this.returnDateUtc,
    required this.branchName,
    required this.processedByName,
    required this.itemCount,
    required this.refundAmount,
    required this.status,
    required this.reason,
    this.customerName,
  });
  final String id,
      returnNumber,
      originalInvoiceNumber,
      branchName,
      processedByName,
      status,
      reason;
  final DateTime returnDateUtc;
  final String? customerName;
  final int itemCount;
  final double refundAmount;
  factory SalesReturnListItem.fromJson(Map<String, dynamic> json) =>
      SalesReturnListItem(
        id: json['id'] as String? ?? '',
        returnNumber: json['returnNumber'] as String? ?? '',
        originalInvoiceNumber: json['originalInvoiceNumber'] as String? ?? '',
        returnDateUtc: DateTime.parse(
          json['returnDateUtc'] as String,
        ).toLocal(),
        branchName: json['branchName'] as String? ?? '',
        processedByName: json['processedByName'] as String? ?? '',
        customerName: json['customerName'] as String?,
        itemCount: json['itemCount'] as int? ?? 0,
        refundAmount: (json['refundAmount'] as num?)?.toDouble() ?? 0,
        status: _enumName(json['status']),
        reason: _enumName(json['reason']),
      );
}

class PagedSalesReturns {
  const PagedSalesReturns({required this.items, required this.totalCount});
  final List<SalesReturnListItem> items;
  final int totalCount;
  factory PagedSalesReturns.fromJson(Map<String, dynamic> json) =>
      PagedSalesReturns(
        items: (json['items'] as List<dynamic>? ?? [])
            .map((x) => SalesReturnListItem.fromJson(x as Map<String, dynamic>))
            .toList(),
        totalCount: json['totalCount'] as int? ?? 0,
      );
}

class SalesReturnDetails {
  const SalesReturnDetails({
    required this.id,
    required this.returnNumber,
    required this.originalInvoiceNumber,
    required this.returnDateUtc,
    required this.branchName,
    required this.processedByName,
    required this.reason,
    required this.refundAmount,
    required this.customerCreditReductionAmount,
    required this.cashRefundAmount,
    required this.items,
    required this.refundPayments,
    this.customerName,
    this.notes,
  });
  final String id,
      returnNumber,
      originalInvoiceNumber,
      branchName,
      processedByName,
      reason;
  final DateTime returnDateUtc;
  final String? customerName, notes;
  final double refundAmount, customerCreditReductionAmount, cashRefundAmount;
  final List<SalesReturnItemDetail> items;
  final List<SalesRefundPaymentDetail> refundPayments;
  factory SalesReturnDetails.fromJson(
    Map<String, dynamic> json,
  ) => SalesReturnDetails(
    id: json['id'] as String? ?? '',
    returnNumber: json['returnNumber'] as String? ?? '',
    originalInvoiceNumber: json['originalInvoiceNumber'] as String? ?? '',
    returnDateUtc: DateTime.parse(json['returnDateUtc'] as String).toLocal(),
    branchName: json['branchName'] as String? ?? '',
    processedByName: json['processedByName'] as String? ?? '',
    reason: _enumName(json['reason']),
    notes: json['notes'] as String?,
    customerName: json['customerName'] as String?,
    refundAmount: (json['refundAmount'] as num?)?.toDouble() ?? 0,
    customerCreditReductionAmount:
        (json['customerCreditReductionAmount'] as num?)?.toDouble() ?? 0,
    cashRefundAmount: (json['cashRefundAmount'] as num?)?.toDouble() ?? 0,
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => SalesReturnItemDetail.fromJson(x as Map<String, dynamic>))
        .toList(),
    refundPayments: (json['refundPayments'] as List<dynamic>? ?? [])
        .map(
          (x) => SalesRefundPaymentDetail.fromJson(x as Map<String, dynamic>),
        )
        .toList(),
  );
}

class SalesReturnItemDetail {
  const SalesReturnItemDetail({
    required this.productName,
    required this.sku,
    required this.quantity,
    required this.refundAmount,
    required this.allocations,
  });
  final String productName, sku;
  final int quantity;
  final double refundAmount;
  final List<SalesReturnAllocationDetail> allocations;
  factory SalesReturnItemDetail.fromJson(Map<String, dynamic> json) =>
      SalesReturnItemDetail(
        productName: json['productName'] as String? ?? '',
        sku: json['sku'] as String? ?? '',
        quantity: json['quantity'] as int? ?? 0,
        refundAmount: (json['refundAmount'] as num?)?.toDouble() ?? 0,
        allocations: (json['allocations'] as List<dynamic>? ?? [])
            .map(
              (x) => SalesReturnAllocationDetail.fromJson(
                x as Map<String, dynamic>,
              ),
            )
            .toList(),
      );
}

class SalesReturnAllocationDetail {
  const SalesReturnAllocationDetail({
    required this.batchNumber,
    required this.quantity,
    required this.disposition,
    required this.refundAmount,
  });
  final String batchNumber, disposition;
  final int quantity;
  final double refundAmount;
  factory SalesReturnAllocationDetail.fromJson(Map<String, dynamic> json) =>
      SalesReturnAllocationDetail(
        batchNumber: json['batchNumber'] as String? ?? '',
        quantity: json['quantity'] as int? ?? 0,
        disposition: _enumName(json['disposition']),
        refundAmount: (json['refundAmount'] as num?)?.toDouble() ?? 0,
      );
}

class SalesRefundPaymentDetail {
  const SalesRefundPaymentDetail({
    required this.method,
    required this.amount,
    this.referenceNumber,
  });
  final String method;
  final double amount;
  final String? referenceNumber;
  factory SalesRefundPaymentDetail.fromJson(Map<String, dynamic> json) =>
      SalesRefundPaymentDetail(
        method: _enumName(json['method']),
        amount: (json['amount'] as num?)?.toDouble() ?? 0,
        referenceNumber: json['referenceNumber'] as String?,
      );
}

String _enumName(dynamic value) {
  if (value == null) return '';
  if (value is String) return value;
  return value.toString();
}

class FinancialAccountInfo {
  const FinancialAccountInfo({
    required this.id,
    required this.branchId,
    required this.branchName,
    required this.name,
    required this.accountType,
    required this.openingBalance,
    required this.currentBalance,
    required this.isActive,
    this.notes,
  });
  final String id, branchId, branchName, name, accountType;
  final double openingBalance, currentBalance;
  final bool isActive;
  final String? notes;
  factory FinancialAccountInfo.fromJson(Map<String, dynamic> json) =>
      FinancialAccountInfo(
        id: json['id'] as String? ?? '',
        branchId: json['branchId'] as String? ?? '',
        branchName: json['branchName'] as String? ?? '',
        name: json['name'] as String? ?? '',
        accountType: _enumName(json['accountType']),
        openingBalance: (json['openingBalance'] as num?)?.toDouble() ?? 0,
        currentBalance: (json['currentBalance'] as num?)?.toDouble() ?? 0,
        isActive: json['isActive'] as bool? ?? false,
        notes: json['notes'] as String?,
      );
}

class ExpenseCategoryInfo {
  const ExpenseCategoryInfo({
    required this.id,
    required this.name,
    required this.isActive,
    this.description,
  });
  final String id, name;
  final bool isActive;
  final String? description;
  factory ExpenseCategoryInfo.fromJson(Map<String, dynamic> json) =>
      ExpenseCategoryInfo(
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        isActive: json['isActive'] as bool? ?? false,
        description: json['description'] as String?,
      );
}

class ExpenseInfo {
  const ExpenseInfo({
    required this.id,
    required this.expenseNumber,
    required this.branchName,
    required this.categoryName,
    required this.accountName,
    required this.expenseDateUtc,
    required this.amount,
    required this.description,
    required this.createdByName,
    this.payee,
  });
  final String id,
      expenseNumber,
      branchName,
      categoryName,
      accountName,
      description,
      createdByName;
  final DateTime expenseDateUtc;
  final double amount;
  final String? payee;
  factory ExpenseInfo.fromJson(Map<String, dynamic> json) => ExpenseInfo(
    id: json['id'] as String? ?? '',
    expenseNumber: json['expenseNumber'] as String? ?? '',
    branchName: json['branchName'] as String? ?? '',
    categoryName: json['categoryName'] as String? ?? '',
    accountName: json['accountName'] as String? ?? '',
    expenseDateUtc: DateTime.parse(json['expenseDateUtc'] as String).toLocal(),
    amount: (json['amount'] as num?)?.toDouble() ?? 0,
    description: json['description'] as String? ?? '',
    createdByName: json['createdByName'] as String? ?? '',
    payee: json['payee'] as String?,
  );
}

class FinancialLedgerItem {
  const FinancialLedgerItem({
    required this.id,
    required this.occurredAtUtc,
    required this.entryType,
    required this.description,
    required this.amount,
    required this.runningBalance,
    required this.createdByName,
  });
  final String id, entryType, description, createdByName;
  final DateTime occurredAtUtc;
  final double amount, runningBalance;
  factory FinancialLedgerItem.fromJson(Map<String, dynamic> json) =>
      FinancialLedgerItem(
        id: json['id'] as String? ?? '',
        occurredAtUtc: DateTime.parse(
          json['occurredAtUtc'] as String,
        ).toLocal(),
        entryType: _enumName(json['entryType']),
        description: json['description'] as String? ?? '',
        amount: (json['amount'] as num?)?.toDouble() ?? 0,
        runningBalance: (json['runningBalance'] as num?)?.toDouble() ?? 0,
        createdByName: json['createdByName'] as String? ?? '',
      );
}

class DailyCashPosition {
  const DailyCashPosition({
    required this.openingBalance,
    required this.moneyIn,
    required this.moneyOut,
    required this.closingBalance,
  });
  final double openingBalance, moneyIn, moneyOut, closingBalance;
  factory DailyCashPosition.fromJson(Map<String, dynamic> json) =>
      DailyCashPosition(
        openingBalance: (json['openingBalance'] as num?)?.toDouble() ?? 0,
        moneyIn: (json['moneyIn'] as num?)?.toDouble() ?? 0,
        moneyOut: (json['moneyOut'] as num?)?.toDouble() ?? 0,
        closingBalance: (json['closingBalance'] as num?)?.toDouble() ?? 0,
      );
}

class StockTransferListItem {
  const StockTransferListItem({
    required this.id,
    required this.transferNumber,
    required this.transferDate,
    required this.status,
    required this.sourceBranchId,
    required this.sourceBranchName,
    required this.sourceGodownId,
    required this.sourceGodownName,
    required this.destinationBranchId,
    required this.destinationBranchName,
    required this.destinationGodownId,
    required this.destinationGodownName,
    required this.quantityRequested,
    required this.quantityApproved,
    required this.quantityDispatched,
    required this.quantityReceived,
    required this.quantityInTransit,
    required this.createdAt,
    this.requestedBy,
  });
  final String id, transferNumber, status;
  final String sourceBranchId, sourceBranchName, sourceGodownId, sourceGodownName;
  final String destinationBranchId, destinationBranchName, destinationGodownId, destinationGodownName;
  final String? requestedBy;
  final DateTime transferDate, createdAt;
  final int quantityRequested, quantityApproved, quantityDispatched, quantityReceived, quantityInTransit;
  factory StockTransferListItem.fromJson(Map<String, dynamic> json) => StockTransferListItem(
    id: json['id'] as String,
    transferNumber: json['transferNumber'] as String? ?? '',
    transferDate: _date(json['transferDate']) ?? DateTime.now(),
    status: _enumName(json['status']),
    sourceBranchId: json['sourceBranchId'] as String? ?? '',
    sourceBranchName: json['sourceBranchName'] as String? ?? '',
    sourceGodownId: json['sourceGodownId'] as String? ?? '',
    sourceGodownName: json['sourceGodownName'] as String? ?? '',
    destinationBranchId: json['destinationBranchId'] as String? ?? '',
    destinationBranchName: json['destinationBranchName'] as String? ?? '',
    destinationGodownId: json['destinationGodownId'] as String? ?? '',
    destinationGodownName: json['destinationGodownName'] as String? ?? '',
    quantityRequested: json['quantityRequested'] as int? ?? 0,
    quantityApproved: json['quantityApproved'] as int? ?? 0,
    quantityDispatched: json['quantityDispatched'] as int? ?? 0,
    quantityReceived: json['quantityReceived'] as int? ?? 0,
    quantityInTransit: json['quantityInTransit'] as int? ?? 0,
    requestedBy: json['requestedBy'] as String?,
    createdAt: _date(json['createdAt']) ?? DateTime.now(),
  );
}

class PagedStockTransfers {
  const PagedStockTransfers({required this.items, required this.totalCount});
  final List<StockTransferListItem> items;
  final int totalCount;
  factory PagedStockTransfers.fromJson(Map<String, dynamic> json) => PagedStockTransfers(
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => StockTransferListItem.fromJson(x as Map<String, dynamic>))
        .toList(),
    totalCount: json['totalCount'] as int? ?? 0,
  );
}

class StockTransferLineItem {
  const StockTransferLineItem({
    required this.id,
    required this.productId,
    required this.productName,
    required this.sku,
    required this.sourceProductBatchId,
    required this.batchNumber,
    required this.expiryDate,
    required this.unitCostSnapshot,
    required this.quantityRequested,
    required this.quantityApproved,
    required this.quantityDispatched,
    required this.quantityReceived,
    required this.quantityInTransit,
    this.destinationProductBatchId,
    this.notes,
  });
  final String id, productId, productName, sku, sourceProductBatchId, batchNumber;
  final String? destinationProductBatchId, notes;
  final DateTime expiryDate;
  final double unitCostSnapshot;
  final int quantityRequested, quantityApproved, quantityDispatched, quantityReceived, quantityInTransit;
  factory StockTransferLineItem.fromJson(Map<String, dynamic> json) => StockTransferLineItem(
    id: json['id'] as String,
    productId: json['productId'] as String? ?? '',
    productName: json['productName'] as String? ?? '',
    sku: json['sku'] as String? ?? '',
    sourceProductBatchId: json['sourceProductBatchId'] as String? ?? '',
    batchNumber: json['batchNumber'] as String? ?? '',
    expiryDate: _date(json['expiryDate']) ?? DateTime.now(),
    unitCostSnapshot: (json['unitCostSnapshot'] as num?)?.toDouble() ?? 0,
    destinationProductBatchId: json['destinationProductBatchId'] as String?,
    quantityRequested: json['quantityRequested'] as int? ?? 0,
    quantityApproved: json['quantityApproved'] as int? ?? 0,
    quantityDispatched: json['quantityDispatched'] as int? ?? 0,
    quantityReceived: json['quantityReceived'] as int? ?? 0,
    quantityInTransit: json['quantityInTransit'] as int? ?? 0,
    notes: json['notes'] as String?,
  );
}

class StockTransferDetails {
  const StockTransferDetails({
    required this.id,
    required this.transferNumber,
    required this.transferDate,
    required this.status,
    required this.sourceBranchId,
    required this.sourceBranchName,
    required this.sourceGodownId,
    required this.sourceGodownName,
    required this.destinationBranchId,
    required this.destinationBranchName,
    required this.destinationGodownId,
    required this.destinationGodownName,
    required this.createdAt,
    required this.items,
    this.notes,
    this.createdBy,
    this.requestedBy,
    this.requestedAtUtc,
    this.approvedBy,
    this.approvedAtUtc,
    this.dispatchedBy,
    this.dispatchedAtUtc,
    this.receivedBy,
    this.receivedAtUtc,
    this.cancelledBy,
    this.cancelledAtUtc,
    this.cancellationReason,
  });
  final String id, transferNumber, status;
  final String sourceBranchId, sourceBranchName, sourceGodownId, sourceGodownName;
  final String destinationBranchId, destinationBranchName, destinationGodownId, destinationGodownName;
  final String? notes, createdBy, requestedBy, approvedBy, dispatchedBy, receivedBy, cancelledBy, cancellationReason;
  final DateTime transferDate, createdAt;
  final DateTime? requestedAtUtc, approvedAtUtc, dispatchedAtUtc, receivedAtUtc, cancelledAtUtc;
  final List<StockTransferLineItem> items;
  factory StockTransferDetails.fromJson(Map<String, dynamic> json) => StockTransferDetails(
    id: json['id'] as String,
    transferNumber: json['transferNumber'] as String? ?? '',
    transferDate: _date(json['transferDate']) ?? DateTime.now(),
    status: _enumName(json['status']),
    notes: json['notes'] as String?,
    sourceBranchId: json['sourceBranchId'] as String? ?? '',
    sourceBranchName: json['sourceBranchName'] as String? ?? '',
    sourceGodownId: json['sourceGodownId'] as String? ?? '',
    sourceGodownName: json['sourceGodownName'] as String? ?? '',
    destinationBranchId: json['destinationBranchId'] as String? ?? '',
    destinationBranchName: json['destinationBranchName'] as String? ?? '',
    destinationGodownId: json['destinationGodownId'] as String? ?? '',
    destinationGodownName: json['destinationGodownName'] as String? ?? '',
    createdBy: json['createdBy'] as String?,
    createdAt: _date(json['createdAt']) ?? DateTime.now(),
    requestedBy: json['requestedBy'] as String?,
    requestedAtUtc: _date(json['requestedAtUtc']),
    approvedBy: json['approvedBy'] as String?,
    approvedAtUtc: _date(json['approvedAtUtc']),
    dispatchedBy: json['dispatchedBy'] as String?,
    dispatchedAtUtc: _date(json['dispatchedAtUtc']),
    receivedBy: json['receivedBy'] as String?,
    receivedAtUtc: _date(json['receivedAtUtc']),
    cancelledBy: json['cancelledBy'] as String?,
    cancelledAtUtc: _date(json['cancelledAtUtc']),
    cancellationReason: json['cancellationReason'] as String?,
    items: (json['items'] as List<dynamic>? ?? [])
        .map((x) => StockTransferLineItem.fromJson(x as Map<String, dynamic>))
        .toList(),
  );
}

class TransferableBatch {
  const TransferableBatch({
    required this.productBatchId,
    required this.productId,
    required this.productName,
    required this.sku,
    required this.batchNumber,
    required this.expiryDate,
    required this.quantityAvailable,
    required this.purchasePrice,
    required this.retailPrice,
  });
  final String productBatchId, productId, productName, sku, batchNumber;
  final DateTime expiryDate;
  final int quantityAvailable;
  final double purchasePrice, retailPrice;
  factory TransferableBatch.fromJson(Map<String, dynamic> json) => TransferableBatch(
    productBatchId: json['productBatchId'] as String,
    productId: json['productId'] as String? ?? '',
    productName: json['productName'] as String? ?? '',
    sku: json['sku'] as String? ?? '',
    batchNumber: json['batchNumber'] as String? ?? '',
    expiryDate: _date(json['expiryDate']) ?? DateTime.now(),
    quantityAvailable: json['quantityAvailable'] as int? ?? 0,
    purchasePrice: (json['purchasePrice'] as num?)?.toDouble() ?? 0,
    retailPrice: (json['retailPrice'] as num?)?.toDouble() ?? 0,
  );
}
