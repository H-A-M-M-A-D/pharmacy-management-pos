using Pharmacy.Application.DTOs.Auth;

namespace Pharmacy.Application.Services.Auth;

public interface ITokenService
{
    AccessTokenResult CreateToken(CurrentUserDto user, int tokenVersion);
}
