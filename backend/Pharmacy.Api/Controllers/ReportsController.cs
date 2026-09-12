using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
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
    public Task<object> Overview([FromQuery] ReportRequest request, CancellationToken ct) =>
        reports.DashboardAsync(UserId(), request.Query(), ct);

    // Permission required varies per report category/name (resolved dynamically inside
    // ReportingService.ExecuteAsync), so a single static [HasPermission] cannot express it here without
    // either over- or under-restricting - [Authorize] enforces the authenticated-user baseline for
    // defense-in-depth while the service performs the real, per-report permission check.
    [HttpGet("{category}/{name}"), Authorize]
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
    Guid? GodownId = null, Guid? SourceGodownId = null, Guid? DestinationGodownId = null, Guid? ProductId = null, string? Status = null,
    Pharmacy.Domain.Entities.SaleType? SaleType = null, Guid? CategoryId = null, Guid? ManufacturerId = null,
    Guid? CustomerId = null, Guid? SupplierId = null, Guid? UserId = null, Guid? PriceLevelId = null,
    int FastMovingQuantity = 100, int SlowMovingDays = 90, int DeadStockDays = 180, decimal AbcA = 80, decimal AbcB = 95, Guid? BatchId = null)
{
    public ReportQuery Query() => new(BranchId, FromUtc, ToUtc, Page, PageSize, Search, GodownId, SourceGodownId, DestinationGodownId,
        ProductId, Status, SaleType, CategoryId, ManufacturerId, CustomerId, SupplierId, UserId, PriceLevelId,
        FastMovingQuantity, SlowMovingDays, DeadStockDays, AbcA, AbcB) { BatchId = BatchId };
}

public static class CsvWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static string Write(object value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var items)) root = items;
        if (root.ValueKind == JsonValueKind.Object && root.EnumerateObject().Any(x => x.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array))
        {
            var summary = new StringBuilder().AppendLine("section,metric,value");
            void Append(JsonElement element, string section)
            {
                if (element.ValueKind == JsonValueKind.Object)
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                            Append(property.Value, string.IsNullOrEmpty(section) ? property.Name : section + "/" + property.Name);
                        else summary.AppendLine(string.Join(',', Escape(Heading(section)), Escape(Heading(property.Name)), Escape(Value(property.Value))));
                    }
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    var row = 0;
                    foreach (var item in element.EnumerateArray()) Append(item, section + "/" + (++row).ToString(CultureInfo.InvariantCulture));
                }
            }
            Append(root, "");
            return summary.ToString();
        }
        var rows = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToList() : [root];
        if (rows.Count == 0) return string.Empty;
        var properties = rows[0].EnumerateObject().Select(x => x.Name).ToList();
        var output = new StringBuilder().AppendLine(string.Join(',', properties.Select(p => Escape(Heading(p)))));
        foreach (var row in rows) output.AppendLine(string.Join(',', properties.Select(p => Escape(row.TryGetProperty(p, out var cell) ? Value(cell) : ""))));
        return output.ToString();
    }
    private static string Value(JsonElement value) => value.ValueKind switch { JsonValueKind.String => value.GetString() ?? "", JsonValueKind.Null => "", _ => value.ToString() };
    private static string Heading(string value) => Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
    private static string Escape(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
