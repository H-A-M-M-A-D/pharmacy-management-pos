using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Quotations;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.SalesOrders;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Godowns;
using Pharmacy.Application.Services.Pricing;
using Pharmacy.Application.Services.Sales;
using Pharmacy.Application.Services.SalesOrders;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Quotations;

/// <summary>
/// Draft -&gt; Sent -&gt; Accepted/Rejected/Expired, with Cancel available at any non-terminal status and
/// Converted as the terminal success path. Never touches stock or the journal. Converting to a
/// SalesOrder copies each line's snapshot (UnitPrice/DiscountPercent/amounts) verbatim - it is
/// written directly via ISalesOrderRepository rather than SalesOrderService.CreateOrderAsync so the
/// quoted price is never silently re-resolved. Converting directly to a Sale instead delegates to
/// ISalesService.PostSaleAsync (SaleType.Wholesale, QuotationId set), which re-validates quotation
/// state itself and performs the Converted transition atomically inside its own transaction - see
/// SalesService.BuildPostedSale for the other half of that flow.
/// </summary>
public sealed class SalesQuotationService(
    ISalesQuotationRepository repository,
    ISalesOrderRepository salesOrderRepository,
    IGodownAccessService godownAccess,
    IPriceResolutionService priceResolver,
    ISalesService salesService,
    TimeProvider timeProvider) : ISalesQuotationService
{
    public async Task<PagedResult<QuotationListItemDto>> ListQuotationsAsync(Guid actorId, QuotationListQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.QuotationsView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        var scope = Scope(actor, query.BranchId);
        return await repository.ListQuotationsAsync(query with { BranchId = scope.BranchId }, actor.BranchId, scope.CanSelectBranch, cancellationToken);
    }

    public async Task<QuotationDetailsDto> GetQuotationAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        await Require(actorId, PermissionCatalog.QuotationsView, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<QuotationDetailsDto> CreateQuotationAsync(Guid actorId, QuotationRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.QuotationsCreate, cancellationToken);
        if (request.Items.Count == 0) throw new RequestValidationException("At least one item is required.");
        var branchId = request.BranchId ?? actor.BranchId;
        EnsureBranchAccess(actor, branchId);
        SalesQuotation? quotation = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var branch = await RequiredActiveBranch(branchId, ct);
            var customer = await RequiredCustomer(request.CustomerId, ct);
            if (request.GodownId.HasValue) await EnsureGodownAccessAsync(actor, branch.Id, request.GodownId.Value, ct);
            quotation = new SalesQuotation
            {
                QuotationNumber = await repository.NextQuotationNumberAsync(ct),
                BranchId = branch.Id,
                GodownId = request.GodownId,
                CustomerId = customer.Id,
                PriceLevelId = request.PriceLevelId ?? customer.PriceLevelId,
                QuotationDate = request.QuotationDate,
                ValidUntil = request.ValidUntil,
                Status = SalesQuotationStatus.Draft,
                Notes = Clean(request.Notes),
                CreatedByUserId = actorId,
                Items = await BuildItemsAsync(actor, customer.Id, request.PriceLevelId ?? customer.PriceLevelId, request.Items, ct)
            };
            Totals(quotation);
            await repository.AddQuotationAsync(quotation, ct);
            await Audit(actorId, "QuotationCreated", quotation.Id, new { quotation.QuotationNumber, quotation.CustomerId, ItemCount = quotation.Items.Count, quotation.NetTotal }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(quotation!.Id, cancellationToken);
    }

    public async Task<QuotationDetailsDto> UpdateQuotationAsync(Guid actorId, Guid id, QuotationRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.QuotationsUpdate, cancellationToken);
        if (request.Items.Count == 0) throw new RequestValidationException("At least one item is required.");
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var quotation = await RequiredQuotation(id, ct);
            EnsureBranchAccess(actor, quotation.BranchId);
            if (quotation.Status != SalesQuotationStatus.Draft) throw new RequestValidationException("Only draft quotations can be edited.");
            var branchId = request.BranchId ?? quotation.BranchId;
            EnsureBranchAccess(actor, branchId);
            var branch = await RequiredActiveBranch(branchId, ct);
            var customer = await RequiredCustomer(request.CustomerId, ct);
            if (request.GodownId.HasValue) await EnsureGodownAccessAsync(actor, branch.Id, request.GodownId.Value, ct);
            quotation.BranchId = branch.Id;
            quotation.GodownId = request.GodownId;
            quotation.CustomerId = customer.Id;
            quotation.PriceLevelId = request.PriceLevelId ?? customer.PriceLevelId;
            quotation.QuotationDate = request.QuotationDate;
            quotation.ValidUntil = request.ValidUntil;
            quotation.Notes = Clean(request.Notes);
            quotation.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            var items = await BuildItemsAsync(actor, customer.Id, quotation.PriceLevelId, request.Items, ct);
            repository.ReplaceQuotationItems(quotation, items);
            Totals(quotation);
            await Audit(actorId, "QuotationUpdated", quotation.Id, new { quotation.QuotationNumber, ItemCount = items.Count, quotation.NetTotal }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<QuotationDetailsDto> SendQuotationAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.QuotationsSend, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var quotation = await RequiredQuotation(id, ct);
            EnsureBranchAccess(actor, quotation.BranchId);
            await ExpireIfDueAsync(quotation, ct);
            if (quotation.Status != SalesQuotationStatus.Draft) throw new RequestValidationException("Only draft quotations can be sent.");
            if (quotation.Items.Count == 0) throw new RequestValidationException("At least one item is required before sending a quotation.");
            quotation.Status = SalesQuotationStatus.Sent;
            quotation.SentAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            quotation.UpdatedAt = quotation.SentAtUtc.Value;
            await Audit(actorId, "QuotationSent", quotation.Id, new { quotation.QuotationNumber }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<QuotationDetailsDto> AcceptQuotationAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.QuotationsAccept, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var quotation = await RequiredQuotation(id, ct);
            EnsureBranchAccess(actor, quotation.BranchId);
            await ExpireIfDueAsync(quotation, ct);
            if (quotation.Status is not (SalesQuotationStatus.Draft or SalesQuotationStatus.Sent))
                throw new RequestValidationException("Only draft or sent quotations can be accepted.");
            var now = timeProvider.GetUtcNow().UtcDateTime;
            quotation.Status = SalesQuotationStatus.Accepted;
            quotation.ApprovedByUserId = actorId;
            quotation.RespondedAtUtc = now;
            quotation.UpdatedAt = now;
            await Audit(actorId, "QuotationAccepted", quotation.Id, new { quotation.QuotationNumber }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<QuotationDetailsDto> RejectQuotationAsync(Guid actorId, Guid id, RejectQuotationRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.QuotationsAccept, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var quotation = await RequiredQuotation(id, ct);
            EnsureBranchAccess(actor, quotation.BranchId);
            await ExpireIfDueAsync(quotation, ct);
            if (quotation.Status is not (SalesQuotationStatus.Draft or SalesQuotationStatus.Sent))
                throw new RequestValidationException("Only draft or sent quotations can be rejected.");
            var now = timeProvider.GetUtcNow().UtcDateTime;
            quotation.Status = SalesQuotationStatus.Rejected;
            quotation.RespondedAtUtc = now;
            quotation.UpdatedAt = now;
            if (!string.IsNullOrWhiteSpace(request.Reason)) quotation.Notes = AppendNote(quotation.Notes, $"Rejected: {request.Reason.Trim()}");
            await Audit(actorId, "QuotationRejected", quotation.Id, new { quotation.QuotationNumber, request.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<QuotationDetailsDto> CancelQuotationAsync(Guid actorId, Guid id, CancelQuotationRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.QuotationsCancel, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("Cancellation reason is required.");
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var quotation = await RequiredQuotation(id, ct);
            EnsureBranchAccess(actor, quotation.BranchId);
            if (quotation.Status is SalesQuotationStatus.Converted or SalesQuotationStatus.Cancelled)
                throw new RequestValidationException("This quotation can no longer be cancelled.");
            var now = timeProvider.GetUtcNow().UtcDateTime;
            quotation.Status = SalesQuotationStatus.Cancelled;
            quotation.CancelledAtUtc = now;
            quotation.CancellationReason = request.Reason.Trim();
            quotation.UpdatedAt = now;
            await Audit(actorId, "QuotationCancelled", quotation.Id, new { quotation.QuotationNumber, request.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<SalesOrderDetailsDto> ConvertToSalesOrderAsync(Guid actorId, Guid id, ConvertQuotationToOrderRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.QuotationsConvert, cancellationToken);
        Guid orderId = Guid.Empty;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var quotation = await RequiredQuotation(id, ct);
            EnsureBranchAccess(actor, quotation.BranchId);
            await ExpireIfDueAsync(quotation, ct);
            if (quotation.ConvertedToSalesOrderId.HasValue || quotation.ConvertedToSaleId.HasValue)
                throw new ResourceConflictException("This quotation has already been converted.");
            if (quotation.Status != SalesQuotationStatus.Accepted) throw new RequestValidationException("Only accepted quotations can be converted.");
            var order = new SalesOrder
            {
                OrderNumber = await salesOrderRepository.NextOrderNumberAsync(ct),
                BranchId = quotation.BranchId,
                GodownId = quotation.GodownId,
                CustomerId = quotation.CustomerId,
                PriceLevelId = quotation.PriceLevelId,
                QuotationId = quotation.Id,
                OrderDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime),
                ExpectedDeliveryDate = request.ExpectedDeliveryDate,
                Status = SalesOrderStatus.Draft,
                Notes = quotation.Notes,
                CreatedByUserId = actorId,
                Subtotal = quotation.Subtotal,
                DiscountTotal = quotation.DiscountTotal,
                NetTotal = quotation.NetTotal,
                Items = quotation.Items.Select(x => new SalesOrderItem
                {
                    ProductId = x.ProductId,
                    OrderedQuantity = x.Quantity,
                    FulfilledQuantity = 0,
                    UnitPrice = x.UnitPrice,
                    DiscountPercent = x.DiscountPercent,
                    GrossAmount = x.GrossAmount,
                    DiscountAmount = x.DiscountAmount,
                    NetAmount = x.NetAmount
                }).ToList()
            };
            await salesOrderRepository.AddOrderAsync(order, ct);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            quotation.Status = SalesQuotationStatus.Converted;
            quotation.ConvertedToSalesOrderId = order.Id;
            quotation.UpdatedAt = now;
            await Audit(actorId, "QuotationConvertedToSalesOrder", quotation.Id, new { quotation.QuotationNumber, OrderNumber = order.OrderNumber }, ct);
            await repository.SaveChangesAsync(ct);
            orderId = order.Id;
        }, IsolationLevel.Serializable, cancellationToken);
        return await salesOrderRepository.GetOrderDetailsAsync(orderId, cancellationToken) ?? throw new ResourceNotFoundException("Sales order was not found.");
    }

    public async Task<SaleDetailsDto> ConvertToSaleAsync(Guid actorId, Guid id, IReadOnlyList<SalePaymentRequest> payments, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.QuotationsConvert, cancellationToken);
        var quotation = await repository.GetQuotationAsync(id, cancellationToken) ?? throw new ResourceNotFoundException("Quotation was not found.");
        EnsureBranchAccess(actor, quotation.BranchId);
        if (quotation.ConvertedToSalesOrderId.HasValue || quotation.ConvertedToSaleId.HasValue)
            throw new ResourceConflictException("This quotation has already been converted.");
        if (quotation.Status != SalesQuotationStatus.Accepted) throw new RequestValidationException("Only accepted quotations can be converted.");
        var lines = quotation.Items.Select(x => new SaleLineRequest(x.ProductId, x.Quantity)).ToList();
        return await salesService.PostSaleAsync(actorId, new PostSaleRequest(
            BranchId: quotation.BranchId, CustomerId: quotation.CustomerId, CustomerName: null, CustomerPhone: null, Notes: quotation.Notes,
            Items: lines, Payments: payments, GodownId: quotation.GodownId, SaleType: SaleType.Wholesale,
            PriceLevelId: quotation.PriceLevelId, QuotationId: quotation.Id, SalesOrderId: null), cancellationToken);
    }

    private async Task<List<SalesQuotationItem>> BuildItemsAsync(User actor, Guid customerId, Guid? priceLevelId, IReadOnlyList<QuotationLineRequest> items, CancellationToken ct)
    {
        var built = new List<SalesQuotationItem>();
        foreach (var line in items)
        {
            if (line.Quantity <= 0) throw new RequestValidationException("Quantity must be greater than zero.");
            if (line.DiscountPercent is < 0 or > 100) throw new RequestValidationException("Discount must be between 0 and 100.");
            var product = await repository.GetProductAsync(line.ProductId, ct);
            if (product is not { IsActive: true }) throw new RequestValidationException("Product is invalid or inactive.");

            decimal unitPrice;
            if (line.UnitPriceOverride.HasValue)
            {
                if (actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.SalesPriceOverride) != true)
                    throw new ForbiddenOperationException("The current user is not permitted to override the selling price.");
                unitPrice = Money(line.UnitPriceOverride.Value);
            }
            else
            {
                var resolved = await priceResolver.ResolveAsync(customerId, product.Id, line.Quantity, priceLevelId, cancellationToken: ct);
                unitPrice = resolved.Price ?? product.RetailPrice;
            }
            if (line.DiscountPercent > product.MaximumDiscountPercent
                && actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.SalesDiscountOverride) != true)
                throw new RequestValidationException("Discount exceeds the product maximum.");

            var gross = Money(unitPrice * line.Quantity);
            var discount = Money(gross * line.DiscountPercent / 100m);
            built.Add(new SalesQuotationItem
            {
                ProductId = product.Id,
                Quantity = line.Quantity,
                UnitPrice = unitPrice,
                DiscountPercent = line.DiscountPercent,
                GrossAmount = gross,
                DiscountAmount = discount,
                NetAmount = Money(gross - discount)
            });
        }
        return built;
    }

    private async Task ExpireIfDueAsync(SalesQuotation quotation, CancellationToken ct)
    {
        if (quotation.Status is SalesQuotationStatus.Draft or SalesQuotationStatus.Sent
            && quotation.ValidUntil.HasValue && quotation.ValidUntil.Value < BusinessDate())
        {
            quotation.Status = SalesQuotationStatus.Expired;
            quotation.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            await repository.SaveChangesAsync(ct);
            throw new RequestValidationException("This quotation has expired.");
        }
    }

    private static void Totals(SalesQuotation quotation)
    {
        quotation.Subtotal = Money(quotation.Items.Sum(x => x.GrossAmount));
        quotation.DiscountTotal = Money(quotation.Items.Sum(x => x.DiscountAmount));
        quotation.NetTotal = Money(quotation.Items.Sum(x => x.NetAmount));
    }

    private async Task<User> Require(Guid actorId, string permission, CancellationToken cancellationToken)
    {
        var actor = await repository.GetActorAsync(actorId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private static bool CanSelectBranch(User actor) =>
        actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.UsersView) == true;

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

    private async Task<SalesQuotation> RequiredQuotation(Guid id, CancellationToken ct) =>
        await repository.GetQuotationForUpdateAsync(id, ct) ?? throw new ResourceNotFoundException("Quotation was not found.");

    private async Task<QuotationDetailsDto> Details(Guid id, CancellationToken ct) =>
        await repository.GetQuotationDetailsAsync(id, ct) ?? throw new ResourceNotFoundException("Quotation was not found.");

    private DateOnly BusinessDate() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time")));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string AppendNote(string? existing, string addition) => string.IsNullOrWhiteSpace(existing) ? addition : $"{existing}\n{addition}";
    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private Task Audit(Guid actor, string action, Guid id, object? current, CancellationToken ct) => repository.AddAuditAsync(new AuditLog
    { UserId = actor, Action = action, EntityType = "SalesQuotation", EntityId = id, NewValues = current is null ? null : JsonSerializer.Serialize(current) }, ct);
    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
    }
}
