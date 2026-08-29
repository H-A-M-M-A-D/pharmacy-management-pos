using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pharmacy.Infrastructure.Data;

/// <summary>
/// Design-time factory for creating PharmacyDbContext instances.
/// This is used by EF Core migrations during development.
/// </summary>
public class PharmacyDbContextFactory : IDesignTimeDbContextFactory<PharmacyDbContext>
{
    public PharmacyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PharmacyDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection must be provided for EF Core tooling.");

        optionsBuilder.UseNpgsql(connectionString);

        return new PharmacyDbContext(optionsBuilder.Options);
    }
}
