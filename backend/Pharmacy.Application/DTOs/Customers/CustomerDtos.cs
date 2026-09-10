using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Customers;

public enum CustomerAdjustmentType { Debit, Credit }

public sealed record CustomerRequest(
    string Name, string? PhoneNumber, string? AlternatePhone, string? Email,
    string? Address, string? City, string? BusinessName, string? NTN,
    decimal OpeningBalance, decimal CreditLimit, bool IsActive = true, int? CreditDays = null);

public sealed record CustomerUpdateRequest(
    string Name, string? PhoneNumber, string? AlternatePhone, string? Email,
    string? Address, string? City, string? BusinessName, string? NTN,
    decimal CreditLimit, int? CreditDays = null);

public sealed record CustomerListQuery(
    int Page = 1, int PageSize = 25, string? Search = null, bool? IsActive = null,
    string? City = null, bool? HasOutstandingBalance = null, bool? OverCreditLimit = null,
    string SortBy = "name", bool Descending = false);

public sealed record CustomerListItemDto(
    Guid Id, string CustomerCode, string Name, string? PhoneNumber, string? Email,
    string? City, string? BusinessName, decimal CreditLimit, decimal OutstandingBalance,
    decimal AdvanceBalance, bool IsActive);

public sealed record CustomerDetailsDto(
    Guid Id, string CustomerCode, string Name, string? PhoneNumber, string? AlternatePhone,
    string? Email, string? Address, string? City, string? BusinessName, string? NTN,
    decimal OpeningBalance, decimal CreditLimit, bool IsActive,
    decimal OutstandingBalance, decimal AdvanceBalance, decimal TotalPayments,
    DateTime? LastPaymentAtUtc, DateTime CreatedAt, DateTime UpdatedAt, int? CreditDays = null);

/// <summary>
/// An open credit-sale document (an unsettled or partially settled invoice) used both by the
/// default FIFO payment-allocation logic and by AR aging drill-down. Outstanding is always
/// OriginalAmount minus allocated payments minus linked sales-return credit reductions.
/// </summary>
public sealed record OpenReceivableDto(
    Guid SaleId, Guid CustomerId, Guid BranchId, string? InvoiceNumber,
    DateOnly DocumentDate, DateTime? DueDateUtc, decimal OriginalAmount, decimal Outstanding);

public sealed record CustomerLookupDto(
    Guid Id, string CustomerCode, string Name, string? PhoneNumber, decimal CreditLimit,
    decimal OutstandingBalance, decimal AvailableCredit, bool IsActive);

public sealed record CustomerLedgerQuery(
    int Page = 1, int PageSize = 25, Guid? BranchId = null,
    CustomerLedgerEntryType? EntryType = null, DateOnly? DateFrom = null,
    DateOnly? DateTo = null);

public sealed record CustomerLedgerEntryDto(
    Guid Id, DateTime CreatedAt, DateOnly EntryDate, CustomerLedgerEntryType EntryType,
    decimal Amount, decimal RunningBalance, string BranchName, string? UserName,
    string? PaymentMethod, string? ReferenceNumber, string? ReferenceType,
    Guid? ReferenceId, string? Notes);

public sealed record CustomerPaymentRequest(
    Guid CustomerId, Guid BranchId, decimal Amount, DateTime PaymentDateUtc,
    CustomerPaymentMethod PaymentMethod, string? ReferenceNumber, string? Notes,
    Guid? FinancialAccountId = null);

public sealed record CustomerAdjustmentRequest(
    Guid CustomerId, Guid BranchId, CustomerAdjustmentType Type, decimal Amount,
    string Reason, string? Notes);

public sealed record CustomerPaymentReceiptDto(
    Guid Id, string ReceiptNumber, Guid CustomerId, string CustomerCode, string CustomerName,
    Guid BranchId, string BranchName, DateTime PaymentDateUtc, decimal Amount,
    CustomerPaymentMethod PaymentMethod, string? ReferenceNumber, string ReceivedByName,
    string? Notes);
