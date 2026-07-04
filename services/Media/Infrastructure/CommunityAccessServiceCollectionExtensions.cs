using Media.Client;
using Media.Config;

namespace Media.Infrastructure;

public static class CommunityAccessServiceCollectionExtensions
{
    public static IServiceCollection AddTangleCommunityAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CommunityClientOptions>(configuration.GetSection(CommunityClientOptions.SectionName));
        var options = configuration.GetSection(CommunityClientOptions.SectionName).Get<CommunityClientOptions>()
            ?? new CommunityClientOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "CommunityClient:BaseUrl is not configured. Point it at the community-service base URL (e.g. http://community:8080 in Compose).");
        }

        services.AddHttpClient(nameof(HttpCommunityAccessClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<ICommunityAccessClient, HttpCommunityAccessClient>();

        return services;
    }
}
