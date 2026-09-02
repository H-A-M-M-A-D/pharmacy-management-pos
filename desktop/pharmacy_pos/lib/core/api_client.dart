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
  Future<PagedSupplierLedger> supplierLedger(String token, String id);
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
  Future<List<PosProduct>> searchPosProducts(String token, {String? query});
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
  void close();
}

class ApiClient implements PharmacyApi {
  ApiClient({Uri? baseUri, HttpClient? httpClient})
    : baseUri =
          baseUri ??
          Uri.parse(
            const String.fromEnvironment(
              'API_BASE_URL',
              defaultValue: 'http://localhost:5000',
            ),
          ),
      _httpClient = httpClient ?? HttpClient();

  final Uri baseUri;
  final HttpClient _httpClient;

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
  Future<PagedSupplierLedger> supplierLedger(String token, String id) async =>
      PagedSupplierLedger.fromJson(
        (await _request('GET', '/api/suppliers/$id/ledger', token: token))!,
      );

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
  Future<List<PosProduct>> searchPosProducts(
    String token, {
    String? query,
  }) async {
    final q = <String, String>{'take': '25'};
    if (query?.trim().isNotEmpty == true) q['q'] = query!.trim();
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
