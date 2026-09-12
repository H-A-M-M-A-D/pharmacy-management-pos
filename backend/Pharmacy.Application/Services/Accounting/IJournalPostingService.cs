using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting;

/// <summary>
/// A line to post expressed in terms of a stable semantic account role rather than a raw
/// <see cref="ChartOfAccount"/> id, so callers never hard-code account identifiers. Exactly one
/// of <see cref="Debit"/>/<see cref="Credit"/> should be non-zero.
/// </summary>
/// <summary><see cref="CostCenterId"/> is left null by every automatic Sales/Purchase/Inventory/etc.
/// posting call site — it exists only so hand-entered postings that go through this shared engine
/// (currently Expense/Other Income) can optionally tag their line with a cost center.</summary>
public sealed record JournalLineInput(AccountMappingKey Account, decimal Debit, decimal Credit, Guid? CustomerId = null, Guid? SupplierId = null, string? Description = null, Guid? CostCenterId = null);

public sealed record JournalPostingRequest(
    JournalSourceType SourceType, Guid SourceId, Guid BranchId, DateTime OccurredAtUtc,
    string? Reference, string Description, Guid PostedByUserId, IReadOnlyList<JournalLineInput> Lines);

/// <summary>
/// Internal cross-cutting capability other bounded-context services (Sales, Purchasing, Customers,
/// Suppliers, Finance) call to post their accounting entries. <see cref="PostAsync"/> stages the
/// journal entry into the repository's tracked change set but never calls SaveChanges or opens its
/// own transaction — it always runs inside the caller's existing transaction, so a sale and its
/// journal entry commit (or roll back) together.
/// </summary>
public interface IJournalPostingService
{
    Task PostAsync(JournalPostingRequest request, CancellationToken cancellationToken = default);
}
