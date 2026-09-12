using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting;

/// <summary>Shared statement interpretation for accounting reports and MIS. Posting is unaffected.</summary>
public static class FinancialStatements
{
    public static IReadOnlyList<FinancialStatementRowDto> Rows(IEnumerable<TrialBalanceRowDto> rows) => rows
        .Select(x => new FinancialStatementRowDto(x.ChartOfAccountId, x.AccountCode, x.AccountName,
            x.AccountType is AccountType.Asset or AccountType.CostOfSales or AccountType.Expense ? x.Debit - x.Credit : x.Credit - x.Debit))
        .Where(x => x.Amount != 0).OrderBy(x => x.AccountCode).ToList();

    public static ProfitAndLossDto ProfitAndLoss(DateTime fromUtc, DateTime toUtc, IReadOnlyList<TrialBalanceRowDto> rows,
        IReadOnlyDictionary<AccountMappingKey, Guid> mappings)
    {
        var otherIncomeId = mappings.GetValueOrDefault(AccountMappingKey.OtherIncomeDefault);
        var badDebtId = mappings.GetValueOrDefault(AccountMappingKey.BadDebtExpense);
        var revenue = Rows(rows.Where(x => x.AccountType == AccountType.Income && x.ChartOfAccountId != otherIncomeId));
        var otherIncome = Rows(rows.Where(x => x.AccountType == AccountType.Income && x.ChartOfAccountId == otherIncomeId));
        var cost = Rows(rows.Where(x => x.AccountType == AccountType.CostOfSales));
        var expenses = Rows(rows.Where(x => x.AccountType == AccountType.Expense && x.ChartOfAccountId != badDebtId));
        var otherExpenses = Rows(rows.Where(x => x.AccountType == AccountType.Expense && x.ChartOfAccountId == badDebtId));
        var netRevenue = revenue.Sum(x => x.Amount); var totalCost = cost.Sum(x => x.Amount);
        var grossProfit = netRevenue - totalCost; var totalExpenses = expenses.Sum(x => x.Amount);
        var totalOtherIncome = otherIncome.Sum(x => x.Amount); var totalOtherExpenses = otherExpenses.Sum(x => x.Amount);
        return new(fromUtc, toUtc, revenue, netRevenue, cost, totalCost, grossProfit, expenses, totalExpenses,
            grossProfit - totalExpenses + totalOtherIncome - totalOtherExpenses, otherIncome, totalOtherIncome, otherExpenses, totalOtherExpenses);
    }
}
