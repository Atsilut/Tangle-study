using Community.Client;
using Community.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Community.Infrastructure;

public static class LocationServiceCollectionExtensions
{
    public static IServiceCollection AddTangleLocationClient(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpLocationClient, ILocationClient, LocationClientOptions>(
            configuration,
            LocationClientOptions.SectionName,
            "LocationClient:BaseUrl is not configured. Set it to the location-service base URL.");
}
