using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Inventory;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Inventory;

public sealed class InventoryService(
    IInventoryRepository repository,
    IFefoAllocationService fefo,
    IJournalPostingService journalPosting,
    TimeProvider timeProvider) : IInventoryService
{
    public async Task<PagedResult<InventoryListItemDto>> ListInventoryAsync(Guid actorId, InventoryListQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        if (!new[] { "productname", "quantity", "nearestexpiry", "stockvalue" }.Contains(query.SortBy.ToLowerInvariant()))
            throw new RequestValidationException("The requested inventory sort is not supported.");
        var scope = Scope(actor, query.BranchId);
        return await repository.ListInventoryAsync(query with { BranchId = scope.BranchId }, actor.BranchId, scope.CanSelectBranch, BusinessDate(), cancellationToken);
    }

    public async Task<InventoryDetailsDto> GetInventoryDetailsAsync(Guid actorId, Guid branchId, Guid productId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryView, cancellationToken);
        EnsureBranchAccess(actor, branchId);
        return await repository.GetInventoryDetailsAsync(branchId, productId, BusinessDate(), cancellationToken)
            ?? throw new ResourceNotFoundException("Inventory was not found.");
    }

    public async Task<PagedResult<BatchListItemDto>> ListBatchesAsync(Guid actorId, BatchListQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.BatchesView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        var scope = Scope(actor, query.BranchId);
        return await repository.ListBatchesAsync(query with { BranchId = scope.BranchId }, actor.BranchId, scope.CanSelectBranch, BusinessDate(), cancellationToken);
    }

    public async Task<IReadOnlyList<ExpiryListItemDto>> ListExpiryAsync(Guid actorId, ExpiryQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryView, cancellationToken);
        var scope = Scope(actor, query.BranchId);
        return await repository.ListExpiryAsync(query with { BranchId = scope.BranchId }, actor.BranchId, scope.CanSelectBranch, BusinessDate(), cancellationToken);
    }

    public async Task<PagedResult<StockMovementListItemDto>> ListMovementsAsync(Guid actorId, StockMovementListQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryMovementsView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        var scope = Scope(actor, query.BranchId);
        return await repository.ListMovementsAsync(query with { BranchId = scope.BranchId }, actor.BranchId, scope.CanSelectBranch, cancellationToken);
    }

    public async Task<InventoryDetailsDto> AddOpeningStockAsync(Guid actorId, OpeningStockRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryOpeningStock, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        ValidateOpening(request, BusinessDate());
        ProductBatch? batch = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var branch = await repository.GetBranchAsync(request.BranchId, ct);
            var product = await repository.GetProductAsync(request.ProductId, ct);
            if (branch is null || !branch.IsActive) throw new RequestValidationException("Branch is invalid or inactive.");
            if (product is null || !product.IsActive) throw new RequestValidationException("Product is invalid or inactive.");
            if (request.SupplierId.HasValue && await repository.GetSupplierAsync(request.SupplierId.Value, ct) is null)
                throw new RequestValidationException("Supplier is invalid.");

            batch = await repository.GetBatchByNumberAsync(request.BranchId, request.ProductId, request.BatchNumber.Trim(), ct);
            if (batch is null)
            {
                batch = new ProductBatch
                {
                    BranchId = request.BranchId,
                    ProductId = request.ProductId,
                    SupplierId = request.SupplierId,
                    BatchNumber = request.BatchNumber.Trim(),
                    ManufacturingDate = request.ManufacturingDate,
                    ExpiryDate = request.ExpiryDate,
                    PurchasePrice = request.PurchasePrice,
                    RetailPrice = request.RetailPrice,
                    QuantityReceived = 0,
                    QuantityAvailable = 0
                };
                await repository.AddBatchAsync(batch, ct);
                await Audit(actorId, "BatchCreated", "ProductBatch", batch.Id, null, BatchValues(batch), ct);
            }
            else if (batch.IsDisposed || batch.ExpiryDate < BusinessDate())
            {
                throw new RequestValidationException("Opening stock cannot be added to an expired or disposed batch.");
            }

            var movement = await ApplyDelta(batch, await InventoryFor(batch, product, ct), StockMovementType.OpeningStock, request.Quantity, actorId, "OpeningStock", request.Notes, ct);
            await PostInventoryJournalAsync(movement, batch, JournalSourceType.OpeningBalance, actorId, ct);
            await Audit(actorId, "OpeningStockAdded", "ProductBatch", batch.Id, null, new { batch.ProductId, batch.BranchId, Quantity = request.Quantity, batch.QuantityAvailable }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetInventoryDetailsAsync(request.BranchId, request.ProductId, BusinessDate(), cancellationToken)
            ?? throw new ResourceNotFoundException("Inventory was not found.");
    }

    public Task<InventoryDetailsDto> AdjustStockIncreaseAsync(Guid actorId, StockAdjustmentRequest request, CancellationToken cancellationToken = default) =>
        Adjust(actorId, request, StockMovementType.AdjustmentIncrease, "StockAdjustedIncrease", cancellationToken);

    public Task<InventoryDetailsDto> AdjustStockDecreaseAsync(Guid actorId, StockAdjustmentRequest request, CancellationToken cancellationToken = default) =>
        Adjust(actorId, request, request.Reason == AdjustmentReason.Damaged ? StockMovementType.Damaged : StockMovementType.AdjustmentDecrease, request.Reason == AdjustmentReason.Damaged ? "DamagedStockRemoved" : "StockAdjustedDecrease", cancellationToken);

    public async Task<InventoryDetailsDto> ReconcileStockCountAsync(Guid actorId, StockCountRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryStockCount, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        if (request.PhysicalQuantity < 0) throw new RequestValidationException("Physical quantity cannot be negative.");
        if (string.IsNullOrWhiteSpace(request.Notes)) throw new RequestValidationException("Stock count note is required.");
        var businessDate = BusinessDate();
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var batch = await RequiredBatch(request.ProductBatchId, ct);
            EnsureBatchMatches(batch, request.BranchId, request.ProductId);
            var product = await RequiredProduct(request.ProductId, ct);
            var inventory = await RequiredInventory(batch, ct);
            var variance = request.PhysicalQuantity - batch.QuantityAvailable;
            if (variance != 0)
            {
                var movementType = variance > 0 ? StockMovementType.AdjustmentIncrease : StockMovementType.AdjustmentDecrease;
                var movement = await ApplyDelta(batch, inventory, movementType, Math.Abs(variance), actorId, "StockCount", request.Notes, ct);
                await PostInventoryJournalAsync(movement, batch, JournalSourceType.StockAdjustment, actorId, ct);
            }
            inventory.LastCountedAt = timeProvider.GetUtcNow().UtcDateTime;
            await Audit(actorId, "StockCountReconciled", "ProductBatch", batch.Id, new { SystemQuantity = batch.QuantityAvailable - variance }, new { request.PhysicalQuantity, Variance = variance, request.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetInventoryDetailsAsync(request.BranchId, request.ProductId, businessDate, cancellationToken)
            ?? throw new ResourceNotFoundException("Inventory was not found.");
    }

    public async Task<InventoryDetailsDto> DisposeExpiredStockAsync(Guid actorId, DisposeExpiredStockRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryExpiryManage, cancellationToken);
        if (request.Quantity <= 0) throw new RequestValidationException("Quantity must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("Reason is required.");
        ProductBatch? batch = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            batch = await RequiredBatch(request.ProductBatchId, ct);
            EnsureBranchAccess(actor, batch.BranchId);
            if (batch.ExpiryDate >= BusinessDate()) throw new RequestValidationException("Only expired batches can be disposed through expired-stock disposal.");
            var inventory = await RequiredInventory(batch, ct);
            EnsureAvailable(batch, request.Quantity);
            var movement = await ApplyDelta(batch, inventory, StockMovementType.Expired, request.Quantity, actorId, "ExpiredStockDisposal", request.Reason, ct);
            await PostInventoryJournalAsync(movement, batch, JournalSourceType.StockWriteOff, actorId, ct);
            if (batch.QuantityAvailable == 0) batch.IsDisposed = true;
            await Audit(actorId, "ExpiredStockDisposed", "ProductBatch", batch.Id, null, new { batch.ProductId, batch.BranchId, request.Quantity, batch.QuantityAvailable, request.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetInventoryDetailsAsync(batch!.BranchId, batch.ProductId, BusinessDate(), cancellationToken)
            ?? throw new ResourceNotFoundException("Inventory was not found.");
    }

    public async Task<FefoPreviewDto> PreviewFefoAsync(Guid actorId, FefoPreviewRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryView, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        var results = fefo.Allocate(await repository.GetEligibleBatchesAsync(request.BranchId, request.ProductId, cancellationToken),
            request.ProductId, request.BranchId, request.Quantity, request.BusinessDate ?? BusinessDate(), cancellationToken);
        return new FefoPreviewDto(results.Select(x => new FefoPreviewItemDto(x.BatchId, x.BatchNumber, x.AllocatedQuantity, x.ExpiryDate)).ToList());
    }

    public async Task<IReadOnlyList<InventoryIntegrityIssueDto>> CheckIntegrityAsync(Guid actorId, Guid? branchId, Guid? productId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryView, cancellationToken);
        if (branchId.HasValue) EnsureBranchAccess(actor, branchId.Value);
        return await repository.CheckIntegrityAsync(branchId ?? (CanSelectBranch(actor) ? null : actor.BranchId), productId, cancellationToken);
    }

    public async Task<InventoryOptionsDto> GetOptionsAsync(Guid actorId, string? productSearch, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryView, cancellationToken);
        return await repository.GetOptionsAsync(productSearch, actor.BranchId, CanSelectBranch(actor), cancellationToken);
    }

    public async Task<StockCountSessionDto> CreateStockCountSessionAsync(Guid actorId, CreateStockCountSessionRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryStockCount, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        var businessDate = BusinessDate();
        if (request.CountDate > businessDate) throw new RequestValidationException("Count date cannot be in the future.");
        if (request.Scope == StockCountScope.Category && request.CategoryId is null)
            throw new RequestValidationException("Category is required for a category-based stock count.");
        if (request.Scope == StockCountScope.SelectedProducts && (request.ProductIds is null || request.ProductIds.Count == 0))
            throw new RequestValidationException("At least one product is required for a selected-products stock count.");
        if (request.Scope == StockCountScope.SelectedBatches && (request.ProductBatchIds is null || request.ProductBatchIds.Count == 0))
            throw new RequestValidationException("At least one batch is required for a selected-batches stock count.");

        StockCountSession? session = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var batches = await repository.GetEligibleBatchesForCountAsync(request.BranchId, request.Scope, request.CategoryId, request.ProductIds, request.ProductBatchIds, ct);
            if (batches.Count == 0) throw new RequestValidationException("No eligible batches were found for the requested stock count scope.");
            session = new StockCountSession
            {
                CountNumber = await repository.NextStockCountNumberAsync(request.CountDate, ct),
                BranchId = request.BranchId,
                CountDate = request.CountDate,
                Status = StockCountStatus.Draft,
                Scope = request.Scope,
                CategoryId = request.CategoryId,
                Notes = request.Notes,
                CreatedByUserId = actorId,
                Items = batches.Select(batch => new StockCountItem
                {
                    ProductId = batch.ProductId,
                    ProductBatchId = batch.Id,
                    SystemQuantity = batch.QuantityAvailable,
                    UnitCostSnapshot = batch.PurchasePrice
                }).ToList()
            };
            await repository.AddStockCountSessionAsync(session, ct);
            await Audit(actorId, "StockCountSessionCreated", "StockCountSession", session.Id, null, new { session.CountNumber, session.BranchId, session.Scope, ItemCount = session.Items.Count }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetStockCountSessionDetailsAsync(session!.Id, cancellationToken) ?? throw new ResourceNotFoundException("Stock count session was not found.");
    }

    public async Task<StockCountSessionDto> StartStockCountSessionAsync(Guid actorId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryStockCount, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var session = await RequiredSession(sessionId, ct);
            EnsureBranchAccess(actor, session.BranchId);
            if (session.Status != StockCountStatus.Draft) throw new RequestValidationException("Only draft stock count sessions can be started.");
            session.Status = StockCountStatus.InProgress;
            session.StartedByUserId = actorId;
            session.StartedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            session.UpdatedAt = session.StartedAtUtc.Value;
            await Audit(actorId, "StockCountSessionStarted", "StockCountSession", session.Id, null, new { session.CountNumber }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetStockCountSessionDetailsAsync(sessionId, cancellationToken) ?? throw new ResourceNotFoundException("Stock count session was not found.");
    }

    public async Task<StockCountSessionDto> SubmitStockCountEntriesAsync(Guid actorId, Guid sessionId, SubmitStockCountEntriesRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryStockCount, cancellationToken);
        if (request.Entries.Count == 0) throw new RequestValidationException("At least one count entry is required.");
        if (request.Entries.Any(x => x.CountedQuantity < 0)) throw new RequestValidationException("Counted quantity cannot be negative.");
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var session = await RequiredSession(sessionId, ct);
            EnsureBranchAccess(actor, session.BranchId);
            if (session.Status != StockCountStatus.InProgress) throw new RequestValidationException("Stock counts can only be entered while the session is in progress.");
            var now = timeProvider.GetUtcNow().UtcDateTime;
            foreach (var entry in request.Entries)
            {
                var item = session.Items.FirstOrDefault(x => x.Id == entry.StockCountItemId)
                    ?? throw new RequestValidationException("One or more count entries do not belong to this session.");
                item.CountedQuantity = entry.CountedQuantity;
                item.Reason = entry.Reason?.ToString();
                item.Notes = entry.Notes;
                item.CountedByUserId = actorId;
                item.CountedAtUtc = now;
                item.UpdatedAt = now;
            }
            session.UpdatedAt = now;
            await Audit(actorId, "StockCountEntriesSubmitted", "StockCountSession", session.Id, null, new { session.CountNumber, Entries = request.Entries.Count }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetStockCountSessionDetailsAsync(sessionId, cancellationToken) ?? throw new ResourceNotFoundException("Stock count session was not found.");
    }

    public async Task<StockCountSessionDto> FinalizeStockCountSessionAsync(Guid actorId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryStockCountFinalize, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var session = await RequiredSession(sessionId, ct);
            EnsureBranchAccess(actor, session.BranchId);
            if (session.Status != StockCountStatus.InProgress) throw new RequestValidationException("Only in-progress stock count sessions can be finalized.");
            foreach (var item in session.Items)
            {
                if (!item.CountedQuantity.HasValue || item.CountedQuantity.Value == item.SystemQuantity) continue;
                var batch = await RequiredBatch(item.ProductBatchId, ct);
                if (batch.IsDisposed) continue;
                var variance = item.CountedQuantity.Value - item.SystemQuantity;
                var movementType = variance > 0 ? StockMovementType.AdjustmentIncrease : StockMovementType.AdjustmentDecrease;
                var inventory = await RequiredInventory(batch, ct);
                var movement = await ApplyDelta(batch, inventory, movementType, Math.Abs(variance), actorId, "StockCount", $"Stock count {session.CountNumber}", ct);
                await PostInventoryJournalAsync(movement, batch, JournalSourceType.StockAdjustment, actorId, ct);
            }
            session.Status = StockCountStatus.Completed;
            session.CompletedByUserId = actorId;
            session.CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            session.UpdatedAt = session.CompletedAtUtc.Value;
            await Audit(actorId, "StockCountSessionFinalized", "StockCountSession", session.Id, null,
                new { session.CountNumber, TotalItems = session.Items.Count, CountedItems = session.Items.Count(x => x.CountedQuantity.HasValue),
                    VarianceItems = session.Items.Count(x => x.CountedQuantity.HasValue && x.CountedQuantity != x.SystemQuantity) }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetStockCountSessionDetailsAsync(sessionId, cancellationToken) ?? throw new ResourceNotFoundException("Stock count session was not found.");
    }

    public async Task<StockCountSessionDto> CancelStockCountSessionAsync(Guid actorId, Guid sessionId, CancelStockCountSessionRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryStockCount, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new RequestValidationException("Cancellation reason is required.");
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var session = await RequiredSession(sessionId, ct);
            EnsureBranchAccess(actor, session.BranchId);
            if (session.Status is StockCountStatus.Completed or StockCountStatus.Cancelled)
                throw new RequestValidationException("Only draft or in-progress stock count sessions can be cancelled.");
            session.Status = StockCountStatus.Cancelled;
            session.CancelledByUserId = actorId;
            session.CancelledAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            session.UpdatedAt = session.CancelledAtUtc.Value;
            await Audit(actorId, "StockCountSessionCancelled", "StockCountSession", session.Id, null, new { session.CountNumber, request.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetStockCountSessionDetailsAsync(sessionId, cancellationToken) ?? throw new ResourceNotFoundException("Stock count session was not found.");
    }

    public async Task<StockCountSessionDto> GetStockCountSessionAsync(Guid actorId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryStockCountView, cancellationToken);
        var dto = await repository.GetStockCountSessionDetailsAsync(sessionId, cancellationToken) ?? throw new ResourceNotFoundException("Stock count session was not found.");
        EnsureBranchAccess(actor, dto.BranchId);
        return dto;
    }

    public async Task<PagedResult<StockCountSessionListItemDto>> ListStockCountSessionsAsync(Guid actorId, StockCountSessionListQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryStockCountView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        var scope = Scope(actor, query.BranchId);
        return await repository.ListStockCountSessionsAsync(query with { BranchId = scope.BranchId }, actor.BranchId, scope.CanSelectBranch, cancellationToken);
    }

    public async Task<IReadOnlyList<StockCountItemDto>> GetStockCountDiscrepanciesAsync(Guid actorId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryStockCountView, cancellationToken);
        var dto = await repository.GetStockCountSessionDetailsAsync(sessionId, cancellationToken) ?? throw new ResourceNotFoundException("Stock count session was not found.");
        EnsureBranchAccess(actor, dto.BranchId);
        return dto.Items.Where(x => x.Variance is not null and not 0).OrderByDescending(x => Math.Abs(x.VarianceValue ?? 0)).ToList();
    }

    private async Task<StockCountSession> RequiredSession(Guid sessionId, CancellationToken ct) =>
        await repository.GetStockCountSessionForUpdateAsync(sessionId, ct) ?? throw new ResourceNotFoundException("Stock count session was not found.");

    private const int LargeAdjustmentAbsoluteThreshold = 100;
    private const decimal LargeAdjustmentRelativeThreshold = 0.5m;

    private async Task<InventoryDetailsDto> Adjust(Guid actorId, StockAdjustmentRequest request, StockMovementType movementType, string auditAction, CancellationToken cancellationToken)
    {
        var actor = await Require(actorId, PermissionCatalog.InventoryAdjust, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        ValidateAdjustment(request);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var batch = await RequiredBatch(request.ProductBatchId, ct);
            EnsureBatchMatches(batch, request.BranchId, request.ProductId);
            var inventory = await RequiredInventory(batch, ct);
            if (movementType is StockMovementType.AdjustmentDecrease or StockMovementType.Damaged) EnsureAvailable(batch, request.Quantity);
            var priorAvailable = batch.QuantityAvailable;
            var significant = IsSignificantAdjustment(request.Quantity, priorAvailable);
            var movement = await ApplyDelta(batch, inventory, movementType, request.Quantity, actorId, movementType == StockMovementType.Damaged ? "DamagedStock" : "StockAdjustment", request.Notes ?? request.Reason.ToString(), ct);
            await PostInventoryJournalAsync(movement, batch,
                movementType == StockMovementType.Damaged ? JournalSourceType.StockWriteOff : JournalSourceType.StockAdjustment, actorId, ct);
            await Audit(actorId, significant ? $"{auditAction}Significant" : auditAction, "ProductBatch", batch.Id,
                new { QuantityBeforeAdjustment = priorAvailable },
                new { batch.ProductId, batch.BranchId, request.Quantity, request.Reason, batch.QuantityAvailable, Significant = significant }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await repository.GetInventoryDetailsAsync(request.BranchId, request.ProductId, BusinessDate(), cancellationToken)
            ?? throw new ResourceNotFoundException("Inventory was not found.");
    }

    /// <summary>
    /// A manual stock adjustment is flagged significant (and gets a distinct audit action so it stands
    /// out in the audit log) when it moves a large absolute quantity or a large share of the batch's
    /// current stock, regardless of direction.
    /// </summary>
    private static bool IsSignificantAdjustment(int quantity, int priorAvailable) =>
        quantity >= LargeAdjustmentAbsoluteThreshold || (priorAvailable > 0 && quantity >= priorAvailable * LargeAdjustmentRelativeThreshold);

    private async Task<User> Require(Guid actorId, string permission, CancellationToken cancellationToken)
    {
        var actor = await repository.GetActorAsync(actorId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private (Guid? BranchId, bool CanSelectBranch) Scope(User actor, Guid? requestedBranchId)
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

    private static void ValidateOpening(OpeningStockRequest request, DateOnly businessDate)
    {
        if (string.IsNullOrWhiteSpace(request.BatchNumber)) throw new RequestValidationException("Batch number is required.");
        if (request.Quantity <= 0) throw new RequestValidationException("Quantity must be greater than zero.");
        if (request.PurchasePrice < 0 || request.RetailPrice < 0) throw new RequestValidationException("Prices cannot be negative.");
        if (request.ManufacturingDate.HasValue && request.ManufacturingDate.Value > request.ExpiryDate)
            throw new RequestValidationException("Manufacturing date cannot be after expiry date.");
        if (request.ExpiryDate < businessDate)
            throw new RequestValidationException("Expired batches cannot be entered as sellable opening stock.");
    }

    private static void ValidateAdjustment(StockAdjustmentRequest request)
    {
        if (request.Quantity <= 0) throw new RequestValidationException("Quantity must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.Notes) && request.Reason == AdjustmentReason.Other)
            throw new RequestValidationException("Reason notes are required when reason is Other.");
    }

    private DateOnly BusinessDate() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time")));

    private async Task<Product> RequiredProduct(Guid productId, CancellationToken ct) =>
        await repository.GetProductAsync(productId, ct) ?? throw new ResourceNotFoundException("Product was not found.");

    private async Task<ProductBatch> RequiredBatch(Guid batchId, CancellationToken ct) =>
        await repository.GetBatchAsync(batchId, ct) ?? throw new ResourceNotFoundException("Batch was not found.");

    private async Task<Pharmacy.Domain.Entities.Inventory> RequiredInventory(ProductBatch batch, CancellationToken ct) =>
        await repository.GetInventoryAsync(batch.BranchId, batch.ProductId, batch.Id, ct) ?? throw new ResourceNotFoundException("Inventory balance was not found.");

    private async Task<Pharmacy.Domain.Entities.Inventory> InventoryFor(ProductBatch batch, Product product, CancellationToken ct)
    {
        var inventory = await repository.GetInventoryAsync(batch.BranchId, batch.ProductId, batch.Id, ct);
        if (inventory is not null) return inventory;
        inventory = new Pharmacy.Domain.Entities.Inventory { BranchId = batch.BranchId, ProductId = batch.ProductId, ProductBatchId = batch.Id, ReorderLevel = product.ReorderLevel, QuantityInStock = 0, LastCountedAt = timeProvider.GetUtcNow().UtcDateTime };
        await repository.AddInventoryAsync(inventory, ct);
        return inventory;
    }

    private async Task<StockMovement> ApplyDelta(ProductBatch batch, Pharmacy.Domain.Entities.Inventory inventory, StockMovementType movementType, int absoluteQuantity, Guid actorId, string referenceType, string? notes, CancellationToken ct)
    {
        var signed = movementType is StockMovementType.OpeningStock or StockMovementType.Purchase or StockMovementType.SaleReturn or StockMovementType.TransferIn or StockMovementType.AdjustmentIncrease
            ? absoluteQuantity : -absoluteQuantity;
        StockMovement.ValidateQuantityForMovementType(movementType, signed);
        if (batch.QuantityAvailable + signed < 0 || inventory.QuantityInStock + signed < 0)
            throw new RequestValidationException("Insufficient stock in selected batch.");
        batch.QuantityReceived += signed > 0 ? signed : 0;
        batch.QuantityAvailable += signed;
        batch.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        inventory.QuantityInStock += signed;
        inventory.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        var movement = new StockMovement
        {
            MovementType = movementType,
            BranchId = batch.BranchId,
            ProductId = batch.ProductId,
            ProductBatchId = batch.Id,
            Quantity = signed,
            ReferenceType = referenceType,
            Notes = notes,
            PerformedByUserId = actorId
        };
        await repository.AddMovementAsync(movement, ct);
        return movement;
    }

    private async Task PostInventoryJournalAsync(StockMovement movement, ProductBatch batch, JournalSourceType sourceType, Guid actorId, CancellationToken ct)
    {
        var amount = decimal.Round(Math.Abs(movement.Quantity) * batch.PurchasePrice, 2, MidpointRounding.AwayFromZero);
        if (amount == 0) return;
        var increase = movement.Quantity > 0;
        var counterpart = sourceType == JournalSourceType.OpeningBalance
            ? AccountMappingKey.RetainedEarnings
            : increase ? AccountMappingKey.InventoryAdjustmentGain : AccountMappingKey.InventoryLossExpense;
        var lines = increase
            ? new List<JournalLineInput> { new(AccountMappingKey.Inventory, amount, 0), new(counterpart, 0, amount) }
            : new List<JournalLineInput> { new(counterpart, amount, 0), new(AccountMappingKey.Inventory, 0, amount) };
        await journalPosting.PostAsync(new JournalPostingRequest(sourceType, movement.Id, movement.BranchId, timeProvider.GetUtcNow().UtcDateTime,
            movement.ReferenceType, movement.Notes ?? $"{movement.MovementType} inventory posting", actorId, lines), ct);
    }

    private static void EnsureBatchMatches(ProductBatch batch, Guid branchId, Guid productId)
    {
        if (batch.BranchId != branchId || batch.ProductId != productId)
            throw new RequestValidationException("Batch does not belong to the requested branch and product.");
        if (batch.IsDisposed) throw new RequestValidationException("Disposed batches cannot be adjusted.");
    }

    private static void EnsureAvailable(ProductBatch batch, int quantity)
    {
        if (batch.QuantityAvailable < quantity) throw new RequestValidationException("Insufficient stock in selected batch.");
    }

    private Task Audit(Guid actor, string action, string type, Guid id, object? old, object? current, CancellationToken ct) => repository.AddAuditAsync(new AuditLog
    { UserId = actor, Action = action, EntityType = type, EntityId = id, OldValues = old is null ? null : JsonSerializer.Serialize(old), NewValues = current is null ? null : JsonSerializer.Serialize(current) }, ct);
    private static object BatchValues(ProductBatch x) => new { x.ProductId, x.BranchId, x.SupplierId, x.BatchNumber, x.ManufacturingDate, x.ExpiryDate, x.PurchasePrice, x.RetailPrice, x.QuantityReceived, x.QuantityAvailable, x.IsDisposed };
    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
    }
}
