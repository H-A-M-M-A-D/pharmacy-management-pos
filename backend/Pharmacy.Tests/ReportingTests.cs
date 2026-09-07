using Pharmacy.Application.Services.Reports;
using Pharmacy.Api.Controllers;
using Pharmacy.Application.DTOs.Reports;

namespace Pharmacy.Tests;

public sealed class ReportingTests
{
    [Fact] public void Sales_returns_reduce_posted_sales() => Assert.Equal(700m, ReportCalculations.NetAfterReturns(900m, 200m));
    [Fact] public void Credit_sale_is_revenue_but_not_cash() { Assert.Equal(5000m, ReportCalculations.NetAfterReturns(5000m, 0)); Assert.Equal(2000m, ReportCalculations.ClosingCash(0, [2000m])); Assert.Equal(3000m, ReportCalculations.LedgerBalance([3000m])); }
    [Fact] public void Mixed_batch_profit_uses_allocation_costs() { var cost = 3 * 80m + 2 * 90m; Assert.Equal(100m, ReportCalculations.GrossProfit(3 * 100m + 2 * 110m, cost)); }
    [Fact] public void Return_reverses_original_revenue_and_cost() => Assert.Equal(60m, ReportCalculations.GrossProfit(3 * 100m, 3 * 80m));
    [Fact] public void Bonus_quantities_are_physical_but_not_payable() { Assert.Equal(110, ReportCalculations.NetPhysicalPurchase(100, 10, 0, 0)); Assert.Equal(1000m, 100 * 10m); Assert.Equal(100, ReportCalculations.NetPhysicalPurchase(100, 10, 0, 10)); }
    [Fact] public void Inventory_valuation_uses_available_batch_cost() => Assert.Equal(900m, ReportCalculations.InventoryValue([(10, 50m), (5, 80m)]));
    [Fact] public void Customer_and_supplier_outstanding_are_ledger_sums() { Assert.Equal(3000m, ReportCalculations.LedgerBalance([2000m, 5000m, -3000m, -1000m])); Assert.Equal(9000m, ReportCalculations.LedgerBalance([10000m, 5000m, -4000m, -2000m])); }
    [Fact] public void Daily_cash_matches_ledger_movements() => Assert.Equal(11200m, ReportCalculations.ClosingCash(10000m, [3000m, 1000m, -500m, -2000m, -300m]));
    [Fact] public void Internal_transfer_is_neutral_to_consolidated_activity() => Assert.Equal(0m, ReportCalculations.ExternalActivity([("TransferOut", -3000m), ("TransferIn", 3000m)]));
    [Fact] public void Karachi_day_uses_utc_plus_five_boundary() { var range = ReportCalculations.KarachiDay(new DateOnly(2026, 9, 8)); Assert.Equal(new DateTime(2026, 9, 7, 19, 0, 0, DateTimeKind.Utc), range.FromUtc); Assert.Equal(TimeSpan.FromDays(1), range.ToUtc - range.FromUtc); }
    [Fact] public void Csv_export_escapes_quotes_commas_and_newlines() { var csv = CsvWriter.Write(new[] { new { Name = "A, \"quoted\"\nvalue", Amount = 12.5m } }); Assert.Contains("name,amount", csv); Assert.Contains("\"A, \"\"quoted\"\"\nvalue\"", csv); }
    [Fact] public void Csv_export_unwraps_paged_report_items() { var csv = CsvWriter.Write(new PagedReport<object>([new { Code = "P1", Total = 10m }], 1, 1, 50)); Assert.StartsWith("code,total", csv); Assert.DoesNotContain("pageSize", csv); }
}
