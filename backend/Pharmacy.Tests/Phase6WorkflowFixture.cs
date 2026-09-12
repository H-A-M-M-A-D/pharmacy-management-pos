using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Services;

namespace Pharmacy.Tests;

internal sealed class Phase6WorkflowFixture : IAsyncDisposable
{
    public required PharmacyDbContext Db { get; init; }
    public required DbContextOptions<PharmacyDbContext> Options { get; init; }
    public required User Actor { get; init; }
    public required Product Product { get; init; }
    public required ProductBatch Batch { get; init; }
    public required Supplier Supplier { get; init; }
    public required Customer Customer { get; init; }
    public string? TestSchema { get; init; }
    public Phase6Service Service => new(Db, TimeProvider.System);
    public static async Task<Phase6WorkflowFixture> Create(bool postgres = false)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var schema = postgres ? "phase6_" + suffix : null;
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PHARMACY_TEST_CONNECTION_STRING") ?? "");
        if (postgres) {
            await using var bootstrap = new NpgsqlConnection(connection.ConnectionString);
            await bootstrap.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", bootstrap);
            await command.ExecuteNonQueryAsync();
            connection.SearchPath = schema;
            connection.Pooling = false;
        }
        var options = postgres ? new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql(connection.ConnectionString).Options
            : new DbContextOptionsBuilder<PharmacyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new PharmacyDbContext(options);
        if (postgres) await db.Database.MigrateAsync();
        var branch = new Branch { Name = "Phase6 test " + suffix, Code = suffix[..12], NormalizedCode = suffix[..12].ToUpperInvariant() };
        var godown = new Godown { Branch = branch, Code = "MAIN", NormalizedCode = "MAIN", Name = "Main", IsDefault = true };
        var role = postgres ? await db.Roles.Include(x => x.RolePermissions).ThenInclude(x => x.Permission).SingleAsync(x => x.Name == RoleCatalog.Manager)
            : new Role { Name = RoleCatalog.Manager };
        if (!postgres) foreach (var code in new[] { PermissionCatalog.PricingManage, PermissionCatalog.PricingView, PermissionCatalog.PricingSuggest, PermissionCatalog.InventoryReorderView,
            PermissionCatalog.PurchaseOrdersCreate, PermissionCatalog.AlertsManage, PermissionCatalog.AlertsView, PermissionCatalog.AutomationManage, PermissionCatalog.AutomationRun,
            PermissionCatalog.AutomationView, PermissionCatalog.SalesCostView, PermissionCatalog.SalesCreate, PermissionCatalog.ReportsView, PermissionCatalog.ReportsProfitability, PermissionCatalog.SystemSettingsManage,
            PermissionCatalog.AccountsAgingReceivablesView, PermissionCatalog.AccountsAgingPayablesView, PermissionCatalog.ReportsInventory, PermissionCatalog.ReportsSales })
            role.RolePermissions.Add(new RolePermission { Permission = new Permission { Code = code, Description = code, Category = "test" } });
        var actor = new User { Username = suffix, NormalizedUsername = suffix.ToUpperInvariant(), FullName = "Phase6 test", PasswordHash = "test", Branch = branch, Role = role, IsActive = true };
        var supplier = new Supplier { Name = suffix, NormalizedName = suffix.ToUpperInvariant() };
        var customer = new Customer { CustomerCode = suffix[..15], Name = suffix, NormalizedName = suffix.ToUpperInvariant(), CreditLimit = 100000 };
        var product = new Product { SKU = suffix, NormalizedSku = suffix.ToUpperInvariant(), Name = "Phase6 tablet", Unit = "unit", PackSize = 1, PurchasePrice = 80, RetailPrice = 100, ReorderLevel = 10,
            Category = new ProductCategory { Name = suffix, NormalizedName = suffix.ToUpperInvariant() } };
        var batch = new ProductBatch { Branch = branch, Godown = godown, Product = product, Supplier = supplier, BatchNumber = suffix,
            PurchasePrice = 80, RetailPrice = 100, QuantityReceived = 1, QuantityAvailable = 1, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)), CreatedAt = DateTime.UtcNow.AddDays(-200) };
        db.AddRange(actor, customer, batch, new Pharmacy.Domain.Entities.Inventory { Branch = branch, Godown = godown, Product = product, ProductBatch = batch, QuantityInStock = 1, ReorderLevel = 10 },
            new StockMovement { Branch = branch, Godown = godown, Product = product, ProductBatch = batch, MovementType = StockMovementType.OpeningStock, Quantity = 1, CreatedAt = DateTime.UtcNow.AddDays(-200) });
        await db.SaveChangesAsync();
        return new() { Db = db, Options = options, Actor = actor, Product = product, Batch = batch, Supplier = supplier, Customer = customer, TestSchema = schema };
    }
    public async ValueTask DisposeAsync()
    {
        if (TestSchema != null)
        {
            await Db.DisposeAsync();
            // The generated schema is the fixture's complete isolated database namespace.
            if (!System.Text.RegularExpressions.Regex.IsMatch(TestSchema, "^phase6_[0-9a-f]{32}$")) throw new InvalidOperationException("Unexpected test schema.");
            await using var cleanup = new NpgsqlConnection(Environment.GetEnvironmentVariable("PHARMACY_TEST_CONNECTION_STRING"));
            await cleanup.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP SCHEMA \"{TestSchema}\" CASCADE", cleanup);
            await command.ExecuteNonQueryAsync();
            return;
        }
        await Db.DisposeAsync();
    }
}
