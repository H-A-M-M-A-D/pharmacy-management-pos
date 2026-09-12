using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Customers;
using Pharmacy.Application.DTOs.Suppliers;
using Pharmacy.Application.Services.Accounting.PartyAdjustments;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class PartyAdjustmentRepository(PharmacyDbContext context) : IPartyAdjustmentRepository
{
    public Task<JournalEntry?> GetJournalForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default) =>
        context.JournalEntries.Include(x => x.Lines).SingleOrDefaultAsync(x => x.SourceType == sourceType && x.SourceId == sourceId, cancellationToken);
    public Task<bool> JournalHasReversalAsync(Guid journalId, CancellationToken cancellationToken = default) =>
        context.JournalEntries.AnyAsync(x => x.ReversesJournalEntryId == journalId, cancellationToken);
    public async Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default) => await context.JournalEntries.AddAsync(entry, cancellationToken);
    public Task<SupplierLedgerEntry?> GetSupplierSettlementAsync(Guid supplierId, string referenceNumber, CancellationToken cancellationToken = default) =>
        context.SupplierLedgerEntries.SingleOrDefaultAsync(x => x.SupplierId == supplierId && x.ReferenceNumber == referenceNumber && x.Amount < 0, cancellationToken);
    public async Task<IReadOnlyList<SupplierPaymentAllocation>> RemoveSupplierAllocationsAsync(Guid ledgerEntryId, CancellationToken cancellationToken = default)
    {
        var allocations = await context.SupplierPaymentAllocations.Where(x => x.SupplierLedgerEntryId == ledgerEntryId).ToListAsync(cancellationToken);
        context.SupplierPaymentAllocations.RemoveRange(allocations);
        return allocations;
    }
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);
    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) => context.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default) => context.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Sale?> GetSaleAsync(Guid id, CancellationToken cancellationToken = default) => context.Sales.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<GoodsReceipt?> GetGoodsReceiptAsync(Guid id, CancellationToken cancellationToken = default) => context.GoodsReceipts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<FinancialAccount?> GetFinancialAccountAsync(Guid id, CancellationToken cancellationToken = default) => context.FinancialAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<decimal> GetFinancialAccountBalanceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await context.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == id, cancellationToken);
        var movement = await context.FinancialLedgerEntries.AsNoTracking().Where(x => x.FinancialAccountId == id).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        // The opening balance is represented by its own ledger entry.
        return movement;
    }

    public async Task<string> NextNumberAsync(string sequenceName, string prefix, DateTime dateUtc, CancellationToken cancellationToken = default)
    {
        var qualified = $"\"{sequenceName}\"";
        var next = await context.Database.SqlQuery<long>($"SELECT nextval({qualified}::regclass) AS \"Value\"").SingleAsync(cancellationToken);
        return $"{prefix}-{dateUtc.Year}-{next:000000}";
    }

    public async Task<string> GetJournalEntryNumberForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default) =>
        await context.JournalEntries.AsNoTracking().Where(x => x.SourceType == sourceType && x.SourceId == sourceId)
            .Select(x => x.EntryNumber).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

    public Task<IReadOnlyList<OpenReceivableDto>> GetOpenReceivablesAsync(Guid customerId, Guid? branchId, CancellationToken cancellationToken = default) =>
        OpenDocumentQueries.GetOpenReceivablesAsync(context, customerId, branchId, cancellationToken);
    public Task<IReadOnlyList<OpenPayableDto>> GetOpenPayablesAsync(Guid supplierId, Guid? branchId, CancellationToken cancellationToken = default) =>
        OpenDocumentQueries.GetOpenPayablesAsync(context, supplierId, branchId, cancellationToken);

    public async Task AddCustomerPaymentAsync(CustomerPayment payment, CancellationToken cancellationToken = default) => await context.CustomerPayments.AddAsync(payment, cancellationToken);
    public async Task AddCustomerPaymentAllocationAsync(CustomerPaymentAllocation allocation, CancellationToken cancellationToken = default) => await context.CustomerPaymentAllocations.AddAsync(allocation, cancellationToken);
    public async Task AddCustomerLedgerEntryAsync(CustomerLedgerEntry entry, CancellationToken cancellationToken = default) => await context.CustomerLedgerEntries.AddAsync(entry, cancellationToken);
    public async Task AddSupplierLedgerEntryAsync(SupplierLedgerEntry entry, CancellationToken cancellationToken = default) => await context.SupplierLedgerEntries.AddAsync(entry, cancellationToken);
    public async Task AddSupplierPaymentAllocationAsync(SupplierPaymentAllocation allocation, CancellationToken cancellationToken = default) => await context.SupplierPaymentAllocations.AddAsync(allocation, cancellationToken);
    public async Task AddFinancialLedgerEntryAsync(FinancialLedgerEntry entry, CancellationToken cancellationToken = default) => await context.FinancialLedgerEntries.AddAsync(entry, cancellationToken);

    public async Task AddCreditNoteAsync(CreditNote note, CancellationToken cancellationToken = default) => await context.CreditNotes.AddAsync(note, cancellationToken);
    public async Task<IReadOnlyList<CreditNote>> ListCreditNotesAsync(Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var query = context.CreditNotes.AsNoTracking().Include(x => x.Customer).Include(x => x.AppliedToSale).Include(x => x.CreatedByUser).AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        return await query.OrderByDescending(x => x.IssueDateUtc).ToListAsync(cancellationToken);
    }

    public async Task AddDebitNoteAsync(DebitNote note, CancellationToken cancellationToken = default) => await context.DebitNotes.AddAsync(note, cancellationToken);
    public async Task<IReadOnlyList<DebitNote>> ListDebitNotesAsync(Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var query = context.DebitNotes.AsNoTracking().Include(x => x.Supplier).Include(x => x.AppliedToGoodsReceipt).Include(x => x.CreatedByUser).AsQueryable();
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        return await query.OrderByDescending(x => x.IssueDateUtc).ToListAsync(cancellationToken);
    }

    public async Task AddCustomerWriteOffAsync(CustomerWriteOff writeOff, CancellationToken cancellationToken = default) => await context.CustomerWriteOffs.AddAsync(writeOff, cancellationToken);
    public async Task<IReadOnlyList<CustomerWriteOff>> ListCustomerWriteOffsAsync(Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var query = context.CustomerWriteOffs.AsNoTracking().Include(x => x.Customer).Include(x => x.CreatedByUser).AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        return await query.OrderByDescending(x => x.WriteOffDateUtc).ToListAsync(cancellationToken);
    }

    public async Task AddSupplierWriteOffAsync(SupplierWriteOff writeOff, CancellationToken cancellationToken = default) => await context.SupplierWriteOffs.AddAsync(writeOff, cancellationToken);
    public async Task<IReadOnlyList<SupplierWriteOff>> ListSupplierWriteOffsAsync(Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var query = context.SupplierWriteOffs.AsNoTracking().Include(x => x.Supplier).Include(x => x.CreatedByUser).AsQueryable();
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        return await query.OrderByDescending(x => x.WriteOffDateUtc).ToListAsync(cancellationToken);
    }

    public async Task AddCustomerAdvanceAsync(CustomerAdvance advance, CancellationToken cancellationToken = default) => await context.CustomerAdvances.AddAsync(advance, cancellationToken);
    public Task<CustomerAdvance?> GetCustomerAdvanceAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.CustomerAdvances.Include(x => x.Customer).Include(x => x.FinancialAccount).Include(x => x.CreatedByUser).Include(x => x.Applications)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task<IReadOnlyList<CustomerAdvance>> ListCustomerAdvancesAsync(Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var query = context.CustomerAdvances.AsNoTracking().Include(x => x.Customer).Include(x => x.FinancialAccount).Include(x => x.CreatedByUser).AsQueryable();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        return await query.OrderByDescending(x => x.ReceivedDateUtc).ToListAsync(cancellationToken);
    }
    public async Task AddCustomerAdvanceApplicationAsync(CustomerAdvanceApplication application, CancellationToken cancellationToken = default) => await context.CustomerAdvanceApplications.AddAsync(application, cancellationToken);

    public async Task AddSupplierAdvanceAsync(SupplierAdvance advance, CancellationToken cancellationToken = default) => await context.SupplierAdvances.AddAsync(advance, cancellationToken);
    public Task<SupplierAdvance?> GetSupplierAdvanceAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.SupplierAdvances.Include(x => x.Supplier).Include(x => x.FinancialAccount).Include(x => x.CreatedByUser).Include(x => x.Applications)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task<IReadOnlyList<SupplierAdvance>> ListSupplierAdvancesAsync(Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var query = context.SupplierAdvances.AsNoTracking().Include(x => x.Supplier).Include(x => x.FinancialAccount).Include(x => x.CreatedByUser).AsQueryable();
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId);
        if (branchId.HasValue) query = query.Where(x => x.BranchId == branchId);
        return await query.OrderByDescending(x => x.PaidDateUtc).ToListAsync(cancellationToken);
    }
    public async Task AddSupplierAdvanceApplicationAsync(SupplierAdvanceApplication application, CancellationToken cancellationToken = default) => await context.SupplierAdvanceApplications.AddAsync(application, cancellationToken);

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
            throw new ResourceConflictException("A record with the same unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            var error = (PostgresException)ex.InnerException!;
            throw new RequestValidationException($"A data constraint was violated ({error.ConstraintName ?? error.MessageText}).");
        }
    }
}
