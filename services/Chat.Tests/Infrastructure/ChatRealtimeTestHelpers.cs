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
        long userId) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "hubs/chat"), options =>
            {
                options.HttpMessageHandlerFactory = _ => new GatewayIdentityHttpHandler(
                    factory.Server.CreateHandler(),
                    userId,
                    GatewayTestAuthHelpers.TestGatewaySecret);
            })
            .Build();

    public static string GetWorkQueueStreamKey(RedisOptions options, string streamName) =>
        options.WorkQueueStreamPrefix.EndsWith(':')
            ? $"{options.WorkQueueStreamPrefix}{streamName}"
            : $"{options.WorkQueueStreamPrefix}:{streamName}";

    public static string GetWorkQueueStreamKey(RedisOptions options) =>
        GetWorkQueueStreamKey(options, WorkQueueStreams.ChatMessageCreated);

    private sealed class GatewayIdentityHttpHandler(
        HttpMessageHandler inner,
        long userId,
        string gatewaySecret) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.Headers.Remove("X-User-Id");
            request.Headers.Remove("X-Gateway-Secret");
            request.Headers.Add("X-Gateway-Secret", gatewaySecret);
            request.Headers.Add("X-User-Id", userId.ToString());
            return base.SendAsync(request, cancellationToken);
        }
    }
}
