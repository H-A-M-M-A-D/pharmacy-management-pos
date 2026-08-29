using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Pharmacy.Application.DTOs.Auth;
using Pharmacy.Application.Services.Auth;

namespace Pharmacy.Infrastructure.Services.Auth;

public sealed class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(IConfiguration configuration, TimeProvider timeProvider)
    {
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public AccessTokenResult CreateToken(CurrentUserDto user, int tokenVersion)
    {
        var secretKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured.");
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32 || secretKey.StartsWith('<') ||
            secretKey.Contains("change-this", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("JWT signing key must be supplied through secure configuration.");
        }

        var issuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured.");
        var audience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not configured.");
        if (!int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var expirationMinutes) || expirationMinutes <= 0)
        {
            throw new InvalidOperationException("JWT ExpirationMinutes must be a positive integer.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAtUtc = now.AddMinutes(expirationMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("FullName", user.FullName),
            new("BranchId", user.Branch.Id.ToString()),
            new("token_version", tokenVersion.ToString()),
            new("must_change_password", user.MustChangePassword.ToString().ToLowerInvariant())
        };
        if (user.Email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Name));
            claims.Add(new Claim("RoleId", role.Id.ToString()));
        }

        claims.AddRange(user.Permissions.Distinct(StringComparer.Ordinal).Select(code => new Claim("permission", code)));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, now, expiresAtUtc, credentials);
        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
