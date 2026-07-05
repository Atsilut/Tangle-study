namespace Users.Exceptions;

public sealed class EntityAlreadyExistsException : Exception
{
    public int StatusCode { get; }

    public EntityAlreadyExistsException(string entityName, string key, int statusCode = StatusCodes.Status409Conflict)
        : base($"{entityName} with key '{key}' already exists.")
    {
        StatusCode = statusCode;
    }

    public EntityAlreadyExistsException(string message, int statusCode = StatusCodes.Status409Conflict)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public EntityAlreadyExistsException(string message, Exception innerException, int statusCode = StatusCodes.Status409Conflict)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
