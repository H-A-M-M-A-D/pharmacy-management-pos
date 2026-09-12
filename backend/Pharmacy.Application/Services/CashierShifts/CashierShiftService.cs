using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.CashierShifts;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.CashierShifts;

public sealed class CashierShiftService(ICashierShiftRepository repository, IJournalPostingService journalPosting, TimeProvider timeProvider) : ICashierShiftService
{
    public async Task<CashierShiftDto> OpenShiftAsync(Guid actorId, OpenCashierShiftRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.CashierShiftOpen, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        if (request.OpeningCash < 0 || decimal.Round(request.OpeningCash, 2) != request.OpeningCash)
            throw new RequestValidationException("Opening cash cannot be negative and supports at most two decimal places.");
        if (await repository.GetBranchAsync(request.BranchId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Branch is invalid or inactive.");
        if (await repository.GetOpenShiftForCashierAsync(actorId, cancellationToken) is not null)
            throw new ResourceConflictException("You already have an open cashier shift. Close it before opening a new one.");
        if (request.FinancialAccountId.HasValue)
        {
            var financialAccount = await repository.GetFinancialAccountAsync(request.FinancialAccountId.Value, cancellationToken);
            if (financialAccount is not { IsActive: true, AccountType: FinancialAccountType.Cash } || financialAccount.BranchId != request.BranchId)
                throw new RequestValidationException("Financial account must be an active Cash-type account in the selected branch.");
        }

        CashierShift? shift = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            shift = new CashierShift
            {
                BranchId = request.BranchId,
                CashierUserId = actorId,
                TerminalName = Clean(request.TerminalName),
                OpeningCash = request.OpeningCash,
                OpenedAtUtc = UtcNow(),
                OpeningNotes = Clean(request.OpeningNotes),
                Status = CashierShiftStatus.Open,
                FinancialAccountId = request.FinancialAccountId
            };
            await repository.AddShiftAsync(shift, ct);
            await Audit(actorId, "CashierShiftOpened", "CashierShift", shift.Id, new { shift.BranchId, shift.OpeningCash, shift.TerminalName }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await BuildDto(shift!, cancellationToken);
    }

    public async Task<CashierShiftDto> AddDrawerEntryAsync(Guid actorId, Guid shiftId, AddDrawerEntryRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.CashierShiftDrawerAdjust, cancellationToken);
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount)
            throw new RequestValidationException("Drawer amount must be greater than zero with at most two decimal places.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new RequestValidationException("A reason is required for manual drawer entries.");

        CashierShift? shift = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            shift = await RequiredShift(shiftId, ct);
            EnsureBranchAccess(actor, shift.BranchId);
            EnsureOwnShiftOrOverride(actor, shift, PermissionCatalog.CashierShiftCloseAny);
            if (shift.Status != CashierShiftStatus.Open)
                throw new RequestValidationException("Drawer entries can only be recorded while the shift is open.");
            var entry = new CashierShiftDrawerEntry
            {
                CashierShiftId = shift.Id,
                EntryType = request.EntryType,
                Amount = request.Amount,
                Reason = request.Reason.Trim(),
                CreatedByUserId = actorId
            };
            await repository.AddDrawerEntryAsync(entry, ct);
            await PostDrawerEntryJournalAsync(shift, entry, ct);
            await Audit(actorId, "CashierShiftDrawerEntryAdded", "CashierShift", shift.Id, new { entry.EntryType, entry.Amount, entry.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await BuildDto(shift!, cancellationToken);
    }

    public async Task<CashierShiftDto> CloseShiftAsync(Guid actorId, Guid shiftId, CloseCashierShiftRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await RequireAny(actorId, [PermissionCatalog.CashierShiftClose, PermissionCatalog.CashierShiftCloseAny], cancellationToken);
        if (request.ActualCountedCash < 0 || decimal.Round(request.ActualCountedCash, 2) != request.ActualCountedCash)
            throw new RequestValidationException("Actual counted cash cannot be negative and supports at most two decimal places.");

        CashierShift? shift = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            shift = await RequiredShift(shiftId, ct);
            EnsureBranchAccess(actor, shift.BranchId);
            EnsureOwnShiftOrOverride(actor, shift, PermissionCatalog.CashierShiftCloseAny);
            if (shift.Status != CashierShiftStatus.Open)
                throw new RequestValidationException("Only an open shift can be closed.");

            var closedAtUtc = UtcNow();
            var figures = await repository.ComputeCashFiguresAsync(shift.BranchId, shift.CashierUserId, shift.OpenedAtUtc, closedAtUtc, ct);
            var manualIn = shift.DrawerEntries.Where(x => x.EntryType == CashierShiftDrawerEntryType.CashIn).Sum(x => x.Amount);
            var manualOut = shift.DrawerEntries.Where(x => x.EntryType == CashierShiftDrawerEntryType.CashOut).Sum(x => x.Amount);
            var cashSales = figures.PaymentBreakdown.TryGetValue(SalePaymentMethod.Cash, out var cashRow) ? cashRow.Sales : 0;
            var cashRefunds = figures.PaymentBreakdown.TryGetValue(SalePaymentMethod.Cash, out var cashRowAgain) ? cashRowAgain.Refunds : 0;
            var expected = shift.OpeningCash + cashSales + figures.CustomerCashReceived + manualIn - cashRefunds - figures.CashPaidOut - manualOut;
            var variance = request.ActualCountedCash - expected;

            foreach (var (method, amounts) in figures.PaymentBreakdown)
            {
                var summary = new CashierShiftPaymentSummary { CashierShiftId = shift.Id, PaymentMethod = method, SalesAmount = amounts.Sales, RefundsAmount = amounts.Refunds };
                await repository.AddPaymentSummaryAsync(summary, ct);
            }

            shift.Status = CashierShiftStatus.Closed;
            shift.ClosedAtUtc = closedAtUtc;
            shift.ExpectedCash = expected;
            shift.ActualCountedCash = request.ActualCountedCash;
            shift.CashVariance = variance;
            shift.CustomerCashReceivedSnapshot = figures.CustomerCashReceived;
            shift.CashPaidOutSnapshot = figures.CashPaidOut;
            shift.ClosingNotes = Clean(request.ClosingNotes);

            await Audit(actorId, "CashierShiftClosed", "CashierShift", shift.Id,
                new { shift.ExpectedCash, shift.ActualCountedCash, shift.CashVariance }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await BuildDto(shift!, cancellationToken);
    }

    public async Task<CashierShiftDto> ReconcileShiftAsync(Guid actorId, Guid shiftId, ReconcileCashierShiftRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.CashierShiftReconcile, cancellationToken);
        CashierShift? shift = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            shift = await RequiredShift(shiftId, ct);
            EnsureBranchAccess(actor, shift.BranchId);
            if (shift.Status != CashierShiftStatus.Closed)
                throw new RequestValidationException("Only a closed shift can be reconciled.");
            shift.Status = CashierShiftStatus.Reconciled;
            shift.ReconciledByUserId = actorId;
            shift.ReconciledAtUtc = UtcNow();
            shift.ReconciliationNotes = Clean(request.ReconciliationNotes);
            await PostShiftVarianceJournalAsync(shift, actorId, ct);
            await Audit(actorId, "CashierShiftReconciled", "CashierShift", shift.Id, new { shift.CashVariance, request.ReconciliationNotes }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await BuildDto(shift!, cancellationToken);
    }

    public async Task<CashierShiftDto> GetShiftAsync(Guid actorId, Guid shiftId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.CashierShiftView, cancellationToken);
        var shift = await RequiredShift(shiftId, cancellationToken);
        EnsureBranchAccess(actor, shift.BranchId);
        return await BuildDto(shift, cancellationToken);
    }

    public async Task<CashierShiftDto?> GetMyOpenShiftAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        await RequireAny(actorId, [PermissionCatalog.CashierShiftOpen, PermissionCatalog.CashierShiftView], cancellationToken);
        var shift = await repository.GetOpenShiftForCashierAsync(actorId, cancellationToken);
        return shift is null ? null : await BuildDto(shift, cancellationToken);
    }

    public async Task<PagedResult<CashierShiftListItemDto>> ListShiftsAsync(Guid actorId, CashierShiftListQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.CashierShiftView, cancellationToken);
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
            throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
        var scope = Scope(actor, query.BranchId);
        return await repository.ListShiftsAsync(query with { BranchId = scope }, actor.BranchId, CanSelectBranch(actor), cancellationToken);
    }

    public async Task<DailyClosingSummaryDto> GetDailyClosingSummaryAsync(Guid actorId, Guid branchId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.CashierShiftView, cancellationToken);
        EnsureBranchAccess(actor, branchId);
        var branch = await repository.GetBranchAsync(branchId, cancellationToken) ?? throw new ResourceNotFoundException("Branch was not found.");
        var (fromUtc, toUtc) = BusinessDayBoundsUtc(date);
        var shifts = await repository.ListShiftsForDateAsync(branchId, fromUtc, toUtc, cancellationToken);
        var openCount = await repository.CountOpenShiftsAsync(branchId, fromUtc, toUtc, cancellationToken);

        var breakdown = shifts.SelectMany(x => x.PaymentSummaries)
            .GroupBy(x => x.PaymentMethod)
            .Select(g => new CashierShiftPaymentSummaryDto(g.Key, g.Sum(x => x.SalesAmount), g.Sum(x => x.RefundsAmount)))
            .OrderBy(x => x.PaymentMethod)
            .ToList();

        return new DailyClosingSummaryDto(branchId, branch.Name, date, shifts.Count, openCount,
            shifts.Sum(x => x.OpeningCash), shifts.Sum(x => x.ExpectedCash ?? 0), shifts.Sum(x => x.ActualCountedCash ?? 0),
            shifts.Sum(x => x.CashVariance ?? 0), breakdown);
    }

    private async Task<CashierShiftDto> BuildDto(CashierShift shift, CancellationToken ct)
    {
        var branch = shift.Branch ?? await repository.GetBranchAsync(shift.BranchId, ct);
        var cashier = shift.CashierUser ?? await repository.GetActorAsync(shift.CashierUserId, ct);
        User? reconciledBy = null;
        if (shift.ReconciledByUserId.HasValue)
            reconciledBy = shift.ReconciledByUser ?? await repository.GetActorAsync(shift.ReconciledByUserId.Value, ct);

        IReadOnlyList<CashierShiftPaymentSummaryDto> breakdown;
        decimal cashSales, cashRefunds, customerCash, cashPaidOut, manualIn, manualOut, expected;
        decimal? variance = shift.CashVariance;

        manualIn = shift.DrawerEntries.Where(x => x.EntryType == CashierShiftDrawerEntryType.CashIn).Sum(x => x.Amount);
        manualOut = shift.DrawerEntries.Where(x => x.EntryType == CashierShiftDrawerEntryType.CashOut).Sum(x => x.Amount);

        if (shift.Status == CashierShiftStatus.Open)
        {
            var figures = await repository.ComputeCashFiguresAsync(shift.BranchId, shift.CashierUserId, shift.OpenedAtUtc, UtcNow(), ct);
            breakdown = figures.PaymentBreakdown.Select(kv => new CashierShiftPaymentSummaryDto(kv.Key, kv.Value.Sales, kv.Value.Refunds)).OrderBy(x => x.PaymentMethod).ToList();
            cashSales = figures.PaymentBreakdown.TryGetValue(SalePaymentMethod.Cash, out var row) ? row.Sales : 0;
            cashRefunds = figures.PaymentBreakdown.TryGetValue(SalePaymentMethod.Cash, out var row2) ? row2.Refunds : 0;
            customerCash = figures.CustomerCashReceived;
            cashPaidOut = figures.CashPaidOut;
            expected = shift.OpeningCash + cashSales + customerCash + manualIn - cashRefunds - cashPaidOut - manualOut;
        }
        else
        {
            breakdown = shift.PaymentSummaries.Select(x => new CashierShiftPaymentSummaryDto(x.PaymentMethod, x.SalesAmount, x.RefundsAmount)).OrderBy(x => x.PaymentMethod).ToList();
            cashSales = shift.PaymentSummaries.FirstOrDefault(x => x.PaymentMethod == SalePaymentMethod.Cash)?.SalesAmount ?? 0;
            cashRefunds = shift.PaymentSummaries.FirstOrDefault(x => x.PaymentMethod == SalePaymentMethod.Cash)?.RefundsAmount ?? 0;
            customerCash = shift.CustomerCashReceivedSnapshot ?? 0;
            cashPaidOut = shift.CashPaidOutSnapshot ?? 0;
            expected = shift.ExpectedCash ?? 0;
        }

        FinancialAccount? financialAccount = null;
        if (shift.FinancialAccountId.HasValue)
            financialAccount = shift.FinancialAccount ?? await repository.GetFinancialAccountAsync(shift.FinancialAccountId.Value, ct);

        return new CashierShiftDto(
            shift.Id, shift.BranchId, branch?.Name ?? string.Empty, shift.CashierUserId, cashier?.FullName ?? string.Empty, shift.TerminalName,
            shift.OpeningCash, shift.OpenedAtUtc, shift.OpeningNotes, shift.Status,
            shift.ClosedAtUtc, shift.Status == CashierShiftStatus.Open ? expected : shift.ExpectedCash, shift.ActualCountedCash, variance, shift.ClosingNotes,
            reconciledBy?.FullName, shift.ReconciledAtUtc, shift.ReconciliationNotes,
            breakdown.Sum(x => x.SalesAmount), breakdown.Sum(x => x.RefundsAmount), cashSales, cashRefunds, customerCash,
            cashPaidOut, manualIn, manualOut, breakdown,
            shift.DrawerEntries.OrderBy(x => x.CreatedAt).Select(x => new CashierShiftDrawerEntryDto(x.Id, x.EntryType, x.Amount, x.Reason, x.CreatedByUser?.FullName ?? string.Empty, x.CreatedAt)).ToList(),
            shift.FinancialAccountId, financialAccount?.Name);
    }

    private async Task PostDrawerEntryJournalAsync(CashierShift shift, CashierShiftDrawerEntry entry, CancellationToken ct)
    {
        var lines = entry.EntryType == CashierShiftDrawerEntryType.CashIn
            ? new List<JournalLineInput>
            {
                new(AccountMappingKey.Cash, entry.Amount, 0),
                new(AccountMappingKey.DrawerClearing, 0, entry.Amount)
            }
            : new List<JournalLineInput>
            {
                new(AccountMappingKey.DrawerClearing, entry.Amount, 0),
                new(AccountMappingKey.Cash, 0, entry.Amount)
            };
        await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.CashierDrawerEntry, entry.Id, shift.BranchId, entry.CreatedAt,
            entry.EntryType.ToString(), entry.Reason, entry.CreatedByUserId, lines), ct);
        if (shift.FinancialAccountId.HasValue)
        {
            var signedAmount = entry.EntryType == CashierShiftDrawerEntryType.CashIn ? entry.Amount : -entry.Amount;
            await repository.AddFinancialLedgerEntryAsync(new FinancialLedgerEntry
            {
                FinancialAccountId = shift.FinancialAccountId.Value, BranchId = shift.BranchId,
                EntryType = signedAmount > 0 ? FinancialLedgerEntryType.AdjustmentCredit : FinancialLedgerEntryType.AdjustmentDebit, Amount = signedAmount,
                ReferenceType = "CashierShiftDrawerEntry", ReferenceId = entry.Id, Description = entry.Reason,
                CreatedByUserId = entry.CreatedByUserId, OccurredAtUtc = entry.CreatedAt
            }, ct);
        }
    }

    private async Task PostShiftVarianceJournalAsync(CashierShift shift, Guid actorId, CancellationToken ct)
    {
        var variance = shift.CashVariance ?? 0;
        if (variance == 0) return;
        var amount = Math.Abs(variance);
        var lines = variance > 0
            ? new List<JournalLineInput>
            {
                new(AccountMappingKey.Cash, amount, 0),
                new(AccountMappingKey.CashOverShort, 0, amount)
            }
            : new List<JournalLineInput>
            {
                new(AccountMappingKey.CashOverShort, amount, 0),
                new(AccountMappingKey.Cash, 0, amount)
            };
        await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.CashierShiftVariance, shift.Id, shift.BranchId,
            shift.ReconciledAtUtc!.Value, "Shift reconciliation", $"Cash variance for cashier shift {shift.Id}", actorId, lines), ct);
        if (shift.FinancialAccountId.HasValue)
        {
            await repository.AddFinancialLedgerEntryAsync(new FinancialLedgerEntry
            {
                FinancialAccountId = shift.FinancialAccountId.Value, BranchId = shift.BranchId,
                EntryType = variance > 0 ? FinancialLedgerEntryType.AdjustmentCredit : FinancialLedgerEntryType.AdjustmentDebit, Amount = variance,
                ReferenceType = "CashierShiftVariance", ReferenceId = shift.Id, Description = $"Cash variance for cashier shift {shift.Id}",
                CreatedByUserId = actorId, OccurredAtUtc = shift.ReconciledAtUtc!.Value
            }, ct);
        }
    }

    private async Task<CashierShift> RequiredShift(Guid id, CancellationToken ct) =>
        await repository.GetShiftAsync(id, ct) ?? throw new ResourceNotFoundException("Cashier shift was not found.");

    private static void EnsureOwnShiftOrOverride(User actor, CashierShift shift, string overridePermission)
    {
        if (actor.Id == shift.CashierUserId) return;
        if (actor.Role?.RolePermissions.Any(x => x.Permission?.Code == overridePermission) == true) return;
        throw new ForbiddenOperationException("Only the assigned cashier or a manager can perform this action on this shift.");
    }

    private async Task<User> Require(Guid actorId, string permission, CancellationToken ct)
    {
        var actor = await repository.GetActorAsync(actorId, ct);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private async Task<User> RequireAny(Guid actorId, string[] permissions, CancellationToken ct)
    {
        var actor = await repository.GetActorAsync(actorId, ct);
        if (actor is null || !actor.IsActive || !permissions.Any(p => actor.Role?.RolePermissions.Any(x => x.Permission?.Code == p) == true))
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private static Guid? Scope(User actor, Guid? requested)
    {
        if (CanSelectBranch(actor)) return requested;
        if (requested.HasValue && requested != actor.BranchId) throw new ForbiddenOperationException("The current user cannot access this branch.");
        return actor.BranchId;
    }

    private static void EnsureBranchAccess(User actor, Guid branchId)
    {
        if (!CanSelectBranch(actor) && actor.BranchId != branchId)
            throw new ForbiddenOperationException("The current user cannot access this branch.");
    }

    private static bool CanSelectBranch(User actor) => actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager;

    private (DateTime FromUtc, DateTime ToUtc) BusinessDayBoundsUtc(DateOnly date)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), timeZone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);
        return (fromUtc, toUtc);
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private async Task Audit(Guid userId, string action, string entityType, Guid entityId, object values, CancellationToken ct) =>
        await repository.AddAuditAsync(new AuditLog { UserId = userId, Action = action, EntityType = entityType, EntityId = entityId, NewValues = JsonSerializer.Serialize(values) }, ct);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
