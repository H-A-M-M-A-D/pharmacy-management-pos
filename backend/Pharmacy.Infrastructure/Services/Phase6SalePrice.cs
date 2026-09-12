using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Pricing;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Services;

public sealed partial class Phase6Service
{
    public async Task<ResolvedPriceDto> ResolveSalePriceAsync(Guid actorId, Guid? customerId, Guid productId, int quantity, SaleType saleType, CancellationToken ct = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesCreate, ct);
        if (quantity < 1 || !Enum.IsDefined(saleType)) throw new RequestValidationException("Quantity and sale type are invalid.");
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == productId && x.IsActive, ct) ?? throw new ResourceNotFoundException("Active product was not found.");
        var result = await new PriceResolutionService(db).ResolveForContextAsync(customerId, productId, quantity, branchId: actor.BranchId, saleType: saleType, atUtc: clock.GetUtcNow().UtcDateTime, cancellationToken: ct);
        return new(result.Price, result.Source.ToString(), result.PriceLevelId, product.RetailPrice);
    }
}
