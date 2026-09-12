using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.StockTransfers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Godowns;
using Pharmacy.Application.Services.StockTransfers;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Tests;

public sealed class StockTransferManagementTests
{
    private static readonly DateOnly Expiry = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(90);

    [Fact]
    public async Task Create_snapshots_batch_identity_and_leaves_transfer_in_draft_with_no_stock_movement()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate);
        var batch = f.AddSourceBatch(50, "LOT-1");
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 20));

        Assert.Equal(StockTransferStatus.Draft, created.Status);
        Assert.StartsWith("TRF-", created.TransferNumber);
        var item = Assert.Single(created.Items);
        Assert.Equal("LOT-1", item.BatchNumber);
        Assert.Equal(batch.ExpiryDate, item.ExpiryDate);
        Assert.Equal(batch.PurchasePrice, item.UnitCostSnapshot);
        Assert.Equal(20, item.QuantityRequested);
        Assert.Equal(0, item.QuantityDispatched);
        Assert.Empty(f.Movements);
        Assert.Equal(50, batch.QuantityAvailable);
    }

    [Fact]
    public async Task Create_rejects_identical_source_and_destination_godown()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate);
        var batch = f.AddSourceBatch(50);
        var request = f.Request(batch, 10) with { DestinationGodownId = f.SourceGodown.Id };
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateTransferAsync(f.Actor.Id, request));
    }

    [Fact]
    public async Task Create_is_rejected_when_actor_lacks_access_to_the_source_godown()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate);
        var batch = f.AddSourceBatch(50);
        f.Actor.Role!.Name = RoleCatalog.StoreKeeper;
        f.GodownAccess.AccessPredicate = (_, godownId) => godownId != f.SourceGodown.Id;
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10)));
    }

    [Fact]
    public async Task Create_rejects_a_batch_that_does_not_belong_to_the_selected_source_godown()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate);
        var otherGodown = f.AddGodown(f.BranchA, "OTHER");
        var batch = new ProductBatch { BranchId = f.BranchA.Id, GodownId = otherGodown.Id, ProductId = f.Product.Id, BatchNumber = "X", ExpiryDate = Expiry, QuantityAvailable = 10, QuantityReceived = 10, PurchasePrice = 8, RetailPrice = 12 };
        f.Batches.Add(batch);
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 5)));
    }

    [Fact]
    public async Task Missing_permission_is_forbidden()
    {
        var f = new Fixture();
        var batch = f.AddSourceBatch(50);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10)));
    }

    [Fact]
    public async Task Full_workflow_request_approve_dispatch_receive_moves_stock_and_creates_transfer_movements()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch, PermissionCatalog.StockTransfersReceive);
        var batch = f.AddSourceBatch(50, "LOT-1");
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 20));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        var approved = await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));
        Assert.Equal(StockTransferStatus.Approved, approved.Status);
        Assert.Equal(20, approved.Items.Single().QuantityApproved);

        var dispatched = await f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null));
        Assert.Equal(StockTransferStatus.Dispatched, dispatched.Status);
        Assert.Equal(30, batch.QuantityAvailable);
        var outMovement = Assert.Single(f.Movements, x => x.MovementType == StockMovementType.TransferOut);
        Assert.Equal(-20, outMovement.Quantity);
        Assert.Equal(f.SourceGodown.Id, outMovement.GodownId);
        Assert.Equal("StockTransfer", outMovement.ReferenceType);
        Assert.Equal(created.Id, outMovement.ReferenceId);
        // Destination is untouched until receipt.
        Assert.DoesNotContain(f.Movements, x => x.MovementType == StockMovementType.TransferIn);

        var itemId = dispatched.Items.Single().Id;
        var received = await f.Service.ReceiveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 20)], null));
        Assert.Equal(StockTransferStatus.Received, received.Status);
        var inMovement = Assert.Single(f.Movements, x => x.MovementType == StockMovementType.TransferIn);
        Assert.Equal(20, inMovement.Quantity);
        Assert.Equal(f.DestGodown.Id, inMovement.GodownId);

        var destBatch = f.Batches.Single(x => x.GodownId == f.DestGodown.Id);
        Assert.Equal("LOT-1", destBatch.BatchNumber);
        Assert.Equal(batch.ExpiryDate, destBatch.ExpiryDate);
        Assert.Equal(20, destBatch.QuantityAvailable);
        Assert.Equal(received.Items.Single().DestinationProductBatchId, destBatch.Id);
    }

    [Fact]
    public async Task Duplicate_dispatch_submission_is_rejected_before_any_stock_effect()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch);
        var batch = f.AddSourceBatch(50, "LOT-1");
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 20));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));

        f.DuplicateGuard.Reject = true;
        await Assert.ThrowsAsync<ResourceConflictException>(() => f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null)));
        Assert.Equal(1, f.DuplicateGuard.CallCount);
        Assert.Equal(50, batch.QuantityAvailable);
        Assert.DoesNotContain(f.Movements, x => x.MovementType == StockMovementType.TransferOut);
    }

    [Fact]
    public async Task Approving_less_than_requested_caps_dispatch_and_leaves_the_remainder_unapproved()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch);
        var batch = f.AddSourceBatch(50);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 20));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        var itemId = created.Items.Single().Id;
        var approved = await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 12)]));
        Assert.Equal(12, approved.Items.Single().QuantityApproved);

        var dispatched = await f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null));
        Assert.Equal(12, dispatched.Items.Single().QuantityDispatched);
        Assert.Equal(38, batch.QuantityAvailable);
    }

    [Fact]
    public async Task Approve_rejects_a_quantity_above_what_was_requested()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest, PermissionCatalog.StockTransfersApprove);
        var batch = f.AddSourceBatch(50);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        var itemId = created.Items.Single().Id;
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 11)])));
    }

    [Fact]
    public async Task Dispatch_is_blocked_when_source_stock_is_insufficient()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch);
        var batch = f.AddSourceBatch(10);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));
        batch.QuantityAvailable = 3; // stock moved out from under the transfer after approval, before dispatch
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null)));
        Assert.Empty(f.Movements);
    }

    [Fact]
    public async Task Dispatch_is_blocked_when_actor_lacks_access_to_the_source_godown()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch);
        var batch = f.AddSourceBatch(50);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));
        f.Actor.Role!.Name = RoleCatalog.StoreKeeper;
        f.GodownAccess.AccessPredicate = (_, godownId) => godownId != f.SourceGodown.Id;
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null)));
    }

    [Fact]
    public async Task Duplicate_dispatch_of_an_already_dispatched_transfer_is_blocked()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch);
        var batch = f.AddSourceBatch(50);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));
        await f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null)));
        Assert.Single(f.Movements, x => x.MovementType == StockMovementType.TransferOut);
    }

    [Fact]
    public async Task Receive_is_blocked_when_actor_lacks_access_to_the_destination_godown()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch, PermissionCatalog.StockTransfersReceive);
        var batch = f.AddSourceBatch(50);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));
        var dispatched = await f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null));
        f.Actor.Role!.Name = RoleCatalog.StoreKeeper;
        f.GodownAccess.AccessPredicate = (_, godownId) => godownId != f.DestGodown.Id;
        var itemId = dispatched.Items.Single().Id;
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => f.Service.ReceiveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 10)], null)));
    }

    [Fact]
    public async Task Partial_receipt_then_final_receipt_transitions_through_PartiallyReceived_to_Received()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch, PermissionCatalog.StockTransfersReceive);
        var batch = f.AddSourceBatch(100);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 100));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));
        var dispatched = await f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null));
        var itemId = dispatched.Items.Single().Id;

        var firstReceipt = await f.Service.ReceiveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 80)], null));
        Assert.Equal(StockTransferStatus.PartiallyReceived, firstReceipt.Status);
        Assert.Equal(80, firstReceipt.Items.Single().QuantityReceived);
        Assert.Equal(20, firstReceipt.Items.Single().QuantityInTransit);

        var secondReceipt = await f.Service.ReceiveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 20)], null));
        Assert.Equal(StockTransferStatus.Received, secondReceipt.Status);
        Assert.Equal(100, secondReceipt.Items.Single().QuantityReceived);
        Assert.Equal(0, secondReceipt.Items.Single().QuantityInTransit);
        Assert.Equal(2, f.Movements.Count(x => x.MovementType == StockMovementType.TransferIn));

        var destBatch = f.Batches.Single(x => x.GodownId == f.DestGodown.Id);
        Assert.Equal(100, destBatch.QuantityAvailable);
    }

    [Fact]
    public async Task Over_receipt_beyond_dispatched_quantity_is_blocked()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch, PermissionCatalog.StockTransfersReceive);
        var batch = f.AddSourceBatch(50);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 20));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));
        var dispatched = await f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null));
        var itemId = dispatched.Items.Single().Id;
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ReceiveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 25)], null)));
    }

    [Fact]
    public async Task Duplicate_receipt_retry_beyond_what_remains_in_transit_is_blocked()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch, PermissionCatalog.StockTransfersReceive);
        var batch = f.AddSourceBatch(50);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 20));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));
        var dispatched = await f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null));
        var itemId = dispatched.Items.Single().Id;
        await f.Service.ReceiveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 20)], null));
        // Retrying the same receipt after the transfer is already fully received must not double-post.
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ReceiveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 20)], null)));
        Assert.Single(f.Movements, x => x.MovementType == StockMovementType.TransferIn);
    }

    [Fact]
    public async Task Cancel_is_allowed_before_dispatch_but_blocked_afterwards()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch, PermissionCatalog.StockTransfersCancel);
        var batch = f.AddSourceBatch(50);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        var cancelled = await f.Service.CancelTransferAsync(f.Actor.Id, created.Id, new("changed my mind"));
        Assert.Equal(StockTransferStatus.Cancelled, cancelled.Status);

        var second = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10));
        await f.Service.RequestTransferAsync(f.Actor.Id, second.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, second.Id, new(null));
        await f.Service.DispatchTransferAsync(f.Actor.Id, second.Id, new(null));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.CancelTransferAsync(f.Actor.Id, second.Id, new("too late")));
    }

    [Fact]
    public async Task Resolve_discrepancy_writes_off_the_shortage_and_closes_the_transfer()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch,
            PermissionCatalog.StockTransfersReceive, PermissionCatalog.StockTransfersCancel);
        var batch = f.AddSourceBatch(100);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 100));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));
        var dispatched = await f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null));
        var itemId = dispatched.Items.Single().Id;
        await f.Service.ReceiveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 80)], null));

        var resolved = await f.Service.ResolveDiscrepancyAsync(f.Actor.Id, created.Id, new("lost in transit"));
        Assert.Equal(StockTransferStatus.Received, resolved.Status);
        var posting = Assert.Single(f.Journal.Posted);
        Assert.Equal(JournalSourceType.StockWriteOff, posting.SourceType);
        Assert.Equal(2, posting.Lines.Count);
        Assert.Equal(20 * batch.PurchasePrice, posting.Lines.Sum(x => x.Debit));
        Assert.Equal(20 * batch.PurchasePrice, posting.Lines.Sum(x => x.Credit));
        // No further stock movement is posted for the write-off - the physical quantity was already correct.
        Assert.Equal(2, f.Movements.Count);
    }

    [Fact]
    public async Task Resolve_discrepancy_is_rejected_when_nothing_is_outstanding()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch,
            PermissionCatalog.StockTransfersReceive, PermissionCatalog.StockTransfersCancel);
        var batch = f.AddSourceBatch(50);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));
        var dispatched = await f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null));
        var itemId = dispatched.Items.Single().Id;
        await f.Service.ReceiveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 10)], null));
        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.ResolveDiscrepancyAsync(f.Actor.Id, created.Id, new("no reason to")));
    }

    [Fact]
    public async Task Cross_branch_transfer_moves_stock_between_branches()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch, PermissionCatalog.StockTransfersReceive);
        f.Actor.Role!.Name = RoleCatalog.Manager; // branch-selecting role: can act across both branches
        var otherBranchGodown = f.AddGodown(f.BranchB, "B-MAIN");
        var batch = f.AddSourceBatch(40);
        var request = f.Request(batch, 15) with { DestinationBranchId = f.BranchB.Id, DestinationGodownId = otherBranchGodown.Id };
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, request);
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));
        var dispatched = await f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null));
        var itemId = dispatched.Items.Single().Id;
        var received = await f.Service.ReceiveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 15)], null));

        Assert.Equal(StockTransferStatus.Received, received.Status);
        var destBatch = f.Batches.Single(x => x.GodownId == otherBranchGodown.Id);
        Assert.Equal(f.BranchB.Id, destBatch.BranchId);
        Assert.Equal(15, destBatch.QuantityAvailable);
        Assert.Equal(25, batch.QuantityAvailable);
    }

    [Fact]
    public async Task Receiving_a_second_batch_with_the_same_number_reuses_the_existing_destination_batch()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch, PermissionCatalog.StockTransfersReceive);
        var batch = f.AddSourceBatch(60, "LOT-SHARED");

        var first = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10));
        await f.Service.RequestTransferAsync(f.Actor.Id, first.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, first.Id, new(null));
        var firstDispatched = await f.Service.DispatchTransferAsync(f.Actor.Id, first.Id, new(null));
        await f.Service.ReceiveTransferAsync(f.Actor.Id, first.Id, new([new(firstDispatched.Items.Single().Id, 10)], null));

        var second = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 5));
        await f.Service.RequestTransferAsync(f.Actor.Id, second.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, second.Id, new(null));
        var secondDispatched = await f.Service.DispatchTransferAsync(f.Actor.Id, second.Id, new(null));
        await f.Service.ReceiveTransferAsync(f.Actor.Id, second.Id, new([new(secondDispatched.Items.Single().Id, 5)], null));

        var destBatches = f.Batches.Where(x => x.GodownId == f.DestGodown.Id).ToList();
        var destBatch = Assert.Single(destBatches);
        Assert.Equal(15, destBatch.QuantityAvailable);
    }

    [Fact]
    public async Task Update_replaces_header_and_items_while_transfer_remains_draft()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate);
        var batch = f.AddSourceBatch(50, "LOT-1");
        var otherGodown = f.AddGodown(f.BranchA, "ALT-DST");
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10));

        var editRequest = f.Request(batch, 25) with { DestinationGodownId = otherGodown.Id, Notes = "revised" };
        var updated = await f.Service.UpdateTransferAsync(f.Actor.Id, created.Id, editRequest);

        Assert.Equal(StockTransferStatus.Draft, updated.Status);
        Assert.Equal("revised", updated.Notes);
        Assert.Equal(otherGodown.Id, updated.DestinationGodownId);
        var item = Assert.Single(updated.Items);
        Assert.Equal(25, item.QuantityRequested);
    }

    [Fact]
    public async Task Update_is_rejected_once_the_transfer_has_left_draft()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest);
        var batch = f.AddSourceBatch(50);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.UpdateTransferAsync(f.Actor.Id, created.Id, f.Request(batch, 15)));
    }

    [Fact]
    public async Task Update_rejects_identical_source_and_destination_godown()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate);
        var batch = f.AddSourceBatch(50);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10));
        var editRequest = f.Request(batch, 10) with { DestinationGodownId = f.SourceGodown.Id };

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.UpdateTransferAsync(f.Actor.Id, created.Id, editRequest));
    }

    [Fact]
    public async Task Update_rejects_a_batch_that_does_not_belong_to_the_edited_source_godown()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate);
        var batch = f.AddSourceBatch(50);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 10));
        var otherGodown = f.AddGodown(f.BranchA, "OTHER");
        var foreignBatch = new ProductBatch { BranchId = f.BranchA.Id, GodownId = otherGodown.Id, ProductId = f.Product.Id, BatchNumber = "X", ExpiryDate = Expiry, QuantityAvailable = 10, QuantityReceived = 10, PurchasePrice = 8, RetailPrice = 12 };
        f.Batches.Add(foreignBatch);

        await Assert.ThrowsAsync<RequestValidationException>(() => f.Service.UpdateTransferAsync(f.Actor.Id, created.Id, f.Request(foreignBatch, 5)));
    }

    [Fact]
    public async Task Receive_appends_item_level_notes_without_overwriting_the_creation_time_note()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch, PermissionCatalog.StockTransfersReceive);
        var batch = f.AddSourceBatch(50);
        var request = f.Request(batch, 20) with { Items = [new(f.Product.Id, batch.Id, 20, "handle with care")] };
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, request);
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));
        var dispatched = await f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null));
        var itemId = dispatched.Items.Single().Id;

        var received = await f.Service.ReceiveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 15, "3 units damaged in transit")], null));

        var note = received.Items.Single().Notes;
        Assert.Contains("handle with care", note);
        Assert.Contains("3 units damaged in transit", note);
    }

    [Fact]
    public async Task Receive_appends_header_notes_to_the_transfer()
    {
        var f = new Fixture(PermissionCatalog.StockTransfersCreate, PermissionCatalog.StockTransfersRequest,
            PermissionCatalog.StockTransfersApprove, PermissionCatalog.StockTransfersDispatch, PermissionCatalog.StockTransfersReceive);
        var batch = f.AddSourceBatch(50);
        var created = await f.Service.CreateTransferAsync(f.Actor.Id, f.Request(batch, 20));
        await f.Service.RequestTransferAsync(f.Actor.Id, created.Id);
        await f.Service.ApproveTransferAsync(f.Actor.Id, created.Id, new(null));
        var dispatched = await f.Service.DispatchTransferAsync(f.Actor.Id, created.Id, new(null));
        var itemId = dispatched.Items.Single().Id;

        var received = await f.Service.ReceiveTransferAsync(f.Actor.Id, created.Id, new([new(itemId, 20)], "arrived via alternate route"));

        Assert.Contains("arrived via alternate route", received.Notes);
    }

    private sealed class Fixture : IStockTransferRepository
    {
        public readonly Branch BranchA = new() { Code = "A", Name = "Branch A", IsActive = true };
        public readonly Branch BranchB = new() { Code = "B", Name = "Branch B", IsActive = true };
        public readonly Dictionary<Guid, Branch> Branches = [];
        public readonly Product Product = new()
        {
            SKU = "SKU-1", NormalizedSku = "SKU-1", Name = "Panadol", Unit = "Tablet", PackSize = 1,
            PurchasePrice = 8, RetailPrice = 12, MaximumDiscountPercent = 0, ReorderLevel = 5, IsActive = true
        };
        public readonly User Actor;
        public readonly List<ProductBatch> Batches = [];
        public readonly List<Inventory> InventoryRows = [];
        public readonly List<StockMovement> Movements = [];
        public readonly List<AuditLog> Audits = [];
        public readonly List<StockTransfer> Transfers = [];
        public RecordingJournalPostingService Journal { get; } = new();
        public readonly FakeGodownAccessService GodownAccess = new();
        public readonly FakeDuplicateSubmissionGuard DuplicateGuard = new();
        public readonly Godown SourceGodown;
        public readonly Godown DestGodown;
        public IStockTransferService Service { get; }

        public Fixture(params string[] permissions)
        {
            Branches[BranchA.Id] = BranchA;
            Branches[BranchB.Id] = BranchB;
            SourceGodown = AddGodown(BranchA, "SRC");
            DestGodown = AddGodown(BranchA, "DST");
            var role = new Role { Name = RoleCatalog.Manager };
            foreach (var permission in permissions) role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = permission, Description = permission, Category = "test" } });
            Actor = new User { Username = "actor", NormalizedUsername = "ACTOR", FullName = "Actor", PasswordHash = "hash", BranchId = BranchA.Id, RoleId = role.Id, Role = role, IsActive = true };
            Service = new StockTransferService(this, GodownAccess, Journal, DuplicateGuard, TimeProvider.System);
        }

        public Godown AddGodown(Branch branch, string code)
        {
            var godown = new Godown { BranchId = branch.Id, Code = code, Name = code, IsActive = true };
            GodownAccess.Godowns[godown.Id] = godown;
            return godown;
        }

        public ProductBatch AddSourceBatch(int quantity, string batchNumber = "B-1")
        {
            var batch = new ProductBatch
            {
                BranchId = SourceGodown.BranchId, GodownId = SourceGodown.Id, ProductId = Product.Id, BatchNumber = batchNumber,
                ExpiryDate = Expiry, QuantityReceived = quantity, QuantityAvailable = quantity, PurchasePrice = 8, RetailPrice = 12
            };
            Batches.Add(batch);
            InventoryRows.Add(new Inventory { BranchId = batch.BranchId, GodownId = batch.GodownId, ProductId = Product.Id, ProductBatchId = batch.Id, QuantityInStock = quantity, ReorderLevel = 5 });
            return batch;
        }

        public StockTransferRequest Request(ProductBatch batch, int quantity) => new(
            SourceGodown.BranchId, SourceGodown.Id, DestGodown.BranchId, DestGodown.Id,
            DateOnly.FromDateTime(DateTime.UtcNow), "test transfer", [new(Product.Id, batch.Id, quantity, null)]);

        public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(Actor.Id == actorId ? Actor : null);
        public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult(Branches.GetValueOrDefault(branchId));
        public Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(Product.Id == productId ? Product : null);
        public Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(Batches.FirstOrDefault(x => x.Id == batchId));
        public Task<ProductBatch?> GetBatchForUpdateAsync(Guid batchId, CancellationToken cancellationToken = default) => GetBatchAsync(batchId, cancellationToken);
        public Task<ProductBatch?> GetBatchByNumberAsync(Guid branchId, Guid? godownId, Guid productId, string batchNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(Batches.FirstOrDefault(x => x.BranchId == branchId && x.GodownId == godownId && x.ProductId == productId && x.BatchNumber == batchNumber));
        public Task<Inventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(InventoryRows.FirstOrDefault(x => x.BranchId == branchId && x.ProductId == productId && x.ProductBatchId == batchId));
        public Task AddBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default) { Batches.Add(batch); return Task.CompletedTask; }
        public Task AddInventoryAsync(Inventory inventory, CancellationToken cancellationToken = default) { InventoryRows.Add(inventory); return Task.CompletedTask; }
        public Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default) { Movements.Add(movement); return Task.CompletedTask; }
        public Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) { Audits.Add(audit); return Task.CompletedTask; }

        public Task<string> NextTransferNumberAsync(DateOnly transferDate, CancellationToken cancellationToken = default) => Task.FromResult($"TRF-{transferDate.Year}-{Transfers.Count + 1:000000}");
        public Task AddTransferAsync(StockTransfer transfer, CancellationToken cancellationToken = default) { Transfers.Add(transfer); return Task.CompletedTask; }
        public Task<StockTransfer?> GetTransferForUpdateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Transfers.FirstOrDefault(x => x.Id == id));
        public void ReplaceTransferItems(StockTransfer transfer, IReadOnlyCollection<StockTransferItem> items)
        {
            transfer.Items.Clear();
            foreach (var item in items) { item.StockTransferId = transfer.Id; transfer.Items.Add(item); }
        }

        public Task<StockTransferDetailsDto?> GetTransferDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Map(Transfers.FirstOrDefault(x => x.Id == id)));
        public Task<PagedResult<StockTransferListItemDto>> ListTransfersAsync(StockTransferListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<StockTransferListItemDto>([], query.Page, query.PageSize, 0));
        public Task<IReadOnlyList<TransferableBatchDto>> ListTransferableBatchesAsync(Guid branchId, Guid godownId, Guid? productId, string? search, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TransferableBatchDto>>(Batches.Where(x => x.BranchId == branchId && x.GodownId == godownId && x.QuantityAvailable > 0)
                .Select(x => new TransferableBatchDto(x.Id, x.ProductId, Product.Name, Product.SKU, x.BatchNumber, x.ExpiryDate, x.QuantityAvailable, x.PurchasePrice, x.RetailPrice)).ToList());

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default) => operation(cancellationToken);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        private StockTransferDetailsDto? Map(StockTransfer? x)
        {
            if (x is null) return null;
            var items = x.Items.Select(i => new StockTransferItemDto(
                i.Id, i.ProductId, Product.Name, Product.SKU, i.SourceProductBatchId, i.BatchNumber, i.ExpiryDate, i.UnitCostSnapshot,
                i.DestinationProductBatchId, i.QuantityRequested, i.QuantityApproved, i.QuantityDispatched, i.QuantityReceived,
                i.QuantityDispatched - i.QuantityReceived, i.Notes)).ToList();
            return new StockTransferDetailsDto(
                x.Id, x.TransferNumber, x.TransferDate, x.Status, x.Notes,
                x.SourceBranchId, Branches[x.SourceBranchId].Name, x.SourceGodownId, GodownAccess.Godowns[x.SourceGodownId].Name,
                x.DestinationBranchId, Branches[x.DestinationBranchId].Name, x.DestinationGodownId, GodownAccess.Godowns[x.DestinationGodownId].Name,
                Actor.FullName, x.CreatedAt, x.RequestedByUserId.HasValue ? Actor.FullName : null, x.RequestedAtUtc,
                x.ApprovedByUserId.HasValue ? Actor.FullName : null, x.ApprovedAtUtc, x.DispatchedByUserId.HasValue ? Actor.FullName : null, x.DispatchedAtUtc,
                x.ReceivedByUserId.HasValue ? Actor.FullName : null, x.ReceivedAtUtc, x.CancelledByUserId.HasValue ? Actor.FullName : null, x.CancelledAtUtc,
                x.CancellationReason, items);
        }
    }

    private sealed class RecordingJournalPostingService : IJournalPostingService
    {
        public readonly List<JournalPostingRequest> Posted = [];
        public Task PostAsync(JournalPostingRequest request, CancellationToken cancellationToken = default)
        {
            Posted.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGodownAccessService : IGodownAccessService
    {
        public readonly Dictionary<Guid, Godown> Godowns = [];
        public Func<Guid, Guid, bool> AccessPredicate = (_, _) => true;

        public Task<Godown?> GetGodownAsync(Guid godownId, CancellationToken cancellationToken = default) => Task.FromResult(Godowns.GetValueOrDefault(godownId));
        public Task<Guid?> GetDefaultGodownIdAsync(Guid branchId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
        public Task<bool> UserHasAccessAsync(Guid userId, Guid godownId, CancellationToken cancellationToken = default) => Task.FromResult(AccessPredicate(userId, godownId));
    }

    private sealed class FakeDuplicateSubmissionGuard : Pharmacy.Application.Common.IDuplicateSubmissionGuard
    {
        public bool Reject;
        public int CallCount;
        public Task GuardAsync(string operation, Guid actorId, object fingerprintPayload, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Reject) throw new Pharmacy.Application.Common.ResourceConflictException("This exact request was already submitted moments ago. Check the result before retrying.");
            return Task.CompletedTask;
        }
    }
}
