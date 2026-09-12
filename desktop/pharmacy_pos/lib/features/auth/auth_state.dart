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
  Future<dynamic> administration(
    String path, {
    String method = 'GET',
    Map<String, dynamic>? body,
  }) => _api.administration(_requiredToken, path, method: method, body: body);
  Future<dynamic> accounting(
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  }) => _api.accounting(
    _requiredToken,
    path,
    method: method,
    query: query,
    body: body,
  );
  Future<dynamic> stockTransfers(
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  }) => _api.stockTransfers(
    _requiredToken,
    path,
    method: method,
    query: query,
    body: body,
  );
  Future<dynamic> pricing(
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  }) => _api.pricing(
    _requiredToken,
    path,
    method: method,
    query: query,
    body: body,
  );
  Future<dynamic> phase6(
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  }) => _api.phase6(
    _requiredToken,
    path,
    method: method,
    query: query,
    body: body,
  );
  Future<dynamic> salesQuotations(
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  }) => _api.salesQuotations(
    _requiredToken,
    path,
    method: method,
    query: query,
    body: body,
  );
  Future<dynamic> salesOrders(
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  }) => _api.salesOrders(
    _requiredToken,
    path,
    method: method,
    query: query,
    body: body,
  );
  Future<void> signOutEverywhere() async {
    await administration('sessions/revoke', method: 'POST');
    await logout();
  }

  Future<void> expireSession() async {
    _token = null;
    currentUser = null;
    errorMessage = 'Your session expired. Sign in again.';
    status = AuthenticationStatus.unauthenticated;
    await _tokenStore.clear();
    notifyListeners();
  }

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
  Future<List<ExpiryItem>> listExpiry({int? days, String? godownId}) =>
      _api.listExpiry(_requiredToken, days: days, godownId: godownId);
  Future<PagedBatches> listBatches({String? search, String? godownId}) =>
      _api.listBatches(_requiredToken, search: search, godownId: godownId);
  Future<PagedMovements> listMovements({String? search, String? godownId}) =>
      _api.listMovements(_requiredToken, search: search, godownId: godownId);
  Future<PagedStockCountSessions> listStockCountSessions({
    String? branchId,
    String? status,
  }) => _api.listStockCountSessions(
    _requiredToken,
    branchId: branchId,
    status: status,
  );
  Future<StockCountSession> getStockCountSession(String id) =>
      _api.getStockCountSession(_requiredToken, id);
  Future<StockCountSession> createStockCountSession(
    Map<String, dynamic> values,
  ) => _api.createStockCountSession(_requiredToken, values);
  Future<StockCountSession> startStockCountSession(String id) =>
      _api.startStockCountSession(_requiredToken, id);
  Future<StockCountSession> submitStockCountEntries(
    String id,
    List<Map<String, dynamic>> entries,
  ) => _api.submitStockCountEntries(_requiredToken, id, entries);
  Future<StockCountSession> finalizeStockCountSession(String id) =>
      _api.finalizeStockCountSession(_requiredToken, id);
  Future<StockCountSession> cancelStockCountSession(String id, String reason) =>
      _api.cancelStockCountSession(_requiredToken, id, reason);
  Future<CashierShift?> myOpenCashierShift() =>
      _api.myOpenCashierShift(_requiredToken);
  Future<CashierShift> openCashierShift(Map<String, dynamic> values) =>
      _api.openCashierShift(_requiredToken, values);
  Future<CashierShift> addCashierShiftDrawerEntry(
    String id,
    Map<String, dynamic> values,
  ) => _api.addCashierShiftDrawerEntry(_requiredToken, id, values);
  Future<CashierShift> closeCashierShift(String id, Map<String, dynamic> values) =>
      _api.closeCashierShift(_requiredToken, id, values);
  Future<CashierShift> reconcileCashierShift(String id, String? notes) =>
      _api.reconcileCashierShift(_requiredToken, id, notes);
  Future<CashierShift> cashierShiftDetails(String id) =>
      _api.cashierShiftDetails(_requiredToken, id);
  Future<PagedCashierShifts> listCashierShifts({String? status}) =>
      _api.listCashierShifts(_requiredToken, status: status);
  Future<DailyClosingSummary> dailyCashierClosingSummary(
    String branchId,
    DateTime date,
  ) => _api.dailyCashierClosingSummary(_requiredToken, branchId, date);
  Future<PagedGodowns> listGodowns({
    String? branchId,
    String? search,
    bool? isActive,
  }) => _api.listGodowns(
    _requiredToken,
    branchId: branchId,
    search: search,
    isActive: isActive,
  );
  Future<List<GodownLookup>> lookupGodowns({
    String? branchId,
    bool activeOnly = true,
  }) => _api.lookupGodowns(
    _requiredToken,
    branchId: branchId,
    activeOnly: activeOnly,
  );
  Future<List<GodownLookup>> myGodowns({String? branchId}) =>
      _api.myGodowns(_requiredToken, branchId: branchId);
  Future<GodownListItem> createGodown(Map<String, dynamic> values) =>
      _api.createGodown(_requiredToken, values);
  Future<GodownListItem> updateGodown(
    String id,
    Map<String, dynamic> values,
  ) => _api.updateGodown(_requiredToken, id, values);
  Future<void> setGodownActive(String id, bool active) =>
      _api.setGodownActive(_requiredToken, id, active);
  Future<GodownListItem> setGodownDefault(String id) =>
      _api.setGodownDefault(_requiredToken, id);
  Future<List<UserGodownAssignment>> listGodownUsers(String id) =>
      _api.listGodownUsers(_requiredToken, id);
  Future<void> assignUserGodown(String godownId, Map<String, dynamic> values) =>
      _api.assignUserGodown(_requiredToken, godownId, values);
  Future<void> unassignUserGodown(String godownId, String userId) =>
      _api.unassignUserGodown(_requiredToken, godownId, userId);
  Future<void> setUserDefaultGodown(String godownId, String userId) =>
      _api.setUserDefaultGodown(_requiredToken, godownId, userId);
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
  Future<PagedSupplierLedger> supplierLedger(String id, {DateTime? from, DateTime? to, String? branchId}) =>
      _api.supplierLedger(_requiredToken, id, from: from, to: to, branchId: branchId);
  Future<void> recordSupplierPayment(String id, Map<String, dynamic> values) =>
      _api.recordSupplierPayment(_requiredToken, id, values);
  Future<void> adjustSupplierBalance(String id, Map<String, dynamic> values) =>
      _api.adjustSupplierBalance(_requiredToken, id, values);
  Future<PagedCustomers> listCustomers({String? search, bool? isActive}) =>
      _api.listCustomers(_requiredToken, search: search, isActive: isActive);
  Future<List<CustomerLookup>> lookupCustomers({String? search}) =>
      _api.lookupCustomers(_requiredToken, search: search);
  Future<CustomerDetails> customerDetails(String id) =>
      _api.customerDetails(_requiredToken, id);
  Future<CustomerListItem> createCustomer(Map<String, dynamic> values) =>
      _api.createCustomer(_requiredToken, values);
  Future<CustomerListItem> updateCustomer(
    String id,
    Map<String, dynamic> values,
  ) => _api.updateCustomer(_requiredToken, id, values);
  Future<void> setCustomerActive(String id, bool active) =>
      _api.setCustomerActive(_requiredToken, id, active);
  Future<PagedCustomerLedger> customerLedger(String id, {DateTime? from, DateTime? to, String? branchId}) =>
      _api.customerLedger(_requiredToken, id, from: from, to: to, branchId: branchId);
  Future<void> recordCustomerPayment(String id, Map<String, dynamic> values) =>
      _api.recordCustomerPayment(_requiredToken, id, values);
  Future<void> adjustCustomerBalance(String id, Map<String, dynamic> values) =>
      _api.adjustCustomerBalance(_requiredToken, id, values);

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
  Future<ReturnablePurchase> returnablePurchase(String receiptId) =>
      _api.returnablePurchase(_requiredToken, receiptId);
  Future<PurchaseReturnDetails> postPurchaseReturn(
    String receiptId,
    Map<String, dynamic> values,
  ) => _api.postPurchaseReturn(_requiredToken, receiptId, values);
  Future<PagedPurchaseReturns> listPurchaseReturns({String? search}) =>
      _api.listPurchaseReturns(_requiredToken, search: search);
  Future<PurchaseReturnDetails> purchaseReturnDetails(String id) =>
      _api.purchaseReturnDetails(_requiredToken, id);
  Future<PurchaseReturnDetails> purchaseReturnNote(String id) =>
      _api.purchaseReturnNote(_requiredToken, id);
  Future<PurchaseReturnDetails> reprintPurchaseReturnNote(String id) =>
      _api.reprintPurchaseReturnNote(_requiredToken, id);

  Future<List<PosProduct>> searchPosProducts({
    String? query,
    String? godownId,
  }) => _api.searchPosProducts(_requiredToken, query: query, godownId: godownId);
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
  Future<ReturnableSale> returnableSale(String saleId) =>
      _api.returnableSale(_requiredToken, saleId);
  Future<SalesReturnDetails> postSalesReturn(
    String saleId,
    Map<String, dynamic> values,
  ) => _api.postSalesReturn(_requiredToken, saleId, values);
  Future<PagedSalesReturns> listSalesReturns({String? search}) =>
      _api.listSalesReturns(_requiredToken, search: search);
  Future<SalesReturnDetails> salesReturnDetails(String id) =>
      _api.salesReturnDetails(_requiredToken, id);
  Future<SalesReturnDetails> salesReturnReceipt(String id) =>
      _api.salesReturnReceipt(_requiredToken, id);
  Future<SalesReturnDetails> reprintSalesReturnReceipt(String id) =>
      _api.reprintSalesReturnReceipt(_requiredToken, id);
  Future<List<FinancialAccountInfo>> listFinancialAccounts({
    String? branchId,
  }) => _api.listFinancialAccounts(_requiredToken, branchId: branchId);
  Future<FinancialAccountInfo> createFinancialAccount(
    Map<String, dynamic> values,
  ) => _api.createFinancialAccount(_requiredToken, values);
  Future<List<ExpenseCategoryInfo>> listExpenseCategories() =>
      _api.listExpenseCategories(_requiredToken);
  Future<List<ExpenseInfo>> listExpenses() => _api.listExpenses(_requiredToken);
  Future<ExpenseInfo> postExpense(Map<String, dynamic> values) =>
      _api.postExpense(_requiredToken, values);
  Future<void> reverseExpense(String id, String reason) =>
      _api.reverseExpense(_requiredToken, id, reason);
  Future<void> postOtherIncome(Map<String, dynamic> values) =>
      _api.postOtherIncome(_requiredToken, values);
  Future<void> reverseOtherIncome(String id, String reason) =>
      _api.reverseOtherIncome(_requiredToken, id, reason);
  Future<void> postFinancialTransfer(Map<String, dynamic> values) =>
      _api.postFinancialTransfer(_requiredToken, values);
  Future<List<FinancialLedgerItem>> financialLedger(String accountId) =>
      _api.financialLedger(_requiredToken, accountId);
  Future<DailyCashPosition> dailyCashPosition(
    String branchId,
    DateTime date, {
    String? accountId,
  }) => _api.dailyCashPosition(
    _requiredToken,
    branchId,
    date,
    accountId: accountId,
  );
  Future<dynamic> report(
    String path, {
    required DateTime fromUtc,
    required DateTime toUtc,
    String? branchId,
    String? option,
    Map<String, String>? filters,
  }) => _api.report(
    _requiredToken,
    path,
    fromUtc: fromUtc,
    toUtc: toUtc,
    branchId: branchId,
    option: option,
    filters: filters,
  );
  Future<List<int>> exportReport(
    String path, {
    required DateTime fromUtc,
    required DateTime toUtc,
    String? branchId,
    String? option,
    Map<String, String>? filters,
  }) => _api.exportReport(
    _requiredToken,
    path,
    fromUtc: fromUtc,
    toUtc: toUtc,
    branchId: branchId,
    option: option,
    filters: filters,
  );
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
