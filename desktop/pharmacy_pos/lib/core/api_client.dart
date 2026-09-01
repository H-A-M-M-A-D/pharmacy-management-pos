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
  Future<void> _void(String method, String path, String token) async {
    await _request(method, path, token: token, expectBody: false);
  }

  Future<Map<String, dynamic>?> _request(
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
      return jsonDecode(responseBody) as Map<String, dynamic>;
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
