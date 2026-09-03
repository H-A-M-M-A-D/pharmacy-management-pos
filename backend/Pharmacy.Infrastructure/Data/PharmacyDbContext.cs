using Microsoft.EntityFrameworkCore;
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
    public DbSet<Sale> Sales { get; set; } = null!;
    public DbSet<SaleItem> SaleItems { get; set; } = null!;
    public DbSet<SaleItemBatchAllocation> SaleItemBatchAllocations { get; set; } = null!;
    public DbSet<SalePayment> SalePayments { get; set; } = null!;
    public DbSet<SalesReturn> SalesReturns { get; set; } = null!;
    public DbSet<SalesReturnItem> SalesReturnItems { get; set; } = null!;
    public DbSet<SalesReturnAllocation> SalesReturnAllocations { get; set; } = null!;
    public DbSet<SalesRefundPayment> SalesRefundPayments { get; set; } = null!;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateStockMovements();
        ValidateSupplierLedgerEntries();
        ValidatePurchasingDocuments();
        ValidateSalesDocuments();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateStockMovements();
        ValidateSupplierLedgerEntries();
        ValidatePurchasingDocuments();
        ValidateSalesDocuments();
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

        // Apply entity configurations
        ConfigureBranch(modelBuilder);
        ConfigureRole(modelBuilder);
        ConfigurePermission(modelBuilder);
        ConfigureRolePermission(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigureProductCategory(modelBuilder);
        ConfigureManufacturer(modelBuilder);
        ConfigureSupplier(modelBuilder);
        ConfigureProduct(modelBuilder);
        ConfigureProductBatch(modelBuilder);
        ConfigureInventory(modelBuilder);
        ConfigureStockMovement(modelBuilder);
        ConfigureAuditLog(modelBuilder);
        ConfigureSupplierLedgerEntry(modelBuilder);
        ConfigurePurchaseOrder(modelBuilder);
        ConfigurePurchaseOrderItem(modelBuilder);
        ConfigureGoodsReceipt(modelBuilder);
        ConfigureGoodsReceiptItem(modelBuilder);
        ConfigureSale(modelBuilder);
        ConfigureSaleItem(modelBuilder);
        ConfigureSaleItemBatchAllocation(modelBuilder);
        ConfigureSalePayment(modelBuilder);
        ConfigureSalesReturn(modelBuilder);
        ConfigureSalesReturnItem(modelBuilder);
        ConfigureSalesReturnAllocation(modelBuilder);
        ConfigureSalesRefundPayment(modelBuilder);
    }

    private void ConfigureBranch(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Branch>();

        entity.HasKey(e => e.Id);
        entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Address).HasMaxLength(500);
        entity.Property(e => e.City).HasMaxLength(100);
        entity.Property(e => e.PhoneNumber).HasMaxLength(20);
        entity.Property(e => e.Email).HasMaxLength(100);

        entity.HasIndex(e => e.Code).IsUnique();
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
        entity.Property(e => e.ChangeGiven).HasPrecision(18, 2);
        entity.HasIndex(e => e.InvoiceNumber).IsUnique().HasFilter("\"InvoiceNumber\" IS NOT NULL");
        entity.HasIndex(e => e.HoldNumber).IsUnique().HasFilter("\"HoldNumber\" IS NOT NULL");
        entity.HasIndex(e => new { e.BranchId, e.PostedAtUtc });
        entity.HasIndex(e => new { e.CashierUserId, e.PostedAtUtc });
        entity.HasIndex(e => new { e.Status, e.CreatedAt });
        entity.HasIndex(e => e.CustomerPhone);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Sales_Status", "\"Status\" IN (1, 2, 3)");
            table.HasCheckConstraint("CK_Sales_Posted_HasInvoice", "(\"Status\" <> 2) OR (\"InvoiceNumber\" IS NOT NULL AND \"PostedAtUtc\" IS NOT NULL)");
            table.HasCheckConstraint("CK_Sales_Money_NonNegative", "\"Subtotal\" >= 0 AND \"DiscountTotal\" >= 0 AND \"TaxTotal\" >= 0 AND \"NetTotal\" >= 0 AND \"AmountPaid\" >= 0 AND \"ChangeGiven\" >= 0");
            table.HasCheckConstraint("CK_Sales_Posted_Paid", "(\"Status\" <> 2) OR (\"AmountPaid\" = \"NetTotal\")");
        });
        entity.HasOne(e => e.Branch).WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.CashierUser).WithMany().HasForeignKey(e => e.CashierUserId).OnDelete(DeleteBehavior.Restrict);
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
            table.HasCheckConstraint("CK_SalesReturns_Money_NonNegative", "\"GrossReturnAmount\" >= 0 AND \"DiscountReturnAmount\" >= 0 AND \"TaxReturnAmount\" >= 0 AND \"RefundAmount\" >= 0");
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
    }
}
