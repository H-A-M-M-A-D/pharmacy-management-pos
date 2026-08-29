namespace Pharmacy.Application.Services.Auth;

public interface ITokenService
{
    string CreateToken(Guid userId, string username, string email, string fullName, string roleName, Guid roleId, Guid branchId, IReadOnlyCollection<string> permissionCodes);
}
