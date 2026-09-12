using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.RecurringJournals;

/// <summary>
/// Recurring journal templates post through the same immutable <see cref="JournalEntry"/> engine as
/// everything else, via an explicit "Generate Due Entries" action rather than a background scheduler
/// — this project has no job-scheduling infrastructure, so recurring posting is a manual, idempotent
/// action an accountant runs periodically. Idempotency is enforced by a unique
/// (TemplateId, ScheduledDate) <see cref="RecurringJournalOccurrence"/> row per generated entry.
/// </summary>
public sealed class RecurringJournalService(IRecurringJournalRepository repository, TimeProvider timeProvider) : IRecurringJournalService
{
    public async Task<IReadOnlyList<RecurringJournalTemplateDto>> ListTemplatesAsync(Guid actorId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsRecurringView, cancellationToken);
        var templates = await repository.ListTemplatesAsync(includeInactive, cancellationToken, Scope(actor));
        return templates.Select(Map).ToList();
    }

    public async Task<RecurringJournalTemplateDto> CreateTemplateAsync(Guid actorId, RecurringJournalTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsRecurringManage, cancellationToken);
        EnsureBranch(actor, request.BranchId);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200) throw new RequestValidationException("Template name is required and must be 200 characters or fewer.");
        if (!Enum.IsDefined(request.Frequency)) throw new RequestValidationException("Frequency is invalid.");
        if (request.EndDate.HasValue && request.EndDate < request.StartDate) throw new RequestValidationException("End date cannot be before start date.");
        if (request.Lines.Count < 2) throw new RequestValidationException("A recurring journal template requires at least two lines.");
        if (await repository.GetBranchAsync(request.BranchId, cancellationToken) is not { IsActive: true }) throw new RequestValidationException("Branch is invalid or inactive.");

        decimal totalDebit = 0, totalCredit = 0;
        var lines = new List<RecurringJournalTemplateLine>();
        foreach (var line in request.Lines)
        {
            if (line.Debit < 0 || line.Credit < 0) throw new RequestValidationException("Line amounts cannot be negative.");
            if (line.Debit > 0 && line.Credit > 0) throw new RequestValidationException("A line cannot carry both a debit and a credit.");
            if (line.Debit == 0 && line.Credit == 0) throw new RequestValidationException("Every line must carry a non-zero debit or credit.");
            var account = await repository.GetAccountAsync(line.ChartOfAccountId, cancellationToken) ?? throw new RequestValidationException("One or more accounts were not found.");
            if (!account.IsActive) throw new RequestValidationException($"Account {account.Code} is inactive.");
            if (!account.IsPostingAccount) throw new RequestValidationException($"Account {account.Code} is a header/summary account and cannot be posted to directly.");
            totalDebit += line.Debit;
            totalCredit += line.Credit;
            lines.Add(new RecurringJournalTemplateLine { ChartOfAccountId = line.ChartOfAccountId, Debit = line.Debit, Credit = line.Credit, Description = Clean(line.Description) });
        }
        if (decimal.Round(totalDebit, 2) != decimal.Round(totalCredit, 2))
            throw new RequestValidationException($"The template does not balance: total debit {totalDebit} vs total credit {totalCredit}.");

        var template = new RecurringJournalTemplate
        {
            Name = request.Name.Trim(), Description = Clean(request.Description), Frequency = request.Frequency,
            StartDate = request.StartDate, EndDate = request.EndDate, NextRunDate = request.StartDate,
            BranchId = request.BranchId, IsActive = true, CreatedByUserId = actorId, Lines = lines
        };
        await repository.AddTemplateAsync(template, cancellationToken);
        await Audit(actorId, "RecurringJournalTemplateCreated", template.Id, new { template.Name, template.Frequency, template.StartDate }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map((await repository.GetTemplateAsync(template.Id, cancellationToken))!);
    }

    public async Task SetTemplateActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsRecurringManage, cancellationToken);
        var template = await repository.GetTemplateAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Recurring journal template was not found.");
        EnsureBranch(actor, template.BranchId);
        template.IsActive = active;
        template.UpdatedAt = UtcNow();
        await Audit(actorId, active ? "RecurringJournalTemplateActivated" : "RecurringJournalTemplateDeactivated", template.Id, new { template.Name }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<GenerateDueRecurringJournalsResultDto> GenerateDueEntriesAsync(Guid actorId, GenerateDueRecurringJournalsRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsRecurringManage, cancellationToken);
        if (HasPermission(actor, PermissionCatalog.AccountsPostToSoftClosed)) repository.AllowPostingIntoSoftClosedPeriod();
        var asOfDate = request.AsOfDate ?? DateOnly.FromDateTime(UtcNow());
        var due = await repository.ListDueTemplatesAsync(asOfDate, cancellationToken, Scope(actor));
        var generated = new List<GeneratedRecurringJournalDto>();
        var failures = new List<RecurringJournalGenerationFailureDto>();

        // A template's entire batch, occurrence records, audit and schedule commit together.
        // A database row lock prevents two generators from reading the same schedule.
        foreach (var dueTemplate in due)
        {
            var scheduledDate = dueTemplate.NextRunDate;
            var templateGenerated = new List<GeneratedRecurringJournalDto>();
            try
            {
                await repository.ExecuteInTransactionAsync(async ct =>
                {
                    var template = await repository.LockTemplateForGenerationAsync(dueTemplate.Id, ct)
                        ?? throw new ResourceNotFoundException("Recurring journal template was not found.");
                    EnsureBranch(actor, template.BranchId);
                    if (!template.IsActive) return;
                    scheduledDate = template.NextRunDate;
                    var iterations = 0;
                    while (scheduledDate <= asOfDate && (!template.EndDate.HasValue || scheduledDate <= template.EndDate.Value) && iterations++ < 60)
                    {
                        var currentScheduledDate = scheduledDate;
                        if (!await repository.OccurrenceExistsAsync(template.Id, currentScheduledDate, ct))
                        {
                            var entryDate = currentScheduledDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                            var entry = new JournalEntry
                            {
                                EntryNumber = await repository.NextJournalEntryNumberAsync(entryDate, ct),
                                EntryDateUtc = entryDate, SourceType = JournalSourceType.RecurringJournal, SourceId = Guid.NewGuid(),
                                Reference = template.Name, Description = $"{template.Name} ({currentScheduledDate:yyyy-MM-dd})",
                                BranchId = template.BranchId, PostedByUserId = actorId, PostedAtUtc = UtcNow(), Status = JournalEntryStatus.Posted,
                                Lines = template.Lines.Select(l => new JournalEntryLine
                                {
                                    ChartOfAccountId = l.ChartOfAccountId, Debit = l.Debit, Credit = l.Credit, BranchId = template.BranchId, Description = l.Description
                                }).ToList()
                            };
                            await repository.AddJournalEntryAsync(entry, ct);
                            await repository.AddOccurrenceAsync(new RecurringJournalOccurrence
                            {
                                RecurringJournalTemplateId = template.Id, ScheduledDate = currentScheduledDate, JournalEntryId = entry.Id,
                                GeneratedAtUtc = UtcNow(), GeneratedByUserId = actorId
                            }, ct);
                            await Audit(actorId, "RecurringJournalGenerated", template.Id, new { template.Name, ScheduledDate = currentScheduledDate, entry.EntryNumber }, ct);
                            await repository.SaveChangesAsync(ct);
                            templateGenerated.Add(new GeneratedRecurringJournalDto(template.Id, template.Name, currentScheduledDate, entry.Id, entry.EntryNumber));
                        }
                        scheduledDate = Advance(currentScheduledDate, template.Frequency);
                    }
                    template.NextRunDate = scheduledDate;
                    template.UpdatedAt = UtcNow();
                    await repository.SaveChangesAsync(ct);
                }, IsolationLevel.ReadCommitted, cancellationToken);
                generated.AddRange(templateGenerated);
            }
            catch (OperationCanceledException)
            {
                repository.DiscardPendingChanges();
                throw;
            }
            catch (Exception ex)
            {
                repository.DiscardPendingChanges();
                failures.Add(new RecurringJournalGenerationFailureDto(dueTemplate.Id, dueTemplate.Name, scheduledDate, ex.Message));
            }
        }
        return new GenerateDueRecurringJournalsResultDto(generated, failures);
    }

    private static DateOnly Advance(DateOnly date, RecurringJournalFrequency frequency) => frequency switch
    {
        RecurringJournalFrequency.Weekly => date.AddDays(7),
        RecurringJournalFrequency.Monthly => date.AddMonths(1),
        RecurringJournalFrequency.Quarterly => date.AddMonths(3),
        RecurringJournalFrequency.Yearly => date.AddYears(1),
        _ => throw new InvalidOperationException("Unsupported recurring journal frequency.")
    };

    private static RecurringJournalTemplateDto Map(RecurringJournalTemplate t) => new(t.Id, t.Name, t.Description, t.Frequency, t.StartDate, t.EndDate,
        t.NextRunDate, t.BranchId, t.Branch?.Name ?? string.Empty, t.IsActive,
        t.Lines.Select(l => new RecurringJournalTemplateLineDto(l.ChartOfAccountId, l.ChartOfAccount?.Code ?? string.Empty, l.ChartOfAccount?.Name ?? string.Empty, l.Debit, l.Credit, l.Description)).ToList());

    private async Task<Domain.Entities.User> Require(Guid actorId, string permission, CancellationToken ct)
    {
        var actor = await repository.GetActorAsync(actorId, ct);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private static Guid? Scope(User actor) => actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager ? null : actor.BranchId;
    private static void EnsureBranch(User actor, Guid branchId)
    {
        if (Scope(actor) is { } allowedBranch && allowedBranch != branchId)
            throw new ForbiddenOperationException("The current user cannot access this branch.");
    }
    private static bool HasPermission(Domain.Entities.User actor, string permission) =>
        actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) == true;

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private async Task Audit(Guid userId, string action, Guid entityId, object values, CancellationToken ct) =>
        await repository.AddAuditAsync(new AuditLog { UserId = userId, Action = action, EntityType = "RecurringJournalTemplate", EntityId = entityId, NewValues = JsonSerializer.Serialize(values) }, ct);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
