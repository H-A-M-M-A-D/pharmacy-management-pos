# Pharmacy Management System Desktop

Flutter Windows client for authentication and user management. The default development API is `http://localhost:5273`, matching the backend's `http` launch profile. Override it with `--dart-define=API_BASE_URL=...` or the runtime `PHARMACY_API_URL` environment variable (which takes precedence). Login posts to `/api/auth/login`. Access tokens use `flutter_secure_storage`; no credentials are stored in source.

## Getting Started

This project is a starting point for a Flutter application.

A few resources to get you started if this is your first Flutter project:

- [Learn Flutter](https://docs.flutter.dev/get-started/learn-flutter)
- [Write your first Flutter app](https://docs.flutter.dev/get-started/codelab)
- [Flutter learning resources](https://docs.flutter.dev/reference/learning-resources)

For help getting started with Flutter development, view the
[online documentation](https://docs.flutter.dev/), which offers tutorials,
samples, guidance on mobile development, and a full API reference.
