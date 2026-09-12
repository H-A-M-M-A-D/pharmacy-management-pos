using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.DTOs.Customers;
using Pharmacy.Application.DTOs.Suppliers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.Vouchers;

public interface IVoucherRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ChartOfAccount?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Dictionary<AccountMappingKey, Guid>> GetAccountMappingLookupAsync(CancellationToken cancellationToken = default);
    Task<FinancialAccount?> GetFinancialAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddFinancialLedgerEntryAsync(FinancialLedgerEntry entry, CancellationToken cancellationToken = default);

    Task<string> NextVoucherNumberAsync(VoucherType type, DateTime voucherDateUtc, CancellationToken cancellationToken = default);
    Task<string> NextJournalEntryNumberAsync(DateTime entryDateUtc, CancellationToken cancellationToken = default);
    Task AddVoucherAsync(Voucher voucher, CancellationToken cancellationToken = default);
    Task<Voucher?> GetVoucherAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<VoucherListItemDto>> ListVouchersAsync(VoucherListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task AddJournalEntryAsync(JournalEntry entry, CancellationToken cancellationToken = default);
    Task<bool> JournalEntryExistsForSourceAsync(JournalSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default);
    Task<bool> VoucherHasReversalAsync(Guid voucherId, CancellationToken cancellationToken = default);

    Task<string> NextCustomerPaymentReceiptNumberAsync(DateTime paymentDateUtc, CancellationToken cancellationToken = default);
    Task AddCustomerPaymentAsync(CustomerPayment payment, CancellationToken cancellationToken = default);
    Task AddCustomerLedgerEntryAsync(CustomerLedgerEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpenReceivableDto>> GetOpenReceivablesAsync(Guid customerId, Guid? branchId, CancellationToken cancellationToken = default);
    Task AddCustomerPaymentAllocationAsync(CustomerPaymentAllocation allocation, CancellationToken cancellationToken = default);

    Task AddSupplierLedgerEntryAsync(SupplierLedgerEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpenPayableDto>> GetOpenPayablesAsync(Guid supplierId, Guid? branchId, CancellationToken cancellationToken = default);
    Task AddSupplierPaymentAllocationAsync(SupplierPaymentAllocation allocation, CancellationToken cancellationToken = default);

    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    void AllowPostingIntoSoftClosedPeriod();
}
