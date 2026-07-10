using System.Net;
using System.Net.Http.Json;
using Location.Dto;
using Microsoft.AspNetCore.SignalR.Client;
using Tangle.TestSupport.Auth;
using Tangle.TestSupport.Harness;
using Tangle.TestSupport.Integration;
using Users.Dto;

namespace Stack.Tests.Infrastructure;

internal static class LocationHarnessHelpers
{
    public const string SessionsBase = "/api/location/sessions";
    public const string PinsBase = "/api/location/pins";
    public const string ClustersBase = "/api/location/clusters";

    public static HubConnection BuildHubConnection(HttpClient client) =>
        HarnessHubConnectionFactory.Build(client, "hubs/location");

    public static async Task<MapPinGetResponseDto> CreateMapPinAsync(
        HttpClient client,
        UserGetResponseDto owner,
        decimal latitude,
        decimal longitude)
    {
        await HarnessAuthHelpers.LoginAsAsync(client, owner);
        var res = await client.PostAsJsonAsync(
            PinsBase,
            new MapPinCreateRequestDto { Latitude = latitude, Longitude = longitude },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<MapPinGetResponseDto>(TestContext.Current.CancellationToken))!;
    }

    public static Task<List<MapClusterGetResponseDto>> PollClustersUntilReadyAsync(
        HttpClient client,
        decimal minLatitude,
        decimal maxLatitude,
        decimal minLongitude,
        decimal maxLongitude,
        int zoom,
        TimeSpan timeout)
    {
        var query =
            $"{ClustersBase}?minLatitude={minLatitude}&maxLatitude={maxLatitude}" +
            $"&minLongitude={minLongitude}&maxLongitude={maxLongitude}&zoom={zoom}";

        return IntegrationTestPolling.PollUntilAsync(
            async ct =>
            {
                var res = await client.GetAsync(query, ct);
                if (res.StatusCode != HttpStatusCode.OK) return new List<MapClusterGetResponseDto>();
                return (await res.Content.ReadFromJsonAsync<List<MapClusterGetResponseDto>>(ct)) ?? [];
            },
            clusters => clusters.Count > 0,
            timeout,
            delay: TimeSpan.FromMilliseconds(500),
            cancellationToken: TestContext.Current.CancellationToken);
    }

    public static async Task<LocationSessionGetResponseDto> StartSessionAsync(
        HttpClient client,
        UserGetResponseDto owner,
        long groupId,
        decimal latitude = 37.5m,
        decimal longitude = 126.9m)
    {
        await HarnessAuthHelpers.LoginAsAsync(client, owner);
        var res = await client.PostAsJsonAsync(
            SessionsBase,
            new LocationSessionCreateRequestDto
            {
                GroupId = groupId,
                Latitude = latitude,
                Longitude = longitude,
            },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<LocationSessionGetResponseDto>(TestContext.Current.CancellationToken))!;
    }
}
