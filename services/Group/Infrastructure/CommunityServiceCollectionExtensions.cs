using Group.Client;
using Group.Config;

namespace Group.Infrastructure;

public static class CommunityServiceCollectionExtensions
{
    public static IServiceCollection AddTangleCommunityClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CommunityClientOptions>(configuration.GetSection(CommunityClientOptions.SectionName));
        var options = configuration.GetSection(CommunityClientOptions.SectionName).Get<CommunityClientOptions>()
            ?? new CommunityClientOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "CommunityClient:BaseUrl is not configured. Set it to the community-service base URL.");
        }

        services.AddHttpClient(nameof(HttpCommunityClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<ICommunityClient, HttpCommunityClient>();

        return services;
    }
}
