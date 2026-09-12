using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Accounting;

public sealed record StartBankReconciliationRequest(Guid FinancialAccountId, DateOnly StatementStartDate, DateOnly StatementEndDate, decimal StatementOpeningBalance, decimal StatementClosingBalance, string? Notes);

public sealed record BankReconciliationCandidateLineDto(
    Guid FinancialLedgerEntryId, DateTime OccurredAtUtc, FinancialLedgerEntryType EntryType, decimal Amount,
    string ReferenceType, string? ReferenceNumber, string Description, bool IsMatched);

/// <summary>The list endpoint returns this shape too, with <see cref="Lines"/> always empty and
/// <see cref="TotalCandidateCount"/> 0 — fetching every reconciliation's full candidate-line set just to
/// render a summary row was the unbounded N+1 query this type used to force; only the single-record
/// Get (and the mutating actions that return the record they just changed) populate <see cref="Lines"/>,
/// bounded to the most recent rows on/before the statement end date. When <see cref="TotalCandidateCount"/>
/// exceeds <see cref="Lines"/>.Count, older candidates exist but were not loaded.</summary>
public sealed record BankReconciliationDto(
    Guid Id, Guid FinancialAccountId, string FinancialAccountName, DateOnly StatementStartDate, DateOnly StatementEndDate,
    decimal StatementOpeningBalance, decimal StatementClosingBalance, BankReconciliationStatus Status, string? Notes,
    decimal MatchedTotal, decimal UnmatchedTotal, decimal BookBalance, decimal Difference,
    string CreatedBy, DateTime CreatedAtUtc, string? FinalizedBy, DateTime? FinalizedAtUtc,
    IReadOnlyList<BankReconciliationCandidateLineDto> Lines, int TotalCandidateCount);

public sealed record MatchReconciliationLinesRequest(IReadOnlyList<Guid> FinancialLedgerEntryIds);
public sealed record UnmatchReconciliationLinesRequest(IReadOnlyList<Guid> FinancialLedgerEntryIds);
/// <summary>Finalizing normally requires the book balance to exactly match the statement closing
/// balance. Set <see cref="AcknowledgeDifference"/> to finalize anyway with a nonzero difference —
/// this is the "explicit authorized adjustment workflow" the spec calls for, deliberately kept as an
/// explicit acknowledgement rather than a separate adjustment-journal feature, since the difference
/// stays visible on the finalized record for follow-up rather than being silently absorbed.</summary>
public sealed record FinalizeReconciliationRequest(string? Notes, bool AcknowledgeDifference);
public sealed record ReopenReconciliationRequest(string Reason);
