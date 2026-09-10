using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Suppliers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Accounting.PaymentAllocation;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Suppliers;

public sealed class SupplierService(ISupplierRepository repository, IJournalPostingService journalPosting, TimeProvider timeProvider) : ISupplierService
{
    public async Task<PagedResult<SupplierListItemDto>> ListSuppliersAsync(Guid actorId, SupplierListQuery query, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.SuppliersView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        if (!new[] { "name", "createdat", "outstandingbalance" }.Contains(query.SortBy.ToLowerInvariant()))
            throw new RequestValidationException("The requested supplier sort is not supported.");
        return await repository.ListSuppliersAsync(query, cancellationToken);
    }

    public async Task<SupplierDetailsDto> GetSupplierAsync(Guid actorId, Guid supplierId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SuppliersView, cancellationToken);
        return await repository.GetSupplierDetailsAsync(supplierId, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Supplier was not found.");
    }

    public async Task<SupplierDetailsDto> CreateSupplierAsync(Guid actorId, SupplierRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SuppliersCreate, cancellationToken);
        Validate(request.Name, request.Email, request.CreditLimit, request.PaymentTermsDays);
        var normalized = Normalize(request.Name);
        if (await repository.NormalizedNameExistsAsync(normalized, null, cancellationToken))
            throw new ResourceConflictException("A supplier with this name already exists.");
        Supplier? supplier = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            supplier = new Supplier
            {
                Name = request.Name.Trim(),
                NormalizedName = normalized,
                ShortName = Clean(request.ShortName),
                ContactPerson = Clean(request.ContactPerson),
                PhoneNumber = Clean(request.PhoneNumber),
                AlternatePhone = Clean(request.AlternatePhone),
                WhatsApp = Clean(request.WhatsApp),
                Email = Clean(request.Email),
                Address = Clean(request.Address),
                City = Clean(request.City),
                TaxNumber = Clean(request.NTN),
                STRN = Clean(request.STRN),
                OpeningBalance = request.OpeningBalance,
                CreditLimit = request.CreditLimit,
                PaymentTermsDays = request.PaymentTermsDays,
                IsActive = request.IsActive
            };
            await repository.AddSupplierAsync(supplier, ct);
            await Audit(actorId, "SupplierCreated", "Supplier", supplier.Id, null, Values(supplier), ct);
            if (request.OpeningBalance != 0)
            {
                var opening = new SupplierLedgerEntry
                {
                    SupplierId = supplier.Id,
                    BranchId = actor.BranchId,
                    EntryType = SupplierLedgerEntryType.OpeningBalance,
                    Amount = request.OpeningBalance,
                    EntryDate = BusinessDate(),
                    ReferenceType = "SupplierOpeningBalance",
                    Notes = "Opening balance",
                    CreatedByUserId = actorId
                };
                await repository.AddLedgerEntryAsync(opening, ct);
                await PostSupplierOpeningBalanceJournalAsync(actor, supplier, opening, ct);
                await Audit(actorId, "SupplierOpeningBalanceRecorded", "Supplier", supplier.Id, null, new { supplier.Id, request.OpeningBalance, BranchId = actor.BranchId }, ct);
            }
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return (await repository.GetSupplierDetailsAsync(supplier!.Id, actor.BranchId, CanSelectBranch(actor), cancellationToken))!;
    }

    public async Task<SupplierDetailsDto> UpdateSupplierAsync(Guid actorId, Guid supplierId, SupplierUpdateRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.SuppliersUpdate, cancellationToken);
        Validate(request.Name, request.Email, request.CreditLimit, request.PaymentTermsDays);
        var supplier = await RequiredSupplier(supplierId, cancellationToken);
        var normalized = Normalize(request.Name);
        if (await repository.NormalizedNameExistsAsync(normalized, supplierId, cancellationToken))
            throw new ResourceConflictException("A supplier with this name already exists.");
        var old = Values(supplier);
        supplier.Name = request.Name.Trim();
        supplier.NormalizedName = normalized;
        supplier.ShortName = Clean(request.ShortName);
        supplier.ContactPerson = Clean(request.ContactPerson);
        supplier.PhoneNumber = Clean(request.PhoneNumber);
        supplier.AlternatePhone = Clean(request.AlternatePhone);
        supplier.WhatsApp = Clean(request.WhatsApp);
        supplier.Email = Clean(request.Email);
        supplier.Address = Clean(request.Address);
        supplier.City = Clean(request.City);
        supplier.TaxNumber = Clean(request.NTN);
        supplier.STRN = Clean(request.STRN);
        supplier.CreditLimit = request.CreditLimit;
        supplier.PaymentTermsDays = request.PaymentTermsDays;
        supplier.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await Audit(actorId, "SupplierUpdated", "Supplier", supplier.Id, old, Values(supplier), cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        var actor = await repository.GetActorAsync(actorId, cancellationToken);
        return (await repository.GetSupplierDetailsAsync(supplier.Id, actor!.BranchId, CanSelectBranch(actor), cancellationToken))!;
    }

    public async Task SetSupplierActiveAsync(Guid actorId, Guid supplierId, bool active, CancellationToken cancellationToken = default)
    {
        await Require(actorId, active ? PermissionCatalog.SuppliersActivate : PermissionCatalog.SuppliersDeactivate, cancellationToken);
        var supplier = await RequiredSupplier(supplierId, cancellationToken);
        if (supplier.IsActive == active) return;
        var old = new { supplier.IsActive };
        supplier.IsActive = active;
        supplier.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await Audit(actorId, active ? "SupplierActivated" : "SupplierDeactivated", "Supplier", supplier.Id, old, new { supplier.IsActive }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierLookupDto>> LookupSuppliersAsync(Guid actorId, string? search, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.SuppliersView, cancellationToken);
        return await repository.LookupSuppliersAsync(search, activeOnly, cancellationToken);
    }

    public async Task<PagedResult<SupplierLedgerEntryDto>> ListLedgerAsync(Guid actorId, Guid supplierId, SupplierLedgerQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SuppliersLedgerView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        await RequiredSupplier(supplierId, cancellationToken);
        if (query.BranchId.HasValue) EnsureBranchAccess(actor, query.BranchId.Value);
        return await repository.ListLedgerAsync(supplierId, query, actor.BranchId, CanSelectBranch(actor), cancellationToken);
    }

    public async Task<SupplierDetailsDto> RecordPaymentAsync(Guid actorId, SupplierPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SuppliersPaymentCreate, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        if (request.Amount <= 0) throw new RequestValidationException("Payment amount must be greater than zero.");
        if (await repository.GetBranchAsync(request.BranchId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Branch is invalid or inactive.");
        await RequiredSupplier(request.SupplierId, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var entry = new SupplierLedgerEntry
            {
                SupplierId = request.SupplierId,
                BranchId = request.BranchId,
                EntryType = SupplierLedgerEntryType.Payment,
                Amount = -request.Amount,
                EntryDate = request.PaymentDate,
                PaymentMethod = request.PaymentMethod.ToString(),
                ReferenceNumber = Clean(request.ReferenceNumber),
                ReferenceType = "SupplierPayment",
                Notes = Clean(request.Notes),
                CreatedByUserId = actorId,
                FinancialAccountId = request.FinancialAccountId
            };
            await repository.AddLedgerEntryAsync(entry, ct);
            await SupplierPaymentAllocator.AllocateFifoAsync(repository.GetOpenPayablesAsync, repository.AddPaymentAllocationAsync,
                request.SupplierId, request.BranchId, entry.Id, request.Amount, actorId, timeProvider.GetUtcNow().UtcDateTime, ct);
            await PostSupplierPaymentJournalAsync(actor, request, entry, ct);
            await Audit(actorId, "SupplierPaymentRecorded", "Supplier", request.SupplierId, null, new { request.SupplierId, request.BranchId, SupplierLedgerEntryType.Payment, Amount = -request.Amount, request.PaymentDate }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return (await repository.GetSupplierDetailsAsync(request.SupplierId, actor.BranchId, CanSelectBranch(actor), cancellationToken))!;
    }

    public async Task<SupplierDetailsDto> AdjustBalanceAsync(Guid actorId, SupplierAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SuppliersAdjustBalance, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        if (request.Amount <= 0) throw new RequestValidationException("Adjustment amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("Adjustment reason is required.");
        if (await repository.GetBranchAsync(request.BranchId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Branch is invalid or inactive.");
        await RequiredSupplier(request.SupplierId, cancellationToken);
        var type = request.Type == SupplierAdjustmentType.Debit ? SupplierLedgerEntryType.AdjustmentDebit : SupplierLedgerEntryType.AdjustmentCredit;
        var amount = request.Type == SupplierAdjustmentType.Debit ? request.Amount : -request.Amount;
        await AddLedger(actorId, request.SupplierId, request.BranchId, type, amount, BusinessDate(), null, null, "SupplierBalanceAdjustment", request.Notes ?? request.Reason, "SupplierBalanceAdjusted", cancellationToken);
        return (await repository.GetSupplierDetailsAsync(request.SupplierId, actor.BranchId, CanSelectBranch(actor), cancellationToken))!;
    }

    private async Task AddLedger(Guid actorId, Guid supplierId, Guid branchId, SupplierLedgerEntryType type, decimal amount, DateOnly date, string? paymentMethod, string? referenceNumber, string referenceType, string? notes, string auditAction, CancellationToken cancellationToken, Guid? financialAccountId = null)
    {
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var entry = new SupplierLedgerEntry
            {
                SupplierId = supplierId,
                BranchId = branchId,
                EntryType = type,
                Amount = amount,
                EntryDate = date,
                PaymentMethod = paymentMethod,
                ReferenceNumber = Clean(referenceNumber),
                ReferenceType = referenceType,
                Notes = Clean(notes),
                CreatedByUserId = actorId,
                FinancialAccountId = financialAccountId
            };
            await repository.AddLedgerEntryAsync(entry, ct);
            await PostSupplierAdjustmentJournalAsync(entry, ct);
            await Audit(actorId, auditAction, "Supplier", supplierId, null, new { supplierId, branchId, type, amount, date }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
    }

    private async Task PostSupplierPaymentJournalAsync(User actor, SupplierPaymentRequest request, SupplierLedgerEntry entry, CancellationToken ct)
    {
        await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.SupplierPayment, entry.Id, request.BranchId, timeProvider.GetUtcNow().UtcDateTime,
            entry.ReferenceNumber, $"Supplier payment to {request.SupplierId}", actor.Id,
            [new(AccountMappingKey.AccountsPayable, request.Amount, 0, SupplierId: request.SupplierId), new(PaymentAccount(request.PaymentMethod), 0, request.Amount)]), ct);
    }

    private async Task PostSupplierAdjustmentJournalAsync(SupplierLedgerEntry entry, CancellationToken ct)
    {
        var amount = Math.Abs(entry.Amount);
        var lines = entry.Amount > 0
            ? new List<JournalLineInput>
            {
                new(AccountMappingKey.AccountsPayableAdjustmentSuspense, amount, 0),
                new(AccountMappingKey.AccountsPayable, 0, amount, SupplierId: entry.SupplierId)
            }
            : new List<JournalLineInput>
            {
                new(AccountMappingKey.AccountsPayable, amount, 0, SupplierId: entry.SupplierId),
                new(AccountMappingKey.AccountsPayableAdjustmentSuspense, 0, amount)
            };
        await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.SupplierAdjustment, entry.Id, entry.BranchId, timeProvider.GetUtcNow().UtcDateTime,
            entry.ReferenceType, entry.Notes ?? "Supplier balance adjustment", entry.CreatedByUserId ?? throw new InvalidOperationException("Adjustment creator is required."), lines), ct);
    }

    private async Task PostSupplierOpeningBalanceJournalAsync(User actor, Supplier supplier, SupplierLedgerEntry entry, CancellationToken ct)
    {
        var amount = Math.Abs(supplier.OpeningBalance);
        var lines = supplier.OpeningBalance > 0
            ? new List<JournalLineInput> { new(AccountMappingKey.RetainedEarnings, amount, 0), new(AccountMappingKey.AccountsPayable, 0, amount, SupplierId: supplier.Id) }
            : new List<JournalLineInput> { new(AccountMappingKey.AccountsPayable, amount, 0, SupplierId: supplier.Id), new(AccountMappingKey.RetainedEarnings, 0, amount) };
        await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.OpeningBalance, entry.Id, entry.BranchId, timeProvider.GetUtcNow().UtcDateTime,
            "Opening balance", $"Opening balance for {supplier.Name}", actor.Id, lines), ct);
    }

    private static AccountMappingKey PaymentAccount(SupplierPaymentMethod method) => method == SupplierPaymentMethod.Cash ? AccountMappingKey.Cash : AccountMappingKey.Bank;

    private async Task<User> Require(Guid actorId, string permission, CancellationToken cancellationToken)
    {
        var actor = await repository.GetActorAsync(actorId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private async Task<Supplier> RequiredSupplier(Guid id, CancellationToken ct) =>
        await repository.GetSupplierAsync(id, ct) ?? throw new ResourceNotFoundException("Supplier was not found.");

    private static bool CanSelectBranch(User actor) =>
        actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.UsersView) == true;

    private static void EnsureBranchAccess(User actor, Guid branchId)
    {
        if (!CanSelectBranch(actor) && actor.BranchId != branchId)
            throw new ForbiddenOperationException("You do not have access to this branch.");
    }

    private static void Validate(string name, string? email, decimal? creditLimit, int? paymentTermsDays)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2) throw new RequestValidationException("Supplier name is required.");
        if (!string.IsNullOrWhiteSpace(email) && !Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new RequestValidationException("Enter a valid email address.");
        if (creditLimit < 0) throw new RequestValidationException("Credit limit cannot be negative.");
        if (paymentTermsDays < 0) throw new RequestValidationException("Payment terms days cannot be negative.");
    }

    private DateOnly BusinessDate() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time")));
    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static object Values(Supplier x) => new { x.Name, x.ShortName, x.ContactPerson, x.PhoneNumber, x.AlternatePhone, x.WhatsApp, x.Email, x.Address, x.City, NTN = x.TaxNumber, x.STRN, x.OpeningBalance, x.CreditLimit, x.PaymentTermsDays, x.IsActive };
    private Task Audit(Guid actor, string action, string type, Guid id, object? old, object? current, CancellationToken ct) => repository.AddAuditAsync(new AuditLog
    { UserId = actor, Action = action, EntityType = type, EntityId = id, OldValues = old is null ? null : JsonSerializer.Serialize(old), NewValues = current is null ? null : JsonSerializer.Serialize(current) }, ct);
    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
    }
}
