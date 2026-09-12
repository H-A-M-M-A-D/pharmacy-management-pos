using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting.PaymentAllocation;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.PartyAdjustments;

/// <summary>
/// Credit notes, debit notes, and customer/supplier write-offs all reuse the exact same payment
/// settlement machinery a real customer/supplier payment uses — a <see cref="CustomerPayment"/>/
/// <see cref="CustomerPaymentAllocation"/> pair (or the supplier-side <see cref="SupplierLedgerEntry"/>/
/// <see cref="SupplierPaymentAllocation"/> pair) tagged with a distinguishing payment method — so AR/AP
/// aging, the customer/supplier ledger, and everything downstream treat them exactly like a real
/// payment except for which GL account absorbs the non-cash side. Customer/supplier advances are kept
/// as their own liability/asset (never posted directly as revenue/expense) until explicitly applied,
/// at which point applying one also settles through this same machinery.
/// </summary>
public sealed class PartyAdjustmentService(IPartyAdjustmentRepository repository, IJournalPostingService journalPosting, TimeProvider timeProvider) : IPartyAdjustmentService
{
    public async Task<SupplierAdjustmentReversalDto> ReverseSupplierAdjustmentAsync(Guid actorId, JournalSourceType sourceType, Guid sourceId, ReverseSupplierAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsJournalReverse, cancellationToken);
        ValidateReason(request.Reason);
        if (sourceType is not (JournalSourceType.DebitNote or JournalSourceType.SupplierWriteOff or JournalSourceType.SupplierAdvance))
            throw new RequestValidationException("Only supplier debit notes, write-offs and unapplied advances can be reversed here.");
        JournalEntry? reversal = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var original = await repository.GetJournalForSourceAsync(sourceType, sourceId, ct) ?? throw new ResourceNotFoundException("Supplier adjustment was not found.");
            EnsureBranchAccess(actor, original.BranchId);
            if (await repository.JournalHasReversalAsync(original.Id, ct)) throw new RequestValidationException("This supplier adjustment has already been reversed.");
            var supplierId = original.Lines.Select(x => x.SupplierId).FirstOrDefault(x => x.HasValue)
                ?? throw new RequestValidationException("Supplier adjustment has no supplier attribution.");
            var now = UtcNow();
            if (sourceType == JournalSourceType.SupplierAdvance)
            {
                var advance = await repository.GetSupplierAdvanceAsync(sourceId, ct) ?? throw new ResourceNotFoundException("Supplier advance was not found.");
                if (advance.AmountApplied != 0) throw new RequestValidationException("An applied supplier advance cannot be reversed; its invoice settlements must be corrected first.");
                await repository.AddFinancialLedgerEntryAsync(new FinancialLedgerEntry
                {
                    FinancialAccountId = advance.FinancialAccountId, BranchId = advance.BranchId, EntryType = FinancialLedgerEntryType.AdjustmentCredit,
                    Amount = advance.Amount, ReferenceType = "SupplierAdvanceReversal", ReferenceId = advance.Id, ReferenceNumber = advance.AdvanceNumber,
                    Description = request.Reason.Trim(), CreatedByUserId = actorId, OccurredAtUtc = now
                }, ct);
            }
            else
            {
                var settlement = await repository.GetSupplierSettlementAsync(supplierId, original.Reference!, ct)
                    ?? throw new RequestValidationException("Supplier settlement was not found.");
                var released = await repository.RemoveSupplierAllocationsAsync(settlement.Id, ct);
                // Allocations are current settlement links. Preserve the released links in the
                // immutable audit while compensating the permanent ledger instead of editing it.
                await Audit(actorId, "SupplierAdjustmentAllocationsReleased", sourceId,
                    released.Select(x => new { x.Id, x.GoodsReceiptId, x.AllocatedAmount }).ToList(), ct);
                await repository.AddSupplierLedgerEntryAsync(new SupplierLedgerEntry
                {
                    SupplierId = supplierId, BranchId = original.BranchId, EntryType = SupplierLedgerEntryType.AdjustmentDebit, Amount = -settlement.Amount,
                    EntryDate = DateOnly.FromDateTime(now), ReferenceType = "SupplierAdjustmentReversal", ReferenceId = sourceId,
                    ReferenceNumber = original.Reference, Notes = request.Reason.Trim(), CreatedByUserId = actorId
                }, ct);
            }
            reversal = new JournalEntry
            {
                EntryNumber = await repository.NextNumberAsync("JournalEntryNumberSequence", "JV", now, ct), EntryDateUtc = now,
                SourceType = JournalSourceType.JournalReversal, SourceId = original.Id, Reference = original.EntryNumber,
                Description = $"Reversal of {original.EntryNumber}: {request.Reason.Trim()}", BranchId = original.BranchId,
                PostedByUserId = actorId, PostedAtUtc = now, ReversesJournalEntryId = original.Id, ReversalReason = request.Reason.Trim(),
                Lines = original.Lines.Select(x => new JournalEntryLine { ChartOfAccountId = x.ChartOfAccountId, Debit = x.Credit, Credit = x.Debit,
                    SupplierId = x.SupplierId, BranchId = original.BranchId, CostCenterId = x.CostCenterId, Description = x.Description }).ToList()
            };
            await repository.AddJournalEntryAsync(reversal, ct);
            await Audit(actorId, "SupplierAdjustmentReversed", sourceId, new { sourceType, reversal.EntryNumber, request.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return new(reversal!.Id, reversal.EntryNumber);
    }
    // ---- Credit notes ----

    public async Task<IReadOnlyList<CreditNoteDto>> ListCreditNotesAsync(Guid actorId, Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsCreditNotesView, cancellationToken);
        var notes = await repository.ListCreditNotesAsync(customerId, Scope(actor, branchId), cancellationToken);
        return notes.Select(Map).ToList();
    }

    public async Task<CreditNoteDto> CreateCreditNoteAsync(Guid actorId, CreateCreditNoteRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsCreditNotesCreate, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        ValidatePositive(request.Amount, "Amount");
        ValidateReason(request.Reason);
        if (await repository.GetCustomerAsync(request.CustomerId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Customer is invalid or inactive.");
        await ValidateAppliedSaleAsync(request.CustomerId, request.AppliedToSaleId, request.Amount, cancellationToken);

        CreditNote? note = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var id = Guid.NewGuid();
            var number = await repository.NextNumberAsync("CreditNoteNumberSequence", "CN", request.IssueDateUtc, ct);
            await SettleCustomerReceivableAsync(request.CustomerId, request.BranchId, request.Amount, CustomerPaymentMethod.CreditNote,
                request.AppliedToSaleId, number, $"Credit note {number}: {request.Reason.Trim()}", actorId, request.IssueDateUtc, ct);
            await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.CreditNote, id, request.BranchId, request.IssueDateUtc,
                number, $"Credit note {number}: {request.Reason.Trim()}", actorId,
                [new(AccountMappingKey.AccountsReceivableAdjustmentSuspense, request.Amount, 0), new(AccountMappingKey.AccountsReceivable, 0, request.Amount, CustomerId: request.CustomerId)]), ct);
            note = new CreditNote
            {
                Id = id, CreditNoteNumber = number, CustomerId = request.CustomerId, BranchId = request.BranchId, Amount = Money(request.Amount),
                Reason = request.Reason.Trim(), Notes = Clean(request.Notes), IssueDateUtc = AsUtc(request.IssueDateUtc),
                AppliedToSaleId = request.AppliedToSaleId, CreatedByUserId = actorId, PostedAtUtc = UtcNow()
            };
            await repository.AddCreditNoteAsync(note, ct);
            await Audit(actorId, "CreditNoteIssued", note.Id, new { note.CreditNoteNumber, note.CustomerId, note.Amount, note.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync(note!, cancellationToken);
    }

    // ---- Debit notes ----

    public async Task<IReadOnlyList<DebitNoteDto>> ListDebitNotesAsync(Guid actorId, Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsDebitNotesView, cancellationToken);
        var notes = await repository.ListDebitNotesAsync(supplierId, Scope(actor, branchId), cancellationToken);
        return notes.Select(Map).ToList();
    }

    public async Task<DebitNoteDto> CreateDebitNoteAsync(Guid actorId, CreateDebitNoteRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsDebitNotesCreate, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        ValidatePositive(request.Amount, "Amount");
        ValidateReason(request.Reason);
        if (await repository.GetSupplierAsync(request.SupplierId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Supplier is invalid or inactive.");
        await ValidateAppliedGoodsReceiptAsync(request.SupplierId, request.AppliedToGoodsReceiptId, request.Amount, cancellationToken);

        DebitNote? note = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var id = Guid.NewGuid();
            var number = await repository.NextNumberAsync("DebitNoteNumberSequence", "DN", request.IssueDateUtc, ct);
            await SettleSupplierPayableAsync(request.SupplierId, request.BranchId, request.Amount, "DebitNote",
                request.AppliedToGoodsReceiptId, number, $"Debit note {number}: {request.Reason.Trim()}", actorId, request.IssueDateUtc, ct);
            await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.DebitNote, id, request.BranchId, request.IssueDateUtc,
                number, $"Debit note {number}: {request.Reason.Trim()}", actorId,
                [new(AccountMappingKey.AccountsPayable, request.Amount, 0, SupplierId: request.SupplierId), new(AccountMappingKey.AccountsPayableAdjustmentSuspense, 0, request.Amount)]), ct);
            note = new DebitNote
            {
                Id = id, DebitNoteNumber = number, SupplierId = request.SupplierId, BranchId = request.BranchId, Amount = Money(request.Amount),
                Reason = request.Reason.Trim(), Notes = Clean(request.Notes), IssueDateUtc = AsUtc(request.IssueDateUtc),
                AppliedToGoodsReceiptId = request.AppliedToGoodsReceiptId, CreatedByUserId = actorId, PostedAtUtc = UtcNow()
            };
            await repository.AddDebitNoteAsync(note, ct);
            await Audit(actorId, "DebitNoteIssued", note.Id, new { note.DebitNoteNumber, note.SupplierId, note.Amount, note.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync(note!, cancellationToken);
    }

    // ---- Customer write-offs ----

    public async Task<IReadOnlyList<CustomerWriteOffDto>> ListCustomerWriteOffsAsync(Guid actorId, Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsWriteOffsView, cancellationToken);
        var writeOffs = await repository.ListCustomerWriteOffsAsync(customerId, Scope(actor, branchId), cancellationToken);
        return writeOffs.Select(Map).ToList();
    }

    public async Task<CustomerWriteOffDto> CreateCustomerWriteOffAsync(Guid actorId, CreateCustomerWriteOffRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsWriteOffsCreate, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        ValidatePositive(request.Amount, "Amount");
        ValidateReason(request.Reason);
        if (await repository.GetCustomerAsync(request.CustomerId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Customer is invalid or inactive.");
        await ValidateAppliedSaleAsync(request.CustomerId, request.AppliedToSaleId, request.Amount, cancellationToken);

        CustomerWriteOff? writeOff = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var id = Guid.NewGuid();
            var number = await repository.NextNumberAsync("CustomerWriteOffNumberSequence", "CWO", request.WriteOffDateUtc, ct);
            var payment = await SettleCustomerReceivableAsync(request.CustomerId, request.BranchId, request.Amount, CustomerPaymentMethod.WriteOff,
                request.AppliedToSaleId, number, $"Customer write-off {number}: {request.Reason.Trim()}", actorId, request.WriteOffDateUtc, ct);
            await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.CustomerWriteOff, id, request.BranchId, request.WriteOffDateUtc,
                number, $"Customer write-off {number}: {request.Reason.Trim()}", actorId,
                [new(AccountMappingKey.BadDebtExpense, request.Amount, 0), new(AccountMappingKey.AccountsReceivable, 0, request.Amount, CustomerId: request.CustomerId)]), ct);
            writeOff = new CustomerWriteOff
            {
                Id = id, WriteOffNumber = number, CustomerId = request.CustomerId, BranchId = request.BranchId, Amount = Money(request.Amount),
                Reason = request.Reason.Trim(), Notes = Clean(request.Notes), WriteOffDateUtc = AsUtc(request.WriteOffDateUtc),
                AppliedToSaleId = request.AppliedToSaleId, CustomerPaymentId = payment.Id, CreatedByUserId = actorId, PostedAtUtc = UtcNow()
            };
            await repository.AddCustomerWriteOffAsync(writeOff, ct);
            await Audit(actorId, "CustomerWriteOffPosted", writeOff.Id, new { writeOff.WriteOffNumber, writeOff.CustomerId, writeOff.Amount, writeOff.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync(writeOff!, cancellationToken);
    }

    // ---- Supplier write-offs ----

    public async Task<IReadOnlyList<SupplierWriteOffDto>> ListSupplierWriteOffsAsync(Guid actorId, Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsWriteOffsView, cancellationToken);
        var writeOffs = await repository.ListSupplierWriteOffsAsync(supplierId, Scope(actor, branchId), cancellationToken);
        return writeOffs.Select(Map).ToList();
    }

    public async Task<SupplierWriteOffDto> CreateSupplierWriteOffAsync(Guid actorId, CreateSupplierWriteOffRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsWriteOffsCreate, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        ValidatePositive(request.Amount, "Amount");
        ValidateReason(request.Reason);
        if (await repository.GetSupplierAsync(request.SupplierId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Supplier is invalid or inactive.");
        await ValidateAppliedGoodsReceiptAsync(request.SupplierId, request.AppliedToGoodsReceiptId, request.Amount, cancellationToken);

        SupplierWriteOff? writeOff = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var id = Guid.NewGuid();
            var number = await repository.NextNumberAsync("SupplierWriteOffNumberSequence", "SWO", request.WriteOffDateUtc, ct);
            var ledgerEntry = await SettleSupplierPayableAsync(request.SupplierId, request.BranchId, request.Amount, "WriteOff",
                request.AppliedToGoodsReceiptId, number, $"Supplier write-off {number}: {request.Reason.Trim()}", actorId, request.WriteOffDateUtc, ct);
            await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.SupplierWriteOff, id, request.BranchId, request.WriteOffDateUtc,
                number, $"Supplier write-off {number}: {request.Reason.Trim()}", actorId,
                [new(AccountMappingKey.AccountsPayable, request.Amount, 0, SupplierId: request.SupplierId), new(AccountMappingKey.PayablesWriteOffIncome, 0, request.Amount)]), ct);
            writeOff = new SupplierWriteOff
            {
                Id = id, WriteOffNumber = number, SupplierId = request.SupplierId, BranchId = request.BranchId, Amount = Money(request.Amount),
                Reason = request.Reason.Trim(), Notes = Clean(request.Notes), WriteOffDateUtc = AsUtc(request.WriteOffDateUtc),
                AppliedToGoodsReceiptId = request.AppliedToGoodsReceiptId, SupplierLedgerEntryId = ledgerEntry.Id, CreatedByUserId = actorId, PostedAtUtc = UtcNow()
            };
            await repository.AddSupplierWriteOffAsync(writeOff, ct);
            await Audit(actorId, "SupplierWriteOffPosted", writeOff.Id, new { writeOff.WriteOffNumber, writeOff.SupplierId, writeOff.Amount, writeOff.Reason }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync(writeOff!, cancellationToken);
    }

    // ---- Customer advances ----

    public async Task<IReadOnlyList<CustomerAdvanceDto>> ListCustomerAdvancesAsync(Guid actorId, Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsAdvancesView, cancellationToken);
        var advances = await repository.ListCustomerAdvancesAsync(customerId, Scope(actor, branchId), cancellationToken);
        return advances.Select(Map).ToList();
    }

    public async Task<CustomerAdvanceDto> RecordCustomerAdvanceAsync(Guid actorId, RecordCustomerAdvanceRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsAdvancesCreate, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        ValidatePositive(request.Amount, "Amount");
        if (await repository.GetCustomerAsync(request.CustomerId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Customer is invalid or inactive.");
        var account = await repository.GetFinancialAccountAsync(request.FinancialAccountId, cancellationToken);
        if (account is not { IsActive: true } || account.BranchId != request.BranchId) throw new RequestValidationException("Financial account is invalid, inactive, or belongs to another branch.");

        CustomerAdvance? advance = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var id = Guid.NewGuid();
            var number = await repository.NextNumberAsync("CustomerAdvanceNumberSequence", "CADV", request.ReceivedDateUtc, ct);
            await repository.AddFinancialLedgerEntryAsync(new FinancialLedgerEntry
            {
                FinancialAccountId = account.Id, BranchId = request.BranchId, EntryType = FinancialLedgerEntryType.CustomerPayment, Amount = Money(request.Amount),
                ReferenceType = "CustomerAdvance", ReferenceId = id, ReferenceNumber = number, Description = $"Customer advance {number}",
                CreatedByUserId = actorId, OccurredAtUtc = AsUtc(request.ReceivedDateUtc)
            }, ct);
            await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.CustomerAdvance, id, request.BranchId, request.ReceivedDateUtc,
                number, $"Customer advance {number}", actorId,
                [new(PaymentAccount(account.AccountType), request.Amount, 0), new(AccountMappingKey.CustomerAdvances, 0, request.Amount, CustomerId: request.CustomerId)]), ct);
            advance = new CustomerAdvance
            {
                Id = id, AdvanceNumber = number, CustomerId = request.CustomerId, BranchId = request.BranchId, Amount = Money(request.Amount), AmountApplied = 0,
                FinancialAccountId = account.Id, ReceivedDateUtc = AsUtc(request.ReceivedDateUtc), ReferenceNumber = Clean(request.ReferenceNumber),
                Notes = Clean(request.Notes), CreatedByUserId = actorId, PostedAtUtc = UtcNow()
            };
            await repository.AddCustomerAdvanceAsync(advance, ct);
            await Audit(actorId, "CustomerAdvanceRecorded", advance.Id, new { advance.AdvanceNumber, advance.CustomerId, advance.Amount }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync(advance!, cancellationToken);
    }

    public async Task<CustomerAdvanceDto> ApplyCustomerAdvanceAsync(Guid actorId, Guid advanceId, ApplyCustomerAdvanceRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsAdvancesApply, cancellationToken);
        var advance = await repository.GetCustomerAdvanceAsync(advanceId, cancellationToken) ?? throw new ResourceNotFoundException("Customer advance was not found.");
        EnsureBranchAccess(actor, advance.BranchId);
        ValidatePositive(request.Amount, "Amount");
        var remaining = decimal.Round(advance.Amount - advance.AmountApplied, 2);
        if (request.Amount > remaining) throw new RequestValidationException($"Only {remaining:0.00} of this advance remains unapplied.");
        var sale = await repository.GetSaleAsync(request.SaleId, cancellationToken);
        if (sale is null || sale.CustomerId != advance.CustomerId) throw new RequestValidationException("The selected sale does not belong to this customer.");
        var receivables = await repository.GetOpenReceivablesAsync(advance.CustomerId, null, cancellationToken);
        var target = receivables.FirstOrDefault(x => x.SaleId == request.SaleId);
        if (target is null || request.Amount > target.Outstanding) throw new RequestValidationException("Applied amount exceeds this invoice's outstanding balance.");

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var id = Guid.NewGuid();
            var dateUtc = UtcNow();
            var number = $"{advance.AdvanceNumber}-APP-{advance.Applications.Count + 1}";
            var payment = new CustomerPayment
            {
                ReceiptNumber = number, CustomerId = advance.CustomerId, BranchId = advance.BranchId, Amount = Money(request.Amount),
                PaymentMethod = CustomerPaymentMethod.AppliedAdvance, PaymentDateUtc = dateUtc, ReferenceNumber = advance.AdvanceNumber,
                Notes = $"Advance {advance.AdvanceNumber} applied to invoice", ReceivedByUserId = actorId
            };
            await repository.AddCustomerPaymentAsync(payment, ct);
            await repository.AddCustomerLedgerEntryAsync(new CustomerLedgerEntry
            {
                CustomerId = advance.CustomerId, BranchId = advance.BranchId, EntryType = CustomerLedgerEntryType.Payment, Amount = -Money(request.Amount),
                EntryDate = DateOnly.FromDateTime(dateUtc), ReferenceNumber = advance.AdvanceNumber, ReferenceType = "AppliedAdvance",
                ReferenceId = payment.Id, Notes = $"Advance {advance.AdvanceNumber} applied", CreatedByUserId = actorId
            }, ct);
            await repository.AddCustomerPaymentAllocationAsync(new CustomerPaymentAllocation
            {
                CustomerPaymentId = payment.Id, SaleId = request.SaleId, CustomerId = advance.CustomerId, BranchId = advance.BranchId,
                AllocatedAmount = Money(request.Amount), AllocatedAtUtc = dateUtc, CreatedByUserId = actorId
            }, ct);
            await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.CustomerAdvanceApplication, id, advance.BranchId, dateUtc,
                number, $"Apply advance {advance.AdvanceNumber} to {sale.InvoiceNumber}", actorId,
                [new(AccountMappingKey.CustomerAdvances, request.Amount, 0, CustomerId: advance.CustomerId), new(AccountMappingKey.AccountsReceivable, 0, request.Amount, CustomerId: advance.CustomerId)]), ct);
            await repository.AddCustomerAdvanceApplicationAsync(new CustomerAdvanceApplication
            {
                Id = id, CustomerAdvanceId = advance.Id, SaleId = request.SaleId, AppliedAmount = Money(request.Amount), AppliedAtUtc = dateUtc,
                CustomerPaymentId = payment.Id, CreatedByUserId = actorId
            }, ct);
            advance.AmountApplied += Money(request.Amount);
            advance.UpdatedAt = dateUtc;
            await Audit(actorId, "CustomerAdvanceApplied", advance.Id, new { advance.AdvanceNumber, request.SaleId, request.Amount }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync((await repository.GetCustomerAdvanceAsync(advanceId, cancellationToken))!, cancellationToken);
    }

    // ---- Supplier advances ----

    public async Task<IReadOnlyList<SupplierAdvanceDto>> ListSupplierAdvancesAsync(Guid actorId, Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsAdvancesView, cancellationToken);
        var advances = await repository.ListSupplierAdvancesAsync(supplierId, Scope(actor, branchId), cancellationToken);
        var results = new List<SupplierAdvanceDto>();
        foreach (var advance in advances) results.Add(await MapAsync(advance, cancellationToken));
        return results;
    }

    public async Task<SupplierAdvanceDto> RecordSupplierAdvanceAsync(Guid actorId, RecordSupplierAdvanceRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsAdvancesCreate, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        ValidatePositive(request.Amount, "Amount");
        if (await repository.GetSupplierAsync(request.SupplierId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Supplier is invalid or inactive.");
        var account = await repository.GetFinancialAccountAsync(request.FinancialAccountId, cancellationToken);
        if (account is not { IsActive: true } || account.BranchId != request.BranchId) throw new RequestValidationException("Financial account is invalid, inactive, or belongs to another branch.");
        if (await repository.GetFinancialAccountBalanceAsync(account.Id, cancellationToken) < Money(request.Amount))
            throw new ResourceConflictException("The financial account has insufficient funds.");

        SupplierAdvance? advance = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var id = Guid.NewGuid();
            var number = await repository.NextNumberAsync("SupplierAdvanceNumberSequence", "SADV", request.PaidDateUtc, ct);
            await repository.AddFinancialLedgerEntryAsync(new FinancialLedgerEntry
            {
                FinancialAccountId = account.Id, BranchId = request.BranchId, EntryType = FinancialLedgerEntryType.SupplierPayment, Amount = -Money(request.Amount),
                ReferenceType = "SupplierAdvance", ReferenceId = id, ReferenceNumber = number, Description = $"Supplier advance {number}",
                CreatedByUserId = actorId, OccurredAtUtc = AsUtc(request.PaidDateUtc)
            }, ct);
            await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.SupplierAdvance, id, request.BranchId, request.PaidDateUtc,
                number, $"Supplier advance {number}", actorId,
                [new(AccountMappingKey.SupplierAdvances, request.Amount, 0, SupplierId: request.SupplierId), new(PaymentAccount(account.AccountType), 0, request.Amount)]), ct);
            advance = new SupplierAdvance
            {
                Id = id, AdvanceNumber = number, SupplierId = request.SupplierId, BranchId = request.BranchId, Amount = Money(request.Amount), AmountApplied = 0,
                FinancialAccountId = account.Id, PaidDateUtc = AsUtc(request.PaidDateUtc), ReferenceNumber = Clean(request.ReferenceNumber),
                Notes = Clean(request.Notes), CreatedByUserId = actorId, PostedAtUtc = UtcNow()
            };
            await repository.AddSupplierAdvanceAsync(advance, ct);
            await Audit(actorId, "SupplierAdvanceRecorded", advance.Id, new { advance.AdvanceNumber, advance.SupplierId, advance.Amount }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync(advance!, cancellationToken);
    }

    public async Task<SupplierAdvanceDto> ApplySupplierAdvanceAsync(Guid actorId, Guid advanceId, ApplySupplierAdvanceRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsAdvancesApply, cancellationToken);
        var advance = await repository.GetSupplierAdvanceAsync(advanceId, cancellationToken) ?? throw new ResourceNotFoundException("Supplier advance was not found.");
        EnsureBranchAccess(actor, advance.BranchId);
        var advanceJournal = await repository.GetJournalForSourceAsync(JournalSourceType.SupplierAdvance, advanceId, cancellationToken);
        if (advanceJournal is not null && await repository.JournalHasReversalAsync(advanceJournal.Id, cancellationToken))
            throw new RequestValidationException("A reversed supplier advance cannot be applied.");
        ValidatePositive(request.Amount, "Amount");
        var remaining = decimal.Round(advance.Amount - advance.AmountApplied, 2);
        if (request.Amount > remaining) throw new RequestValidationException($"Only {remaining:0.00} of this advance remains unapplied.");
        var goodsReceipt = await repository.GetGoodsReceiptAsync(request.GoodsReceiptId, cancellationToken);
        if (goodsReceipt is null || goodsReceipt.SupplierId != advance.SupplierId) throw new RequestValidationException("The selected goods receipt does not belong to this supplier.");
        var payables = await repository.GetOpenPayablesAsync(advance.SupplierId, null, cancellationToken);
        var target = payables.FirstOrDefault(x => x.GoodsReceiptId == request.GoodsReceiptId);
        if (target is null || request.Amount > target.Outstanding) throw new RequestValidationException("Applied amount exceeds this goods receipt's outstanding balance.");

        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var id = Guid.NewGuid();
            var dateUtc = UtcNow();
            var number = $"{advance.AdvanceNumber}-APP-{advance.Applications.Count + 1}";
            var ledgerEntry = new SupplierLedgerEntry
            {
                SupplierId = advance.SupplierId, BranchId = advance.BranchId, EntryType = SupplierLedgerEntryType.Payment, Amount = -Money(request.Amount),
                EntryDate = DateOnly.FromDateTime(dateUtc), PaymentMethod = "AppliedAdvance", ReferenceNumber = advance.AdvanceNumber,
                ReferenceType = "AppliedAdvance", Notes = $"Advance {advance.AdvanceNumber} applied", CreatedByUserId = actorId
            };
            await repository.AddSupplierLedgerEntryAsync(ledgerEntry, ct);
            await repository.AddSupplierPaymentAllocationAsync(new SupplierPaymentAllocation
            {
                SupplierLedgerEntryId = ledgerEntry.Id, GoodsReceiptId = request.GoodsReceiptId, SupplierId = advance.SupplierId, BranchId = advance.BranchId,
                AllocatedAmount = Money(request.Amount), AllocatedAtUtc = dateUtc, CreatedByUserId = actorId
            }, ct);
            await journalPosting.PostAsync(new JournalPostingRequest(JournalSourceType.SupplierAdvanceApplication, id, advance.BranchId, dateUtc,
                number, $"Apply advance {advance.AdvanceNumber} to {goodsReceipt.GrnNumber}", actorId,
                [new(AccountMappingKey.AccountsPayable, request.Amount, 0, SupplierId: advance.SupplierId), new(AccountMappingKey.SupplierAdvances, 0, request.Amount, SupplierId: advance.SupplierId)]), ct);
            await repository.AddSupplierAdvanceApplicationAsync(new SupplierAdvanceApplication
            {
                Id = id, SupplierAdvanceId = advance.Id, GoodsReceiptId = request.GoodsReceiptId, AppliedAmount = Money(request.Amount), AppliedAtUtc = dateUtc,
                SupplierLedgerEntryId = ledgerEntry.Id, CreatedByUserId = actorId
            }, ct);
            advance.AmountApplied += Money(request.Amount);
            advance.UpdatedAt = dateUtc;
            await Audit(actorId, "SupplierAdvanceApplied", advance.Id, new { advance.AdvanceNumber, request.GoodsReceiptId, request.Amount }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync((await repository.GetSupplierAdvanceAsync(advanceId, cancellationToken))!, cancellationToken);
    }

    // ---- Shared settlement helpers ----

    private async Task<CustomerPayment> SettleCustomerReceivableAsync(Guid customerId, Guid branchId, decimal amount, CustomerPaymentMethod method,
        Guid? appliedToSaleId, string referenceNumber, string description, Guid actorId, DateTime dateUtc, CancellationToken ct)
    {
        var payment = new CustomerPayment
        {
            ReceiptNumber = referenceNumber, CustomerId = customerId, BranchId = branchId, Amount = Money(amount), PaymentMethod = method,
            PaymentDateUtc = AsUtc(dateUtc), ReferenceNumber = referenceNumber, Notes = description, ReceivedByUserId = actorId
        };
        await repository.AddCustomerPaymentAsync(payment, ct);
        await repository.AddCustomerLedgerEntryAsync(new CustomerLedgerEntry
        {
            CustomerId = customerId, BranchId = branchId, EntryType = CustomerLedgerEntryType.Payment, Amount = -Money(amount),
            EntryDate = DateOnly.FromDateTime(dateUtc), ReferenceNumber = referenceNumber, ReferenceType = method.ToString(),
            ReferenceId = payment.Id, Notes = description, CreatedByUserId = actorId
        }, ct);
        if (appliedToSaleId.HasValue)
        {
            await repository.AddCustomerPaymentAllocationAsync(new CustomerPaymentAllocation
            {
                CustomerPaymentId = payment.Id, SaleId = appliedToSaleId.Value, CustomerId = customerId, BranchId = branchId,
                AllocatedAmount = Money(amount), AllocatedAtUtc = AsUtc(dateUtc), CreatedByUserId = actorId
            }, ct);
        }
        else
        {
            await CustomerPaymentAllocator.AllocateFifoAsync(repository.GetOpenReceivablesAsync, repository.AddCustomerPaymentAllocationAsync,
                customerId, branchId, payment.Id, amount, actorId, dateUtc, ct);
        }
        return payment;
    }

    private async Task<SupplierLedgerEntry> SettleSupplierPayableAsync(Guid supplierId, Guid branchId, decimal amount, string paymentMethod,
        Guid? appliedToGoodsReceiptId, string referenceNumber, string description, Guid actorId, DateTime dateUtc, CancellationToken ct)
    {
        var entry = new SupplierLedgerEntry
        {
            SupplierId = supplierId, BranchId = branchId, EntryType = SupplierLedgerEntryType.Payment, Amount = -Money(amount),
            EntryDate = DateOnly.FromDateTime(dateUtc), PaymentMethod = paymentMethod, ReferenceNumber = referenceNumber,
            ReferenceType = paymentMethod, Notes = description, CreatedByUserId = actorId
        };
        await repository.AddSupplierLedgerEntryAsync(entry, ct);
        if (appliedToGoodsReceiptId.HasValue)
        {
            await repository.AddSupplierPaymentAllocationAsync(new SupplierPaymentAllocation
            {
                SupplierLedgerEntryId = entry.Id, GoodsReceiptId = appliedToGoodsReceiptId.Value, SupplierId = supplierId, BranchId = branchId,
                AllocatedAmount = Money(amount), AllocatedAtUtc = AsUtc(dateUtc), CreatedByUserId = actorId
            }, ct);
        }
        else
        {
            await SupplierPaymentAllocator.AllocateFifoAsync(repository.GetOpenPayablesAsync, repository.AddSupplierPaymentAllocationAsync,
                supplierId, branchId, entry.Id, amount, actorId, dateUtc, ct);
        }
        return entry;
    }

    private async Task ValidateAppliedSaleAsync(Guid customerId, Guid? saleId, decimal amount, CancellationToken ct)
    {
        if (!saleId.HasValue) return;
        var receivables = await repository.GetOpenReceivablesAsync(customerId, null, ct);
        var target = receivables.FirstOrDefault(x => x.SaleId == saleId);
        if (target is null || amount > target.Outstanding) throw new RequestValidationException("Amount exceeds this invoice's outstanding balance, or the invoice does not belong to this customer.");
    }

    private async Task ValidateAppliedGoodsReceiptAsync(Guid supplierId, Guid? goodsReceiptId, decimal amount, CancellationToken ct)
    {
        if (!goodsReceiptId.HasValue) return;
        var payables = await repository.GetOpenPayablesAsync(supplierId, null, ct);
        var target = payables.FirstOrDefault(x => x.GoodsReceiptId == goodsReceiptId);
        if (target is null || amount > target.Outstanding) throw new RequestValidationException("Amount exceeds this goods receipt's outstanding balance, or it does not belong to this supplier.");
    }

    private static AccountMappingKey PaymentAccount(FinancialAccountType type) => type == FinancialAccountType.Cash ? AccountMappingKey.Cash : AccountMappingKey.Bank;

    // ---- Mapping ----

    private static CreditNoteDto Map(CreditNote n) => new(n.Id, n.CreditNoteNumber, n.CustomerId, n.Customer?.Name ?? string.Empty, n.BranchId, n.Amount,
        n.Reason, n.Notes, n.IssueDateUtc, n.AppliedToSaleId, n.AppliedToSale?.InvoiceNumber, n.CreatedByUser?.FullName ?? string.Empty, n.PostedAtUtc, string.Empty);
    private async Task<CreditNoteDto> MapAsync(CreditNote n, CancellationToken ct)
    {
        var m = Map(n);
        return m with { JournalEntryNumber = await repository.GetJournalEntryNumberForSourceAsync(JournalSourceType.CreditNote, n.Id, ct) };
    }

    private static DebitNoteDto Map(DebitNote n) => new(n.Id, n.DebitNoteNumber, n.SupplierId, n.Supplier?.Name ?? string.Empty, n.BranchId, n.Amount,
        n.Reason, n.Notes, n.IssueDateUtc, n.AppliedToGoodsReceiptId, n.AppliedToGoodsReceipt?.GrnNumber, n.CreatedByUser?.FullName ?? string.Empty, n.PostedAtUtc, string.Empty);
    private async Task<DebitNoteDto> MapAsync(DebitNote n, CancellationToken ct)
    {
        var m = Map(n);
        return m with { JournalEntryNumber = await repository.GetJournalEntryNumberForSourceAsync(JournalSourceType.DebitNote, n.Id, ct) };
    }

    private static CustomerWriteOffDto Map(CustomerWriteOff w) => new(w.Id, w.WriteOffNumber, w.CustomerId, w.Customer?.Name ?? string.Empty, w.BranchId, w.Amount,
        w.Reason, w.Notes, w.WriteOffDateUtc, w.AppliedToSaleId, w.CreatedByUser?.FullName ?? string.Empty, w.PostedAtUtc, string.Empty);
    private async Task<CustomerWriteOffDto> MapAsync(CustomerWriteOff w, CancellationToken ct)
    {
        var m = Map(w);
        return m with { JournalEntryNumber = await repository.GetJournalEntryNumberForSourceAsync(JournalSourceType.CustomerWriteOff, w.Id, ct) };
    }

    private static SupplierWriteOffDto Map(SupplierWriteOff w) => new(w.Id, w.WriteOffNumber, w.SupplierId, w.Supplier?.Name ?? string.Empty, w.BranchId, w.Amount,
        w.Reason, w.Notes, w.WriteOffDateUtc, w.AppliedToGoodsReceiptId, w.CreatedByUser?.FullName ?? string.Empty, w.PostedAtUtc, string.Empty);
    private async Task<SupplierWriteOffDto> MapAsync(SupplierWriteOff w, CancellationToken ct)
    {
        var m = Map(w);
        return m with { JournalEntryNumber = await repository.GetJournalEntryNumberForSourceAsync(JournalSourceType.SupplierWriteOff, w.Id, ct) };
    }

    private static CustomerAdvanceDto Map(CustomerAdvance a) => new(a.Id, a.AdvanceNumber, a.CustomerId, a.Customer?.Name ?? string.Empty, a.BranchId, a.Amount,
        a.AmountApplied, decimal.Round(a.Amount - a.AmountApplied, 2), a.FinancialAccountId, a.FinancialAccount?.Name ?? string.Empty,
        a.ReceivedDateUtc, a.ReferenceNumber, a.Notes, a.CreatedByUser?.FullName ?? string.Empty, a.PostedAtUtc);
    private Task<CustomerAdvanceDto> MapAsync(CustomerAdvance a, CancellationToken ct) => Task.FromResult(Map(a));

    private static SupplierAdvanceDto Map(SupplierAdvance a) => new(a.Id, a.AdvanceNumber, a.SupplierId, a.Supplier?.Name ?? string.Empty, a.BranchId, a.Amount,
        a.AmountApplied, decimal.Round(a.Amount - a.AmountApplied, 2), a.FinancialAccountId, a.FinancialAccount?.Name ?? string.Empty,
        a.PaidDateUtc, a.ReferenceNumber, a.Notes, a.CreatedByUser?.FullName ?? string.Empty, a.PostedAtUtc);
    private async Task<SupplierAdvanceDto> MapAsync(SupplierAdvance a, CancellationToken ct)
    {
        var journal = await repository.GetJournalForSourceAsync(JournalSourceType.SupplierAdvance, a.Id, ct);
        var reversed = journal is not null && await repository.JournalHasReversalAsync(journal.Id, ct);
        return Map(a) with { IsReversed = reversed, AmountRemaining = reversed ? 0 : decimal.Round(a.Amount - a.AmountApplied, 2) };
    }

    private async Task<Domain.Entities.User> Require(Guid actorId, string permission, CancellationToken ct)
    {
        var actor = await repository.GetActorAsync(actorId, ct);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private static Guid? Scope(Domain.Entities.User actor, Guid? requested) { if (CanSelectBranch(actor)) return requested; if (requested.HasValue && requested != actor.BranchId) throw new ForbiddenOperationException("The current user cannot access this branch."); return actor.BranchId; }
    private static void EnsureBranchAccess(Domain.Entities.User actor, Guid branchId) { if (!CanSelectBranch(actor) && actor.BranchId != branchId) throw new ForbiddenOperationException("The current user cannot access this branch."); }
    private static bool CanSelectBranch(Domain.Entities.User actor) => actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager;
    private static void ValidatePositive(decimal amount, string label) { if (amount <= 0 || decimal.Round(amount, 2) != amount) throw new RequestValidationException(label + " must be greater than zero with at most two decimal places."); }
    private static void ValidateReason(string reason) { if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) throw new RequestValidationException("Reason is required and must be 500 characters or fewer."); }
    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
    private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private async Task Audit(Guid userId, string action, Guid entityId, object values, CancellationToken ct) =>
        await repository.AddAuditAsync(new AuditLog { UserId = userId, Action = action, EntityType = "PartyAdjustment", EntityId = entityId, NewValues = JsonSerializer.Serialize(values) }, ct);
}
