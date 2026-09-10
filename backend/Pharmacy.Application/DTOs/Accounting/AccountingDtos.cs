using Pharmacy.Domain.Entities;
using Pharmacy.Application.DTOs.Users;

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
    DateTime? FromUtc = null, DateTime? ToUtc = null, Guid? ChartOfAccountId = null, string? Search = null);

public sealed record TrialBalanceRowDto(Guid ChartOfAccountId, string AccountCode, string AccountName, AccountType AccountType, NormalBalance NormalBalance, decimal Debit, decimal Credit);
public sealed record TrialBalanceDto(DateTime AsOfUtc, IReadOnlyList<TrialBalanceRowDto> Rows, decimal TotalDebit, decimal TotalCredit);

public sealed record GeneralLedgerQuery(
    Guid ChartOfAccountId, int Page = 1, int PageSize = 50, Guid? BranchId = null,
    JournalSourceType? SourceType = null, DateTime? FromUtc = null, DateTime? ToUtc = null);
public sealed record GeneralLedgerLineDto(
    Guid JournalEntryId, string EntryNumber, DateTime EntryDateUtc, JournalSourceType SourceType,
    string? Reference, string Description, decimal Debit, decimal Credit, decimal RunningBalance);
public sealed record GeneralLedgerDto(
    Guid ChartOfAccountId, string AccountCode, string AccountName, NormalBalance NormalBalance,
    decimal OpeningBalance, decimal TotalDebit, decimal TotalCredit, decimal ClosingBalance,
    PagedResult<GeneralLedgerLineDto> Entries);

public sealed record FinancialStatementRowDto(Guid ChartOfAccountId, string AccountCode, string AccountName, decimal Amount);
public sealed record ProfitAndLossDto(
    DateTime FromUtc, DateTime ToUtc, IReadOnlyList<FinancialStatementRowDto> Revenue,
    decimal NetRevenue, IReadOnlyList<FinancialStatementRowDto> CostOfGoodsSold, decimal TotalCostOfGoodsSold,
    decimal GrossProfit, IReadOnlyList<FinancialStatementRowDto> OperatingExpenses,
    decimal TotalOperatingExpenses, decimal NetProfit);
public sealed record BalanceSheetDto(
    DateTime AsOfUtc, IReadOnlyList<FinancialStatementRowDto> Assets, decimal TotalAssets,
    IReadOnlyList<FinancialStatementRowDto> Liabilities, decimal TotalLiabilities,
    IReadOnlyList<FinancialStatementRowDto> Equity, decimal AccountEquity,
    decimal CurrentPeriodEarnings, decimal TotalEquity, bool IsBalanced);

/// <summary>
/// Aging bucket relative to <see cref="AgingBucketExtensions.BucketFor"/>'s days-overdue calculation.
/// Documents with no stored due date (legacy rows predating due-date tracking, and opening-balance
/// rows which are always "due immediately") age from their own document date instead.
/// </summary>
public enum AgingBucket { Current = 0, Days1To30 = 1, Days31To60 = 2, Days61To90 = 3, Over90 = 4 }

public static class AgingBucketExtensions
{
    public static AgingBucket BucketFor(int daysOverdue) => daysOverdue switch
    {
        <= 0 => AgingBucket.Current,
        <= 30 => AgingBucket.Days1To30,
        <= 60 => AgingBucket.Days31To60,
        <= 90 => AgingBucket.Days61To90,
        _ => AgingBucket.Over90
    };
}

public sealed record ArAgingSummaryRowDto(
    Guid CustomerId, string CustomerCode, string CustomerName,
    decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal Over90, decimal Total);

public sealed record ArAgingSummaryDto(
    DateTime AsOfUtc, IReadOnlyList<ArAgingSummaryRowDto> Rows,
    decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal Over90, decimal Total);

public sealed record ArAgingDetailRowDto(
    Guid SaleId, string? InvoiceNumber, DateOnly DocumentDate, DateTime? DueDateUtc,
    decimal OriginalAmount, decimal SettledAmount, decimal Outstanding, int DaysOverdue, AgingBucket Bucket, bool IsOpeningBalance);

public sealed record ArAgingDetailDto(
    DateTime AsOfUtc, Guid CustomerId, string CustomerCode, string CustomerName,
    IReadOnlyList<ArAgingDetailRowDto> Rows, decimal Total);

public sealed record ApAgingSummaryRowDto(
    Guid SupplierId, string SupplierName,
    decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal Over90, decimal Total);

public sealed record ApAgingSummaryDto(
    DateTime AsOfUtc, IReadOnlyList<ApAgingSummaryRowDto> Rows,
    decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal Over90, decimal Total);

public sealed record ApAgingDetailRowDto(
    Guid GoodsReceiptId, string? GrnNumber, DateOnly DocumentDate, DateOnly? DueDate,
    decimal OriginalAmount, decimal SettledAmount, decimal Outstanding, int DaysOverdue, AgingBucket Bucket, bool IsOpeningBalance);

public sealed record ApAgingDetailDto(
    DateTime AsOfUtc, Guid SupplierId, string SupplierName,
    IReadOnlyList<ApAgingDetailRowDto> Rows, decimal Total);
