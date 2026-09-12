using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.SalesOrders;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Godowns;
using Pharmacy.Application.Services.Pricing;
using Pharmacy.Application.Services.Sales;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.SalesOrders;

/// <summary>
/// Draft -&gt; Confirmed -&gt; PartiallyFulfilled/Fulfilled workflow, with Cancel available before the
/// order is fully fulfilled. Confirming never touches stock or the journal - only fulfilling does,
/// by delegating to ISalesService.PostSaleAsync (SaleType.Wholesale, SalesOrderId set) so the exact
/// same FEFO/journal/credit-limit machinery as any other sale applies. Each fulfillment call may
/// create one Sale for a partial quantity; SalesService is the one that increments
/// SalesOrderItem.FulfilledQuantity, inside the same transaction as the Sale it posts, using the
/// order item's own snapshot price/discount (never trusting client-supplied pricing for a
/// document-linked line - see SalesService.BuildPostedSale).
/// </summary>
public sealed class SalesOrderService(
    ISalesOrderRepository repository,
    IGodownAccessService godownAccess,
    IPriceResolutionService priceResolver,
    ISalesService salesService,
    TimeProvider timeProvider) : ISalesOrderService
{
    public async Task<PagedResult<SalesOrderListItemDto>> ListOrdersAsync(Guid actorId, SalesOrderListQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesOrdersView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        var scope = Scope(actor, query.BranchId);
        return await repository.ListOrdersAsync(query with { BranchId = scope.BranchId }, actor.BranchId, scope.CanSelectBranch, cancellationToken);
    }

    public async Task<SalesOrderDetailsDto> GetOrderAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.SalesOrdersView, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<SalesOrderDetailsDto> CreateOrderAsync(Guid actorId, SalesOrderRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesOrdersCreate, cancellationToken);
        ValidateHeader(request);
        var branchId = request.BranchId ?? actor.BranchId;
        EnsureBranchAccess(actor, branchId);
        SalesOrder? order = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var branch = await RequiredActiveBranch(branchId, ct);
            var customer = await RequiredCustomer(request.CustomerId, ct);
            if (request.GodownId.HasValue) await EnsureGodownAccessAsync(actor, branch.Id, request.GodownId.Value, ct);
            order = new SalesOrder
            {
                OrderNumber = await repository.NextOrderNumberAsync(ct),
                BranchId = branch.Id,
                GodownId = request.GodownId,
                CustomerId = customer.Id,
                PriceLevelId = request.PriceLevelId ?? customer.PriceLevelId,
                OrderDate = request.OrderDate,
                ExpectedDeliveryDate = request.ExpectedDeliveryDate,
                Status = SalesOrderStatus.Draft,
                Notes = Clean(request.Notes),
                CreatedByUserId = actorId,
                Items = await BuildItemsAsync(actor, customer.Id, order: null, request.Items, ct)
            };
            Totals(order);
            await repository.AddOrderAsync(order, ct);
            await Audit(actorId, "SalesOrderCreated", order.Id, new { order.OrderNumber, order.CustomerId, ItemCount = order.Items.Count, order.NetTotal }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(order!.Id, cancellationToken);
    }

    public async Task<SalesOrderDetailsDto> UpdateOrderAsync(Guid actorId, Guid id, SalesOrderRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesOrdersUpdate, cancellationToken);
        ValidateHeader(request);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var order = await RequiredOrder(id, ct);
            EnsureBranchAccess(actor, order.BranchId);
            if (order.Status != SalesOrderStatus.Draft) throw new RequestValidationException("Only draft sales orders can be edited.");
            var branchId = request.BranchId ?? order.BranchId;
            EnsureBranchAccess(actor, branchId);
            var branch = await RequiredActiveBranch(branchId, ct);
            var customer = await RequiredCustomer(request.CustomerId, ct);
            if (request.GodownId.HasValue) await EnsureGodownAccessAsync(actor, branch.Id, request.GodownId.Value, ct);
            order.BranchId = branch.Id;
            order.GodownId = request.GodownId;
            order.CustomerId = customer.Id;
            order.PriceLevelId = request.PriceLevelId ?? customer.PriceLevelId;
            order.OrderDate = request.OrderDate;
            order.ExpectedDeliveryDate = request.ExpectedDeliveryDate;
            order.Notes = Clean(request.Notes);
            order.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            var items = await BuildItemsAsync(actor, customer.Id, order, request.Items, ct);
            repository.ReplaceOrderItems(order, items);
            Totals(order);
            await Audit(actorId, "SalesOrderUpdated", order.Id, new { order.OrderNumber, ItemCount = items.Count, order.NetTotal }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<SalesOrderDetailsDto> ConfirmOrderAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesOrdersConfirm, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var order = await RequiredOrder(id, ct);
            EnsureBranchAccess(actor, order.BranchId);
            if (order.Status != SalesOrderStatus.Draft) throw new RequestValidationException("Only draft sales orders can be confirmed.");
            if (order.Items.Count == 0) throw new RequestValidationException("At least one item is required before confirming an order.");
            var now = timeProvider.GetUtcNow().UtcDateTime;
            order.Status = SalesOrderStatus.Confirmed;
            order.ConfirmedByUserId = actorId;
            order.ConfirmedAtUtc = now;
            order.UpdatedAt = now;
            await Audit(actorId, "SalesOrderConfirmed", order.Id, new { order.OrderNumber }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<SalesOrderDetailsDto> CancelOrderAsync(Guid actorId, Guid id, CancelSalesOrderRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesOrdersCancel, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("Cancellation reason is required.");
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var order = await RequiredOrder(id, ct);
            EnsureBranchAccess(actor, order.BranchId);
            if (order.Status is SalesOrderStatus.Fulfilled or SalesOrderStatus.Cancelled)
                throw new RequestValidationException("This sales order can no longer be cancelled.");
            var now = timeProvider.GetUtcNow().UtcDateTime;
            order.Status = SalesOrderStatus.Cancelled;
            order.CancelledAtUtc = now;
            order.CancellationReason = request.Reason.Trim();
            order.UpdatedAt = now;
            await Audit(actorId, "SalesOrderCancelled", order.Id, new { order.OrderNumber, request.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<SaleDetailsDto> FulfillOrderAsync(Guid actorId, Guid id, FulfillSalesOrderRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesOrdersFulfill, cancellationToken);
        if (request.Items.Count == 0) throw new RequestValidationException("At least one item is required to fulfill an order.");
        var order = await RequiredOrder(id, cancellationToken);
        EnsureBranchAccess(actor, order.BranchId);
        if (order.Status is not (SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyFulfilled))
            throw new RequestValidationException("Only confirmed or partially fulfilled sales orders can be fulfilled.");
        var lines = request.Items.Select(x => new SaleLineRequest(x.ProductId, x.Quantity)).ToList();
        return await salesService.PostSaleAsync(actorId, new PostSaleRequest(
            BranchId: order.BranchId, CustomerId: order.CustomerId, CustomerName: null, CustomerPhone: null, Notes: null,
            Items: lines, Payments: request.Payments, GodownId: order.GodownId, SaleType: SaleType.Wholesale,
            PriceLevelId: order.PriceLevelId, QuotationId: null, SalesOrderId: order.Id,
            CustomerPoNumber: request.CustomerPoNumber, DueDateOverride: request.DueDateOverride,
            CreditLimitOverrideReason: request.CreditLimitOverrideReason), cancellationToken);
    }

    private async Task<List<SalesOrderItem>> BuildItemsAsync(User actor, Guid customerId, SalesOrder? order, IReadOnlyList<SalesOrderLineRequest> items, CancellationToken ct)
    {
        if (items.Count == 0) throw new RequestValidationException("At least one item is required.");
        if (items.Select(x => x.ProductId).Distinct().Count() != items.Count)
            throw new RequestValidationException("Duplicate product lines are not allowed on a single sales order.");
        var existingFulfilled = order?.Items.ToDictionary(x => x.ProductId, x => x.FulfilledQuantity) ?? [];
        var built = new List<SalesOrderItem>();
        foreach (var line in items)
        {
            if (line.Quantity <= 0) throw new RequestValidationException("Quantity must be greater than zero.");
            if (line.DiscountPercent is < 0 or > 100) throw new RequestValidationException("Discount must be between 0 and 100.");
            var product = await repository.GetProductAsync(line.ProductId, ct);
            if (product is not { IsActive: true }) throw new RequestValidationException("Product is invalid or inactive.");
            existingFulfilled.TryGetValue(line.ProductId, out var alreadyFulfilled);
            if (line.Quantity < alreadyFulfilled) throw new RequestValidationException($"{product.Name} already has {alreadyFulfilled} unit(s) fulfilled; the order quantity cannot be reduced below that.");

            decimal unitPrice;
            if (line.UnitPriceOverride.HasValue)
            {
                if (actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.SalesPriceOverride) != true)
                    throw new ForbiddenOperationException("The current user is not permitted to override the selling price.");
                if (string.IsNullOrWhiteSpace(line.PriceOverrideReason)) throw new RequestValidationException("A reason is required to override the selling price.");
                unitPrice = Money(line.UnitPriceOverride.Value);
            }
            else
            {
                var resolved = await priceResolver.ResolveAsync(customerId, product.Id, line.Quantity, order?.PriceLevelId, cancellationToken: ct);
                unitPrice = resolved.Price ?? product.RetailPrice;
            }

            if (line.DiscountPercent > product.MaximumDiscountPercent)
            {
                if (actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.SalesDiscountOverride) != true)
                    throw new RequestValidationException("Discount exceeds the product maximum.");
                if (string.IsNullOrWhiteSpace(line.DiscountOverrideReason)) throw new RequestValidationException("A reason is required to exceed the maximum discount.");
            }

            var gross = Money(unitPrice * line.Quantity);
            var discount = Money(gross * line.DiscountPercent / 100m);
            built.Add(new SalesOrderItem
            {
                ProductId = product.Id,
                OrderedQuantity = line.Quantity,
                FulfilledQuantity = alreadyFulfilled,
                UnitPrice = unitPrice,
                DiscountPercent = line.DiscountPercent,
                GrossAmount = gross,
                DiscountAmount = discount,
                NetAmount = Money(gross - discount)
            });
        }
        return built;
    }

    private static void Totals(SalesOrder order)
    {
        order.Subtotal = Money(order.Items.Sum(x => x.GrossAmount));
        order.DiscountTotal = Money(order.Items.Sum(x => x.DiscountAmount));
        order.NetTotal = Money(order.Items.Sum(x => x.NetAmount));
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

    private static void EnsureBranchAccess(User actor, Guid branchId)
    {
        if (!CanSelectBranch(actor) && actor.BranchId != branchId) throw new ForbiddenOperationException("The current user is not permitted to manage this branch.");
    }

    private static (Guid? BranchId, bool CanSelectBranch) Scope(User actor, Guid? requestedBranchId)
    {
        var canSelect = CanSelectBranch(actor);
        return canSelect ? (requestedBranchId, true) : (actor.BranchId, false);
    }

    private async Task EnsureGodownAccessAsync(User actor, Guid branchId, Guid godownId, CancellationToken ct)
    {
        var godown = await godownAccess.GetGodownAsync(godownId, ct) ?? throw new RequestValidationException("Godown is invalid.");
        if (godown.BranchId != branchId) throw new RequestValidationException("Godown does not belong to the selected branch.");
        if (!godown.IsActive) throw new RequestValidationException("Godown is inactive.");
        if (CanSelectBranch(actor)) return;
        if (!await godownAccess.UserHasAccessAsync(actor.Id, godownId, ct))
            throw new ForbiddenOperationException("You do not have access to this godown.");
    }

    private async Task<Branch> RequiredActiveBranch(Guid branchId, CancellationToken ct)
    {
        var branch = await repository.GetBranchAsync(branchId, ct);
        return branch is { IsActive: true } ? branch : throw new RequestValidationException("Branch is invalid or inactive.");
    }

    private async Task<Customer> RequiredCustomer(Guid customerId, CancellationToken ct)
    {
        var customer = await repository.GetCustomerAsync(customerId, ct) ?? throw new RequestValidationException("Customer is invalid.");
        return customer is { IsActive: true } ? customer : throw new RequestValidationException("Customer is inactive.");
    }

    private async Task<SalesOrder> RequiredOrder(Guid id, CancellationToken ct) =>
        await repository.GetOrderForUpdateAsync(id, ct) ?? throw new ResourceNotFoundException("Sales order was not found.");

    private async Task<SalesOrderDetailsDto> Details(Guid id, CancellationToken ct) =>
        await repository.GetOrderDetailsAsync(id, ct) ?? throw new ResourceNotFoundException("Sales order was not found.");

    private static void ValidateHeader(SalesOrderRequest request)
    {
        if (request.Items.Count == 0) throw new RequestValidationException("At least one item is required.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private Task Audit(Guid actor, string action, Guid id, object? current, CancellationToken ct) => repository.AddAuditAsync(new AuditLog
    { UserId = actor, Action = action, EntityType = "SalesOrder", EntityId = id, NewValues = current is null ? null : JsonSerializer.Serialize(current) }, ct);
    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
    }
}
