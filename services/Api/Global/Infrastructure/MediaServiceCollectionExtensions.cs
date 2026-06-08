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
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            services.AddSingleton<IMediaStorage, NoOpMediaStorage>();
            return services;
        }

        services.AddSingleton<IMediaStorage, AzureBlobMediaStorage>();
        return services;
    }
}
