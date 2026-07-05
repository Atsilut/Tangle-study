using Group.Client;
using Group.Config;

namespace Group.Infrastructure;

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
            throw new InvalidOperationException(
                "Users:BaseUrl is not configured. Set it to the users-service base URL for access checks.");
        }

        services.AddHttpClient(nameof(HttpUserClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<IUserClient, HttpUserClient>();

        return services;
    }
}
