using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.Periods;

/// <summary>
/// Accounting periods gate financial posting centrally at the <c>PharmacyDbContext</c> SaveChanges
/// level (see <c>ValidateAccountingPeriodLocks</c>) — this service only manages period/fiscal-year
/// lifecycle (create, soft-close, close, reopen) and never touches journal posting logic directly.
/// Fiscal year close is a procedural lock plus an audit-trail snapshot of the year's final P&amp;L, not
/// a GL-posting "closing journal": this system's Balance Sheet already derives current-period
/// earnings dynamically from full Income/CostOfSales/Expense account history (see
/// AccountingService.GetBalanceSheetAsync), and posted journal entries are immutable, so a real
/// closing entry that zeroed P&amp;L accounts into Retained Earnings could never be un-posted on reopen
/// without double-counting earnings forever. Requiring every period in the year to already be
/// individually Closed is what actually blocks further ordinary posting.
/// </summary>
public sealed class AccountingPeriodService(IAccountingPeriodRepository repository, IAccountingService accountingService, TimeProvider timeProvider) : IAccountingPeriodService
{
    public async Task<IReadOnlyList<AccountingPeriodDto>> ListPeriodsAsync(Guid actorId, int? fiscalYear, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsPeriodsView, cancellationToken);
        var periods = await repository.ListPeriodsAsync(fiscalYear, cancellationToken);
        return periods.Select(Map).ToList();
    }

    public async Task<AccountingPeriodDto> CreatePeriodAsync(Guid actorId, AccountingPeriodRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsPeriodsManage, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100) throw new RequestValidationException("Period name is required and must be 100 characters or fewer.");
        if (request.EndDate < request.StartDate) throw new RequestValidationException("End date cannot be before start date.");
        if (request.FiscalYear < 2000 || request.FiscalYear > 2200) throw new RequestValidationException("Fiscal year is out of range.");
        if (request.PeriodNumber < 1 || request.PeriodNumber > 12) throw new RequestValidationException("Period number must be between 1 and 12.");

        AccountingPeriod? period = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var overlapping = await repository.GetOverlappingPeriodsAsync(request.StartDate, request.EndDate, ct);
            if (overlapping.Count > 0)
                throw new ResourceConflictException($"This period overlaps with existing period '{overlapping[0].Name}' ({overlapping[0].StartDate:yyyy-MM-dd} to {overlapping[0].EndDate:yyyy-MM-dd}).");
            period = new AccountingPeriod
            {
                FiscalYear = request.FiscalYear, PeriodNumber = request.PeriodNumber, Name = request.Name.Trim(),
                StartDate = request.StartDate, EndDate = request.EndDate, Status = AccountingPeriodStatus.Open, Notes = Clean(request.Notes)
            };
            await repository.AddPeriodAsync(period, ct);
            await Audit(actorId, "AccountingPeriodCreated", period.Id, new { period.FiscalYear, period.PeriodNumber, period.Name, period.StartDate, period.EndDate }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return Map(period!);
    }

    public async Task<AccountingPeriodDto> SoftClosePeriodAsync(Guid actorId, Guid id, CloseAccountingPeriodRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsPeriodsClose, cancellationToken);
        var period = await Required(id, cancellationToken);
        if (period.Status != AccountingPeriodStatus.Open) throw new RequestValidationException("Only an open period can be soft-closed.");
        period.Status = AccountingPeriodStatus.SoftClosed;
        period.Notes = Clean(request.Notes) ?? period.Notes;
        period.UpdatedAt = UtcNow();
        await Audit(actorId, "AccountingPeriodSoftClosed", period.Id, new { period.Name }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(period);
    }

    public async Task<AccountingPeriodDto> ClosePeriodAsync(Guid actorId, Guid id, CloseAccountingPeriodRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsPeriodsClose, cancellationToken);
        var period = await Required(id, cancellationToken);
        if (period.Status == AccountingPeriodStatus.Closed) throw new RequestValidationException("This period is already closed.");
        period.Status = AccountingPeriodStatus.Closed;
        period.ClosedAtUtc = UtcNow();
        period.ClosedByUserId = actorId;
        period.Notes = Clean(request.Notes) ?? period.Notes;
        period.UpdatedAt = UtcNow();
        await Audit(actorId, "AccountingPeriodClosed", period.Id, new { period.Name }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(period);
    }

    public async Task<AccountingPeriodDto> ReopenPeriodAsync(Guid actorId, Guid id, ReopenAccountingPeriodRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsPeriodsReopen, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("A reason is required to reopen an accounting period.");
        var period = await Required(id, cancellationToken);
        if (period.Status == AccountingPeriodStatus.Open) throw new RequestValidationException("This period is already open.");
        var fiscalYearClose = await repository.GetFiscalYearCloseAsync(period.FiscalYear, cancellationToken);
        if (fiscalYearClose is { Status: FiscalYearCloseStatus.Closed })
            throw new RequestValidationException("This period's fiscal year is closed. Reopen the fiscal year before reopening one of its periods.");
        period.Status = AccountingPeriodStatus.Open;
        period.ReopenedAtUtc = UtcNow();
        period.ReopenedByUserId = actorId;
        period.Notes = AppendNote(period.Notes, $"Reopened: {request.Reason.Trim()}");
        period.UpdatedAt = UtcNow();
        await Audit(actorId, "AccountingPeriodReopened", period.Id, new { period.Name, request.Reason }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(period);
    }

    public async Task<FiscalYearCloseDto?> GetFiscalYearCloseAsync(Guid actorId, int fiscalYear, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsPeriodsView, cancellationToken);
        var close = await repository.GetFiscalYearCloseAsync(fiscalYear, cancellationToken);
        return close is null ? null : Map(close);
    }

    public async Task<FiscalYearCloseDto> CloseFiscalYearAsync(Guid actorId, CloseFiscalYearRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsPeriodsClose, cancellationToken);
        if (await repository.GetFiscalYearCloseAsync(request.FiscalYear, cancellationToken) is { Status: FiscalYearCloseStatus.Closed })
            throw new RequestValidationException($"Fiscal year {request.FiscalYear} is already closed.");
        var periods = await repository.ListPeriodsAsync(request.FiscalYear, cancellationToken);
        if (periods.Count == 0) throw new RequestValidationException($"No accounting periods are defined for fiscal year {request.FiscalYear}. Create and close its periods first.");
        var notClosed = periods.Where(p => p.Status != AccountingPeriodStatus.Closed).ToList();
        if (notClosed.Count > 0)
            throw new RequestValidationException($"All periods in fiscal year {request.FiscalYear} must be closed first. Still open: {string.Join(", ", notClosed.Select(p => p.Name))}.");

        var yearStart = periods.Min(p => p.StartDate).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var yearEnd = periods.Max(p => p.EndDate).ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var pnl = await accountingService.GetProfitAndLossAsync(actorId, yearStart, yearEnd, null, cancellationToken);

        FiscalYearClose? close = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            close = new FiscalYearClose
            {
                FiscalYear = request.FiscalYear, ClosedAtUtc = UtcNow(), ClosedByUserId = actorId,
                TotalRevenue = pnl.NetRevenue + pnl.TotalOtherIncome, TotalCostOfGoodsSold = pnl.TotalCostOfGoodsSold,
                TotalOperatingExpenses = pnl.TotalOperatingExpenses + pnl.TotalOtherExpenses, NetProfit = pnl.NetProfit,
                Notes = Clean(request.Notes), Status = FiscalYearCloseStatus.Closed
            };
            await repository.AddFiscalYearCloseAsync(close, ct);
            await Audit(actorId, "FiscalYearClosed", close.Id, new { close.FiscalYear, close.NetProfit }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return Map(close!);
    }

    public async Task<FiscalYearCloseDto> ReopenFiscalYearAsync(Guid actorId, int fiscalYear, ReopenFiscalYearRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsPeriodsReopen, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("A reason is required to reopen a fiscal year.");
        var close = await repository.GetFiscalYearCloseAsync(fiscalYear, cancellationToken) ?? throw new ResourceNotFoundException("This fiscal year has not been closed.");
        if (close.Status != FiscalYearCloseStatus.Closed) throw new RequestValidationException("This fiscal year is not currently closed.");
        close.Status = FiscalYearCloseStatus.Reopened;
        close.ReopenedAtUtc = UtcNow();
        close.ReopenedByUserId = actorId;
        close.ReopenReason = request.Reason.Trim();
        close.UpdatedAt = UtcNow();
        await Audit(actorId, "FiscalYearReopened", close.Id, new { close.FiscalYear, request.Reason }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(close);
    }

    private async Task<AccountingPeriod> Required(Guid id, CancellationToken ct) =>
        await repository.GetPeriodAsync(id, ct) ?? throw new ResourceNotFoundException("Accounting period was not found.");

    private async Task<User> Require(Guid actorId, string permission, CancellationToken ct)
    {
        var actor = await repository.GetActorAsync(actorId, ct);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private static AccountingPeriodDto Map(AccountingPeriod p) => new(p.Id, p.FiscalYear, p.PeriodNumber, p.Name, p.StartDate, p.EndDate, p.Status,
        p.ClosedAtUtc, p.ClosedByUser?.FullName, p.ReopenedAtUtc, p.ReopenedByUser?.FullName, p.Notes);

    private static FiscalYearCloseDto Map(FiscalYearClose c) => new(c.Id, c.FiscalYear, c.ClosedAtUtc, c.ClosedByUser?.FullName ?? string.Empty,
        c.TotalRevenue, c.TotalCostOfGoodsSold, c.TotalOperatingExpenses, c.NetProfit, c.Notes, c.Status, c.ReopenedAtUtc, c.ReopenedByUser?.FullName, c.ReopenReason);

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private async Task Audit(Guid userId, string action, Guid entityId, object values, CancellationToken ct) =>
        await repository.AddAuditAsync(new AuditLog { UserId = userId, Action = action, EntityType = "AccountingPeriod", EntityId = entityId, NewValues = JsonSerializer.Serialize(values) }, ct);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string AppendNote(string? existing, string addition) => string.IsNullOrWhiteSpace(existing) ? addition : $"{existing}\n{addition}";
}
