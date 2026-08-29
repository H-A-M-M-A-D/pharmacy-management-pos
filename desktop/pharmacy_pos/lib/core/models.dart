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
