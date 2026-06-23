using Api.Domain.Chat.Realtime;
using Api.Global.Config;
using Api.Global.Queue;
using Microsoft.AspNetCore.SignalR.Client;

namespace Api.Tests.Infrastructure;

internal static class ChatRealtimeTestHelpers
{
    public static HubConnection BuildHubConnection(
        ApiWebApplicationFactory factory,
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

    public static string GetNicknameCacheKey(RedisOptions options, long userId) =>
        $"{options.InstanceName}users:nickname:{userId}";
}
