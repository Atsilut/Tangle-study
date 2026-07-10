using Microsoft.AspNetCore.SignalR.Client;
using Tangle.TestSupport.Integration;

namespace Tangle.TestSupport.Harness;

public static class HarnessRealtimeTestHelpers
{
    public static async Task<(HubConnection Connection, TPayload Payload)> ConnectJoinAndWaitForEventAsync<TPayload>(
        HttpClient client,
        string hubPath,
        string eventName,
        string joinMethodName,
        object? joinArgument,
        Func<TPayload, bool>? predicate = null,
        TimeSpan? timeout = null)
    {
        predicate ??= _ => true;
        var received = new TaskCompletionSource<TPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = HarnessHubConnectionFactory.Build(client, hubPath);
        connection.On<TPayload>(eventName, payload =>
        {
            if (predicate(payload))
                received.TrySetResult(payload);
        });
        await connection.StartAsync(TestContext.Current.CancellationToken);
        if (joinArgument is null)
            await connection.InvokeAsync(joinMethodName, TestContext.Current.CancellationToken);
        else
            await connection.InvokeAsync(joinMethodName, joinArgument, TestContext.Current.CancellationToken);

        var waitTask = received.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        return (connection, await waitTask);
    }

    public static async Task<HubConnection> ConnectAndJoinAsync(
        HttpClient client,
        string hubPath,
        string joinMethodName,
        object joinArgument)
    {
        var connection = HarnessHubConnectionFactory.Build(client, hubPath);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await connection.InvokeAsync(joinMethodName, joinArgument, TestContext.Current.CancellationToken);
        return connection;
    }

    public static Task<TPayload> WaitForHubEventAsync<TPayload>(
        HubConnection connection,
        string eventName,
        Func<TPayload, bool>? predicate = null,
        TimeSpan? timeout = null)
    {
        predicate ??= _ => true;
        var received = new TaskCompletionSource<TPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<TPayload>(eventName, payload =>
        {
            if (predicate(payload))
                received.TrySetResult(payload);
        });
        return received.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }
}
