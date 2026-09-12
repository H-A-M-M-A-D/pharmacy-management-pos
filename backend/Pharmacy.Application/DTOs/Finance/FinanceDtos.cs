using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Finance;

public sealed record FinancialAccountRequest(Guid BranchId, string Name, FinancialAccountType AccountType,
    decimal OpeningBalance, string? Notes, bool IsActive = true);
public sealed record FinancialAccountUpdateRequest(string Name, FinancialAccountType AccountType, string? Notes);
public sealed record FinancialAccountDto(Guid Id, Guid BranchId, string BranchName, string Name,
    FinancialAccountType AccountType, decimal OpeningBalance, decimal CurrentBalance,
    bool IsActive, string? Notes, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record FinancialLedgerQuery(DateTime? DateFrom = null, DateTime? DateTo = null,
    FinancialLedgerEntryType? EntryType = null, int Page = 1, int PageSize = 50);
public sealed record FinancialLedgerEntryDto(Guid Id, DateTime OccurredAtUtc,
    FinancialLedgerEntryType EntryType, string ReferenceType, Guid ReferenceId,
    string? ReferenceNumber, string Description, decimal Amount, decimal RunningBalance,
    string CreatedByName);

public sealed record ExpenseCategoryRequest(string Name, string? Description, bool IsActive = true);
public sealed record ExpenseCategoryDto(Guid Id, string Name, string? Description, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record PostExpenseRequest(Guid BranchId, Guid ExpenseCategoryId, Guid FinancialAccountId,
    DateTime ExpenseDateUtc, decimal Amount, string Description, string? Payee,
    string? ReferenceNumber, string? Notes, Guid? CostCenterId = null);
public sealed record ExpenseDto(Guid Id, string ExpenseNumber, Guid BranchId, string BranchName,
    Guid ExpenseCategoryId, string CategoryName, Guid FinancialAccountId, string AccountName,
    DateTime ExpenseDateUtc, decimal Amount, string Description, string? Payee,
    string? ReferenceNumber, string? Notes, string CreatedByName, DateTime PostedAtUtc,
    DateTime? ReversedAtUtc = null, string? ReversedBy = null, string? ReversalReason = null,
    Guid? CostCenterId = null, string? CostCenterName = null);
public sealed record ExpenseQuery(Guid? BranchId = null, Guid? CategoryId = null,
    Guid? FinancialAccountId = null, DateTime? DateFrom = null, DateTime? DateTo = null,
    int Page = 1, int PageSize = 50);
public sealed record ReverseExpenseRequest(string Reason);

public sealed record PostOtherIncomeRequest(Guid BranchId, Guid FinancialAccountId,
    DateTime OccurredAtUtc, decimal Amount, string Description, string? ReferenceNumber, string? Notes, Guid? CostCenterId = null);
public sealed record OtherIncomeDto(Guid Id, string IncomeNumber, Guid BranchId,
    Guid FinancialAccountId, decimal Amount, string Description, DateTime OccurredAtUtc,
    DateTime? ReversedAtUtc = null, string? ReversedBy = null, string? ReversalReason = null,
    Guid? CostCenterId = null, string? CostCenterName = null);
public sealed record ReverseOtherIncomeRequest(string Reason);

public sealed record PostTransferRequest(Guid BranchId, Guid SourceAccountId,
    Guid DestinationAccountId, DateTime OccurredAtUtc, decimal Amount,
    string? ReferenceNumber, string? Notes);
public sealed record FinancialTransferDto(Guid Id, string TransferNumber, Guid BranchId,
    Guid SourceAccountId, Guid DestinationAccountId, decimal Amount, DateTime OccurredAtUtc);

public enum FinancialAdjustmentType { Debit, Credit }
public sealed record PostFinancialAdjustmentRequest(Guid BranchId, Guid FinancialAccountId,
    FinancialAdjustmentType Type, decimal Amount, DateTime OccurredAtUtc, string Reason);

public sealed record CashPositionBreakdownDto(FinancialLedgerEntryType EntryType, decimal MoneyIn, decimal MoneyOut);
public sealed record DailyCashPositionDto(Guid BranchId, DateOnly Date, Guid? FinancialAccountId,
    decimal OpeningBalance, decimal MoneyIn, decimal MoneyOut, decimal ClosingBalance,
    IReadOnlyList<CashPositionBreakdownDto> Breakdown);
