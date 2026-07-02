using Api.Client;
using Api.Domain.Media.Service;
using Api.Global.Config;
using Api.Global.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Global.Infrastructure;

public static class MediaClientServiceCollectionExtensions
{
    public static IServiceCollection AddTangleMediaClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MediaClientOptions>(configuration.GetSection(MediaClientOptions.SectionName));
        services.Configure<InternalAccessOptions>(configuration.GetSection(InternalAccessOptions.SectionName));
        services.AddScoped<InternalAccessAuthorizationFilter>();

        var options = configuration.GetSection(MediaClientOptions.SectionName).Get<MediaClientOptions>()
            ?? new MediaClientOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            services.AddScoped<IMediaClient, InProcessMediaClient>();
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
