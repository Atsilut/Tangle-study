using Users.Client;
using Users.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Users.Infrastructure;

public static class SocialClientServiceCollectionExtensions
{
    public static IServiceCollection AddTangleSocialClient(this IServiceCollection services, IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpSocialClient, ISocialClient, SocialClientOptions>(
            configuration,
            SocialClientOptions.SectionName,
            "SocialClient:BaseUrl is not configured. Point it at the social-service base URL (e.g. http://social:8080 in Compose).",
            client => client.Timeout = UsersHttpClientDefaults.OutboundTimeout);
}
