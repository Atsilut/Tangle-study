using Media.Client;
using Media.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Media.Infrastructure;

public static class UsersServiceCollectionExtensions
{
    public static IServiceCollection AddTangleUsersAccess(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddTangleInternalClientWithFallback<HttpUserClient, IUserClient, UsersOptions, UnconfiguredUsersAccessClient>(
            configuration,
            UsersOptions.SectionName,
            "Users:BaseUrl is not configured. Set it to the users-service base URL for access checks.");
}

internal sealed class UnconfiguredUsersAccessClient : IUserClient
{
    private static InvalidOperationException NotConfigured() =>
        new("Users:BaseUrl is not configured. Set it to the users-service base URL for access checks.");

    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default) =>
        throw NotConfigured();
}
