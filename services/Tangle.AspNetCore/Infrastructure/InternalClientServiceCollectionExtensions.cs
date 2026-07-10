using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Config;

namespace Tangle.AspNetCore.Infrastructure;

public static class InternalClientServiceCollectionExtensions
{
    public static IServiceCollection AddTangleInternalClient<TClient, TInterface, TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        string missingBaseUrlMessage,
        Action<HttpClient>? configureClient = null)
        where TClient : class, TInterface
        where TInterface : class
        where TOptions : InternalServiceClientOptions, new()
    {
        services.Configure<TOptions>(configuration.GetSection(sectionName));
        var options = configuration.GetSection(sectionName).Get<TOptions>() ?? new TOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            throw new InvalidOperationException(missingBaseUrlMessage);

        var clientBuilder = services.AddHttpClient(
            typeof(TClient).Name,
            client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                configureClient?.Invoke(client);
            });
        _ = clientBuilder;
        services.AddScoped<TInterface, TClient>();

        return services;
    }

    public static IServiceCollection AddTangleInternalClientWithFallback<TClient, TInterface, TOptions, TFallback>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        string missingBaseUrlMessage,
        Action<HttpClient>? configureClient = null)
        where TClient : class, TInterface
        where TInterface : class
        where TOptions : InternalServiceClientOptions, new()
        where TFallback : class, TInterface
    {
        services.Configure<TOptions>(configuration.GetSection(sectionName));
        var options = configuration.GetSection(sectionName).Get<TOptions>() ?? new TOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            services.AddScoped<TInterface, TFallback>();
            return services;
        }

        var clientBuilder = services.AddHttpClient(
            typeof(TClient).Name,
            client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                configureClient?.Invoke(client);
            });
        _ = clientBuilder;
        services.AddScoped<TInterface, TClient>();

        return services;
    }
}
