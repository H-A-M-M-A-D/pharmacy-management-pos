using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A bank/cash statement reconciliation session for one <see cref="FinancialAccount"/>. Matching
/// works directly against that account's existing <see cref="FinancialLedgerEntry"/> rows (see
/// <see cref="FinancialLedgerEntry.BankReconciliationId"/>/<see cref="FinancialLedgerEntry.ReconciledAtUtc"/>)
/// rather than a separate line-item/candidate table, so there is no second ledger to keep in sync.
/// </summary>
public class BankReconciliation : Entity
{
    public Guid FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }
    public DateOnly StatementStartDate { get; set; }
    public DateOnly StatementEndDate { get; set; }
    public decimal StatementOpeningBalance { get; set; }
    public decimal StatementClosingBalance { get; set; }
    public BankReconciliationStatus Status { get; set; } = BankReconciliationStatus.InProgress;
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? FinalizedByUserId { get; set; }
    public User? FinalizedByUser { get; set; }
    public DateTime? FinalizedAtUtc { get; set; }
    public decimal? BookBalanceAtFinalization { get; set; }
    public decimal? DifferenceAtFinalization { get; set; }
    public Guid? ReopenedByUserId { get; set; }
    public User? ReopenedByUser { get; set; }
    public DateTime? ReopenedAtUtc { get; set; }
    public string? ReopenReason { get; set; }
    public ICollection<FinancialLedgerEntry> MatchedEntries { get; set; } = [];
}

public enum BankReconciliationStatus
{
    InProgress = 1,
    Finalized = 2
}
