namespace Api.Global.Exceptions;

public sealed class EntityAlreadyExistsException : Exception
{
    public EntityAlreadyExistsException(string entityName, string key)
        : base($"{entityName} with key '{key}' already exists.")
    {
    }

    public EntityAlreadyExistsException(string message)
        : base(message)
    {
    }

    public EntityAlreadyExistsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
