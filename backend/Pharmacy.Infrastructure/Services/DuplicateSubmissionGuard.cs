using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Services;

public sealed class DuplicateSubmissionGuard(PharmacyDbContext db, TimeProvider clock) : IDuplicateSubmissionGuard
{
    // Long enough to catch a double-click or a client timeout-retry; short enough that a cashier
    // legitimately re-selling the exact same cart minutes later is never blocked.
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(20);

    public async Task GuardAsync(string operation, Guid actorId, object fingerprintPayload, CancellationToken cancellationToken = default)
    {
        var hash = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(operation + "|" + actorId + "|" + JsonSerializer.Serialize(fingerprintPayload))).AsSpan(0, 16));
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BitConverter.ToInt64(hash.ToByteArray(), 0)})", cancellationToken);

        var cutoff = clock.GetUtcNow().UtcDateTime - Window;
        var duplicate = await db.SubmissionFingerprints.AsNoTracking()
            .AnyAsync(x => x.Operation == operation && x.FingerprintHash == hash && x.CreatedAt >= cutoff, cancellationToken);
        if (duplicate)
            throw new ResourceConflictException("This exact request was already submitted moments ago. Check the result before retrying.");

        db.SubmissionFingerprints.Add(new SubmissionFingerprint
        {
            Operation = operation,
            FingerprintHash = hash,
            ActorId = actorId,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        });
    }
}
