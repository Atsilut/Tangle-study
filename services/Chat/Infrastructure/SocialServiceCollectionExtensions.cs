using Chat.Client;
using Chat.Config;

namespace Chat.Infrastructure;

public static class SocialServiceCollectionExtensions
{
    public static IServiceCollection AddTangleSocialClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SocialClientOptions>(configuration.GetSection(SocialClientOptions.SectionName));
        var options = configuration.GetSection(SocialClientOptions.SectionName).Get<SocialClientOptions>()
            ?? new SocialClientOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "SocialClient:BaseUrl is not configured. Set it to the social-service base URL.");
        }

        services.AddHttpClient(nameof(HttpSocialClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<ISocialClient, HttpSocialClient>();

        return services;
    }
}
