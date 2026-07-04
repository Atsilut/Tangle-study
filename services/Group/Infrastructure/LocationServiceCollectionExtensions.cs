using Group.Client;
using Group.Config;

namespace Group.Infrastructure;

public static class LocationServiceCollectionExtensions
{
    public static IServiceCollection AddTangleLocationClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LocationClientOptions>(configuration.GetSection(LocationClientOptions.SectionName));
        var options = configuration.GetSection(LocationClientOptions.SectionName).Get<LocationClientOptions>()
            ?? new LocationClientOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "LocationClient:BaseUrl is not configured. Set it to the location-service base URL.");
        }

        services.AddHttpClient(nameof(HttpLocationClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<ILocationClient, HttpLocationClient>();

        return services;
    }
}
