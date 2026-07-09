namespace Tangle.AspNetCore.Queue;

public interface IRedisWorkQueueOptions
{
    string WorkQueueStreamPrefix { get; }
}
