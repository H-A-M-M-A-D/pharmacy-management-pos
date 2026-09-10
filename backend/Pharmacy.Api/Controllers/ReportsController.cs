using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Reports;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController(IReportingService reports) : ControllerBase
{
    [HttpGet("overview"), HasPermission(PermissionCatalog.ReportsView)]
    public Task<DashboardDto> Overview([FromQuery] ReportRequest request, CancellationToken ct) =>
        reports.DashboardAsync(UserId(), request.Query(), ct);

    [HttpGet("{category}/{name}")]
    public Task<object> Report(string category, string name, [FromQuery] ReportRequest request, CancellationToken ct) =>
        reports.ExecuteAsync(UserId(), $"{category}/{name}", request.Query(), request.Option, ct);

    [HttpGet("{category}/{name}/export.csv"), HasPermission(PermissionCatalog.ReportsExport)]
    public async Task<FileContentResult> Export(string category, string name, [FromQuery] ReportRequest request, CancellationToken ct)
    {
        var query = request.Query() with { Page = 1, PageSize = 200 };
        var result = await reports.ExecuteAsync(UserId(), $"{category}/{name}", query, request.Option, ct);
        var rows = new List<JsonElement>();
        var first = JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("items", out var items))
        {
            var total = first.GetProperty("total").GetInt32();
            rows.AddRange(items.EnumerateArray().Select(x => x.Clone()));
            for (var page = 2; rows.Count < total && rows.Count < 100_000; page++)
            {
                var next = await reports.ExecuteAsync(UserId(), $"{category}/{name}", query with { Page = page }, request.Option, ct);
                var element = JsonSerializer.SerializeToElement(next, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var nextItems = element.GetProperty("items").EnumerateArray().Select(x => x.Clone()).ToList();
                if (nextItems.Count == 0) break;
                rows.AddRange(nextItems);
            }
            result = rows;
        }
        var csv = CsvWriter.Write(result);
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv; charset=utf-8", $"{category}-{name}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public sealed record ReportRequest(Guid? BranchId, DateTime FromUtc, DateTime ToUtc, int Page = 1, int PageSize = 50, string? Search = null, string? Option = null,
    Guid? GodownId = null, Guid? SourceGodownId = null, Guid? DestinationGodownId = null, Guid? ProductId = null, string? Status = null)
{
    public ReportQuery Query() => new(BranchId, FromUtc, ToUtc, Page, PageSize, Search, GodownId, SourceGodownId, DestinationGodownId, ProductId, Status);
}

public static class CsvWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static string Write(object value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var items)) root = items;
        var rows = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToList() : [root];
        if (rows.Count == 0) return string.Empty;
        var properties = rows[0].EnumerateObject().Select(x => x.Name).ToList();
        var output = new StringBuilder().AppendLine(string.Join(',', properties.Select(Escape)));
        foreach (var row in rows) output.AppendLine(string.Join(',', properties.Select(p => Escape(Value(row.GetProperty(p))))));
        return output.ToString();
    }
    private static string Value(JsonElement value) => value.ValueKind switch { JsonValueKind.String => value.GetString() ?? "", JsonValueKind.Null => "", _ => value.ToString() };
    private static string Escape(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
