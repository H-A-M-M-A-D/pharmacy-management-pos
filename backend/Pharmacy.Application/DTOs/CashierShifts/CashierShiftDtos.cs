using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.CashierShifts;

public sealed record OpenCashierShiftRequest(Guid BranchId, decimal OpeningCash, string? TerminalName, string? OpeningNotes);
public sealed record AddDrawerEntryRequest(CashierShiftDrawerEntryType EntryType, decimal Amount, string Reason);
public sealed record CloseCashierShiftRequest(decimal ActualCountedCash, string? ClosingNotes);
public sealed record ReconcileCashierShiftRequest(string? ReconciliationNotes);

public sealed record CashierShiftListQuery(
    int Page = 1, int PageSize = 25, Guid? BranchId = null, Guid? CashierUserId = null,
    CashierShiftStatus? Status = null, DateOnly? From = null, DateOnly? To = null);

public sealed record CashierShiftPaymentSummaryDto(SalePaymentMethod PaymentMethod, decimal SalesAmount, decimal RefundsAmount);
public sealed record CashierShiftDrawerEntryDto(Guid Id, CashierShiftDrawerEntryType EntryType, decimal Amount, string Reason, string CreatedBy, DateTime CreatedAtUtc);

public sealed record CashierShiftDto(
    Guid Id, Guid BranchId, string BranchName, Guid CashierUserId, string CashierName, string? TerminalName,
    decimal OpeningCash, DateTime OpenedAtUtc, string? OpeningNotes, CashierShiftStatus Status,
    DateTime? ClosedAtUtc, decimal? ExpectedCash, decimal? ActualCountedCash, decimal? CashVariance, string? ClosingNotes,
    string? ReconciledBy, DateTime? ReconciledAtUtc, string? ReconciliationNotes,
    decimal TotalSales, decimal TotalRefunds, decimal CashSales, decimal CashRefunds, decimal CustomerCashReceived,
    decimal CashPaidOut, decimal ManualCashIn, decimal ManualCashOut,
    IReadOnlyList<CashierShiftPaymentSummaryDto> PaymentBreakdown, IReadOnlyList<CashierShiftDrawerEntryDto> DrawerEntries);

public sealed record CashierShiftListItemDto(
    Guid Id, Guid BranchId, string BranchName, Guid CashierUserId, string CashierName, string? TerminalName,
    decimal OpeningCash, DateTime OpenedAtUtc, CashierShiftStatus Status, DateTime? ClosedAtUtc,
    decimal? ExpectedCash, decimal? ActualCountedCash, decimal? CashVariance);

public sealed record DailyClosingSummaryDto(
    Guid BranchId, string BranchName, DateOnly Date, int ShiftCount, int OpenShiftCount,
    decimal TotalOpeningCash, decimal TotalExpectedCash, decimal TotalActualCash, decimal TotalVariance,
    IReadOnlyList<CashierShiftPaymentSummaryDto> PaymentBreakdown);
