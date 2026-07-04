using Api.Client;
using Api.Global.Config;

namespace Api.Global.Infrastructure;

public static class CommunityClientServiceCollectionExtensions
{
    public static IServiceCollection AddTangleCommunityClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CommunityClientOptions>(configuration.GetSection(CommunityClientOptions.SectionName));

        var options = configuration.GetSection(CommunityClientOptions.SectionName).Get<CommunityClientOptions>()
            ?? new CommunityClientOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "CommunityClient:BaseUrl is not configured. Point it at the community-service base URL (e.g. http://community:8080 in Compose).");
        }

        services.AddHttpClient(nameof(HttpCommunityClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<ICommunityClient, HttpCommunityClient>();

        return services;
    }
}
