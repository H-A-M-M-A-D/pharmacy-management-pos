using System.Data;
using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Accounting;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Accounting.Budgets;

/// <summary>
/// Deliberately basic, accounting-focused budgeting: a single target amount per account/fiscal-year
/// (optionally one month within it, optionally one branch), compared against actual account activity.
/// Not a forecasting/planning engine. A period-level budget's "period" is the calendar month of the
/// fiscal year (month == <see cref="AccountBudget.PeriodNumber"/>); a year-level budget (no period)
/// compares against the full calendar year — this keeps date-range resolution simple and predictable
/// without depending on <see cref="AccountingPeriod"/> records existing for that year.
/// </summary>
public sealed class BudgetService(IBudgetRepository repository) : IBudgetService
{
    public async Task<IReadOnlyList<AccountBudgetDto>> ListBudgetsAsync(Guid actorId, int fiscalYear, int? periodNumber, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsBudgetsView, cancellationToken);
        var budgets = await repository.ListBudgetsAsync(fiscalYear, periodNumber, Scope(actor, branchId), cancellationToken);
        return budgets.Select(Map).ToList();
    }

    public async Task<AccountBudgetDto> SetBudgetAsync(Guid actorId, AccountBudgetRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsBudgetsManage, cancellationToken);
        if (request.FiscalYear < 2000 || request.FiscalYear > 2200) throw new RequestValidationException("Fiscal year is out of range.");
        if (request.PeriodNumber is < 1 or > 12) throw new RequestValidationException("Period number must be between 1 and 12.");
        if (decimal.Round(request.BudgetAmount, 2) != request.BudgetAmount) throw new RequestValidationException("Budget amount supports at most two decimal places.");
        if (await repository.GetAccountAsync(request.ChartOfAccountId, cancellationToken) is not { IsActive: true } account)
            throw new RequestValidationException("Account was not found or is inactive.");
        if (request.BranchId.HasValue)
        {
            EnsureBranchAccess(actor, request.BranchId.Value);
            if (await repository.GetBranchAsync(request.BranchId.Value, cancellationToken) is not { IsActive: true })
                throw new RequestValidationException("Branch is invalid or inactive.");
        }

        AccountBudget? budget = null;
        await repository.ExecuteInTransactionAsync(async ct =>
        {
            var existing = await repository.FindExistingAsync(request.FiscalYear, request.PeriodNumber, request.ChartOfAccountId, request.BranchId, ct);
            if (existing is null)
            {
                budget = new AccountBudget
                {
                    FiscalYear = request.FiscalYear, PeriodNumber = request.PeriodNumber, ChartOfAccountId = request.ChartOfAccountId,
                    BranchId = request.BranchId, BudgetAmount = request.BudgetAmount, Notes = Clean(request.Notes), CreatedByUserId = actorId
                };
                await repository.AddBudgetAsync(budget, ct);
            }
            else
            {
                existing.BudgetAmount = request.BudgetAmount;
                existing.Notes = Clean(request.Notes);
                existing.UpdatedAt = DateTime.UtcNow;
                budget = existing;
            }
            await Audit(actorId, "AccountBudgetSet", budget.Id, new { request.FiscalYear, request.PeriodNumber, account.Code, request.BranchId, request.BudgetAmount }, ct);
            await repository.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellationToken);
        return Map(budget!, account);
    }

    public async Task<BudgetVsActualDto> GetBudgetVsActualAsync(Guid actorId, BudgetVsActualQuery query, CancellationToken cancellationToken = default)
    {
        var actor = await Require(actorId, PermissionCatalog.AccountsBudgetsView, cancellationToken);
        var scope = Scope(actor, query.BranchId);
        var budgets = await repository.ListBudgetsAsync(query.FiscalYear, query.PeriodNumber, scope, cancellationToken);
        var (fromUtc, toUtc) = DateRangeFor(query.FiscalYear, query.PeriodNumber);
        var actuals = await repository.GetActualActivityAsync(fromUtc, toUtc, scope, cancellationToken);
        var actualByAccount = actuals.ToDictionary(x => x.ChartOfAccountId, x => NetAmount(x));

        var rows = budgets.Select(b =>
        {
            var actual = actualByAccount.GetValueOrDefault(b.ChartOfAccountId);
            var variance = decimal.Round(actual - b.BudgetAmount, 2);
            decimal? variancePercent = b.BudgetAmount != 0 ? decimal.Round(variance / Math.Abs(b.BudgetAmount) * 100, 2) : null;
            return new BudgetVsActualRowDto(b.ChartOfAccountId, b.ChartOfAccount!.Code, b.ChartOfAccount.Name, b.ChartOfAccount.AccountType, b.BudgetAmount, actual, variance, variancePercent);
        }).OrderBy(x => x.AccountCode).ToList();
        return new BudgetVsActualDto(query.FiscalYear, query.PeriodNumber, query.BranchId, rows, rows.Sum(x => x.BudgetAmount), rows.Sum(x => x.ActualAmount), rows.Sum(x => x.VarianceAmount));
    }

    private static decimal NetAmount(TrialBalanceRowDto row) =>
        row.AccountType is AccountType.Asset or AccountType.CostOfSales or AccountType.Expense ? row.Debit - row.Credit : row.Credit - row.Debit;

    private static (DateTime FromUtc, DateTime ToUtc) DateRangeFor(int fiscalYear, int? periodNumber)
    {
        if (periodNumber.HasValue)
        {
            var start = new DateTime(fiscalYear, periodNumber.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, start.AddMonths(1).AddTicks(-1));
        }
        return (new DateTime(fiscalYear, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(fiscalYear, 12, 31, 23, 59, 59, DateTimeKind.Utc));
    }

    private static AccountBudgetDto Map(AccountBudget b) => new(b.Id, b.FiscalYear, b.PeriodNumber, b.ChartOfAccountId, b.ChartOfAccount?.Code ?? string.Empty,
        b.ChartOfAccount?.Name ?? string.Empty, b.BranchId, b.Branch?.Name, b.BudgetAmount, b.Notes);
    private static AccountBudgetDto Map(AccountBudget b, ChartOfAccount account) => new(b.Id, b.FiscalYear, b.PeriodNumber, b.ChartOfAccountId, account.Code,
        account.Name, b.BranchId, b.Branch?.Name, b.BudgetAmount, b.Notes);

    private async Task<User> Require(Guid actorId, string permission, CancellationToken ct)
    {
        var actor = await repository.GetActorAsync(actorId, ct);
        if (actor is null || !actor.IsActive || actor.Role?.RolePermissions.Any(x => x.Permission?.Code == permission) != true)
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        return actor;
    }

    private static Guid? Scope(User actor, Guid? requested) { if (CanSelectBranch(actor)) return requested; if (requested.HasValue && requested != actor.BranchId) throw new ForbiddenOperationException("The current user cannot access this branch."); return actor.BranchId; }
    private static void EnsureBranchAccess(User actor, Guid branchId) { if (!CanSelectBranch(actor) && actor.BranchId != branchId) throw new ForbiddenOperationException("The current user cannot access this branch."); }
    private static bool CanSelectBranch(User actor) => actor.Role?.Name is RoleCatalog.Owner or RoleCatalog.Manager;
    private async Task Audit(Guid userId, string action, Guid entityId, object values, CancellationToken ct) =>
        await repository.AddAuditAsync(new AuditLog { UserId = userId, Action = action, EntityType = "AccountBudget", EntityId = entityId, NewValues = JsonSerializer.Serialize(values) }, ct);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
