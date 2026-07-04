using Posts.Client;
using Posts.Config;

namespace Posts.Infrastructure;

public static class MonolithServiceCollectionExtensions
{
    public static IServiceCollection AddTangleMonolithAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MonolithOptions>(configuration.GetSection(MonolithOptions.SectionName));
        var options = configuration.GetSection(MonolithOptions.SectionName).Get<MonolithOptions>() ?? new MonolithOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "Monolith:BaseUrl is not configured. Set it to the monolith API base URL for access checks.");
        }

        services.AddHttpClient(nameof(HttpMonolithAccessClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<IMonolithAccessClient, HttpMonolithAccessClient>();

        return services;
    }
}
