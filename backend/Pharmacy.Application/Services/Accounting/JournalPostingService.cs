using Pharmacy.Application.Common;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting;

public sealed class JournalPostingService(IAccountingRepository repository, TimeProvider timeProvider) : IJournalPostingService
{
    public async Task PostAsync(JournalPostingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Lines.Count == 0)
            throw new RequestValidationException("A journal entry requires at least one line.");
        if (await repository.JournalEntryExistsForSourceAsync(request.SourceType, request.SourceId, cancellationToken))
            return;

        var mappings = await repository.GetAccountMappingLookupAsync(cancellationToken);
        decimal totalDebit = 0, totalCredit = 0;
        var lines = new List<JournalEntryLine>();
        foreach (var line in request.Lines)
        {
            if (line.Debit < 0 || line.Credit < 0)
                throw new RequestValidationException("Journal line amounts cannot be negative.");
            if (line.Debit > 0 && line.Credit > 0)
                throw new RequestValidationException("A journal line cannot carry both a debit and a credit.");
            if (line.Debit == 0 && line.Credit == 0)
                throw new RequestValidationException("A journal line must carry a non-zero debit or credit.");
            if (!mappings.TryGetValue(line.Account, out var accountId))
                throw new InvalidOperationException($"No chart of accounts mapping is configured for '{line.Account}'. Configure it under Accounts before posting.");

            totalDebit += line.Debit;
            totalCredit += line.Credit;
            lines.Add(new JournalEntryLine
            {
                ChartOfAccountId = accountId,
                Debit = line.Debit,
                Credit = line.Credit,
                BranchId = request.BranchId,
                CustomerId = line.CustomerId,
                SupplierId = line.SupplierId,
                Description = line.Description
            });
        }

        if (decimal.Round(totalDebit, 2) != decimal.Round(totalCredit, 2))
            throw new InvalidOperationException($"Journal entry for {request.SourceType} {request.SourceId} does not balance: debit {totalDebit} vs credit {totalCredit}.");

        var entry = new JournalEntry
        {
            EntryNumber = await repository.NextJournalEntryNumberAsync(request.OccurredAtUtc, cancellationToken),
            EntryDateUtc = request.OccurredAtUtc,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            Reference = request.Reference,
            Description = request.Description,
            BranchId = request.BranchId,
            PostedByUserId = request.PostedByUserId,
            PostedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            Status = JournalEntryStatus.Posted,
            Lines = lines
        };
        await repository.AddJournalEntryAsync(entry, cancellationToken);
    }
}
