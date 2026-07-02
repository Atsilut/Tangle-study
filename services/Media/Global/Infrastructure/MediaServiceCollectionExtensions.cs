using Media;
using Media.Storage;
using Media.Global.Config;
using Media.Global.Security;
using Azure.Storage.Blobs;

namespace Media.Global.Infrastructure;

public static class MediaServiceCollectionExtensions
{
    public static IServiceCollection AddTangleMedia(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MediaOptions>(configuration.GetSection(MediaOptions.SectionName));
        services.AddSingleton<MediaLimitPolicy>();
        services.AddScoped<WorkerCallbackAuthorizationFilter>();

        var options = configuration.GetSection(MediaOptions.SectionName).Get<MediaOptions>() ?? new MediaOptions();
        EnsureLimitsConfigured(options);

        var connectionString = configuration[$"{MediaOptions.SectionName}:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (options.Enabled)
                throw new InvalidOperationException(
                    "Media:Enabled is true but Media:ConnectionString is not configured. " +
                    "Start Azurite (docker compose up azurite) and set the connection string.");

            return services;
        }

        try
        {
            _ = AzureBlobMediaStorage.CreateServiceClient(connectionString);
        }
        catch (Exception ex)
        {
            var preview = connectionString.Length > 48
                ? connectionString[..48] + "..."
                : connectionString;
            throw new InvalidOperationException(
                $"Media:ConnectionString is set but could not be parsed for Azure Blob Storage (length={connectionString.Length}, preview={preview}).",
                ex);
        }

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
