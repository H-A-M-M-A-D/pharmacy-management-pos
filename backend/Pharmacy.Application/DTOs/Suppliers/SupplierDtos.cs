using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Suppliers;

public enum SupplierAdjustmentType { Debit, Credit }
public enum SupplierPaymentMethod { Cash, BankTransfer, Card, Cheque, Easypaisa, JazzCash, Other }

public sealed record SupplierRequest(
    string Name, string? ShortName, string? ContactPerson, string? PhoneNumber,
    string? AlternatePhone, string? WhatsApp, string? Email, string? Address,
    string? City, string? NTN, string? STRN, decimal OpeningBalance,
    decimal? CreditLimit, int? PaymentTermsDays, bool IsActive = true);

public sealed record SupplierUpdateRequest(
    string Name, string? ShortName, string? ContactPerson, string? PhoneNumber,
    string? AlternatePhone, string? WhatsApp, string? Email, string? Address,
    string? City, string? NTN, string? STRN, decimal? CreditLimit,
    int? PaymentTermsDays);

public sealed record SupplierListQuery(
    int Page = 1, int PageSize = 25, string? Search = null, bool? IsActive = null,
    string? City = null, bool? HasOutstandingBalance = null, string SortBy = "name",
    bool Descending = false);

public sealed record SupplierListItemDto(
    Guid Id, string Name, string? ShortName, string? ContactPerson, string? PhoneNumber,
    string? WhatsApp, string? Email, string? City, decimal? CreditLimit,
    decimal OutstandingBalance, bool IsActive);

public sealed record SupplierDetailsDto(
    Guid Id, string Name, string? ShortName, string? ContactPerson, string? PhoneNumber,
    string? AlternatePhone, string? WhatsApp, string? Email, string? Address,
    string? City, string? NTN, string? STRN, decimal OpeningBalance,
    decimal? CreditLimit, int? PaymentTermsDays, bool IsActive,
    decimal OutstandingBalance, decimal TotalPayments, DateOnly? LastPaymentDate,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record SupplierLookupDto(Guid Id, string Name, string? ShortName, bool IsActive);

public sealed record SupplierLedgerQuery(
    int Page = 1, int PageSize = 25, Guid? BranchId = null,
    SupplierLedgerEntryType? EntryType = null, DateOnly? DateFrom = null,
    DateOnly? DateTo = null);

public sealed record SupplierLedgerEntryDto(
    Guid Id, DateTime CreatedAt, DateOnly EntryDate, SupplierLedgerEntryType EntryType,
    decimal Amount, decimal RunningBalance, string BranchName, string? UserName,
    string? PaymentMethod, string? ReferenceNumber, string? ReferenceType,
    Guid? ReferenceId, string? Notes);

public sealed record SupplierPaymentRequest(
    Guid SupplierId, Guid BranchId, decimal Amount, DateOnly PaymentDate,
    SupplierPaymentMethod PaymentMethod, string? ReferenceNumber, string? Notes);

public sealed record SupplierAdjustmentRequest(
    Guid SupplierId, Guid BranchId, SupplierAdjustmentType Type, decimal Amount,
    string Reason, string? Notes);
