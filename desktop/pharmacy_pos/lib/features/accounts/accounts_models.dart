class AccountInfo {
  const AccountInfo({
    required this.id,
    required this.code,
    required this.name,
    required this.accountType,
    required this.normalBalance,
    required this.isPostingAccount,
    required this.isActive,
    this.parentAccountId,
    this.parentAccountName,
    this.description,
  });

  final String id, code, name, accountType, normalBalance;
  final String? parentAccountId, parentAccountName, description;
  final bool isPostingAccount, isActive;

  factory AccountInfo.fromJson(Map<String, dynamic> json) => AccountInfo(
    id: json['id'] as String? ?? '',
    code: json['code'] as String? ?? '',
    name: json['name'] as String? ?? '',
    parentAccountId: json['parentAccountId'] as String?,
    parentAccountName: json['parentAccountName'] as String?,
    accountType: enumName(json['accountType']),
    normalBalance: enumName(json['normalBalance']),
    isPostingAccount: json['isPostingAccount'] as bool? ?? false,
    isActive: json['isActive'] as bool? ?? false,
    description: json['description'] as String?,
  );
}

class AccountMappingInfo {
  const AccountMappingInfo({required this.key, required this.accountId, required this.code, required this.name});
  final String key, accountId, code, name;
  factory AccountMappingInfo.fromJson(Map<String, dynamic> json) => AccountMappingInfo(
    key: enumName(json['mappingKey']),
    accountId: json['chartOfAccountId'] as String? ?? '',
    code: json['chartOfAccountCode'] as String? ?? '',
    name: json['chartOfAccountName'] as String? ?? '',
  );
}

class JournalListItem {
  const JournalListItem({required this.id, required this.number, required this.date, required this.sourceType,
    required this.description, required this.branchName, required this.totalDebit, this.reference});
  final String id, number, sourceType, description, branchName;
  final String? reference;
  final DateTime date;
  final double totalDebit;
  factory JournalListItem.fromJson(Map<String, dynamic> json) => JournalListItem(
    id: json['id'] as String? ?? '', number: json['entryNumber'] as String? ?? '',
    date: DateTime.parse(json['entryDateUtc'] as String).toLocal(), sourceType: enumName(json['sourceType']),
    reference: json['reference'] as String?, description: json['description'] as String? ?? '',
    branchName: json['branchName'] as String? ?? '', totalDebit: amount(json['totalDebit']),
  );
}

class JournalLineInfo {
  const JournalLineInfo({required this.accountId, required this.code, required this.name, required this.debit,
    required this.credit, this.description, this.customerName, this.supplierName});
  final String accountId, code, name;
  final double debit, credit;
  final String? description, customerName, supplierName;
  factory JournalLineInfo.fromJson(Map<String, dynamic> json) => JournalLineInfo(
    accountId: json['chartOfAccountId'] as String? ?? '', code: json['accountCode'] as String? ?? '',
    name: json['accountName'] as String? ?? '', debit: amount(json['debit']), credit: amount(json['credit']),
    description: json['description'] as String?, customerName: json['customerName'] as String?,
    supplierName: json['supplierName'] as String?,
  );
}

class JournalDetails {
  const JournalDetails({required this.id, required this.number, required this.date, required this.sourceType,
    required this.description, required this.branchName, required this.postedBy, required this.status,
    required this.totalDebit, required this.totalCredit, required this.lines, this.reference});
  final String id, number, sourceType, description, branchName, postedBy, status;
  final String? reference;
  final DateTime date;
  final double totalDebit, totalCredit;
  final List<JournalLineInfo> lines;
  factory JournalDetails.fromJson(Map<String, dynamic> json) => JournalDetails(
    id: json['id'] as String? ?? '', number: json['entryNumber'] as String? ?? '',
    date: DateTime.parse(json['entryDateUtc'] as String).toLocal(), sourceType: enumName(json['sourceType']),
    reference: json['reference'] as String?, description: json['description'] as String? ?? '',
    branchName: json['branchName'] as String? ?? '', postedBy: json['postedBy'] as String? ?? '',
    status: enumName(json['status']), totalDebit: amount(json['totalDebit']), totalCredit: amount(json['totalCredit']),
    lines: (json['lines'] as List<dynamic>? ?? []).map((x) => JournalLineInfo.fromJson(x as Map<String, dynamic>)).toList(),
  );
}

class TrialBalanceRow {
  const TrialBalanceRow({required this.accountId, required this.code, required this.name, required this.accountType,
    required this.debit, required this.credit});
  final String accountId, code, name, accountType;
  final double debit, credit;
  factory TrialBalanceRow.fromJson(Map<String, dynamic> json) => TrialBalanceRow(
    accountId: json['chartOfAccountId'] as String? ?? '', code: json['accountCode'] as String? ?? '',
    name: json['accountName'] as String? ?? '', accountType: enumName(json['accountType']),
    debit: amount(json['debit']), credit: amount(json['credit']),
  );
}

class StatementRow {
  const StatementRow({required this.code, required this.name, required this.amount});
  final String code, name;
  final double amount;
  factory StatementRow.fromJson(Map<String, dynamic> json) => StatementRow(
    code: json['accountCode'] as String? ?? '', name: json['accountName'] as String? ?? '', amount: amount(json['amount']),
  );
}

String enumName(dynamic value) => value?.toString() ?? '';
double amount(dynamic value) => (value as num?)?.toDouble() ?? 0;
