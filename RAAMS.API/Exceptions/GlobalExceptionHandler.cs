using Microsoft.AspNetCore.Diagnostics;
using RAAMS.Application.Common.Exceptions;

namespace RAAMS.API.Exceptions;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, message, level) = exception switch
        {
            WorkflowViolationException => (StatusCodes.Status400BadRequest, exception.Message, LogLevel.Warning),
            FluentValidation.ValidationException => (StatusCodes.Status400BadRequest, exception.Message, LogLevel.Warning),
            ForbiddenException => (StatusCodes.Status403Forbidden, exception.Message, LogLevel.Warning),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Access denied.", LogLevel.Warning),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", LogLevel.Error)
        };

        logger.Log(level, exception, "Request failed with status {StatusCode}", statusCode);
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new { error = message }, cancellationToken);
        return true;
    }
}
