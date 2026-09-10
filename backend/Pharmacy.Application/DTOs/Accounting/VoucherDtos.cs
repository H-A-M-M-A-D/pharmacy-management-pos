using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Accounting;

/// <summary>
/// One raw debit/credit line, used only for <see cref="VoucherType.Journal"/> vouchers where the
/// caller picks arbitrary accounts directly (mirrors <see cref="ManualJournalLineRequest"/>).
/// </summary>
public sealed record VoucherLineRequest(Guid ChartOfAccountId, decimal Debit, decimal Credit, Guid? CustomerId = null, Guid? SupplierId = null, string? Description = null);

/// <summary>
/// A single request shape covers every voucher type since they differ only in which optional
/// fields are required:
/// - Cash/Bank Receipt: Amount + exactly one of CustomerId (posts to Accounts Receivable) or ChartOfAccountId.
/// - Cash/Bank Payment: Amount + exactly one of SupplierId (posts to Accounts Payable) or ChartOfAccountId.
/// - Contra: Amount + ChartOfAccountId (credited/source) + ContraToChartOfAccountId (debited/destination).
/// - Journal: Lines (>= 2, arbitrary accounts, must balance) — Amount/ChartOfAccountId/party ignored.
/// </summary>
public sealed record VoucherCreateRequest(
    VoucherType Type, DateTime VoucherDateUtc, Guid BranchId, string? Reference, string Description,
    Guid? CustomerId = null, Guid? SupplierId = null, Guid? ChartOfAccountId = null, decimal? Amount = null,
    Guid? ContraToChartOfAccountId = null, IReadOnlyList<VoucherLineRequest>? Lines = null);

public sealed record VoucherLineDto(
    Guid Id, Guid ChartOfAccountId, string AccountCode, string AccountName, decimal Debit, decimal Credit,
    string? CustomerName, string? SupplierName, string? Description);

public sealed record VoucherDto(
    Guid Id, string VoucherNumber, VoucherType Type, DateTime VoucherDateUtc, Guid BranchId, string BranchName,
    string? Reference, string Description, Guid? CustomerId, string? CustomerName, Guid? SupplierId, string? SupplierName,
    Guid? ChartOfAccountId, string? ChartOfAccountName, VoucherStatus Status,
    string CreatedByName, DateTime CreatedAt, string? PostedByName, DateTime? PostedAtUtc,
    Guid? JournalEntryId, string? JournalEntryNumber, Guid? ReversalOfVoucherId,
    decimal TotalDebit, decimal TotalCredit, IReadOnlyList<VoucherLineDto> Lines);

public sealed record VoucherListItemDto(
    Guid Id, string VoucherNumber, VoucherType Type, DateTime VoucherDateUtc, Guid BranchId, string BranchName,
    string? Reference, string? PartyName, string Description, decimal TotalDebit, VoucherStatus Status, string? PostedByName);

public sealed record VoucherListQuery(
    int Page = 1, int PageSize = 25, Guid? BranchId = null, VoucherType? Type = null, VoucherStatus? Status = null,
    Guid? CustomerId = null, Guid? SupplierId = null, DateTime? FromUtc = null, DateTime? ToUtc = null, string? Search = null);
