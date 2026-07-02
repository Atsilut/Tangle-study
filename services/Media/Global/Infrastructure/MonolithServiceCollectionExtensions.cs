using Media.Global.Config;

namespace Media.Global.Infrastructure;

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
            services.AddScoped<Media.Client.IMonolithAccessClient, UnconfiguredMonolithAccessClient>();
            return services;
        }

        services.AddHttpClient(nameof(Media.Client.HttpMonolithAccessClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<Media.Client.IMonolithAccessClient, Media.Client.HttpMonolithAccessClient>();

        return services;
    }
}

internal sealed class UnconfiguredMonolithAccessClient : Media.Client.IMonolithAccessClient
{
    private static InvalidOperationException NotConfigured() =>
        new("Monolith:BaseUrl is not configured. Set it to the monolith API base URL for access checks.");

    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task EnsureCanViewPostMediaAsync(long postId, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task EnsureCanViewCommentMediaAsync(long commentId, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task EnsureCanAccessChatMessageMediaAsync(long chatMessageId, CancellationToken cancellationToken = default) =>
        throw NotConfigured();
}
