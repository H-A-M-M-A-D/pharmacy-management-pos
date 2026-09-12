using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Reports;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Reports;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Persistence;
using System.Text.Json;

namespace Pharmacy.Tests;

public sealed class ManagementReportingTests
{
    [Theory]
    [InlineData(12, 0, null)]
    [InlineData(0, 0, null)]
    [InlineData(150, 100, 50)]
    [InlineData(-50, -100, 50)]
    public void Comparison_handles_zero_and_negative_baselines(decimal current, decimal previous, int? percent)
    {
        var result = ReportPeriods.Compare("Sales", current, previous);
        Assert.Equal(current - previous, result.AbsoluteChange);
        Assert.Equal(percent.HasValue ? (decimal?)percent.Value : null, result.PercentageChange);
    }

    [Fact]
    public void Previous_calendar_month_and_year_preserve_calendar_boundaries()
    {
        var q = new ReportQuery(null, new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), new(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc), ReportPeriods.Previous(q, "month").FromUtc);
        Assert.Equal(TimeSpan.FromDays(29), ReportPeriods.Previous(q, "month").ToUtc - ReportPeriods.Previous(q, "month").FromUtc);
        Assert.Equal(q.FromUtc.AddYears(-1), ReportPeriods.Previous(q, "year").FromUtc);
        Assert.Equal(q.FromUtc, ReportPeriods.Previous(q, null).ToUtc);
    }

    [Fact]
    public async Task Sales_report_omits_cost_and_profit_fields_instead_of_returning_zeroes()
    {
        await using var db = new PharmacyDbContext(new DbContextOptionsBuilder<PharmacyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var branch = new Branch { Code = "B", NormalizedCode = "B", Name = "Branch" };
        var role = new Role { Name = RoleCatalog.Manager };
        role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = PermissionCatalog.ReportsSales, Description = "Sales", Category = "Reports" } });
        var user = new User { Username = "reader", NormalizedUsername = "READER", FullName = "Reader", PasswordHash = "hash", BranchId = branch.Id, Role = role };
        db.AddRange(branch, user);
        await db.SaveChangesAsync();
        var service = new ReportingService(new ReportingRepository(db), TimeProvider.System);
        var q = new ReportQuery(branch.Id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        var json = JsonSerializer.Serialize(await service.ExecuteAsync(user.Id, "sales/products", q, null, default));
        Assert.DoesNotContain("costOfGoodsSold", json);
        Assert.DoesNotContain("grossProfit", json);
        Assert.DoesNotContain("grossMarginPercent", json);
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => service.ExecuteAsync(user.Id, "profitability/products", q, null, default));
        await Assert.ThrowsAsync<ForbiddenOperationException>(() => service.ExecuteAsync(user.Id, "management/overview", q, null, default));
    }
}
