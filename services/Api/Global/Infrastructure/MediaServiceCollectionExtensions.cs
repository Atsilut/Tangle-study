using Api.Domain.Media;
using Api.Domain.Media.Storage;
using Api.Global.Config;

namespace Api.Global.Infrastructure;

public static class MediaServiceCollectionExtensions
{
    public static IServiceCollection AddTangleMedia(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MediaOptions>(configuration.GetSection(MediaOptions.SectionName));
        services.AddSingleton<MediaLimitPolicy>();

        var options = configuration.GetSection(MediaOptions.SectionName).Get<MediaOptions>() ?? new MediaOptions();
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            if (options.Enabled)
                throw new InvalidOperationException(
                    "Media:Enabled is true but Media:ConnectionString is not configured. " +
                    "Start Azurite (docker compose up azurite) and set the connection string.");

            return services;
        }

        EnsureLimitsConfigured(options);
        services.AddSingleton<IMediaStorage, AzureBlobMediaStorage>();
        return services;
    }

    private static void EnsureLimitsConfigured(MediaOptions options)
    {
        if (options.IngressMultiplier <= 0)
            throw new InvalidOperationException(
                "Media:IngressMultiplier must be greater than zero. Configure it in media-limits.yml.");

        EnsureContextLimitsConfigured("Post", options.Post);
        EnsureContextLimitsConfigured("Comment", options.Comment);
        EnsureContextLimitsConfigured("ChatMessage", options.ChatMessage);
    }

    private static void EnsureContextLimitsConfigured(string contextName, MediaContextLimitOptions limits)
    {
        if (limits.VideoPerFileBytes <= 0)
            throw new InvalidOperationException(
                $"Media:{contextName}:VideoPerFileBytes must be greater than zero. Configure it in media-limits.yml.");

        if (limits.ImagePerFileBytes <= 0)
            throw new InvalidOperationException(
                $"Media:{contextName}:ImagePerFileBytes must be greater than zero. Configure it in media-limits.yml.");
    }
}
