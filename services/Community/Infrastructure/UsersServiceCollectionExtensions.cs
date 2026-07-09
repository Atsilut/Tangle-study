using Community.Client;
using Community.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Community.Infrastructure;

public static class UsersServiceCollectionExtensions
{
    public static IServiceCollection AddTangleUsersAccess(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpUserClient, IUserClient, UsersOptions>(
            configuration,
            UsersOptions.SectionName,
            "Users:BaseUrl is not configured. Set it to the users-service base URL for access checks.");
}
