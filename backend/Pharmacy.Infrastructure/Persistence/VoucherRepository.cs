using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Customers;
using Pharmacy.Application.DTOs.Suppliers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.Accounting.Vouchers;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class VoucherRepository(PharmacyDbContext context) : IVoucherRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);
    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) => context.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default) => context.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default) => context.ChartOfAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task<Dictionary<AccountMappingKey, Guid>> GetAccountMappingLookupAsync(CancellationToken cancellationToken = default) =>
        await context.AccountMappings.AsNoTracking().ToDictionaryAsync(x => x.MappingKey, x => x.ChartOfAccountId, cancellationToken);

    public async Task<string> NextVoucherNumberAsync(VoucherType type, DateTime voucherDateUtc, CancellationToken cancellationToken = default)
    {
        var (sql, prefix) = type switch
        {
            VoucherType.CashReceipt => ("SELECT nextval('\"CashReceiptVoucherNumberSequence\"'::regclass) AS \"Value\"", "CRV"),
            VoucherType.CashPayment => ("SELECT nextval('\"CashPaymentVoucherNumberSequence\"'::regclass) AS \"Value\"", "CPV"),
            VoucherType.BankReceipt => ("SELECT nextval('\"BankReceiptVoucherNumberSequence\"'::regclass) AS \"Value\"", "BRV"),
            VoucherType.BankPayment => ("SELECT nextval('\"BankPaymentVoucherNumberSequence\"'::regclass) AS \"Value\"", "BPV"),
            VoucherType.Contra => ("SELECT nextval('\"ContraVoucherNumberSequence\"'::regclass) AS \"Value\"", "CV"),
            VoucherType.Journal => ("SELECT nextval('\"JournalVoucherNumberSequence\"'::regclass) AS \"Value\"", "JV"),
            _ => throw new InvalidOperationException("Unsupported voucher type.")
        };
        var next = await context.Database.SqlQueryRaw<long>(sql).SingleAsync(cancellationToken);
        return $"{prefix}-{voucherDateUtc.Year}-{next:000000}";
    }

    public async Task<string> NextJournalEntryNumberAsync(DateTime entryDateUtc, CancellationToken cancellationToken = default)
    {
        var next = await context.Database.SqlQueryRaw<long>("SELECT nextval('\"JournalEntryNumberSequence\"'::regclass) AS \"Value\"").SingleAsync(cancellationToken);
        return $"JV-{entryDateUtc.Year}-{next:000000}";
    }

    public async Task AddVoucherAsync(Voucher voucher, CancellationToken cancellationToken = default) => await context.Vouchers.AddAsync(voucher, cancellationToken);

    public Task<Voucher?> GetVoucherAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Vouchers
            .Include(v => v.Branch)
            .Include(v => v.Customer)
            .Include(v => v.Supplier)
            .Include(v => v.ChartOfAccount)
            .Include(v => v.ContraToChartOfAccount)
            .Include(v => v.CreatedByUser)
            .Include(v => v.PostedByUser)
            .Include(v => v.JournalEntry)
            .Include(v => v.FinancialAccount)
            .Include(v => v.Lines).ThenInclude(l => l.ChartOfAccount)
            .Include(v => v.Lines).ThenInclude(l => l.Customer)
            .Include(v => v.Lines).ThenInclude(l => l.Supplier)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public Task<FinancialAccount?> GetFinancialAccountAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.FinancialAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddFinancialLedgerEntryAsync(FinancialLedgerEntry entry, CancellationToken cancellationToken = default) => await context.FinancialLedgerEntries.AddAsync(entry, cancellationToken);

    public async Task<PagedResult<VoucherListItemDto>> ListVouchersAsync(VoucherListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var vouchers = context.Vouchers.AsNoTracking().Include(v => v.Branch).Include(v => v.Customer).Include(v => v.Supplier).Include(v => v.PostedByUser).AsQueryable();
        if (!canSelectBranch && actorBranchId.HasValue) vouchers = vouchers.Where(v => v.BranchId == actorBranchId);
        if (query.BranchId.HasValue) vouchers = vouchers.Where(v => v.BranchId == query.BranchId);
        if (query.Type.HasValue) vouchers = vouchers.Where(v => v.Type == query.Type);
        if (query.Status.HasValue) vouchers = vouchers.Where(v => v.Status == query.Status);
        if (query.CustomerId.HasValue) vouchers = vouchers.Where(v => v.CustomerId == query.CustomerId);
        if (query.SupplierId.HasValue) vouchers = vouchers.Where(v => v.SupplierId == query.SupplierId);
        if (query.FromUtc.HasValue) vouchers = vouchers.Where(v => v.VoucherDateUtc >= query.FromUtc);
        if (query.ToUtc.HasValue) vouchers = vouchers.Where(v => v.VoucherDateUtc <= query.ToUtc);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            vouchers = vouchers.Where(v => EF.Functions.ILike(v.VoucherNumber, pattern) ||
                (v.Reference != null && EF.Functions.ILike(v.Reference, pattern)) ||
                EF.Functions.ILike(v.Description, pattern));
        }
        var total = await vouchers.CountAsync(cancellationToken);
        var items = await vouchers.OrderByDescending(v => v.VoucherDateUtc).ThenByDescending(v => v.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(v => new VoucherListItemDto(v.Id, v.VoucherNumber, v.Type, v.VoucherDateUtc, v.BranchId, v.Branch!.Name,
                v.Reference, v.Customer != null ? v.Customer.Name : v.Supplier != null ? v.Supplier.Name : null, v.Description,
                v.Lines.Sum(l => l.Debit), v.Status, v.PostedByUser == null ? null : v.PostedByUser.FullName))
            .ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        // Typed posting appends pre-keyed lines to an already persisted draft. Explicitly
        // mark new lines as inserts before EF interprets their generated keys as updates.
        var voucher = context.ChangeTracker.Entries<Voucher>().Select(x => x.Entity)
            .FirstOrDefault(x => x.Id == entry.SourceId);
        if (voucher is not null)
        {
            var persistedIds = await context.VoucherLines.AsNoTracking().Where(x => x.VoucherId == voucher.Id)
                .Select(x => x.Id).ToListAsync(cancellationToken);
            foreach (var line in voucher.Lines)
                if (!persistedIds.Contains(line.Id)) context.Entry(line).State = EntityState.Added;
        }
        await context.JournalEntries.AddAsync(entry, cancellationToken);
    }

    public Task<bool> JournalEntryExistsForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default)
    {
        if (context.JournalEntries.Local.Any(x => x.SourceType == sourceType && x.SourceId == sourceId))
            return Task.FromResult(true);
        return context.JournalEntries.AnyAsync(x => x.SourceType == sourceType && x.SourceId == sourceId, cancellationToken);
    }

    public Task<bool> VoucherHasReversalAsync(Guid voucherId, CancellationToken cancellationToken = default)
    {
        if (context.Vouchers.Local.Any(x => x.ReversalOfVoucherId == voucherId))
            return Task.FromResult(true);
        return context.Vouchers.AnyAsync(x => x.ReversalOfVoucherId == voucherId, cancellationToken);
    }

    public async Task<string> NextCustomerPaymentReceiptNumberAsync(DateTime paymentDateUtc, CancellationToken cancellationToken = default)
    {
        var next = await context.Database.SqlQueryRaw<long>("SELECT nextval('\"CustomerPaymentReceiptSequence\"'::regclass) AS \"Value\"").SingleAsync(cancellationToken);
        return $"CR-{paymentDateUtc.Year}-{next:000000}";
    }

    public async Task AddCustomerPaymentAsync(CustomerPayment payment, CancellationToken cancellationToken = default) => await context.CustomerPayments.AddAsync(payment, cancellationToken);
    public async Task AddCustomerLedgerEntryAsync(CustomerLedgerEntry entry, CancellationToken cancellationToken = default) => await context.CustomerLedgerEntries.AddAsync(entry, cancellationToken);
    public Task<IReadOnlyList<OpenReceivableDto>> GetOpenReceivablesAsync(Guid customerId, Guid? branchId, CancellationToken cancellationToken = default) =>
        OpenDocumentQueries.GetOpenReceivablesAsync(context, customerId, branchId, cancellationToken);
    public async Task AddCustomerPaymentAllocationAsync(CustomerPaymentAllocation allocation, CancellationToken cancellationToken = default) => await context.CustomerPaymentAllocations.AddAsync(allocation, cancellationToken);

    public async Task AddSupplierLedgerEntryAsync(SupplierLedgerEntry entry, CancellationToken cancellationToken = default) => await context.SupplierLedgerEntries.AddAsync(entry, cancellationToken);
    public Task<IReadOnlyList<OpenPayableDto>> GetOpenPayablesAsync(Guid supplierId, Guid? branchId, CancellationToken cancellationToken = default) =>
        OpenDocumentQueries.GetOpenPayablesAsync(context, supplierId, branchId, cancellationToken);
    public async Task AddSupplierPaymentAllocationAsync(SupplierPaymentAllocation allocation, CancellationToken cancellationToken = default) => await context.SupplierPaymentAllocations.AddAsync(allocation, cancellationToken);

    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        await using var tx = await context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        try { await operation(cancellationToken); await tx.CommitAsync(cancellationToken); }
        catch { await tx.RollbackAsync(cancellationToken); throw; }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ResourceConflictException("A voucher with this unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Voucher financial constraints were violated.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            throw new ResourceConflictException("This voucher was changed by another action at the same time. Refresh and try again.");
        }
    }

    public void AllowPostingIntoSoftClosedPeriod() => context.AllowPostingIntoSoftClosedPeriod = true;
}
