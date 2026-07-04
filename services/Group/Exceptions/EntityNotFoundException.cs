namespace Group.Exceptions;

public sealed class EntityNotFoundException : Exception
{
    public int StatusCode { get; }

    public EntityNotFoundException(string entityName, string key, int statusCode = StatusCodes.Status404NotFound)
        : base($"Cannot find {entityName} with key '{key}'.")
    {
        StatusCode = statusCode;
    }

    public EntityNotFoundException(string message, int statusCode = StatusCodes.Status404NotFound)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public EntityNotFoundException(string message, Exception innerException, int statusCode = StatusCodes.Status404NotFound)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
