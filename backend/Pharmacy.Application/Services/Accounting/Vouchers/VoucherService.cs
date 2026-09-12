using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting.PaymentAllocation;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.Vouchers;

/// <summary>
/// Business-friendly typed vouchers (Cash/Bank Receipt, Cash/Bank Payment, Contra, Journal) on top
/// of the existing <see cref="JournalEntry"/>/<see cref="JournalEntryLine"/> engine. There is no
/// second accounting engine here: posting a voucher resolves semantic accounts (Cash/Bank/AR/AP)
/// through the same <see cref="AccountMapping"/> table every other posting path uses, and every
/// posted voucher writes exactly one real journal entry, enforced balanced/immutable by the same
/// DbContext-level invariant as everything else. A voucher that targets a customer or supplier
/// additionally writes the same ledger-entry/payment/allocation rows the direct Customer/Supplier
/// payment APIs would, so the operational subledger and the GL stay reconcilable — it never posts
/// a bare AccountsReceivable/AccountsPayable line without also updating the subledger that backs it.
/// </summary>
public sealed class VoucherService(IVoucherRepository repository, TimeProvider timeProvider) : IVoucherService
{
    public async Task<VoucherDto> CreateDraftAsync(Guid actorId, VoucherCreateRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsVoucherCreate, cancellationToken);
        EnsureBranchAccess(actor, request.BranchId);
        if (string.IsNullOrWhiteSpace(request.Description)) throw new RequestValidationException("Description is required.");
        if (!Enum.IsDefined(request.Type)) throw new RequestValidationException("Voucher type is invalid.");
        if (await repository.GetBranchAsync(request.BranchId, cancellationToken) is not { IsActive: true })
            throw new RequestValidationException("Branch is invalid or inactive.");
        if (request.FinancialAccountId.HasValue)
        {
            if (request.Type is not (VoucherType.CashReceipt or VoucherType.CashPayment or VoucherType.BankReceipt or VoucherType.BankPayment))
                throw new RequestValidationException("A financial account can only be attached to a Cash/Bank Receipt or Payment voucher.");
            var financialAccount = await repository.GetFinancialAccountAsync(request.FinancialAccountId.Value, cancellationToken);
            if (financialAccount is not { IsActive: true } || financialAccount.BranchId != request.BranchId)
                throw new RequestValidationException("Financial account is invalid, inactive, or belongs to another branch.");
            var expectCash = request.Type is VoucherType.CashReceipt or VoucherType.CashPayment;
            if (expectCash != (financialAccount.AccountType == FinancialAccountType.Cash))
                throw new RequestValidationException(expectCash ? "Choose a Cash-type financial account for this voucher type." : "Choose a non-Cash financial account for this voucher type.");
        }

        Voucher? voucher = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            switch (request.Type)
            {
                case VoucherType.CashReceipt or VoucherType.BankReceipt:
                    await ValidateReceiptOrPaymentAsync(request, isReceipt: true, ct);
                    break;
                case VoucherType.CashPayment or VoucherType.BankPayment:
                    await ValidateReceiptOrPaymentAsync(request, isReceipt: false, ct);
                    break;
                case VoucherType.Contra:
                    await ValidateContraAsync(request, ct);
                    break;
                case VoucherType.Journal:
                    await ValidateJournalLinesAsync(request, ct);
                    break;
            }

            voucher = new Voucher
            {
                VoucherNumber = await repository.NextVoucherNumberAsync(request.Type, request.VoucherDateUtc, ct),
                Type = request.Type,
                VoucherDateUtc = request.VoucherDateUtc,
                BranchId = request.BranchId,
                Reference = Clean(request.Reference),
                Description = request.Description.Trim(),
                CustomerId = request.CustomerId,
                SupplierId = request.SupplierId,
                ChartOfAccountId = request.ChartOfAccountId,
                ContraToChartOfAccountId = request.ContraToChartOfAccountId,
                Amount = request.Amount,
                Status = VoucherStatus.Draft,
                CreatedByUserId = actorId,
                FinancialAccountId = request.FinancialAccountId
            };
            if (request.Type == VoucherType.Journal)
            {
                foreach (var line in request.Lines!)
                {
                    voucher.Lines.Add(new VoucherLine
                    {
                        ChartOfAccountId = line.ChartOfAccountId, Debit = line.Debit, Credit = line.Credit,
                        CustomerId = line.CustomerId, SupplierId = line.SupplierId, Description = Clean(line.Description)
                    });
                }
            }
            await repository.AddVoucherAsync(voucher, ct);
            await Audit(actorId, "VoucherDraftCreated", voucher.Id, new { voucher.VoucherNumber, voucher.Type, voucher.BranchId }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync((await repository.GetVoucherAsync(voucher!.Id, cancellationToken))!, cancellationToken);
    }

    public async Task<VoucherDto> PostAsync(Guid actorId, Guid voucherId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsVoucherPost, cancellationToken);
        if (HasPermission(actor, PermissionCatalog.AccountsPostToSoftClosed)) repository.AllowPostingIntoSoftClosedPeriod();
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var voucher = await repository.GetVoucherAsync(voucherId, ct) ?? throw new ResourceNotFoundException("Voucher was not found.");
            EnsureBranchAccess(actor, voucher.BranchId);
            if (voucher.Status != VoucherStatus.Draft) throw new RequestValidationException("Only a draft voucher can be posted.");

            var entry = voucher.Type switch
            {
                VoucherType.CashReceipt or VoucherType.BankReceipt => await PostReceiptAsync(actor, voucher, ct),
                VoucherType.CashPayment or VoucherType.BankPayment => await PostPaymentAsync(actor, voucher, ct),
                VoucherType.Contra => await PostContraAsync(voucher, ct),
                VoucherType.Journal => await PostJournalAsync(voucher, ct),
                _ => throw new InvalidOperationException("Unsupported voucher type.")
            };

            voucher.JournalEntryId = entry.Id;
            voucher.Status = VoucherStatus.Posted;
            voucher.PostedByUserId = actorId;
            voucher.PostedAtUtc = UtcNow();
            await Audit(actorId, "VoucherPosted", voucher.Id, new { voucher.VoucherNumber, entry.EntryNumber }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync((await repository.GetVoucherAsync(voucherId, cancellationToken))!, cancellationToken);
    }

    public async Task<VoucherDto> CancelAsync(Guid actorId, Guid voucherId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsVoucherCreate, cancellationToken);
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var voucher = await repository.GetVoucherAsync(voucherId, ct) ?? throw new ResourceNotFoundException("Voucher was not found.");
            EnsureBranchAccess(actor, voucher.BranchId);
            if (voucher.Status != VoucherStatus.Draft) throw new RequestValidationException("Only a draft voucher can be cancelled.");
            voucher.Status = VoucherStatus.Cancelled;
            await Audit(actorId, "VoucherCancelled", voucher.Id, new { voucher.VoucherNumber }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync((await repository.GetVoucherAsync(voucherId, cancellationToken))!, cancellationToken);
    }

    public async Task<VoucherDto> ReverseAsync(Guid actorId, Guid voucherId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsVoucherReverse, cancellationToken);
        if (HasPermission(actor, PermissionCatalog.AccountsPostToSoftClosed)) repository.AllowPostingIntoSoftClosedPeriod();
        Guid reversalId = Guid.Empty;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var original = await repository.GetVoucherAsync(voucherId, ct) ?? throw new ResourceNotFoundException("Voucher was not found.");
            EnsureBranchAccess(actor, original.BranchId);
            if (original.Status != VoucherStatus.Posted) throw new RequestValidationException("Only a posted voucher can be reversed.");
            if (original.CustomerId.HasValue || original.SupplierId.HasValue)
                throw new RequestValidationException("Vouchers linked to a customer or supplier cannot be auto-reversed, because the linked payment/ledger/allocation would go out of sync with the journal entry. Post a manual adjustment instead.");
            if (original.Lines.Count == 0) throw new InvalidOperationException("Posted voucher has no lines to reverse.");
            if (await repository.VoucherHasReversalAsync(original.Id, ct))
                throw new RequestValidationException("This voucher has already been reversed.");

            var reversal = new Voucher
            {
                VoucherNumber = await repository.NextVoucherNumberAsync(original.Type, UtcNow(), ct),
                Type = original.Type,
                VoucherDateUtc = UtcNow(),
                BranchId = original.BranchId,
                Reference = original.VoucherNumber,
                Description = $"Reversal of {original.VoucherNumber}",
                ChartOfAccountId = original.ChartOfAccountId,
                ContraToChartOfAccountId = original.ContraToChartOfAccountId,
                Amount = original.Amount,
                Status = VoucherStatus.Draft,
                CreatedByUserId = actorId,
                ReversalOfVoucherId = original.Id,
                FinancialAccountId = original.FinancialAccountId
            };
            foreach (var line in original.Lines)
            {
                reversal.Lines.Add(new VoucherLine
                {
                    ChartOfAccountId = line.ChartOfAccountId, Debit = line.Credit, Credit = line.Debit,
                    Description = line.Description
                });
            }
            await repository.AddVoucherAsync(reversal, ct);

            if (original.FinancialAccountId.HasValue)
            {
                var isReceipt = original.Type is VoucherType.CashReceipt or VoucherType.BankReceipt;
                await repository.AddFinancialLedgerEntryAsync(new FinancialLedgerEntry
                {
                    FinancialAccountId = original.FinancialAccountId.Value, BranchId = reversal.BranchId,
                    EntryType = isReceipt ? FinancialLedgerEntryType.AdjustmentDebit : FinancialLedgerEntryType.AdjustmentCredit,
                    Amount = isReceipt ? -original.Amount!.Value : original.Amount!.Value,
                    ReferenceType = "Voucher", ReferenceId = reversal.Id, ReferenceNumber = reversal.VoucherNumber,
                    Description = reversal.Description, CreatedByUserId = actor.Id, OccurredAtUtc = reversal.VoucherDateUtc
                }, ct);
            }

            var entry = await BuildAndAddJournalEntryAsync(reversal, actor.Id, ct, original.JournalEntryId, $"Reversal of voucher {original.VoucherNumber}");
            reversal.JournalEntryId = entry.Id;
            reversal.Status = VoucherStatus.Posted;
            reversal.PostedByUserId = actorId;
            reversal.PostedAtUtc = UtcNow();
            reversalId = reversal.Id;
            await Audit(actorId, "VoucherReversed", original.Id, new { original.VoucherNumber, ReversalVoucherNumber = reversal.VoucherNumber }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return await MapAsync((await repository.GetVoucherAsync(reversalId, cancellationToken))!, cancellationToken);
    }

    public async Task<VoucherDto> GetVoucherAsync(Guid actorId, Guid voucherId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsVoucherView, cancellationToken);
        var voucher = await repository.GetVoucherAsync(voucherId, cancellationToken) ?? throw new ResourceNotFoundException("Voucher was not found.");
        EnsureBranchAccess(actor, voucher.BranchId);
        return await MapAsync(voucher, cancellationToken);
    }

    public async Task<PagedResult<VoucherListItemDto>> ListVouchersAsync(Guid actorId, VoucherListQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsVoucherView, cancellationToken);
        if (query.Page < 1 || query.PageSize is < 1 or > 100) throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
        return await repository.ListVouchersAsync(query, actor.BranchId, CanSelectBranch(actor), cancellationToken);
    }

    // ---- posting ----

    private async Task<JournalEntry> PostReceiptAsync(User actor, Voucher voucher, CancellationToken ct)
    {
        var amount = voucher.Amount ?? throw new InvalidOperationException("Receipt voucher is missing its amount.");
        var cashOrBankKey = voucher.Type == VoucherType.CashReceipt ? AccountMappingKey.Cash : AccountMappingKey.Bank;
        var mappings = await repository.GetAccountMappingLookupAsync(ct);
        var cashOrBankAccountId = ResolveMapping(mappings, cashOrBankKey);

        Guid otherAccountId;
        Guid? customerId = voucher.CustomerId;
        if (voucher.CustomerId.HasValue)
        {
            otherAccountId = ResolveMapping(mappings, AccountMappingKey.AccountsReceivable);
            await RecordCustomerReceiptAsync(actor, voucher, amount, ct);
        }
        else
        {
            otherAccountId = voucher.ChartOfAccountId ?? throw new InvalidOperationException("Receipt voucher is missing its target account.");
        }

        voucher.Lines.Add(new VoucherLine { ChartOfAccountId = cashOrBankAccountId, Debit = amount, Credit = 0 });
        voucher.Lines.Add(new VoucherLine { ChartOfAccountId = otherAccountId, Debit = 0, Credit = amount, CustomerId = customerId });
        if (voucher.FinancialAccountId.HasValue)
        {
            await repository.AddFinancialLedgerEntryAsync(new FinancialLedgerEntry
            {
                FinancialAccountId = voucher.FinancialAccountId.Value, BranchId = voucher.BranchId,
                EntryType = customerId.HasValue ? FinancialLedgerEntryType.CustomerPayment : FinancialLedgerEntryType.AdjustmentCredit, Amount = amount,
                ReferenceType = "Voucher", ReferenceId = voucher.Id, ReferenceNumber = voucher.VoucherNumber,
                Description = voucher.Description, CreatedByUserId = actor.Id, OccurredAtUtc = voucher.VoucherDateUtc
            }, ct);
        }
        return await BuildAndAddJournalEntryAsync(voucher, actor.Id, ct);
    }

    private async Task<JournalEntry> PostPaymentAsync(User actor, Voucher voucher, CancellationToken ct)
    {
        var amount = voucher.Amount ?? throw new InvalidOperationException("Payment voucher is missing its amount.");
        var cashOrBankKey = voucher.Type == VoucherType.CashPayment ? AccountMappingKey.Cash : AccountMappingKey.Bank;
        var mappings = await repository.GetAccountMappingLookupAsync(ct);
        var cashOrBankAccountId = ResolveMapping(mappings, cashOrBankKey);

        Guid otherAccountId;
        Guid? supplierId = voucher.SupplierId;
        if (voucher.SupplierId.HasValue)
        {
            otherAccountId = ResolveMapping(mappings, AccountMappingKey.AccountsPayable);
            await RecordSupplierPaymentAsync(actor, voucher, amount, ct);
        }
        else
        {
            otherAccountId = voucher.ChartOfAccountId ?? throw new InvalidOperationException("Payment voucher is missing its target account.");
        }

        voucher.Lines.Add(new VoucherLine { ChartOfAccountId = otherAccountId, Debit = amount, Credit = 0, SupplierId = supplierId });
        voucher.Lines.Add(new VoucherLine { ChartOfAccountId = cashOrBankAccountId, Debit = 0, Credit = amount });
        if (voucher.FinancialAccountId.HasValue)
        {
            await repository.AddFinancialLedgerEntryAsync(new FinancialLedgerEntry
            {
                FinancialAccountId = voucher.FinancialAccountId.Value, BranchId = voucher.BranchId,
                EntryType = supplierId.HasValue ? FinancialLedgerEntryType.SupplierPayment : FinancialLedgerEntryType.AdjustmentDebit, Amount = -amount,
                ReferenceType = "Voucher", ReferenceId = voucher.Id, ReferenceNumber = voucher.VoucherNumber,
                Description = voucher.Description, CreatedByUserId = actor.Id, OccurredAtUtc = voucher.VoucherDateUtc
            }, ct);
        }
        return await BuildAndAddJournalEntryAsync(voucher, actor.Id, ct);
    }

    private async Task<JournalEntry> PostContraAsync(Voucher voucher, CancellationToken ct)
    {
        var amount = voucher.Amount ?? throw new InvalidOperationException("Contra voucher is missing its amount.");
        var fromAccountId = voucher.ChartOfAccountId ?? throw new InvalidOperationException("Contra voucher is missing its source account.");
        var toAccountId = voucher.ContraToChartOfAccountId ?? throw new InvalidOperationException("Contra voucher is missing its destination account.");
        voucher.Lines.Add(new VoucherLine { ChartOfAccountId = toAccountId, Debit = amount, Credit = 0 });
        voucher.Lines.Add(new VoucherLine { ChartOfAccountId = fromAccountId, Debit = 0, Credit = amount });
        return await BuildAndAddJournalEntryAsync(voucher, voucher.CreatedByUserId, ct);
    }

    private async Task<JournalEntry> PostJournalAsync(Voucher voucher, CancellationToken ct)
    {
        foreach (var line in voucher.Lines) await RequireActivePostingAccountAsync(line.ChartOfAccountId, ct);
        return await BuildAndAddJournalEntryAsync(voucher, voucher.CreatedByUserId, ct);
    }

    private async Task<JournalEntry> BuildAndAddJournalEntryAsync(Voucher voucher, Guid postedByUserId, CancellationToken ct,
        Guid? reversesJournalEntryId = null, string? reversalReason = null)
    {
        var entry = new JournalEntry
        {
            EntryNumber = await repository.NextJournalEntryNumberAsync(voucher.VoucherDateUtc, ct),
            EntryDateUtc = voucher.VoucherDateUtc,
            SourceType = SourceTypeFor(voucher.Type),
            SourceId = voucher.Id,
            Reference = voucher.Reference ?? voucher.VoucherNumber,
            Description = voucher.Description,
            BranchId = voucher.BranchId,
            PostedByUserId = postedByUserId,
            PostedAtUtc = UtcNow(),
            Status = JournalEntryStatus.Posted,
            ReversesJournalEntryId = reversesJournalEntryId,
            ReversalReason = reversalReason,
            Lines = voucher.Lines.Select(l => new JournalEntryLine
            {
                ChartOfAccountId = l.ChartOfAccountId, Debit = l.Debit, Credit = l.Credit,
                BranchId = voucher.BranchId, CustomerId = l.CustomerId, SupplierId = l.SupplierId, Description = l.Description
            }).ToList()
        };
        await repository.AddJournalEntryAsync(entry, ct);
        return entry;
    }

    private async Task RecordCustomerReceiptAsync(User actor, Voucher voucher, decimal amount, CancellationToken ct)
    {
        var customer = await repository.GetCustomerAsync(voucher.CustomerId!.Value, ct) ?? throw new RequestValidationException("Customer was not found.");
        if (!customer.IsActive) throw new RequestValidationException("Customer is invalid or inactive.");
        var receiptNumber = await repository.NextCustomerPaymentReceiptNumberAsync(voucher.VoucherDateUtc, ct);
        var paymentMethod = voucher.Type == VoucherType.CashReceipt ? CustomerPaymentMethod.Cash : CustomerPaymentMethod.BankTransfer;
        var payment = new CustomerPayment
        {
            ReceiptNumber = receiptNumber,
            CustomerId = voucher.CustomerId.Value,
            BranchId = voucher.BranchId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            PaymentDateUtc = voucher.VoucherDateUtc,
            ReferenceNumber = voucher.VoucherNumber,
            Notes = voucher.Description,
            ReceivedByUserId = actor.Id
        };
        await repository.AddCustomerPaymentAsync(payment, ct);
        await repository.AddCustomerLedgerEntryAsync(new CustomerLedgerEntry
        {
            CustomerId = voucher.CustomerId.Value,
            BranchId = voucher.BranchId,
            EntryType = CustomerLedgerEntryType.Payment,
            Amount = -amount,
            EntryDate = DateOnly.FromDateTime(voucher.VoucherDateUtc),
            PaymentMethod = paymentMethod.ToString(),
            ReferenceNumber = payment.ReceiptNumber,
            ReferenceType = "CustomerPayment",
            ReferenceId = payment.Id,
            Notes = $"Voucher {voucher.VoucherNumber}",
            CreatedByUserId = actor.Id
        }, ct);
        await CustomerPaymentAllocator.AllocateFifoAsync(repository.GetOpenReceivablesAsync, repository.AddCustomerPaymentAllocationAsync,
            voucher.CustomerId.Value, voucher.BranchId, payment.Id, amount, actor.Id, voucher.VoucherDateUtc, ct);
    }

    private async Task RecordSupplierPaymentAsync(User actor, Voucher voucher, decimal amount, CancellationToken ct)
    {
        var supplier = await repository.GetSupplierAsync(voucher.SupplierId!.Value, ct) ?? throw new RequestValidationException("Supplier was not found.");
        if (!supplier.IsActive) throw new RequestValidationException("Supplier is invalid or inactive.");
        var paymentMethod = voucher.Type == VoucherType.CashPayment ? "Cash" : "BankTransfer";
        var entry = new SupplierLedgerEntry
        {
            SupplierId = voucher.SupplierId.Value,
            BranchId = voucher.BranchId,
            EntryType = SupplierLedgerEntryType.Payment,
            Amount = -amount,
            EntryDate = DateOnly.FromDateTime(voucher.VoucherDateUtc),
            PaymentMethod = paymentMethod,
            ReferenceNumber = voucher.VoucherNumber,
            ReferenceType = "SupplierPayment",
            Notes = $"Voucher {voucher.VoucherNumber}",
            CreatedByUserId = actor.Id
        };
        await repository.AddSupplierLedgerEntryAsync(entry, ct);
        await SupplierPaymentAllocator.AllocateFifoAsync(repository.GetOpenPayablesAsync, repository.AddSupplierPaymentAllocationAsync,
            voucher.SupplierId.Value, voucher.BranchId, entry.Id, amount, actor.Id, voucher.VoucherDateUtc, ct);
    }

    // ---- validation ----

    private async Task ValidateReceiptOrPaymentAsync(VoucherCreateRequest request, bool isReceipt, CancellationToken ct)
    {
        if (request.Amount is not > 0) throw new RequestValidationException("Amount must be greater than zero.");
        if (isReceipt && request.SupplierId.HasValue) throw new RequestValidationException("A receipt voucher cannot target a supplier.");
        if (!isReceipt && request.CustomerId.HasValue) throw new RequestValidationException("A payment voucher cannot target a customer.");
        var partyId = isReceipt ? request.CustomerId : request.SupplierId;
        var choiceCount = (partyId.HasValue ? 1 : 0) + (request.ChartOfAccountId.HasValue ? 1 : 0);
        if (choiceCount != 1) throw new RequestValidationException("Choose exactly one of a party or an account for this voucher.");
        if (request.CustomerId.HasValue && await repository.GetCustomerAsync(request.CustomerId.Value, ct) is not { IsActive: true })
            throw new RequestValidationException("Customer is invalid or inactive.");
        if (request.SupplierId.HasValue && await repository.GetSupplierAsync(request.SupplierId.Value, ct) is not { IsActive: true })
            throw new RequestValidationException("Supplier is invalid or inactive.");
        if (request.ChartOfAccountId.HasValue) await RequireActivePostingAccountAsync(request.ChartOfAccountId.Value, ct);
    }

    private async Task ValidateContraAsync(VoucherCreateRequest request, CancellationToken ct)
    {
        if (request.Amount is not > 0) throw new RequestValidationException("Amount must be greater than zero.");
        if (request.CustomerId.HasValue || request.SupplierId.HasValue) throw new RequestValidationException("A contra voucher cannot target a customer or supplier.");
        if (request.ChartOfAccountId is null || request.ContraToChartOfAccountId is null) throw new RequestValidationException("Both a source and a destination account are required for a contra voucher.");
        if (request.ChartOfAccountId == request.ContraToChartOfAccountId) throw new RequestValidationException("Source and destination accounts must differ.");
        await RequireActivePostingAccountAsync(request.ChartOfAccountId.Value, ct);
        await RequireActivePostingAccountAsync(request.ContraToChartOfAccountId.Value, ct);
    }

    private async Task ValidateJournalLinesAsync(VoucherCreateRequest request, CancellationToken ct)
    {
        if (request.Lines is null || request.Lines.Count < 2) throw new RequestValidationException("A journal voucher requires at least two lines.");
        decimal totalDebit = 0, totalCredit = 0;
        foreach (var line in request.Lines)
        {
            if (line.Debit < 0 || line.Credit < 0) throw new RequestValidationException("Line amounts cannot be negative.");
            if (line.Debit > 0 && line.Credit > 0) throw new RequestValidationException("A line cannot carry both a debit and a credit.");
            if (line.Debit == 0 && line.Credit == 0) throw new RequestValidationException("Every line must carry a non-zero debit or credit.");
            await RequireActivePostingAccountAsync(line.ChartOfAccountId, ct);
            if (line.CustomerId.HasValue && await repository.GetCustomerAsync(line.CustomerId.Value, ct) is null) throw new RequestValidationException("Customer was not found.");
            if (line.SupplierId.HasValue && await repository.GetSupplierAsync(line.SupplierId.Value, ct) is null) throw new RequestValidationException("Supplier was not found.");
            totalDebit += line.Debit;
            totalCredit += line.Credit;
        }
        if (decimal.Round(totalDebit, 2) != decimal.Round(totalCredit, 2))
            throw new RequestValidationException($"The voucher does not balance: total debit {totalDebit} vs total credit {totalCredit}.");
    }

    private async Task RequireActivePostingAccountAsync(Guid id, CancellationToken ct)
    {
        var account = await repository.GetAccountAsync(id, ct) ?? throw new RequestValidationException("Account was not found.");
        if (!account.IsActive) throw new RequestValidationException($"Account {account.Code} is inactive.");
        if (!account.IsPostingAccount) throw new RequestValidationException($"Account {account.Code} is a header/summary account and cannot be posted to directly.");
    }

    private static Guid ResolveMapping(Dictionary<AccountMappingKey, Guid> mappings, AccountMappingKey key) =>
        mappings.TryGetValue(key, out var id) ? id : throw new InvalidOperationException($"No chart of accounts mapping is configured for '{key}'. Configure it under Accounts before posting.");

    private static JournalSourceType SourceTypeFor(VoucherType type) => type switch
    {
        VoucherType.CashReceipt => JournalSourceType.CashReceiptVoucher,
        VoucherType.CashPayment => JournalSourceType.CashPaymentVoucher,
        VoucherType.BankReceipt => JournalSourceType.BankReceiptVoucher,
        VoucherType.BankPayment => JournalSourceType.BankPaymentVoucher,
        VoucherType.Contra => JournalSourceType.ContraVoucher,
        VoucherType.Journal => JournalSourceType.JournalVoucher,
        _ => throw new InvalidOperationException("Unsupported voucher type.")
    };

    // ---- mapping / plumbing ----

    private static Task<VoucherDto> MapAsync(Voucher voucher, CancellationToken ct)
    {
        var lines = voucher.Lines.Select(l => new VoucherLineDto(l.Id, l.ChartOfAccountId, l.ChartOfAccount?.Code ?? string.Empty,
            l.ChartOfAccount?.Name ?? string.Empty, l.Debit, l.Credit, l.Customer?.Name, l.Supplier?.Name, l.Description)).ToList();
        return Task.FromResult(new VoucherDto(voucher.Id, voucher.VoucherNumber, voucher.Type, voucher.VoucherDateUtc, voucher.BranchId, voucher.Branch?.Name ?? string.Empty,
            voucher.Reference, voucher.Description, voucher.CustomerId, voucher.Customer?.Name, voucher.SupplierId, voucher.Supplier?.Name,
            voucher.ChartOfAccountId, voucher.ChartOfAccount?.Name, voucher.Status,
            voucher.CreatedByUser?.FullName ?? string.Empty, voucher.CreatedAt, voucher.PostedByUser?.FullName, voucher.PostedAtUtc,
            voucher.JournalEntryId, voucher.JournalEntry?.EntryNumber, voucher.ReversalOfVoucherId,
            lines.Sum(x => x.Debit), lines.Sum(x => x.Credit), lines, voucher.FinancialAccountId, voucher.FinancialAccount?.Name));
    }

    private async Task<User> Require(Guid actorId, string permission, CancellationToken ct)
    {
        var actor = await repository.GetActorAsync(actorId, ct);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private static bool HasPermission(User actor, string permission) =>
        actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) == true;

    private static bool CanSelectBranch(User actor) => actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager;

    private static void EnsureBranchAccess(User actor, Guid branchId)
    {
        if (!CanSelectBranch(actor) && actor.BranchId != branchId)
            throw new ForbiddenOperationException("The current user cannot access this branch.");
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private async Task Audit(Guid userId, string action, Guid entityId, object values, CancellationToken ct) =>
        await repository.AddAuditAsync(new AuditLog { UserId = userId, Action = action, EntityType = "Voucher", EntityId = entityId, NewValues = JsonSerializer.Serialize(values) }, ct);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
