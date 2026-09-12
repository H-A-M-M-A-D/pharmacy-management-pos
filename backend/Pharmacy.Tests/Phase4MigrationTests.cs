using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Tests;

public sealed class Phase4MigrationTests
{
    [Fact]
    public void Cost_center_corrective_migration_target_model_includes_columns_indexes_and_restrictive_FKs()
    {
        using var db = new PharmacyDbContext(new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations["20260912040000_AddFinanceCostCenters"], db.Database.ProviderName!);
        foreach (var type in new[] { typeof(Expense), typeof(OtherIncome) })
        {
            var entity = migration.TargetModel.FindEntityType(type.FullName!)!;
            Assert.NotNull(entity.FindProperty("CostCenterId"));
            Assert.Contains(entity.GetIndexes(), x => x.Properties.Count == 1 && x.Properties[0].Name == "CostCenterId");
            var fk = Assert.Single(entity.GetForeignKeys(), x => x.Properties.Count == 1 && x.Properties[0].Name == "CostCenterId");
            Assert.Equal(typeof(CostCenter).FullName, fk.PrincipalEntityType.Name);
            Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        }
    }
}
