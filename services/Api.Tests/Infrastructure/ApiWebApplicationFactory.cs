using Api.Client;
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
using Npgsql;
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
    private readonly FakeMediaClient _fakeMediaClient = new();
    private readonly FakeChatClient _fakeChatClient = new();
    private readonly FakeLocationClient _fakeLocationClient = new();
    private readonly FakeCommunityClient _fakeCommunityClient = new();

    public FakeMediaClient FakeMediaClient => _fakeMediaClient;
    public FakeChatClient FakeChatClient => _fakeChatClient;
    public FakeLocationClient FakeLocationClient => _fakeLocationClient;
    public FakeCommunityClient FakeCommunityClient => _fakeCommunityClient;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("WorkerCallback:Secret", TestWorkerCallbackSecret);
        builder.UseSetting("MediaClient:BaseUrl", "http://media.test");
        builder.UseSetting("MediaClient:InternalSecret", "test-internal-service-secret");
        builder.UseSetting("ChatClient:BaseUrl", "http://chat.test");
        builder.UseSetting("ChatClient:InternalSecret", "test-internal-service-secret");
        builder.UseSetting("LocationClient:BaseUrl", "http://location.test");
        builder.UseSetting("LocationClient:InternalSecret", "test-internal-service-secret");
        builder.UseSetting("CommunityClient:BaseUrl", "http://community.test");
        builder.UseSetting("CommunityClient:InternalSecret", "test-internal-service-secret");

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
                ["MediaClient:BaseUrl"] = "http://media.test",
                ["MediaClient:InternalSecret"] = "test-internal-service-secret",
                ["ChatClient:BaseUrl"] = "http://chat.test",
                ["ChatClient:InternalSecret"] = "test-internal-service-secret",
                ["LocationClient:BaseUrl"] = "http://location.test",
                ["LocationClient:InternalSecret"] = "test-internal-service-secret",
                ["CommunityClient:BaseUrl"] = "http://community.test",
                ["CommunityClient:InternalSecret"] = "test-internal-service-secret",
                ["InternalAccess:Secret"] = "test-internal-service-secret",
                ["WorkerCallback:Secret"] = TestWorkerCallbackSecret,
                ["Metrics:RequireScrapeSecret"] = _metricsRequireScrapeSecret ? "true" : "false",
                ["Metrics:ScrapeSecret"] = _metricsScrapeSecret ?? "",
                ["Jwt:Secret"] = "integration-test-jwt-secret-at-least-32-characters-long",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            RemoveService<IMediaClient>(services);
            services.AddSingleton<IMediaClient>(_fakeMediaClient);

            RemoveService<IChatClient>(services);
            services.AddSingleton<IChatClient>(_fakeChatClient);

            RemoveService<ILocationClient>(services);
            services.AddSingleton<ILocationClient>(_fakeLocationClient);

            RemoveService<ICommunityClient>(services);
            services.AddSingleton<ICommunityClient>(_fakeCommunityClient);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                NpgsqlConnection.ClearPool(connection);
            }
            catch
            {
                // Best-effort pool cleanup between per-test factory instances.
            }
        }

        base.Dispose(disposing);
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(T)).ToList())
            services.Remove(descriptor);
    }
}
