using System.Data;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.Periods;

public interface IAccountingPeriodRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountingPeriod>> ListPeriodsAsync(int? fiscalYear, CancellationToken cancellationToken = default);
    Task<AccountingPeriod?> GetPeriodAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountingPeriod>> GetOverlappingPeriodsAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task AddPeriodAsync(AccountingPeriod period, CancellationToken cancellationToken = default);

    Task<FiscalYearClose?> GetFiscalYearCloseAsync(int fiscalYear, CancellationToken cancellationToken = default);
    Task AddFiscalYearCloseAsync(FiscalYearClose close, CancellationToken cancellationToken = default);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
