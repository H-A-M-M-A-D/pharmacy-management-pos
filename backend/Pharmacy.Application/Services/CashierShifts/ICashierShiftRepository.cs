using System.Data;
using Pharmacy.Application.DTOs.CashierShifts;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.CashierShifts;

public sealed record ShiftCashFigures(
    IReadOnlyDictionary<SalePaymentMethod, (decimal Sales, decimal Refunds)> PaymentBreakdown,
    decimal CustomerCashReceived,
    decimal CashPaidOut);

public interface ICashierShiftRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<CashierShift?> GetOpenShiftForCashierAsync(Guid cashierUserId, CancellationToken cancellationToken = default);
    Task<CashierShift?> GetShiftAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddShiftAsync(CashierShift shift, CancellationToken cancellationToken = default);
    Task AddDrawerEntryAsync(CashierShiftDrawerEntry entry, CancellationToken cancellationToken = default);
    Task AddPaymentSummaryAsync(CashierShiftPaymentSummary summary, CancellationToken cancellationToken = default);
    Task<ShiftCashFigures> ComputeCashFiguresAsync(Guid branchId, Guid cashierUserId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<PagedResult<CashierShiftListItemDto>> ListShiftsAsync(CashierShiftListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashierShift>> ListShiftsForDateAsync(Guid branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<int> CountOpenShiftsAsync(Guid branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
