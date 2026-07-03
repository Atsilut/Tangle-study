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
            services.AddScoped<IMediaClient, UnconfiguredMediaClient>();
            return services;
        }

        services.AddHttpClient(nameof(HttpMediaClient), client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<IMediaClient, HttpMediaClient>();

        return services;
    }
}

internal sealed class UnconfiguredMediaClient : IMediaClient
{
    private static InvalidOperationException NotConfigured() =>
        new("MediaClient:BaseUrl is not configured. Set it to the media-service base URL.");

    public Task LinkToChatMessageAsync(long chatMessageId, long senderUserId, long? mediaAssetId) =>
        throw NotConfigured();

    public Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByChatMessageIdsAsync(
        IReadOnlyCollection<long> chatMessageIds) =>
        throw NotConfigured();

    public Task<MediaAssetGetResponseDto?> GetMediaForChatMessageAsync(long chatMessageId) =>
        throw NotConfigured();

    public Task DeleteBlobStorageForChatMessageAsync(long chatMessageId) =>
        throw NotConfigured();
}
