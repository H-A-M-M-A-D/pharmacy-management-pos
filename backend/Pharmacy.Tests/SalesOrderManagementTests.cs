using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.SalesOrders;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Godowns;
using Pharmacy.Application.Services.Pricing;
using Pharmacy.Application.Services.Sales;
using Pharmacy.Application.Services.SalesOrders;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class SalesOrderManagementTests
{
    [Fact]
    public async Task Creating_an_order_resolves_price_per_line_and_computes_totals()
    {
        var f = new Fixture(PermissionCatalog.SalesOrdersCreate);
        f.PriceResolver.Resolve = (_, _, qty, _) => qty >= 10 ? new(9m, PriceSource.QuantityBreak, null, null) : new(null, PriceSource.Default, null, null);

        var order = await f.Service.CreateOrderAsync(f.Actor.Id, new(
            f.Branch.Id, f.Customer.Id, null, null, f.Today, null, "Bulk order", [new(f.Product.Id, 10)]));

        var item = Assert.Single(order.Items);
        Assert.Equal(9m, item.UnitPrice);
        Assert.Equal(90, order.NetTotal);
        Assert.Equal(SalesOrderStatus.Draft, order.Status);
    }

    [Fact]
    public async Task Duplicate_product_lines_on_one_order_are_rejected()
    {
        var f = new Fixture(PermissionCatalog.SalesOrdersCreate);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateOrderAsync(f.Actor.Id, new(
            f.Branch.Id, f.Customer.Id, null, null, f.Today, null, null, [new(f.Product.Id, 1), new(f.Product.Id, 2)])));
    }

    [Fact]
    public async Task Only_draft_orders_can_be_edited_or_confirmed()
    {
        var f = new Fixture(PermissionCatalog.SalesOrdersCreate, PermissionCatalog.SalesOrdersUpdate, PermissionCatalog.SalesOrdersConfirm);
        var order = await f.Service.CreateOrderAsync(f.Actor.Id, new(f.Branch.Id, f.Customer.Id, null, null, f.Today, null, null, [new(f.Product.Id, 1)]));
        await f.Service.ConfirmOrderAsync(f.Actor.Id, order.Id);

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.UpdateOrderAsync(f.Actor.Id, order.Id, new(
            f.Branch.Id, f.Customer.Id, null, null, f.Today, null, null, [new(f.Product.Id, 2)])));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ConfirmOrderAsync(f.Actor.Id, order.Id));
    }

    [Fact]
    public async Task Confirming_an_order_with_no_items_is_rejected()
    {
        var f = new Fixture(PermissionCatalog.SalesOrdersCreate, PermissionCatalog.SalesOrdersConfirm, PermissionCatalog.SalesOrdersUpdate);
        var order = await f.Service.CreateOrderAsync(f.Actor.Id, new(f.Branch.Id, f.Customer.Id, null, null, f.Today, null, null, [new(f.Product.Id, 1)]));
        f.Orders.Single().Items.Clear();
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ConfirmOrderAsync(f.Actor.Id, order.Id));
    }

    [Fact]
    public async Task Cancelling_a_fulfilled_order_is_rejected_and_reason_is_required()
    {
        var f = new Fixture(PermissionCatalog.SalesOrdersCreate, PermissionCatalog.SalesOrdersCancel);
        var order = await f.Service.CreateOrderAsync(f.Actor.Id, new(f.Branch.Id, f.Customer.Id, null, null, f.Today, null, null, [new(f.Product.Id, 1)]));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CancelOrderAsync(f.Actor.Id, order.Id, new("")));

        f.Orders.Single().Status = SalesOrderStatus.Fulfilled;
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CancelOrderAsync(f.Actor.Id, order.Id, new("changed my mind")));
    }

    [Fact]
    public async Task Fulfilling_an_order_delegates_to_sales_service_with_wholesale_type_and_order_context()
    {
        var f = new Fixture(PermissionCatalog.SalesOrdersCreate, PermissionCatalog.SalesOrdersConfirm, PermissionCatalog.SalesOrdersFulfill);
        var order = await f.Service.CreateOrderAsync(f.Actor.Id, new(f.Branch.Id, f.Customer.Id, f.Godown.Id, null, f.Today, null, null, [new(f.Product.Id, 5)]));
        await f.Service.ConfirmOrderAsync(f.Actor.Id, order.Id);

        await f.Service.FulfillOrderAsync(f.Actor.Id, order.Id, new(
            [new(f.Product.Id, 5)], [new(SalePaymentMethod.Cash, 60, 60)], "PO-99", null, null));

        Assert.NotNull(f.SalesService.LastRequest);
        Assert.Equal(SaleType.Wholesale, f.SalesService.LastRequest!.SaleType);
        Assert.Equal(order.Id, f.SalesService.LastRequest.SalesOrderId);
        Assert.Equal(f.Customer.Id, f.SalesService.LastRequest.CustomerId);
        Assert.Equal(f.Godown.Id, f.SalesService.LastRequest.GodownId);
        Assert.Equal("PO-99", f.SalesService.LastRequest.CustomerPoNumber);
    }

    [Fact]
    public async Task Fulfilling_a_draft_order_is_rejected()
    {
        var f = new Fixture(PermissionCatalog.SalesOrdersCreate, PermissionCatalog.SalesOrdersFulfill);
        var order = await f.Service.CreateOrderAsync(f.Actor.Id, new(f.Branch.Id, f.Customer.Id, null, null, f.Today, null, null, [new(f.Product.Id, 5)]));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.FulfillOrderAsync(f.Actor.Id, order.Id, new(
            [new(f.Product.Id, 5)], [new(SalePaymentMethod.Cash, 60, 60)])));
    }

    private sealed class Fixture : ISalesOrderRepository
    {
        public readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
        public readonly Branch Branch = new() { Code = "MAIN", Name = "Main", IsActive = true };
        public readonly Godown Godown;
        public readonly Product Product = new() { SKU = "SKU-1", NormalizedSku = "SKU-1", Name = "Panadol", Unit = "Tablet", PackSize = 1, PurchasePrice = 8, RetailPrice = 12, IsActive = true };
        public readonly Customer Customer = new() { CustomerCode = "CUS-000001", Name = "Ali Traders", NormalizedName = "ALI TRADERS", CreditLimit = 100000, IsActive = true };
        public readonly User Actor;
        public readonly List<SalesOrder> Orders = [];
        public readonly List<AuditLog> Audits = [];
        public readonly FakePriceResolutionService PriceResolver = new();
        public readonly FakeSalesService SalesService = new();
        public SalesOrderService Service { get; }

        public Fixture(params string[] permissions)
        {
            Godown = new Godown { BranchId = Branch.Id, Code = "MAIN", Name = "Main Godown", IsActive = true, IsDefault = true };
            var role = new Role { Name = RoleCatalog.Manager };
            foreach (var permission in permissions)
                role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "actor", NormalizedUsername = "ACTOR", FullName = "Actor", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role, IsActive = true };
            Service = new(this, new FakeGodownAccessService(Godown), PriceResolver, SalesService, TimeProvider.System);
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(Branch.Id == branchId ? Branch : null);
        public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(Product.Id == productId ? Product : null);
        public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<Customer?>(Customer.Id == customerId ? Customer : null);
        public Task<SalesOrder?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Orders.FirstOrDefault(x => x.Id == id));
        public Task<SalesOrder?> GetOrderForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Orders.FirstOrDefault(x => x.Id == id));
        public Task<string> NextOrderNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult($"SO-2026-{Orders.Count + 1:000000}");
        public Task AddOrderAsync(SalesOrder order, CancellationToken cancellationToken = default) { Orders.Add(order); return Task.CompletedTask; }
        public void ReplaceOrderItems(SalesOrder order, List<SalesOrderItem> items) => order.Items = items;
        public Task<IReadOnlyList<LinkedSaleDto>> GetLinkedSalesAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LinkedSaleDto>>([]);
        public Task<SalesOrderDetailsDto?> GetOrderDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var order = Orders.FirstOrDefault(x => x.Id == id);
            if (order is null) return Task.FromResult<SalesOrderDetailsDto?>(null);
            var items = order.Items.Select(x => new SalesOrderItemDto(x.Id, x.ProductId, Product.Name, Product.SKU, x.OrderedQuantity, x.FulfilledQuantity, x.UnitPrice, x.DiscountPercent, x.GrossAmount, x.DiscountAmount, x.NetAmount)).ToList();
            return Task.FromResult<SalesOrderDetailsDto?>(new(order.Id, order.OrderNumber, order.BranchId, Branch.Name, order.GodownId, Godown.Name, order.CustomerId, Customer.CustomerCode, Customer.Name,
                order.PriceLevelId, null, order.QuotationId, null, order.OrderDate, order.ExpectedDeliveryDate, order.Status, order.Notes, order.Subtotal, order.DiscountTotal, order.NetTotal,
                order.CreatedByUserId, Actor.FullName, order.ConfirmedByUserId, order.ConfirmedByUserId.HasValue ? Actor.FullName : null, order.ConfirmedAtUtc, order.CancelledAtUtc, order.CancellationReason, order.CreatedAt, items, []));
        }
        public Task<PagedResult<SalesOrderListItemDto>> ListOrdersAsync(SalesOrderListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<SalesOrderListItemDto>([], query.Page, query.PageSize, 0));
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => await operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeGodownAccessService(Godown godown) : IGodownAccessService
    {
        public Task<Godown?> GetGodownAsync(Guid godownId, CancellationToken cancellationToken = default) => Task.FromResult<Godown?>(godownId == godown.Id ? godown : null);
        public Task<Guid?> GetDefaultGodownIdAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
        public Task<bool> UserHasAccessAsync(Guid userId, Guid godownId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakePriceResolutionService : IPriceResolutionService
    {
        public Func<Guid?, Guid, int, Guid?, PriceResolutionResult> Resolve = (_, _, _, _) => new(null, PriceSource.Default, null, null);
        public Task<PriceResolutionResult> ResolveAsync(Guid? customerId, Guid productId, int quantity, Guid? explicitPriceLevelId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Resolve(customerId, productId, quantity, explicitPriceLevelId));
    }

    /// <summary>Records the request it was asked to post rather than exercising real FEFO/pricing/journal
    /// logic - SalesOrderService's own orchestration (permission checks, status transitions, request
    /// building) is what this file tests; SalesServiceTests.cs covers the deeper posting behaviour.</summary>
    private sealed class FakeSalesService : ISalesService
    {
        public PostSaleRequest? LastRequest;
        public Task<SaleDetailsDto> PostSaleAsync(Guid actorId, PostSaleRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new SaleDetailsDto(Guid.NewGuid(), "INV-2026-000001", null, SaleStatus.Posted, DateTime.UtcNow, DateTime.UtcNow,
                request.BranchId ?? Guid.Empty, "Main", null, null, Guid.NewGuid(), "Actor", request.CustomerId, null, request.CustomerName, request.CustomerPhone,
                60, 0, 0, 60, 60, 0, 0, request.Notes, [], []));
        }
        public Task<IReadOnlyList<PosProductDto>> SearchProductsAsync(Guid actorId, PosProductSearchQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SaleDetailsDto> HoldSaleAsync(Guid actorId, HoldSaleRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SaleDetailsDto> UpdateHeldSaleAsync(Guid actorId, Guid id, HoldSaleRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<SaleListItemDto>> ListHeldSalesAsync(Guid actorId, HeldSalesQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SaleDetailsDto> GetSaleAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CancelHeldSaleAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SaleDetailsDto> PostHeldSaleAsync(Guid actorId, Guid id, PostHeldSaleRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<SaleListItemDto>> ListSalesAsync(Guid actorId, SalesHistoryQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ReceiptDto> ReceiptAsync(Guid actorId, Guid id, bool auditReprint, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
