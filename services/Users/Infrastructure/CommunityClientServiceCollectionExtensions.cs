using Users.Client;
using Users.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Users.Infrastructure;

public static class CommunityClientServiceCollectionExtensions
{
    public static IServiceCollection AddTangleCommunityClient(this IServiceCollection services, IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpCommunityClient, ICommunityClient, CommunityClientOptions>(
            configuration,
            CommunityClientOptions.SectionName,
            "CommunityClient:BaseUrl is not configured. Point it at the community-service base URL (e.g. http://community:8080 in Compose).",
            client => client.Timeout = UsersHttpClientDefaults.OutboundTimeout);
}
