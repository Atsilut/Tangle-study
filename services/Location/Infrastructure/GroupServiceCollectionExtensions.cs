using Location.Client;
using Location.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Location.Infrastructure;

public static class GroupServiceCollectionExtensions
{
    public static IServiceCollection AddTangleGroupClient(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpGroupClient, IGroupClient, GroupClientOptions>(
            configuration,
            GroupClientOptions.SectionName,
            "GroupClient:BaseUrl is not configured. Set it to the group-service base URL.");
}
