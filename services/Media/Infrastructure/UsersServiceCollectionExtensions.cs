using Media.Config;

namespace Media.Infrastructure;

public static class UsersServiceCollectionExtensions
{
    public static IServiceCollection AddTangleUsersAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<UsersOptions>(configuration.GetSection(UsersOptions.SectionName));
        var options = configuration.GetSection(UsersOptions.SectionName).Get<UsersOptions>() ?? new UsersOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            services.AddScoped<Media.Client.IUserClient, UnconfiguredUsersAccessClient>();
            return services;
        }

        services.AddHttpClient(nameof(Media.Client.HttpUserClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<Media.Client.IUserClient, Media.Client.HttpUserClient>();

        return services;
    }
}

internal sealed class UnconfiguredUsersAccessClient : Media.Client.IUserClient
{
    private static InvalidOperationException NotConfigured() =>
        new("Users:BaseUrl is not configured. Set it to the users-service base URL for access checks.");

    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default) =>
        throw NotConfigured();
}
