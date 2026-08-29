using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Auth;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Users;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class IdentityCatalogController(IUserManagementService users) : ControllerBase
{
    [HttpGet("roles")]
    [HasPermission(PermissionCatalog.RolesView)]
    public Task<IReadOnlyList<RoleDto>> Roles(CancellationToken cancellationToken) =>
        users.ListRolesAsync(cancellationToken);

    [HttpGet("permissions")]
    [HasPermission(PermissionCatalog.PermissionsView)]
    public Task<IReadOnlyList<PermissionDto>> Permissions(CancellationToken cancellationToken) =>
        users.ListPermissionsAsync(cancellationToken);
}
