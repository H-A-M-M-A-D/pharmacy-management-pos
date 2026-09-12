using Pharmacy.Application.DTOs.Reports;

namespace Pharmacy.Application.Services.Reports;

public static class ReportPeriods
{
    public static ReportQuery Previous(ReportQuery query, string? preset) => preset switch
    {
        "month" => query with { FromUtc = query.FromUtc.AddMonths(-1), ToUtc = query.ToUtc.AddMonths(-1) },
        "year" => query with { FromUtc = query.FromUtc.AddYears(-1), ToUtc = query.ToUtc.AddYears(-1) },
        _ => query with { FromUtc = query.FromUtc - (query.ToUtc - query.FromUtc), ToUtc = query.FromUtc }
    };

    public static ComparisonMetricDto Compare(string metric, decimal current, decimal previous) =>
        new(metric, current, previous, current - previous,
            previous == 0 ? null : decimal.Round((current - previous) / Math.Abs(previous) * 100, 2));
}
