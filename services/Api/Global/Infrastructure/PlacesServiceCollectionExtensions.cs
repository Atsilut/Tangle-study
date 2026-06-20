using Api.Global.Config;

namespace Api.Global.Infrastructure;

public static class PlacesServiceCollectionExtensions
{
    public static IServiceCollection AddTanglePlaces(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PlacesOptions>(configuration.GetSection(PlacesOptions.SectionName));
        services.AddHttpClient("GooglePlaces", client => client.Timeout = TimeSpan.FromSeconds(10));
        return services;
    }
}
