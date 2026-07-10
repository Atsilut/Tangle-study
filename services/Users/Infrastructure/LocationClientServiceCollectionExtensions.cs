using Users.Client;
using Users.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Users.Infrastructure;

public static class LocationClientServiceCollectionExtensions
{
    public static IServiceCollection AddTangleLocationClient(this IServiceCollection services, IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpLocationClient, ILocationClient, LocationClientOptions>(
            configuration,
            LocationClientOptions.SectionName,
            "LocationClient:BaseUrl is not configured. Point it at the location-service base URL (e.g. http://location:8080 in Compose).",
            client => client.Timeout = UsersHttpClientDefaults.OutboundTimeout);
}
