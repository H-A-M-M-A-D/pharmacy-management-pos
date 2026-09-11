using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Quotations;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.SalesOrders;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Godowns;
using Pharmacy.Application.Services.Pricing;
using Pharmacy.Application.Services.Quotations;
using Pharmacy.Application.Services.Sales;
using Pharmacy.Application.Services.SalesOrders;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class SalesQuotationManagementTests
{
    [Fact]
    public async Task Creating_a_quotation_resolves_and_snapshots_price_per_line()
    {
        var f = new Fixture(PermissionCatalog.QuotationsCreate);
        f.PriceResolver.Resolve = (_, _, _, _) => new(11m, PriceSource.PriceLevel, null, null);

        var quotation = await f.Service.CreateQuotationAsync(f.Actor.Id, new(
            f.Branch.Id, f.Customer.Id, null, null, f.Today, f.Today.AddDays(7), "Draft quote", [new(f.Product.Id, 4)]));

        var item = Assert.Single(quotation.Items);
        Assert.Equal(11m, item.UnitPrice);
        Assert.Equal(44, quotation.NetTotal);
        Assert.Equal(SalesQuotationStatus.Draft, quotation.Status);
    }

    [Fact]
    public async Task Only_draft_quotations_can_be_edited()
    {
        var f = new Fixture(PermissionCatalog.QuotationsCreate, PermissionCatalog.QuotationsUpdate, PermissionCatalog.QuotationsSend);
        var quotation = await f.Service.CreateQuotationAsync(f.Actor.Id, new(f.Branch.Id, f.Customer.Id, null, null, f.Today, null, null, [new(f.Product.Id, 1)]));
        await f.Service.SendQuotationAsync(f.Actor.Id, quotation.Id);

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.UpdateQuotationAsync(f.Actor.Id, quotation.Id, new(
            f.Branch.Id, f.Customer.Id, null, null, f.Today, null, null, [new(f.Product.Id, 2)])));
    }

    [Fact]
    public async Task Accepting_then_rejecting_the_same_quotation_is_rejected()
    {
        var f = new Fixture(PermissionCatalog.QuotationsCreate, PermissionCatalog.QuotationsAccept);
        var quotation = await f.Service.CreateQuotationAsync(f.Actor.Id, new(f.Branch.Id, f.Customer.Id, null, null, f.Today, null, null, [new(f.Product.Id, 1)]));
        var accepted = await f.Service.AcceptQuotationAsync(f.Actor.Id, quotation.Id);
        Assert.Equal(SalesQuotationStatus.Accepted, accepted.Status);

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.RejectQuotationAsync(f.Actor.Id, quotation.Id, new("changed mind")));
    }

    [Fact]
    public async Task An_expired_quotation_cannot_be_sent_or_accepted_and_transitions_automatically()
    {
        var f = new Fixture(PermissionCatalog.QuotationsCreate, PermissionCatalog.QuotationsSend, PermissionCatalog.QuotationsAccept);
        var quotation = await f.Service.CreateQuotationAsync(f.Actor.Id, new(
            f.Branch.Id, f.Customer.Id, null, null, f.Today.AddDays(-10), f.Today.AddDays(-1), null, [new(f.Product.Id, 1)]));

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.SendQuotationAsync(f.Actor.Id, quotation.Id));
        Assert.Equal(SalesQuotationStatus.Expired, f.Quotations.Single().Status);

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.AcceptQuotationAsync(f.Actor.Id, quotation.Id));
    }

    [Fact]
    public async Task Cancelling_a_converted_quotation_is_rejected_and_reason_is_required()
    {
        var f = new Fixture(PermissionCatalog.QuotationsCreate, PermissionCatalog.QuotationsCancel);
        var quotation = await f.Service.CreateQuotationAsync(f.Actor.Id, new(f.Branch.Id, f.Customer.Id, null, null, f.Today, null, null, [new(f.Product.Id, 1)]));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CancelQuotationAsync(f.Actor.Id, quotation.Id, new("")));

        f.Quotations.Single().Status = SalesQuotationStatus.Converted;
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CancelQuotationAsync(f.Actor.Id, quotation.Id, new("no longer needed")));
    }

    [Fact]
    public async Task Converting_to_a_sales_order_copies_the_quoted_price_verbatim_and_cannot_repeat()
    {
        var f = new Fixture(PermissionCatalog.QuotationsCreate, PermissionCatalog.QuotationsAccept, PermissionCatalog.QuotationsConvert);
        f.PriceResolver.Resolve = (_, _, _, _) => new(11m, PriceSource.PriceLevel, null, null);
        var quotation = await f.Service.CreateQuotationAsync(f.Actor.Id, new(f.Branch.Id, f.Customer.Id, null, null, f.Today, null, null, [new(f.Product.Id, 4)]));
        await f.Service.AcceptQuotationAsync(f.Actor.Id, quotation.Id);

        // A price change after quoting must not leak into the resulting order.
        f.PriceResolver.Resolve = (_, _, _, _) => new(999m, PriceSource.PriceLevel, null, null);
        var order = await f.Service.ConvertToSalesOrderAsync(f.Actor.Id, quotation.Id, new(null));

        var item = Assert.Single(order.Items);
        Assert.Equal(11m, item.UnitPrice);
        Assert.Equal(SalesQuotationStatus.Converted, f.Quotations.Single().Status);
        Assert.Equal(order.Id, f.Quotations.Single().ConvertedToSalesOrderId);

        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.ConvertToSalesOrderAsync(f.Actor.Id, quotation.Id, new(null)));
    }

    [Fact]
    public async Task Converting_directly_to_a_sale_delegates_with_wholesale_type_and_quotation_context()
    {
        var f = new Fixture(PermissionCatalog.QuotationsCreate, PermissionCatalog.QuotationsAccept, PermissionCatalog.QuotationsConvert);
        var quotation = await f.Service.CreateQuotationAsync(f.Actor.Id, new(f.Branch.Id, f.Customer.Id, null, null, f.Today, null, null, [new(f.Product.Id, 2)]));
        await f.Service.AcceptQuotationAsync(f.Actor.Id, quotation.Id);

        await f.Service.ConvertToSaleAsync(f.Actor.Id, quotation.Id, [new(SalePaymentMethod.Cash, 24, 24)]);

        Assert.NotNull(f.SalesService.LastRequest);
        Assert.Equal(SaleType.Wholesale, f.SalesService.LastRequest!.SaleType);
        Assert.Equal(quotation.Id, f.SalesService.LastRequest.QuotationId);
        Assert.Equal(f.Customer.Id, f.SalesService.LastRequest.CustomerId);
    }

    private sealed class Fixture : ISalesQuotationRepository
    {
        public readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
        public readonly Branch Branch = new() { Code = "MAIN", Name = "Main", IsActive = true };
        public readonly Product Product = new() { SKU = "SKU-1", NormalizedSku = "SKU-1", Name = "Panadol", Unit = "Tablet", PackSize = 1, PurchasePrice = 8, RetailPrice = 12, IsActive = true };
        public readonly Customer Customer = new() { CustomerCode = "CUS-000001", Name = "Ali Traders", NormalizedName = "ALI TRADERS", CreditLimit = 100000, IsActive = true };
        public readonly User Actor;
        public readonly List<SalesQuotation> Quotations = [];
        public readonly List<AuditLog> Audits = [];
        public readonly FakePriceResolutionService PriceResolver = new();
        public readonly FakeSalesOrderRepository SalesOrders = new();
        public readonly FakeSalesService SalesService = new();
        public SalesQuotationService Service { get; }

        public Fixture(params string[] permissions)
        {
            var role = new Role { Name = RoleCatalog.Manager };
            foreach (var permission in permissions)
                role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "actor", NormalizedUsername = "ACTOR", FullName = "Actor", PasswordHash = "hash", BranchId = Branch.Id, RoleId = role.Id, Role = role, IsActive = true };
            Service = new(this, SalesOrders, new FakeGodownAccessService(), PriceResolver, SalesService, TimeProvider.System);
        }

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Branch?>(Branch.Id == branchId ? Branch : null);
        public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(Product.Id == productId ? Product : null);
        public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<Customer?>(Customer.Id == customerId ? Customer : null);
        public Task<SalesQuotation?> GetQuotationAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Quotations.FirstOrDefault(x => x.Id == id));
        public Task<SalesQuotation?> GetQuotationForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Quotations.FirstOrDefault(x => x.Id == id));
        public Task<string> NextQuotationNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult($"QT-2026-{Quotations.Count + 1:000000}");
        public Task AddQuotationAsync(SalesQuotation quotation, CancellationToken cancellationToken = default) { Quotations.Add(quotation); return Task.CompletedTask; }
        public void ReplaceQuotationItems(SalesQuotation quotation, List<SalesQuotationItem> items) => quotation.Items = items;
        public Task<QuotationDetailsDto?> GetQuotationDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var quotation = Quotations.FirstOrDefault(x => x.Id == id);
            if (quotation is null) return Task.FromResult<QuotationDetailsDto?>(null);
            var items = quotation.Items.Select(x => new QuotationItemDto(x.Id, x.ProductId, Product.Name, Product.SKU, x.Quantity, x.UnitPrice, x.DiscountPercent, x.GrossAmount, x.DiscountAmount, x.NetAmount)).ToList();
            return Task.FromResult<QuotationDetailsDto?>(new(quotation.Id, quotation.QuotationNumber, quotation.BranchId, Branch.Name, quotation.GodownId, null, quotation.CustomerId, Customer.CustomerCode, Customer.Name,
                quotation.PriceLevelId, null, quotation.QuotationDate, quotation.ValidUntil, quotation.Status, quotation.Notes, quotation.Subtotal, quotation.DiscountTotal, quotation.NetTotal,
                quotation.CreatedByUserId, Actor.FullName, quotation.ApprovedByUserId, quotation.ApprovedByUserId.HasValue ? Actor.FullName : null,
                quotation.ConvertedToSalesOrderId, null, quotation.ConvertedToSaleId, null,
                quotation.SentAtUtc, quotation.RespondedAtUtc, quotation.CancelledAtUtc, quotation.CancellationReason, quotation.CreatedAt, items));
        }
        public Task<PagedResult<QuotationListItemDto>> ListQuotationsAsync(QuotationListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<QuotationListItemDto>([], query.Page, query.PageSize, 0));
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }
        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => await operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeGodownAccessService : IGodownAccessService
    {
        public Task<Godown?> GetGodownAsync(Guid godownId, CancellationToken cancellationToken = default) => Task.FromResult<Godown?>(null);
        public Task<Guid?> GetDefaultGodownIdAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
        public Task<bool> UserHasAccessAsync(Guid userId, Guid godownId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakePriceResolutionService : IPriceResolutionService
    {
        public Func<Guid?, Guid, int, Guid?, PriceResolutionResult> Resolve = (_, _, _, _) => new(null, PriceSource.Default, null, null);
        public Task<PriceResolutionResult> ResolveAsync(Guid? customerId, Guid productId, int quantity, Guid? explicitPriceLevelId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Resolve(customerId, productId, quantity, explicitPriceLevelId));
    }

    private sealed class FakeSalesOrderRepository : ISalesOrderRepository
    {
        public readonly List<SalesOrder> Orders = [];
        public Task<SalesOrder?> GetOrderForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Orders.FirstOrDefault(x => x.Id == id));
        public Task<string> NextOrderNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult($"SO-2026-{Orders.Count + 1:000000}");
        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SalesOrder?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddOrderAsync(SalesOrder order, CancellationToken cancellationToken = default) { Orders.Add(order); return Task.CompletedTask; }
        public void ReplaceOrderItems(SalesOrder order, List<SalesOrderItem> items) => throw new NotImplementedException();
        public Task<IReadOnlyList<LinkedSaleDto>> GetLinkedSalesAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LinkedSaleDto>>([]);
        public Task<SalesOrderDetailsDto?> GetOrderDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var order = Orders.FirstOrDefault(x => x.Id == id);
            if (order is null) return Task.FromResult<SalesOrderDetailsDto?>(null);
            var items = order.Items.Select(x => new SalesOrderItemDto(x.Id, x.ProductId, "Panadol", "SKU-1", x.OrderedQuantity, x.FulfilledQuantity, x.UnitPrice, x.DiscountPercent, x.GrossAmount, x.DiscountAmount, x.NetAmount)).ToList();
            return Task.FromResult<SalesOrderDetailsDto?>(new(order.Id, order.OrderNumber, order.BranchId, "Main", order.GodownId, null, order.CustomerId, "CUS-000001", "Ali Traders",
                order.PriceLevelId, null, order.QuotationId, null, order.OrderDate, order.ExpectedDeliveryDate, order.Status, order.Notes, order.Subtotal, order.DiscountTotal, order.NetTotal,
                order.CreatedByUserId, "Actor", null, null, null, null, null, order.CreatedAt, items, []));
        }
        public Task<PagedResult<SalesOrderListItemDto>> ListOrdersAsync(SalesOrderListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSalesService : ISalesService
    {
        public PostSaleRequest? LastRequest;
        public Task<SaleDetailsDto> PostSaleAsync(Guid actorId, PostSaleRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new SaleDetailsDto(Guid.NewGuid(), "INV-2026-000001", null, SaleStatus.Posted, DateTime.UtcNow, DateTime.UtcNow,
                request.BranchId ?? Guid.Empty, "Main", null, null, Guid.NewGuid(), "Actor", request.CustomerId, null, request.CustomerName, request.CustomerPhone,
                24, 0, 0, 24, 24, 0, 0, request.Notes, [], []));
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
