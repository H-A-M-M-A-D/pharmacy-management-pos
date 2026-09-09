using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pharmacy.Api.Authorization;
using Pharmacy.Api.Middleware;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Accounting;
using Pharmacy.Application.Services.Auth;
using Pharmacy.Application.Services.CashierShifts;
using Pharmacy.Application.Services.Catalog;
using Pharmacy.Application.Services.Customers;
using Pharmacy.Application.Services.Finance;
using Pharmacy.Application.Services.Inventory;
using Pharmacy.Application.Services.Purchasing;
using Pharmacy.Application.Services.Reports;
using Pharmacy.Application.Services.Sales;
using Pharmacy.Application.Services.Suppliers;
using Pharmacy.Application.Services.Users;
using Pharmacy.Application.Services.Administration;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<PharmacyDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("Pharmacy.Infrastructure")));

var securityOptions = builder.Configuration
    .GetSection(AuthenticationSecurityOptions.SectionName)
    .Get<AuthenticationSecurityOptions>() ?? new AuthenticationSecurityOptions();
if (securityOptions.MaximumFailedAttempts < 1 || securityOptions.LockoutMinutes < 1)
{
    throw new InvalidOperationException("Authentication security settings must be positive.");
}

builder.Services.AddSingleton(securityOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IUserAccountRepository, UserAccountRepository>();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IAdministrationRepository, AdministrationRepository>();
builder.Services.AddScoped<IAdministrationService, AdministrationService>();
builder.Services.AddScoped<IProductMasterRepository, ProductMasterRepository>();
builder.Services.AddScoped<IProductMasterService, ProductMasterService>();
builder.Services.AddScoped<IFefoAllocationService, FefoAllocationService>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IFinanceRepository, FinanceRepository>();
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<IReportingRepository, ReportingRepository>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IPurchasingRepository, PurchasingRepository>();
builder.Services.AddScoped<IPurchasingService, PurchasingService>();
builder.Services.AddScoped<ISalesRepository, SalesRepository>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<ISalesReturnRepository, SalesReturnRepository>();
builder.Services.AddScoped<ISalesReturnService, SalesReturnService>();
builder.Services.AddScoped<ICashierShiftRepository, CashierShiftRepository>();
builder.Services.AddScoped<ICashierShiftService, CashierShiftService>();
builder.Services.AddScoped<IAccountingRepository, AccountingRepository>();
builder.Services.AddScoped<IAccountingService, AccountingService>();
builder.Services.AddScoped<IJournalPostingService, JournalPostingService>();

var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not configured. Set Jwt__Key or a user secret.");
if (jwtKey.Length < 32 || jwtKey.Contains("change-this", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("JWT Key must be a non-placeholder value of at least 32 characters.");
}

var jwtIssuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured.");
var jwtAudience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience not configured.");
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var idValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var versionValue = context.Principal?.FindFirstValue("token_version");
                if (!Guid.TryParse(idValue, out var userId) || !int.TryParse(versionValue, out var tokenVersion))
                {
                    context.Fail("Invalid session.");
                    return;
                }

                var database = context.HttpContext.RequestServices.GetRequiredService<PharmacyDbContext>();
                var valid = await database.Users.AsNoTracking().AnyAsync(
                    user => user.Id == userId && user.IsActive && user.TokenVersion == tokenVersion,
                    context.HttpContext.RequestAborted);
                if (!valid)
                {
                    context.Fail("Session is no longer valid.");
                }
            }
        };
    });

builder.Services.AddPermissionAuthorization();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("PharmacyClient", policy =>
{
    if (builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    else if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
}));

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseCors("PharmacyClient");
app.UseAuthentication();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => new { status = "healthy" })
    .WithName("HealthCheck");
app.MapGet("/api/health/live", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/api/health/ready", async (PharmacyDbContext database, CancellationToken ct) =>
    await database.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "healthy" })
        : Results.Json(new { status = "unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable));

app.Run();

public partial class Program;

