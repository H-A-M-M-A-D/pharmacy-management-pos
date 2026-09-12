using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Pricing;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Pricing;

public sealed class PricingService(IPricingRepository repository, IPriceResolutionService resolver, TimeProvider timeProvider) : IPricingService
{
    public async Task<IReadOnlyList<PriceLevelDto>> ListPriceLevelsAsync(Guid actorId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PricingView, cancellationToken);
        var scopeBranchId = CanSelectBranch(actor) ? null : (Guid?)actor.BranchId;
        return await repository.ListPriceLevelsAsync(activeOnly, scopeBranchId, cancellationToken);
    }

    public async Task<PriceLevelDto> CreatePriceLevelAsync(Guid actorId, PriceLevelRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PricingManage, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        Validate(request);
        if (await repository.PriceLevelCodeExistsAsync(NormalizeCode(request.Code), null, cancellationToken))
            throw new RequestValidationException("A price level with this code already exists.");
        if (request.IsDefault) await repository.ClearDefaultPriceLevelAsync(null, cancellationToken);
        var level = new PriceLevel
        {
            Name = request.Name.Trim(),
            Code = NormalizeCode(request.Code),
            Priority = request.Priority,
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
            BranchId = request.BranchId
        };
        await repository.AddPriceLevelAsync(level, cancellationToken);
        await Audit(actorId, "PriceLevelCreated", level.Id, new { level.Name, level.Code, level.Priority, level.IsDefault, level.IsActive }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return (await repository.ListPriceLevelsAsync(false, null, cancellationToken)).Single(x => x.Id == level.Id);
    }

    public async Task<PriceLevelDto> UpdatePriceLevelAsync(Guid actorId, Guid id, PriceLevelRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PricingManage, cancellationToken);
        Validate(request);
        var level = await repository.GetPriceLevelAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Price level was not found.");
        EnsureBranchAccess(actor, level.BranchId);
        EnsureBranchAccess(actor, request.BranchId);
        var normalizedCode = NormalizeCode(request.Code);
        if (await repository.PriceLevelCodeExistsAsync(normalizedCode, id, cancellationToken))
            throw new RequestValidationException("A price level with this code already exists.");
        if (request.IsDefault && !level.IsDefault) await repository.ClearDefaultPriceLevelAsync(id, cancellationToken);
        level.Name = request.Name.Trim();
        level.Code = normalizedCode;
        level.Priority = request.Priority;
        level.IsDefault = request.IsDefault;
        level.IsActive = request.IsActive;
        level.BranchId = request.BranchId;
        level.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await Audit(actorId, "PriceLevelUpdated", level.Id, new { level.Name, level.Code, level.Priority, level.IsDefault, level.IsActive }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return (await repository.ListPriceLevelsAsync(false, null, cancellationToken)).Single(x => x.Id == level.Id);
    }

    public async Task<IReadOnlyList<ProductPriceLevelDto>> ListProductPricesAsync(Guid actorId, Guid? productId, Guid? priceLevelId, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.PricingView, cancellationToken);
        return await repository.ListProductPriceLevelsAsync(productId, priceLevelId, cancellationToken);
    }

    public async Task<ProductPriceLevelDto> SetProductPriceAsync(Guid actorId, ProductPriceLevelRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.PricingManage, cancellationToken);
        if (request.SellingPrice < 0) throw new RequestValidationException("Selling price cannot be negative.");
        if (await repository.GetProductAsync(request.ProductId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Product is invalid or inactive.");
        if (await repository.GetPriceLevelAsync(request.PriceLevelId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Price level is invalid or inactive.");
        var existing = await repository.FindProductPriceLevelAsync(request.ProductId, request.PriceLevelId, cancellationToken);
        if (existing is null)
        {
            existing = new ProductPriceLevel { ProductId = request.ProductId, PriceLevelId = request.PriceLevelId, SellingPrice = Money(request.SellingPrice), IsActive = request.IsActive };
            await repository.AddProductPriceLevelAsync(existing, cancellationToken);
        }
        else
        {
            var oldPrice = existing.SellingPrice;
            existing.SellingPrice = Money(request.SellingPrice);
            existing.IsActive = request.IsActive;
            existing.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            if (oldPrice != existing.SellingPrice)
                await repository.AddPriceHistoryAsync(new PricingPriceHistory
                {
                    ProductId = existing.ProductId,
                    PriceLevelId = existing.PriceLevelId,
                    OldPrice = oldPrice,
                    NewPrice = existing.SellingPrice,
                    Reason = "Product price updated",
                    ActorId = actorId
                }, cancellationToken);
        }
        await Audit(actorId, "ProductPriceLevelSet", existing.Id, new { request.ProductId, request.PriceLevelId, existing.SellingPrice, existing.IsActive }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return (await repository.ListProductPriceLevelsAsync(request.ProductId, request.PriceLevelId, cancellationToken)).Single(x => x.Id == existing.Id);
    }

    public async Task RemoveProductPriceAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.PricingManage, cancellationToken);
        var entry = await repository.GetProductPriceLevelAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Product price was not found.");
        await repository.RemoveProductPriceLevelAsync(entry, cancellationToken);
        await Audit(actorId, "ProductPriceLevelRemoved", id, new { entry.ProductId, entry.PriceLevelId }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductPriceBreakDto>> ListProductPriceBreaksAsync(Guid actorId, Guid? productId, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.PricingView, cancellationToken);
        return await repository.ListProductPriceBreaksAsync(productId, cancellationToken);
    }

    public async Task<ProductPriceBreakDto> SetProductPriceBreakAsync(Guid actorId, ProductPriceBreakRequest request, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.PricingManage, cancellationToken);
        if (request.SellingPrice < 0) throw new RequestValidationException("Selling price cannot be negative.");
        if (request.MinimumQuantity <= 0) throw new RequestValidationException("Minimum quantity must be greater than zero.");
        if (await repository.GetProductAsync(request.ProductId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Product is invalid or inactive.");
        if (request.PriceLevelId.HasValue && await repository.GetPriceLevelAsync(request.PriceLevelId.Value, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Price level is invalid or inactive.");
        var existing = await repository.FindProductPriceBreakAsync(request.ProductId, request.PriceLevelId, request.MinimumQuantity, cancellationToken);
        if (existing is null)
        {
            existing = new ProductPriceBreak { ProductId = request.ProductId, PriceLevelId = request.PriceLevelId, MinimumQuantity = request.MinimumQuantity, SellingPrice = Money(request.SellingPrice), IsActive = request.IsActive };
            await repository.AddProductPriceBreakAsync(existing, cancellationToken);
        }
        else
        {
            existing.SellingPrice = Money(request.SellingPrice);
            existing.IsActive = request.IsActive;
            existing.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        }
        await Audit(actorId, "ProductPriceBreakSet", existing.Id, new { request.ProductId, request.PriceLevelId, request.MinimumQuantity, existing.SellingPrice, existing.IsActive }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return (await repository.ListProductPriceBreaksAsync(request.ProductId, cancellationToken)).Single(x => x.Id == existing.Id);
    }

    public async Task RemoveProductPriceBreakAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.PricingManage, cancellationToken);
        var entry = await repository.GetProductPriceBreakAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Product price break was not found.");
        await repository.RemoveProductPriceBreakAsync(entry, cancellationToken);
        await Audit(actorId, "ProductPriceBreakRemoved", id, new { entry.ProductId, entry.PriceLevelId, entry.MinimumQuantity }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<ResolvedPriceDto> PreviewResolvedPriceAsync(Guid actorId, Guid? customerId, Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.PricingView, cancellationToken);
        if (quantity <= 0) throw new RequestValidationException("Quantity must be greater than zero.");
        var product = await repository.GetProductAsync(productId, cancellationToken) ?? throw new ResourceNotFoundException("Product was not found.");
        var result = await resolver.ResolveAsync(customerId, productId, quantity, cancellationToken: cancellationToken);
        return new ResolvedPriceDto(result.Price, result.Source.ToString(), result.PriceLevelId, product.RetailPrice);
    }

    private async Task<User> Require(Guid actorId, string permission, CancellationToken cancellationToken)
    {
        var actor = await repository.GetActorAsync(actorId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private static bool CanSelectBranch(User actor) =>
        actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.BranchesView) == true;

    private static void EnsureBranchAccess(User actor, Guid? branchId)
    {
        if (branchId.HasValue && !CanSelectBranch(actor) && actor.BranchId != branchId)
            throw new ForbiddenOperationException("The current user is not permitted to manage this branch.");
    }

    private static void Validate(PriceLevelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new RequestValidationException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Code)) throw new RequestValidationException("Code is required.");
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private Task Audit(Guid actor, string action, Guid id, object? current, CancellationToken ct) => repository.AddAuditAsync(new AuditLog
    { UserId = actor, Action = action, EntityType = "Pricing", EntityId = id, NewValues = current is null ? null : JsonSerializer.Serialize(current) }, ct);
}
