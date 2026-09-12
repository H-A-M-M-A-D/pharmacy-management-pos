using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed partial class AccountingRepository(PharmacyDbContext context) : IAccountingRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);
    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) => context.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default) => context.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default) => context.ChartOfAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<ChartOfAccount?> GetAccountByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) =>
        context.ChartOfAccounts.FirstOrDefaultAsync(x => x.NormalizedCode == normalizedCode, cancellationToken);
    public async Task<IReadOnlyList<ChartOfAccount>> ListAccountsAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        await context.ChartOfAccounts.AsNoTracking().Where(x => includeInactive || x.IsActive).OrderBy(x => x.Code).ToListAsync(cancellationToken);
    public async Task AddAccountAsync(ChartOfAccount account, CancellationToken cancellationToken = default) => await context.ChartOfAccounts.AddAsync(account, cancellationToken);

    public async Task<decimal> GetAccountBalanceAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await context.ChartOfAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == accountId, cancellationToken);
        if (account is null) return 0;
        var debit = await context.JournalEntryLines.AsNoTracking().Where(x => x.ChartOfAccountId == accountId).SumAsync(x => (decimal?)x.Debit, cancellationToken) ?? 0;
        var credit = await context.JournalEntryLines.AsNoTracking().Where(x => x.ChartOfAccountId == accountId).SumAsync(x => (decimal?)x.Credit, cancellationToken) ?? 0;
        return account.NormalBalance == NormalBalance.Debit ? debit - credit : credit - debit;
    }

    public async Task<IReadOnlyList<AccountMapping>> ListAccountMappingsAsync(CancellationToken cancellationToken = default) =>
        await context.AccountMappings.AsNoTracking().Include(x => x.ChartOfAccount).OrderBy(x => x.MappingKey).ToListAsync(cancellationToken);
    public async Task<Dictionary<AccountMappingKey, Guid>> GetAccountMappingLookupAsync(CancellationToken cancellationToken = default) =>
        await context.AccountMappings.AsNoTracking().ToDictionaryAsync(x => x.MappingKey, x => x.ChartOfAccountId, cancellationToken);
    public Task<AccountMapping?> GetAccountMappingAsync(AccountMappingKey key, CancellationToken cancellationToken = default) =>
        context.AccountMappings.FirstOrDefaultAsync(x => x.MappingKey == key, cancellationToken);
    public async Task AddAccountMappingAsync(AccountMapping mapping, CancellationToken cancellationToken = default) => await context.AccountMappings.AddAsync(mapping, cancellationToken);
    public Task<bool> IsAccountMappedAsync(Guid accountId, CancellationToken cancellationToken = default) => context.AccountMappings.AnyAsync(x => x.ChartOfAccountId == accountId, cancellationToken);

    public async Task<string> NextJournalEntryNumberAsync(DateTime entryDateUtc, CancellationToken cancellationToken = default)
    {
        var next = await context.Database.SqlQueryRaw<long>("SELECT nextval('\"JournalEntryNumberSequence\"'::regclass) AS \"Value\"").SingleAsync(cancellationToken);
        return $"JV-{entryDateUtc.Year}-{next:000000}";
    }

    public Task<bool> JournalEntryExistsForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default)
    {
        if (context.JournalEntries.Local.Any(x => x.SourceType == sourceType && x.SourceId == sourceId))
            return Task.FromResult(true);
        return context.JournalEntries.AnyAsync(x => x.SourceType == sourceType && x.SourceId == sourceId, cancellationToken);
    }

    public async Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default) => await context.JournalEntries.AddAsync(entry, cancellationToken);

    public Task<JournalEntry?> GetJournalEntryAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.JournalEntries.Include(x => x.Branch).Include(x => x.PostedByUser)
            .Include(x => x.Lines).ThenInclude(x => x.ChartOfAccount)
            .Include(x => x.Lines).ThenInclude(x => x.Customer)
            .Include(x => x.Lines).ThenInclude(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<PagedResult<JournalEntryListItemDto>> ListJournalEntriesAsync(JournalEntryListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var entries = context.JournalEntries.AsNoTracking().Include(x => x.Branch).Include(x => x.PostedByUser).Include(x => x.Lines).AsQueryable();
        if (!canSelectBranch && actorBranchId.HasValue) entries = entries.Where(x => x.BranchId == actorBranchId);
        if (query.BranchId.HasValue) entries = entries.Where(x => x.BranchId == query.BranchId);
        if (query.SourceType.HasValue) entries = entries.Where(x => x.SourceType == query.SourceType);
        if (query.FromUtc.HasValue) entries = entries.Where(x => x.EntryDateUtc >= query.FromUtc);
        if (query.ToUtc.HasValue) entries = entries.Where(x => x.EntryDateUtc <= query.ToUtc);
        if (query.ChartOfAccountId.HasValue) entries = entries.Where(x => x.Lines.Any(l => l.ChartOfAccountId == query.ChartOfAccountId));
        if (query.CostCenterId.HasValue) entries = entries.Where(x => x.Lines.Any(l => l.CostCenterId == query.CostCenterId));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            entries = entries.Where(x => x.EntryNumber.Contains(search) ||
                (x.Reference != null && x.Reference.Contains(search)) || x.Description.Contains(search));
        }
        var total = await entries.CountAsync(cancellationToken);
        var items = await entries.OrderByDescending(x => x.EntryDateUtc).ThenByDescending(x => x.EntryNumber)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new JournalEntryListItemDto(x.Id, x.EntryNumber, x.EntryDateUtc, x.SourceType, x.Reference, x.Description,
                x.BranchId, x.Branch!.Name, x.PostedByUser!.FullName, x.Lines.Sum(l => l.Debit)))
            .ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var lines = context.JournalEntryLines.AsNoTracking().Include(x => x.ChartOfAccount)
            .Where(x => x.JournalEntry!.EntryDateUtc <= asOfUtc);
        if (branchId.HasValue) lines = lines.Where(x => x.BranchId == branchId);
        var grouped = await lines.GroupBy(x => new { x.ChartOfAccountId, x.ChartOfAccount!.Code, x.ChartOfAccount.Name, x.ChartOfAccount.AccountType, x.ChartOfAccount.NormalBalance })
            .Select(g => new { g.Key.ChartOfAccountId, g.Key.Code, g.Key.Name, g.Key.AccountType, g.Key.NormalBalance, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .ToListAsync(cancellationToken);
        return grouped.Where(x => x.Debit != 0 || x.Credit != 0).OrderBy(x => x.Code)
            .Select(x => new TrialBalanceRowDto(x.ChartOfAccountId, x.Code, x.Name, x.AccountType, x.NormalBalance, x.Debit, x.Credit)).ToList();
    }

    public async Task<IReadOnlyList<TrialBalanceRowDto>> GetAccountActivityAsync(DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var lines = AccountActivityLines(fromUtc, toUtc, branchId);
        if (branchId.HasValue) lines = lines.Where(x => x.BranchId == branchId);
        var grouped = await lines.GroupBy(x => new { x.ChartOfAccountId, x.ChartOfAccount!.Code, x.ChartOfAccount.Name, x.ChartOfAccount.AccountType, x.ChartOfAccount.NormalBalance })
            .Select(g => new { g.Key.ChartOfAccountId, g.Key.Code, g.Key.Name, g.Key.AccountType, g.Key.NormalBalance, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .ToListAsync(cancellationToken);
        return grouped.Where(x => x.Debit != 0 || x.Credit != 0).OrderBy(x => x.Code)
            .Select(x => new TrialBalanceRowDto(x.ChartOfAccountId, x.Code, x.Name, x.AccountType, x.NormalBalance, x.Debit, x.Credit)).ToList();
    }

    public async Task<GeneralLedgerDto> GetGeneralLedgerAsync(GeneralLedgerQuery query, CancellationToken cancellationToken = default)
    {
        var account = await context.ChartOfAccounts.AsNoTracking().SingleAsync(x => x.Id == query.ChartOfAccountId, cancellationToken);
        var baseLines = context.JournalEntryLines.AsNoTracking()
            .Where(x => x.ChartOfAccountId == query.ChartOfAccountId);
        if (query.BranchId.HasValue) baseLines = baseLines.Where(x => x.BranchId == query.BranchId);

        var openingDebit = query.FromUtc.HasValue
            ? await baseLines.Where(x => x.JournalEntry!.EntryDateUtc < query.FromUtc.Value).SumAsync(x => (decimal?)x.Debit, cancellationToken) ?? 0
            : 0;
        var openingCredit = query.FromUtc.HasValue
            ? await baseLines.Where(x => x.JournalEntry!.EntryDateUtc < query.FromUtc.Value).SumAsync(x => (decimal?)x.Credit, cancellationToken) ?? 0
            : 0;
        var opening = Balance(account.NormalBalance, openingDebit, openingCredit);

        var period = baseLines;
        if (query.FromUtc.HasValue) period = period.Where(x => x.JournalEntry!.EntryDateUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) period = period.Where(x => x.JournalEntry!.EntryDateUtc <= query.ToUtc.Value);
        if (query.SourceType.HasValue) period = period.Where(x => x.JournalEntry!.SourceType == query.SourceType.Value);
        var total = await period.CountAsync(cancellationToken);
        var totalDebit = await period.SumAsync(x => (decimal?)x.Debit, cancellationToken) ?? 0;
        var totalCredit = await period.SumAsync(x => (decimal?)x.Credit, cancellationToken) ?? 0;
        var skip = (query.Page - 1) * query.PageSize;
        var ordered = period.OrderBy(x => x.JournalEntry!.EntryDateUtc).ThenBy(x => x.JournalEntry!.EntryNumber).ThenBy(x => x.Id);
        var priorRows = await ordered.Take(skip).Select(x => new { x.Debit, x.Credit }).ToListAsync(cancellationToken);
        var running = opening + priorRows.Sum(x => Delta(account.NormalBalance, x.Debit, x.Credit));
        var pageRows = await ordered.Skip(skip).Take(query.PageSize).Select(x => new
        {
            x.JournalEntryId, x.JournalEntry!.EntryNumber, x.JournalEntry.EntryDateUtc, x.JournalEntry.SourceType,
            x.JournalEntry.Reference, JournalDescription = x.JournalEntry.Description, LineDescription = x.Description, x.Debit, x.Credit
        }).ToListAsync(cancellationToken);
        var items = pageRows.Select(x =>
        {
            running += Delta(account.NormalBalance, x.Debit, x.Credit);
            return new GeneralLedgerLineDto(x.JournalEntryId, x.EntryNumber, x.EntryDateUtc, x.SourceType, x.Reference,
                x.LineDescription ?? x.JournalDescription, x.Debit, x.Credit, running);
        }).ToList();
        return new GeneralLedgerDto(account.Id, account.Code, account.Name, account.NormalBalance, opening, totalDebit, totalCredit,
            opening + Delta(account.NormalBalance, totalDebit, totalCredit), new PagedResult<GeneralLedgerLineDto>(items, query.Page, query.PageSize, total));
    }

    private static decimal Balance(NormalBalance normalBalance, decimal debit, decimal credit) =>
        normalBalance == NormalBalance.Debit ? debit - credit : credit - debit;
    private static decimal Delta(NormalBalance normalBalance, decimal debit, decimal credit) =>
        normalBalance == NormalBalance.Debit ? debit - credit : credit - debit;

    public async Task<IReadOnlyList<ArAgingSummaryRowDto>> GetArAgingSummaryAsync(DateTime asOfUtc, Guid? branchId, Guid? customerId, CancellationToken cancellationToken = default)
    {
        var rows = await ComputeArAgingRowsAsync(asOfUtc, branchId, customerId, cancellationToken);
        return rows.GroupBy(x => x.CustomerId).Select(g =>
        {
            var first = g.First();
            decimal Sum(AgingBucket b) => g.Where(x => x.Row.Bucket == b).Sum(x => x.Row.Outstanding);
            return new ArAgingSummaryRowDto(g.Key, first.CustomerCode, first.CustomerName,
                Sum(AgingBucket.Current), Sum(AgingBucket.Days1To30), Sum(AgingBucket.Days31To60),
                Sum(AgingBucket.Days61To90), Sum(AgingBucket.Over90), g.Sum(x => x.Row.Outstanding));
        }).Where(x => x.Total != 0).OrderBy(x => x.CustomerName).ToList();
    }

    public async Task<ArAgingDetailDto?> GetArAgingDetailAsync(Guid customerId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var customer = await context.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        if (customer is null) return null;
        var rows = await ComputeArAgingRowsAsync(asOfUtc, branchId, customerId, cancellationToken);
        var detailRows = rows.Select(x => x.Row).OrderBy(x => x.DueDateUtc ?? DateTime.MaxValue).ThenBy(x => x.DocumentDate).ToList();
        return new ArAgingDetailDto(asOfUtc, customerId, customer.CustomerCode, customer.Name, detailRows, detailRows.Sum(x => x.Outstanding));
    }

    private async Task<List<(Guid CustomerId, string CustomerCode, string CustomerName, ArAgingDetailRowDto Row)>> ComputeArAgingRowsAsync(
        DateTime asOfUtc, Guid? branchId, Guid? customerId, CancellationToken cancellationToken)
    {
        var asOfDate = DateOnly.FromDateTime(asOfUtc);
        var sales = await context.Sales.AsNoTracking()
            .Where(s => s.CustomerId != null && s.Status == SaleStatus.Posted && s.CreditAmount > 0 && s.PostedAtUtc <= asOfUtc)
            .Where(s => !branchId.HasValue || s.BranchId == branchId)
            .Where(s => !customerId.HasValue || s.CustomerId == customerId)
            .Select(s => new { s.Id, s.CustomerId, s.BranchId, s.InvoiceNumber, s.PostedAtUtc, s.DueDateUtc, s.CreditAmount })
            .ToListAsync(cancellationToken);
        var saleIds = sales.Select(s => s.Id).ToList();
        var allocatedMap = saleIds.Count == 0 ? new Dictionary<Guid, decimal>() : (await context.CustomerPaymentAllocations.AsNoTracking()
            .Where(a => saleIds.Contains(a.SaleId) && a.AllocatedAtUtc <= asOfUtc)
            .GroupBy(a => a.SaleId).Select(g => new { SaleId = g.Key, Total = g.Sum(x => x.AllocatedAmount) })
            .ToListAsync(cancellationToken)).ToDictionary(x => x.SaleId, x => x.Total);
        var returnMap = saleIds.Count == 0 ? new Dictionary<Guid, decimal>() : (await context.SalesReturns.AsNoTracking()
            .Where(r => saleIds.Contains(r.OriginalSaleId) && r.PostedAtUtc <= asOfUtc)
            .GroupBy(r => r.OriginalSaleId).Select(g => new { SaleId = g.Key, Total = g.Sum(x => x.CustomerCreditReductionAmount) })
            .ToListAsync(cancellationToken)).ToDictionary(x => x.SaleId, x => x.Total);

        var result = new List<(Guid CustomerId, string CustomerCode, string CustomerName, ArAgingDetailRowDto Row)>();
        foreach (var s in sales)
        {
            var settled = allocatedMap.GetValueOrDefault(s.Id) + returnMap.GetValueOrDefault(s.Id);
            var outstanding = decimal.Round(s.CreditAmount - settled, 2);
            if (outstanding <= 0) continue;
            var documentDate = DateOnly.FromDateTime(s.PostedAtUtc!.Value);
            var dueDate = s.DueDateUtc ?? s.PostedAtUtc.Value;
            var daysOverdue = asOfDate.DayNumber - DateOnly.FromDateTime(dueDate).DayNumber;
            var row = new ArAgingDetailRowDto(s.Id, s.InvoiceNumber, documentDate, s.DueDateUtc, s.CreditAmount, settled, outstanding, daysOverdue, AgingBucketExtensions.BucketFor(daysOverdue), false);
            result.Add((s.CustomerId!.Value, string.Empty, string.Empty, row));
        }

        var openings = await context.CustomerLedgerEntries.AsNoTracking()
            .Where(e => e.EntryType == CustomerLedgerEntryType.OpeningBalance && e.Amount > 0 && e.EntryDate <= asOfDate)
            .Where(e => !branchId.HasValue || e.BranchId == branchId)
            .Where(e => !customerId.HasValue || e.CustomerId == customerId)
            .Select(e => new { e.Id, e.CustomerId, e.EntryDate, e.Amount })
            .ToListAsync(cancellationToken);
        foreach (var o in openings)
        {
            var daysOverdue = asOfDate.DayNumber - o.EntryDate.DayNumber;
            var row = new ArAgingDetailRowDto(o.Id, "Opening Balance", o.EntryDate, o.EntryDate.ToDateTime(TimeOnly.MinValue), o.Amount, 0, o.Amount, daysOverdue, AgingBucketExtensions.BucketFor(daysOverdue), true);
            result.Add((o.CustomerId, string.Empty, string.Empty, row));
        }

        if (result.Count == 0) return [];
        var allCustomerIds = result.Select(x => x.CustomerId).Distinct().ToList();
        var customerMap = (await context.Customers.AsNoTracking().Where(c => allCustomerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.CustomerCode, c.Name }).ToListAsync(cancellationToken)).ToDictionary(c => c.Id);
        return result.Select(x => (x.CustomerId, customerMap[x.CustomerId].CustomerCode, customerMap[x.CustomerId].Name, x.Row)).ToList();
    }

    public async Task<IReadOnlyList<ApAgingSummaryRowDto>> GetApAgingSummaryAsync(DateTime asOfUtc, Guid? branchId, Guid? supplierId, CancellationToken cancellationToken = default)
    {
        var rows = await ComputeApAgingRowsAsync(asOfUtc, branchId, supplierId, cancellationToken);
        return rows.GroupBy(x => x.SupplierId).Select(g =>
        {
            var first = g.First();
            decimal Sum(AgingBucket b) => g.Where(x => x.Row.Bucket == b).Sum(x => x.Row.Outstanding);
            return new ApAgingSummaryRowDto(g.Key, first.SupplierName,
                Sum(AgingBucket.Current), Sum(AgingBucket.Days1To30), Sum(AgingBucket.Days31To60),
                Sum(AgingBucket.Days61To90), Sum(AgingBucket.Over90), g.Sum(x => x.Row.Outstanding));
        }).Where(x => x.Total != 0).OrderBy(x => x.SupplierName).ToList();
    }

    public async Task<ApAgingDetailDto?> GetApAgingDetailAsync(Guid supplierId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var supplier = await context.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == supplierId, cancellationToken);
        if (supplier is null) return null;
        var rows = await ComputeApAgingRowsAsync(asOfUtc, branchId, supplierId, cancellationToken);
        var detailRows = rows.Select(x => x.Row).OrderBy(x => x.DueDate ?? DateOnly.MaxValue).ThenBy(x => x.DocumentDate).ToList();
        return new ApAgingDetailDto(asOfUtc, supplierId, supplier.Name, detailRows, detailRows.Sum(x => x.Outstanding));
    }

    private async Task<List<(Guid SupplierId, string SupplierName, ApAgingDetailRowDto Row)>> ComputeApAgingRowsAsync(
        DateTime asOfUtc, Guid? branchId, Guid? supplierId, CancellationToken cancellationToken)
    {
        var asOfDate = DateOnly.FromDateTime(asOfUtc);
        var receipts = await context.GoodsReceipts.AsNoTracking()
            .Where(r => r.Status == GoodsReceiptStatus.Posted && r.NetTotal > 0 && r.ReceiptDate <= asOfDate)
            .Where(r => !branchId.HasValue || r.BranchId == branchId)
            .Where(r => !supplierId.HasValue || r.SupplierId == supplierId)
            .Select(r => new { r.Id, r.SupplierId, r.BranchId, r.GrnNumber, r.ReceiptDate, r.DueDate, r.NetTotal })
            .ToListAsync(cancellationToken);
        var receiptIds = receipts.Select(r => r.Id).ToList();
        var allocatedMap = receiptIds.Count == 0 ? new Dictionary<Guid, decimal>() : (await context.SupplierPaymentAllocations.AsNoTracking()
            .Where(a => receiptIds.Contains(a.GoodsReceiptId) && a.AllocatedAtUtc <= asOfUtc)
            .GroupBy(a => a.GoodsReceiptId).Select(g => new { GoodsReceiptId = g.Key, Total = g.Sum(x => x.AllocatedAmount) })
            .ToListAsync(cancellationToken)).ToDictionary(x => x.GoodsReceiptId, x => x.Total);
        var returnMap = receiptIds.Count == 0 ? new Dictionary<Guid, decimal>() : (await context.PurchaseReturns.AsNoTracking()
            .Where(r => receiptIds.Contains(r.OriginalGoodsReceiptId) && r.PostedAtUtc <= asOfUtc)
            .GroupBy(r => r.OriginalGoodsReceiptId).Select(g => new { GoodsReceiptId = g.Key, Total = g.Sum(x => x.NetSupplierCredit) })
            .ToListAsync(cancellationToken)).ToDictionary(x => x.GoodsReceiptId, x => x.Total);

        var result = new List<(Guid SupplierId, string SupplierName, ApAgingDetailRowDto Row)>();
        foreach (var r in receipts)
        {
            var settled = allocatedMap.GetValueOrDefault(r.Id) + returnMap.GetValueOrDefault(r.Id);
            var outstanding = decimal.Round(r.NetTotal - settled, 2);
            if (outstanding <= 0) continue;
            var dueDate = r.DueDate ?? r.ReceiptDate;
            var daysOverdue = asOfDate.DayNumber - dueDate.DayNumber;
            var row = new ApAgingDetailRowDto(r.Id, r.GrnNumber, r.ReceiptDate, r.DueDate, r.NetTotal, settled, outstanding, daysOverdue, AgingBucketExtensions.BucketFor(daysOverdue), false);
            result.Add((r.SupplierId, string.Empty, row));
        }

        var openings = await context.SupplierLedgerEntries.AsNoTracking()
            .Where(e => e.EntryType == SupplierLedgerEntryType.OpeningBalance && e.Amount > 0 && e.EntryDate <= asOfDate)
            .Where(e => !branchId.HasValue || e.BranchId == branchId)
            .Where(e => !supplierId.HasValue || e.SupplierId == supplierId)
            .Select(e => new { e.Id, e.SupplierId, e.EntryDate, e.Amount })
            .ToListAsync(cancellationToken);
        foreach (var o in openings)
        {
            var daysOverdue = asOfDate.DayNumber - o.EntryDate.DayNumber;
            var row = new ApAgingDetailRowDto(o.Id, "Opening Balance", o.EntryDate, o.EntryDate, o.Amount, 0, o.Amount, daysOverdue, AgingBucketExtensions.BucketFor(daysOverdue), true);
            result.Add((o.SupplierId, string.Empty, row));
        }

        if (result.Count == 0) return [];
        var allSupplierIds = result.Select(x => x.SupplierId).Distinct().ToList();
        var supplierMap = (await context.Suppliers.AsNoTracking().Where(s => allSupplierIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name }).ToListAsync(cancellationToken)).ToDictionary(s => s.Id);
        return result.Select(x => (x.SupplierId, supplierMap[x.SupplierId].Name, x.Row)).ToList();
    }

    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ResourceConflictException("An accounting record with the same unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Accounting constraints were violated.");
        }
    }

    // ---- Journal reversal ----

    public Task<bool> JournalEntryHasReversalAsync(Guid journalEntryId, CancellationToken cancellationToken = default) =>
        context.JournalEntries.AnyAsync(x => x.ReversesJournalEntryId == journalEntryId, cancellationToken);

    public Task<bool> JournalEntryLinkedToVoucherAsync(Guid journalEntryId, CancellationToken cancellationToken = default) =>
        context.Vouchers.AnyAsync(x => x.JournalEntryId == journalEntryId, cancellationToken);

    public Task<AccountingPeriod?> GetCoveringPeriodAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        context.AccountingPeriods.AsNoTracking().FirstOrDefaultAsync(x => x.StartDate <= date && x.EndDate >= date, cancellationToken);

    public void AllowPostingIntoSoftClosedPeriod() => context.AllowPostingIntoSoftClosedPeriod = true;

    // ---- Cost centers ----

    public async Task<IReadOnlyList<CostCenter>> ListCostCentersAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        await context.CostCenters.AsNoTracking().Where(x => includeInactive || x.IsActive).OrderBy(x => x.Code).ToListAsync(cancellationToken);
    public Task<CostCenter?> GetCostCenterAsync(Guid id, CancellationToken cancellationToken = default) => context.CostCenters.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<CostCenter?> GetCostCenterByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) =>
        context.CostCenters.FirstOrDefaultAsync(x => x.NormalizedCode == normalizedCode, cancellationToken);
    public async Task AddCostCenterAsync(CostCenter costCenter, CancellationToken cancellationToken = default) => await context.CostCenters.AddAsync(costCenter, cancellationToken);

    // ---- Cash Book / Bank Book / Day Book ----

    public async Task<CashBankBookDto> GetCashBankBookAsync(AccountMappingKey mappingKey, CashBankBookQuery query, CancellationToken cancellationToken = default)
    {
        var mapping = await context.AccountMappings.AsNoTracking().FirstOrDefaultAsync(x => x.MappingKey == mappingKey, cancellationToken);
        if (mapping is null) return new CashBankBookDto(query.FromUtc, query.ToUtc, 0, 0, 0, 0, new PagedResult<CashBankBookLineDto>([], query.Page, query.PageSize, 0));
        var account = await context.ChartOfAccounts.AsNoTracking().SingleAsync(x => x.Id == mapping.ChartOfAccountId, cancellationToken);
        var baseLines = context.JournalEntryLines.AsNoTracking().Where(x => x.ChartOfAccountId == mapping.ChartOfAccountId);
        if (query.BranchId.HasValue) baseLines = baseLines.Where(x => x.BranchId == query.BranchId);

        var opening = query.FromUtc.HasValue
            ? Balance(account.NormalBalance,
                await baseLines.Where(x => x.JournalEntry!.EntryDateUtc < query.FromUtc.Value).SumAsync(x => (decimal?)x.Debit, cancellationToken) ?? 0,
                await baseLines.Where(x => x.JournalEntry!.EntryDateUtc < query.FromUtc.Value).SumAsync(x => (decimal?)x.Credit, cancellationToken) ?? 0)
            : 0;

        var period = baseLines;
        if (query.FromUtc.HasValue) period = period.Where(x => x.JournalEntry!.EntryDateUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) period = period.Where(x => x.JournalEntry!.EntryDateUtc <= query.ToUtc.Value);
        var total = await period.CountAsync(cancellationToken);
        var totalDebit = await period.SumAsync(x => (decimal?)x.Debit, cancellationToken) ?? 0;
        var totalCredit = await period.SumAsync(x => (decimal?)x.Credit, cancellationToken) ?? 0;
        var skip = (query.Page - 1) * query.PageSize;
        var ordered = period.OrderBy(x => x.JournalEntry!.EntryDateUtc).ThenBy(x => x.JournalEntry!.EntryNumber).ThenBy(x => x.Id);
        var priorRows = await ordered.Take(skip).Select(x => new { x.Debit, x.Credit }).ToListAsync(cancellationToken);
        var running = opening + priorRows.Sum(x => Delta(account.NormalBalance, x.Debit, x.Credit));
        var pageRows = await ordered.Skip(skip).Take(query.PageSize).Select(x => new
        {
            x.JournalEntryId, x.JournalEntry!.EntryNumber, x.JournalEntry.EntryDateUtc, x.JournalEntry.SourceType,
            x.JournalEntry.Reference, JournalDescription = x.JournalEntry.Description, LineDescription = x.Description,
            x.Debit, x.Credit, PostedBy = x.JournalEntry.PostedByUser!.FullName
        }).ToListAsync(cancellationToken);
        var items = pageRows.Select(x =>
        {
            running += Delta(account.NormalBalance, x.Debit, x.Credit);
            return new CashBankBookLineDto(x.EntryDateUtc, x.Reference ?? x.EntryNumber, x.LineDescription ?? x.JournalDescription,
                x.Debit, x.Credit, running, x.SourceType, x.PostedBy, x.JournalEntryId, x.EntryNumber);
        }).ToList();
        return new CashBankBookDto(query.FromUtc, query.ToUtc, opening, totalDebit, totalCredit,
            opening + Delta(account.NormalBalance, totalDebit, totalCredit), new PagedResult<CashBankBookLineDto>(items, query.Page, query.PageSize, total));
    }

    public async Task<DayBookDto> GetDayBookAsync(DayBookQuery query, CancellationToken cancellationToken = default)
    {
        var entries = context.JournalEntries.AsNoTracking().Include(x => x.Branch).Include(x => x.PostedByUser).AsQueryable();
        if (query.BranchId.HasValue) entries = entries.Where(x => x.BranchId == query.BranchId);
        if (query.FromUtc.HasValue) entries = entries.Where(x => x.EntryDateUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) entries = entries.Where(x => x.EntryDateUtc <= query.ToUtc.Value);
        var total = await entries.CountAsync(cancellationToken);
        var totalDebit = await entries.SelectMany(x => x.Lines).SumAsync(x => (decimal?)x.Debit, cancellationToken) ?? 0;
        var totalCredit = await entries.SelectMany(x => x.Lines).SumAsync(x => (decimal?)x.Credit, cancellationToken) ?? 0;
        var items = await entries.OrderByDescending(x => x.EntryDateUtc).ThenByDescending(x => x.EntryNumber)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new DayBookLineDto(x.EntryDateUtc, x.EntryNumber, x.SourceType, x.Reference, x.Description,
                x.Lines.Sum(l => l.Debit), x.Lines.Sum(l => l.Credit), x.PostedByUser!.FullName, x.BranchId, x.Branch!.Name, x.Id))
            .ToListAsync(cancellationToken);
        return new DayBookDto(query.FromUtc, query.ToUtc, totalDebit, totalCredit, new PagedResult<DayBookLineDto>(items, query.Page, query.PageSize, total));
    }

    // ---- Cash flow statement ----

    public async Task<decimal> GetCashAndBankBalanceAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var mappedIds = await context.AccountMappings.AsNoTracking()
            .Where(x => x.MappingKey == AccountMappingKey.Cash || x.MappingKey == AccountMappingKey.Bank)
            .Select(x => x.ChartOfAccountId).ToListAsync(cancellationToken);
        if (mappedIds.Count == 0) return 0;
        var lines = context.JournalEntryLines.AsNoTracking().Where(x => mappedIds.Contains(x.ChartOfAccountId) && x.JournalEntry!.EntryDateUtc <= asOfUtc);
        if (branchId.HasValue) lines = lines.Where(x => x.BranchId == branchId);
        var debit = await lines.SumAsync(x => (decimal?)x.Debit, cancellationToken) ?? 0;
        var credit = await lines.SumAsync(x => (decimal?)x.Credit, cancellationToken) ?? 0;
        return debit - credit;
    }

    public async Task<IReadOnlyList<(Guid ChartOfAccountId, AccountType AccountType, CashFlowClassification? Classification, decimal OpeningBalance, decimal ClosingBalance)>>
        GetNonCashBalanceMovementsAsync(DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var cashBankIds = await context.AccountMappings.AsNoTracking()
            .Where(x => x.MappingKey == AccountMappingKey.Cash || x.MappingKey == AccountMappingKey.Bank)
            .Select(x => x.ChartOfAccountId).ToListAsync(cancellationToken);
        var accounts = await context.ChartOfAccounts.AsNoTracking()
            .Where(x => x.AccountType == AccountType.Asset || x.AccountType == AccountType.Liability || x.AccountType == AccountType.Equity)
            .Where(x => !cashBankIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var result = new List<(Guid, AccountType, CashFlowClassification?, decimal, decimal)>();
        foreach (var account in accounts)
        {
            var lines = context.JournalEntryLines.AsNoTracking().Where(x => x.ChartOfAccountId == account.Id);
            if (branchId.HasValue) lines = lines.Where(x => x.BranchId == branchId);
            var openingDebit = await lines.Where(x => x.JournalEntry!.EntryDateUtc < fromUtc).SumAsync(x => (decimal?)x.Debit, cancellationToken) ?? 0;
            var openingCredit = await lines.Where(x => x.JournalEntry!.EntryDateUtc < fromUtc).SumAsync(x => (decimal?)x.Credit, cancellationToken) ?? 0;
            var closingDebit = await lines.Where(x => x.JournalEntry!.EntryDateUtc <= toUtc).SumAsync(x => (decimal?)x.Debit, cancellationToken) ?? 0;
            var closingCredit = await lines.Where(x => x.JournalEntry!.EntryDateUtc <= toUtc).SumAsync(x => (decimal?)x.Credit, cancellationToken) ?? 0;
            if (openingDebit == 0 && openingCredit == 0 && closingDebit == 0 && closingCredit == 0) continue;
            var opening = Balance(account.NormalBalance, openingDebit, openingCredit);
            var closing = Balance(account.NormalBalance, closingDebit, closingCredit);
            if (opening == 0 && closing == 0) continue;
            result.Add((account.Id, account.AccountType, account.CashFlowClassification, opening, closing));
        }
        return result;
    }

    // ---- Control-account reconciliations ----

    public async Task<ControlReconciliationDto> GetArControlReconciliationAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var asOfDate = DateOnly.FromDateTime(asOfUtc);
        var subledgerQuery = context.CustomerLedgerEntries.AsNoTracking().Where(x => x.EntryDate <= asOfDate);
        if (branchId.HasValue) subledgerQuery = subledgerQuery.Where(x => x.BranchId == branchId);
        var subledger = await subledgerQuery.GroupBy(x => x.CustomerId)
            .Select(g => new { CustomerId = g.Key, Balance = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.CustomerId, x => x.Balance, cancellationToken);

        var arAccountId = await context.AccountMappings.AsNoTracking()
            .Where(x => x.MappingKey == AccountMappingKey.AccountsReceivable).Select(x => (Guid?)x.ChartOfAccountId).FirstOrDefaultAsync(cancellationToken);
        var glQuery = context.JournalEntryLines.AsNoTracking()
            .Where(x => x.ChartOfAccountId == arAccountId && x.CustomerId != null && x.JournalEntry!.EntryDateUtc <= asOfUtc);
        if (branchId.HasValue) glQuery = glQuery.Where(x => x.BranchId == branchId);
        var gl = await glQuery.GroupBy(x => x.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Balance = g.Sum(x => x.Debit) - g.Sum(x => x.Credit) }).ToDictionaryAsync(x => x.CustomerId, x => x.Balance, cancellationToken);

        var allIds = subledger.Keys.Union(gl.Keys).ToList();
        if (allIds.Count == 0) return new ControlReconciliationDto(asOfUtc, 0, 0, 0, []);
        var names = await context.Customers.AsNoTracking().Where(c => allIds.Contains(c.Id)).Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var rows = allIds.Select(id =>
        {
            var sub = subledger.GetValueOrDefault(id);
            var glBal = gl.GetValueOrDefault(id);
            return new ControlReconciliationRowDto(id, names.GetValueOrDefault(id, "Unknown"), sub, glBal, decimal.Round(sub - glBal, 2));
        }).Where(x => x.Difference != 0).OrderByDescending(x => Math.Abs(x.Difference)).ToList();
        return new ControlReconciliationDto(asOfUtc, subledger.Values.Sum(), gl.Values.Sum(), decimal.Round(subledger.Values.Sum() - gl.Values.Sum(), 2), rows);
    }

    public async Task<ControlReconciliationDto> GetApControlReconciliationAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var asOfDate = DateOnly.FromDateTime(asOfUtc);
        var subledgerQuery = context.SupplierLedgerEntries.AsNoTracking().Where(x => x.EntryDate <= asOfDate);
        if (branchId.HasValue) subledgerQuery = subledgerQuery.Where(x => x.BranchId == branchId);
        var subledger = await subledgerQuery.GroupBy(x => x.SupplierId)
            .Select(g => new { SupplierId = g.Key, Balance = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.SupplierId, x => x.Balance, cancellationToken);

        var apAccountId = await context.AccountMappings.AsNoTracking()
            .Where(x => x.MappingKey == AccountMappingKey.AccountsPayable).Select(x => (Guid?)x.ChartOfAccountId).FirstOrDefaultAsync(cancellationToken);
        var glQuery = context.JournalEntryLines.AsNoTracking()
            .Where(x => x.ChartOfAccountId == apAccountId && x.SupplierId != null && x.JournalEntry!.EntryDateUtc <= asOfUtc);
        if (branchId.HasValue) glQuery = glQuery.Where(x => x.BranchId == branchId);
        var gl = await glQuery.GroupBy(x => x.SupplierId!.Value)
            .Select(g => new { SupplierId = g.Key, Balance = g.Sum(x => x.Credit) - g.Sum(x => x.Debit) }).ToDictionaryAsync(x => x.SupplierId, x => x.Balance, cancellationToken);

        var allIds = subledger.Keys.Union(gl.Keys).ToList();
        if (allIds.Count == 0) return new ControlReconciliationDto(asOfUtc, 0, 0, 0, []);
        var names = await context.Suppliers.AsNoTracking().Where(s => allIds.Contains(s.Id)).Select(s => new { s.Id, s.Name }).ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
        var rows = allIds.Select(id =>
        {
            var sub = subledger.GetValueOrDefault(id);
            var glBal = gl.GetValueOrDefault(id);
            return new ControlReconciliationRowDto(id, names.GetValueOrDefault(id, "Unknown"), sub, glBal, decimal.Round(sub - glBal, 2));
        }).Where(x => x.Difference != 0).OrderByDescending(x => Math.Abs(x.Difference)).ToList();
        return new ControlReconciliationDto(asOfUtc, subledger.Values.Sum(), gl.Values.Sum(), decimal.Round(subledger.Values.Sum() - gl.Values.Sum(), 2), rows);
    }

    public async Task<CashBankControlReconciliationDto> GetCashBankControlReconciliationAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var accountsQuery = context.FinancialAccounts.AsNoTracking().Where(x => x.IsActive);
        if (branchId.HasValue) accountsQuery = accountsQuery.Where(x => x.BranchId == branchId);
        var accounts = await accountsQuery.ToListAsync(cancellationToken);
        var rows = new List<CashBankControlRowDto>();
        foreach (var account in accounts)
        {
            var operational = (await context.FinancialLedgerEntries.AsNoTracking()
                .Where(x => x.FinancialAccountId == account.Id && x.OccurredAtUtc <= asOfUtc)
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0);
            rows.Add(new CashBankControlRowDto(account.Id, account.Name, account.AccountType, decimal.Round(operational, 2)));
        }
        var cashTotal = rows.Where(x => x.AccountType == FinancialAccountType.Cash).Sum(x => x.OperationalBalance);
        var bankTotal = rows.Where(x => x.AccountType != FinancialAccountType.Cash).Sum(x => x.OperationalBalance);
        var glCash = await GetMappedAccountBalanceAsync(AccountMappingKey.Cash, asOfUtc, branchId, cancellationToken);
        var glBank = await GetMappedAccountBalanceAsync(AccountMappingKey.Bank, asOfUtc, branchId, cancellationToken);
        return new CashBankControlReconciliationDto(asOfUtc, cashTotal, glCash, decimal.Round(cashTotal - glCash, 2),
            bankTotal, glBank, decimal.Round(bankTotal - glBank, 2), rows.OrderBy(x => x.FinancialAccountName).ToList());
    }

    public async Task<decimal> GetMappedAccountBalanceAsync(AccountMappingKey key, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var accountId = await context.AccountMappings.AsNoTracking().Where(x => x.MappingKey == key).Select(x => (Guid?)x.ChartOfAccountId).FirstOrDefaultAsync(cancellationToken);
        if (accountId is null) return 0;
        var lines = context.JournalEntryLines.AsNoTracking().Where(x => x.ChartOfAccountId == accountId && x.JournalEntry!.EntryDateUtc <= asOfUtc);
        if (branchId.HasValue) lines = lines.Where(x => x.BranchId == branchId);
        var debit = await lines.SumAsync(x => (decimal?)x.Debit, cancellationToken) ?? 0;
        var credit = await lines.SumAsync(x => (decimal?)x.Credit, cancellationToken) ?? 0;
        return debit - credit;
    }

    public async Task<decimal> GetInventoryValuationAsync(Guid? branchId, Guid? godownId, CancellationToken cancellationToken = default)
    {
        var batches = context.ProductBatches.AsNoTracking().Where(x => !x.IsDisposed);
        if (branchId.HasValue) batches = batches.Where(x => x.BranchId == branchId);
        if (godownId.HasValue) batches = batches.Where(x => x.GodownId == godownId);
        return await batches.SumAsync(x => (decimal?)(x.QuantityAvailable * x.PurchasePrice), cancellationToken) ?? 0;
    }
}
