using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Inventory;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Sales;

public sealed class SalesService(ISalesRepository repository, IFefoAllocationService fefo, TimeProvider timeProvider) : ISalesService
{
    public async Task<IReadOnlyList<PosProductDto>> SearchProductsAsync(Guid actorId, PosProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesCreate, cancellationToken);
        var branchId = query.BranchId ?? actor.BranchId;
        EnsureBranchAccess(actor, branchId);
        if (query.Take is < 1 or > 50) throw new RequestValidationException("Search size must be between 1 and 50.");
        return await repository.SearchProductsAsync(query with { BranchId = branchId }, actor.BranchId, CanSelectBranch(actor), BusinessDate(), cancellationToken);
    }

    public async Task<SaleDetailsDto> HoldSaleAsync(Guid actorId, HoldSaleRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesHold, cancellationToken);
        var branchId = request.BranchId ?? actor.BranchId;
        EnsureBranchAccess(actor, branchId);
        ValidateLines(request.Items, actor);
        Sale? sale = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var branch = await RequireActiveBranch(branchId, ct);
            sale = new Sale
            {
                BranchId = branch.Id,
                CashierUserId = actorId,
                HoldNumber = await repository.NextHoldNumberAsync(UtcNow(), ct),
                Status = SaleStatus.Held,
                CustomerName = Clean(request.CustomerName),
                CustomerPhone = Clean(request.CustomerPhone),
                Notes = Clean(request.Notes)
            };
            foreach (var line in request.Items)
            {
                var product = await RequireActiveProduct(line.ProductId, ct);
                ValidateDiscount(actor, product, line.DiscountPercent);
                sale.Items.Add(new SaleItem { ProductId = product.Id, RequestedQuantity = line.Quantity, DiscountPercent = line.DiscountPercent });
            }
            await repository.AddSaleAsync(sale, ct);
            await Audit(actorId, "SaleHeld", "Sale", sale.Id, null, new { sale.HoldNumber, sale.BranchId, ItemCount = sale.Items.Count }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(actor, sale!.Id, cancellationToken);
    }

    public async Task<SaleDetailsDto> UpdateHeldSaleAsync(Guid actorId, Guid id, HoldSaleRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesHold, cancellationToken);
        ValidateLines(request.Items, actor);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var sale = await RequiredSale(id, ct);
            EnsureBranchAccess(actor, sale.BranchId);
            if (sale.Status != SaleStatus.Held) throw new RequestValidationException("Only held sales can be edited.");
            var branchId = request.BranchId ?? sale.BranchId;
            EnsureBranchAccess(actor, branchId);
            await RequireActiveBranch(branchId, ct);
            sale.BranchId = branchId;
            sale.CustomerName = Clean(request.CustomerName);
            sale.CustomerPhone = Clean(request.CustomerPhone);
            sale.Notes = Clean(request.Notes);
            sale.Items.Clear();
            foreach (var line in request.Items)
            {
                var product = await RequireActiveProduct(line.ProductId, ct);
                ValidateDiscount(actor, product, line.DiscountPercent);
                sale.Items.Add(new SaleItem { ProductId = product.Id, RequestedQuantity = line.Quantity, DiscountPercent = line.DiscountPercent });
            }
            sale.UpdatedAt = UtcNow();
            await Audit(actorId, "SaleHeldUpdated", "Sale", sale.Id, null, new { sale.HoldNumber, sale.BranchId, ItemCount = sale.Items.Count }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(actor, id, cancellationToken);
    }

    public async Task<PagedResult<SaleListItemDto>> ListHeldSalesAsync(Guid actorId, HeldSalesQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesHold, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        var scope = Scope(actor, query.BranchId);
        return await repository.ListHeldSalesAsync(query with { BranchId = scope.BranchId }, actor.BranchId, scope.CanSelectBranch, cancellationToken);
    }

    public async Task<SaleDetailsDto> GetSaleAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesView, cancellationToken);
        return await Details(actor, id, cancellationToken);
    }

    public async Task CancelHeldSaleAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesHold, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var sale = await RequiredSale(id, ct);
            EnsureBranchAccess(actor, sale.BranchId);
            if (sale.Status != SaleStatus.Held) throw new RequestValidationException("Only held sales can be cancelled.");
            sale.Status = SaleStatus.Cancelled;
            sale.UpdatedAt = UtcNow();
            await Audit(actorId, "HeldSaleCancelled", "Sale", sale.Id, null, new { sale.HoldNumber, sale.Status }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
    }

    public async Task<SaleDetailsDto> PostSaleAsync(Guid actorId, PostSaleRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesCreate, cancellationToken);
        var branchId = request.BranchId ?? actor.BranchId;
        EnsureBranchAccess(actor, branchId);
        ValidateLines(request.Items, actor);
        ValidatePaymentsShape(request.Payments);
        Sale? sale = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            sale = await BuildPostedSale(actor, branchId, request.CustomerId, request.CustomerName, request.CustomerPhone, request.Notes, request.Items, request.Payments, ct);
            await repository.AddSaleAsync(sale, ct);
            await Audit(actorId, "SalePosted", "Sale", sale.Id, null, AuditValues(sale), ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(actor, sale!.Id, cancellationToken);
    }

    public async Task<SaleDetailsDto> PostHeldSaleAsync(Guid actorId, Guid id, PostHeldSaleRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesCreate, cancellationToken);
        ValidatePaymentsShape(request.Payments);
        Guid postedId = id;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var held = await RequiredSale(id, ct);
            EnsureBranchAccess(actor, held.BranchId);
            if (held.Status != SaleStatus.Held) throw new RequestValidationException("Only held sales can be posted.");
            var lines = held.Items.Select(x => new SaleLineRequest(x.ProductId, x.RequestedQuantity, x.DiscountPercent)).ToList();
            var posted = await BuildPostedSale(actor, held.BranchId, request.CustomerId, held.CustomerName, held.CustomerPhone, held.Notes, lines, request.Payments, ct);
            held.Status = SaleStatus.Cancelled;
            held.UpdatedAt = UtcNow();
            await repository.AddSaleAsync(posted, ct);
            postedId = posted.Id;
            await Audit(actorId, "SalePosted", "Sale", posted.Id, null, AuditValues(posted), ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(actor, postedId, cancellationToken);
    }

    public async Task<PagedResult<SaleListItemDto>> ListSalesAsync(Guid actorId, SalesHistoryQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        var scope = Scope(actor, query.BranchId);
        return await repository.ListSalesAsync(query with { BranchId = scope.BranchId }, actor.BranchId, scope.CanSelectBranch, cancellationToken);
    }

    public async Task<ReceiptDto> ReceiptAsync(Guid actorId, Guid id, bool auditReprint, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, auditReprint ? PermissionCatalog.SalesReprint : PermissionCatalog.SalesView, cancellationToken);
        var sale = await Details(actor, id, cancellationToken);
        if (sale.Status != SaleStatus.Posted || sale.InvoiceNumber is null || sale.PostedAtUtc is null) throw new RequestValidationException("Only posted sales have receipts.");
        if (auditReprint)
        {
            await repository.ExecuteInTransactionAsync(async ct =>
            {
                await Audit(actorId, "ReceiptReprinted", "Sale", sale.Id, null, new { sale.InvoiceNumber }, ct);
                await repository.SaveChangesAsync(ct);
            }, IsolationLevel.ReadCommitted, cancellationToken);
        }
        return new ReceiptDto(sale.InvoiceNumber, sale.BranchName, sale.BranchAddress, sale.BranchPhone, sale.PostedAtUtc.Value, sale.CashierName, sale.CustomerName, sale.Subtotal, sale.DiscountTotal, sale.TaxTotal, sale.NetTotal, sale.AmountPaid, sale.CreditAmount, sale.ChangeGiven, sale.Items, sale.Payments);
    }

    private async Task<Sale> BuildPostedSale(User actor, Guid branchId, Guid? customerId, string? customerName, string? customerPhone, string? notes, IReadOnlyList<SaleLineRequest> lines, IReadOnlyList<SalePaymentRequest> payments, CancellationToken ct)
    {
        var branch = await RequireActiveBranch(branchId, ct);
        var now = UtcNow();
        var sale = new Sale
        {
            BranchId = branch.Id,
            CashierUserId = actor.Id,
            InvoiceNumber = await repository.NextInvoiceNumberAsync(now, ct),
            Status = SaleStatus.Posted,
            PostedAtUtc = now,
            CustomerId = customerId,
            CustomerName = Clean(customerName),
            CustomerPhone = Clean(customerPhone),
            Notes = Clean(notes)
        };

        foreach (var line in lines)
        {
            var product = await RequireActiveProduct(line.ProductId, ct);
            ValidateDiscount(actor, product, line.DiscountPercent);
            var batches = await repository.GetEligibleBatchesAsync(branch.Id, product.Id, ct);
            IReadOnlyList<FefoAllocationResult> fefoResults;
            try { fefoResults = fefo.Allocate(batches, product.Id, branch.Id, line.Quantity, BusinessDate(), ct); }
            catch (InvalidOperationException) { throw new ResourceConflictException($"Only {batches.Where(x => x.QuantityAvailable > 0 && !x.IsDisposed && x.ExpiryDate >= BusinessDate()).Sum(x => x.QuantityAvailable)} units are currently available."); }

            var saleItem = new SaleItem { ProductId = product.Id, RequestedQuantity = line.Quantity, DiscountPercent = line.DiscountPercent };
            foreach (var result in fefoResults)
            {
                var batch = batches.Single(x => x.Id == result.BatchId);
                var inventory = await repository.GetInventoryAsync(branch.Id, product.Id, batch.Id, ct) ?? throw new ResourceConflictException("Inventory balance is unavailable for selected stock.");
                if (batch.QuantityAvailable < result.AllocatedQuantity || inventory.QuantityInStock < result.AllocatedQuantity) throw new ResourceConflictException("Stock changed while completing the sale. The cart has been refreshed.");
                var gross = Money(batch.RetailPrice * result.AllocatedQuantity);
                var discount = Money(gross * line.DiscountPercent / 100m);
                var net = Money(gross - discount);
                batch.QuantityAvailable -= result.AllocatedQuantity;
                batch.UpdatedAt = now;
                inventory.QuantityInStock -= result.AllocatedQuantity;
                inventory.UpdatedAt = now;
                saleItem.GrossAmount += gross;
                saleItem.DiscountAmount += discount;
                saleItem.NetAmount += net;
                saleItem.Allocations.Add(new SaleItemBatchAllocation
                {
                    ProductBatchId = batch.Id,
                    Quantity = result.AllocatedQuantity,
                    UnitRetailPriceSnapshot = batch.RetailPrice,
                    UnitSalePriceSnapshot = Money(batch.RetailPrice * (1 - line.DiscountPercent / 100m)),
                    UnitCostPriceSnapshot = batch.PurchasePrice,
                    ExpiryDateSnapshot = batch.ExpiryDate,
                    GrossAmount = gross,
                    DiscountAmount = discount,
                    NetAmount = net
                });
                await repository.AddMovementAsync(new StockMovement
                {
                    MovementType = StockMovementType.Sale,
                    BranchId = branch.Id,
                    ProductId = product.Id,
                    ProductBatchId = batch.Id,
                    Quantity = -result.AllocatedQuantity,
                    ReferenceType = "Sale",
                    ReferenceId = sale.Id,
                    Notes = sale.InvoiceNumber,
                    PerformedByUserId = actor.Id
                }, ct);
            }
            saleItem.GrossAmount = Money(saleItem.GrossAmount);
            saleItem.DiscountAmount = Money(saleItem.DiscountAmount);
            saleItem.NetAmount = Money(saleItem.NetAmount);
            sale.Subtotal += saleItem.GrossAmount;
            sale.DiscountTotal += saleItem.DiscountAmount;
            sale.NetTotal += saleItem.NetAmount;
            sale.Items.Add(saleItem);
        }

        sale.Subtotal = Money(sale.Subtotal);
        sale.DiscountTotal = Money(sale.DiscountTotal);
        sale.TaxTotal = 0;
        sale.NetTotal = Money(sale.NetTotal);
        await ApplyPayments(actor, sale, payments, ct);
        return sale;
    }

    private async Task ApplyPayments(User actor, Sale sale, IReadOnlyList<SalePaymentRequest> payments, CancellationToken ct)
    {
        var applied = Money(payments.Sum(x => x.AmountApplied));
        if (applied > sale.NetTotal) throw new RequestValidationException("Payment total cannot exceed sale net total.");
        foreach (var payment in payments)
        {
            if (!Enum.IsDefined(payment.Method)) throw new RequestValidationException("Payment method is invalid.");
            if (payment.AmountApplied <= 0) throw new RequestValidationException("Payment amount must be greater than zero.");
            if (payment.Method == SalePaymentMethod.Cash && (!payment.TenderedAmount.HasValue || payment.TenderedAmount.Value < payment.AmountApplied)) throw new RequestValidationException("Cash tendered amount cannot be less than applied cash amount.");
            if (payment.Method != SalePaymentMethod.Cash && payment.TenderedAmount.HasValue) throw new RequestValidationException("Tendered amount is only valid for cash payments.");
            sale.Payments.Add(new SalePayment { Method = payment.Method, AmountApplied = Money(payment.AmountApplied), TenderedAmount = payment.TenderedAmount.HasValue ? Money(payment.TenderedAmount.Value) : null, ReferenceNumber = Clean(payment.ReferenceNumber), FinancialAccountId = payment.FinancialAccountId });
        }
        sale.AmountPaid = applied;
        sale.CreditAmount = Money(sale.NetTotal - applied);
        if (sale.CreditAmount > 0)
        {
            if (sale.CustomerId is null) throw new RequestValidationException("A customer is required for credit sales.");
            if (actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.SalesCredit) != true)
                throw new ForbiddenOperationException("The current user is not permitted to post credit sales.");
            var customer = await repository.GetCustomerAsync(sale.CustomerId.Value, ct);
            if (customer is not { IsActive: true }) throw new RequestValidationException("Customer is invalid or inactive.");
            sale.CustomerName = customer.Name;
            sale.CustomerPhone = customer.PhoneNumber;
            var balance = await repository.GetCustomerBalanceAsync(customer.Id, sale.BranchId, ct);
            if (balance + sale.CreditAmount > customer.CreditLimit)
                throw new ResourceConflictException("This sale would exceed the customer's credit limit.");
            await repository.AddCustomerLedgerEntryAsync(new CustomerLedgerEntry
            {
                CustomerId = customer.Id,
                BranchId = sale.BranchId,
                EntryType = CustomerLedgerEntryType.CreditSale,
                Amount = sale.CreditAmount,
                EntryDate = BusinessDate(),
                ReferenceType = "Sale",
                ReferenceId = sale.Id,
                ReferenceNumber = sale.InvoiceNumber,
                Notes = "Credit sale",
                CreatedByUserId = actor.Id
            }, ct);
        }
        else if (payments.Count == 0)
        {
            throw new RequestValidationException("At least one payment is required unless the sale is posted fully on customer credit.");
        }
        sale.ChangeGiven = Money(sale.Payments.Where(x => x.Method == SalePaymentMethod.Cash).Sum(x => (x.TenderedAmount ?? x.AmountApplied) - x.AmountApplied));
    }

    private async Task<User> Require(Guid actorId, string permission, CancellationToken cancellationToken)
    {
        var actor = await repository.GetActorAsync(actorId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private async Task<Branch> RequireActiveBranch(Guid branchId, CancellationToken ct)
    {
        var branch = await repository.GetBranchAsync(branchId, ct);
        return branch is { IsActive: true } ? branch : throw new RequestValidationException("Branch is invalid or inactive.");
    }

    private async Task<Product> RequireActiveProduct(Guid productId, CancellationToken ct)
    {
        var product = await repository.GetProductAsync(productId, ct);
        return product is { IsActive: true } ? product : throw new RequestValidationException("Product is invalid or inactive.");
    }

    private async Task<Sale> RequiredSale(Guid id, CancellationToken ct) => await repository.GetSaleAsync(id, ct) ?? throw new ResourceNotFoundException("Sale was not found.");
    private async Task<SaleDetailsDto> Details(User actor, Guid id, CancellationToken ct) => await repository.GetSaleDetailsAsync(id, actor.BranchId, CanSelectBranch(actor), ct) ?? throw new ResourceNotFoundException("Sale was not found.");

    private static void ValidateLines(IReadOnlyList<SaleLineRequest> lines, User actor)
    {
        if (lines.Count == 0) throw new RequestValidationException("At least one sale item is required.");
        foreach (var line in lines)
        {
            if (line.ProductId == Guid.Empty) throw new RequestValidationException("Product is required.");
            if (line.Quantity <= 0) throw new RequestValidationException("Quantity must be greater than zero.");
            if (line.DiscountPercent < 0 || line.DiscountPercent > 100) throw new RequestValidationException("Discount must be between 0 and 100.");
        }
    }

    private static void ValidatePaymentsShape(IReadOnlyList<SalePaymentRequest> payments)
    {
        foreach (var payment in payments)
        {
            if (payment.AmountApplied <= 0) throw new RequestValidationException("Payment amount must be greater than zero.");
        }
    }

    private static void ValidateDiscount(User actor, Product product, decimal discountPercent)
    {
        if (discountPercent > 0 && actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.SalesDiscount) != true)
            throw new ForbiddenOperationException("The current user is not permitted to discount sales.");
        if (discountPercent > product.MaximumDiscountPercent)
            throw new RequestValidationException("Discount exceeds the product maximum.");
    }

    private static (Guid? BranchId, bool CanSelectBranch) Scope(User actor, Guid? requestedBranchId)
    {
        var canSelect = CanSelectBranch(actor);
        if (!canSelect) return (actor.BranchId, false);
        return (requestedBranchId, true);
    }

    private static bool CanSelectBranch(User actor) => actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.UsersView) == true;
    private static void EnsureBranchAccess(User actor, Guid branchId)
    {
        if (!CanSelectBranch(actor) && actor.BranchId != branchId) throw new ForbiddenOperationException("The current user is not permitted to manage this branch.");
    }

    private DateOnly BusinessDate() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time")));
    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private Task Audit(Guid actor, string action, string type, Guid id, object? old, object? current, CancellationToken ct) => repository.AddAuditAsync(new AuditLog { UserId = actor, Action = action, EntityType = type, EntityId = id, OldValues = old is null ? null : JsonSerializer.Serialize(old), NewValues = current is null ? null : JsonSerializer.Serialize(current) }, ct);
    private static object AuditValues(Sale sale) => new { sale.InvoiceNumber, sale.HoldNumber, sale.BranchId, sale.CashierUserId, sale.CustomerId, ItemCount = sale.Items.Count, sale.NetTotal, sale.AmountPaid, sale.CreditAmount, PaymentSummary = string.Join(", ", sale.Payments.Select(x => $"{x.Method}:{x.AmountApplied}")) };
    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
    }
}
