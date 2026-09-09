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
    public ICollection<ChartOfAccount> ChildAccounts { get; set; } = [];
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
    CashOverShort = 18
}
