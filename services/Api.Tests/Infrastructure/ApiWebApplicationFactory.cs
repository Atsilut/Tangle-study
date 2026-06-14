using Api.Domain.Media.Storage;
using Api.Global.Db;
using Api.Global.Events;
using Api.Global.Queue;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Api.Tests.Infrastructure;

public sealed class ApiWebApplicationFactory(
    string connectionString,
    bool redisEnabled = false,
    string? redisConnectionString = null,
    bool metricsRequireScrapeSecret = false,
    string? metricsScrapeSecret = null) : WebApplicationFactory<Program>
{
    public const string TestWorkerCallbackSecret = "test-media-worker-secret";

    private readonly string _connectionString = connectionString;
    private readonly bool _redisEnabled = redisEnabled;
    private readonly string? _redisConnectionString = redisConnectionString;
    private readonly bool _metricsRequireScrapeSecret = metricsRequireScrapeSecret;
    private readonly string? _metricsScrapeSecret = metricsScrapeSecret;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Redis:Enabled"] = _redisEnabled ? "true" : "false",
                ["Redis:ConnectionString"] = _redisEnabled ? _redisConnectionString : "",
                ["Redis:InstanceName"] = "tangle:",
                ["Redis:SignalRChannelPrefix"] = "tangle:signalr:",
                ["Redis:WorkQueueStreamPrefix"] = "tangle:queue:",
                ["Media:Enabled"] = "true",
                ["Media:ConnectionString"] = "UseDevelopmentStorage=true",
                ["Media:WorkerCallbackSecret"] = TestWorkerCallbackSecret,
                ["Metrics:RequireScrapeSecret"] = _metricsRequireScrapeSecret ? "true" : "false",
                ["Metrics:ScrapeSecret"] = _metricsScrapeSecret ?? "",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            RemoveService<IMediaStorage>(services);
            services.AddSingleton<IMediaStorage, FakeMediaStorage>();
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_connectionString, npgsql =>
                    npgsql.EnableRetryOnFailure(maxRetryCount: 5)));
        });

        if (_redisEnabled && !string.IsNullOrWhiteSpace(_redisConnectionString))
            builder.ConfigureTestServices(services =>
            {
                RemoveService<IConnectionMultiplexer>(services);
                RemoveService<IWorkQueue>(services);
                RemoveService<IEventPublisher>(services);

                services.AddSingleton<IConnectionMultiplexer>(_ =>
                    ConnectionMultiplexer.Connect(_redisConnectionString!));
                services.AddSingleton<IWorkQueue, RedisStreamWorkQueue>();
                services.AddSingleton<IEventPublisher, RedisEventPublisher>();
            });
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(T)).ToList())
            services.Remove(descriptor);
    }
}
