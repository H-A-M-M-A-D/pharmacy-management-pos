using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Phase6;
using Pharmacy.Application.Security;

namespace Pharmacy.Infrastructure.Services;

public sealed partial class Phase6Service
{
    private async Task ValidateRuleReferences(Pharmacy.Application.DTOs.Phase6.PricingRuleRequest request, Pharmacy.Domain.Entities.User actor, CancellationToken ct)
    {
        if (request.ProductId.HasValue && !await db.Products.AnyAsync(x => x.Id == request.ProductId, ct)
            || request.CategoryId.HasValue && !await db.ProductCategories.AnyAsync(x => x.Id == request.CategoryId, ct)
            || request.ManufacturerId.HasValue && !await db.Manufacturers.AnyAsync(x => x.Id == request.ManufacturerId, ct)
            || request.CustomerId.HasValue && !await db.Customers.AnyAsync(x => x.Id == request.CustomerId, ct)
            || request.BranchId.HasValue && !await db.Branches.AnyAsync(x => x.Id == request.BranchId, ct))
            throw new Pharmacy.Application.Common.RequestValidationException("A selected pricing scope was not found.");
        if (request.PriceLevelId.HasValue) {
            var level = await db.PriceLevels.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.PriceLevelId, ct) ?? throw new Pharmacy.Application.Common.RequestValidationException("Selected price level was not found.");
            if (level.BranchId.HasValue) EnsureBranch(actor, level.BranchId.Value);
        }
    }
    public async Task<IReadOnlyList<Phase6OptionDto>> ReorderSuppliersAsync(Guid actorId, CancellationToken ct = default)
    {
        await Require(actorId, PermissionCatalog.PurchaseOrdersCreate, ct);
        await Require(actorId, PermissionCatalog.InventoryReorderView, ct);
        return await db.Suppliers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new Phase6OptionDto(x.Id, x.Name)).ToListAsync(ct);
    }
    public async Task<Phase6PricingOptions> PricingOptionsAsync(Guid actorId, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PricingManage, ct);
        return new(await db.ProductCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new Phase6OptionDto(x.Id, x.Name)).ToListAsync(ct),
            await db.Manufacturers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new Phase6OptionDto(x.Id, x.Name)).ToListAsync(ct),
            await db.Products.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new Phase6OptionDto(x.Id, x.Name + " / " + x.SKU)).ToListAsync(ct),
            await db.PriceLevels.AsNoTracking().Where(x => x.IsActive && (x.BranchId == null || x.BranchId == actor.BranchId)).OrderBy(x => x.Name).Select(x => new Phase6OptionDto(x.Id, x.Name)).ToListAsync(ct),
            await db.Customers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new Phase6OptionDto(x.Id, x.Name)).ToListAsync(ct),
            await db.Branches.AsNoTracking().Where(x => x.Id == actor.BranchId && x.IsActive).Select(x => new Phase6OptionDto(x.Id, x.Name)).ToListAsync(ct), await ThresholdsCore(ct));
    }
}
