using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.BankReconciliations;

/// <summary>
/// Matches directly against a <see cref="FinancialAccount"/>'s existing <see cref="FinancialLedgerEntry"/>
/// rows (customer receipts, supplier payments, vouchers, transfers, expenses that were posted with this
/// account attached) rather than a separate candidate/line table, so there is nothing new to keep in
/// sync. A ledger entry can only ever belong to one reconciliation at a time; finalizing locks its
/// matched entries (see <c>PharmacyDbContext.ValidateFinanceDocuments</c>, which allows changing only
/// the reconciliation-tracking fields on an otherwise-immutable row) until an authorized reopen.
/// </summary>
public sealed class BankReconciliationService(IBankReconciliationRepository repository, TimeProvider timeProvider) : IBankReconciliationService
{
    /// <summary>Candidate lines are bounded per-call, most recent first; large historical backlogs are
    /// truncated rather than fully materialized (see <see cref="BankReconciliationDto.TotalCandidateCount"/>).</summary>
    private const int MaxCandidateLines = 500;

    public async Task<IReadOnlyList<BankReconciliationDto>> ListReconciliationsAsync(Guid actorId, Guid? financialAccountId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsReconciliationView, cancellationToken);
        var list = await repository.ListReconciliationsAsync(financialAccountId, cancellationToken, actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager ? null : actor.BranchId);
        var results = new List<BankReconciliationDto>();
        foreach (var r in list) results.Add(await MapSummaryAsync(r, cancellationToken));
        return results;
    }

    public async Task<BankReconciliationDto> GetReconciliationAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsReconciliationView, cancellationToken);
        var reconciliation = await Required(id, actor, cancellationToken);
        return await MapAsync(reconciliation, cancellationToken);
    }

    public async Task<BankReconciliationDto> StartReconciliationAsync(Guid actorId, StartBankReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsReconciliationManage, cancellationToken);
        var account = await repository.GetFinancialAccountAsync(request.FinancialAccountId, cancellationToken) ?? throw new RequestValidationException("Financial account was not found.");
        EnsureBranch(actor, account.BranchId);
        if (!account.IsActive) throw new RequestValidationException("Financial account is inactive.");
        if (request.StatementEndDate < request.StatementStartDate) throw new RequestValidationException("Statement end date cannot be before the start date.");
        if (await repository.GetOpenReconciliationForAccountAsync(request.FinancialAccountId, cancellationToken) is not null)
            throw new ResourceConflictException("This account already has a bank reconciliation in progress. Finalize or reopen it before starting another.");

        BankReconciliation? reconciliation = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            reconciliation = new BankReconciliation
            {
                FinancialAccountId = request.FinancialAccountId, StatementStartDate = request.StatementStartDate, StatementEndDate = request.StatementEndDate,
                StatementOpeningBalance = request.StatementOpeningBalance, StatementClosingBalance = request.StatementClosingBalance,
                Status = BankReconciliationStatus.InProgress, Notes = Clean(request.Notes), CreatedByUserId = actorId, CreatedAtUtc = UtcNow()
            };
            await repository.AddReconciliationAsync(reconciliation, ct);
            await Audit(actorId, "BankReconciliationStarted", reconciliation.Id, new { request.FinancialAccountId, request.StatementStartDate, request.StatementEndDate }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync(reconciliation!, cancellationToken);
    }

    public async Task<BankReconciliationDto> MatchLinesAsync(Guid actorId, Guid id, MatchReconciliationLinesRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsReconciliationManage, cancellationToken);
        var reconciliation = await Required(id, actor, cancellationToken);
        if (reconciliation.Status != BankReconciliationStatus.InProgress) throw new RequestValidationException("Only an in-progress reconciliation can have lines matched.");
        var entries = await repository.GetEntriesByIdsAsync(request.FinancialLedgerEntryIds, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var entry in entries)
            {
                if (entry.FinancialAccountId != reconciliation.FinancialAccountId) throw new RequestValidationException("One or more entries do not belong to this account.");
                if (entry.BankReconciliationId.HasValue && entry.BankReconciliationId != id) throw new RequestValidationException("One or more entries are already matched to a different reconciliation.");
                entry.BankReconciliationId = id;
                entry.ReconciledAtUtc = UtcNow();
            }
            await Audit(actorId, "BankReconciliationLinesMatched", id, new { Count = entries.Count }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync((await repository.GetReconciliationAsync(id, cancellationToken))!, cancellationToken);
    }

    public async Task<BankReconciliationDto> UnmatchLinesAsync(Guid actorId, Guid id, UnmatchReconciliationLinesRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsReconciliationManage, cancellationToken);
        var reconciliation = await Required(id, actor, cancellationToken);
        if (reconciliation.Status != BankReconciliationStatus.InProgress) throw new RequestValidationException("Only an in-progress reconciliation can have lines unmatched.");
        var entries = await repository.GetEntriesByIdsAsync(request.FinancialLedgerEntryIds, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var entry in entries.Where(e => e.BankReconciliationId == id))
            {
                entry.BankReconciliationId = null;
                entry.ReconciledAtUtc = null;
            }
            await Audit(actorId, "BankReconciliationLinesUnmatched", id, new { Count = entries.Count }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync((await repository.GetReconciliationAsync(id, cancellationToken))!, cancellationToken);
    }

    public async Task<BankReconciliationDto> FinalizeReconciliationAsync(Guid actorId, Guid id, FinalizeReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsReconciliationManage, cancellationToken);
        var reconciliation = await Required(id, actor, cancellationToken);
        if (reconciliation.Status != BankReconciliationStatus.InProgress) throw new RequestValidationException("Only an in-progress reconciliation can be finalized.");
        var bookBalance = await repository.GetBookBalanceAsync(reconciliation.FinancialAccountId, reconciliation.StatementEndDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc), cancellationToken);
        var difference = decimal.Round(reconciliation.StatementClosingBalance - bookBalance, 2);
        if (difference != 0 && !request.AcknowledgeDifference)
            throw new RequestValidationException($"Book balance ({bookBalance:0.00}) does not match the statement closing balance ({reconciliation.StatementClosingBalance:0.00}); difference {difference:0.00}. Resolve the difference, or finalize with AcknowledgeDifference to record it as an outstanding reconciling item.");

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            reconciliation.Status = BankReconciliationStatus.Finalized;
            reconciliation.FinalizedByUserId = actorId;
            reconciliation.FinalizedAtUtc = UtcNow();
            reconciliation.BookBalanceAtFinalization = bookBalance;
            reconciliation.DifferenceAtFinalization = difference;
            reconciliation.Notes = Clean(request.Notes) ?? reconciliation.Notes;
            reconciliation.UpdatedAt = UtcNow();
            await Audit(actorId, "BankReconciliationFinalized", id, new { bookBalance, difference }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync(reconciliation, cancellationToken);
    }

    public async Task<BankReconciliationDto> ReopenReconciliationAsync(Guid actorId, Guid id, ReopenReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsReconciliationManage, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("A reason is required to reopen a bank reconciliation.");
        var reconciliation = await Required(id, actor, cancellationToken);
        if (reconciliation.Status != BankReconciliationStatus.Finalized) throw new RequestValidationException("Only a finalized reconciliation can be reopened.");
        reconciliation.Status = BankReconciliationStatus.InProgress;
        reconciliation.BookBalanceAtFinalization = null;
        reconciliation.DifferenceAtFinalization = null;
        reconciliation.ReopenedByUserId = actorId;
        reconciliation.ReopenedAtUtc = UtcNow();
        reconciliation.ReopenReason = request.Reason.Trim();
        reconciliation.UpdatedAt = UtcNow();
        await Audit(actorId, "BankReconciliationReopened", id, new { request.Reason }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAsync(reconciliation, cancellationToken);
    }

    private async Task<BankReconciliation> Required(Guid id, User actor, CancellationToken ct)
    {
        var reconciliation = await repository.GetReconciliationAsync(id, ct) ?? throw new ResourceNotFoundException("Bank reconciliation was not found.");
        var account = await repository.GetFinancialAccountAsync(reconciliation.FinancialAccountId, ct) ?? throw new ResourceNotFoundException("Financial account was not found.");
        EnsureBranch(actor, account.BranchId);
        return reconciliation;
    }
    private static void EnsureBranch(User actor, Guid branchId)
    {
        if (actor.Role?.Name is not (RoleCatalog.Owner or RoleCatalog.Manager) && actor.BranchId != branchId)
            throw new ForbiddenOperationException("The current user cannot access this branch.");
    }
    private async Task<BankReconciliationDto> MapAsync(BankReconciliation r, CancellationToken ct)
    {
        var account = r.FinancialAccount ?? await repository.GetFinancialAccountAsync(r.FinancialAccountId, ct);
        var (candidates, totalCandidateCount) = await repository.GetReconciliationLineEntriesAsync(r.FinancialAccountId, r.StatementEndDate, r.Id, MaxCandidateLines, ct);
        var lines = candidates.Select(e => new BankReconciliationCandidateLineDto(e.Id, e.OccurredAtUtc, e.EntryType, e.Amount, e.ReferenceType, e.ReferenceNumber, e.Description, e.BankReconciliationId == r.Id)).ToList();
        var (matched, unmatched) = await repository.GetCandidateTotalsAsync(r.FinancialAccountId, r.StatementEndDate, r.Id, ct);
        var bookBalance = r.BookBalanceAtFinalization ?? await repository.GetBookBalanceAsync(r.FinancialAccountId, r.StatementEndDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc), ct);
        var difference = r.DifferenceAtFinalization ?? decimal.Round(r.StatementClosingBalance - bookBalance, 2);
        return new BankReconciliationDto(r.Id, r.FinancialAccountId, account?.Name ?? string.Empty, r.StatementStartDate, r.StatementEndDate,
            r.StatementOpeningBalance, r.StatementClosingBalance, r.Status, r.Notes, matched, unmatched, bookBalance, difference,
            r.CreatedByUser?.FullName ?? string.Empty, r.CreatedAtUtc, r.FinalizedByUser?.FullName, r.FinalizedAtUtc, lines, totalCandidateCount);
    }

    /// <summary>Lightweight mapping for list views: skips the candidate-line fetch entirely (the list
    /// screen never renders line-level detail, only headline totals), so listing many reconciliations
    /// for an account no longer re-scans that account's ledger once per row.</summary>
    private async Task<BankReconciliationDto> MapSummaryAsync(BankReconciliation r, CancellationToken ct)
    {
        var account = r.FinancialAccount ?? await repository.GetFinancialAccountAsync(r.FinancialAccountId, ct);
        var (matched, unmatched) = await repository.GetCandidateTotalsAsync(r.FinancialAccountId, r.StatementEndDate, r.Id, ct);
        var bookBalance = r.BookBalanceAtFinalization ?? await repository.GetBookBalanceAsync(r.FinancialAccountId, r.StatementEndDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc), ct);
        var difference = r.DifferenceAtFinalization ?? decimal.Round(r.StatementClosingBalance - bookBalance, 2);
        return new BankReconciliationDto(r.Id, r.FinancialAccountId, account?.Name ?? string.Empty, r.StatementStartDate, r.StatementEndDate,
            r.StatementOpeningBalance, r.StatementClosingBalance, r.Status, r.Notes, matched, unmatched, bookBalance, difference,
            r.CreatedByUser?.FullName ?? string.Empty, r.CreatedAtUtc, r.FinalizedByUser?.FullName, r.FinalizedAtUtc, [], 0);
    }

    private async Task<User> Require(Guid actorId, string permission, CancellationToken ct)
    {
        var actor = await repository.GetActorAsync(actorId, ct);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private async Task Audit(Guid userId, string action, Guid entityId, object values, CancellationToken ct) =>
        await repository.AddAuditAsync(new AuditLog { UserId = userId, Action = action, EntityType = "BankReconciliation", EntityId = entityId, NewValues = JsonSerializer.Serialize(values) }, ct);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
