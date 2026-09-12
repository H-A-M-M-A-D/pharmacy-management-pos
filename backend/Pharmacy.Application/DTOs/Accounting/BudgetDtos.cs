using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Accounting;

public sealed record AccountBudgetRequest(int FiscalYear, int? PeriodNumber, Guid ChartOfAccountId, Guid? BranchId, decimal BudgetAmount, string? Notes);
public sealed record AccountBudgetDto(
    Guid Id, int FiscalYear, int? PeriodNumber, Guid ChartOfAccountId, string AccountCode, string AccountName,
    Guid? BranchId, string? BranchName, decimal BudgetAmount, string? Notes);

public sealed record BudgetVsActualQuery(int FiscalYear, int? PeriodNumber, Guid? BranchId);
public sealed record BudgetVsActualRowDto(
    Guid ChartOfAccountId, string AccountCode, string AccountName, AccountType AccountType,
    decimal BudgetAmount, decimal ActualAmount, decimal VarianceAmount, decimal? VariancePercent);
public sealed record BudgetVsActualDto(
    int FiscalYear, int? PeriodNumber, Guid? BranchId,
    IReadOnlyList<BudgetVsActualRowDto> Rows, decimal TotalBudget, decimal TotalActual, decimal TotalVariance);
