using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Pricing;
using Pharmacy.Application.Services.Pricing;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class PricingRepository(PharmacyDbContext context) : IPricingRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);

    public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) =>
        context.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);

    public Task<PriceLevel?> GetPriceLevelAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.PriceLevels.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> PriceLevelCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken = default) =>
        context.PriceLevels.AnyAsync(x => x.Code == code && (!excludingId.HasValue || x.Id != excludingId.Value), cancellationToken);

    public async Task<IReadOnlyList<PriceLevelDto>> ListPriceLevelsAsync(bool activeOnly, Guid? scopeBranchId, CancellationToken cancellationToken = default)
    {
        var query = context.PriceLevels.AsNoTracking().Include(x => x.Branch).AsQueryable();
        if (activeOnly) query = query.Where(x => x.IsActive);
        if (scopeBranchId.HasValue) query = query.Where(x => x.BranchId == null || x.BranchId == scopeBranchId);
        return await query.OrderBy(x => x.Priority).ThenBy(x => x.Name)
            .Select(x => new PriceLevelDto(x.Id, x.Name, x.Code, x.Priority, x.IsDefault, x.IsActive, x.BranchId, x.Branch != null ? x.Branch.Name : null))
            .ToListAsync(cancellationToken);
    }

    public async Task AddPriceLevelAsync(PriceLevel level, CancellationToken cancellationToken = default) => await context.PriceLevels.AddAsync(level, cancellationToken);

    public async Task ClearDefaultPriceLevelAsync(Guid? excludingId, CancellationToken cancellationToken = default)
    {
        var others = await context.PriceLevels.Where(x => x.IsDefault && (!excludingId.HasValue || x.Id != excludingId.Value)).ToListAsync(cancellationToken);
        foreach (var level in others) level.IsDefault = false;
    }

    public Task<ProductPriceLevel?> GetProductPriceLevelAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ProductPriceLevels.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<ProductPriceLevel?> FindProductPriceLevelAsync(Guid productId, Guid priceLevelId, CancellationToken cancellationToken = default) =>
        context.ProductPriceLevels.FirstOrDefaultAsync(x => x.ProductId == productId && x.PriceLevelId == priceLevelId, cancellationToken);

    public async Task<IReadOnlyList<ProductPriceLevelDto>> ListProductPriceLevelsAsync(Guid? productId, Guid? priceLevelId, CancellationToken cancellationToken = default)
    {
        var query = context.ProductPriceLevels.AsNoTracking().Include(x => x.Product).Include(x => x.PriceLevel).AsQueryable();
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId.Value);
        if (priceLevelId.HasValue) query = query.Where(x => x.PriceLevelId == priceLevelId.Value);
        return await query.OrderBy(x => x.Product!.Name)
            .Select(x => new ProductPriceLevelDto(x.Id, x.ProductId, x.Product!.Name, x.Product.SKU, x.PriceLevelId, x.PriceLevel!.Name, x.SellingPrice, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task AddProductPriceLevelAsync(ProductPriceLevel entry, CancellationToken cancellationToken = default) => await context.ProductPriceLevels.AddAsync(entry, cancellationToken);
    public async Task AddPriceHistoryAsync(PricingPriceHistory entry, CancellationToken cancellationToken = default) => await context.PricingPriceHistories.AddAsync(entry, cancellationToken);
    public Task RemoveProductPriceLevelAsync(ProductPriceLevel entry, CancellationToken cancellationToken = default) { context.ProductPriceLevels.Remove(entry); return Task.CompletedTask; }

    public Task<ProductPriceBreak?> GetProductPriceBreakAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ProductPriceBreaks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<ProductPriceBreak?> FindProductPriceBreakAsync(Guid productId, Guid? priceLevelId, int minimumQuantity, CancellationToken cancellationToken = default) =>
        context.ProductPriceBreaks.FirstOrDefaultAsync(x => x.ProductId == productId && x.PriceLevelId == priceLevelId && x.MinimumQuantity == minimumQuantity, cancellationToken);

    public async Task<IReadOnlyList<ProductPriceBreakDto>> ListProductPriceBreaksAsync(Guid? productId, CancellationToken cancellationToken = default)
    {
        var query = context.ProductPriceBreaks.AsNoTracking().Include(x => x.Product).Include(x => x.PriceLevel).AsQueryable();
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId.Value);
        return await query.OrderBy(x => x.Product!.Name).ThenBy(x => x.MinimumQuantity)
            .Select(x => new ProductPriceBreakDto(x.Id, x.ProductId, x.Product!.Name, x.Product.SKU, x.PriceLevelId, x.PriceLevel != null ? x.PriceLevel.Name : null, x.MinimumQuantity, x.SellingPrice, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task AddProductPriceBreakAsync(ProductPriceBreak entry, CancellationToken cancellationToken = default) => await context.ProductPriceBreaks.AddAsync(entry, cancellationToken);
    public Task RemoveProductPriceBreakAsync(ProductPriceBreak entry, CancellationToken cancellationToken = default) { context.ProductPriceBreaks.Remove(entry); return Task.CompletedTask; }

    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ResourceConflictException("A pricing record with the same unique value already exists.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation })
        {
            throw new RequestValidationException("Pricing constraints were violated.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            throw new ResourceConflictException("This pricing record changed while saving. Refresh and try again.");
        }
    }
}
