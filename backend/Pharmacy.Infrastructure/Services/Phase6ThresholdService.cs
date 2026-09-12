using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Phase6;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Services;

public sealed partial class Phase6Service
{
    private async Task<Phase6Thresholds> ThresholdsCore(CancellationToken ct) =>
        await db.SystemSettings.AsNoTracking().Where(x => x.Key == "phase6.thresholds").Select(x => x.Value).SingleOrDefaultAsync(ct) is { } json
            ? JsonSerializer.Deserialize<Phase6Thresholds>(json)! : new();
    public async Task<Phase6Thresholds> GetThresholdsAsync(Guid actorId, CancellationToken ct = default)
    { await Require(actorId, PermissionCatalog.AlertsView, ct); return await ThresholdsCore(ct); }
    public async Task<Phase6Thresholds> SaveThresholdsAsync(Guid actorId, Phase6Thresholds request, CancellationToken ct = default)
    {
        await Require(actorId, PermissionCatalog.SystemSettingsManage, ct);
        if (request.ExpiryWarningDays is < 1 or > 730 || request.SlowMovingDays < 1 || request.DeadStockDays <= request.SlowMovingDays || request.DeadStockDays > 3650
            || request.MinimumMarginPercent is < 0 or >= 100 || request.DefaultReorderCoverDays is < 1 or > 365 || request.RoundingIncrement is <= 0 or > 1000)
            throw new RequestValidationException("Phase 6 thresholds are invalid; dead-stock days must exceed slow-moving days.");
        var row = await db.SystemSettings.SingleOrDefaultAsync(x => x.Key == "phase6.thresholds", ct);
        var old = row?.Value;
        if (row == null) { row = new SystemSetting { Key = "phase6.thresholds", Value = "", UpdatedByUserId = actorId }; db.SystemSettings.Add(row); }
        row.Value = JsonSerializer.Serialize(request); row.Version++; row.UpdatedByUserId = actorId;
        db.AuditLogs.Add(new AuditLog { UserId = actorId, Action = "Phase6ThresholdsUpdated", EntityType = "SystemSetting", EntityId = row.Id, OldValues = old, NewValues = row.Value });
        await db.SaveChangesAsync(ct); return request;
    }
    public async Task<IReadOnlyList<SlowStockDto>> ListSlowStockAsync(Guid actorId, decimal minimumStockValue, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AlertsView, ct);
        if (minimumStockValue < 0) throw new RequestValidationException("Minimum stock value cannot be negative.");
        var settings = await ThresholdsCore(ct);
        var q = new ReportQuery(actor.BranchId, DateTime.UnixEpoch, clock.GetUtcNow().UtcDateTime,
            SlowMovingDays: settings.SlowMovingDays, DeadStockDays: settings.DeadStockDays)
            { GodownUserId = actor.Role!.Name is RoleCatalog.Owner or RoleCatalog.Manager ? null : actor.Id };
        return await new ReportingRepository(db).SlowStockAsync(q, minimumStockValue, ct);
    }
}
