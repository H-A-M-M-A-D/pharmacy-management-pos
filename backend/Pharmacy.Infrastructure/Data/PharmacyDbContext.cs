using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Infrastructure.Data;

/// <summary>
/// Main database context for the Pharmacy Management System.
/// Configured for PostgreSQL with Entity Framework Core.
/// </summary>
public class PharmacyDbContext : DbContext
{
    public PharmacyDbContext(DbContextOptions<PharmacyDbContext> options) : base(options)
    {
    }

    // DbSet properties for all entities
    public DbSet<Branch> Branches { get; set; } = null!;
    public DbSet<Godown> Godowns { get; set; } = null!;
    public DbSet<UserGodown> UserGodowns { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<ProductCategory> ProductCategories { get; set; } = null!;
    public DbSet<Manufacturer> Manufacturers { get; set; } = null!;
    public DbSet<Supplier> Suppliers { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<ProductBatch> ProductBatches { get; set; } = null!;
    public DbSet<Inventory> Inventory { get; set; } = null!;
    public DbSet<StockMovement> StockMovements { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<SupplierLedgerEntry> SupplierLedgerEntries { get; set; } = null!;
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; } = null!;
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; } = null!;
    public DbSet<GoodsReceipt> GoodsReceipts { get; set; } = null!;
    public DbSet<GoodsReceiptItem> GoodsReceiptItems { get; set; } = null!;
    public DbSet<PurchaseReturn> PurchaseReturns { get; set; } = null!;
    public DbSet<PurchaseReturnItem> PurchaseReturnItems { get; set; } = null!;
    public DbSet<Sale> Sales { get; set; } = null!;
    public DbSet<SaleItem> SaleItems { get; set; } = null!;
    public DbSet<SaleItemBatchAllocation> SaleItemBatchAllocations { get; set; } = null!;
    public DbSet<SalePayment> SalePayments { get; set; } = null!;
    public DbSet<SalesReturn> SalesReturns { get; set; } = null!;
    public DbSet<SalesReturnItem> SalesReturnItems { get; set; } = null!;
    public DbSet<SalesReturnAllocation> SalesReturnAllocations { get; set; } = null!;
    public DbSet<SalesRefundPayment> SalesRefundPayments { get; set; } = null!;
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<CustomerLedgerEntry> CustomerLedgerEntries { get; set; } = null!;
    public DbSet<CustomerPayment> CustomerPayments { get; set; } = null!;
    public DbSet<FinancialAccount> FinancialAccounts { get; set; } = null!;
    public DbSet<FinancialLedgerEntry> FinancialLedgerEntries { get; set; } = null!;
    public DbSet<ExpenseCategory> ExpenseCategories { get; set; } = null!;
    public DbSet<Expense> Expenses { get; set; } = null!;
    public DbSet<OtherIncome> OtherIncomes { get; set; } = null!;
    public DbSet<FinancialTransfer> FinancialTransfers { get; set; } = null!;
    public DbSet<SystemSetting> SystemSettings { get; set; } = null!;
    public DbSet<BackupRecord> BackupRecords { get; set; } = null!;
    public DbSet<StockCountSession> StockCountSessions { get; set; } = null!;
    public DbSet<StockCountItem> StockCountItems { get; set; } = null!;
    public DbSet<CashierShift> CashierShifts { get; set; } = null!;
    public DbSet<CashierShiftDrawerEntry> CashierShiftDrawerEntries { get; set; } = null!;
    public DbSet<CashierShiftPaymentSummary> CashierShiftPaymentSummaries { get; set; } = null!;
    public DbSet<ChartOfAccount> ChartOfAccounts { get; set; } = null!;
    public DbSet<AccountMapping> AccountMappings { get; set; } = null!;
    public DbSet<JournalEntry> JournalEntries { get; set; } = null!;
    public DbSet<JournalEntryLine> JournalEntryLines { get; set; } = null!;
    public DbSet<CustomerPaymentAllocation> CustomerPaymentAllocations { get; set; } = null!;
    public DbSet<SupplierPaymentAllocation> SupplierPaymentAllocations { get; set; } = null!;
    public DbSet<Voucher> Vouchers { get; set; } = null!;
    public DbSet<VoucherLine> VoucherLines { get; set; } = null!;
    public DbSet<StockTransfer> StockTransfers { get; set; } = null!;
    public DbSet<StockTransferItem> StockTransferItems { get; set; } = null!;
    public DbSet<PriceLevel> PriceLevels { get; set; } = null!;
    public DbSet<ProductPriceLevel> ProductPriceLevels { get; set; } = null!;
    public DbSet<ProductPriceBreak> ProductPriceBreaks { get; set; } = null!;
    public DbSet<SalesQuotation> SalesQuotations { get; set; } = null!;
    public DbSet<SalesQuotationItem> SalesQuotationItems { get; set; } = null!;
    public DbSet<SalesOrder> SalesOrders { get; set; } = null!;
    public DbSet<SalesOrderItem> SalesOrderItems { get; set; } = null!;
    public DbSet<AccountingPeriod> AccountingPeriods { get; set; } = null!;
    public DbSet<FiscalYearClose> FiscalYearCloses { get; set; } = null!;
    public DbSet<RecurringJournalTemplate> RecurringJournalTemplates { get; set; } = null!;
    public DbSet<RecurringJournalTemplateLine> RecurringJournalTemplateLines { get; set; } = null!;
    public DbSet<RecurringJournalOccurrence> RecurringJournalOccurrences { get; set; } = null!;
    public DbSet<BankReconciliation> BankReconciliations { get; set; } = null!;
    public DbSet<AccountBudget> AccountBudgets { get; set; } = null!;
    public DbSet<CostCenter> CostCenters { get; set; } = null!;
    public DbSet<CreditNote> CreditNotes { get; set; } = null!;
    public DbSet<DebitNote> DebitNotes { get; set; } = null!;
    public DbSet<CustomerWriteOff> CustomerWriteOffs { get; set; } = null!;
    public DbSet<SupplierWriteOff> SupplierWriteOffs { get; set; } = null!;
    public DbSet<CustomerAdvance> CustomerAdvances { get; set; } = null!;
    public DbSet<CustomerAdvanceApplication> CustomerAdvanceApplications { get; set; } = null!;
    public DbSet<SupplierAdvance> SupplierAdvances { get; set; } = null!;
    public DbSet<SupplierAdvanceApplication> SupplierAdvanceApplications { get; set; } = null!;

    /// <summary>Per-call escape hatch letting a service that has already verified the posting actor
    /// holds <c>accounts.post_to_soft_closed</c> post into a <see cref="AccountingPeriodStatus.SoftClosed"/>
    /// period. Never set for automatic/system postings — see <see cref="ValidateAccountingPeriodLocks"/>.
    /// Defaults false on every DbContext instance (scoped per request), so nothing needs to reset it.</summary>
    public bool AllowPostingIntoSoftClosedPeriod { get; set; }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AddFinancialEntriesForPayments();
        ValidateStockMovements();
        ValidateSupplierLedgerEntries();
        ValidateCustomerFinancialEntries();
        ValidatePurchasingDocuments();
        ValidateSalesDocuments();
        ValidateFinanceDocuments();
        ValidateStockCountDocuments();
        ValidateCashierShiftDocuments();
        ValidateAccountingDocuments();
        ValidateAccountingPeriodLocks();
        ValidatePhase4Documents();
        ProtectAuditLog();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AddFinancialEntriesForPayments();
        ValidateStockMovements();
        ValidateSupplierLedgerEntries();
        ValidateCustomerFinancialEntries();
        ValidatePurchasingDocuments();
        ValidateSalesDocuments();
        ValidateFinanceDocuments();
        ValidateStockCountDocuments();
        ValidateCashierShiftDocuments();
        ValidateAccountingDocuments();
        ValidateAccountingPeriodLocks();
        ValidatePhase4Documents();
        ProtectAuditLog();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Central, single-choke-point enforcement of accounting-period locking. Every posting
    /// path in the system — automatic (Sales/Purchases/Expenses/... via JournalPostingService),
    /// manual journals, and vouchers — ultimately calls <c>DbSet&lt;JournalEntry&gt;.Add</c>, so gating
    /// it here means no individual service needs its own period check. A date with no covering
    /// <see cref="AccountingPeriod"/> at all is always postable (periods are opt-in), a
    /// <see cref="AccountingPeriodStatus.Closed"/> period always blocks, and a
    /// <see cref="AccountingPeriodStatus.SoftClosed"/> period blocks unless
    /// <see cref="AllowPostingIntoSoftClosedPeriod"/> was explicitly set by a caller that already
    /// checked the actor holds the override permission.</summary>
    private void ValidateAccountingPeriodLocks()
    {
        ChangeTracker.DetectChanges();
        var addedEntries = ChangeTracker.Entries<JournalEntry>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
        if (addedEntries.Count == 0) return;
        var dates = addedEntries.Select(e => DateOnly.FromDateTime(e.EntryDateUtc)).ToList();
        var minDate = dates.Min();
        var maxDate = dates.Max();
        var lockedPeriods = AccountingPeriods
            .Where(p => p.Status != AccountingPeriodStatus.Open && p.StartDate <= maxDate && p.EndDate >= minDate)
            .ToList();
        if (lockedPeriods.Count == 0) return;
        foreach (var entry in addedEntries)
        {
            var date = DateOnly.FromDateTime(entry.EntryDateUtc);
            var period = lockedPeriods.FirstOrDefault(p => date >= p.StartDate && date <= p.EndDate);
            if (period is null) continue;
            if (period.Status == AccountingPeriodStatus.Closed)
            {
                Entry(entry).State = EntityState.Detached;
                throw new InvalidOperationException($"Cannot post into closed accounting period '{period.Name}' ({period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd}).");
            }
            if (period.Status == AccountingPeriodStatus.SoftClosed && !AllowPostingIntoSoftClosedPeriod)
            {
                Entry(entry).State = EntityState.Detached;
                throw new InvalidOperationException($"Cannot post into soft-closed accounting period '{period.Name}' without override permission.");
            }
        }
    }

    private void ValidatePhase4Documents()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<RecurringJournalOccurrence>())
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Recurring journal occurrence history is permanent and cannot be updated or deleted.");
        foreach (var entry in ChangeTracker.Entries<CreditNote>())
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Posted credit notes are permanent and cannot be updated or deleted.");
        foreach (var entry in ChangeTracker.Entries<DebitNote>())
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Posted debit notes are permanent and cannot be updated or deleted.");
        foreach (var entry in ChangeTracker.Entries<CustomerWriteOff>())
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Posted customer write-offs are permanent and cannot be updated or deleted.");
        foreach (var entry in ChangeTracker.Entries<SupplierWriteOff>())
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Posted supplier write-offs are permanent and cannot be updated or deleted.");
        foreach (var entry in ChangeTracker.Entries<CustomerAdvance>())
            if (entry.State == EntityState.Deleted || (entry.State == EntityState.Modified && entry.Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(CustomerAdvance.AmountApplied))))
                throw new InvalidOperationException("Posted customer advances are permanent; only the applied-amount running total may change.");
        foreach (var entry in ChangeTracker.Entries<CustomerAdvanceApplication>())
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Customer advance application history is permanent and cannot be updated or deleted.");
        foreach (var entry in ChangeTracker.Entries<SupplierAdvance>())
            if (entry.State == EntityState.Deleted || (entry.State == EntityState.Modified && entry.Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(SupplierAdvance.AmountApplied))))
                throw new InvalidOperationException("Posted supplier advances are permanent; only the applied-amount running total may change.");
        foreach (var entry in ChangeTracker.Entries<SupplierAdvanceApplication>())
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Supplier advance application history is permanent and cannot be updated or deleted.");
    }

    private void ValidateAccountingDocuments()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<ChartOfAccount>())
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Chart of accounts entries are permanent and can only be deactivated.");
        foreach (var entry in ChangeTracker.Entries<JournalEntry>())
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Posted journal entries are permanent and cannot be updated or deleted.");
        foreach (var entry in ChangeTracker.Entries<JournalEntryLine>())
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Posted journal entry lines are permanent and cannot be updated or deleted.");

        var addedLines = ChangeTracker.Entries<JournalEntryLine>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToList();
        if (addedLines.Count == 0) return;
        var addedEntries = ChangeTracker.Entries<JournalEntry>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToDictionary(x => x.Id);
        foreach (var group in addedLines.GroupBy(x => x.JournalEntryId))
        {
            var totalDebit = group.Sum(x => x.Debit);
            var totalCredit = group.Sum(x => x.Credit);
            if (totalDebit != totalCredit)
            {
                var label = addedEntries.TryGetValue(group.Key, out var journalEntry) ? journalEntry.EntryNumber : group.Key.ToString();
                throw new InvalidOperationException($"Journal entry {label} does not balance: total debit {totalDebit} does not equal total credit {totalCredit}.");
            }
        }
    }

    private void ValidateCashierShiftDocuments()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<CashierShift>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Cashier shift history is permanent and cannot be deleted.");
            if (entry.State == EntityState.Modified && entry.OriginalValues.GetValue<CashierShiftStatus>(nameof(CashierShift.Status)) == CashierShiftStatus.Reconciled)
                throw new InvalidOperationException("Reconciled cashier shifts are permanent and cannot be modified.");
        }
        foreach (var entry in ChangeTracker.Entries<CashierShiftDrawerEntry>())
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Cashier shift drawer entries are permanent and cannot be updated or deleted.");
        foreach (var entry in ChangeTracker.Entries<CashierShiftPaymentSummary>())
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Cashier shift payment summaries are permanent and cannot be updated or deleted.");
    }

    private void ValidateStockCountDocuments()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<StockCountSession>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Stock count session history is permanent and cannot be deleted.");
            var originalStatus = entry.State == EntityState.Modified ? entry.OriginalValues.GetValue<StockCountStatus>(nameof(StockCountSession.Status)) : (StockCountStatus?)null;
            if (originalStatus is StockCountStatus.Completed or StockCountStatus.Cancelled && entry.State == EntityState.Modified)
                throw new InvalidOperationException("Completed or cancelled stock count sessions are permanent and cannot be modified.");
        }
        foreach (var entry in ChangeTracker.Entries<StockCountItem>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Stock count item history is permanent and cannot be deleted.");
        }
    }

    private void ValidatePurchasingDocuments()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<GoodsReceipt>())
        {
            if (entry.State == EntityState.Deleted || (entry.State == EntityState.Modified && entry.Entity.Status == GoodsReceiptStatus.Posted))
            {
                throw new InvalidOperationException("Posted goods receipts are permanent and cannot be updated or deleted.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<GoodsReceiptItem>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
            {
                throw new InvalidOperationException("Posted goods receipt item history is permanent and cannot be updated or deleted.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<PurchaseReturn>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
            {
                throw new InvalidOperationException("Posted purchase returns are permanent and cannot be updated or deleted.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<PurchaseReturnItem>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
            {
                throw new InvalidOperationException("Purchase return item history is permanent and cannot be updated or deleted.");
            }
        }
    }

    private void AddFinancialEntriesForPayments()
    {
        ChangeTracker.DetectChanges();
        foreach (var tracked in ChangeTracker.Entries<SalePayment>().Where(x => x.State == EntityState.Added && x.Entity.FinancialAccountId.HasValue).ToList())
        {
            var payment = tracked.Entity;
            var sale = payment.Sale ?? ChangeTracker.Entries<Sale>().Select(x => x.Entity).First(x => x.Id == payment.SaleId);
            FinancialLedgerEntries.Add(new FinancialLedgerEntry { FinancialAccountId = payment.FinancialAccountId!.Value, BranchId = sale.BranchId,
                EntryType = FinancialLedgerEntryType.SalePayment, Amount = payment.AmountApplied, ReferenceType = "SalePayment", ReferenceId = payment.Id,
                ReferenceNumber = sale.InvoiceNumber, Description = "Sale payment", CreatedByUserId = sale.CashierUserId, OccurredAtUtc = sale.PostedAtUtc ?? DateTime.UtcNow });
        }
        foreach (var tracked in ChangeTracker.Entries<CustomerPayment>().Where(x => x.State == EntityState.Added && x.Entity.FinancialAccountId.HasValue).ToList())
        {
            var payment = tracked.Entity;
            FinancialLedgerEntries.Add(new FinancialLedgerEntry { FinancialAccountId = payment.FinancialAccountId!.Value, BranchId = payment.BranchId,
                EntryType = FinancialLedgerEntryType.CustomerPayment, Amount = payment.Amount, ReferenceType = "CustomerPayment", ReferenceId = payment.Id,
                ReferenceNumber = payment.ReceiptNumber, Description = "Customer payment", CreatedByUserId = payment.ReceivedByUserId, OccurredAtUtc = payment.PaymentDateUtc });
        }
        foreach (var tracked in ChangeTracker.Entries<SupplierLedgerEntry>().Where(x => x.State == EntityState.Added && x.Entity.EntryType == SupplierLedgerEntryType.Payment && x.Entity.FinancialAccountId.HasValue).ToList())
        {
            var payment = tracked.Entity;
            FinancialLedgerEntries.Add(new FinancialLedgerEntry { FinancialAccountId = payment.FinancialAccountId!.Value, BranchId = payment.BranchId,
                EntryType = FinancialLedgerEntryType.SupplierPayment, Amount = payment.Amount, ReferenceType = "SupplierPayment", ReferenceId = payment.Id,
                ReferenceNumber = payment.ReferenceNumber, Description = "Supplier payment", CreatedByUserId = payment.CreatedByUserId ?? throw new InvalidOperationException("Supplier payment requires a user."), OccurredAtUtc = payment.CreatedAt });
        }
        foreach (var tracked in ChangeTracker.Entries<SalesRefundPayment>().Where(x => x.State == EntityState.Added && x.Entity.FinancialAccountId.HasValue).ToList())
        {
            var payment = tracked.Entity;
            var salesReturn = payment.SalesReturn ?? ChangeTracker.Entries<SalesReturn>().Select(x => x.Entity).First(x => x.Id == payment.SalesReturnId);
            FinancialLedgerEntries.Add(new FinancialLedgerEntry { FinancialAccountId = payment.FinancialAccountId!.Value, BranchId = salesReturn.BranchId,
                EntryType = FinancialLedgerEntryType.SalesRefund, Amount = -payment.Amount, ReferenceType = "SalesRefundPayment", ReferenceId = payment.Id,
                ReferenceNumber = salesReturn.ReturnNumber, Description = "Sales refund", CreatedByUserId = salesReturn.ProcessedByUserId, OccurredAtUtc = salesReturn.ReturnDateUtc });
        }
    }

    private void ValidateFinanceDocuments()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<FinancialLedgerEntry>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Financial ledger history is permanent and cannot be deleted.");
            if (entry.State == EntityState.Modified && entry.Properties.Any(p => p.IsModified &&
                    p.Metadata.Name is not (nameof(FinancialLedgerEntry.BankReconciliationId) or nameof(FinancialLedgerEntry.ReconciledAtUtc))))
                throw new InvalidOperationException("Financial ledger history is permanent and cannot be updated, aside from bank-reconciliation matching.");
            if (entry.State == EntityState.Added && entry.Entity.Amount == 0)
                throw new InvalidOperationException("Financial ledger amount cannot be zero.");
        }
        foreach (var entry in ChangeTracker.Entries<Expense>())
        {
            if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Posted expenses are permanent and cannot be deleted.");
            if (entry.State == EntityState.Modified && entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not
                    (nameof(Expense.ReversedAtUtc) or nameof(Expense.ReversedByUserId) or nameof(Expense.ReversalReason))))
                throw new InvalidOperationException("Posted expenses are permanent and cannot be updated, aside from being marked reversed.");
        }
        foreach (var entry in ChangeTracker.Entries<OtherIncome>())
        {
            if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Posted other income is permanent and cannot be deleted.");
            if (entry.State == EntityState.Modified && entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not
                    (nameof(OtherIncome.ReversedAtUtc) or nameof(OtherIncome.ReversedByUserId) or nameof(OtherIncome.ReversalReason))))
                throw new InvalidOperationException("Posted other income is permanent and cannot be updated, aside from being marked reversed.");
        }
        foreach (var entry in ChangeTracker.Entries<FinancialTransfer>())
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Posted transfers are permanent and cannot be updated or deleted.");
        foreach (var entry in ChangeTracker.Entries<FinancialAccount>())
            if (entry.State == EntityState.Modified && entry.Property(x => x.OpeningBalance).IsModified) throw new InvalidOperationException("Opening balance cannot be edited after account creation.");
    }


    private void ValidateSalesDocuments()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<Sale>())
        {
            if (entry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException("Sale history is permanent and cannot be deleted.");
            }

            if (entry.State == EntityState.Modified && entry.OriginalValues.GetValue<SaleStatus>(nameof(Sale.Status)) == SaleStatus.Posted)
            {
                throw new InvalidOperationException("Posted sales are permanent and cannot be updated or deleted.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<SalePayment>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Sale payment history is permanent and cannot be updated or deleted.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<SaleItemBatchAllocation>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Sale batch allocation history is permanent and cannot be updated or deleted.");
            }
        }
        foreach (var entry in ChangeTracker.Entries<SalesReturn>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
            {
                throw new InvalidOperationException("Posted sales returns are permanent and cannot be updated or deleted.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<SalesReturnItem>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
            {
                throw new InvalidOperationException("Sales return item history is permanent and cannot be updated or deleted.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<SalesReturnAllocation>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
            {
                throw new InvalidOperationException("Sales return allocation history is permanent and cannot be updated or deleted.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<SalesRefundPayment>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
            {
                throw new InvalidOperationException("Sales refund payment history is permanent and cannot be updated or deleted.");
            }
        }
    }
    private void ValidateSupplierLedgerEntries()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<SupplierLedgerEntry>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
            {
                throw new InvalidOperationException("Supplier ledger history is permanent and cannot be updated or deleted.");
            }

            if (entry.State == EntityState.Added)
            {
                entry.Entity.Validate();
            }
        }
    }

    private void ValidateCustomerFinancialEntries()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<CustomerLedgerEntry>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
            {
                throw new InvalidOperationException("Customer ledger history is permanent and cannot be updated or deleted.");
            }

            if (entry.State == EntityState.Added)
            {
                entry.Entity.Validate();
            }
        }

        foreach (var entry in ChangeTracker.Entries<CustomerPayment>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
            {
                throw new InvalidOperationException("Customer payment history is permanent and cannot be updated or deleted.");
            }
        }
    }

    private void ValidateStockMovements()
    {
        ChangeTracker.DetectChanges();
        var movementDeltas = ChangeTracker.Entries<StockMovement>()
            .Where(entry => entry.State == EntityState.Added)
            .GroupBy(entry => new StockKey(entry.Entity.BranchId, entry.Entity.ProductId, entry.Entity.ProductBatchId))
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Entity.Quantity));

        foreach (var entry in ChangeTracker.Entries<StockMovement>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
            {
                throw new InvalidOperationException("Stock movement history is permanent and cannot be updated or deleted.");
            }

            if (entry.State == EntityState.Added)
            {
                entry.Entity.Validate();
            }
        }

        var batchDeltas = ChangeTracker.Entries<ProductBatch>()
            .Where(entry => entry.State == EntityState.Added || entry.Property(batch => batch.QuantityAvailable).IsModified)
            .Select(entry => new
            {
                Key = new StockKey(entry.Entity.BranchId, entry.Entity.ProductId, entry.Entity.Id),
                Delta = entry.Entity.QuantityAvailable -
                    (entry.State == EntityState.Added ? 0 : entry.Property(batch => batch.QuantityAvailable).OriginalValue)
            })
            .Where(change => change.Delta != 0)
            .ToDictionary(
                change => change.Key,
                change => change.Delta);

        var inventoryDeltas = ChangeTracker.Entries<Inventory>()
            .Where(entry => entry.State == EntityState.Added || entry.Property(inventory => inventory.QuantityInStock).IsModified)
            .Select(entry => new
            {
                Key = new StockKey(entry.Entity.BranchId, entry.Entity.ProductId, entry.Entity.ProductBatchId),
                Delta = entry.Entity.QuantityInStock -
                    (entry.State == EntityState.Added ? 0 : entry.Property(inventory => inventory.QuantityInStock).OriginalValue)
            })
            .Where(change => change.Delta != 0)
            .ToDictionary(
                change => change.Key,
                change => change.Delta);

        var affectedKeys = movementDeltas.Keys
            .Concat(batchDeltas.Keys)
            .Concat(inventoryDeltas.Keys)
            .Distinct();

        foreach (var key in affectedKeys)
        {
            movementDeltas.TryGetValue(key, out var movementDelta);
            batchDeltas.TryGetValue(key, out var batchDelta);
            inventoryDeltas.TryGetValue(key, out var inventoryDelta);
            if (batchDelta != movementDelta || inventoryDelta != movementDelta)
            {
                throw new InvalidOperationException(
                    "ProductBatch and Inventory balances must change atomically with matching StockMovement records.");
            }
        }
    }

    private readonly record struct StockKey(Guid BranchId, Guid ProductId, Guid ProductBatchId);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasSequence<long>("ExpenseNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("OtherIncomeNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("FinancialTransferNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("CashReceiptVoucherNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("CashPaymentVoucherNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("BankReceiptVoucherNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("BankPaymentVoucherNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("ContraVoucherNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("JournalVoucherNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("StockTransferNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("QuotationNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("SalesOrderNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("CreditNoteNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("DebitNoteNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("CustomerWriteOffNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("SupplierWriteOffNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("CustomerAdvanceNumberSequence").StartsAt(1);
        modelBuilder.HasSequence<long>("SupplierAdvanceNumberSequence").StartsAt(1);

        // Apply entity configurations
        ConfigureBranch(modelBuilder);
        ConfigureGodown(modelBuilder);
        ConfigureUserGodown(modelBuilder);
        ConfigureRole(modelBuilder);
        ConfigurePermission(modelBuilder);
        ConfigureRolePermission(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigureProductCategory(modelBuilder);
        ConfigureManufacturer(modelBuilder);
        ConfigureSupplier(modelBuilder);
        ConfigureCustomer(modelBuilder);
        ConfigureProduct(modelBuilder);
        ConfigureProductBatch(modelBuilder);
        ConfigureInventory(modelBuilder);
        ConfigureStockMovement(modelBuilder);
        ConfigureStockCountSession(modelBuilder);
        ConfigureStockCountItem(modelBuilder);
        ConfigureCashierShift(modelBuilder);
        ConfigureCashierShiftDrawerEntry(modelBuilder);
        ConfigureCashierShiftPaymentSummary(modelBuilder);
        ConfigureChartOfAccount(modelBuilder);
        ConfigureAccountMapping(modelBuilder);
        ConfigureJournalEntry(modelBuilder);
        ConfigureJournalEntryLine(modelBuilder);
        ConfigureAuditLog(modelBuilder);
        ConfigureSupplierLedgerEntry(modelBuilder);
        ConfigureCustomerLedgerEntry(modelBuilder);
        ConfigureCustomerPayment(modelBuilder);
        ConfigurePurchaseOrder(modelBuilder);
        ConfigurePurchaseOrderItem(modelBuilder);
        ConfigureGoodsReceipt(modelBuilder);
        ConfigureGoodsReceiptItem(modelBuilder);
        ConfigurePurchaseReturn(modelBuilder);
        ConfigurePurchaseReturnItem(modelBuilder);
        ConfigureSale(modelBuilder);
        ConfigureSaleItem(modelBuilder);
        ConfigureSaleItemBatchAllocation(modelBuilder);
        ConfigureSalePayment(modelBuilder);
        ConfigureSalesReturn(modelBuilder);
        ConfigureSalesReturnItem(modelBuilder);
        ConfigureSalesReturnAllocation(modelBuilder);
        ConfigureSalesRefundPayment(modelBuilder);
        ConfigureFinancialAccount(modelBuilder);
        ConfigureFinancialLedgerEntry(modelBuilder);
        ConfigureExpenseCategory(modelBuilder);
        ConfigureExpense(modelBuilder);
        ConfigureOtherIncome(modelBuilder);
        ConfigureFinancialTransfer(modelBuilder);
        ConfigureSystemSetting(modelBuilder);
        ConfigureBackupRecord(modelBuilder);
        ConfigureCustomerPaymentAllocation(modelBuilder);
        ConfigureSupplierPaymentAllocation(modelBuilder);
        ConfigureVoucher(modelBuilder);
        ConfigureVoucherLine(modelBuilder);
        ConfigureStockTransfer(modelBuilder);
        ConfigureStockTransferItem(modelBuilder);
        ConfigurePriceLevel(modelBuilder);
        ConfigureProductPriceLevel(modelBuilder);
        ConfigureProductPriceBreak(modelBuilder);
        ConfigureSalesQuotation(modelBuilder);
        ConfigureSalesQuotationItem(modelBuilder);
        ConfigureSalesOrder(modelBuilder);
        ConfigureSalesOrderItem(modelBuilder);
        ConfigureAccountingPeriod(modelBuilder);
        ConfigureFiscalYearClose(modelBuilder);
        ConfigureRecurringJournalTemplate(modelBuilder);
        ConfigureRecurringJournalTemplateLine(modelBuilder);
        ConfigureRecurringJournalOccurrence(modelBuilder);
        ConfigureBankReconciliation(modelBuilder);
        ConfigureAccountBudget(modelBuilder);
        ConfigureCostCenter(modelBuilder);
        ConfigureCreditNote(modelBuilder);
        ConfigureDebitNote(modelBuilder);
        ConfigureCustomerWriteOff(modelBuilder);
        ConfigureSupplierWriteOff(modelBuilder);
        ConfigureCustomerAdvance(modelBuilder);
        ConfigureCustomerAdvanceApplication(modelBuilder);
        ConfigureSupplierAdvance(modelBuilder);
        ConfigureSupplierAdvanceApplication(modelBuilder);
    }

    private void ConfigureBranch(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Branch>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
        entity.Property(e => e.NormalizedCode).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Address).HasMaxLength(500);
        entity.Property(e => e.City).HasMaxLength(100);
        entity.Property(e => e.PhoneNumber).HasMaxLength(20);
        entity.Property(e => e.Email).HasMaxLength(100);

        entity.HasIndex(e => e.NormalizedCode).IsUnique();
        entity.HasIndex(e => e.IsActive);
    }

    private void ConfigureGodown(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Godown>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
        entity.Property(e => e.NormalizedCode).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Description).HasMaxLength(500);

        entity.HasIndex(e => new { e.BranchId, e.NormalizedCode }).IsUnique();
        entity.HasIndex(e => new { e.BranchId, e.IsActive });
        entity.HasIndex(e => e.BranchId).IsUnique().HasFilter("\"IsDefault\" = true").HasDatabaseName("IX_Godowns_BranchId_OneDefault");

        entity.HasOne(e => e.Branch)
            .WithMany(b => b.Godowns)
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureUserGodown(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserGodown>();

        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.UserId, e.GodownId }).IsUnique();
        entity.HasIndex(e => new { e.UserId, e.IsDefault });

        entity.HasOne(e => e.User)
            .WithMany(u => u.UserGodowns)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Godown)
            .WithMany(g => g.UserGodowns)
            .HasForeignKey(e => e.GodownId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureRole(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Role>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Description).HasMaxLength(500);

        entity.HasIndex(e => e.Name).IsUnique();
        entity.HasIndex(e => e.IsActive);
    }

    private void ConfigurePermission(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Permission>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
        entity.Property(e => e.Category).IsRequired().HasMaxLength(50);

        entity.HasIndex(e => e.Code).IsUnique();
        entity.HasIndex(e => e.Category);
    }

    private void ConfigureRolePermission(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RolePermission>();

        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();

        entity.HasOne(e => e.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(e => e.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureUser(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<User>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
        entity.Property(e => e.NormalizedUsername).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Email).HasMaxLength(100);
        entity.Property(e => e.NormalizedEmail).HasMaxLength(100);
        entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.PasswordHash).IsRequired();
        entity.Property(e => e.PhoneNumber).HasMaxLength(20);

        entity.HasIndex(e => e.NormalizedUsername).IsUnique();
        entity.HasIndex(e => e.NormalizedEmail).IsUnique().HasFilter("\"NormalizedEmail\" IS NOT NULL");
        entity.HasIndex(e => new { e.BranchId, e.IsActive });
        entity.HasIndex(e => new { e.RoleId, e.IsActive });
        entity.HasIndex(e => e.LockoutEndUtc);

        entity.HasOne(e => e.Branch)
            .WithMany(b => b.Users)
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureProductCategory(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductCategory>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.NormalizedName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Description).HasMaxLength(500);

        entity.HasIndex(e => e.NormalizedName).IsUnique();
        entity.HasIndex(e => e.IsActive);
        entity.HasIndex(e => e.DeletedAtUtc);
        entity.HasQueryFilter(e => !e.IsDeleted);
    }

    private void ConfigureManufacturer(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Manufacturer>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.NormalizedName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.ShortName).HasMaxLength(100);
        entity.Property(e => e.Country).HasMaxLength(100);
        entity.Property(e => e.Email).HasMaxLength(100);
        entity.Property(e => e.PhoneNumber).HasMaxLength(20);
        entity.Property(e => e.Address).HasMaxLength(500);
        entity.Property(e => e.Website).HasMaxLength(300);

        entity.HasIndex(e => e.NormalizedName).IsUnique();
        entity.HasIndex(e => e.IsActive);
        entity.HasIndex(e => e.DeletedAtUtc);
        entity.HasQueryFilter(e => !e.IsDeleted);
    }

    private void ConfigureSupplier(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Supplier>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.NormalizedName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.ShortName).HasMaxLength(100);
        entity.Property(e => e.ContactPerson).HasMaxLength(200);
        entity.Property(e => e.Email).HasMaxLength(100);
        entity.Property(e => e.PhoneNumber).HasMaxLength(20);
        entity.Property(e => e.AlternatePhone).HasMaxLength(20);
        entity.Property(e => e.WhatsApp).HasMaxLength(20);
        entity.Property(e => e.Address).HasMaxLength(500);
        entity.Property(e => e.City).HasMaxLength(100);
        entity.Property(e => e.TaxNumber).HasMaxLength(50);
        entity.Property(e => e.STRN).HasMaxLength(50);
        entity.Property(e => e.PaymentTerms).HasMaxLength(500);
        entity.Property(e => e.OpeningBalance).HasPrecision(18, 2);
        entity.Property(e => e.CreditLimit).HasPrecision(18, 2);

        entity.HasIndex(e => e.NormalizedName).IsUnique();
        entity.HasIndex(e => e.Name);
        entity.HasIndex(e => e.IsActive);
        entity.HasIndex(e => e.City);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Suppliers_CreditLimit_NonNegative", "\"CreditLimit\" IS NULL OR \"CreditLimit\" >= 0");
            table.HasCheckConstraint("CK_Suppliers_PaymentTermsDays_NonNegative", "\"PaymentTermsDays\" IS NULL OR \"PaymentTermsDays\" >= 0");
        });
    }

    private void ConfigureProduct(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Product>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.SKU).IsRequired().HasMaxLength(100);
        entity.Property(e => e.NormalizedSku).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Barcode).HasMaxLength(100);
        entity.Property(e => e.NormalizedBarcode).HasMaxLength(100);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
        entity.Property(e => e.GenericName).HasMaxLength(500);
        entity.Property(e => e.BrandName).HasMaxLength(200);
        entity.Property(e => e.Unit).IsRequired().HasMaxLength(50);
        entity.Property(e => e.PurchasePrice).HasPrecision(18, 2);
        entity.Property(e => e.RetailPrice).HasPrecision(18, 2);
        entity.Property(e => e.TradePrice).HasPrecision(18, 2);
        entity.Property(e => e.MaximumDiscountPercent).HasPrecision(5, 2);

        entity.HasIndex(e => e.NormalizedSku).IsUnique();
        entity.HasIndex(e => e.NormalizedBarcode).IsUnique().HasFilter("\"NormalizedBarcode\" IS NOT NULL");
        entity.HasIndex(e => e.Name);
        entity.HasIndex(e => e.GenericName);
        entity.HasIndex(e => e.BrandName);
        entity.HasIndex(e => e.CategoryId);
        entity.HasIndex(e => e.ManufacturerId);
        entity.HasIndex(e => e.IsActive);

        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Products_PackSize_Positive", "\"PackSize\" > 0");
            table.HasCheckConstraint("CK_Products_Prices_NonNegative", "\"PurchasePrice\" >= 0 AND \"RetailPrice\" >= 0 AND (\"TradePrice\" IS NULL OR \"TradePrice\" >= 0)");
            table.HasCheckConstraint("CK_Products_Discount_Range", "\"MaximumDiscountPercent\" >= 0 AND \"MaximumDiscountPercent\" <= 100");
            table.HasCheckConstraint("CK_Products_ReorderLevel_NonNegative", "\"ReorderLevel\" >= 0");
        });

        entity.HasOne(e => e.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Manufacturer)
            .WithMany(m => m.Products)
            .HasForeignKey(e => e.ManufacturerId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigureProductBatch(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductBatch>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.BatchNumber).IsRequired().HasMaxLength(100);
        entity.Property(e => e.PurchasePrice).HasPrecision(18, 2);
        entity.Property(e => e.RetailPrice).HasPrecision(18, 2);
        entity.Property(e => e.ManufacturingDate).HasColumnType("date");
        entity.Property(e => e.ExpiryDate).HasColumnType("date").IsRequired();

        entity.HasIndex(e => new { e.BranchId, e.GodownId, e.ProductId, e.BatchNumber }).IsUnique();
        entity.HasIndex(e => e.ExpiryDate);
        entity.HasIndex(e => new { e.BranchId, e.ExpiryDate });
        entity.HasIndex(e => new { e.BranchId, e.ProductId });
        entity.HasIndex(e => new { e.BranchId, e.GodownId, e.ProductId });
        entity.HasIndex(e => new { e.ProductId, e.BatchNumber });
        entity.HasIndex(e => e.IsDisposed);
        entity.HasIndex(e => e.QuantityAvailable);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ProductBatches_QuantityAvailable_NonNegative", "\"QuantityAvailable\" >= 0");
            table.HasCheckConstraint("CK_ProductBatches_QuantityReceived_NonNegative", "\"QuantityReceived\" >= 0");
            table.HasCheckConstraint("CK_ProductBatches_Prices_NonNegative", "\"PurchasePrice\" >= 0 AND \"RetailPrice\" >= 0");
            table.HasCheckConstraint("CK_ProductBatches_Manufacturing_Before_Expiry", "\"ManufacturingDate\" IS NULL OR \"ManufacturingDate\" <= \"ExpiryDate\"");
        });

        entity.HasOne(e => e.Product)
            .WithMany(p => p.ProductBatches)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Supplier)
            .WithMany(s => s.ProductBatches)
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.Branch)
            .WithMany(b => b.ProductBatches)
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Godown)
            .WithMany(g => g.ProductBatches)
            .HasForeignKey(e => e.GodownId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureInventory(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Inventory>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.QuantityInStock);
        entity.Property(e => e.ReorderLevel);

        entity.HasIndex(e => new { e.BranchId, e.ProductId, e.ProductBatchId }).IsUnique();
        entity.HasIndex(e => new { e.BranchId, e.ProductId });
        entity.HasIndex(e => new { e.BranchId, e.GodownId, e.ProductId });
        entity.HasIndex(e => new { e.BranchId, e.ReorderLevel }).HasFilter("\"QuantityInStock\" < \"ReorderLevel\"");
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Inventory_QuantityInStock_NonNegative", "\"QuantityInStock\" >= 0");
            table.HasCheckConstraint("CK_Inventory_ReorderLevel_NonNegative", "\"ReorderLevel\" >= 0");
        });

        entity.HasOne(e => e.Branch)
            .WithMany(b => b.Inventory)
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Godown)
            .WithMany(g => g.Inventory)
            .HasForeignKey(e => e.GodownId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Product)
            .WithMany(p => p.Inventory)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.ProductBatch)
            .WithMany(pb => pb.Inventory)
            .HasForeignKey(e => e.ProductBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureStockMovement(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockMovement>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.MovementType);
        entity.Property(e => e.Quantity);
        entity.Property(e => e.ReferenceType).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(500);

        entity.HasIndex(e => new { e.BranchId, e.ProductId, e.ProductBatchId });
        entity.HasIndex(e => new { e.BranchId, e.CreatedAt });
        entity.HasIndex(e => new { e.BranchId, e.GodownId, e.CreatedAt });
        entity.HasIndex(e => new { e.BranchId, e.ProductId, e.CreatedAt });
        entity.HasIndex(e => new { e.ProductBatchId, e.CreatedAt });
        entity.HasIndex(e => new { e.MovementType, e.CreatedAt });
        entity.HasIndex(e => e.MovementType);
        entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId }).IncludeProperties(e => e.Quantity);
        entity.ToTable(table => table.HasCheckConstraint(
            "CK_StockMovements_QuantitySign",
            "\"Quantity\" <> 0 AND ((\"MovementType\" IN (1, 2, 4, 6, 8) AND \"Quantity\" > 0) OR (\"MovementType\" IN (3, 5, 7, 9, 10, 11) AND \"Quantity\" < 0))"));

        entity.HasOne(e => e.Branch)
            .WithMany(b => b.StockMovements)
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Godown)
            .WithMany(g => g.StockMovements)
            .HasForeignKey(e => e.GodownId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Product)
            .WithMany(p => p.StockMovements)
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.ProductBatch)
            .WithMany(pb => pb.StockMovements)
            .HasForeignKey(e => e.ProductBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.PerformedByUser)
            .WithMany(u => u.StockMovements)
            .HasForeignKey(e => e.PerformedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigureStockCountSession(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockCountSession>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CountNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.CountDate).HasColumnType("date").IsRequired();

        entity.HasIndex(e => e.CountNumber).IsUnique();
        entity.HasIndex(e => new { e.BranchId, e.CountDate });
        entity.HasIndex(e => new { e.GodownId, e.CountDate });
        entity.HasIndex(e => e.Status);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_StockCountSessions_Status", "\"Status\" IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_StockCountSessions_Scope", "\"Scope\" IN (1, 2, 3, 4)");
        });

        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Godown).WithMany().HasForeignKey(e => e.GodownId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.StartedByUser).WithMany().HasForeignKey(e => e.StartedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CompletedByUser).WithMany().HasForeignKey(e => e.CompletedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CancelledByUser).WithMany().HasForeignKey(e => e.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureStockCountItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockCountItem>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.UnitCostSnapshot).HasPrecision(18, 2);
        entity.Property(e => e.Reason).HasMaxLength(50);
        entity.Property(e => e.Notes).HasMaxLength(500);

        entity.HasIndex(e => e.StockCountSessionId);
        entity.HasIndex(e => new { e.StockCountSessionId, e.ProductBatchId }).IsUnique();
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_StockCountItems_SystemQuantity_NonNegative", "\"SystemQuantity\" >= 0");
            table.HasCheckConstraint("CK_StockCountItems_CountedQuantity_NonNegative", "\"CountedQuantity\" IS NULL OR \"CountedQuantity\" >= 0");
        });

        entity.HasOne(e => e.StockCountSession).WithMany(s => s.Items).HasForeignKey(e => e.StockCountSessionId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ProductBatch).WithMany().HasForeignKey(e => e.ProductBatchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CountedByUser).WithMany().HasForeignKey(e => e.CountedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureCashierShift(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CashierShift>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.TerminalName).HasMaxLength(100);
        entity.Property(e => e.OpeningNotes).HasMaxLength(500);
        entity.Property(e => e.ClosingNotes).HasMaxLength(500);
        entity.Property(e => e.ReconciliationNotes).HasMaxLength(500);
        entity.Property(e => e.OpeningCash).HasPrecision(18, 2);
        entity.Property(e => e.ExpectedCash).HasPrecision(18, 2);
        entity.Property(e => e.ActualCountedCash).HasPrecision(18, 2);
        entity.Property(e => e.CashVariance).HasPrecision(18, 2);
        entity.Property(e => e.CustomerCashReceivedSnapshot).HasPrecision(18, 2);
        entity.Property(e => e.CashPaidOutSnapshot).HasPrecision(18, 2);

        entity.HasIndex(e => new { e.CashierUserId, e.Status });
        entity.HasIndex(e => new { e.BranchId, e.OpenedAtUtc });
        entity.HasIndex(e => new { e.BranchId, e.ClosedAtUtc });
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.FinancialAccountId);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_CashierShifts_Status", "\"Status\" IN (1, 2, 3)");
            table.HasCheckConstraint("CK_CashierShifts_OpeningCash_NonNegative", "\"OpeningCash\" >= 0");
            table.HasCheckConstraint("CK_CashierShifts_ActualCountedCash_NonNegative", "\"ActualCountedCash\" IS NULL OR \"ActualCountedCash\" >= 0");
            table.HasCheckConstraint("CK_CashierShifts_Closed_Fields", "(\"Status\" = 1 AND \"ClosedAtUtc\" IS NULL) OR (\"Status\" <> 1 AND \"ClosedAtUtc\" IS NOT NULL AND \"ExpectedCash\" IS NOT NULL AND \"ActualCountedCash\" IS NOT NULL)");
            table.HasCheckConstraint("CK_CashierShifts_Reconciled_Fields", "\"Status\" <> 3 OR (\"ReconciledByUserId\" IS NOT NULL AND \"ReconciledAtUtc\" IS NOT NULL)");
        });

        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CashierUser).WithMany().HasForeignKey(e => e.CashierUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ReconciledByUser).WithMany().HasForeignKey(e => e.ReconciledByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.FinancialAccount).WithMany().HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureCashierShiftDrawerEntry(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CashierShiftDrawerEntry>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Reason).IsRequired().HasMaxLength(300);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.HasIndex(e => e.CashierShiftId);
        entity.ToTable(table => table.HasCheckConstraint("CK_CashierShiftDrawerEntries_Amount_Positive", "\"Amount\" > 0"));
        entity.HasOne(e => e.CashierShift).WithMany(s => s.DrawerEntries).HasForeignKey(e => e.CashierShiftId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureCashierShiftPaymentSummary(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CashierShiftPaymentSummary>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SalesAmount).HasPrecision(18, 2);
        entity.Property(e => e.RefundsAmount).HasPrecision(18, 2);
        entity.HasIndex(e => new { e.CashierShiftId, e.PaymentMethod }).IsUnique();
        entity.ToTable(table => table.HasCheckConstraint("CK_CashierShiftPaymentSummaries_NonNegative", "\"SalesAmount\" >= 0 AND \"RefundsAmount\" >= 0"));
        entity.HasOne(e => e.CashierShift).WithMany(s => s.PaymentSummaries).HasForeignKey(e => e.CashierShiftId).OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureChartOfAccount(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ChartOfAccount>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
        entity.Property(e => e.NormalizedCode).IsRequired().HasMaxLength(20);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.HasIndex(e => e.NormalizedCode).IsUnique();
        entity.HasIndex(e => e.ParentAccountId);
        entity.HasIndex(e => new { e.AccountType, e.IsActive });
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ChartOfAccounts_AccountType", "\"AccountType\" IN (1, 2, 3, 4, 5, 6)");
            table.HasCheckConstraint("CK_ChartOfAccounts_NormalBalance", "\"NormalBalance\" IN (1, 2)");
            table.HasCheckConstraint("CK_ChartOfAccounts_CashFlowClassification", "\"CashFlowClassification\" IS NULL OR \"CashFlowClassification\" BETWEEN 1 AND 3");
        });
        entity.HasOne(e => e.ParentAccount).WithMany(e => e.ChildAccounts).HasForeignKey(e => e.ParentAccountId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureAccountMapping(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AccountMapping>();
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.MappingKey).IsUnique();
        entity.ToTable(table => table.HasCheckConstraint("CK_AccountMappings_MappingKey", "\"MappingKey\" BETWEEN 1 AND 22"));
        entity.HasOne(e => e.ChartOfAccount).WithMany().HasForeignKey(e => e.ChartOfAccountId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureJournalEntry(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<JournalEntry>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.EntryNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Reference).HasMaxLength(100);
        entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
        entity.Property(e => e.ReversalReason).HasMaxLength(500);
        entity.HasIndex(e => e.EntryNumber).IsUnique();
        entity.HasIndex(e => new { e.BranchId, e.EntryDateUtc });
        entity.HasIndex(e => new { e.SourceType, e.SourceId })
            .IsUnique()
            .HasFilter("\"SourceId\" IS NOT NULL");
        entity.HasIndex(e => e.EntryDateUtc);
        entity.HasIndex(e => e.ReversesJournalEntryId).IsUnique().HasFilter("\"ReversesJournalEntryId\" IS NOT NULL");
        entity.ToTable(table => table.HasCheckConstraint("CK_JournalEntries_Status", "\"Status\" = 1"));
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.PostedByUser).WithMany().HasForeignKey(e => e.PostedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ReversesJournalEntry).WithMany().HasForeignKey(e => e.ReversesJournalEntryId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureJournalEntryLine(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<JournalEntryLine>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Debit).HasPrecision(18, 2);
        entity.Property(e => e.Credit).HasPrecision(18, 2);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.HasIndex(e => e.JournalEntryId);
        entity.HasIndex(e => new { e.ChartOfAccountId, e.BranchId });
        entity.HasIndex(e => e.CustomerId);
        entity.HasIndex(e => e.SupplierId);
        entity.HasIndex(e => e.CostCenterId);
        entity.ToTable(table => table.HasCheckConstraint("CK_JournalEntryLines_Amounts",
            "\"Debit\" >= 0 AND \"Credit\" >= 0 AND NOT (\"Debit\" > 0 AND \"Credit\" > 0) AND (\"Debit\" > 0 OR \"Credit\" > 0)"));
        entity.HasOne(e => e.JournalEntry).WithMany(j => j.Lines).HasForeignKey(e => e.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.ChartOfAccount).WithMany().HasForeignKey(e => e.ChartOfAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureCustomerPaymentAllocation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CustomerPaymentAllocation>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.AllocatedAmount).HasPrecision(18, 2);
        entity.HasIndex(e => e.CustomerPaymentId);
        entity.HasIndex(e => e.SaleId);
        entity.HasIndex(e => new { e.CustomerId, e.BranchId });
        entity.ToTable(table => table.HasCheckConstraint("CK_CustomerPaymentAllocations_Amount_Positive", "\"AllocatedAmount\" > 0"));
        entity.HasOne(e => e.CustomerPayment).WithMany().HasForeignKey(e => e.CustomerPaymentId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Sale).WithMany().HasForeignKey(e => e.SaleId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSupplierPaymentAllocation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SupplierPaymentAllocation>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.AllocatedAmount).HasPrecision(18, 2);
        entity.HasIndex(e => e.SupplierLedgerEntryId);
        entity.HasIndex(e => e.GoodsReceiptId);
        entity.HasIndex(e => new { e.SupplierId, e.BranchId });
        entity.ToTable(table => table.HasCheckConstraint("CK_SupplierPaymentAllocations_Amount_Positive", "\"AllocatedAmount\" > 0"));
        entity.HasOne(e => e.SupplierLedgerEntry).WithMany().HasForeignKey(e => e.SupplierLedgerEntryId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.GoodsReceipt).WithMany().HasForeignKey(e => e.GoodsReceiptId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureVoucher(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Voucher>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.VoucherNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Reference).HasMaxLength(100);
        entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.HasIndex(e => e.VoucherNumber).IsUnique();
        entity.HasIndex(e => new { e.BranchId, e.VoucherDateUtc });
        entity.HasIndex(e => new { e.Type, e.Status });
        entity.HasIndex(e => e.CustomerId);
        entity.HasIndex(e => e.SupplierId);
        entity.HasIndex(e => e.JournalEntryId);
        entity.HasIndex(e => e.FinancialAccountId);
        entity.HasIndex(e => e.ReversalOfVoucherId).IsUnique().HasFilter("\"ReversalOfVoucherId\" IS NOT NULL");
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Vouchers_Type", "\"Type\" BETWEEN 1 AND 6");
            table.HasCheckConstraint("CK_Vouchers_Status", "\"Status\" BETWEEN 1 AND 3");
            table.HasCheckConstraint("CK_Vouchers_Posted_HasJournalEntry", "(\"Status\" <> 2) OR (\"JournalEntryId\" IS NOT NULL AND \"PostedByUserId\" IS NOT NULL AND \"PostedAtUtc\" IS NOT NULL)");
        });
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.FinancialAccount).WithMany().HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ChartOfAccount).WithMany().HasForeignKey(e => e.ChartOfAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ContraToChartOfAccount).WithMany().HasForeignKey(e => e.ContraToChartOfAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.PostedByUser).WithMany().HasForeignKey(e => e.PostedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.JournalEntry).WithMany().HasForeignKey(e => e.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ReversalOfVoucher).WithMany().HasForeignKey(e => e.ReversalOfVoucherId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureVoucherLine(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<VoucherLine>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Debit).HasPrecision(18, 2);
        entity.Property(e => e.Credit).HasPrecision(18, 2);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.HasIndex(e => e.VoucherId);
        entity.ToTable(table => table.HasCheckConstraint("CK_VoucherLines_Amounts",
            "\"Debit\" >= 0 AND \"Credit\" >= 0 AND NOT (\"Debit\" > 0 AND \"Credit\" > 0) AND (\"Debit\" > 0 OR \"Credit\" > 0)"));
        entity.HasOne(e => e.Voucher).WithMany(v => v.Lines).HasForeignKey(e => e.VoucherId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.ChartOfAccount).WithMany().HasForeignKey(e => e.ChartOfAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureStockTransfer(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockTransfer>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.TransferNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.TransferDate).HasColumnType("date").IsRequired();
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.CancellationReason).HasMaxLength(500);

        entity.HasIndex(e => e.TransferNumber).IsUnique();
        entity.HasIndex(e => new { e.SourceBranchId, e.TransferDate });
        entity.HasIndex(e => new { e.SourceGodownId, e.TransferDate });
        entity.HasIndex(e => new { e.DestinationBranchId, e.TransferDate });
        entity.HasIndex(e => new { e.DestinationGodownId, e.TransferDate });
        entity.HasIndex(e => e.Status);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_StockTransfers_Status", "\"Status\" IN (1, 2, 3, 4, 5, 6, 7)");
            table.HasCheckConstraint("CK_StockTransfers_Source_Destination_Godown_Different", "\"SourceGodownId\" <> \"DestinationGodownId\"");
        });

        entity.HasOne(e => e.SourceBranch).WithMany().HasForeignKey(e => e.SourceBranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.SourceGodown).WithMany().HasForeignKey(e => e.SourceGodownId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.DestinationBranch).WithMany().HasForeignKey(e => e.DestinationBranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.DestinationGodown).WithMany().HasForeignKey(e => e.DestinationGodownId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.RequestedByUser).WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ApprovedByUser).WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.DispatchedByUser).WithMany().HasForeignKey(e => e.DispatchedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ReceivedByUser).WithMany().HasForeignKey(e => e.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CancelledByUser).WithMany().HasForeignKey(e => e.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureStockTransferItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StockTransferItem>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.BatchNumber).IsRequired().HasMaxLength(100);
        entity.Property(e => e.ExpiryDate).HasColumnType("date").IsRequired();
        entity.Property(e => e.UnitCostSnapshot).HasPrecision(18, 2);
        entity.Property(e => e.Notes).HasMaxLength(500);

        entity.HasIndex(e => e.StockTransferId);
        entity.HasIndex(e => new { e.StockTransferId, e.SourceProductBatchId }).IsUnique();
        entity.HasIndex(e => e.ProductId);
        entity.HasIndex(e => e.SourceProductBatchId);
        entity.HasIndex(e => e.DestinationProductBatchId);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_StockTransferItems_Requested_Positive", "\"QuantityRequested\" > 0");
            table.HasCheckConstraint("CK_StockTransferItems_Approved_Range", "\"QuantityApproved\" >= 0 AND \"QuantityApproved\" <= \"QuantityRequested\"");
            table.HasCheckConstraint("CK_StockTransferItems_Dispatched_Range", "\"QuantityDispatched\" >= 0 AND \"QuantityDispatched\" <= \"QuantityApproved\"");
            table.HasCheckConstraint("CK_StockTransferItems_Received_Range", "\"QuantityReceived\" >= 0 AND \"QuantityReceived\" <= \"QuantityDispatched\"");
            table.HasCheckConstraint("CK_StockTransferItems_UnitCost_NonNegative", "\"UnitCostSnapshot\" >= 0");
        });

        entity.HasOne(e => e.StockTransfer).WithMany(t => t.Items).HasForeignKey(e => e.StockTransferId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.SourceProductBatch).WithMany().HasForeignKey(e => e.SourceProductBatchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.DestinationProductBatch).WithMany().HasForeignKey(e => e.DestinationProductBatchId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigurePriceLevel(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PriceLevel>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Code).IsRequired().HasMaxLength(30);
        entity.HasIndex(e => e.Code).IsUnique();
        entity.HasIndex(e => e.BranchId);
        entity.HasIndex(e => e.IsDefault).HasFilter("\"IsDefault\" = true");
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureProductPriceLevel(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductPriceLevel>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SellingPrice).HasPrecision(18, 2);
        entity.HasIndex(e => new { e.ProductId, e.PriceLevelId }).IsUnique();
        entity.ToTable(table => table.HasCheckConstraint("CK_ProductPriceLevels_SellingPrice_NonNegative", "\"SellingPrice\" >= 0"));
        entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.PriceLevel).WithMany(l => l.ProductPrices).HasForeignKey(e => e.PriceLevelId).OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureProductPriceBreak(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProductPriceBreak>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SellingPrice).HasPrecision(18, 2);
        entity.HasIndex(e => new { e.ProductId, e.PriceLevelId, e.MinimumQuantity }).IsUnique();
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ProductPriceBreaks_SellingPrice_NonNegative", "\"SellingPrice\" >= 0");
            table.HasCheckConstraint("CK_ProductPriceBreaks_MinimumQuantity_Positive", "\"MinimumQuantity\" > 0");
        });
        entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.PriceLevel).WithMany(l => l.ProductPriceBreaks).HasForeignKey(e => e.PriceLevelId).OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureSalesQuotation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SalesQuotation>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.QuotationNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.CancellationReason).HasMaxLength(500);
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        entity.Property(e => e.DiscountTotal).HasPrecision(18, 2);
        entity.Property(e => e.NetTotal).HasPrecision(18, 2);
        entity.HasIndex(e => e.QuotationNumber).IsUnique();
        entity.HasIndex(e => new { e.CustomerId, e.QuotationDate });
        entity.HasIndex(e => new { e.BranchId, e.QuotationDate });
        entity.HasIndex(e => e.Status);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SalesQuotations_Status", "\"Status\" IN (1, 2, 3, 4, 5, 6, 7)");
            table.HasCheckConstraint("CK_SalesQuotations_Money_NonNegative", "\"Subtotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"NetTotal\" >= 0");
        });
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Godown).WithMany().HasForeignKey(e => e.GodownId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.PriceLevel).WithMany().HasForeignKey(e => e.PriceLevelId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ApprovedByUser).WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ConvertedToSalesOrder).WithMany().HasForeignKey(e => e.ConvertedToSalesOrderId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ConvertedToSale).WithMany().HasForeignKey(e => e.ConvertedToSaleId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSalesQuotationItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SalesQuotationItem>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
        entity.Property(e => e.DiscountPercent).HasPrecision(5, 2);
        entity.Property(e => e.GrossAmount).HasPrecision(18, 2);
        entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
        entity.Property(e => e.NetAmount).HasPrecision(18, 2);
        entity.HasIndex(e => e.SalesQuotationId);
        entity.HasIndex(e => e.ProductId);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SalesQuotationItems_Quantity_Positive", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_SalesQuotationItems_Discount_Range", "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100");
            table.HasCheckConstraint("CK_SalesQuotationItems_Money_NonNegative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"DiscountAmount\" >= 0 AND \"NetAmount\" >= 0");
        });
        entity.HasOne(e => e.SalesQuotation).WithMany(q => q.Items).HasForeignKey(e => e.SalesQuotationId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSalesOrder(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SalesOrder>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.CancellationReason).HasMaxLength(500);
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        entity.Property(e => e.DiscountTotal).HasPrecision(18, 2);
        entity.Property(e => e.NetTotal).HasPrecision(18, 2);
        entity.HasIndex(e => e.OrderNumber).IsUnique();
        entity.HasIndex(e => new { e.CustomerId, e.OrderDate });
        entity.HasIndex(e => new { e.BranchId, e.OrderDate });
        entity.HasIndex(e => e.Status);
        entity.HasIndex(e => e.QuotationId);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SalesOrders_Status", "\"Status\" IN (1, 2, 3, 4, 5)");
            table.HasCheckConstraint("CK_SalesOrders_Money_NonNegative", "\"Subtotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"NetTotal\" >= 0");
        });
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Godown).WithMany().HasForeignKey(e => e.GodownId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.PriceLevel).WithMany().HasForeignKey(e => e.PriceLevelId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.Quotation).WithMany().HasForeignKey(e => e.QuotationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ConfirmedByUser).WithMany().HasForeignKey(e => e.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSalesOrderItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SalesOrderItem>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
        entity.Property(e => e.DiscountPercent).HasPrecision(5, 2);
        entity.Property(e => e.GrossAmount).HasPrecision(18, 2);
        entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
        entity.Property(e => e.NetAmount).HasPrecision(18, 2);
        entity.HasIndex(e => e.SalesOrderId);
        entity.HasIndex(e => e.ProductId);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SalesOrderItems_Quantity_Positive", "\"OrderedQuantity\" > 0");
            table.HasCheckConstraint("CK_SalesOrderItems_Fulfilled_Range", "\"FulfilledQuantity\" >= 0 AND \"FulfilledQuantity\" <= \"OrderedQuantity\"");
            table.HasCheckConstraint("CK_SalesOrderItems_Discount_Range", "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100");
            table.HasCheckConstraint("CK_SalesOrderItems_Money_NonNegative", "\"UnitPrice\" >= 0 AND \"GrossAmount\" >= 0 AND \"DiscountAmount\" >= 0 AND \"NetAmount\" >= 0");
        });
        entity.HasOne(e => e.SalesOrder).WithMany(o => o.Items).HasForeignKey(e => e.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AuditLog>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
        entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
        entity.Property(e => e.OldValues).HasColumnType("jsonb");
        entity.Property(e => e.NewValues).HasColumnType("jsonb");
        entity.Property(e => e.IPAddress).HasMaxLength(45);
        entity.Property(e => e.UserAgent).HasMaxLength(500);

        entity.HasIndex(e => new { e.UserId, e.CreatedAt });
        entity.HasIndex(e => new { e.EntityType, e.EntityId });
        entity.HasIndex(e => e.CreatedAt);

        entity.HasOne(e => e.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSupplierLedgerEntry(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SupplierLedgerEntry>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.EntryDate).HasColumnType("date").IsRequired();
        entity.Property(e => e.PaymentMethod).HasMaxLength(50);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
        entity.Property(e => e.ReferenceType).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => new { e.SupplierId, e.CreatedAt });
        entity.HasIndex(e => new { e.SupplierId, e.BranchId, e.CreatedAt });
        entity.HasIndex(e => new { e.BranchId, e.CreatedAt });
        entity.HasIndex(e => new { e.EntryType, e.CreatedAt });
        entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId });
        entity.ToTable(table => table.HasCheckConstraint(
            "CK_SupplierLedgerEntries_AmountSign",
            "\"Amount\" <> 0 AND ((\"EntryType\" = 1) OR (\"EntryType\" IN (3, 5) AND \"Amount\" > 0) OR (\"EntryType\" IN (2, 4, 6) AND \"Amount\" < 0))"));

        entity.HasOne(e => e.Supplier)
            .WithMany(s => s.LedgerEntries)
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.FinancialAccount).WithMany().HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureCustomer(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Customer>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.CustomerCode).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.NormalizedName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.PhoneNumber).HasMaxLength(30);
        entity.Property(e => e.AlternatePhone).HasMaxLength(30);
        entity.Property(e => e.Email).HasMaxLength(100);
        entity.Property(e => e.Address).HasMaxLength(500);
        entity.Property(e => e.City).HasMaxLength(100);
        entity.Property(e => e.BusinessName).HasMaxLength(200);
        entity.Property(e => e.NTN).HasMaxLength(50);
        entity.Property(e => e.OpeningBalance).HasPrecision(18, 2);
        entity.Property(e => e.CreditLimit).HasPrecision(18, 2);
        entity.HasIndex(e => e.CustomerCode).IsUnique();
        entity.HasIndex(e => e.Name);
        entity.HasIndex(e => e.NormalizedName);
        entity.HasIndex(e => e.PhoneNumber);
        entity.Property(e => e.ContactPerson).HasMaxLength(200);
        entity.Property(e => e.ShippingAddress).HasMaxLength(500);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.CreditAllowed).HasDefaultValue(true);
        entity.Property(e => e.CustomerType).HasDefaultValue(CustomerType.Retail);
        entity.HasIndex(e => e.Email);
        entity.HasIndex(e => e.City);
        entity.HasIndex(e => e.IsActive);
        entity.HasIndex(e => e.CustomerType);
        entity.HasIndex(e => e.PriceLevelId);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Customers_CreditLimit_NonNegative", "\"CreditLimit\" >= 0");
            table.HasCheckConstraint("CK_Customers_CreditDays_NonNegative", "\"CreditDays\" IS NULL OR \"CreditDays\" >= 0");
            table.HasCheckConstraint("CK_Customers_CustomerType", "\"CustomerType\" IN (1, 2, 3)");
        });
        entity.HasOne(e => e.PriceLevel).WithMany().HasForeignKey(e => e.PriceLevelId).OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigureCustomerLedgerEntry(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CustomerLedgerEntry>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.EntryDate).HasColumnType("date").IsRequired();
        entity.Property(e => e.PaymentMethod).HasMaxLength(50);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
        entity.Property(e => e.ReferenceType).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => new { e.CustomerId, e.CreatedAt });
        entity.HasIndex(e => new { e.CustomerId, e.BranchId, e.CreatedAt });
        entity.HasIndex(e => new { e.BranchId, e.CreatedAt });
        entity.HasIndex(e => new { e.EntryType, e.CreatedAt });
        entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId });
        entity.ToTable(table => table.HasCheckConstraint(
            "CK_CustomerLedgerEntries_AmountSign",
            "\"Amount\" <> 0 AND ((\"EntryType\" = 1) OR (\"EntryType\" IN (2, 5) AND \"Amount\" > 0) OR (\"EntryType\" IN (3, 4, 6) AND \"Amount\" < 0))"));

        entity.HasOne(e => e.Customer)
            .WithMany(c => c.LedgerEntries)
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigureCustomerPayment(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CustomerPayment>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.ReceiptNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.ReceiptNumber).IsUnique();
        entity.HasIndex(e => new { e.CustomerId, e.PaymentDateUtc });
        entity.HasIndex(e => new { e.BranchId, e.PaymentDateUtc });
        entity.HasIndex(e => new { e.PaymentMethod, e.PaymentDateUtc });
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_CustomerPayments_Method", "\"PaymentMethod\" IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10)");
            table.HasCheckConstraint("CK_CustomerPayments_Amount_Positive", "\"Amount\" > 0");
        });

        entity.HasOne(e => e.Customer)
            .WithMany(c => c.Payments)
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ReceivedByUser)
            .WithMany()
            .HasForeignKey(e => e.ReceivedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.FinancialAccount).WithMany().HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigurePurchaseOrder(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PurchaseOrder>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.SupplierReference).HasMaxLength(100);
        entity.Property(e => e.OrderDate).HasColumnType("date").IsRequired();
        entity.Property(e => e.ExpectedDate).HasColumnType("date");
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.OrderNumber).IsUnique();
        entity.HasIndex(e => new { e.BranchId, e.OrderDate });
        entity.HasIndex(e => new { e.SupplierId, e.OrderDate });
        entity.HasIndex(e => new { e.Status, e.OrderDate });
        entity.ToTable(table => table.HasCheckConstraint("CK_PurchaseOrders_Status", "\"Status\" IN (1, 2, 3, 4, 5)"));

        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigurePurchaseOrderItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PurchaseOrderItem>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.ExpectedPurchasePrice).HasPrecision(18, 2);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.PurchaseOrderId);
        entity.HasIndex(e => e.ProductId);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_PurchaseOrderItems_OrderedQuantity_Positive", "\"OrderedQuantity\" > 0");
            table.HasCheckConstraint("CK_PurchaseOrderItems_ReceivedQuantity_Range", "\"ReceivedQuantity\" >= 0 AND \"ReceivedQuantity\" <= \"OrderedQuantity\"");
            table.HasCheckConstraint("CK_PurchaseOrderItems_ExpectedPurchasePrice_NonNegative", "\"ExpectedPurchasePrice\" IS NULL OR \"ExpectedPurchasePrice\" >= 0");
        });

        entity.HasOne(e => e.PurchaseOrder).WithMany(o => o.Items).HasForeignKey(e => e.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureGoodsReceipt(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GoodsReceipt>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.GrnNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.SupplierInvoiceNumber).HasMaxLength(100);
        entity.Property(e => e.NormalizedSupplierInvoiceNumber).HasMaxLength(100);
        entity.Property(e => e.ReceiptDate).HasColumnType("date").IsRequired();
        entity.Property(e => e.DueDate).HasColumnType("date");
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        entity.Property(e => e.DiscountTotal).HasPrecision(18, 2);
        entity.Property(e => e.TaxTotal).HasPrecision(18, 2);
        entity.Property(e => e.NetTotal).HasPrecision(18, 2);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.GrnNumber).IsUnique();
        entity.HasIndex(e => new { e.SupplierId, e.NormalizedSupplierInvoiceNumber }).IsUnique().HasFilter("\"NormalizedSupplierInvoiceNumber\" IS NOT NULL");
        entity.HasIndex(e => new { e.BranchId, e.ReceiptDate });
        entity.HasIndex(e => new { e.SupplierId, e.ReceiptDate });
        entity.HasIndex(e => new { e.Status, e.ReceiptDate });
        entity.HasIndex(e => e.DueDate);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_GoodsReceipts_Status", "\"Status\" IN (1, 2, 3)");
            table.HasCheckConstraint("CK_GoodsReceipts_Totals_NonNegative", "\"Subtotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"TaxTotal\" >= 0 AND \"NetTotal\" >= 0");
        });

        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Godown).WithMany().HasForeignKey(e => e.GodownId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.PurchaseOrder).WithMany(o => o.GoodsReceipts).HasForeignKey(e => e.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ReceivedByUser).WithMany().HasForeignKey(e => e.ReceivedByUserId).OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigureGoodsReceiptItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<GoodsReceiptItem>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.BatchNumber).IsRequired().HasMaxLength(100);
        entity.Property(e => e.ManufacturingDate).HasColumnType("date");
        entity.Property(e => e.ExpiryDate).HasColumnType("date").IsRequired();
        entity.Property(e => e.PurchasePrice).HasPrecision(18, 2);
        entity.Property(e => e.RetailPrice).HasPrecision(18, 2);
        entity.Property(e => e.DiscountPercent).HasPrecision(5, 2);
        entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
        entity.Property(e => e.TaxPercent).HasPrecision(5, 2);
        entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
        entity.Property(e => e.NetLineAmount).HasPrecision(18, 2);
        entity.HasIndex(e => e.GoodsReceiptId);
        entity.HasIndex(e => e.ProductId);
        entity.HasIndex(e => e.PurchaseOrderItemId);
        entity.HasIndex(e => e.ProductBatchId);
        entity.HasIndex(e => new { e.ProductId, e.BatchNumber });
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_GoodsReceiptItems_Quantities", "\"PurchasedQuantity\" > 0 AND \"BonusQuantity\" >= 0");
            table.HasCheckConstraint("CK_GoodsReceiptItems_Prices_NonNegative", "\"PurchasePrice\" >= 0 AND \"RetailPrice\" >= 0");
            table.HasCheckConstraint("CK_GoodsReceiptItems_Discount_Range", "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100 AND \"DiscountAmount\" >= 0");
            table.HasCheckConstraint("CK_GoodsReceiptItems_Tax_Range", "\"TaxPercent\" >= 0 AND \"TaxPercent\" <= 100 AND \"TaxAmount\" >= 0");
            table.HasCheckConstraint("CK_GoodsReceiptItems_NetLineAmount_NonNegative", "\"NetLineAmount\" >= 0");
            table.HasCheckConstraint("CK_GoodsReceiptItems_Manufacturing_Before_Expiry", "\"ManufacturingDate\" IS NULL OR \"ManufacturingDate\" <= \"ExpiryDate\"");
        });

        entity.HasOne(e => e.GoodsReceipt).WithMany(r => r.Items).HasForeignKey(e => e.GoodsReceiptId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.PurchaseOrderItem).WithMany(i => i.GoodsReceiptItems).HasForeignKey(e => e.PurchaseOrderItemId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ProductBatch).WithMany().HasForeignKey(e => e.ProductBatchId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigurePurchaseReturn(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PurchaseReturn>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ReturnNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.ReturnDateUtc).IsRequired();
        entity.Property(e => e.PostedAtUtc).IsRequired();
        entity.Property(e => e.GrossReturnAmount).HasPrecision(18, 2);
        entity.Property(e => e.DiscountAdjustment).HasPrecision(18, 2);
        entity.Property(e => e.TaxAdjustment).HasPrecision(18, 2);
        entity.Property(e => e.NetSupplierCredit).HasPrecision(18, 2);
        entity.HasIndex(e => e.ReturnNumber).IsUnique();
        entity.HasIndex(e => e.OriginalGoodsReceiptId);
        entity.HasIndex(e => new { e.SupplierId, e.PostedAtUtc });
        entity.HasIndex(e => new { e.BranchId, e.PostedAtUtc });
        entity.HasIndex(e => new { e.ProcessedByUserId, e.PostedAtUtc });
        entity.HasIndex(e => e.Reason);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_PurchaseReturns_Status", "\"Status\" IN (1)");
            table.HasCheckConstraint("CK_PurchaseReturns_Reason", "\"Reason\" IN (1, 2, 3, 4, 5, 6)");
            table.HasCheckConstraint("CK_PurchaseReturns_Posted", "\"Status\" = 1 AND \"PostedAtUtc\" IS NOT NULL");
            table.HasCheckConstraint("CK_PurchaseReturns_Money_NonNegative", "\"GrossReturnAmount\" >= 0 AND \"DiscountAdjustment\" >= 0 AND \"TaxAdjustment\" >= 0 AND \"NetSupplierCredit\" >= 0");
        });
        entity.HasOne(e => e.OriginalGoodsReceipt).WithMany().HasForeignKey(e => e.OriginalGoodsReceiptId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Godown).WithMany().HasForeignKey(e => e.GodownId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ProcessedByUser).WithMany().HasForeignKey(e => e.ProcessedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigurePurchaseReturnItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PurchaseReturnItem>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.BatchNumber).IsRequired().HasMaxLength(100);
        entity.Property(e => e.ExpiryDate).HasColumnType("date").IsRequired();
        entity.Property(e => e.PurchasePriceSnapshot).HasPrecision(18, 2);
        entity.Property(e => e.GrossReturnAmount).HasPrecision(18, 2);
        entity.Property(e => e.DiscountAdjustment).HasPrecision(18, 2);
        entity.Property(e => e.TaxAdjustment).HasPrecision(18, 2);
        entity.Property(e => e.NetSupplierCredit).HasPrecision(18, 2);
        entity.HasIndex(e => e.PurchaseReturnId);
        entity.HasIndex(e => e.OriginalGoodsReceiptItemId);
        entity.HasIndex(e => e.ProductBatchId);
        entity.HasIndex(e => new { e.OriginalGoodsReceiptItemId, e.PurchaseReturnId });
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_PurchaseReturnItems_Quantities", "\"PaidReturnQuantity\" >= 0 AND \"BonusReturnQuantity\" >= 0 AND (\"PaidReturnQuantity\" + \"BonusReturnQuantity\") > 0");
            table.HasCheckConstraint("CK_PurchaseReturnItems_Money_NonNegative", "\"PurchasePriceSnapshot\" >= 0 AND \"GrossReturnAmount\" >= 0 AND \"DiscountAdjustment\" >= 0 AND \"TaxAdjustment\" >= 0 AND \"NetSupplierCredit\" >= 0");
        });
        entity.HasOne(e => e.PurchaseReturn).WithMany(r => r.Items).HasForeignKey(e => e.PurchaseReturnId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.OriginalGoodsReceiptItem).WithMany().HasForeignKey(e => e.OriginalGoodsReceiptItemId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ProductBatch).WithMany().HasForeignKey(e => e.ProductBatchId).OnDelete(DeleteBehavior.Restrict);
    }
    private void ConfigureSale(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Sale>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.InvoiceNumber).HasMaxLength(50);
        entity.Property(e => e.HoldNumber).HasMaxLength(50);
        entity.Property(e => e.CustomerName).HasMaxLength(200);
        entity.Property(e => e.CustomerPhone).HasMaxLength(30);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        entity.Property(e => e.DiscountTotal).HasPrecision(18, 2);
        entity.Property(e => e.TaxTotal).HasPrecision(18, 2);
        entity.Property(e => e.NetTotal).HasPrecision(18, 2);
        entity.Property(e => e.AmountPaid).HasPrecision(18, 2);
        entity.Property(e => e.CreditAmount).HasPrecision(18, 2);
        entity.Property(e => e.ChangeGiven).HasPrecision(18, 2);
        entity.Property(e => e.CustomerPoNumber).HasMaxLength(100);
        entity.Property(e => e.SaleType).HasDefaultValue(SaleType.Retail);
        entity.HasIndex(e => e.InvoiceNumber).IsUnique().HasFilter("\"InvoiceNumber\" IS NOT NULL");
        entity.HasIndex(e => e.HoldNumber).IsUnique().HasFilter("\"HoldNumber\" IS NOT NULL");
        entity.HasIndex(e => new { e.BranchId, e.PostedAtUtc });
        entity.HasIndex(e => new { e.CashierUserId, e.PostedAtUtc });
        entity.HasIndex(e => new { e.CustomerId, e.PostedAtUtc });
        entity.HasIndex(e => new { e.Status, e.CreatedAt });
        entity.HasIndex(e => e.CustomerPhone);
        entity.HasIndex(e => e.DueDateUtc);
        entity.HasIndex(e => e.SaleType);
        entity.HasIndex(e => e.QuotationId);
        entity.HasIndex(e => e.SalesOrderId);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Sales_Status", "\"Status\" IN (1, 2, 3)");
            table.HasCheckConstraint("CK_Sales_Posted_HasInvoice", "(\"Status\" <> 2) OR (\"InvoiceNumber\" IS NOT NULL AND \"PostedAtUtc\" IS NOT NULL)");
            table.HasCheckConstraint("CK_Sales_Money_NonNegative", "\"Subtotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"TaxTotal\" >= 0 AND \"NetTotal\" >= 0 AND \"AmountPaid\" >= 0 AND \"CreditAmount\" >= 0 AND \"ChangeGiven\" >= 0");
            table.HasCheckConstraint("CK_Sales_Posted_Settled", "(\"Status\" <> 2) OR (\"AmountPaid\" + \"CreditAmount\" = \"NetTotal\")");
            table.HasCheckConstraint("CK_Sales_CreditRequiresCustomer", "\"CreditAmount\" = 0 OR \"CustomerId\" IS NOT NULL");
            table.HasCheckConstraint("CK_Sales_SaleType", "\"SaleType\" IN (1, 2)");
        });
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Godown).WithMany().HasForeignKey(e => e.GodownId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CashierUser).WithMany().HasForeignKey(e => e.CashierUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Customer).WithMany(c => c.Sales).HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.PriceLevel).WithMany().HasForeignKey(e => e.PriceLevelId).OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.Quotation).WithMany().HasForeignKey(e => e.QuotationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.SalesOrder).WithMany().HasForeignKey(e => e.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSaleItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SaleItem>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DiscountPercent).HasPrecision(5, 2);
        entity.Property(e => e.GrossAmount).HasPrecision(18, 2);
        entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
        entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
        entity.Property(e => e.NetAmount).HasPrecision(18, 2);
        entity.Property(e => e.ResolvedUnitPrice).HasPrecision(18, 2);
        entity.Property(e => e.PriceSource).HasDefaultValue(PriceSource.Default);
        entity.Property(e => e.PriceOverrideReason).HasMaxLength(500);
        entity.Property(e => e.DiscountOverrideReason).HasMaxLength(500);
        entity.Property(e => e.BelowCostOverrideReason).HasMaxLength(500);
        entity.HasIndex(e => e.SaleId);
        entity.HasIndex(e => e.ProductId);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SaleItems_Quantity_Positive", "\"RequestedQuantity\" > 0");
            table.HasCheckConstraint("CK_SaleItems_Discount_Range", "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100 AND \"DiscountAmount\" >= 0");
            table.HasCheckConstraint("CK_SaleItems_Money_NonNegative", "\"GrossAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"NetAmount\" >= 0");
            table.HasCheckConstraint("CK_SaleItems_PriceSource", "\"PriceSource\" IN (1, 2, 3, 4, 5)");
        });
        entity.HasOne(e => e.Sale).WithMany(s => s.Items).HasForeignKey(e => e.SaleId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSaleItemBatchAllocation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SaleItemBatchAllocation>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ExpiryDateSnapshot).HasColumnType("date").IsRequired();
        entity.Property(e => e.UnitRetailPriceSnapshot).HasPrecision(18, 2);
        entity.Property(e => e.UnitSalePriceSnapshot).HasPrecision(18, 2);
        entity.Property(e => e.UnitCostPriceSnapshot).HasPrecision(18, 2);
        entity.Property(e => e.GrossAmount).HasPrecision(18, 2);
        entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
        entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
        entity.Property(e => e.NetAmount).HasPrecision(18, 2);
        entity.HasIndex(e => e.SaleItemId);
        entity.HasIndex(e => e.ProductBatchId);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SaleItemBatchAllocations_Quantity_Positive", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_SaleItemBatchAllocations_Money_NonNegative", "\"UnitRetailPriceSnapshot\" >= 0 AND \"UnitSalePriceSnapshot\" >= 0 AND \"UnitCostPriceSnapshot\" >= 0 AND \"GrossAmount\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"NetAmount\" >= 0");
        });
        entity.HasOne(e => e.SaleItem).WithMany(i => i.Allocations).HasForeignKey(e => e.SaleItemId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.ProductBatch).WithMany().HasForeignKey(e => e.ProductBatchId).OnDelete(DeleteBehavior.Restrict);
    }


    private void ConfigureSalesReturn(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SalesReturn>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ReturnNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.GrossReturnAmount).HasPrecision(18, 2);
        entity.Property(e => e.DiscountReturnAmount).HasPrecision(18, 2);
        entity.Property(e => e.TaxReturnAmount).HasPrecision(18, 2);
        entity.Property(e => e.RefundAmount).HasPrecision(18, 2);
        entity.Property(e => e.CustomerCreditReductionAmount).HasPrecision(18, 2);
        entity.Property(e => e.CashRefundAmount).HasPrecision(18, 2);
        entity.HasIndex(e => e.ReturnNumber).IsUnique();
        entity.HasIndex(e => e.OriginalSaleId);
        entity.HasIndex(e => new { e.BranchId, e.PostedAtUtc });
        entity.HasIndex(e => new { e.ProcessedByUserId, e.PostedAtUtc });
        entity.HasIndex(e => new { e.Status, e.ReturnDateUtc });
        entity.HasIndex(e => e.Reason);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SalesReturns_Status", "\"Status\" IN (1)");
            table.HasCheckConstraint("CK_SalesReturns_Reason", "\"Reason\" IN (1, 2, 3, 4, 5)");
            table.HasCheckConstraint("CK_SalesReturns_Posted", "\"Status\" = 1 AND \"PostedAtUtc\" IS NOT NULL");
            table.HasCheckConstraint("CK_SalesReturns_Money_NonNegative", "\"GrossReturnAmount\" >= 0 AND \"DiscountReturnAmount\" >= 0 AND \"TaxReturnAmount\" >= 0 AND \"RefundAmount\" >= 0 AND \"CustomerCreditReductionAmount\" >= 0 AND \"CashRefundAmount\" >= 0");
            table.HasCheckConstraint("CK_SalesReturns_Settlement", "\"RefundAmount\" = \"CustomerCreditReductionAmount\" + \"CashRefundAmount\"");
        });
        entity.HasOne(e => e.OriginalSale).WithMany().HasForeignKey(e => e.OriginalSaleId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Godown).WithMany().HasForeignKey(e => e.GodownId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ProcessedByUser).WithMany().HasForeignKey(e => e.ProcessedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSalesReturnItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SalesReturnItem>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.GrossReturnAmount).HasPrecision(18, 2);
        entity.Property(e => e.DiscountReturnAmount).HasPrecision(18, 2);
        entity.Property(e => e.TaxReturnAmount).HasPrecision(18, 2);
        entity.Property(e => e.RefundAmount).HasPrecision(18, 2);
        entity.HasIndex(e => e.SalesReturnId);
        entity.HasIndex(e => e.OriginalSaleItemId);
        entity.HasIndex(e => e.ProductId);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SalesReturnItems_Quantity_Positive", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_SalesReturnItems_Money_NonNegative", "\"GrossReturnAmount\" >= 0 AND \"DiscountReturnAmount\" >= 0 AND \"TaxReturnAmount\" >= 0 AND \"RefundAmount\" >= 0");
        });
        entity.HasOne(e => e.SalesReturn).WithMany(r => r.Items).HasForeignKey(e => e.SalesReturnId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.OriginalSaleItem).WithMany().HasForeignKey(e => e.OriginalSaleItemId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Product).WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSalesReturnAllocation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SalesReturnAllocation>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ExpiryDateSnapshot).HasColumnType("date").IsRequired();
        entity.Property(e => e.UnitRetailPriceSnapshot).HasPrecision(18, 2);
        entity.Property(e => e.UnitSalePriceSnapshot).HasPrecision(18, 2);
        entity.Property(e => e.UnitCostPriceSnapshot).HasPrecision(18, 2);
        entity.Property(e => e.GrossReturnAmount).HasPrecision(18, 2);
        entity.Property(e => e.DiscountReturnAmount).HasPrecision(18, 2);
        entity.Property(e => e.TaxReturnAmount).HasPrecision(18, 2);
        entity.Property(e => e.RefundAmount).HasPrecision(18, 2);
        entity.HasIndex(e => e.SalesReturnItemId);
        entity.HasIndex(e => e.OriginalSaleItemBatchAllocationId);
        entity.HasIndex(e => e.ProductBatchId);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SalesReturnAllocations_Quantity_Positive", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_SalesReturnAllocations_Disposition", "\"Disposition\" IN (1, 2)");
            table.HasCheckConstraint("CK_SalesReturnAllocations_Money_NonNegative", "\"UnitRetailPriceSnapshot\" >= 0 AND \"UnitSalePriceSnapshot\" >= 0 AND \"UnitCostPriceSnapshot\" >= 0 AND \"GrossReturnAmount\" >= 0 AND \"DiscountReturnAmount\" >= 0 AND \"TaxReturnAmount\" >= 0 AND \"RefundAmount\" >= 0");
        });
        entity.HasOne(e => e.SalesReturnItem).WithMany(i => i.Allocations).HasForeignKey(e => e.SalesReturnItemId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.OriginalSaleItemBatchAllocation).WithMany().HasForeignKey(e => e.OriginalSaleItemBatchAllocationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ProductBatch).WithMany().HasForeignKey(e => e.ProductBatchId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSalesRefundPayment(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SalesRefundPayment>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
        entity.HasIndex(e => e.SalesReturnId);
        entity.HasIndex(e => new { e.Method, e.CreatedAt });
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SalesRefundPayments_Method", "\"Method\" IN (1, 2, 3, 4, 5, 6)");
            table.HasCheckConstraint("CK_SalesRefundPayments_Amount_Positive", "\"Amount\" > 0");
        });
        entity.HasOne(e => e.SalesReturn).WithMany(r => r.RefundPayments).HasForeignKey(e => e.SalesReturnId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.FinancialAccount).WithMany().HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
    }
    private void ConfigureSalePayment(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SalePayment>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.AmountApplied).HasPrecision(18, 2);
        entity.Property(e => e.TenderedAmount).HasPrecision(18, 2);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
        entity.HasIndex(e => e.SaleId);
        entity.HasIndex(e => new { e.Method, e.CreatedAt });
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SalePayments_Method", "\"Method\" IN (1, 2, 3, 4, 5, 6)");
            table.HasCheckConstraint("CK_SalePayments_Amount_Positive", "\"AmountApplied\" > 0");
            table.HasCheckConstraint("CK_SalePayments_CashTender", "(\"Method\" = 1 AND \"TenderedAmount\" IS NOT NULL AND \"TenderedAmount\" >= \"AmountApplied\") OR (\"Method\" <> 1 AND \"TenderedAmount\" IS NULL)");
        });
        entity.HasOne(e => e.Sale).WithMany(s => s.Payments).HasForeignKey(e => e.SaleId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.FinancialAccount).WithMany().HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureFinancialAccount(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FinancialAccount>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.NormalizedName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.OpeningBalance).HasPrecision(18, 2);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => new { e.BranchId, e.NormalizedName }).IsUnique();
        entity.HasIndex(e => new { e.BranchId, e.IsActive });
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table => table.HasCheckConstraint("CK_FinancialAccounts_Type", "\"AccountType\" IN (1, 2, 3, 4, 5)"));
    }

    private void ConfigureFinancialLedgerEntry(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FinancialLedgerEntry>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.ReferenceType).IsRequired().HasMaxLength(100);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
        entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
        entity.HasIndex(e => new { e.FinancialAccountId, e.OccurredAtUtc });
        entity.HasIndex(e => new { e.BranchId, e.OccurredAtUtc });
        entity.HasIndex(e => new { e.EntryType, e.OccurredAtUtc });
        entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId });
        entity.HasIndex(e => new { e.FinancialAccountId, e.EntryType, e.ReferenceType, e.ReferenceId }).IsUnique();
        entity.HasIndex(e => e.BankReconciliationId);
        entity.HasOne(e => e.FinancialAccount).WithMany(a => a.LedgerEntries).HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.BankReconciliation).WithMany(r => r.MatchedEntries).HasForeignKey(e => e.BankReconciliationId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_FinancialLedgerEntries_Amount_NonZero", "\"Amount\" <> 0");
            table.HasCheckConstraint("CK_FinancialLedgerEntries_Type", "\"EntryType\" IN (1,2,3,4,5,6,7,8,9,10,11)");
            table.HasCheckConstraint("CK_FinancialLedgerEntries_Sign", "(\"EntryType\" IN (2,3,7,9,11) AND \"Amount\" > 0) OR (\"EntryType\" IN (4,5,6,8,10) AND \"Amount\" < 0) OR \"EntryType\" = 1");
        });
    }

    private void ConfigureExpenseCategory(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ExpenseCategory>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.NormalizedName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.HasIndex(e => e.NormalizedName).IsUnique();
        entity.HasIndex(e => e.IsActive);
        entity.HasIndex(e => e.DeletedAtUtc);
        entity.HasQueryFilter(e => !e.IsDeleted);
    }

    private void ConfigureExpense(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Expense>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ExpenseNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
        entity.Property(e => e.Payee).HasMaxLength(200);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.ReversalReason).HasMaxLength(500);
        entity.HasIndex(e => e.ExpenseNumber).IsUnique();
        entity.HasIndex(e => new { e.BranchId, e.ExpenseDateUtc });
        entity.HasIndex(e => new { e.ExpenseCategoryId, e.ExpenseDateUtc });
        entity.HasIndex(e => new { e.FinancialAccountId, e.ExpenseDateUtc });
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ExpenseCategory).WithMany().HasForeignKey(e => e.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.FinancialAccount).WithMany().HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ReversedByUser).WithMany().HasForeignKey(e => e.ReversedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table => table.HasCheckConstraint("CK_Expenses_Amount_Positive", "\"Amount\" > 0"));
    }

    private void ConfigureOtherIncome(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OtherIncome>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.IncomeNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.ReversalReason).HasMaxLength(500);
        entity.HasIndex(e => e.IncomeNumber).IsUnique();
        entity.HasIndex(e => new { e.BranchId, e.OccurredAtUtc });
        entity.HasIndex(e => new { e.FinancialAccountId, e.OccurredAtUtc });
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.FinancialAccount).WithMany().HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ReversedByUser).WithMany().HasForeignKey(e => e.ReversedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table => table.HasCheckConstraint("CK_OtherIncomes_Amount_Positive", "\"Amount\" > 0"));
    }

    private void ConfigureFinancialTransfer(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FinancialTransfer>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.TransferNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.TransferNumber).IsUnique();
        entity.HasIndex(e => new { e.BranchId, e.OccurredAtUtc });
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.SourceAccount).WithMany().HasForeignKey(e => e.SourceAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.DestinationAccount).WithMany().HasForeignKey(e => e.DestinationAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_FinancialTransfers_Amount_Positive", "\"Amount\" > 0");
            table.HasCheckConstraint("CK_FinancialTransfers_DifferentAccounts", "\"SourceAccountId\" <> \"DestinationAccountId\"");
        });
    }

    private static void ConfigureSystemSetting(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SystemSetting>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Value).IsRequired().HasMaxLength(2000);
        entity.HasIndex(e => e.Key).IsUnique();
        entity.HasIndex(e => e.UpdatedAt);
    }

    private static void ConfigureBackupRecord(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BackupRecord>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.FileName).IsRequired().HasMaxLength(260);
        entity.Property(e => e.Status).IsRequired().HasMaxLength(30);
        entity.Property(e => e.ErrorMessage).HasMaxLength(500);
        entity.HasIndex(e => e.CreatedAt);
        entity.HasIndex(e => new { e.Status, e.CreatedAt });
    }

    private void ProtectAuditLog()
    {
        foreach (var entry in ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Audit events are append-only and cannot be updated or deleted.");
            if (entry.State == EntityState.Added)
            {
                entry.Entity.OldValues = ScrubAuditJson(entry.Entity.OldValues);
                entry.Entity.NewValues = ScrubAuditJson(entry.Entity.NewValues);
            }
        }
    }

    private static string? ScrubAuditJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        try
        {
            var node = JsonNode.Parse(json);
            Scrub(node);
            return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Audit values must contain valid JSON.");
        }
    }

    private static void Scrub(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(x => x.Key).ToArray())
            {
                if (key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("connectionstring", StringComparison.OrdinalIgnoreCase))
                    obj[key] = "[REDACTED]";
                else Scrub(obj[key]);
            }
        }
        else if (node is JsonArray array)
            foreach (var child in array) Scrub(child);
    }

    private void ConfigureAccountingPeriod(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AccountingPeriod>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => new { e.FiscalYear, e.PeriodNumber }).IsUnique();
        entity.HasIndex(e => new { e.StartDate, e.EndDate });
        entity.HasIndex(e => e.Status);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_AccountingPeriods_Status", "\"Status\" BETWEEN 1 AND 3");
            table.HasCheckConstraint("CK_AccountingPeriods_DateOrder", "\"EndDate\" >= \"StartDate\"");
        });
        entity.HasOne(e => e.ClosedByUser).WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ReopenedByUser).WithMany().HasForeignKey(e => e.ReopenedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureFiscalYearClose(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FiscalYearClose>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.TotalRevenue).HasPrecision(18, 2);
        entity.Property(e => e.TotalCostOfGoodsSold).HasPrecision(18, 2);
        entity.Property(e => e.TotalOperatingExpenses).HasPrecision(18, 2);
        entity.Property(e => e.NetProfit).HasPrecision(18, 2);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.ReopenReason).HasMaxLength(500);
        entity.HasIndex(e => e.FiscalYear).IsUnique();
        entity.ToTable(table => table.HasCheckConstraint("CK_FiscalYearCloses_Status", "\"Status\" BETWEEN 1 AND 2"));
        entity.HasOne(e => e.ClosedByUser).WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ReopenedByUser).WithMany().HasForeignKey(e => e.ReopenedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureRecurringJournalTemplate(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RecurringJournalTemplate>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.HasIndex(e => new { e.BranchId, e.IsActive });
        entity.HasIndex(e => e.NextRunDate);
        entity.ToTable(table => table.HasCheckConstraint("CK_RecurringJournalTemplates_Frequency", "\"Frequency\" BETWEEN 1 AND 4"));
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureRecurringJournalTemplateLine(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RecurringJournalTemplateLine>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Debit).HasPrecision(18, 2);
        entity.Property(e => e.Credit).HasPrecision(18, 2);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.HasIndex(e => e.RecurringJournalTemplateId);
        entity.ToTable(table => table.HasCheckConstraint("CK_RecurringJournalTemplateLines_Amounts",
            "\"Debit\" >= 0 AND \"Credit\" >= 0 AND NOT (\"Debit\" > 0 AND \"Credit\" > 0) AND (\"Debit\" > 0 OR \"Credit\" > 0)"));
        entity.HasOne(e => e.RecurringJournalTemplate).WithMany(t => t.Lines).HasForeignKey(e => e.RecurringJournalTemplateId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(e => e.ChartOfAccount).WithMany().HasForeignKey(e => e.ChartOfAccountId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureRecurringJournalOccurrence(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RecurringJournalOccurrence>();
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.RecurringJournalTemplateId, e.ScheduledDate }).IsUnique();
        entity.HasIndex(e => e.JournalEntryId).IsUnique();
        entity.HasOne(e => e.RecurringJournalTemplate).WithMany(t => t.Occurrences).HasForeignKey(e => e.RecurringJournalTemplateId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.JournalEntry).WithMany().HasForeignKey(e => e.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.GeneratedByUser).WithMany().HasForeignKey(e => e.GeneratedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureBankReconciliation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BankReconciliation>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.StatementOpeningBalance).HasPrecision(18, 2);
        entity.Property(e => e.StatementClosingBalance).HasPrecision(18, 2);
        entity.Property(e => e.BookBalanceAtFinalization).HasPrecision(18, 2);
        entity.Property(e => e.DifferenceAtFinalization).HasPrecision(18, 2);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.ReopenReason).HasMaxLength(500);
        entity.HasIndex(e => new { e.FinancialAccountId, e.Status });
        entity.HasIndex(e => new { e.FinancialAccountId, e.StatementEndDate });
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_BankReconciliations_Status", "\"Status\" BETWEEN 1 AND 2");
            table.HasCheckConstraint("CK_BankReconciliations_DateOrder", "\"StatementEndDate\" >= \"StatementStartDate\"");
        });
        entity.HasOne(e => e.FinancialAccount).WithMany().HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.FinalizedByUser).WithMany().HasForeignKey(e => e.FinalizedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ReopenedByUser).WithMany().HasForeignKey(e => e.ReopenedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureAccountBudget(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AccountBudget>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.BudgetAmount).HasPrecision(18, 2);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => new { e.FiscalYear, e.PeriodNumber, e.ChartOfAccountId, e.BranchId }).IsUnique().AreNullsDistinct(false);
        entity.ToTable(table => table.HasCheckConstraint("CK_AccountBudgets_PeriodNumber_Range", "\"PeriodNumber\" IS NULL OR \"PeriodNumber\" BETWEEN 1 AND 12"));
        entity.HasOne(e => e.ChartOfAccount).WithMany().HasForeignKey(e => e.ChartOfAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureCostCenter(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CostCenter>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
        entity.Property(e => e.NormalizedCode).IsRequired().HasMaxLength(20);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.HasIndex(e => e.NormalizedCode).IsUnique();
        entity.HasIndex(e => e.IsActive);
    }

    private void ConfigureCreditNote(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CreditNote>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CreditNoteNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.CreditNoteNumber).IsUnique();
        entity.HasIndex(e => new { e.CustomerId, e.IssueDateUtc });
        entity.ToTable(table => table.HasCheckConstraint("CK_CreditNotes_Amount_Positive", "\"Amount\" > 0"));
        entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.AppliedToSale).WithMany().HasForeignKey(e => e.AppliedToSaleId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureDebitNote(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DebitNote>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DebitNoteNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.DebitNoteNumber).IsUnique();
        entity.HasIndex(e => new { e.SupplierId, e.IssueDateUtc });
        entity.ToTable(table => table.HasCheckConstraint("CK_DebitNotes_Amount_Positive", "\"Amount\" > 0"));
        entity.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.AppliedToGoodsReceipt).WithMany().HasForeignKey(e => e.AppliedToGoodsReceiptId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureCustomerWriteOff(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CustomerWriteOff>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.WriteOffNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.WriteOffNumber).IsUnique();
        entity.HasIndex(e => new { e.CustomerId, e.WriteOffDateUtc });
        entity.HasIndex(e => e.CustomerPaymentId).IsUnique();
        entity.ToTable(table => table.HasCheckConstraint("CK_CustomerWriteOffs_Amount_Positive", "\"Amount\" > 0"));
        entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.AppliedToSale).WithMany().HasForeignKey(e => e.AppliedToSaleId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CustomerPayment).WithMany().HasForeignKey(e => e.CustomerPaymentId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSupplierWriteOff(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SupplierWriteOff>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.WriteOffNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.WriteOffNumber).IsUnique();
        entity.HasIndex(e => new { e.SupplierId, e.WriteOffDateUtc });
        entity.HasIndex(e => e.SupplierLedgerEntryId).IsUnique();
        entity.ToTable(table => table.HasCheckConstraint("CK_SupplierWriteOffs_Amount_Positive", "\"Amount\" > 0"));
        entity.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.AppliedToGoodsReceipt).WithMany().HasForeignKey(e => e.AppliedToGoodsReceiptId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.SupplierLedgerEntry).WithMany().HasForeignKey(e => e.SupplierLedgerEntryId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureCustomerAdvance(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CustomerAdvance>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.AdvanceNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.AmountApplied).HasPrecision(18, 2);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.AdvanceNumber).IsUnique();
        entity.HasIndex(e => new { e.CustomerId, e.ReceivedDateUtc });
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_CustomerAdvances_Amount_Positive", "\"Amount\" > 0");
            table.HasCheckConstraint("CK_CustomerAdvances_AmountApplied_Range", "\"AmountApplied\" >= 0 AND \"AmountApplied\" <= \"Amount\"");
        });
        entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.FinancialAccount).WithMany().HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureCustomerAdvanceApplication(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CustomerAdvanceApplication>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.AppliedAmount).HasPrecision(18, 2);
        entity.HasIndex(e => e.CustomerAdvanceId);
        entity.HasIndex(e => e.SaleId);
        entity.HasIndex(e => e.CustomerPaymentId).IsUnique();
        entity.ToTable(table => table.HasCheckConstraint("CK_CustomerAdvanceApplications_Amount_Positive", "\"AppliedAmount\" > 0"));
        entity.HasOne(e => e.CustomerAdvance).WithMany(a => a.Applications).HasForeignKey(e => e.CustomerAdvanceId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Sale).WithMany().HasForeignKey(e => e.SaleId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CustomerPayment).WithMany().HasForeignKey(e => e.CustomerPaymentId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSupplierAdvance(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SupplierAdvance>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.AdvanceNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Amount).HasPrecision(18, 2);
        entity.Property(e => e.AmountApplied).HasPrecision(18, 2);
        entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.AdvanceNumber).IsUnique();
        entity.HasIndex(e => new { e.SupplierId, e.PaidDateUtc });
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SupplierAdvances_Amount_Positive", "\"Amount\" > 0");
            table.HasCheckConstraint("CK_SupplierAdvances_AmountApplied_Range", "\"AmountApplied\" >= 0 AND \"AmountApplied\" <= \"Amount\"");
        });
        entity.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.FinancialAccount).WithMany().HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureSupplierAdvanceApplication(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SupplierAdvanceApplication>();
        entity.HasKey(e => e.Id);
        entity.Property(e => e.AppliedAmount).HasPrecision(18, 2);
        entity.HasIndex(e => e.SupplierAdvanceId);
        entity.HasIndex(e => e.GoodsReceiptId);
        entity.HasIndex(e => e.SupplierLedgerEntryId).IsUnique();
        entity.ToTable(table => table.HasCheckConstraint("CK_SupplierAdvanceApplications_Amount_Positive", "\"AppliedAmount\" > 0"));
        entity.HasOne(e => e.SupplierAdvance).WithMany(a => a.Applications).HasForeignKey(e => e.SupplierAdvanceId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.GoodsReceipt).WithMany().HasForeignKey(e => e.GoodsReceiptId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.SupplierLedgerEntry).WithMany().HasForeignKey(e => e.SupplierLedgerEntryId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
