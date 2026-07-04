using Api.Client;
using Api.Global.Config;

namespace Api.Global.Infrastructure;

public static class SocialClientServiceCollectionExtensions
{
    public static IServiceCollection AddTangleSocialClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SocialClientOptions>(configuration.GetSection(SocialClientOptions.SectionName));

        var options = configuration.GetSection(SocialClientOptions.SectionName).Get<SocialClientOptions>()
            ?? new SocialClientOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "SocialClient:BaseUrl is not configured. Point it at the social-service base URL (e.g. http://social:8080 in Compose).");
        }

        services.AddHttpClient(nameof(HttpSocialClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<ISocialClient, HttpSocialClient>();

        return services;
    }
}
