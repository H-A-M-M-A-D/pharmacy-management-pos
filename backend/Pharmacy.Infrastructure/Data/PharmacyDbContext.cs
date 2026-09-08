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

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AddFinancialEntriesForPayments();
        ValidateStockMovements();
        ValidateSupplierLedgerEntries();
        ValidateCustomerFinancialEntries();
        ValidatePurchasingDocuments();
        ValidateSalesDocuments();
        ValidateFinanceDocuments();
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
        ProtectAuditLog();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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
        foreach (var tracked in ChangeTracker.Entries<SalePayment>().Where(x => x.State == EntityState.Added && x.Entity.FinancialAccountId.HasValue))
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
        foreach (var tracked in ChangeTracker.Entries<SupplierLedgerEntry>().Where(x => x.State == EntityState.Added && x.Entity.EntryType == SupplierLedgerEntryType.Payment && x.Entity.FinancialAccountId.HasValue))
        {
            var payment = tracked.Entity;
            FinancialLedgerEntries.Add(new FinancialLedgerEntry { FinancialAccountId = payment.FinancialAccountId!.Value, BranchId = payment.BranchId,
                EntryType = FinancialLedgerEntryType.SupplierPayment, Amount = payment.Amount, ReferenceType = "SupplierPayment", ReferenceId = payment.Id,
                ReferenceNumber = payment.ReferenceNumber, Description = "Supplier payment", CreatedByUserId = payment.CreatedByUserId ?? throw new InvalidOperationException("Supplier payment requires a user."), OccurredAtUtc = payment.CreatedAt });
        }
        foreach (var tracked in ChangeTracker.Entries<SalesRefundPayment>().Where(x => x.State == EntityState.Added && x.Entity.FinancialAccountId.HasValue))
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
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Financial ledger history is permanent and cannot be updated or deleted.");
            if (entry.State == EntityState.Added && entry.Entity.Amount == 0)
                throw new InvalidOperationException("Financial ledger amount cannot be zero.");
        }
        foreach (var entry in ChangeTracker.Entries<Expense>())
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Posted expenses are permanent and cannot be updated or deleted.");
        foreach (var entry in ChangeTracker.Entries<OtherIncome>())
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("Posted other income is permanent and cannot be updated or deleted.");
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

        // Apply entity configurations
        ConfigureBranch(modelBuilder);
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

        entity.HasIndex(e => new { e.BranchId, e.ProductId, e.BatchNumber }).IsUnique();
        entity.HasIndex(e => e.ExpiryDate);
        entity.HasIndex(e => new { e.BranchId, e.ExpiryDate });
        entity.HasIndex(e => new { e.BranchId, e.ProductId });
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
    }

    private void ConfigureInventory(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Inventory>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.QuantityInStock);
        entity.Property(e => e.ReorderLevel);

        entity.HasIndex(e => new { e.BranchId, e.ProductId, e.ProductBatchId }).IsUnique();
        entity.HasIndex(e => new { e.BranchId, e.ProductId });
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
        entity.HasIndex(e => e.Email);
        entity.HasIndex(e => e.City);
        entity.HasIndex(e => e.IsActive);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Customers_CreditLimit_NonNegative", "\"CreditLimit\" >= 0");
        });
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
            table.HasCheckConstraint("CK_CustomerPayments_Method", "\"PaymentMethod\" IN (1, 2, 3, 4, 5, 6, 7)");
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
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_GoodsReceipts_Status", "\"Status\" IN (1, 2, 3)");
            table.HasCheckConstraint("CK_GoodsReceipts_Totals_NonNegative", "\"Subtotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"TaxTotal\" >= 0 AND \"NetTotal\" >= 0");
        });

        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
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
        entity.HasIndex(e => e.InvoiceNumber).IsUnique().HasFilter("\"InvoiceNumber\" IS NOT NULL");
        entity.HasIndex(e => e.HoldNumber).IsUnique().HasFilter("\"HoldNumber\" IS NOT NULL");
        entity.HasIndex(e => new { e.BranchId, e.PostedAtUtc });
        entity.HasIndex(e => new { e.CashierUserId, e.PostedAtUtc });
        entity.HasIndex(e => new { e.CustomerId, e.PostedAtUtc });
        entity.HasIndex(e => new { e.Status, e.CreatedAt });
        entity.HasIndex(e => e.CustomerPhone);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Sales_Status", "\"Status\" IN (1, 2, 3)");
            table.HasCheckConstraint("CK_Sales_Posted_HasInvoice", "(\"Status\" <> 2) OR (\"InvoiceNumber\" IS NOT NULL AND \"PostedAtUtc\" IS NOT NULL)");
            table.HasCheckConstraint("CK_Sales_Money_NonNegative", "\"Subtotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"TaxTotal\" >= 0 AND \"NetTotal\" >= 0 AND \"AmountPaid\" >= 0 AND \"CreditAmount\" >= 0 AND \"ChangeGiven\" >= 0");
            table.HasCheckConstraint("CK_Sales_Posted_Settled", "(\"Status\" <> 2) OR (\"AmountPaid\" + \"CreditAmount\" = \"NetTotal\")");
            table.HasCheckConstraint("CK_Sales_CreditRequiresCustomer", "\"CreditAmount\" = 0 OR \"CustomerId\" IS NOT NULL");
        });
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CashierUser).WithMany().HasForeignKey(e => e.CashierUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Customer).WithMany(c => c.Sales).HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
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
        entity.HasIndex(e => e.SaleId);
        entity.HasIndex(e => e.ProductId);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SaleItems_Quantity_Positive", "\"RequestedQuantity\" > 0");
            table.HasCheckConstraint("CK_SaleItems_Discount_Range", "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100 AND \"DiscountAmount\" >= 0");
            table.HasCheckConstraint("CK_SaleItems_Money_NonNegative", "\"GrossAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"NetAmount\" >= 0");
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
        entity.HasOne(e => e.FinancialAccount).WithMany(a => a.LedgerEntries).HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
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
        entity.HasIndex(e => e.ExpenseNumber).IsUnique();
        entity.HasIndex(e => new { e.BranchId, e.ExpenseDateUtc });
        entity.HasIndex(e => new { e.ExpenseCategoryId, e.ExpenseDateUtc });
        entity.HasIndex(e => new { e.FinancialAccountId, e.ExpenseDateUtc });
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ExpenseCategory).WithMany().HasForeignKey(e => e.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.FinancialAccount).WithMany().HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
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
        entity.HasIndex(e => e.IncomeNumber).IsUnique();
        entity.HasIndex(e => new { e.BranchId, e.OccurredAtUtc });
        entity.HasIndex(e => new { e.FinancialAccountId, e.OccurredAtUtc });
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.FinancialAccount).WithMany().HasForeignKey(e => e.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
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
}
