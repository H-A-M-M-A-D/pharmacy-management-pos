namespace Pharmacy.Application.Services.Reports;

public static class ReportCalculations
{
    public static decimal NetAfterReturns(decimal postedNetSales, decimal returnValue) => postedNetSales - returnValue;
    public static decimal GrossProfit(decimal netRevenue, decimal historicalCost) => netRevenue - historicalCost;
    public static decimal MarginPercent(decimal revenue, decimal profit) => revenue == 0 ? 0 : decimal.Round(profit / revenue * 100, 2);
    public static decimal InventoryValue(IEnumerable<(int Quantity, decimal UnitCost)> batches) => batches.Sum(x => x.Quantity * x.UnitCost);
    public static decimal LedgerBalance(IEnumerable<decimal> entries) => entries.Sum();
    public static int NetPhysicalPurchase(int paidReceived, int bonusReceived, int paidReturned, int bonusReturned) => paidReceived + bonusReceived - paidReturned - bonusReturned;
    public static decimal ClosingCash(decimal opening, IEnumerable<decimal> movements) => opening + movements.Sum();
    public static decimal ExternalActivity(IEnumerable<(string Type, decimal Amount)> movements) => movements.Where(x => x.Type is not "TransferIn" and not "TransferOut").Sum(x => x.Amount);
    public static (DateTime FromUtc, DateTime ToUtc) KarachiDay(DateOnly date)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time");
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var from = TimeZoneInfo.ConvertTimeToUtc(local, zone);
        return (from, from.AddDays(1));
    }
}
