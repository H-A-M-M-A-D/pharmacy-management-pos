using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Suppliers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Suppliers;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class SupplierManagementTests
{
    [Fact]
    public async Task Create_supplier_normalizes_name_and_records_positive_opening_balance()
    {
        var f = new Fixture(PermissionCatalog.SuppliersCreate);
        var result = await f.Service.CreateSupplierAsync(f.Actor.Id, Request(name: " ABC Pharma ", opening: 10000));
        Assert.Equal("ABC PHARMA", f.Supplier!.NormalizedName);
        Assert.Equal(10000, result.OutstandingBalance);
        Assert.Contains(f.Ledger, x => x.EntryType == SupplierLedgerEntryType.OpeningBalance && x.Amount == 10000);
        Assert.Contains(f.Audits, x => x.Action == "SupplierCreated");

        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(JournalSourceType.OpeningBalance, journal.SourceType);
        Assert.Equal(10000, journal.Lines.Single(x => x.Account == AccountMappingKey.RetainedEarnings).Debit);
        var payable = journal.Lines.Single(x => x.Account == AccountMappingKey.AccountsPayable);
        Assert.Equal(10000, payable.Credit);
        Assert.Equal(f.Supplier!.Id, payable.SupplierId);
    }

    [Fact]
    public async Task Negative_opening_balance_is_recorded_as_advance()
    {
        var f = new Fixture(PermissionCatalog.SuppliersCreate);
        var result = await f.Service.CreateSupplierAsync(f.Actor.Id, Request(opening: -5000));
        Assert.Equal(-5000, result.OutstandingBalance);
        Assert.Contains(f.Ledger, x => x.EntryType == SupplierLedgerEntryType.OpeningBalance && x.Amount == -5000);

        var journal = Assert.Single(f.Journal.Posted);
        var payable = journal.Lines.Single(x => x.Account == AccountMappingKey.AccountsPayable);
        Assert.Equal(5000, payable.Debit);
        Assert.Equal(f.Supplier!.Id, payable.SupplierId);
        Assert.Equal(5000, journal.Lines.Single(x => x.Account == AccountMappingKey.RetainedEarnings).Credit);
    }

    [Fact]
    public async Task Zero_opening_balance_creates_no_ledger_entry()
    {
        var f = new Fixture(PermissionCatalog.SuppliersCreate);
        await f.Service.CreateSupplierAsync(f.Actor.Id, Request(opening: 0));
        Assert.Empty(f.Ledger);
        Assert.Empty(f.Journal.Posted);
    }

    [Fact]
    public async Task Duplicate_name_invalid_email_and_financial_settings_are_rejected()
    {
        var f = new Fixture(PermissionCatalog.SuppliersCreate);
        f.NameExists = true;
        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.CreateSupplierAsync(f.Actor.Id, Request()));
        f.NameExists = false;
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateSupplierAsync(f.Actor.Id, Request(email: "bad")));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateSupplierAsync(f.Actor.Id, Request(credit: -1)));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateSupplierAsync(f.Actor.Id, Request(days: -1)));
    }

    [Fact]
    public async Task Edit_supplier_does_not_change_opening_balance()
    {
        var f = new Fixture(PermissionCatalog.SuppliersUpdate);
        f.ExistingSupplier(opening: 700);
        await f.Service.UpdateSupplierAsync(f.Actor.Id, f.Supplier!.Id, new("Updated", null, null, null, null, null, null, null, null, null, null, 5000, 30));
        Assert.Equal(700, f.Supplier.OpeningBalance);
        Assert.Equal("UPDATED", f.Supplier.NormalizedName);
    }

    [Fact]
    public async Task Payment_reduces_balance_and_adjustments_derive_signs()
    {
        var f = new Fixture(PermissionCatalog.SuppliersPaymentCreate, PermissionCatalog.SuppliersAdjustBalance);
        f.ExistingSupplier();
        await f.Service.RecordPaymentAsync(f.Actor.Id, new(f.Supplier!.Id, f.Branch.Id, 4000, f.Today, SupplierPaymentMethod.Cash, "R-1", null));
        await f.Service.AdjustBalanceAsync(f.Actor.Id, new(f.Supplier.Id, f.Branch.Id, SupplierAdjustmentType.Debit, 2000, "correction", null));
        await f.Service.AdjustBalanceAsync(f.Actor.Id, new(f.Supplier.Id, f.Branch.Id, SupplierAdjustmentType.Credit, 1500, "correction", null));
        Assert.Contains(f.Ledger, x => x.EntryType == SupplierLedgerEntryType.Payment && x.Amount == -4000);
        Assert.Contains(f.Ledger, x => x.EntryType == SupplierLedgerEntryType.AdjustmentDebit && x.Amount == 2000);
        Assert.Contains(f.Ledger, x => x.EntryType == SupplierLedgerEntryType.AdjustmentCredit && x.Amount == -1500);
        Assert.Equal(-3500, f.Balance);

        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(JournalSourceType.SupplierPayment, journal.SourceType);
        Assert.Equal(4000, journal.Lines.Single(x => x.Account == AccountMappingKey.AccountsPayable).Debit);
        Assert.Equal(4000, journal.Lines.Single(x => x.Account == AccountMappingKey.Cash).Credit);
    }

    [Fact]
    public async Task Supplier_payment_journal_posting_failure_rolls_back_the_entire_payment()
    {
        var f = new Fixture(PermissionCatalog.SuppliersPaymentCreate) { Journal = { ThrowOnPost = true } };
        f.ExistingSupplier();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.RecordPaymentAsync(f.Actor.Id, new(f.Supplier!.Id, f.Branch.Id, 4000, f.Today, SupplierPaymentMethod.Cash, "R-1", null)));
        Assert.Empty(f.Ledger);
    }

    [Fact]
    public async Task Payment_and_adjustment_validate_amount_reason_and_branch()
    {
        var f = new Fixture(PermissionCatalog.SuppliersPaymentCreate, PermissionCatalog.SuppliersAdjustBalance);
        f.ExistingSupplier();
        var supplierId = f.Supplier!.Id;
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.RecordPaymentAsync(f.Actor.Id, new(supplierId, f.Branch.Id, 0, f.Today, SupplierPaymentMethod.Cash, null, null)));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.AdjustBalanceAsync(f.Actor.Id, new(supplierId, f.Branch.Id, SupplierAdjustmentType.Debit, 0, "reason", null)));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.AdjustBalanceAsync(f.Actor.Id, new(supplierId, f.Branch.Id, SupplierAdjustmentType.Debit, 1, "", null)));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.RecordPaymentAsync(f.Actor.Id, new(supplierId, Guid.NewGuid(), 1, f.Today, SupplierPaymentMethod.Cash, null, null)));
    }

    [Fact]
    public async Task Status_actions_and_lookup_require_permissions()
    {
        var f = new Fixture(PermissionCatalog.SuppliersDeactivate, PermissionCatalog.SuppliersActivate, PermissionCatalog.SuppliersView);
        f.ExistingSupplier();
        await f.Service.SetSupplierActiveAsync(f.Actor.Id, f.Supplier!.Id, false);
        Assert.False(f.Supplier.IsActive);
        await f.Service.LookupSuppliersAsync(f.Actor.Id, null);
        var denied = new Fixture();
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => denied.Service.LookupSuppliersAsync(denied.Actor.Id, null));
    }

    [Fact]
    public void Supplier_ledger_entries_are_immutable_and_sign_checked()
    {
        Assert.Throws<InvalidOperationException>(() => SupplierLedgerEntry.ValidateAmountForEntryType(SupplierLedgerEntryType.Payment, 1));
        SupplierLedgerEntry.ValidateAmountForEntryType(SupplierLedgerEntryType.OpeningBalance, -1);
    }

    private static SupplierRequest Request(string name = "ABC Pharma", string? email = null, decimal opening = 0, decimal? credit = null, int? days = null) =>
        new(name, "ABC", "Ali", "+923001234567", null, "03001234567", email, "Street", "Lahore", "ntn", "strn", opening, credit, days);

    private sealed class Fixture : ISupplierRepository
    {
        public readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        public readonly Branch Branch = new() { Code = "MAIN", Name = "Main" };
        public readonly User Actor;
        public readonly List<SupplierLedgerEntry> Ledger = [];
        public readonly List<AuditLog> Audits = [];
        public readonly FakeJournalPostingService Journal = new();
        public Supplier? Supplier;
        public bool NameExists;
        public SupplierService Service { get; }
        public decimal Balance => Ledger.Sum(x => x.Amount);

        public Fixture(params string[] permissions)
        {
            var role = new Role { Name = RoleCatalog.StoreKeeper };
            foreach (var permission in permissions)
                role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "actor", NormalizedUsername = "ACTOR", FullName = "Actor", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role };
            Service = new(this, Journal, TimeProvider.System);
        }

        public void ExistingSupplier(decimal opening = 0)
        {
            Supplier = new Supplier { Name = "ABC Pharma", NormalizedName = "ABC PHARMA", OpeningBalance = opening };
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Supplier?> GetSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default) => Task.FromResult<Supplier?>(Supplier is not null && Supplier.Id == supplierId ? Supplier : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(Branch.Id == branchId ? Branch : null);
        public Task<bool> NormalizedNameExistsAsync(string normalizedName, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(NameExists);
        public Task AddSupplierAsync(Supplier supplier, CancellationToken cancellationToken = default) { Supplier = supplier; return Task.CompletedTask; }
        public Task AddLedgerEntryAsync(SupplierLedgerEntry entry, CancellationToken cancellationToken = default) { Ledger.Add(entry); return Task.CompletedTask; }
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public Task<PagedResult<SupplierListItemDto>> ListSuppliersAsync(SupplierListQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<SupplierListItemDto>([], query.Page, query.PageSize, 0));
        public Task<SupplierDetailsDto?> GetSupplierDetailsAsync(Guid supplierId, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult<SupplierDetailsDto?>(Supplier is null ? null : new(Supplier.Id, Supplier.Name, Supplier.ShortName, Supplier.ContactPerson, Supplier.PhoneNumber, Supplier.AlternatePhone, Supplier.WhatsApp, Supplier.Email, Supplier.Address, Supplier.City, Supplier.TaxNumber, Supplier.STRN, Supplier.OpeningBalance, Supplier.CreditLimit, Supplier.PaymentTermsDays, Supplier.IsActive, Balance, Ledger.Where(x => x.EntryType == SupplierLedgerEntryType.Payment).Sum(x => -x.Amount), null, Supplier.CreatedAt, Supplier.UpdatedAt));
        public Task<IReadOnlyList<SupplierLookupDto>> LookupSuppliersAsync(string? search, bool activeOnly, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SupplierLookupDto>>([]);
        public Task<PagedResult<SupplierLedgerEntryDto>> ListLedgerAsync(Guid supplierId, SupplierLedgerQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<SupplierLedgerEntryDto>([], query.Page, query.PageSize, 0));
        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
        {
            var snapshot = (Ledger.ToList(), Audits.ToList());
            try { await operation(cancellationToken); }
            catch
            {
                Ledger.Clear(); Ledger.AddRange(snapshot.Item1);
                Audits.Clear(); Audits.AddRange(snapshot.Item2);
                throw;
            }
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeJournalPostingService : IJournalPostingService
    {
        public readonly List<JournalPostingRequest> Posted = [];
        public bool ThrowOnPost;
        public Task PostAsync(JournalPostingRequest request, CancellationToken cancellationToken = default)
        {
            if (ThrowOnPost) throw new InvalidOperationException("forced journal failure");
            Posted.Add(request);
            return Task.CompletedTask;
        }
    }
}
