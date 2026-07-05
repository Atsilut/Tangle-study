using Location.Config;
using Location.Realtime;
using Microsoft.AspNetCore.SignalR.Client;

namespace Location.Tests.Infrastructure;

internal static class LocationRealtimeTestHelpers
{
    public static HubConnection BuildHubConnection(
        LocationWebApplicationFactory factory,
        HttpClient client,
        long userId) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "hubs/location"), options =>
            {
                options.HttpMessageHandlerFactory = _ => new GatewayIdentityHttpHandler(
                    factory.Server.CreateHandler(),
                    userId,
                    LocationTestAuthHelpers.TestGatewaySecret);
            })
            .Build();

    public static string GetLiveLocationKey(RedisOptions options, long groupId, long userId) =>
        $"{options.InstanceName}location:live:{groupId}:{userId}";

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
