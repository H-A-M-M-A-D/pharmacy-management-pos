using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Pharmacy.Api.Authorization;
using Pharmacy.Api.Middleware;
using Pharmacy.Application.Security;

namespace Pharmacy.Tests;

public sealed class ApiAuthorizationTests
{
    [Fact]
    public async Task Permission_policy_denies_missing_claim_and_allows_matching_claim()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));
        var policy = await provider.GetPolicyAsync(
            PermissionAuthorization.PolicyPrefix + PermissionCatalog.UsersView);
        Assert.NotNull(policy);
        var requirement = Assert.Single(policy.Requirements.OfType<ClaimsAuthorizationRequirement>());

        var missing = new AuthorizationHandlerContext(
            policy.Requirements,
            new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test")),
            null);
        await HandleRequirementsAsync(policy, missing);
        Assert.False(missing.HasSucceeded);

        var allowed = new AuthorizationHandlerContext(
            policy.Requirements,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(PermissionAuthorization.ClaimType, PermissionCatalog.UsersView)], "test")),
            null);
        await HandleRequirementsAsync(policy, allowed);
        Assert.True(allowed.HasSucceeded);
    }

    [Fact]
    public async Task Forced_password_change_blocks_other_backend_routes()
    {
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = AuthenticatedContext("POST", "/api/users");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Theory]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("POST", "/api/auth/change-password")]
    public async Task Forced_password_change_allows_only_profile_and_password_routes(string method, string path)
    {
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(AuthenticatedContext(method, path));

        Assert.True(nextCalled);
    }

    private static DefaultHttpContext AuthenticatedContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("must_change_password", "true")], "test"));
        return context;
    }

    private static async Task HandleRequirementsAsync(
        AuthorizationPolicy policy,
        AuthorizationHandlerContext context)
    {
        foreach (var handler in policy.Requirements.OfType<IAuthorizationHandler>())
        {
            await handler.HandleAsync(context);
        }
    }
}
