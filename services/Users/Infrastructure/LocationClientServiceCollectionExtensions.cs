using Users.Client;
using Users.Config;

namespace Users.Infrastructure;

public static class LocationClientServiceCollectionExtensions
{
    public static IServiceCollection AddTangleLocationClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LocationClientOptions>(configuration.GetSection(LocationClientOptions.SectionName));

        var options = configuration.GetSection(LocationClientOptions.SectionName).Get<LocationClientOptions>()
            ?? new LocationClientOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "LocationClient:BaseUrl is not configured. Point it at the location-service base URL (e.g. http://location:8080 in Compose).");
        }

        services.AddHttpClient(nameof(HttpLocationClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = UsersHttpClientDefaults.OutboundTimeout;
        });
        services.AddScoped<ILocationClient, HttpLocationClient>();

        return services;
    }
}
