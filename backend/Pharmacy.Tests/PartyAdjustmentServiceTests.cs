using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Customers;
using Pharmacy.Application.DTOs.Suppliers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Accounting.PartyAdjustments;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class PartyAdjustmentServiceTests
{
    [Fact]
    public async Task Credit_note_targeted_to_a_sale_rejects_amount_exceeding_outstanding()
    {
        var f = new Fixture(PermissionCatalog.AccountsCreditNotesCreate);
        var sale = f.AddSale(200);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateCreditNoteAsync(f.Actor.Id, new(
            f.Customer.Id, f.Branch.Id, 250, "Billing error", null, DateTime.UtcNow, sale.Id)));

        var note = await f.Service.CreateCreditNoteAsync(f.Actor.Id, new(f.Customer.Id, f.Branch.Id, 150, "Billing error", null, DateTime.UtcNow, sale.Id));
        Assert.Equal(150, note.Amount);
        Assert.Single(f.CustomerPaymentAllocations, x => x.SaleId == sale.Id && x.AllocatedAmount == 150);
        Assert.Single(f.JournalEntries, x => x.SourceType == JournalSourceType.CreditNote);
    }

    [Fact]
    public async Task Customer_write_off_allocates_fifo_across_open_sales_when_no_invoice_chosen()
    {
        var f = new Fixture(PermissionCatalog.AccountsWriteOffsCreate);
        var older = f.AddSale(100, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)));
        var newer = f.AddSale(100, DateOnly.FromDateTime(DateTime.UtcNow));
        var writeOff = await f.Service.CreateCustomerWriteOffAsync(f.Actor.Id, new(f.Customer.Id, f.Branch.Id, 120, "Uncollectible", null, DateTime.UtcNow, null));

        Assert.Equal(120, writeOff.Amount);
        Assert.Equal(100, f.CustomerPaymentAllocations.Single(x => x.SaleId == older.Id).AllocatedAmount);
        Assert.Equal(20, f.CustomerPaymentAllocations.Single(x => x.SaleId == newer.Id).AllocatedAmount);
        var journal = f.JournalEntries.Single(x => x.SourceType == JournalSourceType.CustomerWriteOff);
        Assert.Equal(120, journal.Lines.Sum(x => x.Debit));
        Assert.Equal(120, journal.Lines.Sum(x => x.Credit));
    }

    [Fact]
    public async Task Customer_advance_cannot_be_applied_beyond_its_remaining_balance()
    {
        var f = new Fixture(PermissionCatalog.AccountsAdvancesCreate, PermissionCatalog.AccountsAdvancesApply);
        var advance = await f.Service.RecordCustomerAdvanceAsync(f.Actor.Id, new(f.Customer.Id, f.Branch.Id, 500, f.CashAccount.Id, DateTime.UtcNow, null, null));
        Assert.Equal(500, advance.AmountRemaining);

        var sale = f.AddSale(1000);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ApplyCustomerAdvanceAsync(f.Actor.Id, advance.Id, new(sale.Id, 600)));

        var applied = await f.Service.ApplyCustomerAdvanceAsync(f.Actor.Id, advance.Id, new(sale.Id, 300));
        Assert.Equal(200, applied.AmountRemaining);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ApplyCustomerAdvanceAsync(f.Actor.Id, advance.Id, new(sale.Id, 300)));
    }

    [Fact]
    public async Task Supplier_write_off_posts_AP_income_and_settles_FIFO()
    {
        var f = new Fixture(PermissionCatalog.AccountsWriteOffsCreate);
        var older = f.AddGoodsReceipt(100, new(2026, 1, 1));
        var newer = f.AddGoodsReceipt(100, new(2026, 2, 1));
        var result = await f.Service.CreateSupplierWriteOffAsync(f.Actor.Id, new(f.Supplier.Id, f.Branch.Id, 120, "Waived balance", null, DateTime.UtcNow, null));
        Assert.Equal(100, Assert.Single(f.SupplierPaymentAllocations, x => x.GoodsReceiptId == older.Id).AllocatedAmount);
        Assert.Equal(20, Assert.Single(f.SupplierPaymentAllocations, x => x.GoodsReceiptId == newer.Id).AllocatedAmount);
        Assert.Equal(-120, Assert.Single(f.SupplierLedgerEntries).Amount);
        Assert.Equal(f.SupplierWriteOffs.Single(x => x.Id == result.Id).SupplierLedgerEntryId, f.SupplierLedgerEntries.Single().Id);
        var journal = Assert.Single(f.JournalEntries);
        Assert.Equal(JournalSourceType.SupplierWriteOff, journal.SourceType);
        Assert.Equal(120, journal.Lines.Sum(x => x.Debit));
        Assert.Equal(120, journal.Lines.Sum(x => x.Credit));
        Assert.Equal(f.Supplier.Id, Assert.Single(journal.Lines, x => x.Debit == 120).SupplierId);
        Assert.Empty(f.FinancialLedgerEntries);
    }

    [Fact]
    public async Task Supplier_debit_note_rejects_overallocation_and_posts_GL_and_supplier_ledger()
    {
        var f = new Fixture(PermissionCatalog.AccountsDebitNotesCreate);
        var bill = f.AddGoodsReceipt(200, new(2026, 1, 1));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateDebitNoteAsync(f.Actor.Id,
            new(f.Supplier.Id, f.Branch.Id, 250, "Supplier credit", null, DateTime.UtcNow, bill.Id)));
        Assert.Empty(f.SupplierLedgerEntries);
        var note = await f.Service.CreateDebitNoteAsync(f.Actor.Id, new(f.Supplier.Id, f.Branch.Id, 150, "Supplier credit", null, DateTime.UtcNow, bill.Id));
        Assert.Equal(150, note.Amount);
        Assert.Equal(-150, Assert.Single(f.SupplierLedgerEntries).Amount);
        Assert.Equal(150, Assert.Single(f.SupplierPaymentAllocations).AllocatedAmount);
        Assert.Equal(JournalSourceType.DebitNote, Assert.Single(f.JournalEntries).SourceType);
        Assert.Equal(50, Assert.Single(await f.GetOpenPayablesAsync(f.Supplier.Id, f.Branch.Id)).Outstanding);
    }

    [Fact]
    public async Task Supplier_advance_moves_cash_then_application_settles_AP_without_moving_cash_twice()
    {
        var f = new Fixture(PermissionCatalog.AccountsAdvancesCreate, PermissionCatalog.AccountsAdvancesApply);
        var advance = await f.Service.RecordSupplierAdvanceAsync(f.Actor.Id, new(f.Supplier.Id, f.Branch.Id, 500, f.CashAccount.Id, DateTime.UtcNow, null, null));
        Assert.Equal(-500, Assert.Single(f.FinancialLedgerEntries).Amount);
        Assert.Empty(f.SupplierLedgerEntries);
        Assert.Equal(JournalSourceType.SupplierAdvance, Assert.Single(f.JournalEntries).SourceType);
        var bill = f.AddGoodsReceipt(1000, new(2026, 1, 1));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ApplySupplierAdvanceAsync(f.Actor.Id, advance.Id, new(bill.Id, 600)));
        var applied = await f.Service.ApplySupplierAdvanceAsync(f.Actor.Id, advance.Id, new(bill.Id, 500));
        Assert.Equal(0, applied.AmountRemaining);
        Assert.Equal(-500, Assert.Single(f.SupplierLedgerEntries).Amount);
        Assert.Equal(500, Assert.Single(f.SupplierPaymentAllocations).AllocatedAmount);
        Assert.Single(f.FinancialLedgerEntries);
        Assert.Equal(2, f.JournalEntries.Count);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ApplySupplierAdvanceAsync(f.Actor.Id, advance.Id, new(bill.Id, 500)));
        Assert.Single(f.SupplierAdvanceApplications);
        Assert.Equal(2, f.JournalEntries.Count);
    }

    [Theory]
    [InlineData(JournalSourceType.DebitNote)]
    [InlineData(JournalSourceType.SupplierWriteOff)]
    public async Task Supplier_adjustment_reversal_restores_bill_ledger_and_GL_and_rejects_duplicate(JournalSourceType source)
    {
        var f = new Fixture(PermissionCatalog.AccountsDebitNotesCreate, PermissionCatalog.AccountsWriteOffsCreate, PermissionCatalog.AccountsJournalReverse);
        var bill = f.AddGoodsReceipt(200, new(2026, 1, 1));
        Guid id;
        if (source == JournalSourceType.DebitNote) id = (await f.Service.CreateDebitNoteAsync(f.Actor.Id, new(f.Supplier.Id, f.Branch.Id, 150, "Supplier credit", null, DateTime.UtcNow, bill.Id))).Id;
        else id = (await f.Service.CreateSupplierWriteOffAsync(f.Actor.Id, new(f.Supplier.Id, f.Branch.Id, 150, "Waived", null, DateTime.UtcNow, bill.Id))).Id;
        await f.Service.ReverseSupplierAdjustmentAsync(f.Actor.Id, source, id, new("Correction"));
        Assert.Empty(f.SupplierPaymentAllocations);
        Assert.Equal(200, Assert.Single(await f.GetOpenPayablesAsync(f.Supplier.Id, f.Branch.Id)).Outstanding);
        Assert.Equal(0, f.SupplierLedgerEntries.Sum(x => x.Amount));
        foreach (var account in f.JournalEntries.SelectMany(x => x.Lines).GroupBy(x => x.ChartOfAccountId)) Assert.Equal(0, account.Sum(x => x.Debit - x.Credit));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ReverseSupplierAdjustmentAsync(f.Actor.Id, source, id, new("Retry")));
        Assert.Equal(2, f.JournalEntries.Count);
        Assert.Equal(2, f.SupplierLedgerEntries.Count);
    }

    [Fact]
    public async Task Unapplied_supplier_advance_reversal_restores_cash_and_cannot_be_applied_or_reversed_twice()
    {
        var f = new Fixture(PermissionCatalog.AccountsAdvancesCreate, PermissionCatalog.AccountsAdvancesApply, PermissionCatalog.AccountsJournalReverse, PermissionCatalog.AccountsAdvancesView);
        var advance = await f.Service.RecordSupplierAdvanceAsync(f.Actor.Id, new(f.Supplier.Id, f.Branch.Id, 500, f.CashAccount.Id, DateTime.UtcNow, null, null));
        await f.Service.ReverseSupplierAdjustmentAsync(f.Actor.Id, JournalSourceType.SupplierAdvance, advance.Id, new("Cancelled order"));
        Assert.Equal(0, f.FinancialLedgerEntries.Sum(x => x.Amount));
        var listed = Assert.Single(await f.Service.ListSupplierAdvancesAsync(f.Actor.Id, f.Supplier.Id, f.Branch.Id));
        Assert.True(listed.IsReversed);
        Assert.Equal(0, listed.AmountRemaining);
        var bill = f.AddGoodsReceipt(1000, new(2026, 1, 1));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ApplySupplierAdvanceAsync(f.Actor.Id, advance.Id, new(bill.Id, 100)));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ReverseSupplierAdjustmentAsync(f.Actor.Id, JournalSourceType.SupplierAdvance, advance.Id, new("Retry")));
        Assert.Equal(2, f.FinancialLedgerEntries.Count);
    }

    private sealed class Fixture : IPartyAdjustmentRepository
    {
        public Task<JournalEntry?> GetJournalForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default) => Task.FromResult(JournalEntries.FirstOrDefault(x => x.SourceType == sourceType && x.SourceId == sourceId));
        public Task<bool> JournalHasReversalAsync(Guid journalId, CancellationToken cancellationToken = default) => Task.FromResult(JournalEntries.Any(x => x.ReversesJournalEntryId == journalId));
        public Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default) { JournalEntries.Add(entry); return Task.CompletedTask; }
        public Task<SupplierLedgerEntry?> GetSupplierSettlementAsync(Guid supplierId, string referenceNumber, CancellationToken cancellationToken = default) => Task.FromResult(SupplierLedgerEntries.FirstOrDefault(x => x.SupplierId == supplierId && x.ReferenceNumber == referenceNumber && x.Amount < 0));
        public Task<IReadOnlyList<SupplierPaymentAllocation>> RemoveSupplierAllocationsAsync(Guid ledgerEntryId, CancellationToken cancellationToken = default)
        {
            var allocations = SupplierPaymentAllocations.Where(x => x.SupplierLedgerEntryId == ledgerEntryId).ToList();
            SupplierPaymentAllocations.RemoveAll(x => x.SupplierLedgerEntryId == ledgerEntryId);
            return Task.FromResult<IReadOnlyList<SupplierPaymentAllocation>>(allocations);
        }
        public readonly Branch Branch = new() { Code = "MAIN", Name = "Main" };
        public readonly Customer Customer = new() { CustomerCode = "C-1", NormalizedName = "TEST CUSTOMER", Name = "Test Customer", IsActive = true };
        public readonly Supplier Supplier = new() { NormalizedName = "TEST SUPPLIER", Name = "Test Supplier", IsActive = true };
        public readonly FinancialAccount CashAccount;
        public readonly List<Sale> Sales = [];
        public readonly List<GoodsReceipt> GoodsReceipts = [];
        public readonly List<CustomerPayment> CustomerPayments = [];
        public readonly List<CustomerPaymentAllocation> CustomerPaymentAllocations = [];
        public readonly List<CustomerLedgerEntry> CustomerLedgerEntries = [];
        public readonly List<SupplierLedgerEntry> SupplierLedgerEntries = [];
        public readonly List<SupplierPaymentAllocation> SupplierPaymentAllocations = [];
        public readonly List<FinancialLedgerEntry> FinancialLedgerEntries = [];
        public readonly List<CreditNote> CreditNotes = [];
        public readonly List<DebitNote> DebitNotes = [];
        public readonly List<CustomerWriteOff> CustomerWriteOffs = [];
        public readonly List<SupplierWriteOff> SupplierWriteOffs = [];
        public readonly List<CustomerAdvance> CustomerAdvances = [];
        public readonly List<CustomerAdvanceApplication> CustomerAdvanceApplications = [];
        public readonly List<SupplierAdvance> SupplierAdvances = [];
        public readonly List<SupplierAdvanceApplication> SupplierAdvanceApplications = [];
        public readonly List<JournalEntry> JournalEntries = [];
        private int sequence;
        public User Actor { get; }
        public PartyAdjustmentService Service { get; }

        public Fixture(params string[] permissions)
        {
            var role = new Role { Name = RoleCatalog.Accountant };
            foreach (var permission in permissions) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "acct", NormalizedUsername = "ACCT", FullName = "Accountant", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role };
            CashAccount = new FinancialAccount { BranchId = Branch.Id, Name = "Cash", NormalizedName = "CASH", AccountType = FinancialAccountType.Cash, OpeningBalance = 10000, IsActive = true };
            var journalPosting = new JournalPostingService(new JournalPostingAdapter(this), TimeProvider.System);
            Service = new PartyAdjustmentService(this, journalPosting, TimeProvider.System);
        }

        public Sale AddSale(decimal creditAmount, DateOnly? postedDate = null)
        {
            var sale = new Sale
            {
                BranchId = Branch.Id, CustomerId = Customer.Id, CashierUserId = Actor.Id, InvoiceNumber = $"INV-{Sales.Count + 1}",
                Status = SaleStatus.Posted, CreditAmount = creditAmount, NetTotal = creditAmount,
                PostedAtUtc = (postedDate ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            };
            Sales.Add(sale);
            return sale;
        }

        public GoodsReceipt AddGoodsReceipt(decimal amount, DateOnly date)
        {
            var receipt = new GoodsReceipt { BranchId = Branch.Id, SupplierId = Supplier.Id, GrnNumber = $"GRN-{GoodsReceipts.Count}", ReceiptDate = date, Status = GoodsReceiptStatus.Posted, NetTotal = amount };
            GoodsReceipts.Add(receipt);
            return receipt;
        }

        public GoodsReceipt AddGoodsReceipt(decimal netTotal, DateOnly? receiptDate = null)
        {
            var receipt = new GoodsReceipt
            {
                BranchId = Branch.Id, SupplierId = Supplier.Id, GrnNumber = $"GRN-{GoodsReceipts.Count + 1}",
                Status = GoodsReceiptStatus.Posted, NetTotal = netTotal, ReceiptDate = receiptDate ?? DateOnly.FromDateTime(DateTime.UtcNow)
            };
            GoodsReceipts.Add(receipt);
            return receipt;
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(Branch.Id == branchId ? Branch : null);
        public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Customer?>(Customer.Id == id ? Customer : null);
        public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Supplier?>(Supplier.Id == id ? Supplier : null);
        public Task<Sale?> GetSaleAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Sales.FirstOrDefault(x => x.Id == id));
        public Task<GoodsReceipt?> GetGoodsReceiptAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(GoodsReceipts.FirstOrDefault(x => x.Id == id));
        public Task<FinancialAccount?> GetFinancialAccountAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<FinancialAccount?>(CashAccount.Id == id ? CashAccount : null);
        public Task<decimal> GetFinancialAccountBalanceAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(CashAccount.OpeningBalance + FinancialLedgerEntries.Where(x => x.FinancialAccountId == id).Sum(x => x.Amount));

        public Task<string> NextNumberAsync(string sequenceName, string prefix, DateTime dateUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult($"{prefix}-{dateUtc.Year}-{++sequence:000000}");
        public Task<string> GetJournalEntryNumberForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(JournalEntries.FirstOrDefault(x => x.SourceType == sourceType && x.SourceId == sourceId)?.EntryNumber ?? string.Empty);

        public Task<IReadOnlyList<OpenReceivableDto>> GetOpenReceivablesAsync(Guid customerId, Guid? branchId, CancellationToken cancellationToken = default)
        {
            var allocated = CustomerPaymentAllocations.GroupBy(x => x.SaleId).ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));
            var open = Sales.Where(s => s.CustomerId == customerId && s.Status == SaleStatus.Posted && s.CreditAmount > 0)
                .Select(s => new OpenReceivableDto(s.Id, customerId, s.BranchId, s.InvoiceNumber, DateOnly.FromDateTime(s.PostedAtUtc!.Value), s.DueDateUtc, s.CreditAmount,
                    decimal.Round(s.CreditAmount - allocated.GetValueOrDefault(s.Id), 2)))
                .Where(x => x.Outstanding > 0).OrderBy(x => x.DocumentDate).ToList();
            return Task.FromResult<IReadOnlyList<OpenReceivableDto>>(open);
        }

        public Task<IReadOnlyList<OpenPayableDto>> GetOpenPayablesAsync(Guid supplierId, Guid? branchId, CancellationToken cancellationToken = default)
        {
            var allocated = SupplierPaymentAllocations.GroupBy(x => x.GoodsReceiptId).ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));
            var open = GoodsReceipts.Where(r => r.SupplierId == supplierId && r.Status == GoodsReceiptStatus.Posted && r.NetTotal > 0)
                .Select(r => new OpenPayableDto(r.Id, supplierId, r.BranchId, r.GrnNumber, r.ReceiptDate, r.DueDate, r.NetTotal,
                    decimal.Round(r.NetTotal - allocated.GetValueOrDefault(r.Id), 2)))
                .Where(x => x.Outstanding > 0).OrderBy(x => x.DocumentDate).ToList();
            return Task.FromResult<IReadOnlyList<OpenPayableDto>>(open);
        }

        public Task AddCustomerPaymentAsync(CustomerPayment payment, CancellationToken cancellationToken = default) { CustomerPayments.Add(payment); return Task.CompletedTask; }
        public Task AddCustomerPaymentAllocationAsync(CustomerPaymentAllocation allocation, CancellationToken cancellationToken = default) { CustomerPaymentAllocations.Add(allocation); return Task.CompletedTask; }
        public Task AddCustomerLedgerEntryAsync(CustomerLedgerEntry entry, CancellationToken cancellationToken = default) { CustomerLedgerEntries.Add(entry); return Task.CompletedTask; }
        public Task AddSupplierLedgerEntryAsync(SupplierLedgerEntry entry, CancellationToken cancellationToken = default) { SupplierLedgerEntries.Add(entry); return Task.CompletedTask; }
        public Task AddSupplierPaymentAllocationAsync(SupplierPaymentAllocation allocation, CancellationToken cancellationToken = default) { SupplierPaymentAllocations.Add(allocation); return Task.CompletedTask; }
        public Task AddFinancialLedgerEntryAsync(FinancialLedgerEntry entry, CancellationToken cancellationToken = default) { FinancialLedgerEntries.Add(entry); return Task.CompletedTask; }

        public Task AddCreditNoteAsync(CreditNote note, CancellationToken cancellationToken = default) { CreditNotes.Add(note); return Task.CompletedTask; }
        public Task<IReadOnlyList<CreditNote>> ListCreditNotesAsync(Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CreditNote>>(CreditNotes);
        public Task AddDebitNoteAsync(DebitNote note, CancellationToken cancellationToken = default) { DebitNotes.Add(note); return Task.CompletedTask; }
        public Task<IReadOnlyList<DebitNote>> ListDebitNotesAsync(Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DebitNote>>(DebitNotes);
        public Task AddCustomerWriteOffAsync(CustomerWriteOff writeOff, CancellationToken cancellationToken = default) { CustomerWriteOffs.Add(writeOff); return Task.CompletedTask; }
        public Task<IReadOnlyList<CustomerWriteOff>> ListCustomerWriteOffsAsync(Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerWriteOff>>(CustomerWriteOffs);
        public Task AddSupplierWriteOffAsync(SupplierWriteOff writeOff, CancellationToken cancellationToken = default) { SupplierWriteOffs.Add(writeOff); return Task.CompletedTask; }
        public Task<IReadOnlyList<SupplierWriteOff>> ListSupplierWriteOffsAsync(Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SupplierWriteOff>>(SupplierWriteOffs);

        public Task AddCustomerAdvanceAsync(CustomerAdvance advance, CancellationToken cancellationToken = default) { CustomerAdvances.Add(advance); return Task.CompletedTask; }
        public Task<CustomerAdvance?> GetCustomerAdvanceAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(CustomerAdvances.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<CustomerAdvance>> ListCustomerAdvancesAsync(Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerAdvance>>(CustomerAdvances);
        public Task AddCustomerAdvanceApplicationAsync(CustomerAdvanceApplication application, CancellationToken cancellationToken = default)
        {
            CustomerAdvanceApplications.Add(application);
            CustomerAdvances.Single(x => x.Id == application.CustomerAdvanceId).Applications.Add(application);
            return Task.CompletedTask;
        }

        public Task AddSupplierAdvanceAsync(SupplierAdvance advance, CancellationToken cancellationToken = default) { SupplierAdvances.Add(advance); return Task.CompletedTask; }
        public Task<SupplierAdvance?> GetSupplierAdvanceAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(SupplierAdvances.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<SupplierAdvance>> ListSupplierAdvancesAsync(Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SupplierAdvance>>(SupplierAdvances);
        public Task AddSupplierAdvanceApplicationAsync(SupplierAdvanceApplication application, CancellationToken cancellationToken = default)
        {
            SupplierAdvanceApplications.Add(application);
            SupplierAdvances.Single(x => x.Id == application.SupplierAdvanceId).Applications.Add(application);
            return Task.CompletedTask;
        }

        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>Bridges <see cref="IJournalPostingService"/>'s narrower <see cref="IAccountingRepository"/>
        /// dependency onto this fixture, resolving the same handful of semantic accounts every posting
        /// path in this test file needs (AR/AP, their adjustment suspense accounts, Bad Debt Expense,
        /// Payables Write-off Income, Customer/Supplier Advances, Cash).</summary>
        private sealed class JournalPostingAdapter(Fixture fixture) : IAccountingRepository
        {
            private readonly Dictionary<AccountMappingKey, Guid> mappings = Enum.GetValues<AccountMappingKey>().ToDictionary(k => k, _ => Guid.NewGuid());

            public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(fixture.Actor.Id == actorId ? fixture.Actor : null);
            public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(null);
            public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Customer?>(null);
            public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Supplier?>(null);
            public Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<ChartOfAccount?>(null);
            public Task<ChartOfAccount?> GetAccountByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) => Task.FromResult<ChartOfAccount?>(null);
            public Task<IReadOnlyList<ChartOfAccount>> ListAccountsAsync(bool includeInactive, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ChartOfAccount>>([]);
            public Task AddAccountAsync(ChartOfAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<decimal> GetAccountBalanceAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult(0m);
            public Task<IReadOnlyList<AccountMapping>> ListAccountMappingsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AccountMapping>>([]);
            public Task<Dictionary<AccountMappingKey, Guid>> GetAccountMappingLookupAsync(CancellationToken cancellationToken = default) => Task.FromResult(mappings);
            public Task<AccountMapping?> GetAccountMappingAsync(AccountMappingKey key, CancellationToken cancellationToken = default) => Task.FromResult<AccountMapping?>(null);
            public Task AddAccountMappingAsync(AccountMapping mapping, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<bool> IsAccountMappedAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult(false);
            public Task<string> NextJournalEntryNumberAsync(DateTime entryDateUtc, CancellationToken cancellationToken = default) => Task.FromResult($"JV-{entryDateUtc.Year}-{fixture.JournalEntries.Count + 1:000000}");
            public Task<bool> JournalEntryExistsForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default) =>
                Task.FromResult(fixture.JournalEntries.Any(x => x.SourceType == sourceType && x.SourceId == sourceId));
            public Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default) { fixture.JournalEntries.Add(entry); return Task.CompletedTask; }
            public Task<JournalEntry?> GetJournalEntryAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(fixture.JournalEntries.FirstOrDefault(x => x.Id == id));
            public Task<PagedResult<JournalEntryListItemDto>> ListJournalEntriesAsync(JournalEntryListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) =>
                Task.FromResult(new PagedResult<JournalEntryListItemDto>([], 1, 25, 0));
            public Task<IReadOnlyList<TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TrialBalanceRowDto>>([]);
            public Task<IReadOnlyList<TrialBalanceRowDto>> GetAccountActivityAsync(DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TrialBalanceRowDto>>([]);
            public Task<GeneralLedgerDto> GetGeneralLedgerAsync(GeneralLedgerQuery query, CancellationToken cancellationToken = default) =>
                Task.FromResult(new GeneralLedgerDto(query.ChartOfAccountId, string.Empty, string.Empty, NormalBalance.Debit, 0, 0, 0, 0, new PagedResult<GeneralLedgerLineDto>([], 1, 50, 0)));
            public Task<IReadOnlyList<ArAgingSummaryRowDto>> GetArAgingSummaryAsync(DateTime asOfUtc, Guid? branchId, Guid? customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ArAgingSummaryRowDto>>([]);
            public Task<ArAgingDetailDto?> GetArAgingDetailAsync(Guid customerId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<ArAgingDetailDto?>(null);
            public Task<IReadOnlyList<ApAgingSummaryRowDto>> GetApAgingSummaryAsync(DateTime asOfUtc, Guid? branchId, Guid? supplierId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ApAgingSummaryRowDto>>([]);
            public Task<ApAgingDetailDto?> GetApAgingDetailAsync(Guid supplierId, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<ApAgingDetailDto?>(null);
            public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => operation(cancellationToken);
            public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<bool> JournalEntryHasReversalAsync(Guid journalEntryId, CancellationToken cancellationToken = default) => Task.FromResult(false);
            public Task<bool> JournalEntryLinkedToVoucherAsync(Guid journalEntryId, CancellationToken cancellationToken = default) => Task.FromResult(false);
            public Task<AccountingPeriod?> GetCoveringPeriodAsync(DateOnly date, CancellationToken cancellationToken = default) => Task.FromResult<AccountingPeriod?>(null);
            public void AllowPostingIntoSoftClosedPeriod() { }
            public Task<IReadOnlyList<CostCenter>> ListCostCentersAsync(bool includeInactive, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CostCenter>>([]);
            public Task<CostCenter?> GetCostCenterAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<CostCenter?>(null);
            public Task<CostCenter?> GetCostCenterByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) => Task.FromResult<CostCenter?>(null);
            public Task AddCostCenterAsync(CostCenter costCenter, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<CashBankBookDto> GetCashBankBookAsync(AccountMappingKey mappingKey, CashBankBookQuery query, CancellationToken cancellationToken = default) =>
                Task.FromResult(new CashBankBookDto(query.FromUtc, query.ToUtc, 0, 0, 0, 0, new PagedResult<CashBankBookLineDto>([], query.Page, query.PageSize, 0)));
            public Task<DayBookDto> GetDayBookAsync(DayBookQuery query, CancellationToken cancellationToken = default) =>
                Task.FromResult(new DayBookDto(query.FromUtc, query.ToUtc, 0, 0, new PagedResult<DayBookLineDto>([], query.Page, query.PageSize, 0)));
            public Task<decimal> GetCashAndBankBalanceAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult(0m);
            public Task<IReadOnlyList<(Guid ChartOfAccountId, AccountType AccountType, CashFlowClassification? Classification, decimal OpeningBalance, decimal ClosingBalance)>> GetNonCashBalanceMovementsAsync(
                DateTime fromUtc, DateTime toUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<(Guid, AccountType, CashFlowClassification?, decimal, decimal)>>([]);
            public Task<ControlReconciliationDto> GetArControlReconciliationAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult(new ControlReconciliationDto(asOfUtc, 0, 0, 0, []));
            public Task<ControlReconciliationDto> GetApControlReconciliationAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult(new ControlReconciliationDto(asOfUtc, 0, 0, 0, []));
            public Task<CashBankControlReconciliationDto> GetCashBankControlReconciliationAsync(DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) =>
                Task.FromResult(new CashBankControlReconciliationDto(asOfUtc, 0, 0, 0, 0, 0, 0, []));
            public Task<decimal> GetInventoryValuationAsync(Guid? branchId, Guid? godownId, CancellationToken cancellationToken = default) => Task.FromResult(0m);
            public Task<decimal> GetMappedAccountBalanceAsync(AccountMappingKey key, DateTime asOfUtc, Guid? branchId, CancellationToken cancellationToken = default) => Task.FromResult(0m);
        }
    }
}
