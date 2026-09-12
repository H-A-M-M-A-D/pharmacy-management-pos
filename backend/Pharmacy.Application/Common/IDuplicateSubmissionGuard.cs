namespace Pharmacy.Application.Common;

/// <summary>
/// Rejects an exact repeat of a money- or stock-moving request (a double-click or client retry)
/// submitted by the same actor within a short window, without blocking a genuinely new, later
/// transaction that happens to carry identical content (e.g. the same cart sold again an hour later).
/// Call once, near the top of the caller's own transaction, before any business mutation, so a
/// rejected duplicate leaves no partial state. <paramref name="fingerprintPayload"/> should include
/// every field that determines the transaction's business effect (branch/godown, lines, amounts,
/// payment breakdown) but not a client-generated nonce, so genuine retries of the same attempt collide.
/// </summary>
public interface IDuplicateSubmissionGuard
{
    Task GuardAsync(string operation, Guid actorId, object fingerprintPayload, CancellationToken cancellationToken = default);
}
