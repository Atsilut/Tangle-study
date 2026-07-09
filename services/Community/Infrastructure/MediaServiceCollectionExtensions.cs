using Community.Client;
using Community.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Community.Infrastructure;

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
