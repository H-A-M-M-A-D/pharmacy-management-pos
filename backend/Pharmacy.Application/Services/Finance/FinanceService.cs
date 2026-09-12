using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Finance;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Finance;

public sealed class FinanceService(IFinanceRepository repository, IJournalPostingService journalPosting, TimeProvider timeProvider) : IFinanceService
{
    public async Task<IReadOnlyList<FinancialAccountDto>> ListAccountsAsync(Guid actorId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsView, cancellationToken);
        var scope = Scope(actor, branchId);
        return await repository.ListAccountsAsync(scope, cancellationToken);
    }

    public async Task<FinancialAccountDto> GetAccountAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsView, cancellationToken);
        var account = await RequiredAccount(id, cancellationToken);
        EnsureBranchAccess(actor, account.BranchId);
        return await MapAccount(account, cancellationToken);
    }

    public async Task<FinancialAccountDto> CreateAccountAsync(Guid actorId, FinancialAccountRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsManage, cancellationToken);
        ValidateAccount(request.Name, request.AccountType, request.OpeningBalance);
        EnsureBranchAccess(actor, request.BranchId);
        if (await repository.GetBranchAsync(request.BranchId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Branch is invalid or inactive.");
        var normalized = Normalize(request.Name);
        if (await repository.AccountNameExistsAsync(request.BranchId, normalized, cancellationToken: cancellationToken))
            throw new ResourceConflictException("An account with this name already exists in the branch.");

        FinancialAccount? account = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            account = new FinancialAccount
            {
                BranchId = request.BranchId,
                Name = request.Name.Trim(),
                NormalizedName = normalized,
                AccountType = request.AccountType,
                OpeningBalance = Money(request.OpeningBalance),
                Notes = Clean(request.Notes),
                IsActive = request.IsActive
            };
            await repository.AddAccountAsync(account, ct);
            if (account.OpeningBalance != 0)
            {
                await repository.AddLedgerEntryAsync(Entry(account, FinancialLedgerEntryType.OpeningBalance,
                    account.OpeningBalance, "FinancialAccount", account.Id, "Opening balance", actorId, UtcNow()), ct);
                await PostFinancialAccountOpeningBalanceJournalAsync(actorId, account, ct);
            }
            await Audit(actorId, "FinancialAccountCreated", "FinancialAccount", account.Id,
                new { account.Name, account.BranchId, account.AccountType, account.OpeningBalance }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAccount(account!, cancellationToken);
    }

    public async Task<FinancialAccountDto> UpdateAccountAsync(Guid actorId, Guid id, FinancialAccountUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsManage, cancellationToken);
        ValidateAccount(request.Name, request.AccountType, 0);
        var account = await RequiredAccount(id, cancellationToken);
        EnsureBranchAccess(actor, account.BranchId);
        var normalized = Normalize(request.Name);
        if (await repository.AccountNameExistsAsync(account.BranchId, normalized, id, cancellationToken))
            throw new ResourceConflictException("An account with this name already exists in the branch.");
        var old = new { account.Name, account.AccountType, account.Notes };
        account.Name = request.Name.Trim();
        account.NormalizedName = normalized;
        account.AccountType = request.AccountType;
        account.Notes = Clean(request.Notes);
        account.UpdatedAt = UtcNow();
        await Audit(actorId, "FinancialAccountUpdated", "FinancialAccount", account.Id,
            new { Old = old, New = new { account.Name, account.AccountType, account.Notes } }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await MapAccount(account, cancellationToken);
    }

    public async Task SetAccountActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsManage, cancellationToken);
        var account = await RequiredAccount(id, cancellationToken);
        EnsureBranchAccess(actor, account.BranchId);
        account.IsActive = active;
        account.UpdatedAt = UtcNow();
        await Audit(actorId, active ? "FinancialAccountActivated" : "FinancialAccountDeactivated",
            "FinancialAccount", id, new { account.Name }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialLedgerEntryDto>> LedgerAsync(Guid actorId, Guid accountId, FinancialLedgerQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.FinanceLedgerView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        var account = await RequiredAccount(accountId, cancellationToken);
        EnsureBranchAccess(actor, account.BranchId);
        return await repository.ListLedgerAsync(accountId, query, cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(Guid actorId, bool? active, CancellationToken cancellationToken = default)
    {
        await RequireAny(actorId, [PermissionCatalog.ExpensesView, PermissionCatalog.ExpensesCreate], cancellationToken);
        return await repository.ListCategoriesAsync(active, cancellationToken);
    }

    public async Task<ExpenseCategoryDto> CreateCategoryAsync(Guid actorId, ExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsManage, cancellationToken);
        ValidateName(request.Name, "Category name");
        var normalized = Normalize(request.Name);
        if (await repository.CategoryNameExistsAsync(normalized, cancellationToken: cancellationToken))
            throw new ResourceConflictException("An expense category with this name already exists.");
        var category = new ExpenseCategory { Name = request.Name.Trim(), NormalizedName = normalized, Description = Clean(request.Description), IsActive = request.IsActive };
        await repository.AddCategoryAsync(category, cancellationToken);
        await Audit(actorId, "ExpenseCategoryCreated", "ExpenseCategory", category.Id, new { category.Name, category.IsActive }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return MapCategory(category);
    }

    public async Task<ExpenseCategoryDto> UpdateCategoryAsync(Guid actorId, Guid id, ExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsManage, cancellationToken);
        ValidateName(request.Name, "Category name");
        var category = await repository.GetCategoryAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Expense category was not found.");
        var normalized = Normalize(request.Name);
        if (await repository.CategoryNameExistsAsync(normalized, id, cancellationToken)) throw new ResourceConflictException("An expense category with this name already exists.");
        var old = new { category.Name, category.Description, category.IsActive };
        category.Name = request.Name.Trim(); category.NormalizedName = normalized; category.Description = Clean(request.Description); category.IsActive = request.IsActive; category.UpdatedAt = UtcNow();
        await Audit(actorId, "ExpenseCategoryUpdated", "ExpenseCategory", category.Id,
            new { Old = old, New = new { category.Name, category.Description, category.IsActive } }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return MapCategory(category);
    }

    public async Task SetCategoryActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsManage, cancellationToken);
        var category = await repository.GetCategoryAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Expense category was not found.");
        category.IsActive = active; category.UpdatedAt = UtcNow();
        await Audit(actorId, active ? "ExpenseCategoryActivated" : "ExpenseCategoryDeactivated",
            "ExpenseCategory", id, new { category.Name }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseDto>> ListExpensesAsync(Guid actorId, ExpenseQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.ExpensesView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        if (query.BranchId.HasValue) EnsureBranchAccess(actor, query.BranchId.Value);
        return await repository.ListExpensesAsync(query, actor.BranchId, CanSelectBranch(actor), cancellationToken);
    }

    public async Task<ExpenseDto> PostExpenseAsync(Guid actorId, PostExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.ExpensesCreate, cancellationToken);
        _ = await Require(actorId, PermissionCatalog.ExpensesPost, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        ValidatePositive(request.Amount, "Expense amount"); ValidateName(request.Description, "Description");
        Expense? expense = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var account = await LockedActiveAccount(request.FinancialAccountId, request.BranchId, ct);
            var category = await repository.GetCategoryAsync(request.ExpenseCategoryId, ct);
            if (category is not { IsActive: true }) throw new RequestValidationException("Expense category is invalid or inactive.");
            if (request.CostCenterId.HasValue && await repository.GetCostCenterAsync(request.CostCenterId.Value, ct) is not { IsActive: true })
                throw new RequestValidationException("Cost center is invalid or inactive.");
            await EnsureFunds(account.Id, request.Amount, ct);
            var now = UtcNow();
            expense = new Expense { ExpenseNumber = await repository.NextExpenseNumberAsync(request.ExpenseDateUtc, ct), BranchId = request.BranchId,
                ExpenseCategoryId = category.Id, FinancialAccountId = account.Id, ExpenseDateUtc = AsUtc(request.ExpenseDateUtc), Amount = Money(request.Amount),
                Description = request.Description.Trim(), Payee = Clean(request.Payee), ReferenceNumber = Clean(request.ReferenceNumber), Notes = Clean(request.Notes),
                CostCenterId = request.CostCenterId, CreatedByUserId = actorId, PostedAtUtc = now };
            await repository.AddExpenseAsync(expense, ct);
            await repository.AddLedgerEntryAsync(Entry(account, FinancialLedgerEntryType.Expense, -expense.Amount, "Expense", expense.Id, expense.Description, actorId, expense.ExpenseDateUtc, expense.ExpenseNumber), ct);
            await PostExpenseJournalAsync(actorId, expense, account, ct);
            await Audit(actorId, "ExpensePosted", "Expense", expense.Id, new { expense.ExpenseNumber, expense.BranchId, expense.FinancialAccountId, expense.Amount }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return (await repository.ListExpensesAsync(new ExpenseQuery(PageSize: 100), actor.BranchId, CanSelectBranch(actor), cancellationToken)).Single(x => x.Id == expense!.Id);
    }

    public async Task<ExpenseDto> ReverseExpenseAsync(Guid actorId, Guid id, ReverseExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.ExpensesPost, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("A reason is required to reverse an expense.");
        var expense = await repository.GetExpenseEntityAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Expense was not found.");
        EnsureBranchAccess(actor, expense.BranchId);
        if (expense.ReversedAtUtc.HasValue) throw new RequestValidationException("This expense has already been reversed.");

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var account = await LockedActiveAccount(expense.FinancialAccountId, expense.BranchId, ct);
            var now = UtcNow();
            await repository.AddLedgerEntryAsync(Entry(account, FinancialLedgerEntryType.AdjustmentCredit, expense.Amount, "ExpenseReversal", expense.Id,
                $"Reversal of expense {expense.ExpenseNumber}: {request.Reason.Trim()}", actorId, now), ct);
            await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.ExpenseReversal, expense.Id, expense.BranchId, now,
                expense.ExpenseNumber, $"Reversal of expense {expense.ExpenseNumber}: {request.Reason.Trim()}", actorId,
                [new(PaymentAccount(account.AccountType), expense.Amount, 0), new(AccountMappingKey.GeneralExpenseDefault, 0, expense.Amount, CostCenterId: expense.CostCenterId)]), ct);
            expense.ReversedAtUtc = now;
            expense.ReversedByUserId = actorId;
            expense.ReversalReason = request.Reason.Trim();
            await Audit(actorId, "ExpenseReversed", "Expense", expense.Id, new { expense.ExpenseNumber, request.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return (await repository.ListExpensesAsync(new ExpenseQuery(PageSize: 100), actor.BranchId, CanSelectBranch(actor), cancellationToken)).Single(x => x.Id == expense.Id);
    }

    public async Task<OtherIncomeDto> PostOtherIncomeAsync(Guid actorId, PostOtherIncomeRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.FinanceIncomeCreate, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId); ValidatePositive(request.Amount, "Income amount"); ValidateName(request.Description, "Description");
        OtherIncome? income = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var account = await LockedActiveAccount(request.FinancialAccountId, request.BranchId, ct);
            if (request.CostCenterId.HasValue && await repository.GetCostCenterAsync(request.CostCenterId.Value, ct) is not { IsActive: true })
                throw new RequestValidationException("Cost center is invalid or inactive.");
            income = new OtherIncome { IncomeNumber = await repository.NextIncomeNumberAsync(request.OccurredAtUtc, ct), BranchId = request.BranchId,
                FinancialAccountId = account.Id, Amount = Money(request.Amount), Description = request.Description.Trim(), ReferenceNumber = Clean(request.ReferenceNumber),
                Notes = Clean(request.Notes), CostCenterId = request.CostCenterId, OccurredAtUtc = AsUtc(request.OccurredAtUtc), CreatedByUserId = actorId };
            await repository.AddOtherIncomeAsync(income, ct);
            await repository.AddLedgerEntryAsync(Entry(account, FinancialLedgerEntryType.OtherIncome, income.Amount, "OtherIncome", income.Id, income.Description, actorId, income.OccurredAtUtc, income.IncomeNumber), ct);
            await PostOtherIncomeJournalAsync(actorId, income, account, ct);
            await Audit(actorId, "OtherIncomePosted", "OtherIncome", income.Id, new { income.IncomeNumber, income.BranchId, income.FinancialAccountId, income.Amount }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return new(income!.Id, income.IncomeNumber, income.BranchId, income.FinancialAccountId, income.Amount, income.Description, income.OccurredAtUtc,
            CostCenterId: income.CostCenterId);
    }

    public async Task<OtherIncomeDto> ReverseOtherIncomeAsync(Guid actorId, Guid id, ReverseOtherIncomeRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.FinanceIncomeCreate, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("A reason is required to reverse other income.");
        var income = await repository.GetOtherIncomeEntityAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Other income was not found.");
        EnsureBranchAccess(actor, income.BranchId);
        if (income.ReversedAtUtc.HasValue) throw new RequestValidationException("This income has already been reversed.");

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var account = await LockedActiveAccount(income.FinancialAccountId, income.BranchId, ct);
            await EnsureFunds(account.Id, income.Amount, ct);
            var now = UtcNow();
            await repository.AddLedgerEntryAsync(Entry(account, FinancialLedgerEntryType.AdjustmentDebit, -income.Amount, "OtherIncomeReversal", income.Id,
                $"Reversal of income {income.IncomeNumber}: {request.Reason.Trim()}", actorId, now), ct);
            await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.OtherIncomeReversal, income.Id, income.BranchId, now,
                income.IncomeNumber, $"Reversal of income {income.IncomeNumber}: {request.Reason.Trim()}", actorId,
                [new(AccountMappingKey.OtherIncomeDefault, income.Amount, 0, CostCenterId: income.CostCenterId), new(PaymentAccount(account.AccountType), 0, income.Amount)]), ct);
            income.ReversedAtUtc = now;
            income.ReversedByUserId = actorId;
            income.ReversalReason = request.Reason.Trim();
            await Audit(actorId, "OtherIncomeReversed", "OtherIncome", income.Id, new { income.IncomeNumber, request.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return new(income.Id, income.IncomeNumber, income.BranchId, income.FinancialAccountId, income.Amount, income.Description, income.OccurredAtUtc,
            income.ReversedAtUtc, actor.FullName, income.ReversalReason, income.CostCenterId);
    }

    public async Task<FinancialTransferDto> PostTransferAsync(Guid actorId, PostTransferRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.FinanceTransfer, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId); ValidatePositive(request.Amount, "Transfer amount");
        if (request.SourceAccountId == request.DestinationAccountId) throw new RequestValidationException("Source and destination accounts must differ.");
        FinancialTransfer? transfer = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var orderedIds = new[] { request.SourceAccountId, request.DestinationAccountId }.Order().ToArray();
            var first = await repository.GetAccountAsync(orderedIds[0], true, ct);
            var second = await repository.GetAccountAsync(orderedIds[1], true, ct);
            var source = request.SourceAccountId == orderedIds[0] ? first : second;
            var destination = request.DestinationAccountId == orderedIds[0] ? first : second;
            if (source is not { IsActive: true } || destination is not { IsActive: true } || source.BranchId != request.BranchId || destination.BranchId != request.BranchId)
                throw new RequestValidationException("Transfer accounts must be active accounts in the selected branch.");
            await EnsureFunds(source.Id, request.Amount, ct);
            transfer = new FinancialTransfer { TransferNumber = await repository.NextTransferNumberAsync(request.OccurredAtUtc, ct), BranchId = request.BranchId,
                SourceAccountId = source.Id, DestinationAccountId = destination.Id, Amount = Money(request.Amount), OccurredAtUtc = AsUtc(request.OccurredAtUtc),
                ReferenceNumber = Clean(request.ReferenceNumber), Notes = Clean(request.Notes), CreatedByUserId = actorId };
            await repository.AddTransferAsync(transfer, ct);
            await repository.AddLedgerEntryAsync(Entry(source, FinancialLedgerEntryType.TransferOut, -transfer.Amount, "FinancialTransfer", transfer.Id, "Transfer to " + destination.Name, actorId, transfer.OccurredAtUtc, transfer.TransferNumber), ct);
            await repository.AddLedgerEntryAsync(Entry(destination, FinancialLedgerEntryType.TransferIn, transfer.Amount, "FinancialTransfer", transfer.Id, "Transfer from " + source.Name, actorId, transfer.OccurredAtUtc, transfer.TransferNumber), ct);
            await PostTransferJournalAsync(actorId, transfer, source, destination, ct);
            await Audit(actorId, "AccountTransferPosted", "FinancialTransfer", transfer.Id, new { transfer.TransferNumber, transfer.SourceAccountId, transfer.DestinationAccountId, transfer.Amount }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return new(transfer!.Id, transfer.TransferNumber, transfer.BranchId, transfer.SourceAccountId, transfer.DestinationAccountId, transfer.Amount, transfer.OccurredAtUtc);
    }

    public async Task PostAdjustmentAsync(Guid actorId, PostFinancialAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.FinanceAdjust, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId); ValidatePositive(request.Amount, "Adjustment amount"); ValidateName(request.Reason, "Reason");
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var account = await LockedActiveAccount(request.FinancialAccountId, request.BranchId, ct);
            var amount = request.Type == FinancialAdjustmentType.Debit ? -Money(request.Amount) : Money(request.Amount);
            if (amount < 0) await EnsureFunds(account.Id, request.Amount, ct);
            var id = Guid.NewGuid();
            var entry = Entry(account, request.Type == FinancialAdjustmentType.Debit ? FinancialLedgerEntryType.AdjustmentDebit : FinancialLedgerEntryType.AdjustmentCredit,
                amount, "FinancialAdjustment", id, request.Reason.Trim(), actorId, AsUtc(request.OccurredAtUtc));
            await repository.AddLedgerEntryAsync(entry, ct);
            await PostFinancialAdjustmentJournalAsync(actorId, account, entry, ct);
            await Audit(actorId, "FinancialAdjustmentPosted", "FinancialAdjustment", id, new { request.BranchId, request.FinancialAccountId, request.Type, Amount = Money(request.Amount), Reason = request.Reason.Trim() }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task<DailyCashPositionDto> DailyPositionAsync(Guid actorId, Guid branchId, DateOnly date, Guid? accountId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.FinanceLedgerView, cancellationToken);
        EnsureBranchAccess(actor, branchId);
        if (accountId.HasValue)
        {
            var account = await RequiredAccount(accountId.Value, cancellationToken);
            if (account.BranchId != branchId) throw new RequestValidationException("Account does not belong to the selected branch.");
        }
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var start = TimeZoneInfo.ConvertTimeToUtc(localStart, zone);
        var end = start.AddDays(1);
        var opening = accountId.HasValue
            ? await repository.GetBalanceAsync(accountId.Value, start, cancellationToken)
            : (await repository.ListAccountsAsync(branchId, cancellationToken)).Sum(x => x.CurrentBalance - 0m) -
              (await repository.ListPositionEntriesAsync(branchId, null, start, DateTime.MaxValue, cancellationToken)).Sum(x => x.Amount);
        var entries = await repository.ListPositionEntriesAsync(branchId, accountId, start, end, cancellationToken);
        var moneyIn = entries.Where(x => x.Amount > 0).Sum(x => x.Amount);
        var moneyOut = -entries.Where(x => x.Amount < 0).Sum(x => x.Amount);
        var breakdown = entries.GroupBy(x => x.EntryType).OrderBy(x => x.Key).Select(x => new CashPositionBreakdownDto(x.Key,
            x.Where(e => e.Amount > 0).Sum(e => e.Amount), -x.Where(e => e.Amount < 0).Sum(e => e.Amount))).ToList();
        return new(branchId, date, accountId, opening, moneyIn, moneyOut, opening + moneyIn - moneyOut, breakdown);
    }

    private async Task PostExpenseJournalAsync(Guid actorId, Expense expense, FinancialAccount account, CancellationToken ct)
    {
        await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.Expense, expense.Id, expense.BranchId, expense.ExpenseDateUtc,
            expense.ExpenseNumber, $"Expense {expense.ExpenseNumber}", actorId,
            [new(AccountMappingKey.GeneralExpenseDefault, expense.Amount, 0, CostCenterId: expense.CostCenterId), new(PaymentAccount(account.AccountType), 0, expense.Amount)]), ct);
    }

    private async Task PostFinancialAccountOpeningBalanceJournalAsync(Guid actorId, FinancialAccount account, CancellationToken ct)
    {
        var amount = Math.Abs(account.OpeningBalance);
        var accountMapping = PaymentAccount(account.AccountType);
        var lines = account.OpeningBalance > 0
            ? new List<JournalLineInput> { new(accountMapping, amount, 0), new(AccountMappingKey.RetainedEarnings, 0, amount) }
            : new List<JournalLineInput> { new(AccountMappingKey.RetainedEarnings, amount, 0), new(accountMapping, 0, amount) };
        await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.OpeningBalance, account.Id, account.BranchId, UtcNow(),
            "Opening balance", $"Opening balance for {account.Name}", actorId, lines), ct);
    }

    private async Task PostOtherIncomeJournalAsync(Guid actorId, OtherIncome income, FinancialAccount account, CancellationToken ct)
    {
        await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.OtherIncome, income.Id, income.BranchId, income.OccurredAtUtc,
            income.IncomeNumber, $"Other income {income.IncomeNumber}", actorId,
            [new(PaymentAccount(account.AccountType), income.Amount, 0), new(AccountMappingKey.OtherIncomeDefault, 0, income.Amount, CostCenterId: income.CostCenterId)]), ct);
    }

    private async Task PostTransferJournalAsync(Guid actorId, FinancialTransfer transfer, FinancialAccount source, FinancialAccount destination, CancellationToken ct)
    {
        await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.CashTransfer, transfer.Id, transfer.BranchId, transfer.OccurredAtUtc,
            transfer.TransferNumber, $"Account transfer {transfer.TransferNumber}", actorId,
            [new(PaymentAccount(destination.AccountType), transfer.Amount, 0), new(PaymentAccount(source.AccountType), 0, transfer.Amount)]), ct);
    }

    private async Task PostFinancialAdjustmentJournalAsync(Guid actorId, FinancialAccount account, FinancialLedgerEntry entry, CancellationToken ct)
    {
        var amount = Math.Abs(entry.Amount);
        var accountMapping = PaymentAccount(account.AccountType);
        var lines = entry.Amount > 0
            ? new List<JournalLineInput>
            {
                new(accountMapping, amount, 0),
                new(AccountMappingKey.CashBankAdjustmentSuspense, 0, amount)
            }
            : new List<JournalLineInput>
            {
                new(AccountMappingKey.CashBankAdjustmentSuspense, amount, 0),
                new(accountMapping, 0, amount)
            };
        await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.FinancialAccountAdjustment, entry.ReferenceId, entry.BranchId,
            entry.OccurredAtUtc, entry.ReferenceType, entry.Description, actorId, lines), ct);
    }

    private static AccountMappingKey PaymentAccount(FinancialAccountType type) => type == FinancialAccountType.Cash ? AccountMappingKey.Cash : AccountMappingKey.Bank;

    private async Task<FinancialAccount> LockedActiveAccount(Guid accountId, Guid branchId, CancellationToken ct)
    {
        var account = await repository.GetAccountAsync(accountId, true, ct);
        if (account is not { IsActive: true } || account.BranchId != branchId) throw new RequestValidationException("Financial account is invalid, inactive, or belongs to another branch.");
        return account;
    }
    private async Task EnsureFunds(Guid accountId, decimal amount, CancellationToken ct)
    {
        if (await repository.GetBalanceAsync(accountId, cancellationToken: ct) < Money(amount)) throw new ResourceConflictException("The financial account has insufficient funds.");
    }
    private async Task<User> Require(Guid actorId, string permission, CancellationToken ct)
    {
        var actor = await repository.GetActorAsync(actorId, ct);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true) throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }
    private async Task<User> RequireAny(Guid actorId, string[] permissions, CancellationToken ct)
    {
        var actor = await repository.GetActorAsync(actorId, ct);
        if (actor is null || !actor.IsActive || !permissions.Any(p => actor.Role?.RolePermissions.Any(x => x.Permission?.Code == p) == true)) throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }
    private async Task<FinancialAccount> RequiredAccount(Guid id, CancellationToken ct) => await repository.GetAccountAsync(id, cancellationToken: ct) ?? throw new ResourceNotFoundException("Financial account was not found.");
    private async Task<FinancialAccountDto> MapAccount(FinancialAccount account, CancellationToken ct)
    {
        var branch = await repository.GetBranchAsync(account.BranchId, ct);
        return new(account.Id, account.BranchId, branch?.Name ?? string.Empty, account.Name, account.AccountType,
            account.OpeningBalance, await repository.GetBalanceAsync(account.Id, cancellationToken: ct), account.IsActive,
            account.Notes, account.CreatedAt, account.UpdatedAt);
    }
    private static ExpenseCategoryDto MapCategory(ExpenseCategory x) => new(x.Id, x.Name, x.Description, x.IsActive, x.CreatedAt, x.UpdatedAt);
    private async Task Audit(Guid userId, string action, string entityType, Guid entityId, object values, CancellationToken ct) =>
        await repository.AddAuditAsync(new AuditLog { UserId = userId, Action = action, EntityType = entityType, EntityId = entityId, NewValues = JsonSerializer.Serialize(values) }, ct);
    private static FinancialLedgerEntry Entry(FinancialAccount account, FinancialLedgerEntryType type, decimal amount, string referenceType, Guid referenceId, string description, Guid userId, DateTime occurredAtUtc, string? referenceNumber = null) =>
        new() { FinancialAccountId = account.Id, BranchId = account.BranchId, EntryType = type, Amount = Money(amount), ReferenceType = referenceType, ReferenceId = referenceId, ReferenceNumber = referenceNumber, Description = description, CreatedByUserId = userId, OccurredAtUtc = AsUtc(occurredAtUtc) };
    private static Guid? Scope(User actor, Guid? requested) { if (CanSelectBranch(actor)) return requested; if (requested.HasValue && requested != actor.BranchId) throw new ForbiddenOperationException("The current user cannot access this branch."); return actor.BranchId; }
    private static void EnsureBranchAccess(User actor, Guid branchId) { if (!CanSelectBranch(actor) && actor.BranchId != branchId) throw new ForbiddenOperationException("The current user cannot access this branch."); }
    private static bool CanSelectBranch(User actor) => actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager;
    private static void ValidateAccount(string name, FinancialAccountType type, decimal opening) { ValidateName(name, "Account name"); if (!Enum.IsDefined(type)) throw new RequestValidationException("Account type is invalid."); if (decimal.Round(opening, 2) != opening) throw new RequestValidationException("Opening balance supports at most two decimal places."); }
    private static void ValidatePositive(decimal amount, string label) { if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new RequestValidationException(label + " must be greater than zero with at most two decimal places."); }
    private static void ValidateName(string value, string label) { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 300) throw new RequestValidationException(label + " is required and must be 300 characters or fewer."); }
    private static void ValidatePage(int page, int pageSize) { if (page < 1 || pageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100."); }
    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
