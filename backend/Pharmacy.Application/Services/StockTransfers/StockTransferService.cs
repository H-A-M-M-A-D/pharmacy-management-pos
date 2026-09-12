using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.StockTransfers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Godowns;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.StockTransfers;

/// <summary>
/// Inter-godown (and inter-branch) stock transfer workflow: Draft -&gt; Requested -&gt; Approved -&gt;
/// Dispatched -&gt; PartiallyReceived/Received, with Cancel available before dispatch. StockMovement
/// remains the source of truth for actual stock changes: dispatch posts TransferOut against the source
/// batch, each receipt posts TransferIn against the (reused or newly created) destination batch for
/// exactly the quantity received. No GL entry is posted for a normal transfer - it moves the same
/// company's inventory between locations under one Inventory control account, so debit and credit would
/// be the same account. A discrepancy resolution (permanent shortage) is the one case that does post a
/// write-off, since that value is genuinely gone.
/// </summary>
public sealed class StockTransferService(
    IStockTransferRepository repository,
    IGodownAccessService godownAccess,
    IJournalPostingService journalPosting,
    IDuplicateSubmissionGuard duplicateGuard,
    TimeProvider timeProvider) : IStockTransferService
{
    public async Task<PagedResult<StockTransferListItemDto>> ListTransfersAsync(Guid actorId, StockTransferListQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.StockTransfersView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        return await repository.ListTransfersAsync(query, actor.BranchId, CanSelectBranch(actor), cancellationToken);
    }

    public async Task<StockTransferDetailsDto> GetTransferAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.StockTransfersView, cancellationToken);
        var dto = await Details(id, cancellationToken);
        if (!CanSelectBranch(actor) && actor.BranchId != dto.SourceBranchId && actor.BranchId != dto.DestinationBranchId)
            throw new ForbiddenOperationException("The current user is not permitted to view this transfer.");
        return dto;
    }

    public async Task<IReadOnlyList<TransferableBatchDto>> ListTransferableBatchesAsync(Guid actorId, Guid branchId, Guid godownId, Guid? productId, string? search, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.StockTransfersCreate, cancellationToken);
        await EnsureGodownAccessAsync(actor, branchId, godownId, cancellationToken);
        return await repository.ListTransferableBatchesAsync(branchId, godownId, productId, search, cancellationToken);
    }

    public async Task<StockTransferDetailsDto> CreateTransferAsync(Guid actorId, StockTransferRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.StockTransfersCreate, cancellationToken);
        ValidateHeader(request);
        EnsureBranchAccess(actor, request.SourceBranchId);

        StockTransfer? transfer = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var sourceBranch = await RequiredActiveBranch(request.SourceBranchId, ct);
            var destBranch = await RequiredActiveBranch(request.DestinationBranchId, ct);
            await EnsureGodownAccessAsync(actor, sourceBranch.Id, request.SourceGodownId, ct);
            await RequiredValidGodownAsync(destBranch.Id, request.DestinationGodownId, ct);

            transfer = new StockTransfer
            {
                TransferNumber = await repository.NextTransferNumberAsync(request.TransferDate, ct),
                SourceBranchId = sourceBranch.Id,
                SourceGodownId = request.SourceGodownId,
                DestinationBranchId = destBranch.Id,
                DestinationGodownId = request.DestinationGodownId,
                TransferDate = request.TransferDate,
                Status = StockTransferStatus.Draft,
                Notes = Clean(request.Notes),
                CreatedByUserId = actorId,
                Items = await BuildItemsAsync(sourceBranch.Id, request.SourceGodownId, request.Items, ct)
            };
            await repository.AddTransferAsync(transfer, ct);
            await Audit(actorId, "StockTransferCreated", transfer.Id,
                new { transfer.TransferNumber, transfer.SourceGodownId, transfer.DestinationGodownId, ItemCount = transfer.Items.Count }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(transfer!.Id, cancellationToken);
    }

    public async Task<StockTransferDetailsDto> UpdateTransferAsync(Guid actorId, Guid id, StockTransferRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.StockTransfersCreate, cancellationToken);
        ValidateHeader(request);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var transfer = await RequiredTransfer(id, ct);
            if (transfer.Status != StockTransferStatus.Draft) throw new RequestValidationException("Only draft transfers can be edited.");
            EnsureTransferBranchAccess(actor, transfer);
            EnsureBranchAccess(actor, request.SourceBranchId);
            var sourceBranch = await RequiredActiveBranch(request.SourceBranchId, ct);
            var destBranch = await RequiredActiveBranch(request.DestinationBranchId, ct);
            await EnsureGodownAccessAsync(actor, sourceBranch.Id, request.SourceGodownId, ct);
            await RequiredValidGodownAsync(destBranch.Id, request.DestinationGodownId, ct);

            transfer.SourceBranchId = sourceBranch.Id;
            transfer.SourceGodownId = request.SourceGodownId;
            transfer.DestinationBranchId = destBranch.Id;
            transfer.DestinationGodownId = request.DestinationGodownId;
            transfer.TransferDate = request.TransferDate;
            transfer.Notes = Clean(request.Notes);
            transfer.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            var items = await BuildItemsAsync(sourceBranch.Id, request.SourceGodownId, request.Items, ct);
            repository.ReplaceTransferItems(transfer, items);
            await Audit(actorId, "StockTransferUpdated", transfer.Id, new { transfer.TransferNumber, ItemCount = items.Count }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<StockTransferDetailsDto> RequestTransferAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.StockTransfersRequest, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var transfer = await RequiredTransfer(id, ct);
            EnsureTransferBranchAccess(actor, transfer);
            if (transfer.Status != StockTransferStatus.Draft) throw new RequestValidationException("Only draft transfers can be requested.");
            if (transfer.Items.Count == 0) throw new RequestValidationException("At least one item is required before requesting a transfer.");
            transfer.Status = StockTransferStatus.Requested;
            transfer.RequestedByUserId = actorId;
            transfer.RequestedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            transfer.UpdatedAt = transfer.RequestedAtUtc.Value;
            await Audit(actorId, "StockTransferRequested", transfer.Id, new { transfer.TransferNumber }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<StockTransferDetailsDto> ApproveTransferAsync(Guid actorId, Guid id, ApproveStockTransferRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.StockTransfersApprove, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var transfer = await RequiredTransfer(id, ct);
            EnsureTransferBranchAccess(actor, transfer);
            if (transfer.Status != StockTransferStatus.Requested) throw new RequestValidationException("Only requested transfers can be approved.");
            var overrides = (request.Items ?? []).ToDictionary(x => x.StockTransferItemId, x => x.QuantityApproved);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            foreach (var item in transfer.Items)
            {
                var approved = overrides.TryGetValue(item.Id, out var qty) ? qty : item.QuantityRequested;
                if (approved < 0 || approved > item.QuantityRequested)
                    throw new RequestValidationException($"Approved quantity for batch {item.BatchNumber} must be between zero and the requested quantity.");
                item.QuantityApproved = approved;
                item.UpdatedAt = now;
            }
            if (transfer.Items.Sum(x => x.QuantityApproved) <= 0)
                throw new RequestValidationException("At least one item must be approved with a positive quantity.");
            transfer.Status = StockTransferStatus.Approved;
            transfer.ApprovedByUserId = actorId;
            transfer.ApprovedAtUtc = now;
            transfer.UpdatedAt = now;
            await Audit(actorId, "StockTransferApproved", transfer.Id,
                new { transfer.TransferNumber, TotalApproved = transfer.Items.Sum(x => x.QuantityApproved) }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<StockTransferDetailsDto> DispatchTransferAsync(Guid actorId, Guid id, DispatchStockTransferRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.StockTransfersDispatch, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            await duplicateGuard.GuardAsync("StockTransfer.Dispatch", actorId, new { id, request.Items }, ct);
            var transfer = await RequiredTransfer(id, ct);
            EnsureTransferBranchAccess(actor, transfer);
            if (transfer.Status != StockTransferStatus.Approved) throw new RequestValidationException("Only approved transfers can be dispatched.");
            await EnsureGodownAccessAsync(actor, transfer.SourceBranchId, transfer.SourceGodownId, ct);

            var overrides = (request.Items ?? []).ToDictionary(x => x.StockTransferItemId, x => x.QuantityDispatched);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var totalDispatched = 0;
            foreach (var item in transfer.Items.Where(x => x.QuantityApproved > 0))
            {
                var qty = overrides.TryGetValue(item.Id, out var ov) ? ov : item.QuantityApproved;
                if (qty < 0 || qty > item.QuantityApproved)
                    throw new RequestValidationException($"Dispatched quantity for batch {item.BatchNumber} must be between zero and the approved quantity.");
                if (qty == 0) continue;
                var batch = await repository.GetBatchForUpdateAsync(item.SourceProductBatchId, ct)
                    ?? throw new ResourceNotFoundException("Source batch was not found.");
                if (batch.IsDisposed) throw new RequestValidationException($"Batch {batch.BatchNumber} has been disposed and cannot be dispatched.");
                if (batch.QuantityAvailable < qty) throw new RequestValidationException($"Insufficient stock in batch {batch.BatchNumber} to dispatch the requested quantity.");
                var inventory = await repository.GetInventoryAsync(batch.BranchId, batch.ProductId, batch.Id, ct)
                    ?? throw new ResourceNotFoundException("Inventory balance was not found.");
                await ApplyOutboundAsync(batch, inventory, qty, transfer, actorId, ct);
                item.QuantityDispatched = qty;
                item.UpdatedAt = now;
                totalDispatched += qty;
            }
            if (totalDispatched <= 0) throw new RequestValidationException("At least one item must be dispatched with a positive quantity.");
            transfer.Status = StockTransferStatus.Dispatched;
            transfer.DispatchedByUserId = actorId;
            transfer.DispatchedAtUtc = now;
            transfer.UpdatedAt = now;
            await Audit(actorId, "StockTransferDispatched", transfer.Id, new { transfer.TransferNumber, TotalDispatched = totalDispatched }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<StockTransferDetailsDto> ReceiveTransferAsync(Guid actorId, Guid id, ReceiveStockTransferRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.StockTransfersReceive, cancellationToken);
        if (request.Items.Count == 0) throw new RequestValidationException("At least one item is required.");
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            await duplicateGuard.GuardAsync("StockTransfer.Receive", actorId, new { id, request.Items }, ct);
            var transfer = await RequiredTransfer(id, ct);
            EnsureTransferBranchAccess(actor, transfer);
            if (transfer.Status is not (StockTransferStatus.Dispatched or StockTransferStatus.PartiallyReceived))
                throw new RequestValidationException("Only dispatched or partially received transfers can be received.");
            await EnsureGodownAccessAsync(actor, transfer.DestinationBranchId, transfer.DestinationGodownId, ct);
            var destBranch = await RequiredActiveBranch(transfer.DestinationBranchId, ct);

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var totalReceivedNow = 0;
            foreach (var line in request.Items)
            {
                var item = transfer.Items.FirstOrDefault(x => x.Id == line.StockTransferItemId)
                    ?? throw new RequestValidationException("One or more receipt lines do not belong to this transfer.");
                if (line.QuantityReceived <= 0) throw new RequestValidationException("Received quantity must be greater than zero.");
                var remaining = item.QuantityDispatched - item.QuantityReceived;
                if (line.QuantityReceived > remaining)
                    throw new RequestValidationException($"Cannot receive more than the {remaining} unit(s) still in transit for batch {item.BatchNumber}.");

                var product = await RequiredProduct(item.ProductId, ct);
                var destBatch = await BatchForTransferReceiptAsync(destBranch, transfer.DestinationGodownId, item, actorId, ct);
                var inventory = await InventoryFor(destBatch, product, ct);
                await ApplyInboundAsync(destBatch, inventory, line.QuantityReceived, transfer, actorId, ct);
                item.DestinationProductBatchId = destBatch.Id;
                item.QuantityReceived += line.QuantityReceived;
                item.UpdatedAt = now;
                if (!string.IsNullOrWhiteSpace(line.Notes)) item.Notes = AppendNote(item.Notes, line.Notes.Trim());
                totalReceivedNow += line.QuantityReceived;
            }
            if (totalReceivedNow <= 0) throw new RequestValidationException("At least one item must be received with a positive quantity.");

            var totalDispatched = transfer.Items.Sum(x => x.QuantityDispatched);
            var totalReceived = transfer.Items.Sum(x => x.QuantityReceived);
            transfer.Status = totalReceived >= totalDispatched ? StockTransferStatus.Received : StockTransferStatus.PartiallyReceived;
            transfer.ReceivedByUserId = actorId;
            transfer.ReceivedAtUtc = now;
            transfer.UpdatedAt = now;
            if (!string.IsNullOrWhiteSpace(request.Notes)) transfer.Notes = AppendNote(transfer.Notes, request.Notes.Trim());
            await Audit(actorId, transfer.Status == StockTransferStatus.Received ? "StockTransferReceived" : "StockTransferPartiallyReceived",
                transfer.Id, new { transfer.TransferNumber, ReceivedNow = totalReceivedNow, TotalReceived = totalReceived, TotalDispatched = totalDispatched, request.Notes }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    public async Task<StockTransferDetailsDto> CancelTransferAsync(Guid actorId, Guid id, CancelStockTransferRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.StockTransfersCancel, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("Cancellation reason is required.");
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var transfer = await RequiredTransfer(id, ct);
            EnsureTransferBranchAccess(actor, transfer);
            if (transfer.Status is StockTransferStatus.Dispatched or StockTransferStatus.PartiallyReceived or StockTransferStatus.Received or StockTransferStatus.Cancelled)
                throw new RequestValidationException("Only draft, requested, or approved transfers can be cancelled directly; a dispatched transfer must be resolved instead.");
            var now = timeProvider.GetUtcNow().UtcDateTime;
            transfer.Status = StockTransferStatus.Cancelled;
            transfer.CancelledByUserId = actorId;
            transfer.CancelledAtUtc = now;
            transfer.CancellationReason = request.Reason.Trim();
            transfer.UpdatedAt = now;
            await Audit(actorId, "StockTransferCancelled", transfer.Id, new { transfer.TransferNumber, request.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    /// <summary>Closes out a dispatched/partially-received transfer whose remaining in-transit quantity
    /// will never arrive, writing off its value (Dr InventoryLossExpense / Cr Inventory) rather than
    /// leaving it stranded forever or silently restoring source stock. No StockMovement is posted here:
    /// the physical quantity is already correctly accounted for (TransferOut removed the full dispatched
    /// quantity from source; TransferIn only ever added what was actually received), so only the
    /// financial write-off is outstanding.</summary>
    public async Task<StockTransferDetailsDto> ResolveDiscrepancyAsync(Guid actorId, Guid id, ResolveStockTransferDiscrepancyRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.StockTransfersCancel, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("A reason is required to resolve a transfer discrepancy.");
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var transfer = await RequiredTransfer(id, ct);
            EnsureTransferBranchAccess(actor, transfer);
            if (transfer.Status is not (StockTransferStatus.Dispatched or StockTransferStatus.PartiallyReceived))
                throw new RequestValidationException("Only dispatched or partially received transfers have an outstanding discrepancy to resolve.");
            var shortfalls = transfer.Items.Where(x => x.QuantityDispatched > x.QuantityReceived).ToList();
            if (shortfalls.Count == 0) throw new RequestValidationException("There is no outstanding shortage on this transfer.");
            var shortQuantity = shortfalls.Sum(x => x.QuantityDispatched - x.QuantityReceived);
            var amount = decimal.Round(shortfalls.Sum(x => (x.QuantityDispatched - x.QuantityReceived) * x.UnitCostSnapshot), 2, MidpointRounding.AwayFromZero);
            if (amount > 0)
            {
                await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.StockWriteOff, transfer.Id, transfer.SourceBranchId,
                    timeProvider.GetUtcNow().UtcDateTime, transfer.TransferNumber, $"Stock transfer {transfer.TransferNumber} shortage write-off: {request.Reason}", actorId,
                    [new(AccountMappingKey.InventoryLossExpense, amount, 0), new(AccountMappingKey.Inventory, 0, amount)]), ct);
            }
            var now = timeProvider.GetUtcNow().UtcDateTime;
            transfer.Status = StockTransferStatus.Received;
            transfer.ReceivedByUserId = actorId;
            transfer.ReceivedAtUtc = now;
            transfer.UpdatedAt = now;
            transfer.Notes = AppendNote(transfer.Notes, $"Discrepancy resolved ({shortQuantity} unit(s) written off): {request.Reason}");
            await Audit(actorId, "StockTransferDiscrepancyResolved", transfer.Id,
                new { transfer.TransferNumber, ShortageQuantity = shortQuantity, WriteOffAmount = amount, request.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await Details(id, cancellationToken);
    }

    private async Task<List<StockTransferItem>> BuildItemsAsync(Guid sourceBranchId, Guid sourceGodownId, IReadOnlyList<StockTransferItemRequest> items, CancellationToken ct)
    {
        var built = new List<StockTransferItem>();
        foreach (var item in items)
        {
            var product = await repository.GetProductAsync(item.ProductId, ct);
            if (product is null || !product.IsActive) throw new RequestValidationException("Product is invalid or inactive.");
            var batch = await repository.GetBatchAsync(item.ProductBatchId, ct) ?? throw new RequestValidationException("Batch is invalid.");
            if (batch.ProductId != item.ProductId || batch.BranchId != sourceBranchId || batch.GodownId != sourceGodownId)
                throw new RequestValidationException("Batch does not belong to the selected source branch and godown.");
            if (batch.IsDisposed) throw new RequestValidationException("Disposed batches cannot be transferred.");
            built.Add(new StockTransferItem
            {
                ProductId = item.ProductId,
                SourceProductBatchId = batch.Id,
                BatchNumber = batch.BatchNumber,
                ExpiryDate = batch.ExpiryDate,
                UnitCostSnapshot = batch.PurchasePrice,
                QuantityRequested = item.QuantityRequested,
                Notes = Clean(item.Notes)
            });
        }
        return built;
    }

    private async Task<ProductBatch> BatchForTransferReceiptAsync(Branch destBranch, Guid destGodownId, StockTransferItem item, Guid actorId, CancellationToken ct)
    {
        var batch = await repository.GetBatchByNumberAsync(destBranch.Id, destGodownId, item.ProductId, item.BatchNumber, ct);
        if (batch is null)
        {
            var sourceBatch = await repository.GetBatchAsync(item.SourceProductBatchId, ct);
            batch = new ProductBatch
            {
                BranchId = destBranch.Id,
                GodownId = destGodownId,
                ProductId = item.ProductId,
                SupplierId = sourceBatch?.SupplierId,
                BatchNumber = item.BatchNumber,
                ManufacturingDate = sourceBatch?.ManufacturingDate,
                ExpiryDate = item.ExpiryDate,
                PurchasePrice = item.UnitCostSnapshot,
                RetailPrice = sourceBatch?.RetailPrice ?? item.UnitCostSnapshot,
                QuantityReceived = 0,
                QuantityAvailable = 0
            };
            await repository.AddBatchAsync(batch, ct);
            await Audit(actorId, "BatchCreated", batch.Id, new { batch.ProductId, batch.BranchId, batch.GodownId, batch.BatchNumber, Source = "StockTransferReceipt" }, ct);
        }
        else if (batch.IsDisposed)
        {
            throw new RequestValidationException($"Destination batch {batch.BatchNumber} has been disposed and cannot receive stock.");
        }
        return batch;
    }

    private async Task<Pharmacy.Domain.Entities.Inventory> InventoryFor(ProductBatch batch, Product product, CancellationToken ct)
    {
        var inventory = await repository.GetInventoryAsync(batch.BranchId, batch.ProductId, batch.Id, ct);
        if (inventory is not null) return inventory;
        inventory = new Pharmacy.Domain.Entities.Inventory { BranchId = batch.BranchId, GodownId = batch.GodownId, ProductId = batch.ProductId, ProductBatchId = batch.Id, ReorderLevel = product.ReorderLevel, QuantityInStock = 0, LastCountedAt = timeProvider.GetUtcNow().UtcDateTime };
        await repository.AddInventoryAsync(inventory, ct);
        return inventory;
    }

    private async Task ApplyOutboundAsync(ProductBatch batch, Pharmacy.Domain.Entities.Inventory inventory, int quantity, StockTransfer transfer, Guid actorId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        batch.QuantityAvailable -= quantity;
        batch.UpdatedAt = now;
        inventory.QuantityInStock -= quantity;
        inventory.UpdatedAt = now;
        await repository.AddMovementAsync(new StockMovement
        {
            MovementType = StockMovementType.TransferOut,
            BranchId = batch.BranchId,
            GodownId = batch.GodownId,
            ProductId = batch.ProductId,
            ProductBatchId = batch.Id,
            Quantity = -quantity,
            ReferenceType = "StockTransfer",
            ReferenceId = transfer.Id,
            Notes = transfer.TransferNumber,
            PerformedByUserId = actorId
        }, ct);
    }

    private async Task ApplyInboundAsync(ProductBatch batch, Pharmacy.Domain.Entities.Inventory inventory, int quantity, StockTransfer transfer, Guid actorId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        batch.QuantityReceived += quantity;
        batch.QuantityAvailable += quantity;
        batch.UpdatedAt = now;
        inventory.QuantityInStock += quantity;
        inventory.UpdatedAt = now;
        await repository.AddMovementAsync(new StockMovement
        {
            MovementType = StockMovementType.TransferIn,
            BranchId = batch.BranchId,
            GodownId = batch.GodownId,
            ProductId = batch.ProductId,
            ProductBatchId = batch.Id,
            Quantity = quantity,
            ReferenceType = "StockTransfer",
            ReferenceId = transfer.Id,
            Notes = transfer.TransferNumber,
            PerformedByUserId = actorId
        }, ct);
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
        if (!CanSelectBranch(actor) && actor.BranchId != branchId)
            throw new ForbiddenOperationException("The current user is not permitted to manage this branch.");
    }

    private static void EnsureTransferBranchAccess(User actor, StockTransfer transfer)
    {
        if (CanSelectBranch(actor)) return;
        if (actor.BranchId != transfer.SourceBranchId && actor.BranchId != transfer.DestinationBranchId)
            throw new ForbiddenOperationException("The current user is not permitted to manage this transfer.");
    }

    private async Task<Godown> RequiredValidGodownAsync(Guid branchId, Guid godownId, CancellationToken ct)
    {
        var godown = await godownAccess.GetGodownAsync(godownId, ct) ?? throw new RequestValidationException("Godown is invalid.");
        if (godown.BranchId != branchId) throw new RequestValidationException("Godown does not belong to the selected branch.");
        if (!godown.IsActive) throw new RequestValidationException("Godown is inactive.");
        return godown;
    }

    private async Task EnsureGodownAccessAsync(User actor, Guid branchId, Guid godownId, CancellationToken ct)
    {
        await RequiredValidGodownAsync(branchId, godownId, ct);
        if (CanSelectBranch(actor)) return;
        if (!await godownAccess.UserHasAccessAsync(actor.Id, godownId, ct))
            throw new ForbiddenOperationException("You do not have access to this godown.");
    }

    private async Task<StockTransfer> RequiredTransfer(Guid id, CancellationToken ct) =>
        await repository.GetTransferForUpdateAsync(id, ct) ?? throw new ResourceNotFoundException("Stock transfer was not found.");

    private async Task<Branch> RequiredActiveBranch(Guid branchId, CancellationToken ct)
    {
        var branch = await repository.GetBranchAsync(branchId, ct);
        if (branch is not { IsActive: true }) throw new RequestValidationException("Branch is invalid or inactive.");
        return branch;
    }

    private async Task<Product> RequiredProduct(Guid id, CancellationToken ct) =>
        await repository.GetProductAsync(id, ct) ?? throw new ResourceNotFoundException("Product was not found.");

    private async Task<StockTransferDetailsDto> Details(Guid id, CancellationToken ct) =>
        await repository.GetTransferDetailsAsync(id, ct) ?? throw new ResourceNotFoundException("Stock transfer was not found.");

    private void ValidateHeader(StockTransferRequest request)
    {
        if (request.SourceGodownId == request.DestinationGodownId)
            throw new RequestValidationException("Source and destination godown must be different.");
        if (request.Items.Count == 0) throw new RequestValidationException("At least one item is required.");
        if (request.Items.Any(x => x.QuantityRequested <= 0)) throw new RequestValidationException("Requested quantity must be greater than zero.");
        if (request.Items.Select(x => x.ProductBatchId).Distinct().Count() != request.Items.Count)
            throw new RequestValidationException("Duplicate batch lines are not allowed on a single transfer.");
        if (request.TransferDate > BusinessDate()) throw new RequestValidationException("Transfer date cannot be in the future.");
    }

    private DateOnly BusinessDate() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time")));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string AppendNote(string? existing, string addition) => string.IsNullOrWhiteSpace(existing) ? addition : $"{existing}\n{addition}";
    private Task Audit(Guid actor, string action, Guid id, object? current, CancellationToken ct) => repository.AddAuditAsync(new AuditLog
    { UserId = actor, Action = action, EntityType = "StockTransfer", EntityId = id, NewValues = current is null ? null : JsonSerializer.Serialize(current) }, ct);
    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
    }
}
