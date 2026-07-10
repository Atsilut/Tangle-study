using Community.Client;
using Community.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Community.Infrastructure;

public static class SocialServiceCollectionExtensions
{
    public static IServiceCollection AddTangleSocialClient(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpSocialClient, ISocialClient, SocialClientOptions>(
            configuration,
            SocialClientOptions.SectionName,
            "SocialClient:BaseUrl is not configured. Set it to the social-service base URL.");
}
