using System.Data;
using Pharmacy.Application.DTOs.Customers;
using Pharmacy.Application.DTOs.Suppliers;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.PartyAdjustments;

public interface IPartyAdjustmentRepository
{
    Task<JournalEntry?> GetJournalForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default);
    Task<bool> JournalHasReversalAsync(Guid journalId, CancellationToken cancellationToken = default);
    Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default);
    Task<SupplierLedgerEntry?> GetSupplierSettlementAsync(Guid supplierId, string referenceNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierPaymentAllocation>> RemoveSupplierAllocationsAsync(Guid ledgerEntryId, CancellationToken cancellationToken = default);
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Sale?> GetSaleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoodsReceipt?> GetGoodsReceiptAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FinancialAccount?> GetFinancialAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task<decimal> GetFinancialAccountBalanceAsync(Guid id, CancellationToken cancellationToken = default);

    Task<string> NextNumberAsync(string sequenceName, string prefix, DateTime dateUtc, CancellationToken cancellationToken = default);
    Task<string> GetJournalEntryNumberForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpenReceivableDto>> GetOpenReceivablesAsync(Guid customerId, Guid? branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpenPayableDto>> GetOpenPayablesAsync(Guid supplierId, Guid? branchId, CancellationToken cancellationToken = default);

    Task AddCustomerPaymentAsync(CustomerPayment payment, CancellationToken cancellationToken = default);
    Task AddCustomerPaymentAllocationAsync(CustomerPaymentAllocation allocation, CancellationToken cancellationToken = default);
    Task AddCustomerLedgerEntryAsync(CustomerLedgerEntry entry, CancellationToken cancellationToken = default);
    Task AddSupplierLedgerEntryAsync(SupplierLedgerEntry entry, CancellationToken cancellationToken = default);
    Task AddSupplierPaymentAllocationAsync(SupplierPaymentAllocation allocation, CancellationToken cancellationToken = default);
    Task AddFinancialLedgerEntryAsync(FinancialLedgerEntry entry, CancellationToken cancellationToken = default);

    Task AddCreditNoteAsync(CreditNote note, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CreditNote>> ListCreditNotesAsync(Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default);

    Task AddDebitNoteAsync(DebitNote note, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DebitNote>> ListDebitNotesAsync(Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default);

    Task AddCustomerWriteOffAsync(CustomerWriteOff writeOff, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerWriteOff>> ListCustomerWriteOffsAsync(Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default);

    Task AddSupplierWriteOffAsync(SupplierWriteOff writeOff, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierWriteOff>> ListSupplierWriteOffsAsync(Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default);

    Task AddCustomerAdvanceAsync(CustomerAdvance advance, CancellationToken cancellationToken = default);
    Task<CustomerAdvance?> GetCustomerAdvanceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerAdvance>> ListCustomerAdvancesAsync(Guid? customerId, Guid? branchId, CancellationToken cancellationToken = default);
    Task AddCustomerAdvanceApplicationAsync(CustomerAdvanceApplication application, CancellationToken cancellationToken = default);

    Task AddSupplierAdvanceAsync(SupplierAdvance advance, CancellationToken cancellationToken = default);
    Task<SupplierAdvance?> GetSupplierAdvanceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierAdvance>> ListSupplierAdvancesAsync(Guid? supplierId, Guid? branchId, CancellationToken cancellationToken = default);
    Task AddSupplierAdvanceApplicationAsync(SupplierAdvanceApplication application, CancellationToken cancellationToken = default);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
