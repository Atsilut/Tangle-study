using Group.Client;
using Group.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Group.Infrastructure;

public static class CommunityServiceCollectionExtensions
{
    public static IServiceCollection AddTangleCommunityClient(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpCommunityClient, ICommunityClient, CommunityClientOptions>(
            configuration,
            CommunityClientOptions.SectionName,
            "CommunityClient:BaseUrl is not configured. Set it to the community-service base URL.");
}
