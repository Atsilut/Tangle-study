using Api.Client;
using Api.Global.Config;

namespace Api.Global.Infrastructure;

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
        });
        services.AddScoped<ILocationClient, HttpLocationClient>();

        return services;
    }
}
