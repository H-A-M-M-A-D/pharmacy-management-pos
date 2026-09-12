using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.DTOs.Phase6;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Persistence;

namespace Pharmacy.Infrastructure.Services;

public sealed partial class Phase6Service
{
    private async Task<int> RefreshExtendedAlerts(User actor, Phase6Thresholds thresholds, CancellationToken ct, AutomationTriggerType? trigger = null, Dictionary<string, decimal>? conditions = null, string suffix = "")
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var count = 0;
        conditions ??= new();
        var slow = await new ReportingRepository(db).SlowStockAsync(new ReportQuery(actor.BranchId, DateTime.UnixEpoch, now,
            SlowMovingDays: thresholds.SlowMovingDays, DeadStockDays: thresholds.DeadStockDays) { GodownUserId = actor.Role!.Name is RoleCatalog.Owner or RoleCatalog.Manager ? null : actor.Id }, 0, ct);
        foreach (var row in slow.Where(x => !trigger.HasValue))
            count += await UpsertAlert("SlowStock", $"{row.BranchId}:{row.GodownId}:{row.ProductId}", BusinessAlertCategory.Inventory,
                row.Status == "Dead stock" ? BusinessAlertSeverity.Warning : BusinessAlertSeverity.Info,
                row.Status, $"{row.Product}; qty {row.CurrentQuantity}; stock value {row.StockValue:0.00}; last sale {(row.LastSaleDate.HasValue ? row.LastSaleDate.Value.ToString("yyyy-MM-dd") : "never")}; {row.DaysSinceLastSale} days since last sale or receipt.", row.BranchId, row.GodownId, ct);
        var accounting = new AccountingRepository(db);
        var ar = await accounting.GetArAgingSummaryAsync(now, actor.BranchId, null, ct);
        var limits = await db.Customers.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.CreditLimit, ct);
        var window = DateOnly.FromDateTime(now).ToString("yyyy-MM-dd");
        var highOutstanding = conditions.GetValueOrDefault("highOutstandingAmount", 100000);
        var nearLimit = conditions.GetValueOrDefault("nearCreditLimitPercent", 90) / 100;
        foreach (var row in ar.Where(x => !trigger.HasValue || trigger == AutomationTriggerType.CustomerOverdue))
        {
            var overdue = row.Days1To30 + row.Days31To60 + row.Days61To90 + row.Over90;
            var limit = limits.GetValueOrDefault(row.CustomerId);
            async Task Add(string rule, string title, decimal amount, BusinessAlertSeverity severity) {
                count += await UpsertAlert(rule, $"{actor.BranchId}:{row.CustomerId}:{window}{suffix}", BusinessAlertCategory.Receivables, severity,
                    title, $"{row.CustomerName}; outstanding {row.Total:0.00}; {title.ToLowerInvariant()} {amount:0.00}.", actor.BranchId, null, ct);
            }
            if (overdue > 0) await Add("CustomerOverdue", "Customer overdue", overdue, BusinessAlertSeverity.Warning);
            if (limit > 0 && row.Total > limit) await Add("CreditExceeded", "Credit limit exceeded", limit, BusinessAlertSeverity.Critical);
            else if (limit > 0 && row.Total >= limit * nearLimit) await Add("CreditNear", "Near credit limit", limit, BusinessAlertSeverity.Warning);
            if (row.Total >= highOutstanding) await Add("CustomerOutstanding", "High customer outstanding", row.Total, BusinessAlertSeverity.Warning);
        }
        var ap = await accounting.GetApAgingSummaryAsync(now, actor.BranchId, null, ct);
        var dueDays = conditions.GetValueOrDefault("dueWithinDays", 7);
        foreach (var row in ap.Where(x => !trigger.HasValue || trigger == AutomationTriggerType.SupplierDue))
        {
            var overdue = row.Days1To30 + row.Days31To60 + row.Days61To90 + row.Over90;
            async Task Add(string rule, string title, decimal amount) {
                count += await UpsertAlert(rule, $"{actor.BranchId}:{row.SupplierId}:{window}{suffix}", BusinessAlertCategory.Payables, BusinessAlertSeverity.Warning,
                    title, $"{row.SupplierName}; outstanding {row.Total:0.00}; amount {amount:0.00}.", actor.BranchId, null, ct);
            }
            if (overdue > 0) await Add("SupplierOverdue", "Supplier overdue", overdue);
            var detail = await accounting.GetApAgingDetailAsync(row.SupplierId, now, actor.BranchId, ct);
            var due = detail?.Rows.Where(x => x.DueDate.HasValue && x.DueDate.Value <= DateOnly.FromDateTime(now.AddDays((double)dueDays)) && x.DueDate.Value >= DateOnly.FromDateTime(now)).Sum(x => x.Outstanding) ?? 0;
            if (due > 0) await Add("SupplierDue", "Supplier payment due", due);
            if (row.Total >= highOutstanding) await Add("SupplierOutstanding", "High supplier outstanding", row.Total);
        }
        return count;
    }
}
