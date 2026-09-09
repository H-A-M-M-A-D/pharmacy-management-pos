using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Customers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Customers;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class CustomerManagementTests
{
    [Fact]
    public async Task Create_customer_normalizes_name_and_records_positive_opening_balance()
    {
        var f = new Fixture(PermissionCatalog.CustomersCreate);
        var result = await f.Service.CreateCustomerAsync(f.Actor.Id, Request(name: " Ali Traders ", opening: 10000));
        Assert.Equal("ALI TRADERS", f.Customer!.NormalizedName);
        Assert.Equal(10000, result.OutstandingBalance);
        Assert.Equal(0, result.AdvanceBalance);
        Assert.Contains(f.Ledger, x => x.EntryType == CustomerLedgerEntryType.OpeningBalance && x.Amount == 10000);
        Assert.Contains(f.Audits, x => x.Action == "CustomerCreated");

        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(JournalSourceType.OpeningBalance, journal.SourceType);
        var receivable = journal.Lines.Single(x => x.Account == AccountMappingKey.AccountsReceivable);
        Assert.Equal(10000, receivable.Debit);
        Assert.Equal(f.Customer!.Id, receivable.CustomerId);
        Assert.Equal(10000, journal.Lines.Single(x => x.Account == AccountMappingKey.RetainedEarnings).Credit);
    }

    [Fact]
    public async Task Negative_opening_balance_is_recorded_as_advance()
    {
        var f = new Fixture(PermissionCatalog.CustomersCreate);
        var result = await f.Service.CreateCustomerAsync(f.Actor.Id, Request(opening: -5000));
        Assert.Equal(5000, result.AdvanceBalance);
        Assert.Equal(0, result.OutstandingBalance);
        Assert.Contains(f.Ledger, x => x.EntryType == CustomerLedgerEntryType.OpeningBalance && x.Amount == -5000);

        var journal = Assert.Single(f.Journal.Posted);
        Assert.Equal(5000, journal.Lines.Single(x => x.Account == AccountMappingKey.RetainedEarnings).Debit);
        var receivable = journal.Lines.Single(x => x.Account == AccountMappingKey.AccountsReceivable);
        Assert.Equal(5000, receivable.Credit);
        Assert.Equal(f.Customer!.Id, receivable.CustomerId);
    }

    [Fact]
    public async Task Zero_opening_balance_creates_no_ledger_entry()
    {
        var f = new Fixture(PermissionCatalog.CustomersCreate);
        await f.Service.CreateCustomerAsync(f.Actor.Id, Request(opening: 0));
        Assert.Empty(f.Ledger);
        Assert.Empty(f.Journal.Posted);
    }

    [Fact]
    public async Task Invalid_name_email_and_credit_limit_are_rejected()
    {
        var f = new Fixture(PermissionCatalog.CustomersCreate);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateCustomerAsync(f.Actor.Id, Request(name: "A")));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateCustomerAsync(f.Actor.Id, Request(email: "bad")));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateCustomerAsync(f.Actor.Id, Request(credit: -1)));
    }

    [Fact]
    public async Task Edit_customer_does_not_change_opening_balance()
    {
        var f = new Fixture(PermissionCatalog.CustomersUpdate);
        f.ExistingCustomer(opening: 700);
        await f.Service.UpdateCustomerAsync(f.Actor.Id, f.Customer!.Id, new("Updated Traders", null, null, null, null, null, null, null, 5000));
        Assert.Equal(700, f.Customer.OpeningBalance);
        Assert.Equal("UPDATED TRADERS", f.Customer.NormalizedName);
        Assert.Equal(5000, f.Customer.CreditLimit);
    }

    [Fact]
    public async Task Payment_reduces_balance_and_adjustments_derive_signs()
    {
        var f = new Fixture(PermissionCatalog.CustomersPaymentCreate, PermissionCatalog.CustomersAdjustBalance);
        f.ExistingCustomer();
        await f.Service.RecordPaymentAsync(f.Actor.Id, new(f.Customer!.Id, f.Branch.Id, 4000, DateTime.UtcNow, CustomerPaymentMethod.Cash, "R-1", null));
        await f.Service.AdjustBalanceAsync(f.Actor.Id, new(f.Customer.Id, f.Branch.Id, CustomerAdjustmentType.Debit, 2000, "correction", null));
        await f.Service.AdjustBalanceAsync(f.Actor.Id, new(f.Customer.Id, f.Branch.Id, CustomerAdjustmentType.Credit, 1500, "correction", null));
        Assert.Contains(f.Ledger, x => x.EntryType == CustomerLedgerEntryType.Payment && x.Amount == -4000);
        Assert.Contains(f.Ledger, x => x.EntryType == CustomerLedgerEntryType.AdjustmentDebit && x.Amount == 2000);
        Assert.Contains(f.Ledger, x => x.EntryType == CustomerLedgerEntryType.AdjustmentCredit && x.Amount == -1500);
        Assert.Equal(-3500, f.Balance);
        Assert.Single(f.Payments);

        Assert.Equal(3, f.Journal.Posted.Count);
        var journal = Assert.Single(f.Journal.Posted, x => x.SourceType == JournalSourceType.CustomerPayment);
        Assert.Equal(JournalSourceType.CustomerPayment, journal.SourceType);
        Assert.Equal(f.Payments.Single().Id, journal.SourceId);
        Assert.Equal(4000, journal.Lines.Single(x => x.Account == AccountMappingKey.Cash).Debit);
        var receivable = journal.Lines.Single(x => x.Account == AccountMappingKey.AccountsReceivable);
        Assert.Equal(4000, receivable.Credit);
        Assert.Equal(f.Customer!.Id, receivable.CustomerId);
        var debitAdjustment = f.Journal.Posted.Single(x => x.SourceType == JournalSourceType.CustomerAdjustment &&
            x.Lines.Any(line => line.Account == AccountMappingKey.AccountsReceivable && line.Debit == 2000));
        Assert.Equal(2000, debitAdjustment.Lines.Single(x => x.Account == AccountMappingKey.AccountsReceivableAdjustmentSuspense).Credit);
        var creditAdjustment = f.Journal.Posted.Single(x => x.SourceType == JournalSourceType.CustomerAdjustment &&
            x.Lines.Any(line => line.Account == AccountMappingKey.AccountsReceivable && line.Credit == 1500));
        Assert.Equal(1500, creditAdjustment.Lines.Single(x => x.Account == AccountMappingKey.AccountsReceivableAdjustmentSuspense).Debit);
    }

    [Fact]
    public async Task Customer_payment_journal_posting_failure_rolls_back_the_entire_payment()
    {
        var f = new Fixture(PermissionCatalog.CustomersPaymentCreate) { Journal = { ThrowOnPost = true } };
        f.ExistingCustomer();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.RecordPaymentAsync(f.Actor.Id, new(f.Customer!.Id, f.Branch.Id, 4000, DateTime.UtcNow, CustomerPaymentMethod.Cash, "R-1", null)));
        Assert.Empty(f.Ledger);
        Assert.Empty(f.Payments);
    }

    [Fact]
    public async Task Payment_and_adjustment_validate_amount_reason_and_branch()
    {
        var f = new Fixture(PermissionCatalog.CustomersPaymentCreate, PermissionCatalog.CustomersAdjustBalance);
        f.ExistingCustomer();
        var customerId = f.Customer!.Id;
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.RecordPaymentAsync(f.Actor.Id, new(customerId, f.Branch.Id, 0, DateTime.UtcNow, CustomerPaymentMethod.Cash, null, null)));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.AdjustBalanceAsync(f.Actor.Id, new(customerId, f.Branch.Id, CustomerAdjustmentType.Debit, 0, "reason", null)));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.AdjustBalanceAsync(f.Actor.Id, new(customerId, f.Branch.Id, CustomerAdjustmentType.Debit, 1, "", null)));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.RecordPaymentAsync(f.Actor.Id, new(customerId, Guid.NewGuid(), 1, DateTime.UtcNow, CustomerPaymentMethod.Cash, null, null)));
    }

    [Fact]
    public async Task Status_actions_and_lookup_require_permissions()
    {
        var f = new Fixture(PermissionCatalog.CustomersDeactivate, PermissionCatalog.CustomersActivate, PermissionCatalog.CustomersView);
        f.ExistingCustomer();
        await f.Service.SetCustomerActiveAsync(f.Actor.Id, f.Customer!.Id, false);
        Assert.False(f.Customer.IsActive);
        await f.Service.LookupCustomersAsync(f.Actor.Id, null);
        var denied = new Fixture();
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => denied.Service.LookupCustomersAsync(denied.Actor.Id, null));
    }

    [Fact]
    public void Customer_ledger_entries_are_immutable_and_sign_checked()
    {
        Assert.Throws<InvalidOperationException>(() => CustomerLedgerEntry.ValidateAmountForEntryType(CustomerLedgerEntryType.Payment, 1));
        Assert.Throws<InvalidOperationException>(() => CustomerLedgerEntry.ValidateAmountForEntryType(CustomerLedgerEntryType.CreditSale, -1));
        CustomerLedgerEntry.ValidateAmountForEntryType(CustomerLedgerEntryType.OpeningBalance, -1);
    }

    private static CustomerRequest Request(string name = "Ali Traders", string? email = null, decimal opening = 0, decimal? credit = null) =>
        new(name, "03001234567", null, email, "Street", "Lahore", null, null, opening, credit ?? 0);

    private sealed class Fixture : ICustomerRepository
    {
        public readonly Branch Branch = new() { Code = "MAIN", Name = "Main" };
        public readonly User Actor;
        public readonly List<CustomerLedgerEntry> Ledger = [];
        public readonly List<CustomerPayment> Payments = [];
        public readonly List<AuditLog> Audits = [];
        public readonly FakeJournalPostingService Journal = new();
        public Customer? Customer;
        public CustomerService Service { get; }
        public decimal Balance => Ledger.Sum(x => x.Amount);

        public Fixture(params string[] permissions)
        {
            var role = new Role { Name = RoleCatalog.StoreKeeper };
            foreach (var permission in permissions)
                role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "actor", NormalizedUsername = "ACTOR", FullName = "Actor", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role };
            Service = new(this, Journal, TimeProvider.System);
        }

        public void ExistingCustomer(decimal opening = 0, decimal creditLimit = 100000)
        {
            Customer = new Customer { CustomerCode = "CUS-000001", Name = "Ali Traders", NormalizedName = "ALI TRADERS", OpeningBalance = opening, CreditLimit = creditLimit };
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(Branch.Id == branchId ? Branch : null);
        public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<Customer?>(Customer is not null && Customer.Id == customerId ? Customer : null);
        public Task<bool> CustomerCodeExistsAsync(string customerCode, Guid? excludingId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<string> NextCustomerCodeAsync(CancellationToken cancellationToken = default) => Task.FromResult("CUS-000001");
        public Task<string> NextPaymentReceiptNumberAsync(DateTime paymentDateUtc, CancellationToken cancellationToken = default) => Task.FromResult($"CR-{paymentDateUtc.Year}-000001");
        public Task AddCustomerAsync(Customer customer, CancellationToken cancellationToken = default) { Customer = customer; return Task.CompletedTask; }
        public Task AddPaymentAsync(CustomerPayment payment, CancellationToken cancellationToken = default) { Payments.Add(payment); return Task.CompletedTask; }
        public Task AddLedgerEntryAsync(CustomerLedgerEntry entry, CancellationToken cancellationToken = default) { Ledger.Add(entry); return Task.CompletedTask; }
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public Task<PagedResult<CustomerListItemDto>> ListCustomersAsync(CustomerListQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<CustomerListItemDto>([], query.Page, query.PageSize, 0));
        public Task<CustomerDetailsDto?> GetCustomerDetailsAsync(Guid customerId, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult<CustomerDetailsDto?>(Customer is null ? null : new(Customer.Id, Customer.CustomerCode, Customer.Name, Customer.PhoneNumber, Customer.AlternatePhone, Customer.Email, Customer.Address, Customer.City, Customer.BusinessName, Customer.NTN, Customer.OpeningBalance, Customer.CreditLimit, Customer.IsActive, Balance > 0 ? Balance : 0, Balance < 0 ? -Balance : 0, Ledger.Where(x => x.EntryType == CustomerLedgerEntryType.Payment).Sum(x => -x.Amount), null, Customer.CreatedAt, Customer.UpdatedAt));
        public Task<IReadOnlyList<CustomerLookupDto>> LookupCustomersAsync(string? search, bool activeOnly, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerLookupDto>>([]);
        public Task<PagedResult<CustomerLedgerEntryDto>> ListLedgerAsync(Guid customerId, CustomerLedgerQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<CustomerLedgerEntryDto>([], query.Page, query.PageSize, 0));
        public Task<CustomerPaymentReceiptDto?> GetPaymentReceiptAsync(Guid paymentId, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => Task.FromResult<CustomerPaymentReceiptDto?>(null);
        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
        {
            var snapshot = (Ledger.ToList(), Payments.ToList(), Audits.ToList());
            try { await operation(cancellationToken); }
            catch
            {
                Ledger.Clear(); Ledger.AddRange(snapshot.Item1);
                Payments.Clear(); Payments.AddRange(snapshot.Item2);
                Audits.Clear(); Audits.AddRange(snapshot.Item3);
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
