using Api.Domain.Location.Realtime;
using Api.Global.Config;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;

namespace Api.Tests.Infrastructure;

internal static class LocationRealtimeTestHelpers
{
    public static HubConnection BuildHubConnection(
        ApiWebApplicationFactory factory,
        HttpClient client,
        string token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, "hubs/location"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    public static string GetLiveLocationKey(RedisOptions options, long userId) =>
        $"{options.InstanceName}location:live:{userId}";
}
