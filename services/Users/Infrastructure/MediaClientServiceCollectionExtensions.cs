using Users.Client;
using Users.Config;
using Tangle.AspNetCore.Infrastructure;

namespace Users.Infrastructure;

public static class MediaClientServiceCollectionExtensions
{
    public static IServiceCollection AddTangleMediaClient(this IServiceCollection services, IConfiguration configuration) =>
        services.AddTangleInternalClient<HttpMediaClient, IMediaClient, MediaClientOptions>(
            configuration,
            MediaClientOptions.SectionName,
            "MediaClient:BaseUrl is not configured. Point it at the media-service base URL (e.g. http://media:8080 in Compose).",
            client => client.Timeout = UsersHttpClientDefaults.OutboundTimeout);
}
