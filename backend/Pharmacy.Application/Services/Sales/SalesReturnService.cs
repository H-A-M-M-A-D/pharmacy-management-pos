using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Sales;

public sealed class SalesReturnService(ISalesReturnRepository repository, IJournalPostingService journalPosting, TimeProvider timeProvider) : ISalesReturnService
{
    public async Task<ReturnableSaleDto> GetReturnableSaleAsync(Guid actorId, Guid saleId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesReturnsView, cancellationToken);
        return await repository.GetReturnableSaleAsync(saleId, actor.BranchId, CanSelectBranch(actor), BusinessDate(), cancellationToken)
            ?? throw new ResourceNotFoundException("Sale was not found.");
    }

    public async Task<SalesReturnDetailsDto> PostReturnAsync(Guid actorId, Guid saleId, PostSalesReturnRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesReturnsCreate, cancellationToken);
        _ = await Require(actorId, PermissionCatalog.SalesReturnsRefund, cancellationToken);
        ValidateRequest(request);
        SalesReturn? posted = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var sale = await repository.GetOriginalSaleAsync(saleId, ct) ?? throw new ResourceNotFoundException("Original sale was not found.");
            if (sale.Status != SaleStatus.Posted || sale.InvoiceNumber is null) throw new RequestValidationException("Only posted sales can be returned.");
            EnsureBranchAccess(actor, sale.BranchId);

            var requestedByAllocation = request.Allocations
                .GroupBy(x => x.OriginalAllocationId)
                .ToDictionary(x => x.Key, x => x.ToList());
            var originalAllocations = sale.Items.SelectMany(item => item.Allocations.Select(allocation => new { Item = item, Allocation = allocation })).ToDictionary(x => x.Allocation.Id);
            foreach (var allocationId in requestedByAllocation.Keys)
            {
                if (!originalAllocations.ContainsKey(allocationId)) throw new RequestValidationException("This batch was not part of the original sale.");
            }

            var returned = await repository.GetReturnedQuantitiesAsync(requestedByAllocation.Keys, ct);
            var refunded = await repository.GetRefundedAmountsAsync(requestedByAllocation.Keys, ct);
            var now = UtcNow();
            var businessDate = BusinessDate();
            posted = new SalesReturn
            {
                ReturnNumber = await repository.NextReturnNumberAsync(now, ct),
                OriginalSaleId = sale.Id,
                BranchId = sale.BranchId,
                ProcessedByUserId = actor.Id,
                ReturnDateUtc = now,
                PostedAtUtc = now,
                Status = SalesReturnStatus.Posted,
                Reason = request.Reason,
                Notes = Clean(request.Notes)
            };

            foreach (var (allocationId, entries) in requestedByAllocation)
            {
                var original = originalAllocations[allocationId];
                var quantity = entries.Sum(x => x.Quantity);
                var disposition = entries.Select(x => x.Disposition).Distinct().Single();
                var alreadyReturned = returned.GetValueOrDefault(allocationId);
                var remaining = original.Allocation.Quantity - alreadyReturned;
                if (quantity > remaining) throw new ResourceConflictException($"Only {remaining} units remain returnable.");
                var batch = await repository.GetBatchAsync(original.Allocation.ProductBatchId, ct) ?? throw new ResourceConflictException("Original batch is unavailable.");
                if (batch.BranchId != sale.BranchId || batch.ProductId != original.Item.ProductId) throw new RequestValidationException("Original allocation no longer matches its batch.");
                if (batch.IsDisposed && disposition == SalesReturnDisposition.Restockable) throw new ResourceConflictException("Restocking is not allowed for a disposed batch.");
                var inventory = await repository.GetInventoryAsync(sale.BranchId, original.Item.ProductId, batch.Id, ct) ?? throw new ResourceConflictException("Inventory balance is unavailable for returned stock.");

                var gross = AllocateAmount(original.Allocation.GrossAmount, original.Allocation.Quantity, quantity, alreadyReturned, 0, isFinal: false);
                var discount = AllocateAmount(original.Allocation.DiscountAmount, original.Allocation.Quantity, quantity, alreadyReturned, 0, isFinal: false);
                var tax = AllocateAmount(original.Allocation.TaxAmount, original.Allocation.Quantity, quantity, alreadyReturned, 0, isFinal: false);
                var refund = AllocateAmount(original.Allocation.NetAmount, original.Allocation.Quantity, quantity, alreadyReturned, refunded.GetValueOrDefault(allocationId), isFinal: quantity == remaining);
                if (refund > original.Allocation.NetAmount - refunded.GetValueOrDefault(allocationId)) throw new ResourceConflictException("This return would exceed the original refundable amount.");

                var returnItem = posted.Items.FirstOrDefault(x => x.OriginalSaleItemId == original.Item.Id);
                if (returnItem is null)
                {
                    returnItem = new SalesReturnItem { OriginalSaleItemId = original.Item.Id, ProductId = original.Item.ProductId };
                    posted.Items.Add(returnItem);
                }
                returnItem.Quantity += quantity;
                returnItem.GrossReturnAmount = Money(returnItem.GrossReturnAmount + gross);
                returnItem.DiscountReturnAmount = Money(returnItem.DiscountReturnAmount + discount);
                returnItem.TaxReturnAmount = Money(returnItem.TaxReturnAmount + tax);
                returnItem.RefundAmount = Money(returnItem.RefundAmount + refund);
                returnItem.Allocations.Add(new SalesReturnAllocation
                {
                    OriginalSaleItemBatchAllocationId = allocationId,
                    ProductBatchId = batch.Id,
                    Quantity = quantity,
                    Disposition = disposition,
                    UnitRetailPriceSnapshot = original.Allocation.UnitRetailPriceSnapshot,
                    UnitSalePriceSnapshot = original.Allocation.UnitSalePriceSnapshot,
                    UnitCostPriceSnapshot = original.Allocation.UnitCostPriceSnapshot,
                    ExpiryDateSnapshot = original.Allocation.ExpiryDateSnapshot,
                    GrossReturnAmount = gross,
                    DiscountReturnAmount = discount,
                    TaxReturnAmount = tax,
                    RefundAmount = refund
                });

                posted.GrossReturnAmount = Money(posted.GrossReturnAmount + gross);
                posted.DiscountReturnAmount = Money(posted.DiscountReturnAmount + discount);
                posted.TaxReturnAmount = Money(posted.TaxReturnAmount + tax);
                posted.RefundAmount = Money(posted.RefundAmount + refund);

                batch.QuantityAvailable += quantity;
                batch.UpdatedAt = now;
                inventory.QuantityInStock += quantity;
                inventory.UpdatedAt = now;
                await repository.AddMovementAsync(Movement(StockMovementType.SaleReturn, posted.Id, sale.BranchId, original.Item.ProductId, batch.Id, quantity, actor.Id, posted.ReturnNumber), ct);

                if (disposition == SalesReturnDisposition.NonResellable)
                {
                    var removalType = batch.ExpiryDate < businessDate ? StockMovementType.Expired : StockMovementType.Damaged;
                    batch.QuantityAvailable -= quantity;
                    inventory.QuantityInStock -= quantity;
                    await repository.AddMovementAsync(Movement(removalType, posted.Id, sale.BranchId, original.Item.ProductId, batch.Id, -quantity, actor.Id, posted.ReturnNumber), ct);
                }
            }

            posted.CustomerCreditReductionAmount = 0;
            if (sale.CustomerId.HasValue && posted.RefundAmount > 0)
            {
                var customerBalance = await repository.GetCustomerBalanceAsync(sale.CustomerId.Value, sale.BranchId, ct);
                posted.CustomerCreditReductionAmount = Money(Math.Min(posted.RefundAmount, Math.Max(customerBalance, 0)));
                if (posted.CustomerCreditReductionAmount > 0)
                {
                    await repository.AddCustomerLedgerEntryAsync(new CustomerLedgerEntry
                    {
                        CustomerId = sale.CustomerId.Value,
                        BranchId = sale.BranchId,
                        EntryType = CustomerLedgerEntryType.SalesReturn,
                        Amount = -posted.CustomerCreditReductionAmount,
                        EntryDate = businessDate,
                        ReferenceType = "SalesReturn",
                        ReferenceId = posted.Id,
                        ReferenceNumber = posted.ReturnNumber,
                        Notes = "Sales return credit reduction",
                        CreatedByUserId = actor.Id
                    }, ct);
                }
            }
            posted.CashRefundAmount = Money(posted.RefundAmount - posted.CustomerCreditReductionAmount);
            ApplyRefundPayments(posted, request.RefundPayments);
            await PostSalesReturnJournalAsync(actor, sale, posted, ct);
            await repository.AddSalesReturnAsync(posted, ct);
            await repository.AddAuditAsync(new AuditLog
            {
                UserId = actor.Id,
                Action = "SalesReturnPosted",
                EntityType = "SalesReturn",
                EntityId = posted.Id,
                NewValues = JsonSerializer.Serialize(new
                {
                    posted.ReturnNumber,
                    OriginalInvoiceNumber = sale.InvoiceNumber,
                    posted.BranchId,
                    posted.ProcessedByUserId,
                    ItemCount = posted.Items.Count,
                    posted.RefundAmount,
                    Dispositions = posted.Items.SelectMany(x => x.Allocations).GroupBy(x => x.Disposition.ToString()).ToDictionary(x => x.Key, x => x.Sum(a => a.Quantity))
                })
            }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await GetReturnAsync(actorId, posted!.Id, cancellationToken);
    }

    public async Task<PagedResult<SalesReturnListItemDto>> ListReturnsAsync(Guid actorId, SalesReturnsQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesReturnsView, cancellationToken);
        ValidatePage(query.Page, query.PageSize);
        var scope = Scope(actor, query.BranchId);
        return await repository.ListReturnsAsync(query with { BranchId = scope.BranchId }, actor.BranchId, scope.CanSelectBranch, cancellationToken);
    }

    public async Task<SalesReturnDetailsDto> GetReturnAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.SalesReturnsView, cancellationToken);
        return await repository.GetReturnDetailsAsync(id, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Sales return was not found.");
    }

    public async Task<SalesReturnReceiptDto> ReceiptAsync(Guid actorId, Guid id, bool auditReprint, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, auditReprint ? PermissionCatalog.SalesReturnsReprint : PermissionCatalog.SalesReturnsView, cancellationToken);
        var detail = await repository.GetReturnDetailsAsync(id, actor.BranchId, CanSelectBranch(actor), cancellationToken)
            ?? throw new ResourceNotFoundException("Sales return was not found.");
        if (auditReprint)
        {
            await repository.ExecuteInTransactionAsync(async ct =>
            {
                await repository.AddAuditAsync(new AuditLog { UserId = actor.Id, Action = "SalesReturnReceiptReprinted", EntityType = "SalesReturn", EntityId = detail.Id, NewValues = JsonSerializer.Serialize(new { detail.ReturnNumber, detail.OriginalInvoiceNumber }) }, ct);
                await repository.SaveChangesAsync(ct);
            }, IsolationLevel.ReadCommitted, cancellationToken);
        }
        return new SalesReturnReceiptDto(detail.ReturnNumber, detail.OriginalInvoiceNumber, detail.BranchName, detail.BranchAddress, detail.BranchPhone, detail.ReturnDateUtc, detail.ProcessedByName, detail.CustomerName, detail.Reason, detail.RefundAmount, detail.CustomerCreditReductionAmount, detail.CashRefundAmount, detail.Items, detail.RefundPayments);
    }

    private async Task PostSalesReturnJournalAsync(User actor, Sale sale, SalesReturn posted, CancellationToken ct)
    {
        var lines = new List<JournalLineInput>();
        if (posted.RefundAmount > 0)
            lines.Add(new JournalLineInput(AccountMappingKey.SalesReturnsContra, posted.RefundAmount, 0));
        foreach (var payment in posted.RefundPayments)
            if (payment.Amount > 0)
                lines.Add(new JournalLineInput(PaymentAccount(payment.Method), 0, payment.Amount));
        if (posted.CustomerCreditReductionAmount > 0)
            lines.Add(new JournalLineInput(AccountMappingKey.AccountsReceivable, 0, posted.CustomerCreditReductionAmount, CustomerId: sale.CustomerId));
        var cogs = Money(posted.Items.SelectMany(x => x.Allocations).Where(a => a.Disposition == SalesReturnDisposition.Restockable).Sum(a => a.Quantity * a.UnitCostPriceSnapshot));
        if (cogs > 0)
        {
            lines.Add(new JournalLineInput(AccountMappingKey.Inventory, cogs, 0));
            lines.Add(new JournalLineInput(AccountMappingKey.CostOfGoodsSold, 0, cogs));
        }
        if (lines.Count == 0) return;
        await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.SalesReturn, posted.Id, posted.BranchId, posted.PostedAtUtc!.Value,
            posted.ReturnNumber, $"Sales return {posted.ReturnNumber}", actor.Id, lines), ct);
    }

    private static AccountMappingKey PaymentAccount(SalePaymentMethod method) => method == SalePaymentMethod.Cash ? AccountMappingKey.Cash : AccountMappingKey.Bank;

    private static StockMovement Movement(StockMovementType type, Guid returnId, Guid branchId, Guid productId, Guid batchId, int quantity, Guid actorId, string returnNumber) => new()
    {
        MovementType = type,
        BranchId = branchId,
        ProductId = productId,
        ProductBatchId = batchId,
        Quantity = quantity,
        ReferenceType = "SalesReturn",
        ReferenceId = returnId,
        Notes = returnNumber,
        PerformedByUserId = actorId
    };

    private static void ValidateRequest(PostSalesReturnRequest request)
    {
        if (!Enum.IsDefined(request.Reason)) throw new RequestValidationException("Return reason is invalid.");
        if (request.Reason == SalesReturnReason.Other && string.IsNullOrWhiteSpace(request.Notes)) throw new RequestValidationException("Notes are required when return reason is Other.");
        if (request.Allocations.Count == 0) throw new RequestValidationException("At least one returned batch allocation is required.");
        foreach (var allocation in request.Allocations)
        {
            if (allocation.OriginalAllocationId == Guid.Empty) throw new RequestValidationException("Original allocation is required.");
            if (allocation.Quantity <= 0) throw new RequestValidationException("Return quantity must be greater than zero.");
            if (!Enum.IsDefined(allocation.Disposition)) throw new RequestValidationException("Return disposition is invalid.");
        }
        foreach (var duplicateDisposition in request.Allocations.GroupBy(x => x.OriginalAllocationId).Where(x => x.Select(y => y.Disposition).Distinct().Count() > 1))
        {
            throw new RequestValidationException("A returned allocation can have one disposition per return.");
        }
    }

    private static void ApplyRefundPayments(SalesReturn salesReturn, IReadOnlyList<SalesRefundPaymentRequest> payments)
    {
        var total = Money(payments.Sum(x => x.Amount));
        if (total != salesReturn.CashRefundAmount) throw new RequestValidationException("Refund payment total must exactly equal cash refund amount.");
        if (salesReturn.CashRefundAmount > 0 && payments.Count == 0) throw new RequestValidationException("At least one refund payment is required when cash is refunded.");
        foreach (var payment in payments)
        {
            if (!Enum.IsDefined(payment.Method)) throw new RequestValidationException("Refund payment method is invalid.");
            if (payment.Amount <= 0) throw new RequestValidationException("Refund payment amount must be greater than zero.");
            salesReturn.RefundPayments.Add(new SalesRefundPayment { Method = payment.Method, Amount = Money(payment.Amount), ReferenceNumber = Clean(payment.ReferenceNumber), FinancialAccountId = payment.FinancialAccountId });
        }
    }

    private async Task<User> Require(Guid actorId, string permission, CancellationToken cancellationToken)
    {
        var actor = await repository.GetActorAsync(actorId, cancellationToken);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
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
    private static decimal AllocateAmount(decimal originalAmount, int originalQuantity, int returnQuantity, int alreadyReturnedQuantity, decimal alreadyRefundedAmount, bool isFinal)
    {
        if (isFinal) return Money(originalAmount - alreadyRefundedAmount);
        return Money(originalAmount * returnQuantity / originalQuantity);
    }
    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
    }
}
