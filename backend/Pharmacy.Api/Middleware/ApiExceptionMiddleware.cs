using Microsoft.AspNetCore.Mvc;
using Pharmacy.Application.Common;

namespace Pharmacy.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApplicationServiceException exception)
        {
            var statusCode = exception switch
            {
                RequestValidationException => StatusCodes.Status400BadRequest,
                ResourceNotFoundException => StatusCodes.Status404NotFound,
                ResourceConflictException => StatusCodes.Status409Conflict,
                ForbiddenOperationException => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            };
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = statusCode,
                Title = exception.Message
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled API error");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred."
            });
        }
    }
}
