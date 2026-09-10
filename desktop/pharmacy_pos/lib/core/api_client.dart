import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'models.dart';

class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  @override
  String toString() => message;
}

abstract interface class PharmacyApi {
  Future<LoginSession> login(String username, String password);
  Future<CurrentUser> me(String token);
  Future<LoginSession> changePassword(
    String token,
    String currentPassword,
    String newPassword,
  );
  Future<CurrentUser> updateProfile(
    String token, {
    required String fullName,
    String? email,
    String? phoneNumber,
  });
  Future<PagedUsers> listUsers(
    String token, {
    String? search,
    String? roleId,
    String? branchId,
    bool? isActive,
  });
  Future<UserOptions> userOptions(String token);
  Future<UserDetails> userDetails(String token, String id);
  Future<UserDetails> createUser(String token, Map<String, dynamic> values);
  Future<UserDetails> updateUser(
    String token,
    String id,
    Map<String, dynamic> values,
  );
  Future<void> setUserActive(String token, String id, bool active);
  Future<void> resetPassword(String token, String id, String password);
  Future<PagedProducts> listProducts(
    String token, {
    int page = 1,
    String? search,
    String? categoryId,
    String? manufacturerId,
    bool? isActive,
  });
  Future<ProductOptions> productOptions(String token);
  Future<ProductDetails> productDetails(String token, String id);
  Future<ProductDetails> createProduct(
    String token,
    Map<String, dynamic> values,
  );
  Future<ProductDetails> updateProduct(
    String token,
    String id,
    Map<String, dynamic> values,
  );
  Future<void> setProductActive(String token, String id, bool active);
  Future<List<CategoryInfo>> listCategories(
    String token, {
    String? search,
    bool? isActive,
  });
  Future<CategoryInfo> saveCategory(
    String token,
    Map<String, dynamic> values, {
    String? id,
  });
  Future<void> setCategoryActive(String token, String id, bool active);
  Future<List<ManufacturerInfo>> listManufacturers(
    String token, {
    String? search,
    bool? isActive,
  });
  Future<ManufacturerInfo> saveManufacturer(
    String token,
    Map<String, dynamic> values, {
    String? id,
  });
  Future<void> setManufacturerActive(String token, String id, bool active);
  Future<PagedInventory> listInventory(String token, {String? search});
  Future<InventoryOptions> inventoryOptions(
    String token, {
    String? productSearch,
  });
  Future<void> addOpeningStock(String token, Map<String, dynamic> values);
  Future<void> adjustStock(
    String token,
    Map<String, dynamic> values, {
    required bool increase,
  });
  Future<void> reconcileStockCount(String token, Map<String, dynamic> values);
  Future<List<ExpiryItem>> listExpiry(String token, {int? days});
  Future<PagedBatches> listBatches(String token, {String? search});
  Future<PagedMovements> listMovements(String token, {String? search});
  Future<PagedStockCountSessions> listStockCountSessions(
    String token, {
    String? branchId,
    String? status,
  });
  Future<StockCountSession> getStockCountSession(String token, String id);
  Future<StockCountSession> createStockCountSession(
    String token,
    Map<String, dynamic> values,
  );
  Future<StockCountSession> startStockCountSession(String token, String id);
  Future<StockCountSession> submitStockCountEntries(
    String token,
    String id,
    List<Map<String, dynamic>> entries,
  );
  Future<StockCountSession> finalizeStockCountSession(String token, String id);
  Future<StockCountSession> cancelStockCountSession(
    String token,
    String id,
    String reason,
  );
  Future<CashierShift?> myOpenCashierShift(String token);
  Future<CashierShift> openCashierShift(String token, Map<String, dynamic> values);
  Future<CashierShift> addCashierShiftDrawerEntry(
    String token,
    String id,
    Map<String, dynamic> values,
  );
  Future<CashierShift> closeCashierShift(
    String token,
    String id,
    Map<String, dynamic> values,
  );
  Future<CashierShift> reconcileCashierShift(
    String token,
    String id,
    String? notes,
  );
  Future<CashierShift> cashierShiftDetails(String token, String id);
  Future<PagedCashierShifts> listCashierShifts(
    String token, {
    String? status,
  });
  Future<DailyClosingSummary> dailyCashierClosingSummary(
    String token,
    String branchId,
    DateTime date,
  );
  Future<PagedGodowns> listGodowns(
    String token, {
    String? branchId,
    String? search,
    bool? isActive,
  });
  Future<List<GodownLookup>> lookupGodowns(
    String token, {
    String? branchId,
    bool activeOnly = true,
  });
  Future<List<GodownLookup>> myGodowns(String token, {String? branchId});
  Future<GodownListItem> createGodown(
    String token,
    Map<String, dynamic> values,
  );
  Future<GodownListItem> updateGodown(
    String token,
    String id,
    Map<String, dynamic> values,
  );
  Future<void> setGodownActive(String token, String id, bool active);
  Future<GodownListItem> setGodownDefault(String token, String id);
  Future<List<UserGodownAssignment>> listGodownUsers(String token, String id);
  Future<void> assignUserGodown(
    String token,
    String godownId,
    Map<String, dynamic> values,
  );
  Future<void> unassignUserGodown(
    String token,
    String godownId,
    String userId,
  );
  Future<void> setUserDefaultGodown(
    String token,
    String godownId,
    String userId,
  );
  Future<PagedSuppliers> listSuppliers(
    String token, {
    String? search,
    bool? isActive,
  });
  Future<SupplierListItem> createSupplier(
    String token,
    Map<String, dynamic> values,
  );
  Future<SupplierListItem> updateSupplier(
    String token,
    String id,
    Map<String, dynamic> values,
  );
  Future<void> setSupplierActive(String token, String id, bool active);
  Future<PagedSupplierLedger> supplierLedger(String token, String id, {DateTime? from, DateTime? to, String? branchId});
  Future<void> recordSupplierPayment(
    String token,
    String id,
    Map<String, dynamic> values,
  );
  Future<void> adjustSupplierBalance(
    String token,
    String id,
    Map<String, dynamic> values,
  );
  Future<PagedCustomers> listCustomers(
    String token, {
    String? search,
    bool? isActive,
  });
  Future<List<CustomerLookup>> lookupCustomers(String token, {String? search});
  Future<CustomerDetails> customerDetails(String token, String id);
  Future<CustomerListItem> createCustomer(
    String token,
    Map<String, dynamic> values,
  );
  Future<CustomerListItem> updateCustomer(
    String token,
    String id,
    Map<String, dynamic> values,
  );
  Future<void> setCustomerActive(String token, String id, bool active);
  Future<PagedCustomerLedger> customerLedger(String token, String id, {DateTime? from, DateTime? to, String? branchId});
  Future<void> recordCustomerPayment(
    String token,
    String id,
    Map<String, dynamic> values,
  );
  Future<void> adjustCustomerBalance(
    String token,
    String id,
    Map<String, dynamic> values,
  );
  Future<PagedPurchaseOrders> listPurchaseOrders(
    String token, {
    String? search,
  });
  Future<PurchaseOrderDetails> purchaseOrderDetails(String token, String id);
  Future<PurchaseOrderDetails> createPurchaseOrder(
    String token,
    Map<String, dynamic> values,
  );
  Future<PurchaseOrderDetails> submitPurchaseOrder(String token, String id);
  Future<PurchaseOrderDetails> cancelPurchaseOrder(String token, String id);
  Future<PagedPurchases> listPurchases(String token, {String? search});
  Future<PurchaseDetails> purchaseDetails(String token, String id);
  Future<PurchaseDetails> postGoodsReceipt(
    String token,
    Map<String, dynamic> values,
  );
  Future<PurchaseDetails> postDirectPurchase(
    String token,
    Map<String, dynamic> values,
  );
  Future<ReturnablePurchase> returnablePurchase(String token, String receiptId);
  Future<PurchaseReturnDetails> postPurchaseReturn(
    String token,
    String receiptId,
    Map<String, dynamic> values,
  );
  Future<PagedPurchaseReturns> listPurchaseReturns(
    String token, {
    String? search,
  });
  Future<PurchaseReturnDetails> purchaseReturnDetails(String token, String id);
  Future<PurchaseReturnDetails> purchaseReturnNote(String token, String id);
  Future<PurchaseReturnDetails> reprintPurchaseReturnNote(
    String token,
    String id,
  );
  Future<List<PosProduct>> searchPosProducts(
    String token, {
    String? query,
    String? godownId,
  });
  Future<SaleDetails> holdSale(String token, Map<String, dynamic> values);
  Future<SaleDetails> postSale(String token, Map<String, dynamic> values);
  Future<SaleDetails> postHeldSale(
    String token,
    String id,
    Map<String, dynamic> values,
  );
  Future<void> cancelHeldSale(String token, String id);
  Future<PagedSales> listHeldSales(String token, {String? search});
  Future<PagedSales> listSales(String token, {String? search});
  Future<SaleDetails> saleDetails(String token, String id);
  Future<SaleDetails> saleReceipt(String token, String id);
  Future<SaleDetails> reprintSaleReceipt(String token, String id);
  Future<ReturnableSale> returnableSale(String token, String saleId);
  Future<SalesReturnDetails> postSalesReturn(
    String token,
    String saleId,
    Map<String, dynamic> values,
  );
  Future<PagedSalesReturns> listSalesReturns(String token, {String? search});
  Future<SalesReturnDetails> salesReturnDetails(String token, String id);
  Future<SalesReturnDetails> salesReturnReceipt(String token, String id);
  Future<SalesReturnDetails> reprintSalesReturnReceipt(String token, String id);
  Future<List<FinancialAccountInfo>> listFinancialAccounts(
    String token, {
    String? branchId,
  });
  Future<FinancialAccountInfo> createFinancialAccount(
    String token,
    Map<String, dynamic> values,
  );
  Future<List<ExpenseCategoryInfo>> listExpenseCategories(String token);
  Future<List<ExpenseInfo>> listExpenses(String token);
  Future<ExpenseInfo> postExpense(String token, Map<String, dynamic> values);
  Future<void> postOtherIncome(String token, Map<String, dynamic> values);
  Future<void> postFinancialTransfer(String token, Map<String, dynamic> values);
  Future<List<FinancialLedgerItem>> financialLedger(
    String token,
    String accountId,
  );
  Future<DailyCashPosition> dailyCashPosition(
    String token,
    String branchId,
    DateTime date, {
    String? accountId,
  });
  Future<dynamic> report(
    String token,
    String path, {
    required DateTime fromUtc,
    required DateTime toUtc,
    String? branchId,
    String? option,
  });
  Future<List<int>> exportReport(
    String token,
    String path, {
    required DateTime fromUtc,
    required DateTime toUtc,
    String? branchId,
    String? option,
  });
  Future<dynamic> administration(
    String token,
    String path, {
    String method = 'GET',
    Map<String, dynamic>? body,
  });
  Future<dynamic> accounting(
    String token,
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  });
  void close();
}

class ApiClient implements PharmacyApi {
  ApiClient({Uri? baseUri, HttpClient? httpClient, this.onUnauthorized})
    : baseUri =
          baseUri ??
          Uri.parse(
            Platform.environment['PHARMACY_API_URL'] ??
                const String.fromEnvironment(
                  'API_BASE_URL',
                  defaultValue: 'http://localhost:5000',
                ),
          ),
      _httpClient = httpClient ?? HttpClient();

  final Uri baseUri;
  final HttpClient _httpClient;
  final void Function()? onUnauthorized;

  @override
  Future<LoginSession> login(String username, String password) async {
    try {
      final json = await _request(
        'POST',
        '/api/auth/login',
        body: {'username': username, 'password': password},
      );
      return LoginSession.fromJson(json!);
    } on ApiException catch (error) {
      if (error.statusCode == HttpStatus.unauthorized) {
        throw const ApiException(
          'Invalid username or password.',
          statusCode: HttpStatus.unauthorized,
        );
      }
      rethrow;
    }
  }

  @override
  Future<CurrentUser> me(String token) async => CurrentUser.fromJson(
    (await _request('GET', '/api/auth/me', token: token))!,
  );

  @override
  Future<LoginSession> changePassword(
    String token,
    String currentPassword,
    String newPassword,
  ) async => LoginSession.fromJson(
    (await _request(
      'POST',
      '/api/auth/change-password',
      token: token,
      body: {'currentPassword': currentPassword, 'newPassword': newPassword},
    ))!,
  );

  @override
  Future<CurrentUser> updateProfile(
    String token, {
    required String fullName,
    String? email,
    String? phoneNumber,
  }) async => CurrentUser.fromJson(
    (await _request(
      'PUT',
      '/api/auth/me',
      token: token,
      body: {
        'fullName': fullName,
        'email': _nullIfEmpty(email),
        'phoneNumber': _nullIfEmpty(phoneNumber),
      },
    ))!,
  );

  @override
  Future<PagedUsers> listUsers(
    String token, {
    String? search,
    String? roleId,
    String? branchId,
    bool? isActive,
  }) async {
    final query = <String, String>{'page': '1', 'pageSize': '100'};
    if (search?.trim().isNotEmpty == true) query['search'] = search!.trim();
    if (roleId != null) query['roleId'] = roleId;
    if (branchId != null) query['branchId'] = branchId;
    if (isActive != null) query['isActive'] = isActive.toString();
    final uri = Uri(path: '/api/users', queryParameters: query).toString();
    return PagedUsers.fromJson((await _request('GET', uri, token: token))!);
  }

  @override
  Future<UserOptions> userOptions(String token) async => UserOptions.fromJson(
    (await _request('GET', '/api/users/options', token: token))!,
  );

  @override
  Future<UserDetails> userDetails(String token, String id) async =>
      UserDetails.fromJson(
        (await _request('GET', '/api/users/$id', token: token))!,
      );

  @override
  Future<UserDetails> createUser(
    String token,
    Map<String, dynamic> values,
  ) async => UserDetails.fromJson(
    (await _request('POST', '/api/users', token: token, body: values))!,
  );

  @override
  Future<UserDetails> updateUser(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => UserDetails.fromJson(
    (await _request('PUT', '/api/users/$id', token: token, body: values))!,
  );

  @override
  Future<void> setUserActive(String token, String id, bool active) async {
    await _request(
      'POST',
      '/api/users/$id/${active ? 'activate' : 'deactivate'}',
      token: token,
      expectBody: false,
    );
  }

  @override
  Future<void> resetPassword(String token, String id, String password) async {
    await _request(
      'POST',
      '/api/users/$id/reset-password',
      token: token,
      body: {'temporaryPassword': password},
      expectBody: false,
    );
  }

  @override
  Future<PagedProducts> listProducts(
    String token, {
    int page = 1,
    String? search,
    String? categoryId,
    String? manufacturerId,
    bool? isActive,
  }) async {
    final query = <String, String>{'page': '$page', 'pageSize': '25'};
    if (search?.trim().isNotEmpty == true) query['search'] = search!.trim();
    if (categoryId != null) query['categoryId'] = categoryId;
    if (manufacturerId != null) query['manufacturerId'] = manufacturerId;
    if (isActive != null) query['isActive'] = '$isActive';
    return PagedProducts.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/products', queryParameters: query).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<ProductOptions> productOptions(String token) async =>
      ProductOptions.fromJson(
        (await _request('GET', '/api/products/options', token: token))!,
      );
  @override
  Future<ProductDetails> productDetails(String token, String id) async =>
      ProductDetails.fromJson(
        (await _request('GET', '/api/products/$id', token: token))!,
      );
  @override
  Future<ProductDetails> createProduct(
    String token,
    Map<String, dynamic> values,
  ) async => ProductDetails.fromJson(
    (await _request('POST', '/api/products', token: token, body: values))!,
  );
  @override
  Future<ProductDetails> updateProduct(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => ProductDetails.fromJson(
    (await _request('PUT', '/api/products/$id', token: token, body: values))!,
  );
  @override
  Future<void> setProductActive(String token, String id, bool active) async =>
      _void(
        'POST',
        '/api/products/$id/${active ? 'activate' : 'deactivate'}',
        token,
      );
  @override
  Future<List<CategoryInfo>> listCategories(
    String token, {
    String? search,
    bool? isActive,
  }) async {
    final q = <String, String>{};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    if (isActive != null) q['isActive'] = '$isActive';
    final data = (await _request(
      'GET',
      Uri(path: '/api/product-categories', queryParameters: q).toString(),
      token: token,
    ))!;
    return (data['items'] as List<dynamic>? ?? const [])
        .map((x) => CategoryInfo.fromJson(x as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<CategoryInfo> saveCategory(
    String token,
    Map<String, dynamic> values, {
    String? id,
  }) async => CategoryInfo.fromJson(
    (await _request(
      id == null ? 'POST' : 'PUT',
      id == null ? '/api/product-categories' : '/api/product-categories/$id',
      token: token,
      body: values,
    ))!,
  );
  @override
  Future<void> setCategoryActive(String token, String id, bool active) async =>
      _void(
        'POST',
        '/api/product-categories/$id/${active ? 'activate' : 'deactivate'}',
        token,
      );
  @override
  Future<List<ManufacturerInfo>> listManufacturers(
    String token, {
    String? search,
    bool? isActive,
  }) async {
    final q = <String, String>{};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    if (isActive != null) q['isActive'] = '$isActive';
    final data = (await _request(
      'GET',
      Uri(path: '/api/manufacturers', queryParameters: q).toString(),
      token: token,
    ))!;
    return (data['items'] as List<dynamic>? ?? const [])
        .map((x) => ManufacturerInfo.fromJson(x as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<ManufacturerInfo> saveManufacturer(
    String token,
    Map<String, dynamic> values, {
    String? id,
  }) async => ManufacturerInfo.fromJson(
    (await _request(
      id == null ? 'POST' : 'PUT',
      id == null ? '/api/manufacturers' : '/api/manufacturers/$id',
      token: token,
      body: values,
    ))!,
  );
  @override
  Future<void> setManufacturerActive(
    String token,
    String id,
    bool active,
  ) async => _void(
    'POST',
    '/api/manufacturers/$id/${active ? 'activate' : 'deactivate'}',
    token,
  );

  @override
  Future<PagedInventory> listInventory(String token, {String? search}) async {
    final q = <String, String>{'page': '1', 'pageSize': '100'};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    return PagedInventory.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/inventory', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<InventoryOptions> inventoryOptions(
    String token, {
    String? productSearch,
  }) async {
    final q = <String, String>{};
    if (productSearch?.trim().isNotEmpty == true) {
      q['productSearch'] = productSearch!.trim();
    }
    return InventoryOptions.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/inventory/options', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<void> addOpeningStock(
    String token,
    Map<String, dynamic> values,
  ) async => _request(
    'POST',
    '/api/inventory/opening-stock',
    token: token,
    body: values,
  );

  @override
  Future<void> adjustStock(
    String token,
    Map<String, dynamic> values, {
    required bool increase,
  }) async => _request(
    'POST',
    '/api/inventory/adjust/${increase ? 'increase' : 'decrease'}',
    token: token,
    body: values,
  );

  @override
  Future<void> reconcileStockCount(
    String token,
    Map<String, dynamic> values,
  ) async => _request(
    'POST',
    '/api/inventory/stock-count',
    token: token,
    body: values,
  );

  @override
  Future<List<ExpiryItem>> listExpiry(String token, {int? days}) async {
    final q = <String, String>{};
    if (days != null) q['days'] = '$days';
    final data = await _request(
      'GET',
      Uri(path: '/api/inventory/expiry', queryParameters: q).toString(),
      token: token,
    );
    return (data as List<dynamic>? ?? [])
        .map((x) => ExpiryItem.fromJson(x as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<PagedBatches> listBatches(String token, {String? search}) async {
    final q = <String, String>{'page': '1', 'pageSize': '100'};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    return PagedBatches.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/batches', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<PagedMovements> listMovements(String token, {String? search}) async {
    final q = <String, String>{'page': '1', 'pageSize': '100'};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    return PagedMovements.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/stock-movements', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<PagedStockCountSessions> listStockCountSessions(
    String token, {
    String? branchId,
    String? status,
  }) async {
    final q = <String, String>{'page': '1', 'pageSize': '50'};
    if (branchId != null) q['branchId'] = branchId;
    if (status != null) q['status'] = status;
    return PagedStockCountSessions.fromJson(
      (await _request(
        'GET',
        Uri(
          path: '/api/inventory/stock-count/sessions',
          queryParameters: q,
        ).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<StockCountSession> getStockCountSession(
    String token,
    String id,
  ) async => StockCountSession.fromJson(
    (await _request(
      'GET',
      '/api/inventory/stock-count/sessions/$id',
      token: token,
    ))!,
  );

  @override
  Future<StockCountSession> createStockCountSession(
    String token,
    Map<String, dynamic> values,
  ) async => StockCountSession.fromJson(
    (await _request(
      'POST',
      '/api/inventory/stock-count/sessions',
      token: token,
      body: values,
    ))!,
  );

  @override
  Future<StockCountSession> startStockCountSession(
    String token,
    String id,
  ) async => StockCountSession.fromJson(
    (await _request(
      'POST',
      '/api/inventory/stock-count/sessions/$id/start',
      token: token,
    ))!,
  );

  @override
  Future<StockCountSession> submitStockCountEntries(
    String token,
    String id,
    List<Map<String, dynamic>> entries,
  ) async => StockCountSession.fromJson(
    (await _request(
      'POST',
      '/api/inventory/stock-count/sessions/$id/entries',
      token: token,
      body: {'entries': entries},
    ))!,
  );

  @override
  Future<StockCountSession> finalizeStockCountSession(
    String token,
    String id,
  ) async => StockCountSession.fromJson(
    (await _request(
      'POST',
      '/api/inventory/stock-count/sessions/$id/finalize',
      token: token,
    ))!,
  );

  @override
  Future<StockCountSession> cancelStockCountSession(
    String token,
    String id,
    String reason,
  ) async => StockCountSession.fromJson(
    (await _request(
      'POST',
      '/api/inventory/stock-count/sessions/$id/cancel',
      token: token,
      body: {'reason': reason},
    ))!,
  );

  @override
  Future<CashierShift?> myOpenCashierShift(String token) async {
    final data = await _request(
      'GET',
      '/api/cashier-shifts/my-open',
      token: token,
    );
    return data == null ? null : CashierShift.fromJson(data as Map<String, dynamic>);
  }

  @override
  Future<CashierShift> openCashierShift(
    String token,
    Map<String, dynamic> values,
  ) async => CashierShift.fromJson(
    (await _request(
      'POST',
      '/api/cashier-shifts',
      token: token,
      body: values,
    ))!,
  );

  @override
  Future<CashierShift> addCashierShiftDrawerEntry(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => CashierShift.fromJson(
    (await _request(
      'POST',
      '/api/cashier-shifts/$id/drawer-entries',
      token: token,
      body: values,
    ))!,
  );

  @override
  Future<CashierShift> closeCashierShift(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => CashierShift.fromJson(
    (await _request(
      'POST',
      '/api/cashier-shifts/$id/close',
      token: token,
      body: values,
    ))!,
  );

  @override
  Future<CashierShift> reconcileCashierShift(
    String token,
    String id,
    String? notes,
  ) async => CashierShift.fromJson(
    (await _request(
      'POST',
      '/api/cashier-shifts/$id/reconcile',
      token: token,
      body: {'reconciliationNotes': notes},
    ))!,
  );

  @override
  Future<CashierShift> cashierShiftDetails(String token, String id) async =>
      CashierShift.fromJson(
        (await _request('GET', '/api/cashier-shifts/$id', token: token))!,
      );

  @override
  Future<PagedCashierShifts> listCashierShifts(
    String token, {
    String? status,
  }) async {
    final q = <String, String>{'page': '1', 'pageSize': '50'};
    if (status != null) q['status'] = status;
    return PagedCashierShifts.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/cashier-shifts', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<DailyClosingSummary> dailyCashierClosingSummary(
    String token,
    String branchId,
    DateTime date,
  ) async {
    final q = {
      'branchId': branchId,
      'date': date.toIso8601String().substring(0, 10),
    };
    return DailyClosingSummary.fromJson(
      (await _request(
        'GET',
        Uri(
          path: '/api/cashier-shifts/daily-summary',
          queryParameters: q,
        ).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<PagedGodowns> listGodowns(
    String token, {
    String? branchId,
    String? search,
    bool? isActive,
  }) async {
    final q = <String, String>{'page': '1', 'pageSize': '100'};
    if (branchId != null) q['branchId'] = branchId;
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    if (isActive != null) q['isActive'] = '$isActive';
    return PagedGodowns.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/godowns', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<List<GodownLookup>> lookupGodowns(
    String token, {
    String? branchId,
    bool activeOnly = true,
  }) async {
    final q = <String, String>{'activeOnly': '$activeOnly'};
    if (branchId != null) q['branchId'] = branchId;
    final data = await _request(
      'GET',
      Uri(path: '/api/godowns/lookup', queryParameters: q).toString(),
      token: token,
    );
    return (data as List<dynamic>? ?? [])
        .map((x) => GodownLookup.fromJson(x as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<List<GodownLookup>> myGodowns(
    String token, {
    String? branchId,
  }) async {
    final q = <String, String>{};
    if (branchId != null) q['branchId'] = branchId;
    final data = await _request(
      'GET',
      Uri(path: '/api/godowns/mine', queryParameters: q).toString(),
      token: token,
    );
    return (data as List<dynamic>? ?? [])
        .map((x) => GodownLookup.fromJson(x as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<GodownListItem> createGodown(
    String token,
    Map<String, dynamic> values,
  ) async => GodownListItem.fromJson(
    (await _request('POST', '/api/godowns', token: token, body: values))!,
  );

  @override
  Future<GodownListItem> updateGodown(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => GodownListItem.fromJson(
    (await _request('PUT', '/api/godowns/$id', token: token, body: values))!,
  );

  @override
  Future<void> setGodownActive(String token, String id, bool active) async =>
      _void(
        'POST',
        '/api/godowns/$id/${active ? 'activate' : 'deactivate'}',
        token,
      );

  @override
  Future<GodownListItem> setGodownDefault(String token, String id) async =>
      GodownListItem.fromJson(
        (await _request(
          'POST',
          '/api/godowns/$id/set-default',
          token: token,
        ))!,
      );

  @override
  Future<List<UserGodownAssignment>> listGodownUsers(
    String token,
    String id,
  ) async {
    final data = await _request(
      'GET',
      '/api/godowns/$id/users',
      token: token,
    );
    return (data as List<dynamic>? ?? [])
        .map((x) => UserGodownAssignment.fromJson(x as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<void> assignUserGodown(
    String token,
    String godownId,
    Map<String, dynamic> values,
  ) async =>
      _request('POST', '/api/godowns/$godownId/users', token: token, body: values);

  @override
  Future<void> unassignUserGodown(
    String token,
    String godownId,
    String userId,
  ) async => _void('DELETE', '/api/godowns/$godownId/users/$userId', token);

  @override
  Future<void> setUserDefaultGodown(
    String token,
    String godownId,
    String userId,
  ) async =>
      _void('POST', '/api/godowns/$godownId/users/$userId/default', token);

  @override
  Future<PagedSuppliers> listSuppliers(
    String token, {
    String? search,
    bool? isActive,
  }) async {
    final q = <String, String>{'page': '1', 'pageSize': '100'};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    if (isActive != null) q['isActive'] = '$isActive';
    return PagedSuppliers.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/suppliers', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<SupplierListItem> createSupplier(
    String token,
    Map<String, dynamic> values,
  ) async => SupplierListItem.fromJson(
    (await _request('POST', '/api/suppliers', token: token, body: values))!,
  );

  @override
  Future<SupplierListItem> updateSupplier(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => SupplierListItem.fromJson(
    (await _request('PUT', '/api/suppliers/$id', token: token, body: values))!,
  );

  @override
  Future<void> setSupplierActive(String token, String id, bool active) async =>
      _void(
        'POST',
        '/api/suppliers/$id/${active ? 'activate' : 'deactivate'}',
        token,
      );

  @override
  Future<PagedSupplierLedger> supplierLedger(String token, String id, {DateTime? from, DateTime? to, String? branchId}) async {
    final query = <String, String>{};
    if (from != null) query['dateFrom'] = from.toIso8601String().substring(0, 10);
    if (to != null) query['dateTo'] = to.toIso8601String().substring(0, 10);
    if (branchId != null) query['branchId'] = branchId;
    return PagedSupplierLedger.fromJson((await _request('GET', Uri(path: '/api/suppliers/$id/ledger', queryParameters: query).toString(), token: token))!);
  }

  @override
  Future<void> recordSupplierPayment(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => _request(
    'POST',
    '/api/suppliers/$id/payments',
    token: token,
    body: values,
  );

  @override
  Future<void> adjustSupplierBalance(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => _request(
    'POST',
    '/api/suppliers/$id/adjustments',
    token: token,
    body: values,
  );

  @override
  Future<PagedCustomers> listCustomers(
    String token, {
    String? search,
    bool? isActive,
  }) async {
    final q = <String, String>{'page': '1', 'pageSize': '100'};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    if (isActive != null) q['isActive'] = '$isActive';
    return PagedCustomers.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/customers', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<List<CustomerLookup>> lookupCustomers(
    String token, {
    String? search,
  }) async {
    final q = <String, String>{'activeOnly': 'true'};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    final data = await _request(
      'GET',
      Uri(path: '/api/customers/lookup', queryParameters: q).toString(),
      token: token,
    );
    return (data as List<dynamic>? ?? [])
        .map((x) => CustomerLookup.fromJson(x as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<CustomerDetails> customerDetails(String token, String id) async =>
      CustomerDetails.fromJson(
        (await _request('GET', '/api/customers/$id', token: token))!,
      );

  @override
  Future<CustomerListItem> createCustomer(
    String token,
    Map<String, dynamic> values,
  ) async => CustomerListItem.fromJson(
    (await _request('POST', '/api/customers', token: token, body: values))!,
  );

  @override
  Future<CustomerListItem> updateCustomer(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => CustomerListItem.fromJson(
    (await _request('PUT', '/api/customers/$id', token: token, body: values))!,
  );

  @override
  Future<void> setCustomerActive(String token, String id, bool active) async =>
      _void(
        'POST',
        '/api/customers/$id/${active ? 'activate' : 'deactivate'}',
        token,
      );

  @override
  Future<PagedCustomerLedger> customerLedger(String token, String id, {DateTime? from, DateTime? to, String? branchId}) async {
    final query = <String, String>{};
    if (from != null) query['dateFrom'] = from.toIso8601String().substring(0, 10);
    if (to != null) query['dateTo'] = to.toIso8601String().substring(0, 10);
    if (branchId != null) query['branchId'] = branchId;
    return PagedCustomerLedger.fromJson((await _request('GET', Uri(path: '/api/customers/$id/ledger', queryParameters: query).toString(), token: token))!);
  }

  @override
  Future<void> recordCustomerPayment(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => _request(
    'POST',
    '/api/customers/$id/payments',
    token: token,
    body: values,
  );

  @override
  Future<void> adjustCustomerBalance(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => _request(
    'POST',
    '/api/customers/$id/adjustments',
    token: token,
    body: values,
  );

  @override
  Future<PagedPurchaseOrders> listPurchaseOrders(
    String token, {
    String? search,
  }) async {
    final q = <String, String>{'page': '1', 'pageSize': '100'};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    return PagedPurchaseOrders.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/purchase-orders', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<PurchaseOrderDetails> purchaseOrderDetails(
    String token,
    String id,
  ) async => PurchaseOrderDetails.fromJson(
    (await _request('GET', '/api/purchase-orders/$id', token: token))!,
  );

  @override
  Future<PurchaseOrderDetails> createPurchaseOrder(
    String token,
    Map<String, dynamic> values,
  ) async => PurchaseOrderDetails.fromJson(
    (await _request(
      'POST',
      '/api/purchase-orders',
      token: token,
      body: values,
    ))!,
  );

  @override
  Future<PurchaseOrderDetails> submitPurchaseOrder(
    String token,
    String id,
  ) async => PurchaseOrderDetails.fromJson(
    (await _request('POST', '/api/purchase-orders/$id/submit', token: token))!,
  );

  @override
  Future<PurchaseOrderDetails> cancelPurchaseOrder(
    String token,
    String id,
  ) async => PurchaseOrderDetails.fromJson(
    (await _request('POST', '/api/purchase-orders/$id/cancel', token: token))!,
  );

  @override
  Future<PagedPurchases> listPurchases(String token, {String? search}) async {
    final q = <String, String>{'page': '1', 'pageSize': '100'};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    return PagedPurchases.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/purchases', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<PurchaseDetails> purchaseDetails(String token, String id) async =>
      PurchaseDetails.fromJson(
        (await _request('GET', '/api/purchases/$id', token: token))!,
      );

  @override
  Future<PurchaseDetails> postGoodsReceipt(
    String token,
    Map<String, dynamic> values,
  ) async => PurchaseDetails.fromJson(
    (await _request(
      'POST',
      '/api/goods-receipts',
      token: token,
      body: values,
    ))!,
  );

  @override
  Future<PurchaseDetails> postDirectPurchase(
    String token,
    Map<String, dynamic> values,
  ) async => PurchaseDetails.fromJson(
    (await _request(
      'POST',
      '/api/purchases/direct',
      token: token,
      body: values,
    ))!,
  );

  @override
  Future<ReturnablePurchase> returnablePurchase(
    String token,
    String receiptId,
  ) async => ReturnablePurchase.fromJson(
    (await _request(
      'GET',
      '/api/goods-receipts/$receiptId/returnable',
      token: token,
    ))!,
  );

  @override
  Future<PurchaseReturnDetails> postPurchaseReturn(
    String token,
    String receiptId,
    Map<String, dynamic> values,
  ) async => PurchaseReturnDetails.fromJson(
    (await _request(
      'POST',
      '/api/goods-receipts/$receiptId/purchase-returns',
      token: token,
      body: values,
    ))!,
  );

  @override
  Future<PagedPurchaseReturns> listPurchaseReturns(
    String token, {
    String? search,
  }) async {
    final q = <String, String>{'page': '1', 'pageSize': '100'};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    return PagedPurchaseReturns.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/purchase-returns', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<PurchaseReturnDetails> purchaseReturnDetails(
    String token,
    String id,
  ) async => PurchaseReturnDetails.fromJson(
    (await _request('GET', '/api/purchase-returns/$id', token: token))!,
  );

  @override
  Future<PurchaseReturnDetails> purchaseReturnNote(
    String token,
    String id,
  ) async => PurchaseReturnDetails.fromJson(
    (await _request('GET', '/api/purchase-returns/$id/note', token: token))!,
  );

  @override
  Future<PurchaseReturnDetails> reprintPurchaseReturnNote(
    String token,
    String id,
  ) async => PurchaseReturnDetails.fromJson(
    (await _request(
      'POST',
      '/api/purchase-returns/$id/reprint-audit',
      token: token,
    ))!,
  );

  @override
  Future<List<PosProduct>> searchPosProducts(
    String token, {
    String? query,
    String? godownId,
  }) async {
    final q = <String, String>{'take': '25'};
    if (query?.trim().isNotEmpty == true) q['q'] = query!.trim();
    if (godownId != null) q['godownId'] = godownId;
    final data = await _request(
      'GET',
      Uri(path: '/api/pos/products/search', queryParameters: q).toString(),
      token: token,
    );
    return (data as List<dynamic>? ?? [])
        .map((x) => PosProduct.fromJson(x as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<SaleDetails> holdSale(
    String token,
    Map<String, dynamic> values,
  ) async => SaleDetails.fromJson(
    (await _request('POST', '/api/sales/hold', token: token, body: values))!,
  );

  @override
  Future<SaleDetails> postSale(
    String token,
    Map<String, dynamic> values,
  ) async => SaleDetails.fromJson(
    (await _request('POST', '/api/sales/post', token: token, body: values))!,
  );

  @override
  Future<SaleDetails> postHeldSale(
    String token,
    String id,
    Map<String, dynamic> values,
  ) async => SaleDetails.fromJson(
    (await _request(
      'POST',
      '/api/sales/held/$id/post',
      token: token,
      body: values,
    ))!,
  );

  @override
  Future<void> cancelHeldSale(String token, String id) async =>
      _void('POST', '/api/sales/held/$id/cancel', token);

  @override
  Future<PagedSales> listHeldSales(String token, {String? search}) async {
    final q = <String, String>{'page': '1', 'pageSize': '100'};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    return PagedSales.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/sales/held', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<PagedSales> listSales(String token, {String? search}) async {
    final q = <String, String>{'page': '1', 'pageSize': '100'};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    return PagedSales.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/sales', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<SaleDetails> saleDetails(String token, String id) async =>
      SaleDetails.fromJson(
        (await _request('GET', '/api/sales/$id', token: token))!,
      );

  @override
  Future<SaleDetails> saleReceipt(String token, String id) async =>
      SaleDetails.fromJson(
        (await _request('GET', '/api/sales/$id/receipt', token: token))!,
      );

  @override
  Future<SaleDetails> reprintSaleReceipt(String token, String id) async =>
      SaleDetails.fromJson(
        (await _request('POST', '/api/sales/$id/reprint', token: token))!,
      );
  @override
  Future<ReturnableSale> returnableSale(String token, String saleId) async =>
      ReturnableSale.fromJson(
        (await _request('GET', '/api/sales/$saleId/returnable', token: token))!,
      );

  @override
  Future<SalesReturnDetails> postSalesReturn(
    String token,
    String saleId,
    Map<String, dynamic> values,
  ) async => SalesReturnDetails.fromJson(
    (await _request(
      'POST',
      '/api/sales/$saleId/returns',
      token: token,
      body: values,
    ))!,
  );

  @override
  Future<PagedSalesReturns> listSalesReturns(
    String token, {
    String? search,
  }) async {
    final q = <String, String>{'page': '1', 'pageSize': '100'};
    if (search?.trim().isNotEmpty == true) q['search'] = search!.trim();
    return PagedSalesReturns.fromJson(
      (await _request(
        'GET',
        Uri(path: '/api/sales-returns', queryParameters: q).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<SalesReturnDetails> salesReturnDetails(
    String token,
    String id,
  ) async => SalesReturnDetails.fromJson(
    (await _request('GET', '/api/sales-returns/$id', token: token))!,
  );

  @override
  Future<SalesReturnDetails> salesReturnReceipt(
    String token,
    String id,
  ) async => SalesReturnDetails.fromJson(
    (await _request('GET', '/api/sales-returns/$id/receipt', token: token))!,
  );

  @override
  Future<SalesReturnDetails> reprintSalesReturnReceipt(
    String token,
    String id,
  ) async => SalesReturnDetails.fromJson(
    (await _request(
      'POST',
      '/api/sales-returns/$id/reprint-audit',
      token: token,
    ))!,
  );

  @override
  Future<List<FinancialAccountInfo>> listFinancialAccounts(
    String token, {
    String? branchId,
  }) async {
    final query = branchId == null ? '' : '?branchId=$branchId';
    final data = await _request(
      'GET',
      '/api/financial-accounts$query',
      token: token,
    );
    return (data as List<dynamic>? ?? [])
        .map((x) => FinancialAccountInfo.fromJson(x as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<FinancialAccountInfo> createFinancialAccount(
    String token,
    Map<String, dynamic> values,
  ) async => FinancialAccountInfo.fromJson(
    (await _request(
      'POST',
      '/api/financial-accounts',
      token: token,
      body: values,
    ))!,
  );

  @override
  Future<List<ExpenseCategoryInfo>> listExpenseCategories(String token) async {
    final data = await _request(
      'GET',
      '/api/expense-categories?active=true',
      token: token,
    );
    return (data as List<dynamic>? ?? [])
        .map((x) => ExpenseCategoryInfo.fromJson(x as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<List<ExpenseInfo>> listExpenses(String token) async {
    final data = await _request('GET', '/api/expenses', token: token);
    return (data as List<dynamic>? ?? [])
        .map((x) => ExpenseInfo.fromJson(x as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<ExpenseInfo> postExpense(
    String token,
    Map<String, dynamic> values,
  ) async => ExpenseInfo.fromJson(
    (await _request('POST', '/api/expenses', token: token, body: values))!,
  );
  @override
  Future<void> postOtherIncome(
    String token,
    Map<String, dynamic> values,
  ) async =>
      _request('POST', '/api/finance/other-income', token: token, body: values);
  @override
  Future<void> postFinancialTransfer(
    String token,
    Map<String, dynamic> values,
  ) async =>
      _request('POST', '/api/finance/transfers', token: token, body: values);
  @override
  Future<List<FinancialLedgerItem>> financialLedger(
    String token,
    String accountId,
  ) async {
    final data = await _request(
      'GET',
      '/api/financial-accounts/$accountId/ledger',
      token: token,
    );
    return (data as List<dynamic>? ?? [])
        .map((x) => FinancialLedgerItem.fromJson(x as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<DailyCashPosition> dailyCashPosition(
    String token,
    String branchId,
    DateTime date, {
    String? accountId,
  }) async {
    final query = <String, String>{
      'branchId': branchId,
      'date': date.toIso8601String().substring(0, 10),
    };
    if (accountId != null) query['accountId'] = accountId;
    return DailyCashPosition.fromJson(
      (await _request(
        'GET',
        Uri(
          path: '/api/finance/cash-position',
          queryParameters: query,
        ).toString(),
        token: token,
      ))!,
    );
  }

  @override
  Future<dynamic> report(
    String token,
    String path, {
    required DateTime fromUtc,
    required DateTime toUtc,
    String? branchId,
    String? option,
  }) => _request(
    'GET',
    _reportUri(path, fromUtc, toUtc, branchId, option),
    token: token,
  );

  @override
  Future<dynamic> administration(
    String token,
    String path, {
    String method = 'GET',
    Map<String, dynamic>? body,
  }) => _request(method, '/api/admin/$path', token: token, body: body);

  @override
  Future<dynamic> accounting(
    String token,
    String path, {
    String method = 'GET',
    Map<String, String>? query,
    Map<String, dynamic>? body,
  }) => _request(
    method,
    Uri(path: '/api/accounts/$path', queryParameters: query).toString(),
    token: token,
    body: body,
  );

  @override
  Future<List<int>> exportReport(
    String token,
    String path, {
    required DateTime fromUtc,
    required DateTime toUtc,
    String? branchId,
    String? option,
  }) async {
    final request = await _httpClient.openUrl(
      'GET',
      baseUri.resolve(
        _reportUri('$path/export.csv', fromUtc, toUtc, branchId, option),
      ),
    );
    request.headers.set(HttpHeaders.authorizationHeader, 'Bearer $token');
    final response = await request.close();
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw const ApiException('The report export could not be completed.');
    }
    return response.fold<List<int>>(
      <int>[],
      (all, bytes) => all..addAll(bytes),
    );
  }

  static String _reportUri(
    String path,
    DateTime fromUtc,
    DateTime toUtc,
    String? branchId,
    String? option,
  ) {
    final query = <String, String>{
      'fromUtc': fromUtc.toUtc().toIso8601String(),
      'toUtc': toUtc.toUtc().toIso8601String(),
      'page': '1',
      'pageSize': '100',
    };
    if (branchId != null) query['branchId'] = branchId;
    if (option != null) query['option'] = option;
    return Uri(path: '/api/reports/$path', queryParameters: query).toString();
  }

  Future<void> _void(String method, String path, String token) async {
    await _request(method, path, token: token, expectBody: false);
  }

  Future<dynamic> _request(
    String method,
    String path, {
    String? token,
    Map<String, dynamic>? body,
    bool expectBody = true,
  }) async {
    try {
      final request = await _httpClient
          .openUrl(method, baseUri.resolve(path))
          .timeout(const Duration(seconds: 10));
      request.headers.contentType = ContentType.json;
      if (token != null) {
        request.headers.set(HttpHeaders.authorizationHeader, 'Bearer $token');
      }
      if (body != null) request.write(jsonEncode(body));
      final response = await request.close().timeout(
        const Duration(seconds: 10),
      );
      final responseBody = await utf8.decoder.bind(response).join();
      if (response.statusCode < 200 || response.statusCode >= 300) {
        if (response.statusCode == HttpStatus.unauthorized && token != null) {
          onUnauthorized?.call();
          throw const ApiException(
            'Your session expired. Sign in again.',
            statusCode: 401,
          );
        }
        var message = 'The request could not be completed.';
        try {
          final problem = jsonDecode(responseBody) as Map<String, dynamic>;
          message =
              problem['title'] as String? ??
              problem['message'] as String? ??
              message;
        } on FormatException {
          // Keep the safe generic message.
        }
        throw ApiException(message, statusCode: response.statusCode);
      }
      if (!expectBody || responseBody.isEmpty) return null;
      return jsonDecode(responseBody);
    } on ApiException {
      rethrow;
    } on TimeoutException {
      throw const ApiException('The server did not respond in time.');
    } on SocketException {
      throw const ApiException('Cannot connect to the pharmacy server.');
    } on FormatException {
      throw const ApiException('The server returned an invalid response.');
    }
  }

  static String? _nullIfEmpty(String? value) =>
      value?.trim().isEmpty == true ? null : value?.trim();

  @override
  void close() => _httpClient.close(force: true);
}
