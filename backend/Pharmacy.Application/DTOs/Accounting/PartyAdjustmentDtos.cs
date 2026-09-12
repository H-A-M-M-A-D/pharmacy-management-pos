namespace Pharmacy.Application.DTOs.Accounting;

// ---- Credit / debit notes ----

public sealed record CreateCreditNoteRequest(Guid CustomerId, Guid BranchId, decimal Amount, string Reason, string? Notes, DateTime IssueDateUtc, Guid? AppliedToSaleId);
public sealed record CreditNoteDto(
    Guid Id, string CreditNoteNumber, Guid CustomerId, string CustomerName, Guid BranchId, decimal Amount,
    string Reason, string? Notes, DateTime IssueDateUtc, Guid? AppliedToSaleId, string? AppliedToInvoiceNumber,
    string CreatedBy, DateTime PostedAtUtc, string JournalEntryNumber);

public sealed record CreateDebitNoteRequest(Guid SupplierId, Guid BranchId, decimal Amount, string Reason, string? Notes, DateTime IssueDateUtc, Guid? AppliedToGoodsReceiptId);
public sealed record DebitNoteDto(
    Guid Id, string DebitNoteNumber, Guid SupplierId, string SupplierName, Guid BranchId, decimal Amount,
    string Reason, string? Notes, DateTime IssueDateUtc, Guid? AppliedToGoodsReceiptId, string? AppliedToGrnNumber,
    string CreatedBy, DateTime PostedAtUtc, string JournalEntryNumber);

// ---- Write-offs ----

public sealed record CreateCustomerWriteOffRequest(Guid CustomerId, Guid BranchId, decimal Amount, string Reason, string? Notes, DateTime WriteOffDateUtc, Guid? AppliedToSaleId);
public sealed record CustomerWriteOffDto(
    Guid Id, string WriteOffNumber, Guid CustomerId, string CustomerName, Guid BranchId, decimal Amount,
    string Reason, string? Notes, DateTime WriteOffDateUtc, Guid? AppliedToSaleId, string CreatedBy,
    DateTime PostedAtUtc, string JournalEntryNumber);

public sealed record CreateSupplierWriteOffRequest(Guid SupplierId, Guid BranchId, decimal Amount, string Reason, string? Notes, DateTime WriteOffDateUtc, Guid? AppliedToGoodsReceiptId);
public sealed record SupplierWriteOffDto(
    Guid Id, string WriteOffNumber, Guid SupplierId, string SupplierName, Guid BranchId, decimal Amount,
    string Reason, string? Notes, DateTime WriteOffDateUtc, Guid? AppliedToGoodsReceiptId, string CreatedBy,
    DateTime PostedAtUtc, string JournalEntryNumber);

// ---- Advances ----

public sealed record RecordCustomerAdvanceRequest(Guid CustomerId, Guid BranchId, decimal Amount, Guid FinancialAccountId, DateTime ReceivedDateUtc, string? ReferenceNumber, string? Notes);
public sealed record CustomerAdvanceDto(
    Guid Id, string AdvanceNumber, Guid CustomerId, string CustomerName, Guid BranchId, decimal Amount,
    decimal AmountApplied, decimal AmountRemaining, Guid FinancialAccountId, string FinancialAccountName,
    DateTime ReceivedDateUtc, string? ReferenceNumber, string? Notes, string CreatedBy, DateTime PostedAtUtc);
public sealed record ApplyCustomerAdvanceRequest(Guid SaleId, decimal Amount);

public sealed record RecordSupplierAdvanceRequest(Guid SupplierId, Guid BranchId, decimal Amount, Guid FinancialAccountId, DateTime PaidDateUtc, string? ReferenceNumber, string? Notes);
public sealed record SupplierAdvanceDto(
    Guid Id, string AdvanceNumber, Guid SupplierId, string SupplierName, Guid BranchId, decimal Amount,
    decimal AmountApplied, decimal AmountRemaining, Guid FinancialAccountId, string FinancialAccountName,
    DateTime PaidDateUtc, string? ReferenceNumber, string? Notes, string CreatedBy, DateTime PostedAtUtc, bool IsReversed = false);

public sealed record ReverseSupplierAdjustmentRequest(string Reason);
public sealed record SupplierAdjustmentReversalDto(Guid JournalEntryId, string JournalEntryNumber);
public sealed record ApplySupplierAdvanceRequest(Guid GoodsReceiptId, decimal Amount);
