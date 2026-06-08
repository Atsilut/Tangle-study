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

        services.AddSingleton<IMediaStorage, AzureBlobMediaStorage>();
        return services;
    }
}
