using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Accounting;

public sealed record ChartOfAccountRequest(
    string Code, string Name, Guid? ParentAccountId, AccountType AccountType,
    NormalBalance NormalBalance, bool IsPostingAccount, string? Description);

public sealed record ChartOfAccountUpdateRequest(string Name, string? Description, bool IsPostingAccount);
public sealed record SetAccountMappingRequest(AccountMappingKey MappingKey, Guid ChartOfAccountId);

public sealed record ChartOfAccountListItemDto(
    Guid Id, string Code, string Name, Guid? ParentAccountId, string? ParentAccountName,
    AccountType AccountType, NormalBalance NormalBalance, bool IsPostingAccount, bool IsActive, string? Description);

public sealed record ChartOfAccountDto(
    Guid Id, string Code, string Name, Guid? ParentAccountId, string? ParentAccountName,
    AccountType AccountType, NormalBalance NormalBalance, bool IsPostingAccount, bool IsActive,
    string? Description, decimal Balance);

public sealed record AccountMappingDto(AccountMappingKey MappingKey, Guid ChartOfAccountId, string ChartOfAccountCode, string ChartOfAccountName);

public sealed record ManualJournalLineRequest(Guid ChartOfAccountId, decimal Debit, decimal Credit, Guid? CustomerId = null, Guid? SupplierId = null, string? Description = null);
public sealed record PostManualJournalRequest(DateTime EntryDateUtc, Guid BranchId, string? Reference, string Description, IReadOnlyList<ManualJournalLineRequest> Lines);

public sealed record JournalEntryLineDto(
    Guid Id, Guid ChartOfAccountId, string AccountCode, string AccountName, decimal Debit, decimal Credit,
    Guid BranchId, string? CustomerName, string? SupplierName, string? Description);

public sealed record JournalEntryDto(
    Guid Id, string EntryNumber, DateTime EntryDateUtc, JournalSourceType SourceType, Guid? SourceId,
    string? Reference, string Description, Guid BranchId, string BranchName, string PostedBy, DateTime PostedAtUtc,
    JournalEntryStatus Status, decimal TotalDebit, decimal TotalCredit, IReadOnlyList<JournalEntryLineDto> Lines);

public sealed record JournalEntryListItemDto(
    Guid Id, string EntryNumber, DateTime EntryDateUtc, JournalSourceType SourceType, string? Reference,
    string Description, Guid BranchId, string BranchName, string PostedBy, decimal TotalDebit);

public sealed record JournalEntryListQuery(
    int Page = 1, int PageSize = 25, Guid? BranchId = null, JournalSourceType? SourceType = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null, Guid? ChartOfAccountId = null);

public sealed record TrialBalanceRowDto(Guid ChartOfAccountId, string AccountCode, string AccountName, AccountType AccountType, decimal Debit, decimal Credit);
public sealed record TrialBalanceDto(DateTime AsOfUtc, IReadOnlyList<TrialBalanceRowDto> Rows, decimal TotalDebit, decimal TotalCredit);
