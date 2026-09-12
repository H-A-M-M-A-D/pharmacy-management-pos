using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Accounting;

public sealed record RecurringJournalTemplateLineRequest(Guid ChartOfAccountId, decimal Debit, decimal Credit, string? Description = null);
public sealed record RecurringJournalTemplateRequest(
    string Name, string? Description, RecurringJournalFrequency Frequency, DateOnly StartDate, DateOnly? EndDate,
    Guid BranchId, IReadOnlyList<RecurringJournalTemplateLineRequest> Lines);

public sealed record RecurringJournalTemplateLineDto(Guid ChartOfAccountId, string AccountCode, string AccountName, decimal Debit, decimal Credit, string? Description);
public sealed record RecurringJournalTemplateDto(
    Guid Id, string Name, string? Description, RecurringJournalFrequency Frequency, DateOnly StartDate, DateOnly? EndDate,
    DateOnly NextRunDate, Guid BranchId, string BranchName, bool IsActive,
    IReadOnlyList<RecurringJournalTemplateLineDto> Lines);

public sealed record GenerateDueRecurringJournalsRequest(DateOnly? AsOfDate);
public sealed record GeneratedRecurringJournalDto(Guid TemplateId, string TemplateName, DateOnly ScheduledDate, Guid JournalEntryId, string EntryNumber);
/// <summary>A template whose due-occurrence generation failed partway through this run. <see cref="ScheduledDate"/>
/// is the occurrence date generation stopped at; that template's NextRunDate is left unchanged, so re-running
/// "Generate Due" later safely resumes from the same point (already-generated occurrences are never repeated).</summary>
public sealed record RecurringJournalGenerationFailureDto(Guid TemplateId, string TemplateName, DateOnly ScheduledDate, string Error);
/// <summary>Each template is generated independently and atomically: a failure on one template (recorded in
/// <see cref="Failures"/>) never rolls back occurrences already committed for that template, and never prevents
/// other templates in the same batch from being processed. The batch as a whole is therefore not all-or-nothing.</summary>
public sealed record GenerateDueRecurringJournalsResultDto(IReadOnlyList<GeneratedRecurringJournalDto> Generated, IReadOnlyList<RecurringJournalGenerationFailureDto> Failures);
