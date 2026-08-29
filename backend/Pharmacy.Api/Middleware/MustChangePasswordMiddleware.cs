namespace Pharmacy.Api.Middleware;

public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var mustChangePassword = context.User.Identity?.IsAuthenticated == true &&
            string.Equals(context.User.FindFirst("must_change_password")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        var isAllowed =
            (context.Request.Method == HttpMethods.Get && context.Request.Path == "/api/auth/me") ||
            (context.Request.Method == HttpMethods.Post && context.Request.Path == "/api/auth/change-password");
        if (mustChangePassword && !isAllowed)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Password change required.",
                status = StatusCodes.Status403Forbidden
            });
            return;
        }

        await next(context);
    }
}
