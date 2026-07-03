using Chat.Config;
using Chat.Queue;
using Chat.Realtime;
using Microsoft.AspNetCore.SignalR.Client;

namespace Chat.Tests.Infrastructure;

internal static class ChatRealtimeTestHelpers
{
    public static HubConnection BuildHubConnection(
        ChatWebApplicationFactory factory,
        HttpClient client,
        string token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "hubs/chat"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    public static string GetWorkQueueStreamKey(RedisOptions options, string streamName) =>
        options.WorkQueueStreamPrefix.EndsWith(':')
            ? $"{options.WorkQueueStreamPrefix}{streamName}"
            : $"{options.WorkQueueStreamPrefix}:{streamName}";

    public static string GetWorkQueueStreamKey(RedisOptions options) =>
        GetWorkQueueStreamKey(options, WorkQueueStreams.ChatMessageCreated);
}
