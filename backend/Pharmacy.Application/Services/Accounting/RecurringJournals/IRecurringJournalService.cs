using Pharmacy.Application.DTOs.Accounting;

namespace Pharmacy.Application.Services.Accounting.RecurringJournals;

public interface IRecurringJournalService
{
    Task<IReadOnlyList<RecurringJournalTemplateDto>> ListTemplatesAsync(Guid actorId, bool includeInactive, CancellationToken cancellationToken = default);
    Task<RecurringJournalTemplateDto> CreateTemplateAsync(Guid actorId, RecurringJournalTemplateRequest request, CancellationToken cancellationToken = default);
    Task SetTemplateActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default);
    Task<GenerateDueRecurringJournalsResultDto> GenerateDueEntriesAsync(Guid actorId, GenerateDueRecurringJournalsRequest request, CancellationToken cancellationToken = default);
}
