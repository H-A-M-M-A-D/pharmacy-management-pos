using Pharmacy.Application.DTOs.Reports;

namespace Pharmacy.Application.Services.Reports;

public interface IReportingService
{
    Task<object> ExecuteAsync(Guid actorId, string report, ReportQuery query, string? option, CancellationToken ct);
    Task<DashboardDto> DashboardAsync(Guid actorId, ReportQuery query, CancellationToken ct);
}
