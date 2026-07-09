using Media.Client;
using Media.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Media.Infrastructure;

public static class CommunityAccessServiceCollectionExtensions
{
    public static IServiceCollection AddTangleCommunityAccess(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpCommunityAccessClient, ICommunityAccessClient, CommunityClientOptions>(
            configuration,
            CommunityClientOptions.SectionName,
            "CommunityClient:BaseUrl is not configured. Point it at the community-service base URL (e.g. http://community:8080 in Compose).");
}
