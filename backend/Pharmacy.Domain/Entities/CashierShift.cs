using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A cashier's till session for a branch, from opening the drawer to being reconciled by a manager.
/// Sales, refunds, and customer payments are attributed to a shift by cashier + branch + time window
/// rather than a foreign key, since exactly one shift can be open per cashier at a time.
/// </summary>
public class CashierShift : Entity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid CashierUserId { get; set; }
    public User? CashierUser { get; set; }
    public string? TerminalName { get; set; }
    /// <summary>Optional till attribution. When set, drawer entries and the shift-close variance also
    /// write a <see cref="FinancialLedgerEntry"/> against this account so the shift's cash shows up in
    /// that account's Cash Book. Left null, drawer/variance postings behave exactly as before.</summary>
    public Guid? FinancialAccountId { get; set; }
    public FinancialAccount? FinancialAccount { get; set; }

    public decimal OpeningCash { get; set; }
    public DateTime OpenedAtUtc { get; set; }
    public string? OpeningNotes { get; set; }

    public CashierShiftStatus Status { get; set; } = CashierShiftStatus.Open;

    public DateTime? ClosedAtUtc { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? ActualCountedCash { get; set; }
    public decimal? CashVariance { get; set; }
    public decimal? CustomerCashReceivedSnapshot { get; set; }
    public decimal? CashPaidOutSnapshot { get; set; }
    public string? ClosingNotes { get; set; }

    public Guid? ReconciledByUserId { get; set; }
    public User? ReconciledByUser { get; set; }
    public DateTime? ReconciledAtUtc { get; set; }
    public string? ReconciliationNotes { get; set; }

    public ICollection<CashierShiftDrawerEntry> DrawerEntries { get; set; } = [];
    public ICollection<CashierShiftPaymentSummary> PaymentSummaries { get; set; } = [];
}

/// <summary>
/// A manual cash-drawer movement during a shift that isn't a sale, refund, or customer payment
/// (e.g. adding float, removing cash for a bank deposit, a small petty cash payout).
/// </summary>
public class CashierShiftDrawerEntry : Entity
{
    public Guid CashierShiftId { get; set; }
    public CashierShift? CashierShift { get; set; }
    public CashierShiftDrawerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public required string Reason { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}

/// <summary>
/// A per-payment-method sales/refund total computed and frozen when a shift is closed, so the
/// breakdown shown on the closing report never changes even if later reports recompute differently.
/// </summary>
public class CashierShiftPaymentSummary : Entity
{
    public Guid CashierShiftId { get; set; }
    public CashierShift? CashierShift { get; set; }
    public SalePaymentMethod PaymentMethod { get; set; }
    public decimal SalesAmount { get; set; }
    public decimal RefundsAmount { get; set; }
}

public enum CashierShiftStatus
{
    Open = 1,
    Closed = 2,
    Reconciled = 3
}

public enum CashierShiftDrawerEntryType
{
    CashIn = 1,
    CashOut = 2
}
