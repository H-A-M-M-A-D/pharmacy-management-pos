using Pharmacy.Application.DTOs.CashierShifts;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.CashierShifts;

public interface ICashierShiftService
{
    Task<CashierShiftDto> OpenShiftAsync(Guid actorId, OpenCashierShiftRequest request, CancellationToken cancellationToken = default);
    Task<CashierShiftDto> AddDrawerEntryAsync(Guid actorId, Guid shiftId, AddDrawerEntryRequest request, CancellationToken cancellationToken = default);
    Task<CashierShiftDto> CloseShiftAsync(Guid actorId, Guid shiftId, CloseCashierShiftRequest request, CancellationToken cancellationToken = default);
    Task<CashierShiftDto> ReconcileShiftAsync(Guid actorId, Guid shiftId, ReconcileCashierShiftRequest request, CancellationToken cancellationToken = default);
    Task<CashierShiftDto> GetShiftAsync(Guid actorId, Guid shiftId, CancellationToken cancellationToken = default);
    Task<CashierShiftDto?> GetMyOpenShiftAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<PagedResult<CashierShiftListItemDto>> ListShiftsAsync(Guid actorId, CashierShiftListQuery query, CancellationToken cancellationToken = default);
    Task<DailyClosingSummaryDto> GetDailyClosingSummaryAsync(Guid actorId, Guid branchId, DateOnly date, CancellationToken cancellationToken = default);
}
