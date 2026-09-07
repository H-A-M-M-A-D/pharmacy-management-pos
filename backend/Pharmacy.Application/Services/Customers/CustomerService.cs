using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Customers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Customers;

public sealed class CustomerService(ICustomerRepository repository, TimeProvider timeProvider) : ICustomerService
{
    public async Task<PagedResult<CustomerListItemDto>> ListCustomersAsync(Guid actorId, CustomerListQuery query, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.CustomersView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        if (!new[] { "name", "createdat", "outstandingbalance", "code" }.Contains(query.SortBy.ToLowerInvariant()))
            throw new RequestValidationException("The requested customer sort is not supported.");
        return await repository.ListCustomersAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerLookupDto>> LookupCustomersAsync(Guid actorId, string? search, bool activeOnly = true, Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.CustomersView, cancellationToken);
        if (branchId.HasValue) EnsureBranchAccess(actor, branchId.Value);
        return await repository.LookupCustomersAsync(search, activeOnly, branchId ?? actor.BranchId, cancellationToken);
    }

    public async Task<CustomerDetailsDto> GetCustomerAsync(Guid actorId, Guid customerId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.CustomersView, cancellationToken);
        return await repository.GetCustomerDetailsAsync(customerId, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Customer was not found.");
    }

    public async Task<CustomerDetailsDto> CreateCustomerAsync(Guid actorId, CustomerRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.CustomersCreate, cancellationToken);
        Validate(request.Name, request.Email, request.CreditLimit);
        Customer? customer = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            customer = new Customer
            {
                CustomerCode = await repository.NextCustomerCodeAsync(ct),
                Name = request.Name.Trim(),
                NormalizedName = Normalize(request.Name),
                PhoneNumber = Clean(request.PhoneNumber),
                AlternatePhone = Clean(request.AlternatePhone),
                Email = Clean(request.Email),
                Address = Clean(request.Address),
                City = Clean(request.City),
                BusinessName = Clean(request.BusinessName),
                NTN = Clean(request.NTN),
                OpeningBalance = Money(request.OpeningBalance),
                CreditLimit = Money(request.CreditLimit),
                IsActive = request.IsActive
            };
            await repository.AddCustomerAsync(customer, ct);
            await Audit(actorId, "CustomerCreated", "Customer", customer.Id, null, Values(customer), ct);
            if (customer.OpeningBalance != 0)
            {
                await repository.AddLedgerEntryAsync(new CustomerLedgerEntry
                {
                    CustomerId = customer.Id,
                    BranchId = actor.BranchId,
                    EntryType = CustomerLedgerEntryType.OpeningBalance,
                    Amount = customer.OpeningBalance,
                    EntryDate = BusinessDate(),
                    ReferenceType = "CustomerOpeningBalance",
                    Notes = "Opening balance",
                    CreatedByUserId = actorId
                }, ct);
                await Audit(actorId, "CustomerOpeningBalanceRecorded", "Customer", customer.Id, null, new { customer.Id, customer.OpeningBalance, BranchId = actor.BranchId }, ct);
            }
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return (await repository.GetCustomerDetailsAsync(customer!.Id, actor.BranchId, CanSelectBranch(actor), cancellationToken))!;
    }

    public async Task<CustomerDetailsDto> UpdateCustomerAsync(Guid actorId, Guid customerId, CustomerUpdateRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.CustomersUpdate, cancellationToken);
        Validate(request.Name, request.Email, request.CreditLimit);
        var customer = await RequiredCustomer(customerId, cancellationToken);
        var old = Values(customer);
        customer.Name = request.Name.Trim();
        customer.NormalizedName = Normalize(request.Name);
        customer.PhoneNumber = Clean(request.PhoneNumber);
        customer.AlternatePhone = Clean(request.AlternatePhone);
        customer.Email = Clean(request.Email);
        customer.Address = Clean(request.Address);
        customer.City = Clean(request.City);
        customer.BusinessName = Clean(request.BusinessName);
        customer.NTN = Clean(request.NTN);
        customer.CreditLimit = Money(request.CreditLimit);
        customer.UpdatedAt = UtcNow();
        await Audit(actorId, "CustomerUpdated", "Customer", customer.Id, old, Values(customer), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        var actor = await repository.GetActorAsync(actorId, cancellationToken);
        return (await repository.GetCustomerDetailsAsync(customer.Id, actor!.BranchId, CanSelectBranch(actor), cancellationToken))!;
    }

    public async Task SetCustomerActiveAsync(Guid actorId, Guid customerId, bool active, CancellationToken cancellationToken = default)
    {
        await Require(actorId, active ? PermissionCatalog.CustomersActivate : PermissionCatalog.CustomersDeactivate, cancellationToken);
        var customer = await RequiredCustomer(customerId, cancellationToken);
        if (customer.IsActive == active) return;
        var old = new { customer.IsActive };
        customer.IsActive = active;
        customer.UpdatedAt = UtcNow();
        await Audit(actorId, active ? "CustomerActivated" : "CustomerDeactivated", "Customer", customer.Id, old, new { customer.IsActive }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<CustomerLedgerEntryDto>> ListLedgerAsync(Guid actorId, Guid customerId, CustomerLedgerQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.CustomersLedgerView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        await RequiredCustomer(customerId, cancellationToken);
        if (query.BranchId.HasValue) EnsureBranchAccess(actor, query.BranchId.Value);
        return await repository.ListLedgerAsync(customerId, query, actor.BranchId, CanSelectBranch(actor), cancellationToken);
    }

    public async Task<CustomerDetailsDto> RecordPaymentAsync(Guid actorId, CustomerPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.CustomersPaymentCreate, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        if (request.Amount <= 0) throw new RequestValidationException("Payment amount must be greater than zero.");
        if (!Enum.IsDefined(request.PaymentMethod)) throw new RequestValidationException("Payment method is invalid.");
        if (await repository.GetBranchAsync(request.BranchId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Branch is invalid or inactive.");
        await RequiredCustomer(request.CustomerId, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var receiptNumber = await repository.NextPaymentReceiptNumberAsync(request.PaymentDateUtc, ct);
            var payment = new CustomerPayment
            {
                ReceiptNumber = receiptNumber,
                CustomerId = request.CustomerId,
                BranchId = request.BranchId,
                Amount = Money(request.Amount),
                PaymentMethod = request.PaymentMethod,
                PaymentDateUtc = request.PaymentDateUtc.ToUniversalTime(),
                ReferenceNumber = Clean(request.ReferenceNumber),
                Notes = Clean(request.Notes),
                ReceivedByUserId = actorId
            };
            await repository.AddPaymentAsync(payment, ct);
            await repository.AddLedgerEntryAsync(new CustomerLedgerEntry
            {
                CustomerId = request.CustomerId,
                BranchId = request.BranchId,
                EntryType = CustomerLedgerEntryType.Payment,
                Amount = -payment.Amount,
                EntryDate = DateOnly.FromDateTime(payment.PaymentDateUtc),
                PaymentMethod = payment.PaymentMethod.ToString(),
                ReferenceNumber = payment.ReceiptNumber,
                ReferenceType = "CustomerPayment",
                ReferenceId = payment.Id,
                Notes = payment.Notes,
                CreatedByUserId = actorId
            }, ct);
            await Audit(actorId, "CustomerPaymentRecorded", "Customer", request.CustomerId, null, new { request.CustomerId, request.BranchId, payment.Amount, payment.PaymentMethod, payment.ReceiptNumber }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return (await repository.GetCustomerDetailsAsync(request.CustomerId, actor.BranchId, CanSelectBranch(actor), cancellationToken))!;
    }

    public async Task<CustomerDetailsDto> AdjustBalanceAsync(Guid actorId, CustomerAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.CustomersAdjustBalance, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        if (request.Amount <= 0) throw new RequestValidationException("Adjustment amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("Adjustment reason is required.");
        if (await repository.GetBranchAsync(request.BranchId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Branch is invalid or inactive.");
        await RequiredCustomer(request.CustomerId, cancellationToken);
        var type = request.Type == CustomerAdjustmentType.Debit ? CustomerLedgerEntryType.AdjustmentDebit : CustomerLedgerEntryType.AdjustmentCredit;
        var amount = request.Type == CustomerAdjustmentType.Debit ? request.Amount : -request.Amount;
        await AddLedger(actorId, request.CustomerId, request.BranchId, type, Money(amount), BusinessDate(), null, null, "CustomerBalanceAdjustment", request.Notes ?? request.Reason, "CustomerBalanceAdjusted", cancellationToken);
        return (await repository.GetCustomerDetailsAsync(request.CustomerId, actor.BranchId, CanSelectBranch(actor), cancellationToken))!;
    }

    public async Task<CustomerPaymentReceiptDto> PaymentReceiptAsync(Guid actorId, Guid paymentId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.CustomersLedgerView, cancellationToken);
        return await repository.GetPaymentReceiptAsync(paymentId, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Customer payment was not found.");
    }

    private async Task AddLedger(Guid actorId, Guid customerId, Guid branchId, CustomerLedgerEntryType type, decimal amount, DateOnly date, string? paymentMethod, string? referenceNumber, string referenceType, string? notes, string auditAction, CancellationToken cancellationToken)
    {
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            await repository.AddLedgerEntryAsync(new CustomerLedgerEntry
            {
                CustomerId = customerId,
                BranchId = branchId,
                EntryType = type,
                Amount = amount,
                EntryDate = date,
                PaymentMethod = paymentMethod,
                ReferenceNumber = Clean(referenceNumber),
                ReferenceType = referenceType,
                Notes = Clean(notes),
                CreatedByUserId = actorId
            }, ct);
            await Audit(actorId, auditAction, "Customer", customerId, null, new { customerId, branchId, type, amount, date }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
    }

    private async Task<User> Require(Guid actorId, string permission, CancellationToken cancellationToken)
    {
        var actor = await repository.GetActorAsync(actorId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private async Task<Customer> RequiredCustomer(Guid id, CancellationToken ct) =>
        await repository.GetCustomerAsync(id, ct) ?? throw new ResourceNotFoundException("Customer was not found.");

    private static bool CanSelectBranch(User actor) =>
        actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.UsersView) == true;

    private static void EnsureBranchAccess(User actor, Guid branchId)
    {
        if (!CanSelectBranch(actor) && actor.BranchId != branchId)
            throw new ForbiddenOperationException("You do not have access to this branch.");
    }

    private static void Validate(string name, string? email, decimal creditLimit)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2) throw new RequestValidationException("Customer name is required.");
        if (!string.IsNullOrWhiteSpace(email) && !Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new RequestValidationException("Enter a valid email address.");
        if (creditLimit < 0) throw new RequestValidationException("Credit limit cannot be negative.");
    }

    private DateOnly BusinessDate() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time")));
    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static object Values(Customer x) => new { x.CustomerCode, x.Name, x.PhoneNumber, x.AlternatePhone, x.Email, x.Address, x.City, x.BusinessName, x.NTN, x.OpeningBalance, x.CreditLimit, x.IsActive };
    private Task Audit(Guid actor, string action, string type, Guid id, object? old, object? current, CancellationToken ct) => repository.AddAuditAsync(new AuditLog
    { UserId = actor, Action = action, EntityType = type, EntityId = id, OldValues = old is null ? null : JsonSerializer.Serialize(old), NewValues = current is null ? null : JsonSerializer.Serialize(current) }, ct);
    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
    }
}
