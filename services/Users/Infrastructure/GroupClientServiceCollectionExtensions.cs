using Users.Client;
using Users.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Users.Infrastructure;

public static class GroupClientServiceCollectionExtensions
{
    public static IServiceCollection AddTangleGroupClient(this IServiceCollection services, IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpGroupClient, IGroupClient, GroupClientOptions>(
            configuration,
            GroupClientOptions.SectionName,
            "GroupClient:BaseUrl is not configured. Point it at the group-service base URL (e.g. http://group:8080 in Compose).",
            client => client.Timeout = UsersHttpClientDefaults.OutboundTimeout);
}
