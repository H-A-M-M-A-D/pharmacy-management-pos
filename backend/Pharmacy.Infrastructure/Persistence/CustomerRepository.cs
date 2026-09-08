using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Customers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.Customers;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class CustomerRepository(PharmacyDbContext context) : ICustomerRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);
    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);
    public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        context.Customers.FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);
    public Task<bool> CustomerCodeExistsAsync(string customerCode, Guid? excludingId = null, CancellationToken cancellationToken = default) =>
        context.Customers.AnyAsync(x => x.CustomerCode == customerCode && (!excludingId.HasValue || x.Id != excludingId), cancellationToken);
    public async Task<string> NextCustomerCodeAsync(CancellationToken cancellationToken = default)
    {
        var next = await context.Database.SqlQueryRaw<long>("SELECT nextval('\"CustomerCodeSequence\"'::regclass) AS \"Value\"").SingleAsync(cancellationToken);
        return $"CUS-{next:000000}";
    }
    public async Task<string> NextPaymentReceiptNumberAsync(DateTime paymentDateUtc, CancellationToken cancellationToken = default)
    {
        var next = await context.Database.SqlQueryRaw<long>("SELECT nextval('\"CustomerPaymentReceiptSequence\"'::regclass) AS \"Value\"").SingleAsync(cancellationToken);
        return $"CR-{paymentDateUtc.Year}-{next:000000}";
    }
    public async Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default) => await context.Customers.AddAsync(customer, cancellationToken);
    public async Task AddPaymentAsync(CustomerPayment payment, CancellationToken cancellationToken = default) => await context.CustomerPayments.AddAsync(payment, cancellationToken);
    public async Task AddLedgerEntryAsync(CustomerLedgerEntry entry, CancellationToken cancellationToken = default) => await context.CustomerLedgerEntries.AddAsync(entry, cancellationToken);
    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task<PagedResult<CustomerListItemDto>> ListCustomersAsync(CustomerListQuery query, CancellationToken cancellationToken = default)
    {
        var customers = context.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            customers = customers.Where(x => EF.Functions.ILike(x.CustomerCode, pattern) ||
                EF.Functions.ILike(x.Name, pattern) ||
                (x.PhoneNumber != null && EF.Functions.ILike(x.PhoneNumber, pattern)) ||
                (x.AlternatePhone != null && EF.Functions.ILike(x.AlternatePhone, pattern)) ||
                (x.Email != null && EF.Functions.ILike(x.Email, pattern)) ||
                (x.BusinessName != null && EF.Functions.ILike(x.BusinessName, pattern)));
        }
        if (query.IsActive.HasValue) customers = customers.Where(x => x.IsActive == query.IsActive);
        if (!string.IsNullOrWhiteSpace(query.City)) customers = customers.Where(x => x.City != null && EF.Functions.ILike(x.City, $"%{query.City.Trim()}%"));
        var projected = customers.Select(x => new
        {
            Customer = x,
            Outstanding = context.CustomerLedgerEntries.Where(e => e.CustomerId == x.Id).Sum(e => (decimal?)e.Amount) ?? 0
        });
        if (query.HasOutstandingBalance.HasValue) projected = projected.Where(x => query.HasOutstandingBalance.Value ? x.Outstanding > 0 : x.Outstanding <= 0);
        if (query.OverCreditLimit.HasValue) projected = projected.Where(x => query.OverCreditLimit.Value ? x.Outstanding > x.Customer.CreditLimit : x.Outstanding <= x.Customer.CreditLimit);
        var total = await projected.CountAsync(cancellationToken);
        projected = (query.SortBy.ToLowerInvariant(), query.Descending) switch
        {
            ("createdat", false) => projected.OrderBy(x => x.Customer.CreatedAt).ThenBy(x => x.Customer.Name),
            ("createdat", true) => projected.OrderByDescending(x => x.Customer.CreatedAt).ThenBy(x => x.Customer.Name),
            ("outstandingbalance", false) => projected.OrderBy(x => x.Outstanding).ThenBy(x => x.Customer.Name),
            ("outstandingbalance", true) => projected.OrderByDescending(x => x.Outstanding).ThenBy(x => x.Customer.Name),
            ("code", false) => projected.OrderBy(x => x.Customer.CustomerCode),
            ("code", true) => projected.OrderByDescending(x => x.Customer.CustomerCode),
            (_, true) => projected.OrderByDescending(x => x.Customer.Name).ThenBy(x => x.Customer.Id),
            _ => projected.OrderBy(x => x.Customer.Name).ThenBy(x => x.Customer.Id)
        };
        var items = await projected.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CustomerListItemDto(x.Customer.Id, x.Customer.CustomerCode, x.Customer.Name, x.Customer.PhoneNumber,
                x.Customer.Email, x.Customer.City, x.Customer.BusinessName, x.Customer.CreditLimit,
                x.Outstanding > 0 ? x.Outstanding : 0, x.Outstanding < 0 ? -x.Outstanding : 0, x.Customer.IsActive))
            .ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<CustomerDetailsDto?> GetCustomerDetailsAsync(Guid customerId, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var customer = await context.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        if (customer is null) return null;
        var ledger = context.CustomerLedgerEntries.AsNoTracking().Where(x => x.CustomerId == customerId);
        if (!canSelectBranch && actorBranchId.HasValue) ledger = ledger.Where(x => x.BranchId == actorBranchId);
        var balance = await ledger.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        var payments = await ledger.Where(x => x.EntryType == CustomerLedgerEntryType.Payment).SumAsync(x => (decimal?)-x.Amount, cancellationToken) ?? 0;
        var lastPayment = await context.CustomerPayments.AsNoTracking()
            .Where(x => x.CustomerId == customerId && (canSelectBranch || x.BranchId == actorBranchId))
            .OrderByDescending(x => x.PaymentDateUtc)
            .Select(x => (DateTime?)x.PaymentDateUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return new(customer.Id, customer.CustomerCode, customer.Name, customer.PhoneNumber, customer.AlternatePhone,
            customer.Email, customer.Address, customer.City, customer.BusinessName, customer.NTN,
            customer.OpeningBalance, customer.CreditLimit, customer.IsActive, balance > 0 ? balance : 0,
            balance < 0 ? -balance : 0, payments, lastPayment, customer.CreatedAt, customer.UpdatedAt);
    }

    public async Task<IReadOnlyList<CustomerLookupDto>> LookupCustomersAsync(string? search, bool activeOnly, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var customers = context.Customers.AsNoTracking().AsQueryable();
        if (activeOnly) customers = customers.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            customers = customers.Where(x => EF.Functions.ILike(x.CustomerCode, pattern) || EF.Functions.ILike(x.Name, pattern) ||
                (x.PhoneNumber != null && EF.Functions.ILike(x.PhoneNumber, pattern)) ||
                (x.BusinessName != null && EF.Functions.ILike(x.BusinessName, pattern)));
        }
        return await customers.OrderBy(x => x.Name).Take(50).Select(x => new CustomerLookupDto(
            x.Id, x.CustomerCode, x.Name, x.PhoneNumber, x.CreditLimit,
            context.CustomerLedgerEntries.Where(e => e.CustomerId == x.Id && (!branchId.HasValue || e.BranchId == branchId)).Sum(e => (decimal?)e.Amount) ?? 0,
            x.CreditLimit - (context.CustomerLedgerEntries.Where(e => e.CustomerId == x.Id && (!branchId.HasValue || e.BranchId == branchId)).Sum(e => (decimal?)e.Amount) ?? 0),
            x.IsActive)).ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<CustomerLedgerEntryDto>> ListLedgerAsync(Guid customerId, CustomerLedgerQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var entries = context.CustomerLedgerEntries.AsNoTracking().Include(x => x.Branch).Include(x => x.CreatedByUser)
            .Where(x => x.CustomerId == customerId);
        if (!canSelectBranch && actorBranchId.HasValue) entries = entries.Where(x => x.BranchId == actorBranchId);
        if (query.BranchId.HasValue) entries = entries.Where(x => x.BranchId == query.BranchId);
        if (query.EntryType.HasValue) entries = entries.Where(x => x.EntryType == query.EntryType);
        if (query.DateFrom.HasValue) entries = entries.Where(x => x.EntryDate >= query.DateFrom);
        if (query.DateTo.HasValue) entries = entries.Where(x => x.EntryDate <= query.DateTo);
        var all = await entries.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new
        {
            x.Id, x.CreatedAt, x.EntryDate, x.EntryType, x.Amount, BranchName = x.Branch!.Name,
            UserName = x.CreatedByUser == null ? null : x.CreatedByUser.FullName, x.PaymentMethod,
            x.ReferenceNumber, x.ReferenceType, x.ReferenceId, x.Notes
        }).ToListAsync(cancellationToken);
        var running = 0m;
        var mapped = all.Select(x =>
        {
            running += x.Amount;
            return new CustomerLedgerEntryDto(x.Id, x.CreatedAt, x.EntryDate, x.EntryType, x.Amount, running,
                x.BranchName, x.UserName, x.PaymentMethod, x.ReferenceNumber, x.ReferenceType, x.ReferenceId, x.Notes);
        }).ToList();
        return new(mapped.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(), query.Page, query.PageSize, mapped.Count);
    }

    public async Task<CustomerPaymentReceiptDto?> GetPaymentReceiptAsync(Guid paymentId, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var payment = await context.CustomerPayments.AsNoTracking().Include(x => x.Customer).Include(x => x.Branch).Include(x => x.ReceivedByUser)
            .Where(x => x.Id == paymentId && (canSelectBranch || x.BranchId == actorBranchId))
            .FirstOrDefaultAsync(cancellationToken);
        return payment is null ? null : new CustomerPaymentReceiptDto(payment.Id, payment.ReceiptNumber, payment.CustomerId,
            payment.Customer!.CustomerCode, payment.Customer.Name, payment.BranchId, payment.Branch!.Name,
            payment.PaymentDateUtc, payment.Amount, payment.PaymentMethod, payment.ReferenceNumber, payment.ReceivedByUser!.FullName, payment.Notes);
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
            throw new ResourceConflictException("A customer with this unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Customer financial constraints were violated.");
        }
    }
}
