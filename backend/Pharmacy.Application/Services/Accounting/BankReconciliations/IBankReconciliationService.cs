using Pharmacy.Application.DTOs.Accounting;

namespace Pharmacy.Application.Services.Accounting.BankReconciliations;

public interface IBankReconciliationService
{
    Task<IReadOnlyList<BankReconciliationDto>> ListReconciliationsAsync(Guid actorId, Guid? financialAccountId, CancellationToken cancellationToken = default);
    Task<BankReconciliationDto> GetReconciliationAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<BankReconciliationDto> StartReconciliationAsync(Guid actorId, StartBankReconciliationRequest request, CancellationToken cancellationToken = default);
    Task<BankReconciliationDto> MatchLinesAsync(Guid actorId, Guid id, MatchReconciliationLinesRequest request, CancellationToken cancellationToken = default);
    Task<BankReconciliationDto> UnmatchLinesAsync(Guid actorId, Guid id, UnmatchReconciliationLinesRequest request, CancellationToken cancellationToken = default);
    Task<BankReconciliationDto> FinalizeReconciliationAsync(Guid actorId, Guid id, FinalizeReconciliationRequest request, CancellationToken cancellationToken = default);
    Task<BankReconciliationDto> ReopenReconciliationAsync(Guid actorId, Guid id, ReopenReconciliationRequest request, CancellationToken cancellationToken = default);
}
