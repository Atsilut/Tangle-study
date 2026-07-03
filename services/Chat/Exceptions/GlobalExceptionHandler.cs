using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Chat.Exceptions;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!TryMap(exception, out var statusCode, out var title)) return false;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path,
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private static bool TryMap(Exception exception, out int statusCode, out string title)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            switch (ex)
            {
                case EntityNotFoundException notFound:
                    statusCode = notFound.StatusCode;
                    title = TitleForStatus(statusCode);
                    return true;
                case EntityAlreadyExistsException alreadyExists:
                    statusCode = alreadyExists.StatusCode;
                    title = TitleForStatus(statusCode);
                    return true;
                case AccessForbiddenException forbidden:
                    statusCode = forbidden.StatusCode;
                    title = TitleForStatus(statusCode);
                    return true;
                case UnauthorizedAccessException:
                    statusCode = StatusCodes.Status401Unauthorized;
                    title = TitleForStatus(statusCode);
                    return true;
                case ArgumentException:
                    statusCode = StatusCodes.Status400BadRequest;
                    title = TitleForStatus(statusCode);
                    return true;
                case DbUpdateException dbUpdate when IsUniqueConstraintViolation(dbUpdate):
                    statusCode = StatusCodes.Status409Conflict;
                    title = TitleForStatus(statusCode);
                    return true;
            }
        }

        statusCode = default;
        title = default!;
        return false;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static string TitleForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Error",
    };
}
