using Location.Config;
using Location.Realtime;
using Microsoft.AspNetCore.SignalR.Client;

namespace Location.Tests.Infrastructure;

internal static class LocationRealtimeTestHelpers
{
    public static HubConnection BuildHubConnection(
        LocationWebApplicationFactory factory,
        HttpClient client,
        string token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "hubs/location"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    public static string GetLiveLocationKey(RedisOptions options, long groupId, long userId) =>
        $"{options.InstanceName}location:live:{groupId}:{userId}";
}
