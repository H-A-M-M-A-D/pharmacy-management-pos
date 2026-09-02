import 'package:flutter/foundation.dart';

import '../../core/api_client.dart';
import '../../core/models.dart';
import '../../core/token_store.dart';

enum AuthenticationStatus {
  initializing,
  unauthenticated,
  authenticating,
  authenticated,
}

class AuthState extends ChangeNotifier {
  AuthState(this._api, this._tokenStore);

  final PharmacyApi _api;
  final TokenStore _tokenStore;

  AuthenticationStatus status = AuthenticationStatus.initializing;
  CurrentUser? currentUser;
  String? errorMessage;
  String? _token;

  bool get mustChangePassword => currentUser?.mustChangePassword ?? false;
  bool can(String permission) => currentUser?.can(permission) ?? false;

  Future<void> initialize() async {
    final token = await _tokenStore.read();
    if (token == null) {
      status = AuthenticationStatus.unauthenticated;
      notifyListeners();
      return;
    }

    try {
      _token = token;
      currentUser = await _api.me(token);
      status = AuthenticationStatus.authenticated;
    } on ApiException {
      await _tokenStore.clear();
      _token = null;
      currentUser = null;
      status = AuthenticationStatus.unauthenticated;
    }
    notifyListeners();
  }

  Future<bool> login(String username, String password) async {
    status = AuthenticationStatus.authenticating;
    errorMessage = null;
    notifyListeners();
    try {
      final session = await _api.login(username, password);
      _token = session.accessToken;
      currentUser = session.user;
      await _tokenStore.write(session.accessToken);
      status = AuthenticationStatus.authenticated;
      notifyListeners();
      return true;
    } on ApiException catch (error) {
      _token = null;
      currentUser = null;
      status = AuthenticationStatus.unauthenticated;
      errorMessage = error.message;
      notifyListeners();
      return false;
    }
  }

  Future<bool> changePassword(
    String currentPassword,
    String newPassword,
  ) async {
    errorMessage = null;
    try {
      final session = await _api.changePassword(
        _requiredToken,
        currentPassword,
        newPassword,
      );
      _token = session.accessToken;
      currentUser = session.user;
      await _tokenStore.write(session.accessToken);
      notifyListeners();
      return true;
    } on ApiException catch (error) {
      errorMessage = error.message;
      notifyListeners();
      return false;
    }
  }

  Future<bool> updateProfile({
    required String fullName,
    String? email,
    String? phoneNumber,
  }) async {
    errorMessage = null;
    try {
      currentUser = await _api.updateProfile(
        _requiredToken,
        fullName: fullName,
        email: email,
        phoneNumber: phoneNumber,
      );
      notifyListeners();
      return true;
    } on ApiException catch (error) {
      errorMessage = error.message;
      notifyListeners();
      return false;
    }
  }

  Future<PagedUsers> listUsers({
    String? search,
    String? roleId,
    String? branchId,
    bool? isActive,
  }) => _api.listUsers(
    _requiredToken,
    search: search,
    roleId: roleId,
    branchId: branchId,
    isActive: isActive,
  );

  Future<UserOptions> userOptions() => _api.userOptions(_requiredToken);
  Future<UserDetails> userDetails(String id) =>
      _api.userDetails(_requiredToken, id);
  Future<UserDetails> createUser(Map<String, dynamic> values) =>
      _api.createUser(_requiredToken, values);
  Future<UserDetails> updateUser(String id, Map<String, dynamic> values) =>
      _api.updateUser(_requiredToken, id, values);
  Future<void> setUserActive(String id, bool active) =>
      _api.setUserActive(_requiredToken, id, active);
  Future<void> resetPassword(String id, String password) =>
      _api.resetPassword(_requiredToken, id, password);

  Future<PagedProducts> listProducts({
    int page = 1,
    String? search,
    String? categoryId,
    String? manufacturerId,
    bool? isActive,
  }) => _api.listProducts(
    _requiredToken,
    page: page,
    search: search,
    categoryId: categoryId,
    manufacturerId: manufacturerId,
    isActive: isActive,
  );
  Future<ProductOptions> productOptions() =>
      _api.productOptions(_requiredToken);
  Future<ProductDetails> productDetails(String id) =>
      _api.productDetails(_requiredToken, id);
  Future<ProductDetails> createProduct(Map<String, dynamic> values) =>
      _api.createProduct(_requiredToken, values);
  Future<ProductDetails> updateProduct(
    String id,
    Map<String, dynamic> values,
  ) => _api.updateProduct(_requiredToken, id, values);
  Future<void> setProductActive(String id, bool active) =>
      _api.setProductActive(_requiredToken, id, active);
  Future<List<CategoryInfo>> listCategories({String? search, bool? isActive}) =>
      _api.listCategories(_requiredToken, search: search, isActive: isActive);
  Future<CategoryInfo> saveCategory(
    Map<String, dynamic> values, {
    String? id,
  }) => _api.saveCategory(_requiredToken, values, id: id);
  Future<void> setCategoryActive(String id, bool active) =>
      _api.setCategoryActive(_requiredToken, id, active);
  Future<List<ManufacturerInfo>> listManufacturers({
    String? search,
    bool? isActive,
  }) => _api.listManufacturers(
    _requiredToken,
    search: search,
    isActive: isActive,
  );
  Future<ManufacturerInfo> saveManufacturer(
    Map<String, dynamic> values, {
    String? id,
  }) => _api.saveManufacturer(_requiredToken, values, id: id);
  Future<void> setManufacturerActive(String id, bool active) =>
      _api.setManufacturerActive(_requiredToken, id, active);

  Future<PagedInventory> listInventory({String? search}) =>
      _api.listInventory(_requiredToken, search: search);
  Future<InventoryOptions> inventoryOptions({String? productSearch}) =>
      _api.inventoryOptions(_requiredToken, productSearch: productSearch);
  Future<void> addOpeningStock(Map<String, dynamic> values) =>
      _api.addOpeningStock(_requiredToken, values);
  Future<void> adjustStock(
    Map<String, dynamic> values, {
    required bool increase,
  }) => _api.adjustStock(_requiredToken, values, increase: increase);
  Future<void> reconcileStockCount(Map<String, dynamic> values) =>
      _api.reconcileStockCount(_requiredToken, values);
  Future<List<ExpiryItem>> listExpiry({int? days}) =>
      _api.listExpiry(_requiredToken, days: days);
  Future<PagedBatches> listBatches({String? search}) =>
      _api.listBatches(_requiredToken, search: search);
  Future<PagedMovements> listMovements({String? search}) =>
      _api.listMovements(_requiredToken, search: search);
  Future<PagedSuppliers> listSuppliers({String? search, bool? isActive}) =>
      _api.listSuppliers(_requiredToken, search: search, isActive: isActive);
  Future<SupplierListItem> createSupplier(Map<String, dynamic> values) =>
      _api.createSupplier(_requiredToken, values);
  Future<SupplierListItem> updateSupplier(
    String id,
    Map<String, dynamic> values,
  ) => _api.updateSupplier(_requiredToken, id, values);
  Future<void> setSupplierActive(String id, bool active) =>
      _api.setSupplierActive(_requiredToken, id, active);
  Future<PagedSupplierLedger> supplierLedger(String id) =>
      _api.supplierLedger(_requiredToken, id);
  Future<void> recordSupplierPayment(String id, Map<String, dynamic> values) =>
      _api.recordSupplierPayment(_requiredToken, id, values);
  Future<void> adjustSupplierBalance(String id, Map<String, dynamic> values) =>
      _api.adjustSupplierBalance(_requiredToken, id, values);

  Future<PagedPurchaseOrders> listPurchaseOrders({String? search}) =>
      _api.listPurchaseOrders(_requiredToken, search: search);
  Future<PurchaseOrderDetails> purchaseOrderDetails(String id) =>
      _api.purchaseOrderDetails(_requiredToken, id);
  Future<PurchaseOrderDetails> createPurchaseOrder(
    Map<String, dynamic> values,
  ) => _api.createPurchaseOrder(_requiredToken, values);
  Future<PurchaseOrderDetails> submitPurchaseOrder(String id) =>
      _api.submitPurchaseOrder(_requiredToken, id);
  Future<PurchaseOrderDetails> cancelPurchaseOrder(String id) =>
      _api.cancelPurchaseOrder(_requiredToken, id);
  Future<PagedPurchases> listPurchases({String? search}) =>
      _api.listPurchases(_requiredToken, search: search);
  Future<PurchaseDetails> purchaseDetails(String id) =>
      _api.purchaseDetails(_requiredToken, id);
  Future<PurchaseDetails> postGoodsReceipt(Map<String, dynamic> values) =>
      _api.postGoodsReceipt(_requiredToken, values);
  Future<PurchaseDetails> postDirectPurchase(Map<String, dynamic> values) =>
      _api.postDirectPurchase(_requiredToken, values);

  Future<List<PosProduct>> searchPosProducts({String? query}) =>
      _api.searchPosProducts(_requiredToken, query: query);
  Future<SaleDetails> holdSale(Map<String, dynamic> values) =>
      _api.holdSale(_requiredToken, values);
  Future<SaleDetails> postSale(Map<String, dynamic> values) =>
      _api.postSale(_requiredToken, values);
  Future<SaleDetails> postHeldSale(String id, Map<String, dynamic> values) =>
      _api.postHeldSale(_requiredToken, id, values);
  Future<void> cancelHeldSale(String id) =>
      _api.cancelHeldSale(_requiredToken, id);
  Future<PagedSales> listHeldSales({String? search}) =>
      _api.listHeldSales(_requiredToken, search: search);
  Future<PagedSales> listSales({String? search}) =>
      _api.listSales(_requiredToken, search: search);
  Future<SaleDetails> saleDetails(String id) =>
      _api.saleDetails(_requiredToken, id);
  Future<SaleDetails> saleReceipt(String id) =>
      _api.saleReceipt(_requiredToken, id);
  Future<SaleDetails> reprintSaleReceipt(String id) =>
      _api.reprintSaleReceipt(_requiredToken, id);
  Future<void> logout() async {
    await _tokenStore.clear();
    _token = null;
    currentUser = null;
    errorMessage = null;
    status = AuthenticationStatus.unauthenticated;
    notifyListeners();
  }

  String get _requiredToken {
    final token = _token;
    if (token == null) throw const ApiException('Session is unavailable.');
    return token;
  }

  @override
  void dispose() {
    _api.close();
    super.dispose();
  }
}
