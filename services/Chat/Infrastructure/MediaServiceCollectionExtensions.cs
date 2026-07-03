using Chat.Client;
using Chat.Config;

namespace Chat.Infrastructure;

public static class MediaServiceCollectionExtensions
{
    public static IServiceCollection AddTangleMediaClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MediaClientOptions>(configuration.GetSection(MediaClientOptions.SectionName));
        var options = configuration.GetSection(MediaClientOptions.SectionName).Get<MediaClientOptions>()
            ?? new MediaClientOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                "MediaClient:BaseUrl is not configured. Set it to the media-service base URL.");
        }

        services.AddHttpClient(nameof(HttpMediaClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<IMediaClient, HttpMediaClient>();

        return services;
    }
}
