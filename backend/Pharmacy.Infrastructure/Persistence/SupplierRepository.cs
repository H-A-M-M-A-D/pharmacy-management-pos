using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Suppliers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.Suppliers;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class SupplierRepository(PharmacyDbContext context) : ISupplierRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
    public Task<Supplier?> GetSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default) =>
        context.Suppliers.FirstOrDefaultAsync(x => x.Id == supplierId, cancellationToken);
    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);
    public Task<bool> NormalizedNameExistsAsync(string normalizedName, Guid? excludingId = null, CancellationToken cancellationToken = default) =>
        context.Suppliers.AnyAsync(x => x.NormalizedName == normalizedName && (!excludingId.HasValue || x.Id != excludingId), cancellationToken);
    public async Task AddSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default) => await context.Suppliers.AddAsync(supplier, cancellationToken);
    public async Task AddLedgerEntryAsync(SupplierLedgerEntry entry, CancellationToken cancellationToken = default) => await context.SupplierLedgerEntries.AddAsync(entry, cancellationToken);
    public async Task AddPaymentAllocationAsync(SupplierPaymentAllocation allocation, CancellationToken cancellationToken = default) => await context.SupplierPaymentAllocations.AddAsync(allocation, cancellationToken);

    public Task<IReadOnlyList<OpenPayableDto>> GetOpenPayablesAsync(Guid supplierId, Guid? branchId, CancellationToken cancellationToken = default) =>
        OpenDocumentQueries.GetOpenPayablesAsync(context, supplierId, branchId, cancellationToken);
    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task<PagedResult<SupplierListItemDto>> ListSuppliersAsync(SupplierListQuery query, CancellationToken cancellationToken = default)
    {
        var suppliers = context.Suppliers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            suppliers = suppliers.Where(x => EF.Functions.ILike(x.Name, pattern) ||
                (x.ShortName != null && EF.Functions.ILike(x.ShortName, pattern)) ||
                (x.ContactPerson != null && EF.Functions.ILike(x.ContactPerson, pattern)) ||
                (x.PhoneNumber != null && EF.Functions.ILike(x.PhoneNumber, pattern)) ||
                (x.WhatsApp != null && EF.Functions.ILike(x.WhatsApp, pattern)) ||
                (x.Email != null && EF.Functions.ILike(x.Email, pattern)) ||
                (x.TaxNumber != null && EF.Functions.ILike(x.TaxNumber, pattern)));
        }
        if (query.IsActive.HasValue) suppliers = suppliers.Where(x => x.IsActive == query.IsActive);
        if (!string.IsNullOrWhiteSpace(query.City)) suppliers = suppliers.Where(x => x.City != null && EF.Functions.ILike(x.City, $"%{query.City.Trim()}%"));

        var projected = suppliers.Select(x => new
        {
            Supplier = x,
            Outstanding = context.SupplierLedgerEntries.Where(e => e.SupplierId == x.Id).Sum(e => (decimal?)e.Amount) ?? 0
        });
        if (query.HasOutstandingBalance.HasValue)
        {
            projected = projected.Where(x => query.HasOutstandingBalance.Value ? x.Outstanding != 0 : x.Outstanding == 0);
        }
        var total = await projected.CountAsync(cancellationToken);
        projected = (query.SortBy.ToLowerInvariant(), query.Descending) switch
        {
            ("createdat", false) => projected.OrderBy(x => x.Supplier.CreatedAt).ThenBy(x => x.Supplier.Name),
            ("createdat", true) => projected.OrderByDescending(x => x.Supplier.CreatedAt).ThenBy(x => x.Supplier.Name),
            ("outstandingbalance", false) => projected.OrderBy(x => x.Outstanding).ThenBy(x => x.Supplier.Name),
            ("outstandingbalance", true) => projected.OrderByDescending(x => x.Outstanding).ThenBy(x => x.Supplier.Name),
            (_, true) => projected.OrderByDescending(x => x.Supplier.Name).ThenBy(x => x.Supplier.Id),
            _ => projected.OrderBy(x => x.Supplier.Name).ThenBy(x => x.Supplier.Id)
        };
        var items = await projected.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new SupplierListItemDto(x.Supplier.Id, x.Supplier.Name, x.Supplier.ShortName, x.Supplier.ContactPerson,
                x.Supplier.PhoneNumber, x.Supplier.WhatsApp, x.Supplier.Email, x.Supplier.City, x.Supplier.CreditLimit,
                x.Outstanding, x.Supplier.IsActive)).ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<SupplierDetailsDto?> GetSupplierDetailsAsync(Guid supplierId, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var supplier = await context.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == supplierId, cancellationToken);
        if (supplier is null) return null;
        var ledger = context.SupplierLedgerEntries.AsNoTracking().Where(x => x.SupplierId == supplierId);
        if (!canSelectBranch && actorBranchId.HasValue) ledger = ledger.Where(x => x.BranchId == actorBranchId);
        var outstanding = await ledger.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var payments = await ledger.Where(x => x.EntryType == SupplierLedgerEntryType.Payment).SumAsync(x => (decimal?)-x.Amount, cancellationToken) ?? 0;
        var lastPayment = await ledger.Where(x => x.EntryType == SupplierLedgerEntryType.Payment).OrderByDescending(x => x.EntryDate).Select(x => (DateOnly?)x.EntryDate).FirstOrDefaultAsync(cancellationToken);
        return new(supplier.Id, supplier.Name, supplier.ShortName, supplier.ContactPerson, supplier.PhoneNumber,
            supplier.AlternatePhone, supplier.WhatsApp, supplier.Email, supplier.Address, supplier.City,
            supplier.TaxNumber, supplier.STRN, supplier.OpeningBalance, supplier.CreditLimit, supplier.PaymentTermsDays,
            supplier.IsActive, outstanding, payments, lastPayment, supplier.CreatedAt, supplier.UpdatedAt);
    }

    public async Task<IReadOnlyList<SupplierLookupDto>> LookupSuppliersAsync(string? search, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var suppliers = context.Suppliers.AsNoTracking().AsQueryable();
        if (activeOnly) suppliers = suppliers.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            suppliers = suppliers.Where(x => EF.Functions.ILike(x.Name, pattern) || (x.ShortName != null && EF.Functions.ILike(x.ShortName, pattern)));
        }
        return await suppliers.OrderBy(x => x.Name).Take(50).Select(x => new SupplierLookupDto(x.Id, x.Name, x.ShortName, x.IsActive)).ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<SupplierLedgerEntryDto>> ListLedgerAsync(Guid supplierId, SupplierLedgerQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var entries = context.SupplierLedgerEntries.AsNoTracking().Include(x => x.Branch).Include(x => x.CreatedByUser)
            .Where(x => x.SupplierId == supplierId);
        if (!canSelectBranch && actorBranchId.HasValue) entries = entries.Where(x => x.BranchId == actorBranchId);
        if (query.BranchId.HasValue) entries = entries.Where(x => x.BranchId == query.BranchId);
        if (query.EntryType.HasValue) entries = entries.Where(x => x.EntryType == query.EntryType);
        if (query.DateFrom.HasValue) entries = entries.Where(x => x.EntryDate >= query.DateFrom);
        if (query.DateTo.HasValue) entries = entries.Where(x => x.EntryDate <= query.DateTo);
        var ordered = entries.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id);
        var all = await ordered.Select(x => new
        {
            x.Id, x.CreatedAt, x.EntryDate, x.EntryType, x.Amount, BranchName = x.Branch!.Name,
            UserName = x.CreatedByUser == null ? null : x.CreatedByUser.FullName, x.PaymentMethod,
            x.ReferenceNumber, x.ReferenceType, x.ReferenceId, x.Notes
        }).ToListAsync(cancellationToken);
        var running = 0m;
        var mapped = all.Select(x =>
        {
            running += x.Amount;
            return new SupplierLedgerEntryDto(x.Id, x.CreatedAt, x.EntryDate, x.EntryType, x.Amount, running,
                x.BranchName, x.UserName, x.PaymentMethod, x.ReferenceNumber, x.ReferenceType, x.ReferenceId, x.Notes);
        }).ToList();
        return new(mapped.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(), query.Page, query.PageSize, mapped.Count);
    }

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
            throw new ResourceConflictException("A supplier with this name already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Supplier financial constraints were violated.");
        }
    }
}
