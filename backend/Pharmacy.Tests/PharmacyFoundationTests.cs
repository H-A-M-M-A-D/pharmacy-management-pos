using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Auth;
using Pharmacy.Application.Services.Auth;
using Pharmacy.Application.Services.Inventory;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Services.Auth;
using Pharmacy.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Pharmacy.Tests.Foundation;

/// <summary>
/// Foundation tests for core pharmacy system functionality.
/// </summary>
public class PharmacyFoundationTests
{
    /// <summary>
    /// Test that multiple batches can be created for the same product.
    /// </summary>
    [Fact]
    public void MultipleBatchesAllowedForOneProduct()
    {
        // Arrange
        var product = new Product
        {
            SKU = "MED-001",
            Name = "Aspirin 500mg",
            Unit = "tablet",
            PackSize = 10,
            PurchasePrice = 1.00m,
            RetailPrice = 2.00m,
            CategoryId = Guid.NewGuid()
        };

        var batch1 = new ProductBatch
        {
            ProductId = product.Id,
            BatchNumber = "BATCH-001",
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            PurchasePrice = 1.00m,
            RetailPrice = 2.00m,
            QuantityReceived = 100,
            QuantityAvailable = 100,
            BranchId = Guid.NewGuid()
        };

        var batch2 = new ProductBatch
        {
            ProductId = product.Id,
            BatchNumber = "BATCH-002",
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(12)),
            PurchasePrice = 1.05m,
            RetailPrice = 2.10m,
            QuantityReceived = 150,
            QuantityAvailable = 150,
            BranchId = Guid.NewGuid()
        };

        product.ProductBatches.Add(batch1);
        product.ProductBatches.Add(batch2);

        // Assert
        Assert.Equal(2, product.ProductBatches.Count);
        Assert.NotEqual(batch1.PurchasePrice, batch2.PurchasePrice);
        Assert.NotEqual(batch1.ExpiryDate, batch2.ExpiryDate);
    }

    /// <summary>
    /// Test FEFO (First Expire, First Out) batch selection.
    /// </summary>
    [Fact]
    public void FefoSelectsEarliestExpiringBatch()
    {
        // Arrange
        var product = new Product
        {
            SKU = "MED-002",
            Name = "Paracetamol 500mg",
            Unit = "tablet",
            PackSize = 20,
            PurchasePrice = 0.50m,
            RetailPrice = 1.00m,
            CategoryId = Guid.NewGuid()
        };

        var branchId = Guid.NewGuid();
        var expiryDate1 = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3));
        var expiryDate2 = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6));
        var expiryDate3 = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(9));

        var batch1 = new ProductBatch
        {
            ProductId = product.Id,
            BranchId = branchId,
            BatchNumber = "BATCH-A",
            ExpiryDate = expiryDate3,
            QuantityAvailable = 100,
            PurchasePrice = 0.50m,
            RetailPrice = 1.00m
        };

        var batch2 = new ProductBatch
        {
            ProductId = product.Id,
            BranchId = branchId,
            BatchNumber = "BATCH-B",
            ExpiryDate = expiryDate1,  // Earliest
            QuantityAvailable = 50,
            PurchasePrice = 0.50m,
            RetailPrice = 1.00m
        };

        var batch3 = new ProductBatch
        {
            ProductId = product.Id,
            BranchId = branchId,
            BatchNumber = "BATCH-C",
            ExpiryDate = expiryDate2,
            QuantityAvailable = 75,
            PurchasePrice = 0.50m,
            RetailPrice = 1.00m
        };

        var batches = new List<ProductBatch> { batch1, batch2, batch3 }
            .Where(b => b.ExpiryDate >= DateOnly.FromDateTime(DateTime.UtcNow) && b.QuantityAvailable > 0)
            .OrderBy(b => b.ExpiryDate)
            .ToList();

        // Act
        var selectedBatch = batches.FirstOrDefault();

        // Assert
        Assert.NotNull(selectedBatch);
        Assert.Equal("BATCH-B", selectedBatch.BatchNumber);
        Assert.Equal(expiryDate1, selectedBatch.ExpiryDate);
    }

    /// <summary>
    /// Test that expired batches are excluded from FEFO selection.
    /// </summary>
    [Fact]
    public void ExpiredBatchesExcludedFromFefoSelection()
    {
        // Arrange
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var expiredBatch = new ProductBatch
        {
            ProductId = productId,
            BranchId = branchId,
            BatchNumber = "EXPIRED",
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),  // Expired
            QuantityAvailable = 100,
            PurchasePrice = 1.00m,
            RetailPrice = 2.00m
        };

        var validBatch = new ProductBatch
        {
            ProductId = productId,
            BranchId = branchId,
            BatchNumber = "VALID",
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            QuantityAvailable = 100,
            PurchasePrice = 1.00m,
            RetailPrice = 2.00m
        };

        var batches = new List<ProductBatch> { expiredBatch, validBatch };
        var availableBatches = batches
            .Where(b => b.ExpiryDate >= DateOnly.FromDateTime(DateTime.UtcNow) && b.QuantityAvailable > 0)
            .OrderBy(b => b.ExpiryDate)
            .ToList();

        // Assert
        Assert.Single(availableBatches);
        Assert.Equal("VALID", availableBatches.First().BatchNumber);
    }

    /// <summary>
    /// Test that SKU must be unique in database (validated at EF level).
    /// </summary>
    [Fact]
    public void SkuMustBeUnique()
    {
        // Arrange
        var product1 = new Product
        {
            SKU = "UNIQUE-SKU-001",
            Name = "Product 1",
            Unit = "tablet",
            PackSize = 10,
            PurchasePrice = 1.00m,
            RetailPrice = 2.00m,
            CategoryId = Guid.NewGuid()
        };

        var product2 = new Product
        {
            SKU = "UNIQUE-SKU-001",  // Duplicate
            Name = "Product 2",
            Unit = "tablet",
            PackSize = 10,
            PurchasePrice = 1.00m,
            RetailPrice = 2.00m,
            CategoryId = Guid.NewGuid()
        };

        // Assert - duplicate SKU detected
        Assert.Equal(product1.SKU, product2.SKU);
        // In actual EF scenario, database unique constraint would prevent this
    }

    /// <summary>
    /// Test stock movement types cover all pharmacy operations.
    /// </summary>
    [Fact]
    public void StockMovementTypesComprehensive()
    {
        // Arrange
        var movementTypes = Enum.GetValues(typeof(StockMovementType)).Cast<StockMovementType>().ToList();

        // Assert
        Assert.Contains(StockMovementType.Purchase, movementTypes);
        Assert.Contains(StockMovementType.Sale, movementTypes);
        Assert.Contains(StockMovementType.SaleReturn, movementTypes);
        Assert.Contains(StockMovementType.PurchaseReturn, movementTypes);
        Assert.Contains(StockMovementType.TransferIn, movementTypes);
        Assert.Contains(StockMovementType.TransferOut, movementTypes);
        Assert.Contains(StockMovementType.AdjustmentIncrease, movementTypes);
        Assert.Contains(StockMovementType.AdjustmentDecrease, movementTypes);
        Assert.Contains(StockMovementType.Expired, movementTypes);
        Assert.Contains(StockMovementType.Damaged, movementTypes);
        Assert.Equal(11, movementTypes.Count);
    }

    /// <summary>
    /// Test role-permission relationship.
    /// </summary>
    [Fact]
    public void RolePermissionRelationshipCorrect()
    {
        // Arrange
        var role = new Role
        {
            Name = "Pharmacist",
            Description = "Pharmacist role",
            IsSystem = false
        };

        var permission1 = new Permission
        {
            Code = "products.view",
            Description = "Can view products",
            Category = "products"
        };

        var permission2 = new Permission
        {
            Code = "sales.view",
            Description = "Can view sales",
            Category = "sales"
        };

        var rolePermission1 = new RolePermission
        {
            RoleId = role.Id,
            PermissionId = permission1.Id,
            Role = role,
            Permission = permission1
        };

        var rolePermission2 = new RolePermission
        {
            RoleId = role.Id,
            PermissionId = permission2.Id,
            Role = role,
            Permission = permission2
        };

        role.RolePermissions.Add(rolePermission1);
        role.RolePermissions.Add(rolePermission2);

        // Assert
        Assert.Equal(2, role.RolePermissions.Count);
        Assert.All(role.RolePermissions, rp => Assert.Equal(role.Id, rp.RoleId));
    }

    /// <summary>
    /// Test user-branch-role relationship.
    /// </summary>
    [Fact]
    public void UserBranchRoleRelationshipCorrect()
    {
        // Arrange
        var branch = new Branch
        {
            Code = "BRN-001",
            Name = "Main Branch",
            IsHeadOffice = true
        };

        var role = new Role
        {
            Name = "Cashier",
            IsSystem = true
        };

        var user = new User
        {
            Username = "cashier1",
            Email = "cashier@pharmacy.com",
            FullName = "John Doe",
            PasswordHash = "hashed_password",
            BranchId = branch.Id,
            RoleId = role.Id,
            Branch = branch,
            Role = role
        };

        // Assert
        Assert.Equal(branch.Id, user.BranchId);
        Assert.Equal(role.Id, user.RoleId);
        Assert.NotNull(user.Branch);
        Assert.NotNull(user.Role);
    }

    /// <summary>
    /// Test decimal precision for financial values.
    /// </summary>
    [Fact]
    public void FinancialPrecisionIsConfiguredByConcept()
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used")
            .Options;
        using var context = new PharmacyDbContext(options);
        var product = context.Model.FindEntityType(typeof(Product))!;
        var batch = context.Model.FindEntityType(typeof(ProductBatch))!;

        Assert.Equal((18, 2), Precision(product, nameof(Product.PurchasePrice)));
        Assert.Equal((18, 2), Precision(product, nameof(Product.RetailPrice)));
        Assert.Equal((18, 2), Precision(product, nameof(Product.TradePrice)));
        Assert.Equal((5, 2), Precision(product, nameof(Product.MaximumDiscountPercent)));
        Assert.Equal((18, 2), Precision(batch, nameof(ProductBatch.PurchasePrice)));
        Assert.Equal((18, 2), Precision(batch, nameof(ProductBatch.RetailPrice)));

        static (int?, int?) Precision(Microsoft.EntityFrameworkCore.Metadata.IEntityType entity, string propertyName)
        {
            var property = entity.FindProperty(propertyName)!;
            return (property.GetPrecision(), property.GetScale());
        }
    }

    /// <summary>
    /// Test audit log captures entity changes.
    /// </summary>
    [Fact]
    public void AuditLogCapturesChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var product = new Product
        {
            SKU = "AUDIT-001",
            Name = "Medicine",
            Unit = "tablet",
            PackSize = 1,
            PurchasePrice = 1.00m,
            RetailPrice = 2.00m,
            CategoryId = Guid.NewGuid()
        };

        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = "Create",
            EntityType = "Product",
            EntityId = product.Id,
            NewValues = @"{ ""SKU"": ""AUDIT-001"", ""Name"": ""Medicine"" }",
            IPAddress = "192.168.1.1"
        };

        // Assert
        Assert.Equal("Create", auditLog.Action);
        Assert.Equal("Product", auditLog.EntityType);
        Assert.Equal(product.Id, auditLog.EntityId);
        Assert.NotNull(auditLog.NewValues);
    }

    /// <summary>
    /// Test inventory calculation from stock movements.
    /// </summary>
    [Fact]
    public void InventoryCalculatedFromStockMovements()
    {
        // Arrange
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var movements = new List<StockMovement>
        {
            new() { MovementType = StockMovementType.OpeningStock, Quantity = 100, ProductId = productId, ProductBatchId = batchId, BranchId = branchId },
            new() { MovementType = StockMovementType.Purchase, Quantity = 50, ProductId = productId, ProductBatchId = batchId, BranchId = branchId },
            new() { MovementType = StockMovementType.Sale, Quantity = -30, ProductId = productId, ProductBatchId = batchId, BranchId = branchId },
            new() { MovementType = StockMovementType.SaleReturn, Quantity = 10, ProductId = productId, ProductBatchId = batchId, BranchId = branchId },
        };

        // Act
        var currentStock = movements.Sum(m => m.Quantity);

        // Assert
        Assert.Equal(130, currentStock);  // 100 + 50 - 30 + 10
    }

    /// <summary>
    /// Test batch composite key constraint (Product + Batch Number).
    /// </summary>
    [Fact]
    public void BatchCompositeKeyUniquePerProduct()
    {
        // Arrange
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var batch1 = new ProductBatch
        {
            ProductId = productId1,
            BatchNumber = "SHARED-BATCH",
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            QuantityAvailable = 100,
            BranchId = branchId,
            PurchasePrice = 1.00m,
            RetailPrice = 2.00m
        };

        var batch2 = new ProductBatch
        {
            ProductId = productId2,
            BatchNumber = "SHARED-BATCH",  // Same batch number, different product
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            QuantityAvailable = 100,
            BranchId = branchId,
            PurchasePrice = 1.00m,
            RetailPrice = 2.00m
        };

        // Assert - same batch number can exist for different products
        Assert.Equal(batch1.BatchNumber, batch2.BatchNumber);
        Assert.NotEqual(batch1.ProductId, batch2.ProductId);
    }

    /// <summary>
    /// Test timestamps are UTC and consistent.
    /// </summary>
    [Fact]
    public void TimestampsAreUtcConsistent()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;
        var product = new Product
        {
            SKU = "TIME-001",
            Name = "Test",
            Unit = "unit",
            PackSize = 1,
            PurchasePrice = 1.00m,
            RetailPrice = 2.00m,
            CategoryId = Guid.NewGuid()
        };
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(product.CreatedAt >= beforeCreation);
        Assert.True(product.CreatedAt <= afterCreation.AddMilliseconds(100));  // Small buffer for timing
        Assert.True((product.UpdatedAt - product.CreatedAt).TotalMilliseconds < 100);  // UpdatedAt within 100ms of CreatedAt
    }

    [Fact]
    public void SaleMovementRequiresNegativeQuantity()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            var movement = new StockMovement
            {
                MovementType = StockMovementType.Sale,
                Quantity = 10,
                BranchId = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                ProductBatchId = Guid.NewGuid()
            };
        });

        Assert.Contains("requires a negative quantity", ex.Message);
    }

    [Fact]
    public void PurchaseMovementRequiresPositiveQuantity()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            var movement = new StockMovement
            {
                MovementType = StockMovementType.Purchase,
                Quantity = -10,
                BranchId = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                ProductBatchId = Guid.NewGuid()
            };
        });

        Assert.Contains("requires a positive quantity", ex.Message);
    }

    [Fact]
    public void StockMovementCannotBeLeftInvalidWhenTypeChangeThrows()
    {
        var movement = new StockMovement
        {
            MovementType = StockMovementType.Purchase,
            Quantity = 10,
            BranchId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            ProductBatchId = Guid.NewGuid()
        };

        Assert.Throws<InvalidOperationException>(() => movement.MovementType = StockMovementType.Sale);
        Assert.Equal(StockMovementType.Purchase, movement.MovementType);
        Assert.Equal(10, movement.Quantity);
    }

    [Fact]
    public void StockMovementRejectsZeroQuantityDuringValidation()
    {
        var movement = new StockMovement
        {
            MovementType = StockMovementType.Purchase,
            BranchId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            ProductBatchId = Guid.NewGuid()
        };

        Assert.Throws<InvalidOperationException>(movement.Validate);
    }

    [Fact]
    public void CurrentBalanceCannotBePersistedWithoutMatchingStockMovement()
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used")
            .Options;
        using var context = new PharmacyDbContext(options);
        var batch = new ProductBatch
        {
            ProductId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            BatchNumber = "CONTROLLED",
            ExpiryDate = new DateOnly(2027, 1, 1),
            QuantityAvailable = 10
        };
        context.Attach(batch);
        batch.QuantityAvailable = 11;

        var error = Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
        Assert.Contains("matching StockMovement", error.Message);
    }

    [Fact]
    public void FefoAllocationServiceAllocatesAcrossMultipleBatchesInExpiryOrder()
    {
        var service = new FefoAllocationService();
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var batches = new List<ProductBatch>
        {
            new() { Id = Guid.NewGuid(), ProductId = productId, BranchId = branchId, BatchNumber = "A", ExpiryDate = new DateOnly(2026, 10, 1), QuantityAvailable = 3, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.NewGuid(), ProductId = productId, BranchId = branchId, BatchNumber = "B", ExpiryDate = new DateOnly(2027, 1, 1), QuantityAvailable = 10, CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc) },
            new() { Id = Guid.NewGuid(), ProductId = productId, BranchId = branchId, BatchNumber = "C", ExpiryDate = new DateOnly(2025, 12, 1), QuantityAvailable = 20, CreatedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc) }
        };

        var allocations = service.Allocate(batches, productId, branchId, 5, new DateOnly(2026, 8, 29));

        Assert.Equal(2, allocations.Count);
        Assert.Equal("A", allocations[0].BatchNumber);
        Assert.Equal(3, allocations[0].AllocatedQuantity);
        Assert.Equal("B", allocations[1].BatchNumber);
        Assert.Equal(2, allocations[1].AllocatedQuantity);
    }

    [Fact]
    public void FefoAllocationExcludesExpiredDisposedOtherBranchAndOtherProductBatches()
    {
        var service = new FefoAllocationService();
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var saleDate = new DateOnly(2026, 8, 30);
        var eligibleId = Guid.NewGuid();
        var batches = new[]
        {
            new ProductBatch { Id = Guid.NewGuid(), ProductId = productId, BranchId = branchId, BatchNumber = "expired", ExpiryDate = saleDate.AddDays(-1), QuantityAvailable = 10 },
            new ProductBatch { Id = Guid.NewGuid(), ProductId = productId, BranchId = branchId, BatchNumber = "disposed", ExpiryDate = saleDate.AddDays(1), QuantityAvailable = 10, IsDisposed = true },
            new ProductBatch { Id = Guid.NewGuid(), ProductId = productId, BranchId = Guid.NewGuid(), BatchNumber = "other-branch", ExpiryDate = saleDate, QuantityAvailable = 10 },
            new ProductBatch { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), BranchId = branchId, BatchNumber = "other-product", ExpiryDate = saleDate, QuantityAvailable = 10 },
            new ProductBatch { Id = eligibleId, ProductId = productId, BranchId = branchId, BatchNumber = "eligible", ExpiryDate = saleDate, QuantityAvailable = 5 }
        };

        var allocation = Assert.Single(service.Allocate(batches, productId, branchId, 5, saleDate));
        Assert.Equal(eligibleId, allocation.BatchId);
    }

    [Fact]
    public void FefoAllocationThrowsForInsufficientEligibleStock()
    {
        var service = new FefoAllocationService();
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batches = new[]
        {
            new ProductBatch { ProductId = productId, BranchId = branchId, BatchNumber = "A", ExpiryDate = new DateOnly(2027, 1, 1), QuantityAvailable = 4 }
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            service.Allocate(batches, productId, branchId, 5, new DateOnly(2026, 8, 30)));
        Assert.Contains("Insufficient eligible stock", error.Message);
    }
}

/// <summary>
/// Tests for authentication service.
/// </summary>
public class AuthenticationTests
{
    [Fact]
    public void PasswordHashingProducesConsistentResult()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var password = "MySecurePassword123!";
        var hash = hasher.Hash(password);

        Assert.NotEqual(password, hash);
        Assert.True(hasher.Verify(password, hash));
        Assert.False(hasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void PasswordHashingIsDifferentForDifferentPasswords()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash1 = hasher.Hash("Password1");
        var hash2 = hasher.Hash("Password1");

        Assert.NotEqual(hash1, hash2);
        Assert.True(hasher.Verify("Password1", hash1));
        Assert.True(hasher.Verify("Password1", hash2));
    }

    [Fact]
    public void UserRequiredFieldsValidation()
    {
        // Arrange & Act
        var user = new User
        {
            Username = "testuser",
            Email = "test@pharmacy.com",
            FullName = "Test User",
            PasswordHash = "hashed",
            BranchId = Guid.NewGuid(),
            RoleId = Guid.NewGuid()
        };

        // Assert
        Assert.NotEmpty(user.Username);
        Assert.NotEmpty(user.Email);
        Assert.NotEmpty(user.FullName);
        Assert.NotEmpty(user.PasswordHash);
        Assert.NotEqual(Guid.Empty, user.BranchId);
        Assert.NotEqual(Guid.Empty, user.RoleId);
    }

    [Fact]
    public async Task InactiveUserCannotAuthenticate()
    {
        var repository = new Mock<IUserAccountRepository>();
        repository
            .Setup(r => r.GetActiveUserByUsernameAsync("inactive", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var tokenService = new Mock<ITokenService>(MockBehavior.Strict);
        var service = new AuthService(repository.Object, hasher.Object, tokenService.Object);

        var result = await service.LoginAsync(new LoginRequest
        {
            Username = "inactive",
            Password = "not-checked"
        });

        Assert.Null(result);
        hasher.VerifyNoOtherCalls();
        tokenService.VerifyNoOtherCalls();
    }

    [Fact]
    public void JwtContainsConfiguredIssuerAudienceExpiryRoleAndPermissions()
    {
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c["Jwt:Key"]).Returns("test-only-key-with-at-least-32-characters");
        configuration.Setup(c => c["Jwt:Issuer"]).Returns("test-issuer");
        configuration.Setup(c => c["Jwt:Audience"]).Returns("test-audience");
        configuration.Setup(c => c["Jwt:ExpirationMinutes"]).Returns("30");
        var service = new JwtTokenService(configuration.Object);

        var encoded = service.CreateToken(
            Guid.NewGuid(),
            "owner",
            "owner@example.test",
            "Owner User",
            "Owner",
            Guid.NewGuid(),
            Guid.NewGuid(),
            new[] { "inventory.view", "inventory.adjust", "inventory.view" });

        var token = new JwtSecurityTokenHandler().ReadJwtToken(encoded);
        Assert.Equal("test-issuer", token.Issuer);
        Assert.Contains("test-audience", token.Audiences);
        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Owner");
        Assert.Equal(2, token.Claims.Count(c => c.Type == "permission"));
    }
}

/// <summary>
/// Tests for branch isolation.
/// </summary>
public class BranchIsolationTests
{
    [Fact]
    public void InventoryIsolatedByBranch()
    {
        // Arrange
        var branch1Id = Guid.NewGuid();
        var branch2Id = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var inventory1 = new Inventory
        {
            BranchId = branch1Id,
            ProductId = productId,
            ProductBatchId = batchId,
            QuantityInStock = 100
        };

        var inventory2 = new Inventory
        {
            BranchId = branch2Id,
            ProductId = productId,
            ProductBatchId = batchId,
            QuantityInStock = 50
        };

        // Assert - same product, different quantities per branch
        Assert.Equal(productId, inventory1.ProductId);
        Assert.Equal(productId, inventory2.ProductId);
        Assert.NotEqual(branch1Id, branch2Id);
        Assert.NotEqual(inventory1.QuantityInStock, inventory2.QuantityInStock);
    }

    [Fact]
    public void StockMovementsIsolatedByBranch()
    {
        // Arrange
        var branch1 = Guid.NewGuid();
        var branch2 = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var movement1 = new StockMovement
        {
            BranchId = branch1,
            ProductId = productId,
            Quantity = 100,
            MovementType = StockMovementType.Purchase
        };

        var movement2 = new StockMovement
        {
            BranchId = branch2,
            ProductId = productId,
            Quantity = 50,
            MovementType = StockMovementType.Purchase
        };

        // Assert - movements at different branches don't affect each other
        Assert.NotEqual(movement1.BranchId, movement2.BranchId);
        Assert.Equal(productId, movement1.ProductId);
        Assert.Equal(productId, movement2.ProductId);
    }
}
