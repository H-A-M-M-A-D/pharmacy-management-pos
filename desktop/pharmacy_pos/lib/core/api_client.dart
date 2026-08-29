import 'dart:async';
import 'dart:convert';
import 'dart:io';

class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  @override
  String toString() => message;
}

class LoginSession {
  const LoginSession({
    required this.token,
    required this.username,
    required this.fullName,
  });

  final String token;
  final String username;
  final String fullName;

  factory LoginSession.fromJson(Map<String, dynamic> json) {
    final user = json['user'] as Map<String, dynamic>?;
    final token = json['token'] as String?;
    if (user == null || token == null || token.isEmpty) {
      throw const ApiException(
        'The server returned an invalid login response.',
      );
    }

    return LoginSession(
      token: token,
      username: user['username'] as String? ?? '',
      fullName: user['fullName'] as String? ?? '',
    );
  }
}

class ApiClient {
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

  Future<LoginSession> login(String username, String password) async {
    try {
      final request = await _httpClient
          .postUrl(baseUri.resolve('/api/auth/login'))
          .timeout(const Duration(seconds: 10));
      request.headers.contentType = ContentType.json;
      request.write(jsonEncode({'username': username, 'password': password}));

      final response = await request.close().timeout(
        const Duration(seconds: 10),
      );
      final body = await utf8.decoder.bind(response).join();
      if (response.statusCode != HttpStatus.ok) {
        throw ApiException(
          response.statusCode == HttpStatus.unauthorized
              ? 'Invalid username or password.'
              : 'Login failed. Please try again.',
          statusCode: response.statusCode,
        );
      }

      return LoginSession.fromJson(jsonDecode(body) as Map<String, dynamic>);
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

  void close() => _httpClient.close(force: true);
}
