using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A basic, accounting-focused budget figure for one chart-of-account/fiscal-year (optionally one
/// period within it, optionally one branch). Deliberately simple: a single target amount compared
/// against actual account activity for Budget vs Actual reporting, not a forecasting/planning engine.
/// </summary>
public class AccountBudget : Entity
{
    public int FiscalYear { get; set; }
    public int? PeriodNumber { get; set; }
    public Guid ChartOfAccountId { get; set; }
    public ChartOfAccount? ChartOfAccount { get; set; }
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public decimal BudgetAmount { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
