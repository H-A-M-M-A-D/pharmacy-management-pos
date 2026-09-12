using System.Data;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.BankReconciliations;

public interface IBankReconciliationRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<FinancialAccount?> GetFinancialAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task<decimal> GetBookBalanceAsync(Guid financialAccountId, DateTime asOfUtc, CancellationToken cancellationToken = default);
    Task<(decimal Matched, decimal Unmatched)> GetCandidateTotalsAsync(Guid financialAccountId, DateOnly statementEndDate, Guid currentReconciliationId, CancellationToken cancellationToken = default);

    Task<BankReconciliation?> GetOpenReconciliationForAccountAsync(Guid financialAccountId, CancellationToken cancellationToken = default);
    Task<BankReconciliation?> GetReconciliationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BankReconciliation>> ListReconciliationsAsync(Guid? financialAccountId, CancellationToken cancellationToken = default, Guid? branchId = null, int limit = 100);
    Task AddReconciliationAsync(BankReconciliation reconciliation, CancellationToken cancellationToken = default);

    /// <summary>Candidates for matching against this reconciliation's statement: entries on this
    /// financial account dated on or before the statement end date that are either still unmatched to
    /// any reconciliation, or already matched to this one (so they can be displayed/unmatched). Bounded
    /// to <paramref name="maxRows"/> (most recent first) so a long-lived account's full ledger history
    /// is never pulled into memory in one call; <c>TotalCount</c> tells the caller whether the result
    /// was truncated.</summary>
    Task<(IReadOnlyList<FinancialLedgerEntry> Lines, int TotalCount)> GetReconciliationLineEntriesAsync(
        Guid financialAccountId, DateOnly statementEndDate, Guid? currentReconciliationId, int maxRows, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialLedgerEntry>> GetEntriesByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
