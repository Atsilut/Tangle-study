using Location.Client;
using Location.Config;

namespace Location.Infrastructure;

public static class GroupServiceCollectionExtensions
{
    public static IServiceCollection AddTangleGroupClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GroupClientOptions>(configuration.GetSection(GroupClientOptions.SectionName));
        var options = configuration.GetSection(GroupClientOptions.SectionName).Get<GroupClientOptions>()
            ?? new GroupClientOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "GroupClient:BaseUrl is not configured. Set it to the group-service base URL.");
        }

        services.AddHttpClient(nameof(HttpGroupClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<IGroupClient, HttpGroupClient>();

        return services;
    }
}
