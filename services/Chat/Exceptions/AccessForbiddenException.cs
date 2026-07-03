namespace Chat.Exceptions;

public sealed class AccessForbiddenException : Exception
{
    public int StatusCode { get; }

    public AccessForbiddenException(string message, int statusCode = StatusCodes.Status403Forbidden)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
