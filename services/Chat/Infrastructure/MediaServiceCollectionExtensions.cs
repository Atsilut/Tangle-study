using Chat.Client;
using Chat.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Chat.Infrastructure;

public static class MediaServiceCollectionExtensions
{
    public static IServiceCollection AddTangleMediaClient(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpMediaClient, IMediaClient, MediaClientOptions>(
            configuration,
            MediaClientOptions.SectionName,
            "MediaClient:BaseUrl is not configured. Set it to the media-service base URL.");
}
