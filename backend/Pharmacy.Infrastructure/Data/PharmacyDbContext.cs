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

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateStockMovements();
        ValidateSupplierLedgerEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateStockMovements();
        ValidateSupplierLedgerEntries();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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
            if (movementDelta == 0 || batchDelta != movementDelta || inventoryDelta != movementDelta)
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
}
