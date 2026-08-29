using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Pharmacy.Application.Services.Auth;

namespace Pharmacy.Infrastructure.Services.Auth;

public sealed class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreateToken(Guid userId, string username, string email, string fullName, string roleName, Guid roleId, Guid branchId, IReadOnlyCollection<string> permissionCodes)
    {
        var secretKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured.");
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32 || secretKey.StartsWith("<") || secretKey.Contains("change-this", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("JWT signing key must be set via configuration or environment variables in production.");
        }

        var issuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured.");
        var audience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not configured.");
        if (!int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var expirationMinutes) || expirationMinutes <= 0)
        {
            throw new InvalidOperationException("JWT ExpirationMinutes must be a positive integer.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Email, email),
            new("FullName", fullName),
            new(ClaimTypes.Role, roleName),
            new("RoleId", roleId.ToString()),
            new("BranchId", branchId.ToString())
        };

        foreach (var permission in permissionCodes.Distinct())
        {
            claims.Add(new Claim("permission", permission));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
