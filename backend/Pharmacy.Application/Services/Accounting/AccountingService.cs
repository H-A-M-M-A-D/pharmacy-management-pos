using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting;

public sealed class AccountingService(IAccountingRepository repository, TimeProvider timeProvider) : IAccountingService
{
    public async Task<IReadOnlyList<ChartOfAccountListItemDto>> ListAccountsAsync(Guid actorId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsCoaView, cancellationToken);
        var accounts = await repository.ListAccountsAsync(includeInactive, cancellationToken);
        var byId = accounts.ToDictionary(x => x.Id);
        return accounts.Select(x => new ChartOfAccountListItemDto(x.Id, x.Code, x.Name, x.ParentAccountId,
            x.ParentAccountId.HasValue && byId.TryGetValue(x.ParentAccountId.Value, out var parent) ? parent.Name : null,
            x.AccountType, x.NormalBalance, x.IsPostingAccount, x.IsActive, x.Description)).ToList();
    }

    public async Task<ChartOfAccountDto> GetAccountAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsCoaView, cancellationToken);
        var account = await RequiredAccount(id, cancellationToken);
        var balance = await repository.GetAccountBalanceAsync(id, cancellationToken);
        return await MapAccount(account, balance, cancellationToken);
    }

    public async Task<ChartOfAccountDto> CreateAccountAsync(Guid actorId, ChartOfAccountRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsCoaManage, cancellationToken);
        ValidateCode(request.Code);
        ValidateName(request.Name);
        if (!Enum.IsDefined(request.AccountType)) throw new RequestValidationException("Account type is invalid.");
        if (!Enum.IsDefined(request.NormalBalance)) throw new RequestValidationException("Normal balance is invalid.");
        var normalized = Normalize(request.Code);
        if (await repository.GetAccountByNormalizedCodeAsync(normalized, cancellationToken) is not null)
            throw new ResourceConflictException("An account with this code already exists.");
        if (request.ParentAccountId.HasValue && await repository.GetAccountAsync(request.ParentAccountId.Value, cancellationToken) is null)
            throw new RequestValidationException("Parent account was not found.");

        ChartOfAccount? account = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            account = new ChartOfAccount
            {
                Code = request.Code.Trim(),
                NormalizedCode = normalized,
                Name = request.Name.Trim(),
                ParentAccountId = request.ParentAccountId,
                AccountType = request.AccountType,
                NormalBalance = request.NormalBalance,
                IsPostingAccount = request.IsPostingAccount,
                Description = Clean(request.Description)
            };
            await repository.AddAccountAsync(account, ct);
            await Audit(actorId, "ChartOfAccountCreated", "ChartOfAccount", account.Id, new { account.Code, account.Name, account.AccountType, account.NormalBalance }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAccount(account!, 0, cancellationToken);
    }

    public async Task<ChartOfAccountDto> UpdateAccountAsync(Guid actorId, Guid id, ChartOfAccountUpdateRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsCoaManage, cancellationToken);
        ValidateName(request.Name);
        var account = await RequiredAccount(id, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            account.Name = request.Name.Trim();
            account.Description = Clean(request.Description);
            account.IsPostingAccount = request.IsPostingAccount;
            account.UpdatedAt = UtcNow();
            await Audit(actorId, "ChartOfAccountUpdated", "ChartOfAccount", account.Id, new { account.Name, account.IsPostingAccount }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAccount(account, await repository.GetAccountBalanceAsync(id, cancellationToken), cancellationToken);
    }

    public async Task SetAccountActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsCoaManage, cancellationToken);
        var account = await RequiredAccount(id, cancellationToken);
        if (!active && await repository.IsAccountMappedAsync(id, cancellationToken))
            throw new RequestValidationException("This account is used by an active account mapping. Reassign the mapping before deactivating it.");
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            account.IsActive = active;
            account.UpdatedAt = UtcNow();
            await Audit(actorId, active ? "ChartOfAccountActivated" : "ChartOfAccountDeactivated", "ChartOfAccount", account.Id, new { account.Code }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountMappingDto>> ListAccountMappingsAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsCoaView, cancellationToken);
        var mappings = await repository.ListAccountMappingsAsync(cancellationToken);
        return mappings.Select(x => new AccountMappingDto(x.MappingKey, x.ChartOfAccountId, x.ChartOfAccount?.Code ?? string.Empty, x.ChartOfAccount?.Name ?? string.Empty)).ToList();
    }

    public async Task<AccountMappingDto> SetAccountMappingAsync(Guid actorId, SetAccountMappingRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsCoaManage, cancellationToken);
        if (!Enum.IsDefined(request.MappingKey)) throw new RequestValidationException("Mapping key is invalid.");
        var account = await repository.GetAccountAsync(request.ChartOfAccountId, cancellationToken)
            ?? throw new RequestValidationException("Target account was not found.");
        if (!account.IsActive) throw new RequestValidationException("Cannot map to an inactive account.");
        if (!account.IsPostingAccount) throw new RequestValidationException("Cannot map to a header/summary account. Choose a posting account.");

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var existing = await repository.GetAccountMappingAsync(request.MappingKey, ct);
            if (existing is null)
            {
                await repository.AddAccountMappingAsync(new AccountMapping { MappingKey = request.MappingKey, ChartOfAccountId = request.ChartOfAccountId }, ct);
            }
            else
            {
                existing.ChartOfAccountId = request.ChartOfAccountId;
                existing.UpdatedAt = UtcNow();
            }
            await Audit(actorId, "AccountMappingSet", "AccountMapping", request.ChartOfAccountId, new { request.MappingKey, request.ChartOfAccountId }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return new AccountMappingDto(request.MappingKey, request.ChartOfAccountId, account.Code, account.Name);
    }

    public async Task<JournalEntryDto> PostManualJournalAsync(Guid actorId, PostManualJournalRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalPost, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        if (string.IsNullOrWhiteSpace(request.Description)) throw new RequestValidationException("Description is required.");
        if (request.Lines.Count < 2) throw new RequestValidationException("A journal entry requires at least two lines.");
        if (await repository.GetBranchAsync(request.BranchId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Branch is invalid or inactive.");
        if (actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.AccountsPostToSoftClosed) == true)
            repository.AllowPostingIntoSoftClosedPeriod();

        JournalEntry? entry = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            decimal totalDebit = 0, totalCredit = 0;
            var lines = new List<JournalEntryLine>();
            foreach (var line in request.Lines)
            {
                if (line.Debit < 0 || line.Credit < 0) throw new RequestValidationException("Line amounts cannot be negative.");
                if (line.Debit > 0 && line.Credit > 0) throw new RequestValidationException("A line cannot carry both a debit and a credit.");
                if (line.Debit == 0 && line.Credit == 0) throw new RequestValidationException("Every line must carry a non-zero debit or credit.");
                var account = await repository.GetAccountAsync(line.ChartOfAccountId, ct)
                    ?? throw new RequestValidationException("One or more accounts were not found.");
                if (!account.IsActive) throw new RequestValidationException($"Account {account.Code} is inactive.");
                if (!account.IsPostingAccount) throw new RequestValidationException($"Account {account.Code} is a header/summary account and cannot be posted to directly.");
                if (line.CustomerId.HasValue && await repository.GetCustomerAsync(line.CustomerId.Value, ct) is null)
                    throw new RequestValidationException("Customer was not found.");
                if (line.SupplierId.HasValue && await repository.GetSupplierAsync(line.SupplierId.Value, ct) is null)
                    throw new RequestValidationException("Supplier was not found.");
                if (line.CostCenterId.HasValue && await repository.GetCostCenterAsync(line.CostCenterId.Value, ct) is not { IsActive: true })
                    throw new RequestValidationException("Cost center is invalid or inactive.");
                totalDebit += line.Debit;
                totalCredit += line.Credit;
                lines.Add(new JournalEntryLine
                {
                    ChartOfAccountId = line.ChartOfAccountId, Debit = line.Debit, Credit = line.Credit,
                    BranchId = request.BranchId, CustomerId = line.CustomerId, SupplierId = line.SupplierId, Description = Clean(line.Description),
                    CostCenterId = line.CostCenterId
                });
            }
            if (decimal.Round(totalDebit, 2) != decimal.Round(totalCredit, 2))
                throw new RequestValidationException($"The entry does not balance: total debit {totalDebit} vs total credit {totalCredit}.");

            entry = new JournalEntry
            {
                EntryNumber = await repository.NextJournalEntryNumberAsync(request.EntryDateUtc, ct),
                EntryDateUtc = request.EntryDateUtc,
                SourceType = JournalSourceType.ManualVoucher,
                Reference = Clean(request.Reference),
                Description = request.Description.Trim(),
                BranchId = request.BranchId,
                PostedByUserId = actorId,
                PostedAtUtc = UtcNow(),
                Status = JournalEntryStatus.Posted,
                Lines = lines
            };
            await repository.AddJournalEntryAsync(entry, ct);
            await Audit(actorId, "ManualJournalPosted", "JournalEntry", entry.Id, new { entry.EntryNumber, TotalDebit = totalDebit, TotalCredit = totalCredit }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapJournalEntry(entry!, cancellationToken);
    }

    public async Task<JournalEntryDto> GetJournalEntryAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalView, cancellationToken);
        var entry = await repository.GetJournalEntryAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Journal entry was not found.");
        EnsureBranchAccess(actor, entry.BranchId);
        return await MapJournalEntry(entry, cancellationToken);
    }

    public async Task<PagedResult<JournalEntryListItemDto>> ListJournalEntriesAsync(Guid actorId, JournalEntryListQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalView, cancellationToken);
        if (query.Page < 1 || query.PageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
        var scope = Scope(actor, query.BranchId);
        return await repository.ListJournalEntriesAsync(query with { BranchId = scope }, actor.BranchId, CanSelectBranch(actor), cancellationToken);
    }

    public async Task<TrialBalanceDto> GetTrialBalanceAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalView, cancellationToken);
        var scope = Scope(actor, branchId);
        var rows = await repository.GetTrialBalanceAsync(asOfUtc, scope, cancellationToken);
        return new TrialBalanceDto(asOfUtc, rows, rows.Sum(x => x.Debit), rows.Sum(x => x.Credit));
    }

    public async Task<GeneralLedgerDto> GetGeneralLedgerAsync(Guid actorId, GeneralLedgerQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalView, cancellationToken);
        if (query.Page < 1 || query.PageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc > query.ToUtc)
            throw new RequestValidationException("From date cannot be after to date.");
        await RequiredAccount(query.ChartOfAccountId, cancellationToken);
        return await repository.GetGeneralLedgerAsync(query with { BranchId = Scope(actor, query.BranchId) }, cancellationToken);
    }

    public async Task<ProfitAndLossDto> GetProfitAndLossAsync(Guid actorId, DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalView, cancellationToken);
        if (fromUtc > toUtc) throw new RequestValidationException("From date cannot be after to date.");
        var rows = await repository.GetAccountActivityAsync(fromUtc, toUtc, Scope(actor, branchId), cancellationToken);
        var mappings = await repository.GetAccountMappingLookupAsync(cancellationToken);
        var otherIncomeAccountId = mappings.GetValueOrDefault(AccountMappingKey.OtherIncomeDefault);
        var badDebtAccountId = mappings.GetValueOrDefault(AccountMappingKey.BadDebtExpense);
        var revenue = StatementRows(rows.Where(x => x.AccountType == AccountType.Income && x.ChartOfAccountId != otherIncomeAccountId));
        var otherIncome = StatementRows(rows.Where(x => x.AccountType == AccountType.Income && x.ChartOfAccountId == otherIncomeAccountId));
        var cost = StatementRows(rows.Where(x => x.AccountType == AccountType.CostOfSales));
        var expenses = StatementRows(rows.Where(x => x.AccountType == AccountType.Expense && x.ChartOfAccountId != badDebtAccountId));
        var otherExpenses = StatementRows(rows.Where(x => x.AccountType == AccountType.Expense && x.ChartOfAccountId == badDebtAccountId));
        var netRevenue = revenue.Sum(x => x.Amount);
        var totalCost = cost.Sum(x => x.Amount);
        var grossProfit = netRevenue - totalCost;
        var totalExpenses = expenses.Sum(x => x.Amount);
        var totalOtherIncome = otherIncome.Sum(x => x.Amount);
        var totalOtherExpenses = otherExpenses.Sum(x => x.Amount);
        var netProfit = grossProfit - totalExpenses + totalOtherIncome - totalOtherExpenses;
        return new ProfitAndLossDto(fromUtc, toUtc, revenue, netRevenue, cost, totalCost, grossProfit, expenses, totalExpenses, netProfit,
            otherIncome, totalOtherIncome, otherExpenses, totalOtherExpenses);
    }

    public async Task<BalanceSheetDto> GetBalanceSheetAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalView, cancellationToken);
        var rows = await repository.GetTrialBalanceAsync(asOfUtc, Scope(actor, branchId), cancellationToken);
        var assets = StatementRows(rows.Where(x => x.AccountType == AccountType.Asset));
        var liabilities = StatementRows(rows.Where(x => x.AccountType == AccountType.Liability));
        var equity = StatementRows(rows.Where(x => x.AccountType == AccountType.Equity));
        var earnings = StatementRows(rows.Where(x => x.AccountType is AccountType.Income or AccountType.CostOfSales or AccountType.Expense)).Sum(x =>
            x.Amount * (rows.Single(row => row.ChartOfAccountId == x.ChartOfAccountId).AccountType == AccountType.Income ? 1 : -1));
        var totalAssets = assets.Sum(x => x.Amount);
        var totalLiabilities = liabilities.Sum(x => x.Amount);
        var accountEquity = equity.Sum(x => x.Amount);
        var totalEquity = accountEquity + earnings;
        return new BalanceSheetDto(asOfUtc, assets, totalAssets, liabilities, totalLiabilities, equity, accountEquity,
            earnings, totalEquity, decimal.Round(totalAssets - totalLiabilities - totalEquity, 2) == 0);
    }

    public async Task<ArAgingSummaryDto> GetArAgingSummaryAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, Guid? customerId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsAgingReceivablesView, cancellationToken);
        var rows = await repository.GetArAgingSummaryAsync(asOfUtc, Scope(actor, branchId), customerId, cancellationToken);
        return new ArAgingSummaryDto(asOfUtc, rows,
            rows.Sum(x => x.Current), rows.Sum(x => x.Days1To30), rows.Sum(x => x.Days31To60),
            rows.Sum(x => x.Days61To90), rows.Sum(x => x.Over90), rows.Sum(x => x.Total));
    }

    public async Task<ArAgingDetailDto> GetArAgingDetailAsync(Guid actorId, Guid customerId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsAgingReceivablesView, cancellationToken);
        return await repository.GetArAgingDetailAsync(customerId, asOfUtc, Scope(actor, branchId), cancellationToken)
            ?? throw new ResourceNotFoundException("Customer was not found.");
    }

    public async Task<ApAgingSummaryDto> GetApAgingSummaryAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, Guid? supplierId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsAgingPayablesView, cancellationToken);
        var rows = await repository.GetApAgingSummaryAsync(asOfUtc, Scope(actor, branchId), supplierId, cancellationToken);
        return new ApAgingSummaryDto(asOfUtc, rows,
            rows.Sum(x => x.Current), rows.Sum(x => x.Days1To30), rows.Sum(x => x.Days31To60),
            rows.Sum(x => x.Days61To90), rows.Sum(x => x.Over90), rows.Sum(x => x.Total));
    }

    public async Task<ApAgingDetailDto> GetApAgingDetailAsync(Guid actorId, Guid supplierId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsAgingPayablesView, cancellationToken);
        return await repository.GetApAgingDetailAsync(supplierId, asOfUtc, Scope(actor, branchId), cancellationToken)
            ?? throw new ResourceNotFoundException("Supplier was not found.");
    }

    // ---- Journal reversal ----

    public async Task<JournalEntryDto> ReverseJournalEntryAsync(Guid actorId, Guid journalEntryId, ReverseJournalEntryRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalReverse, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("A reason is required to reverse a journal entry.");
        var original = await repository.GetJournalEntryAsync(journalEntryId, cancellationToken) ?? throw new ResourceNotFoundException("Journal entry was not found.");
        EnsureBranchAccess(actor, original.BranchId);
        if (original.SourceType is not (JournalSourceType.ManualVoucher or JournalSourceType.RecurringJournal))
            throw new RequestValidationException($"Journal entries posted by {original.SourceType} carry linked operational or subledger records that would go out of sync if reversed directly. Use the dedicated correction workflow for that document type, or reverse the originating voucher instead.");
        if (original.Lines.Any(l => l.CustomerId.HasValue || l.SupplierId.HasValue))
            throw new RequestValidationException("This journal entry affects a customer or supplier balance. Post a credit note, debit note, write-off, or balance adjustment instead so the customer/supplier ledger stays in sync.");
        if (await repository.JournalEntryHasReversalAsync(journalEntryId, cancellationToken))
            throw new RequestValidationException("This journal entry has already been reversed.");

        var reversalDate = request.ReversalDateUtc ?? UtcNow();
        JournalEntry? reversal = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            reversal = new JournalEntry
            {
                EntryNumber = await repository.NextJournalEntryNumberAsync(reversalDate, ct),
                EntryDateUtc = reversalDate,
                SourceType = JournalSourceType.JournalReversal,
                SourceId = original.Id,
                Reference = original.EntryNumber,
                Description = $"Reversal of {original.EntryNumber}: {request.Reason.Trim()}",
                BranchId = original.BranchId,
                PostedByUserId = actorId,
                PostedAtUtc = UtcNow(),
                Status = JournalEntryStatus.Posted,
                ReversesJournalEntryId = original.Id,
                ReversalReason = request.Reason.Trim(),
                Lines = original.Lines.Select(l => new JournalEntryLine
                {
                    ChartOfAccountId = l.ChartOfAccountId, Debit = l.Credit, Credit = l.Debit,
                    BranchId = l.BranchId, Description = l.Description, CostCenterId = l.CostCenterId
                }).ToList()
            };
            await repository.AddJournalEntryAsync(reversal, ct);
            await Audit(actorId, "JournalEntryReversed", "JournalEntry", original.Id, new { original.EntryNumber, ReversalEntryNumber = reversal.EntryNumber, request.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapJournalEntry(reversal!, cancellationToken);
    }

    // ---- Cost centers ----

    public async Task<IReadOnlyList<CostCenterDto>> ListCostCentersAsync(Guid actorId, bool includeInactive, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsCostCentersView, cancellationToken);
        var list = await repository.ListCostCentersAsync(includeInactive, cancellationToken);
        return list.Select(x => new CostCenterDto(x.Id, x.Code, x.Name, x.Description, x.IsActive)).ToList();
    }

    public async Task<CostCenterDto> CreateCostCenterAsync(Guid actorId, CostCenterRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsCostCentersManage, cancellationToken);
        ValidateCode(request.Code);
        ValidateName(request.Name);
        var normalized = Normalize(request.Code);
        if (await repository.GetCostCenterByNormalizedCodeAsync(normalized, cancellationToken) is not null)
            throw new ResourceConflictException("A cost center with this code already exists.");
        var costCenter = new CostCenter { Code = request.Code.Trim(), NormalizedCode = normalized, Name = request.Name.Trim(), Description = Clean(request.Description), IsActive = request.IsActive };
        await repository.AddCostCenterAsync(costCenter, cancellationToken);
        await Audit(actorId, "CostCenterCreated", "CostCenter", costCenter.Id, new { costCenter.Code, costCenter.Name }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return new CostCenterDto(costCenter.Id, costCenter.Code, costCenter.Name, costCenter.Description, costCenter.IsActive);
    }

    public async Task SetCostCenterActiveAsync(Guid actorId, Guid id, bool active, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.AccountsCostCentersManage, cancellationToken);
        var costCenter = await repository.GetCostCenterAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Cost center was not found.");
        costCenter.IsActive = active;
        costCenter.UpdatedAt = UtcNow();
        await Audit(actorId, active ? "CostCenterActivated" : "CostCenterDeactivated", "CostCenter", id, new { costCenter.Code }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    // ---- Enhanced Trial Balance (opening / period / closing movement) ----

    public async Task<TrialBalanceMovementDto> GetTrialBalanceMovementAsync(Guid actorId, DateTime fromUtc, DateTime asOfUtc, Guid? branchId, bool includeZeroBalances, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalView, cancellationToken);
        if (fromUtc > asOfUtc) throw new RequestValidationException("From date cannot be after the as-of date.");
        var scope = Scope(actor, branchId);
        var opening = await repository.GetTrialBalanceAsync(fromUtc.AddTicks(-1), scope, cancellationToken);
        var closing = await repository.GetTrialBalanceAsync(asOfUtc, scope, cancellationToken);
        var accountMap = (await repository.ListAccountsAsync(true, cancellationToken)).ToDictionary(x => x.Id);
        var openingMap = opening.ToDictionary(x => x.ChartOfAccountId);
        var closingMap = closing.ToDictionary(x => x.ChartOfAccountId);
        var ids = openingMap.Keys.Union(closingMap.Keys).ToList();
        var rows = ids.Select(id =>
        {
            var account = accountMap[id];
            var od = openingMap.GetValueOrDefault(id)?.Debit ?? 0;
            var oc = openingMap.GetValueOrDefault(id)?.Credit ?? 0;
            var cd = closingMap.GetValueOrDefault(id)?.Debit ?? 0;
            var cc = closingMap.GetValueOrDefault(id)?.Credit ?? 0;
            return new TrialBalanceMovementRowDto(id, account.Code, account.Name, account.ParentAccountId, account.AccountType, account.NormalBalance,
                od, oc, cd - od, cc - oc, cd, cc);
        })
        .Where(x => includeZeroBalances || x.ClosingDebit != 0 || x.ClosingCredit != 0 || x.PeriodDebit != 0 || x.PeriodCredit != 0)
        .OrderBy(x => x.AccountCode).ToList();
        var totalClosingDebit = rows.Sum(x => x.ClosingDebit);
        var totalClosingCredit = rows.Sum(x => x.ClosingCredit);
        return new TrialBalanceMovementDto(fromUtc, asOfUtc, rows, rows.Sum(x => x.OpeningDebit), rows.Sum(x => x.OpeningCredit),
            rows.Sum(x => x.PeriodDebit), rows.Sum(x => x.PeriodCredit), totalClosingDebit, totalClosingCredit,
            decimal.Round(totalClosingDebit, 2) == decimal.Round(totalClosingCredit, 2));
    }

    // ---- Cash Book / Bank Book / Day Book ----

    public async Task<CashBankBookDto> GetCashBookAsync(Guid actorId, CashBankBookQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalView, cancellationToken);
        return await repository.GetCashBankBookAsync(AccountMappingKey.Cash, query with { BranchId = Scope(actor, query.BranchId) }, cancellationToken);
    }

    public async Task<CashBankBookDto> GetBankBookAsync(Guid actorId, CashBankBookQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalView, cancellationToken);
        return await repository.GetCashBankBookAsync(AccountMappingKey.Bank, query with { BranchId = Scope(actor, query.BranchId) }, cancellationToken);
    }

    public async Task<DayBookDto> GetDayBookAsync(Guid actorId, DayBookQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalView, cancellationToken);
        return await repository.GetDayBookAsync(query with { BranchId = Scope(actor, query.BranchId) }, cancellationToken);
    }

    // ---- Cash flow statement (indirect method) ----

    public async Task<CashFlowStatementDto> GetCashFlowStatementAsync(Guid actorId, DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalView, cancellationToken);
        if (fromUtc > toUtc) throw new RequestValidationException("From date cannot be after to date.");
        var scope = Scope(actor, branchId);
        var pnl = await GetProfitAndLossAsync(actorId, fromUtc, toUtc, branchId, cancellationToken);
        var movements = await repository.GetNonCashBalanceMovementsAsync(fromUtc, toUtc, scope, cancellationToken);
        var accountMap = (await repository.ListAccountsAsync(true, cancellationToken)).ToDictionary(x => x.Id);
        var operatingAdj = new List<CashFlowLineDto>();
        var investing = new List<CashFlowLineDto>();
        var financing = new List<CashFlowLineDto>();
        foreach (var m in movements)
        {
            var delta = decimal.Round(m.ClosingBalance - m.OpeningBalance, 2);
            if (delta == 0) continue;
            var account = accountMap[m.ChartOfAccountId];
            var cashImpact = m.AccountType == AccountType.Asset ? -delta : delta;
            var classification = m.Classification ?? (m.AccountType == AccountType.Equity ? CashFlowClassification.Financing : CashFlowClassification.Operating);
            var bucket = classification switch { CashFlowClassification.Investing => investing, CashFlowClassification.Financing => financing, _ => operatingAdj };
            bucket.Add(new CashFlowLineDto(account.Name, cashImpact));
        }
        var netOperating = decimal.Round(pnl.NetProfit + operatingAdj.Sum(x => x.Amount), 2);
        var netInvesting = investing.Sum(x => x.Amount);
        var netFinancing = financing.Sum(x => x.Amount);
        var opening = await repository.GetCashAndBankBalanceAsync(fromUtc.AddTicks(-1), scope, cancellationToken);
        var closing = await repository.GetCashAndBankBalanceAsync(toUtc, scope, cancellationToken);
        return new CashFlowStatementDto(fromUtc, toUtc, pnl.NetProfit, operatingAdj, netOperating, investing, netInvesting, financing, netFinancing,
            decimal.Round(closing - opening, 2), opening, closing);
    }

    // ---- Control-account reconciliations ----

    public async Task<ControlReconciliationDto> GetArControlReconciliationAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsReconciliationView, cancellationToken);
        return await repository.GetArControlReconciliationAsync(asOfUtc, Scope(actor, branchId), cancellationToken);
    }

    public async Task<ControlReconciliationDto> GetApControlReconciliationAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsReconciliationView, cancellationToken);
        return await repository.GetApControlReconciliationAsync(asOfUtc, Scope(actor, branchId), cancellationToken);
    }

    public async Task<CashBankControlReconciliationDto> GetCashBankControlReconciliationAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsReconciliationView, cancellationToken);
        return await repository.GetCashBankControlReconciliationAsync(asOfUtc, Scope(actor, branchId), cancellationToken);
    }

    public async Task<InventoryReconciliationDto> GetInventoryReconciliationAsync(Guid actorId, DateTime asOfUtc, Guid? branchId, Guid? godownId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsReconciliationView, cancellationToken);
        var scope = Scope(actor, branchId);
        var valuation = await repository.GetInventoryValuationAsync(scope, godownId, cancellationToken);
        var glBalance = await repository.GetMappedAccountBalanceAsync(AccountMappingKey.Inventory, asOfUtc, scope, cancellationToken);
        return new InventoryReconciliationDto(asOfUtc, scope, godownId, decimal.Round(valuation, 2), decimal.Round(glBalance, 2), decimal.Round(valuation - glBalance, 2));
    }

    private static IReadOnlyList<FinancialStatementRowDto> StatementRows(IEnumerable<TrialBalanceRowDto> rows) =>
        rows.Select(x => new FinancialStatementRowDto(x.ChartOfAccountId, x.AccountCode, x.AccountName,
            x.AccountType is AccountType.Asset or AccountType.CostOfSales or AccountType.Expense
                ? x.Debit - x.Credit : x.Credit - x.Debit))
            .Where(x => x.Amount != 0).OrderBy(x => x.AccountCode).ToList();

    private async Task<ChartOfAccountDto> MapAccount(ChartOfAccount account, decimal balance, CancellationToken ct)
    {
        var parentName = account.ParentAccountId.HasValue
            ? (await repository.GetAccountAsync(account.ParentAccountId.Value, ct))?.Name
            : null;
        return new ChartOfAccountDto(account.Id, account.Code, account.Name, account.ParentAccountId, parentName,
            account.AccountType, account.NormalBalance, account.IsPostingAccount, account.IsActive, account.Description, balance);
    }

    private async Task<JournalEntryDto> MapJournalEntry(JournalEntry entry, CancellationToken ct)
    {
        var branch = await repository.GetBranchAsync(entry.BranchId, ct);
        var postedBy = await repository.GetActorAsync(entry.PostedByUserId, ct);
        var lineDtos = new List<JournalEntryLineDto>();
        foreach (var line in entry.Lines)
        {
            var account = line.ChartOfAccount ?? await repository.GetAccountAsync(line.ChartOfAccountId, ct);
            var customer = line.CustomerId.HasValue ? line.Customer ?? await repository.GetCustomerAsync(line.CustomerId.Value, ct) : null;
            var supplier = line.SupplierId.HasValue ? line.Supplier ?? await repository.GetSupplierAsync(line.SupplierId.Value, ct) : null;
            var costCenter = line.CostCenterId.HasValue ? line.CostCenter ?? await repository.GetCostCenterAsync(line.CostCenterId.Value, ct) : null;
            lineDtos.Add(new JournalEntryLineDto(line.Id, line.ChartOfAccountId, account?.Code ?? string.Empty, account?.Name ?? string.Empty,
                line.Debit, line.Credit, line.BranchId, customer?.Name, supplier?.Name, line.Description, line.CostCenterId, costCenter?.Name));
        }
        return new JournalEntryDto(entry.Id, entry.EntryNumber, entry.EntryDateUtc, entry.SourceType, entry.SourceId, entry.Reference,
            entry.Description, entry.BranchId, branch?.Name ?? string.Empty, postedBy?.FullName ?? string.Empty, entry.PostedAtUtc, entry.Status,
            lineDtos.Sum(x => x.Debit), lineDtos.Sum(x => x.Credit), lineDtos);
    }

    private async Task<ChartOfAccount> RequiredAccount(Guid id, CancellationToken ct) =>
        await repository.GetAccountAsync(id, ct) ?? throw new ResourceNotFoundException("Account was not found.");

    private async Task<User> Require(Guid actorId, string permission, CancellationToken ct)
    {
        var actor = await repository.GetActorAsync(actorId, ct);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private static Guid? Scope(User actor, Guid? requested)
    {
        if (CanSelectBranch(actor)) return requested;
        if (requested.HasValue && requested != actor.BranchId) throw new ForbiddenOperationException("The current user cannot access this branch.");
        return actor.BranchId;
    }

    private static void EnsureBranchAccess(User actor, Guid branchId)
    {
        if (!CanSelectBranch(actor) && actor.BranchId != branchId)
            throw new ForbiddenOperationException("The current user cannot access this branch.");
    }

    private static bool CanSelectBranch(User actor) => actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager;
    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private async Task Audit(Guid userId, string action, string entityType, Guid entityId, object values, CancellationToken ct) =>
        await repository.AddAuditAsync(new AuditLog { UserId = userId, Action = action, EntityType = entityType, EntityId = entityId, NewValues = JsonSerializer.Serialize(values) }, ct);
    private static void ValidateCode(string code) { if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 20) throw new RequestValidationException("Account code is required and must be 20 characters or fewer."); }
    private static void ValidateName(string name) { if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) throw new RequestValidationException("Account name is required and must be 200 characters or fewer."); }
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
