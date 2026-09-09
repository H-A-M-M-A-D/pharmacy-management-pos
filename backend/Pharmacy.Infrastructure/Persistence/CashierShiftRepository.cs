using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.CashierShifts;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.CashierShifts;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class CashierShiftRepository(PharmacyDbContext context) : ICashierShiftRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);

    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);

    public Task<CashierShift?> GetOpenShiftForCashierAsync(Guid cashierUserId, CancellationToken cancellationToken = default) =>
        ShiftQuery().FirstOrDefaultAsync(x => x.CashierUserId == cashierUserId && x.Status == CashierShiftStatus.Open, cancellationToken);

    public Task<CashierShift?> GetShiftAsync(Guid id, CancellationToken cancellationToken = default) =>
        ShiftQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddShiftAsync(CashierShift shift, CancellationToken cancellationToken = default) => await context.CashierShifts.AddAsync(shift, cancellationToken);
    public async Task AddDrawerEntryAsync(CashierShiftDrawerEntry entry, CancellationToken cancellationToken = default) => await context.CashierShiftDrawerEntries.AddAsync(entry, cancellationToken);
    public async Task AddPaymentSummaryAsync(CashierShiftPaymentSummary summary, CancellationToken cancellationToken = default) => await context.CashierShiftPaymentSummaries.AddAsync(summary, cancellationToken);

    public async Task<ShiftCashFigures> ComputeCashFiguresAsync(Guid branchId, Guid cashierUserId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var salesByMethod = await context.SalePayments.AsNoTracking()
            .Where(p => p.Sale!.CashierUserId == cashierUserId && p.Sale.BranchId == branchId && p.Sale.Status == SaleStatus.Posted
                        && p.Sale.PostedAtUtc >= fromUtc && p.Sale.PostedAtUtc < toUtc)
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Total = g.Sum(x => x.AmountApplied) })
            .ToListAsync(cancellationToken);

        var refundsByMethod = await context.SalesRefundPayments.AsNoTracking()
            .Where(p => p.SalesReturn!.ProcessedByUserId == cashierUserId && p.SalesReturn.BranchId == branchId
                        && p.SalesReturn.ReturnDateUtc >= fromUtc && p.SalesReturn.ReturnDateUtc < toUtc)
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var breakdown = new Dictionary<SalePaymentMethod, (decimal Sales, decimal Refunds)>();
        foreach (var row in salesByMethod)
            breakdown[row.Method] = (row.Total, breakdown.TryGetValue(row.Method, out var existing) ? existing.Refunds : 0);
        foreach (var row in refundsByMethod)
            breakdown[row.Method] = (breakdown.TryGetValue(row.Method, out var existing) ? existing.Sales : 0, row.Total);

        var customerCash = await context.CustomerPayments.AsNoTracking()
            .Where(p => p.ReceivedByUserId == cashierUserId && p.BranchId == branchId && p.PaymentMethod == CustomerPaymentMethod.Cash
                        && p.PaymentDateUtc >= fromUtc && p.PaymentDateUtc < toUtc)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0;

        var cashPaidOut = await context.Expenses.AsNoTracking()
            .Where(e => e.CreatedByUserId == cashierUserId && e.BranchId == branchId && e.FinancialAccount!.AccountType == FinancialAccountType.Cash
                        && e.PostedAtUtc >= fromUtc && e.PostedAtUtc < toUtc)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0;

        return new ShiftCashFigures(breakdown, customerCash, cashPaidOut);
    }

    public async Task<PagedResult<CashierShiftListItemDto>> ListShiftsAsync(CashierShiftListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var shifts = context.CashierShifts.AsNoTracking().Include(x => x.Branch).Include(x => x.CashierUser).AsQueryable();
        if (!canSelectBranch && actorBranchId.HasValue) shifts = shifts.Where(x => x.BranchId == actorBranchId);
        if (query.BranchId.HasValue) shifts = shifts.Where(x => x.BranchId == query.BranchId);
        if (query.CashierUserId.HasValue) shifts = shifts.Where(x => x.CashierUserId == query.CashierUserId);
        if (query.Status.HasValue) shifts = shifts.Where(x => x.Status == query.Status);
        if (query.From.HasValue) shifts = shifts.Where(x => x.OpenedAtUtc >= query.From.Value.ToDateTime(TimeOnly.MinValue));
        if (query.To.HasValue) shifts = shifts.Where(x => x.OpenedAtUtc < query.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var total = await shifts.CountAsync(cancellationToken);
        var items = await shifts.OrderByDescending(x => x.OpenedAtUtc).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CashierShiftListItemDto(x.Id, x.BranchId, x.Branch!.Name, x.CashierUserId, x.CashierUser!.FullName, x.TerminalName,
                x.OpeningCash, x.OpenedAtUtc, x.Status, x.ClosedAtUtc, x.ExpectedCash, x.ActualCountedCash, x.CashVariance))
            .ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<CashierShift>> ListShiftsForDateAsync(Guid branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
        await ShiftQuery().Where(x => x.BranchId == branchId && x.ClosedAtUtc >= fromUtc && x.ClosedAtUtc < toUtc
            && (x.Status == CashierShiftStatus.Closed || x.Status == CashierShiftStatus.Reconciled)).ToListAsync(cancellationToken);

    public Task<int> CountOpenShiftsAsync(Guid branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default) =>
        context.CashierShifts.AsNoTracking().CountAsync(x => x.BranchId == branchId && x.Status == CashierShiftStatus.Open
            && x.OpenedAtUtc >= fromUtc && x.OpenedAtUtc < toUtc, cancellationToken);

    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Cashier shift constraints were violated.");
        }
    }

    private IQueryable<CashierShift> ShiftQuery() =>
        context.CashierShifts.Include(x => x.Branch).Include(x => x.CashierUser).Include(x => x.ReconciledByUser)
            .Include(x => x.DrawerEntries).ThenInclude(x => x.CreatedByUser)
            .Include(x => x.PaymentSummaries);
}
