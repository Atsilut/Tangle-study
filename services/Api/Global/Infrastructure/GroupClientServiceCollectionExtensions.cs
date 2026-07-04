using Api.Client;
using Api.Global.Config;

namespace Api.Global.Infrastructure;

public static class GroupClientServiceCollectionExtensions
{
    public static IServiceCollection AddTangleGroupClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GroupClientOptions>(configuration.GetSection(GroupClientOptions.SectionName));

        var options = configuration.GetSection(GroupClientOptions.SectionName).Get<GroupClientOptions>()
            ?? new GroupClientOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "GroupClient:BaseUrl is not configured. Point it at the group-service base URL (e.g. http://group:8080 in Compose).");
        }

        services.AddHttpClient(nameof(HttpGroupClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<IGroupClient, HttpGroupClient>();

        return services;
    }
}
