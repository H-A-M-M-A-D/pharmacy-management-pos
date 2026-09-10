using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Purchasing;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Purchasing;

public sealed class PurchasingService(IPurchasingRepository repository, IJournalPostingService journalPosting, TimeProvider timeProvider) : IPurchasingService
{
    public async Task<PagedResult<PurchaseOrderListItemDto>> ListPurchaseOrdersAsync(Guid actorId, PurchaseOrderListQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchaseOrdersView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        var scope = Scope(actor, query.BranchId);
        return await repository.ListPurchaseOrdersAsync(query with { BranchId = scope.BranchId }, actor.BranchId, scope.CanSelectBranch, cancellationToken);
    }

    public async Task<PurchaseOrderDetailsDto> GetPurchaseOrderAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchaseOrdersView, cancellationToken);
        return await repository.GetPurchaseOrderDetailsAsync(id, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Purchase order was not found.");
    }

    public async Task<PurchaseOrderDetailsDto> CreatePurchaseOrderAsync(Guid actorId, PurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchaseOrdersCreate, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        ValidateOrder(request);
        PurchaseOrder? order = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var branch = await RequireActiveBranch(request.BranchId, ct);
            var supplier = await RequireActiveSupplier(request.SupplierId, ct);
            order = new PurchaseOrder
            {
                BranchId = branch.Id,
                SupplierId = supplier.Id,
                OrderNumber = await repository.NextPurchaseOrderNumberAsync(request.OrderDate, ct),
                SupplierReference = Clean(request.SupplierReference),
                OrderDate = request.OrderDate,
                ExpectedDate = request.ExpectedDate,
                Notes = Clean(request.Notes),
                Status = PurchaseOrderStatus.Draft,
                CreatedByUserId = actorId
            };
            foreach (var item in request.Items)
            {
                var product = await RequireActiveProduct(item.ProductId, ct);
                order.Items.Add(new PurchaseOrderItem
                {
                    ProductId = product.Id,
                    OrderedQuantity = item.OrderedQuantity,
                    ExpectedPurchasePrice = item.ExpectedPurchasePrice,
                    Notes = Clean(item.Notes)
                });
            }
            await repository.AddPurchaseOrderAsync(order, ct);
            await Audit(actorId, "PurchaseOrderCreated", "PurchaseOrder", order.Id, null, new { order.OrderNumber, order.BranchId, order.SupplierId, ItemCount = order.Items.Count }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetPurchaseOrderDetailsAsync(order!.Id, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Purchase order was not found.");
    }

    public async Task<PurchaseOrderDetailsDto> UpdatePurchaseOrderAsync(Guid actorId, Guid id, PurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchaseOrdersUpdate, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        ValidateOrder(request);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var order = await RequiredOrder(id, ct);
            EnsureBranchAccess(actor, order.BranchId);
            if (order.Status != PurchaseOrderStatus.Draft) throw new RequestValidationException("Only draft purchase orders can be edited.");
            await RequireActiveBranch(request.BranchId, ct);
            await RequireActiveSupplier(request.SupplierId, ct);
            var old = OrderValues(order);
            order.BranchId = request.BranchId;
            order.SupplierId = request.SupplierId;
            order.OrderDate = request.OrderDate;
            order.ExpectedDate = request.ExpectedDate;
            order.SupplierReference = Clean(request.SupplierReference);
            order.Notes = Clean(request.Notes);
            var replacementItems = new List<PurchaseOrderItem>(request.Items.Count);
            foreach (var item in request.Items)
            {
                var product = await RequireActiveProduct(item.ProductId, ct);
                replacementItems.Add(new PurchaseOrderItem
                {
                    ProductId = product.Id,
                    OrderedQuantity = item.OrderedQuantity,
                    ExpectedPurchasePrice = item.ExpectedPurchasePrice,
                    Notes = Clean(item.Notes)
                });
            }
            repository.ReplacePurchaseOrderItems(order, replacementItems);
            order.UpdatedAt = UtcNow();
            await Audit(actorId, "PurchaseOrderUpdated", "PurchaseOrder", order.Id, old, OrderValues(order), ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetPurchaseOrderDetailsAsync(id, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Purchase order was not found.");
    }

    public async Task<PurchaseOrderDetailsDto> SubmitPurchaseOrderAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchaseOrdersUpdate, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var order = await RequiredOrder(id, ct);
            EnsureBranchAccess(actor, order.BranchId);
            if (order.Status != PurchaseOrderStatus.Draft) throw new RequestValidationException("Only draft purchase orders can be submitted.");
            order.Status = PurchaseOrderStatus.Submitted;
            order.UpdatedAt = UtcNow();
            await Audit(actorId, "PurchaseOrderSubmitted", "PurchaseOrder", order.Id, null, new { order.OrderNumber, order.Status }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetPurchaseOrderDetailsAsync(id, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Purchase order was not found.");
    }

    public async Task<PurchaseOrderDetailsDto> CancelPurchaseOrderAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchaseOrdersCancel, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var order = await RequiredOrder(id, ct);
            EnsureBranchAccess(actor, order.BranchId);
            if (order.Status == PurchaseOrderStatus.Completed || order.Status == PurchaseOrderStatus.PartiallyReceived)
                throw new RequestValidationException("Received purchase orders cannot be cancelled in this phase.");
            if (order.Status == PurchaseOrderStatus.Cancelled) return;
            order.Status = PurchaseOrderStatus.Cancelled;
            order.UpdatedAt = UtcNow();
            await Audit(actorId, "PurchaseOrderCancelled", "PurchaseOrder", order.Id, null, new { order.OrderNumber, order.Status }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetPurchaseOrderDetailsAsync(id, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Purchase order was not found.");
    }

    public async Task<PagedResult<PurchaseHistoryItemDto>> ListPurchasesAsync(Guid actorId, PurchaseHistoryQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchasesView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        var scope = Scope(actor, query.BranchId);
        return await repository.ListPurchasesAsync(query with { BranchId = scope.BranchId }, actor.BranchId, scope.CanSelectBranch, cancellationToken);
    }

    public async Task<GoodsReceiptDetailsDto> GetGoodsReceiptAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchasesView, cancellationToken);
        return await repository.GetGoodsReceiptDetailsAsync(id, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Purchase was not found.");
    }

    public async Task<GoodsReceiptDetailsDto> PostGoodsReceiptAsync(Guid actorId, GoodsReceiptRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchasesReceive, cancellationToken);
        if (!request.PurchaseOrderId.HasValue) EnsurePermission(actor, PermissionCatalog.PurchasesCreate);
        EnsureBranchAccess(actor, request.BranchId);
        ValidateReceipt(request, BusinessDate());
        GoodsReceipt? receipt = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var branch = await RequireActiveBranch(request.BranchId, ct);
            var supplier = await RequireActiveSupplier(request.SupplierId, ct);
            var normalizedInvoice = NormalizeOptional(request.SupplierInvoiceNumber);
            if (normalizedInvoice is not null && await repository.SupplierInvoiceExistsAsync(supplier.Id, normalizedInvoice, ct))
                throw new ResourceConflictException("This supplier invoice has already been recorded.");

            var order = request.PurchaseOrderId.HasValue ? await RequiredOrder(request.PurchaseOrderId.Value, ct) : null;
            if (order is not null)
            {
                if (order.BranchId != branch.Id || order.SupplierId != supplier.Id) throw new RequestValidationException("Purchase order does not match branch and supplier.");
                if (order.Status is not PurchaseOrderStatus.Submitted and not PurchaseOrderStatus.PartiallyReceived)
                    throw new RequestValidationException("Only submitted or partially received purchase orders can be received.");
            }

            receipt = new GoodsReceipt
            {
                BranchId = branch.Id,
                SupplierId = supplier.Id,
                PurchaseOrderId = order?.Id,
                GrnNumber = await repository.NextGrnNumberAsync(request.ReceiptDate, ct),
                SupplierInvoiceNumber = Clean(request.SupplierInvoiceNumber),
                NormalizedSupplierInvoiceNumber = normalizedInvoice,
                ReceiptDate = request.ReceiptDate,
                Status = GoodsReceiptStatus.Posted,
                Notes = Clean(request.Notes),
                ReceivedByUserId = actorId
            };
            await repository.AddGoodsReceiptAsync(receipt, ct);

            foreach (var item in request.Items)
            {
                var product = await RequireActiveProduct(item.ProductId, ct);
                var orderItem = ResolveOrderItem(order, item);
                if (orderItem is not null && orderItem.ReceivedQuantity + item.PurchasedQuantity > orderItem.OrderedQuantity)
                    throw new RequestValidationException("Received paid quantity cannot exceed ordered quantity.");
                var batch = await BatchForReceipt(branch, supplier, product, item, ct);
                var totals = CalculateLine(item);
                var inventoryQuantity = item.PurchasedQuantity + item.BonusQuantity;
                var inventory = await InventoryFor(batch, product, ct);
                batch.QuantityReceived += inventoryQuantity;
                batch.QuantityAvailable += inventoryQuantity;
                batch.UpdatedAt = UtcNow();
                inventory.QuantityInStock += inventoryQuantity;
                inventory.UpdatedAt = UtcNow();
                receipt.Subtotal += totals.Gross;
                receipt.DiscountTotal += totals.Discount;
                receipt.TaxTotal += totals.Tax;
                receipt.NetTotal += totals.Net;
                receipt.Items.Add(new GoodsReceiptItem
                {
                    ProductId = product.Id,
                    PurchaseOrderItemId = orderItem?.Id,
                    ProductBatchId = batch.Id,
                    BatchNumber = item.BatchNumber.Trim(),
                    ManufacturingDate = item.ManufacturingDate,
                    ExpiryDate = item.ExpiryDate,
                    PurchasedQuantity = item.PurchasedQuantity,
                    BonusQuantity = item.BonusQuantity,
                    PurchasePrice = item.PurchasePrice,
                    RetailPrice = item.RetailPrice,
                    DiscountPercent = item.DiscountPercent,
                    DiscountAmount = totals.Discount,
                    TaxPercent = item.TaxPercent,
                    TaxAmount = totals.Tax,
                    NetLineAmount = totals.Net
                });
                await repository.AddMovementAsync(new StockMovement
                {
                    MovementType = StockMovementType.Purchase,
                    BranchId = branch.Id,
                    ProductId = product.Id,
                    ProductBatchId = batch.Id,
                    Quantity = inventoryQuantity,
                    ReferenceType = "GoodsReceipt",
                    ReferenceId = receipt.Id,
                    Notes = receipt.GrnNumber,
                    PerformedByUserId = actorId
                }, ct);
                if (orderItem is not null) orderItem.ReceivedQuantity += item.PurchasedQuantity;
            }

            receipt.Subtotal = Money(receipt.Subtotal);
            receipt.DiscountTotal = Money(receipt.DiscountTotal);
            receipt.TaxTotal = Money(receipt.TaxTotal);
            receipt.NetTotal = Money(receipt.NetTotal);
            if (receipt.NetTotal > 0)
            {
                receipt.DueDate = receipt.ReceiptDate.AddDays(supplier.PaymentTermsDays ?? 0);
                await repository.AddSupplierLedgerEntryAsync(new SupplierLedgerEntry
                {
                    SupplierId = supplier.Id,
                    BranchId = branch.Id,
                    EntryType = SupplierLedgerEntryType.Purchase,
                    Amount = receipt.NetTotal,
                    EntryDate = request.ReceiptDate,
                    ReferenceNumber = ReferenceNumber(receipt),
                    ReferenceType = "GoodsReceipt",
                    ReferenceId = receipt.Id,
                    Notes = receipt.Notes,
                    CreatedByUserId = actorId
                }, ct);
            }

            if (order is not null)
            {
                order.Status = order.Items.All(x => x.ReceivedQuantity >= x.OrderedQuantity)
                    ? PurchaseOrderStatus.Completed : PurchaseOrderStatus.PartiallyReceived;
                order.UpdatedAt = UtcNow();
            }

            await PostGoodsReceiptJournalAsync(actor, receipt, ct);
            await Audit(actorId, order is null ? "DirectPurchasePosted" : "PurchasePosted", "GoodsReceipt", receipt.Id, null,
                new { receipt.GrnNumber, receipt.SupplierInvoiceNumber, receipt.BranchId, receipt.SupplierId, receipt.PurchaseOrderId, receipt.NetTotal, ItemCount = receipt.Items.Count }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetGoodsReceiptDetailsAsync(receipt!.Id, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Purchase was not found.");
    }


    public async Task<ReturnableGoodsReceiptDto> GetReturnableGoodsReceiptAsync(Guid actorId, Guid goodsReceiptId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchaseReturnsView, cancellationToken);
        return await repository.GetReturnableGoodsReceiptAsync(goodsReceiptId, actor.BranchId, CanSelectBranch(actor), BusinessDate(), cancellationToken)
            ?? throw new ResourceNotFoundException("Purchase was not found.");
    }

    public async Task<PurchaseReturnDetailsDto> PostPurchaseReturnAsync(Guid actorId, Guid goodsReceiptId, PostPurchaseReturnRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchaseReturnsCreate, cancellationToken);
        ValidatePurchaseReturn(request);
        PurchaseReturn? posted = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var receipt = await repository.GetGoodsReceiptAsync(goodsReceiptId, ct) ?? throw new ResourceNotFoundException("Purchase was not found.");
            EnsureBranchAccess(actor, receipt.BranchId);
            if (receipt.Status != GoodsReceiptStatus.Posted) throw new RequestValidationException("Only posted goods receipts can be returned to supplier.");
            if (request.Items.Any(x => receipt.Items.All(i => i.Id != x.OriginalGoodsReceiptItemId)))
                throw new RequestValidationException("This item does not belong to the selected Goods Receipt.");

            var existing = await repository.GetPurchaseReturnTotalsAsync(receipt.Id, ct);
            var now = UtcNow();
            posted = new PurchaseReturn
            {
                OriginalGoodsReceiptId = receipt.Id,
                SupplierId = receipt.SupplierId,
                BranchId = receipt.BranchId,
                ProcessedByUserId = actorId,
                ReturnNumber = await repository.NextPurchaseReturnNumberAsync(now, ct),
                ReturnDateUtc = now,
                PostedAtUtc = now,
                Status = PurchaseReturnStatus.Posted,
                Reason = request.Reason,
                Notes = Clean(request.Notes)
            };
            await repository.AddPurchaseReturnAsync(posted, ct);

            foreach (var line in request.Items)
            {
                var original = receipt.Items.First(x => x.Id == line.OriginalGoodsReceiptItemId);
                var prior = existing.TryGetValue(original.Id, out var totals) ? totals : default;
                var paidRemaining = original.PurchasedQuantity - prior.PaidQuantity;
                var bonusRemaining = original.BonusQuantity - prior.BonusQuantity;
                if (line.PaidReturnQuantity > paidRemaining) throw new ResourceConflictException($"Only {paidRemaining} paid units remain returnable.");
                if (line.BonusReturnQuantity > bonusRemaining) throw new ResourceConflictException($"Only {bonusRemaining} bonus units remain returnable.");
                if (!original.ProductBatchId.HasValue) throw new RequestValidationException("Original receipt item is not linked to a batch.");
                var batch = await repository.GetBatchAsync(original.ProductBatchId.Value, ct) ?? throw new ResourceNotFoundException("Original batch was not found.");
                var physical = line.PaidReturnQuantity + line.BonusReturnQuantity;
                if (batch.BranchId != receipt.BranchId || batch.ProductId != original.ProductId) throw new RequestValidationException("Original batch does not match the receipt item.");
                if (batch.QuantityAvailable < physical) throw new ResourceConflictException($"Only {batch.QuantityAvailable} units are physically available in this batch.");
                var inventory = await InventoryFor(batch, original.Product!, ct);
                if (inventory.QuantityInStock < physical) throw new ResourceConflictException($"Only {inventory.QuantityInStock} units are physically available in inventory.");

                var credit = CalculateReturnCredit(original, prior, line.PaidReturnQuantity);
                if (prior.Net + credit.Net > original.NetLineAmount) throw new ResourceConflictException("This Purchase Return would exceed the original supplier credit.");

                batch.QuantityAvailable -= physical;
                batch.UpdatedAt = now;
                inventory.QuantityInStock -= physical;
                inventory.UpdatedAt = now;
                posted.GrossReturnAmount += credit.Gross;
                posted.DiscountAdjustment += credit.Discount;
                posted.TaxAdjustment += credit.Tax;
                posted.NetSupplierCredit += credit.Net;
                posted.Items.Add(new PurchaseReturnItem
                {
                    OriginalGoodsReceiptItemId = original.Id,
                    ProductId = original.ProductId,
                    ProductBatchId = batch.Id,
                    BatchNumber = original.BatchNumber,
                    ExpiryDate = original.ExpiryDate,
                    PaidReturnQuantity = line.PaidReturnQuantity,
                    BonusReturnQuantity = line.BonusReturnQuantity,
                    PurchasePriceSnapshot = original.PurchasePrice,
                    GrossReturnAmount = credit.Gross,
                    DiscountAdjustment = credit.Discount,
                    TaxAdjustment = credit.Tax,
                    NetSupplierCredit = credit.Net
                });
                await repository.AddMovementAsync(new StockMovement
                {
                    MovementType = StockMovementType.PurchaseReturn,
                    BranchId = receipt.BranchId,
                    ProductId = original.ProductId,
                    ProductBatchId = batch.Id,
                    Quantity = -physical,
                    ReferenceType = "PurchaseReturn",
                    ReferenceId = posted.Id,
                    Notes = posted.ReturnNumber,
                    PerformedByUserId = actorId
                }, ct);
            }

            posted.GrossReturnAmount = Money(posted.GrossReturnAmount);
            posted.DiscountAdjustment = Money(posted.DiscountAdjustment);
            posted.TaxAdjustment = Money(posted.TaxAdjustment);
            posted.NetSupplierCredit = Money(posted.NetSupplierCredit);
            if (posted.NetSupplierCredit > 0)
            {
                await repository.AddSupplierLedgerEntryAsync(new SupplierLedgerEntry
                {
                    SupplierId = receipt.SupplierId,
                    BranchId = receipt.BranchId,
                    EntryType = SupplierLedgerEntryType.PurchaseReturn,
                    Amount = -posted.NetSupplierCredit,
                    EntryDate = DateOnly.FromDateTime(now),
                    ReferenceNumber = posted.ReturnNumber,
                    ReferenceType = "PurchaseReturn",
                    ReferenceId = posted.Id,
                    Notes = posted.Notes,
                    CreatedByUserId = actorId
                }, ct);
            }

            await PostPurchaseReturnJournalAsync(actor, posted, ct);
            await Audit(actorId, "PurchaseReturnPosted", "PurchaseReturn", posted.Id, null,
                new { posted.ReturnNumber, receipt.GrnNumber, posted.SupplierId, posted.BranchId, posted.NetSupplierCredit, PhysicalQuantity = posted.Items.Sum(x => x.PaidReturnQuantity + x.BonusReturnQuantity) }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetPurchaseReturnDetailsAsync(posted!.Id, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Purchase return was not found.");
    }

    public async Task<PagedResult<PurchaseReturnListItemDto>> ListPurchaseReturnsAsync(Guid actorId, PurchaseReturnListQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchaseReturnsView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        var scope = Scope(actor, query.BranchId);
        return await repository.ListPurchaseReturnsAsync(query with { BranchId = scope.BranchId }, actor.BranchId, scope.CanSelectBranch, cancellationToken);
    }

    public async Task<PurchaseReturnDetailsDto> GetPurchaseReturnAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchaseReturnsView, cancellationToken);
        return await repository.GetPurchaseReturnDetailsAsync(id, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Purchase return was not found.");
    }

    public async Task<PurchaseReturnDetailsDto> ReprintPurchaseReturnAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchaseReturnsReprint, cancellationToken);
        var details = await repository.GetPurchaseReturnDetailsAsync(id, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Purchase return was not found.");
        await Audit(actorId, "PurchaseReturnNoteReprinted", "PurchaseReturn", id, null, new { details.ReturnNumber }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return details;
    }

    public async Task<PurchasingOptionsDto> GetOptionsAsync(Guid actorId, string? productSearch, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.PurchasesView, cancellationToken);
        return await repository.GetOptionsAsync(productSearch, actor.BranchId, CanSelectBranch(actor), cancellationToken);
    }

    private async Task PostGoodsReceiptJournalAsync(User actor, GoodsReceipt receipt, CancellationToken ct)
    {
        if (receipt.NetTotal <= 0) return;
        await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.Purchase, receipt.Id, receipt.BranchId, UtcNow(),
            ReferenceNumber(receipt), $"Purchase {receipt.GrnNumber}", actor.Id,
            [new(AccountMappingKey.Inventory, receipt.NetTotal, 0), new(AccountMappingKey.AccountsPayable, 0, receipt.NetTotal, SupplierId: receipt.SupplierId)]), ct);
    }

    private async Task PostPurchaseReturnJournalAsync(User actor, PurchaseReturn posted, CancellationToken ct)
    {
        if (posted.NetSupplierCredit <= 0) return;
        await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.PurchaseReturn, posted.Id, posted.BranchId, posted.PostedAtUtc,
            posted.ReturnNumber, $"Purchase return {posted.ReturnNumber}", actor.Id,
            [new(AccountMappingKey.AccountsPayable, posted.NetSupplierCredit, 0, SupplierId: posted.SupplierId), new(AccountMappingKey.Inventory, 0, posted.NetSupplierCredit)]), ct);
    }

    private async Task<User> Require(Guid actorId, string permission, CancellationToken cancellationToken)
    {
        var actor = await repository.GetActorAsync(actorId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private static void EnsurePermission(User actor, string permission)
    {
        if (actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
    }

    private static (Guid? BranchId, bool CanSelectBranch) Scope(User actor, Guid? requestedBranchId)
    {
        var canSelect = CanSelectBranch(actor);
        if (!canSelect) return (actor.BranchId, false);
        return (requestedBranchId, true);
    }

    private static bool CanSelectBranch(User actor) =>
        actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == PermissionCatalog.UsersView) == true;

    private static void EnsureBranchAccess(User actor, Guid branchId)
    {
        if (!CanSelectBranch(actor) && actor.BranchId != branchId)
            throw new ForbiddenOperationException("The current user is not permitted to manage this branch.");
    }

    private async Task<Branch> RequireActiveBranch(Guid branchId, CancellationToken ct)
    {
        var branch = await repository.GetBranchAsync(branchId, ct);
        return branch is { IsActive: true } ? branch : throw new RequestValidationException("Branch is invalid or inactive.");
    }

    private async Task<Supplier> RequireActiveSupplier(Guid supplierId, CancellationToken ct)
    {
        var supplier = await repository.GetSupplierAsync(supplierId, ct);
        return supplier is { IsActive: true } ? supplier : throw new RequestValidationException("Supplier is invalid or inactive.");
    }

    private async Task<Product> RequireActiveProduct(Guid productId, CancellationToken ct)
    {
        var product = await repository.GetProductAsync(productId, ct);
        return product is { IsActive: true } ? product : throw new RequestValidationException("Product is invalid or inactive.");
    }

    private async Task<PurchaseOrder> RequiredOrder(Guid id, CancellationToken ct) =>
        await repository.GetPurchaseOrderAsync(id, ct) ?? throw new ResourceNotFoundException("Purchase order was not found.");

    private static PurchaseOrderItem? ResolveOrderItem(PurchaseOrder? order, GoodsReceiptItemRequest item)
    {
        if (order is null)
        {
            if (item.PurchaseOrderItemId.HasValue) throw new RequestValidationException("Direct purchases cannot reference purchase order items.");
            return null;
        }
        if (!item.PurchaseOrderItemId.HasValue) throw new RequestValidationException("Purchase order receipt items must reference a purchase order item.");
        var orderItem = order.Items.FirstOrDefault(x => x.Id == item.PurchaseOrderItemId.Value)
            ?? throw new RequestValidationException("Purchase order item is invalid.");
        if (orderItem.ProductId != item.ProductId) throw new RequestValidationException("Receipt product does not match the purchase order item.");
        return orderItem;
    }

    private async Task<ProductBatch> BatchForReceipt(Branch branch, Supplier supplier, Product product, GoodsReceiptItemRequest item, CancellationToken ct)
    {
        var batchNumber = item.BatchNumber.Trim();
        var batch = await repository.GetBatchByNumberAsync(branch.Id, product.Id, batchNumber, ct);
        if (batch is null)
        {
            batch = new ProductBatch
            {
                BranchId = branch.Id,
                ProductId = product.Id,
                SupplierId = supplier.Id,
                BatchNumber = batchNumber,
                ManufacturingDate = item.ManufacturingDate,
                ExpiryDate = item.ExpiryDate,
                PurchasePrice = item.PurchasePrice,
                RetailPrice = item.RetailPrice,
                QuantityReceived = 0,
                QuantityAvailable = 0
            };
            await repository.AddBatchAsync(batch, ct);
            return batch;
        }
        if (batch.IsDisposed) throw new RequestValidationException("Disposed batches cannot be received.");
        if (batch.ExpiryDate != item.ExpiryDate) throw new ResourceConflictException($"Batch {batchNumber} already exists with a different expiry date.");
        if (batch.ManufacturingDate != item.ManufacturingDate) throw new ResourceConflictException($"Batch {batchNumber} already exists with different manufacturing date.");
        if (batch.PurchasePrice != item.PurchasePrice || batch.RetailPrice != item.RetailPrice) throw new ResourceConflictException($"Batch {batchNumber} already exists with different prices.");
        if (batch.SupplierId.HasValue && batch.SupplierId != supplier.Id) throw new ResourceConflictException($"Batch {batchNumber} already exists for another supplier.");
        if (!batch.SupplierId.HasValue) batch.SupplierId = supplier.Id;
        return batch;
    }

    private async Task<Pharmacy.Domain.Entities.Inventory> InventoryFor(ProductBatch batch, Product product, CancellationToken ct)
    {
        var inventory = await repository.GetInventoryAsync(batch.BranchId, batch.ProductId, batch.Id, ct);
        if (inventory is not null) return inventory;
        inventory = new Pharmacy.Domain.Entities.Inventory { BranchId = batch.BranchId, ProductId = batch.ProductId, ProductBatchId = batch.Id, ReorderLevel = product.ReorderLevel, QuantityInStock = 0, LastCountedAt = UtcNow() };
        await repository.AddInventoryAsync(inventory, ct);
        return inventory;
    }

    public static (decimal Gross, decimal Discount, decimal Tax, decimal Net) CalculateLine(GoodsReceiptItemRequest item)
    {
        var gross = Money(item.PurchasedQuantity * item.PurchasePrice);
        var discount = Money(gross * item.DiscountPercent / 100m);
        var taxable = gross - discount;
        var tax = Money(taxable * item.TaxPercent / 100m);
        return (gross, discount, tax, Money(taxable + tax));
    }

    private static void ValidateOrder(PurchaseOrderRequest request)
    {
        if (request.BranchId == Guid.Empty || request.SupplierId == Guid.Empty) throw new RequestValidationException("Branch and supplier are required.");
        if (request.ExpectedDate.HasValue && request.ExpectedDate.Value < request.OrderDate) throw new RequestValidationException("Expected date cannot be before order date.");
        if (request.Items.Count == 0) throw new RequestValidationException("At least one purchase order item is required.");
        foreach (var item in request.Items)
        {
            if (item.ProductId == Guid.Empty) throw new RequestValidationException("Product is required.");
            if (item.OrderedQuantity <= 0) throw new RequestValidationException("Ordered quantity must be greater than zero.");
            if (item.ExpectedPurchasePrice < 0) throw new RequestValidationException("Expected purchase price cannot be negative.");
        }
    }

    private static void ValidateReceipt(GoodsReceiptRequest request, DateOnly businessDate)
    {
        if (request.BranchId == Guid.Empty || request.SupplierId == Guid.Empty) throw new RequestValidationException("Branch and supplier are required.");
        if (request.Items.Count == 0) throw new RequestValidationException("At least one receipt item is required.");
        foreach (var item in request.Items)
        {
            if (item.ProductId == Guid.Empty) throw new RequestValidationException("Product is required.");
            if (string.IsNullOrWhiteSpace(item.BatchNumber)) throw new RequestValidationException("Batch number is required.");
            if (item.PurchasedQuantity <= 0) throw new RequestValidationException("Purchased quantity must be greater than zero.");
            if (item.BonusQuantity < 0) throw new RequestValidationException("Bonus quantity cannot be negative.");
            if (item.PurchasePrice < 0 || item.RetailPrice < 0) throw new RequestValidationException("Prices cannot be negative.");
            if (item.DiscountPercent is < 0 or > 100) throw new RequestValidationException("Discount percentage must be between 0 and 100.");
            if (item.TaxPercent is < 0 or > 100) throw new RequestValidationException("Tax percentage must be between 0 and 100.");
            if (item.ManufacturingDate.HasValue && item.ManufacturingDate.Value > item.ExpiryDate) throw new RequestValidationException("Manufacturing date cannot be after expiry date.");
            if (item.ExpiryDate < businessDate) throw new RequestValidationException("Expired stock cannot be received as sellable stock.");
        }
    }


    private static (decimal Gross, decimal Discount, decimal Tax, decimal Net) CalculateReturnCredit(GoodsReceiptItem original, (int PaidQuantity, int BonusQuantity, decimal Gross, decimal Discount, decimal Tax, decimal Net) prior, int paidReturnQuantity)
    {
        if (paidReturnQuantity == 0) return (0, 0, 0, 0);
        var finalPaidReturn = prior.PaidQuantity + paidReturnQuantity == original.PurchasedQuantity;
        if (finalPaidReturn)
        {
            return (Money(original.PurchasedQuantity * original.PurchasePrice - prior.Gross), Money(original.DiscountAmount - prior.Discount), Money(original.TaxAmount - prior.Tax), Money(original.NetLineAmount - prior.Net));
        }
        return (Money(original.PurchasedQuantity == 0 ? 0 : original.PurchasedQuantity * original.PurchasePrice * paidReturnQuantity / original.PurchasedQuantity),
            Money(original.PurchasedQuantity == 0 ? 0 : original.DiscountAmount * paidReturnQuantity / original.PurchasedQuantity),
            Money(original.PurchasedQuantity == 0 ? 0 : original.TaxAmount * paidReturnQuantity / original.PurchasedQuantity),
            Money(original.PurchasedQuantity == 0 ? 0 : original.NetLineAmount * paidReturnQuantity / original.PurchasedQuantity));
    }

    private static void ValidatePurchaseReturn(PostPurchaseReturnRequest request)
    {
        if (!Enum.IsDefined(request.Reason)) throw new RequestValidationException("Purchase return reason is invalid.");
        if (request.Reason == PurchaseReturnReason.Other && string.IsNullOrWhiteSpace(request.Notes)) throw new RequestValidationException("Notes are required for Other purchase returns.");
        if (request.Items.Count == 0) throw new RequestValidationException("At least one purchase return item is required.");
        var seen = new HashSet<Guid>();
        foreach (var item in request.Items)
        {
            if (item.OriginalGoodsReceiptItemId == Guid.Empty) throw new RequestValidationException("Original receipt item is required.");
            if (!seen.Add(item.OriginalGoodsReceiptItemId)) throw new RequestValidationException("Each original receipt item can appear only once per purchase return.");
            if (item.PaidReturnQuantity < 0 || item.BonusReturnQuantity < 0) throw new RequestValidationException("Return quantities cannot be negative.");
            if (item.PaidReturnQuantity + item.BonusReturnQuantity <= 0) throw new RequestValidationException("At least one paid or bonus unit must be returned.");
        }
    }
    private DateOnly BusinessDate() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time")));
    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string ReferenceNumber(GoodsReceipt receipt) => string.IsNullOrWhiteSpace(receipt.SupplierInvoiceNumber) ? receipt.GrnNumber : $"{receipt.SupplierInvoiceNumber} / {receipt.GrnNumber}";
    private static object OrderValues(PurchaseOrder order) => new { order.OrderNumber, order.BranchId, order.SupplierId, order.OrderDate, order.ExpectedDate, order.SupplierReference, order.Status, ItemCount = order.Items.Count };
    private Task Audit(Guid actor, string action, string type, Guid id, object? old, object? current, CancellationToken ct) => repository.AddAuditAsync(new AuditLog
    { UserId = actor, Action = action, EntityType = type, EntityId = id, OldValues = old is null ? null : JsonSerializer.Serialize(old), NewValues = current is null ? null : JsonSerializer.Serialize(current) }, ct);
    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
    }
}
