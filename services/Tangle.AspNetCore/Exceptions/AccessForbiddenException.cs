namespace Tangle.AspNetCore.Exceptions;

public sealed class AccessForbiddenException(string message, int statusCode = StatusCodes.Status403Forbidden) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
