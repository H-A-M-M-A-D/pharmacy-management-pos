using Microsoft.EntityFrameworkCore;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Tests;

public sealed class Phase6PricingTests
{
    [Fact]
    public async Task Customer_product_rule_wins_over_category_rule_deterministically()
    {
        await using var db = CreateContext();
        var category = new ProductCategory { Name = "Pain", NormalizedName = "PAIN" };
        var branch = new Branch { Code = "B1", NormalizedCode = "B1", Name = "Main" };
        var product = new Product { SKU = "P-1", NormalizedSku = "P-1", Name = "Tablet", Unit = "tablet", PackSize = 1, PurchasePrice = 5, RetailPrice = 20, Category = category };
        var customer = new Customer { CustomerCode = "C-1", Name = "Clinic", NormalizedName = "CLINIC", CustomerType = CustomerType.Institutional };
        db.AddRange(category, branch, product, customer);
        db.PricingRules.AddRange(
            new PricingRule { Name = "Category discount", Category = category, AdjustmentType = PricingAdjustmentType.PercentageDiscount, AdjustmentValue = 50, Priority = 100 },
            new PricingRule { Name = "Customer product price", Customer = customer, Product = product, Branch = branch, AdjustmentType = PricingAdjustmentType.FixedPrice, AdjustmentValue = 8, Priority = 0 });
        await db.SaveChangesAsync();

        var result = await new PriceResolutionService(db).ResolveForContextAsync(customer.Id, product.Id, 1, branchId: branch.Id, saleType: SaleType.Retail);

        Assert.Equal(8, result.Price);
        Assert.Equal(PriceSource.PricingRule, result.Source);
    }

    [Fact]
    public async Task Promotion_never_reduces_price_below_cost_and_no_rule_keeps_default_fallback()
    {
        await using var db = CreateContext();
        var product = new Product { SKU = "P-2", NormalizedSku = "P-2", Name = "Syrup", Unit = "bottle", PackSize = 1, PurchasePrice = 12, RetailPrice = 20, Category = new ProductCategory { Name = "Cold", NormalizedName = "COLD" } };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.PricingRules.Add(new PricingRule { Name = "Aggressive promotion", ProductId = product.Id, Kind = PricingRuleKind.Promotion, AdjustmentType = PricingAdjustmentType.FixedPrice, AdjustmentValue = 2, Priority = 10 });
        await db.SaveChangesAsync();

        var resolver = new PriceResolutionService(db);
        var promoted = await resolver.ResolveAsync(null, product.Id, 1);
        db.PricingRules.RemoveRange(db.PricingRules);
        await db.SaveChangesAsync();
        var fallback = await resolver.ResolveAsync(null, product.Id, 1);

        Assert.Equal(12, promoted.Price);
        Assert.Equal(PriceSource.Promotion, promoted.Source);
        Assert.Null(fallback.Price);
        Assert.Equal(PriceSource.Default, fallback.Source);
    }

    private static PharmacyDbContext CreateContext() => new(new DbContextOptionsBuilder<PharmacyDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}