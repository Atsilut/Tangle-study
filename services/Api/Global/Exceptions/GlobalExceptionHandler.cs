using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Global.Exceptions;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private static readonly Dictionary<Type, Func<Exception, (int StatusCode, string Title)>> Mappings = new()
    {
        [typeof(EntityNotFoundException)] = ex => (((EntityNotFoundException)ex).StatusCode, TitleForStatus(((EntityNotFoundException)ex).StatusCode)),
        [typeof(UnauthorizedAccessException)] = _ => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        [typeof(EntityAlreadyExistsException)] = _ => (StatusCodes.Status400BadRequest, "Bad Request"),
        [typeof(ArgumentException)] = _ => (StatusCodes.Status400BadRequest, "Bad Request"),
    };

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!Mappings.TryGetValue(exception.GetType(), out var map))
            return false;

        var (statusCode, title) = map(exception);

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

    private static string TitleForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status404NotFound => "Not Found",
        _ => "Error",
    };
}
