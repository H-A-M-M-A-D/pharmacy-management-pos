using Pharmacy.Application.DTOs.Accounting;

namespace Pharmacy.Application.Services.Accounting.Periods;

public interface IAccountingPeriodService
{
    Task<IReadOnlyList<AccountingPeriodDto>> ListPeriodsAsync(Guid actorId, int? fiscalYear, CancellationToken cancellationToken = default);
    Task<AccountingPeriodDto> CreatePeriodAsync(Guid actorId, AccountingPeriodRequest request, CancellationToken cancellationToken = default);
    Task<AccountingPeriodDto> SoftClosePeriodAsync(Guid actorId, Guid id, CloseAccountingPeriodRequest request, CancellationToken cancellationToken = default);
    Task<AccountingPeriodDto> ClosePeriodAsync(Guid actorId, Guid id, CloseAccountingPeriodRequest request, CancellationToken cancellationToken = default);
    Task<AccountingPeriodDto> ReopenPeriodAsync(Guid actorId, Guid id, ReopenAccountingPeriodRequest request, CancellationToken cancellationToken = default);

    Task<FiscalYearCloseDto?> GetFiscalYearCloseAsync(Guid actorId, int fiscalYear, CancellationToken cancellationToken = default);
    Task<FiscalYearCloseDto> CloseFiscalYearAsync(Guid actorId, CloseFiscalYearRequest request, CancellationToken cancellationToken = default);
    Task<FiscalYearCloseDto> ReopenFiscalYearAsync(Guid actorId, int fiscalYear, ReopenFiscalYearRequest request, CancellationToken cancellationToken = default);
}
