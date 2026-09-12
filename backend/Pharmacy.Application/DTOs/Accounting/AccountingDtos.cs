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

public sealed record ManualJournalLineRequest(Guid ChartOfAccountId, decimal Debit, decimal Credit, Guid? CustomerId = null, Guid? SupplierId = null, string? Description = null, Guid? CostCenterId = null);
public sealed record PostManualJournalRequest(DateTime EntryDateUtc, Guid BranchId, string? Reference, string Description, IReadOnlyList<ManualJournalLineRequest> Lines);

public sealed record JournalEntryLineDto(
    Guid Id, Guid ChartOfAccountId, string AccountCode, string AccountName, decimal Debit, decimal Credit,
    Guid BranchId, string? CustomerName, string? SupplierName, string? Description, Guid? CostCenterId = null, string? CostCenterName = null);

public sealed record JournalEntryDto(
    Guid Id, string EntryNumber, DateTime EntryDateUtc, JournalSourceType SourceType, Guid? SourceId,
    string? Reference, string Description, Guid BranchId, string BranchName, string PostedBy, DateTime PostedAtUtc,
    JournalEntryStatus Status, decimal TotalDebit, decimal TotalCredit, IReadOnlyList<JournalEntryLineDto> Lines);

public sealed record JournalEntryListItemDto(
    Guid Id, string EntryNumber, DateTime EntryDateUtc, JournalSourceType SourceType, string? Reference,
    string Description, Guid BranchId, string BranchName, string PostedBy, decimal TotalDebit);

public sealed record JournalEntryListQuery(
    int Page = 1, int PageSize = 25, Guid? BranchId = null, JournalSourceType? SourceType = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null, Guid? ChartOfAccountId = null, string? Search = null, Guid? CostCenterId = null);

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
/// <summary>"Other Income"/"Other Expenses" are split out from Revenue/Operating Expenses using the
/// <see cref="AccountMappingKey.OtherIncomeDefault"/>/<see cref="AccountMappingKey.BadDebtExpense"/>
/// mappings (never a hard-coded account id) — everything else of AccountType Income/Expense stays in
/// Revenue/OperatingExpenses as before, so existing consumers reading only those fields see no change.</summary>
public sealed record ProfitAndLossDto(
    DateTime FromUtc, DateTime ToUtc, IReadOnlyList<FinancialStatementRowDto> Revenue,
    decimal NetRevenue, IReadOnlyList<FinancialStatementRowDto> CostOfGoodsSold, decimal TotalCostOfGoodsSold,
    decimal GrossProfit, IReadOnlyList<FinancialStatementRowDto> OperatingExpenses,
    decimal TotalOperatingExpenses, decimal NetProfit,
    IReadOnlyList<FinancialStatementRowDto> OtherIncome, decimal TotalOtherIncome,
    IReadOnlyList<FinancialStatementRowDto> OtherExpenses, decimal TotalOtherExpenses);
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

// ---- Accounting periods / fiscal year close ----

public sealed record AccountingPeriodRequest(int FiscalYear, int PeriodNumber, string Name, DateOnly StartDate, DateOnly EndDate, string? Notes);
public sealed record AccountingPeriodDto(
    Guid Id, int FiscalYear, int PeriodNumber, string Name, DateOnly StartDate, DateOnly EndDate, AccountingPeriodStatus Status,
    DateTime? ClosedAtUtc, string? ClosedBy, DateTime? ReopenedAtUtc, string? ReopenedBy, string? Notes);
public sealed record CloseAccountingPeriodRequest(string? Notes);
public sealed record ReopenAccountingPeriodRequest(string Reason);

public sealed record FiscalYearCloseDto(
    Guid Id, int FiscalYear, DateTime ClosedAtUtc, string ClosedBy, decimal TotalRevenue, decimal TotalCostOfGoodsSold,
    decimal TotalOperatingExpenses, decimal NetProfit, string? Notes, FiscalYearCloseStatus Status,
    DateTime? ReopenedAtUtc, string? ReopenedBy, string? ReopenReason);
public sealed record CloseFiscalYearRequest(int FiscalYear, string? Notes);
public sealed record ReopenFiscalYearRequest(string Reason);

// ---- Trial balance movement (opening / period / closing) ----

public sealed record TrialBalanceMovementRowDto(
    Guid ChartOfAccountId, string AccountCode, string AccountName, Guid? ParentAccountId, AccountType AccountType, NormalBalance NormalBalance,
    decimal OpeningDebit, decimal OpeningCredit, decimal PeriodDebit, decimal PeriodCredit, decimal ClosingDebit, decimal ClosingCredit);
public sealed record TrialBalanceMovementDto(
    DateTime FromUtc, DateTime AsOfUtc, IReadOnlyList<TrialBalanceMovementRowDto> Rows,
    decimal TotalOpeningDebit, decimal TotalOpeningCredit, decimal TotalPeriodDebit, decimal TotalPeriodCredit,
    decimal TotalClosingDebit, decimal TotalClosingCredit, bool IsBalanced);

// ---- Journal reversal ----

public sealed record ReverseJournalEntryRequest(string Reason, DateTime? ReversalDateUtc);

// ---- Cost centers ----

public sealed record CostCenterRequest(string Code, string Name, string? Description, bool IsActive);
public sealed record CostCenterDto(Guid Id, string Code, string Name, string? Description, bool IsActive);

// ---- Cash Book / Bank Book / Day Book ----

/// <summary>Cash Book/Bank Book is built directly from the GL Cash/Bank control account's
/// <see cref="JournalEntryLine"/> activity (per spec: "use journal activity, not a duplicate cash
/// ledger"), so it has no per-financial-account filter — <see cref="JournalEntryLine"/> carries a
/// branch dimension but not a financial-account one. For a single till/bank account's own subsidiary
/// detail, use the existing Financial Account Ledger view instead (Finance module); this report is the
/// authoritative, always-reconciles-with-Trial-Balance company (or branch) wide view.</summary>
public sealed record CashBankBookQuery(Guid? BranchId, DateTime? FromUtc, DateTime? ToUtc, int Page = 1, int PageSize = 50);
public sealed record CashBankBookLineDto(
    DateTime DateUtc, string Reference, string Description, decimal Receipt, decimal Payment, decimal RunningBalance,
    JournalSourceType SourceType, string PostedBy, Guid JournalEntryId, string EntryNumber);
public sealed record CashBankBookDto(
    DateTime? FromUtc, DateTime? ToUtc, decimal OpeningBalance, decimal TotalReceipts, decimal TotalPayments,
    decimal ClosingBalance, PagedResult<CashBankBookLineDto> Lines);

public sealed record DayBookQuery(Guid? BranchId, DateTime? FromUtc, DateTime? ToUtc, int Page = 1, int PageSize = 50);
public sealed record DayBookLineDto(
    DateTime DateUtc, string EntryNumber, JournalSourceType SourceType, string? Reference, string Description,
    decimal TotalDebit, decimal TotalCredit, string PostedBy, Guid BranchId, string BranchName, Guid JournalEntryId);
public sealed record DayBookDto(DateTime? FromUtc, DateTime? ToUtc, decimal TotalDebit, decimal TotalCredit, PagedResult<DayBookLineDto> Lines);

// ---- Cash flow statement ----

public sealed record CashFlowLineDto(string Label, decimal Amount);
public sealed record CashFlowStatementDto(
    DateTime FromUtc, DateTime ToUtc, decimal NetProfit,
    IReadOnlyList<CashFlowLineDto> OperatingAdjustments, decimal NetCashFromOperating,
    IReadOnlyList<CashFlowLineDto> InvestingActivities, decimal NetCashFromInvesting,
    IReadOnlyList<CashFlowLineDto> FinancingActivities, decimal NetCashFromFinancing,
    decimal NetChangeInCash, decimal OpeningCash, decimal ClosingCash);

// ---- Control-account reconciliations ----

public sealed record ControlReconciliationRowDto(Guid PartyId, string PartyName, decimal SubledgerBalance, decimal GlBalance, decimal Difference);
public sealed record ControlReconciliationDto(
    DateTime AsOfUtc, decimal TotalSubledgerBalance, decimal TotalGlBalance, decimal TotalDifference,
    IReadOnlyList<ControlReconciliationRowDto> Mismatches);

public sealed record InventoryReconciliationDto(DateTime AsOfUtc, Guid? BranchId, Guid? GodownId, decimal InventoryValuation, decimal GlBalance, decimal Difference);

/// <summary>Per-account operational balances are listed for visibility, but compared only in
/// aggregate against the GL — <see cref="Domain.Entities.JournalEntryLine"/> carries a branch
/// dimension but not a financial-account one, so a single Cash/Bank GL control account cannot be
/// split back out per till/bank account. See <see cref="CashBankBookQuery"/> for the same limitation.</summary>
public sealed record CashBankControlRowDto(Guid FinancialAccountId, string FinancialAccountName, FinancialAccountType AccountType, decimal OperationalBalance);
public sealed record CashBankControlReconciliationDto(
    DateTime AsOfUtc,
    decimal CashOperationalTotal, decimal GlCashBalance, decimal CashDifference,
    decimal BankOperationalTotal, decimal GlBankBalance, decimal BankDifference,
    IReadOnlyList<CashBankControlRowDto> Accounts);
