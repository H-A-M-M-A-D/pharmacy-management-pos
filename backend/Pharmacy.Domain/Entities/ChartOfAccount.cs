using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A node in the company-wide chart of accounts. Global (not branch-scoped) — branch dimension
/// lives on <see cref="JournalEntryLine"/> so financial statements can still be filtered by branch.
/// </summary>
public class ChartOfAccount : Entity
{
    public required string Code { get; set; }
    public required string NormalizedCode { get; set; }
    public required string Name { get; set; }
    public Guid? ParentAccountId { get; set; }
    public ChartOfAccount? ParentAccount { get; set; }
    public AccountType AccountType { get; set; }
    public NormalBalance NormalBalance { get; set; }
    public bool IsPostingAccount { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    /// <summary>Optional cash-flow-statement bucket for this account. Null means "auto-classify":
    /// Asset/Liability movements default to Operating, Equity movements default to Financing. Tag an
    /// account explicitly (e.g. an Equipment asset as Investing, a Loan Payable/Owner's Capital as
    /// Financing) only when its balance changes shouldn't be treated as ordinary working capital —
    /// this is the "minimal classification metadata" the indirect-method Cash Flow Statement reads
    /// instead of ever hard-coding an account id.</summary>
    public CashFlowClassification? CashFlowClassification { get; set; }
    public ICollection<ChartOfAccount> ChildAccounts { get; set; } = [];
}

public enum CashFlowClassification
{
    Operating = 1,
    Investing = 2,
    Financing = 3
}

/// <summary>
/// Maps a stable semantic role (e.g. "the accounts receivable control account") to the actual
/// <see cref="ChartOfAccount"/> a business has configured for it, so posting logic never hard-codes
/// a raw account id or code.
/// </summary>
public class AccountMapping : Entity
{
    public AccountMappingKey MappingKey { get; set; }
    public Guid ChartOfAccountId { get; set; }
    public ChartOfAccount? ChartOfAccount { get; set; }
}

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Income = 4,
    CostOfSales = 5,
    Expense = 6
}

public enum NormalBalance
{
    Debit = 1,
    Credit = 2
}

public enum AccountMappingKey
{
    Cash = 1,
    Bank = 2,
    AccountsReceivable = 3,
    AccountsPayable = 4,
    Inventory = 5,
    SalesRevenue = 6,
    SalesReturnsContra = 7,
    CostOfGoodsSold = 8,
    InventoryLossExpense = 9,
    GeneralExpenseDefault = 10,
    OtherIncomeDefault = 11,
    RetainedEarnings = 12,
    InventoryAdjustmentGain = 13,
    AccountsReceivableAdjustmentSuspense = 14,
    AccountsPayableAdjustmentSuspense = 15,
    CashBankAdjustmentSuspense = 16,
    DrawerClearing = 17,
    CashOverShort = 18,
    BadDebtExpense = 19,
    PayablesWriteOffIncome = 20,
    CustomerAdvances = 21,
    SupplierAdvances = 22
}
