using Location.Client;
using Location.Config;
using Location.Db;
using Location.Infrastructure;
using Location.Queue;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;

namespace Location.Tests.Infrastructure;

public sealed class LocationWebApplicationFactory(
    string connectionString,
    string redisConnectionString) : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "integration-test-jwt-secret-at-least-32-characters-long";
    public const string TestJwtIssuer = "Tangle";
    public const string TestJwtAudience = "TangleClient";
    public const string TestInternalServiceSecret = "test-internal-service-secret";
    public const string TestWorkerCallbackSecret = "test-location-worker-secret";

    private readonly string _connectionString = connectionString;
    private readonly string _redisConnectionString = redisConnectionString;

    public InMemoryMonolithAccessClient MonolithAccess =>
        (InMemoryMonolithAccessClient)Services.GetRequiredService<IMonolithAccessClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Docker");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Monolith:BaseUrl", "http://monolith.test");
        builder.UseSetting("Monolith:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("InternalAccess:Secret", TestInternalServiceSecret);
        builder.UseSetting("WorkerCallback:Secret", TestWorkerCallbackSecret);
        builder.UseSetting("Redis:ConnectionString", _redisConnectionString);
        builder.UseSetting("Jwt:Secret", TestJwtSecret);
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtAudience);
        builder.UseSetting("Places:Enabled", "false");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Redis:ConnectionString"] = _redisConnectionString,
                ["Redis:WorkQueueStreamPrefix"] = "tangle:queue:",
                ["Redis:SignalRChannelPrefix"] = "tangle:signalr:",
                ["Monolith:BaseUrl"] = "http://monolith.test",
                ["Monolith:InternalSecret"] = TestInternalServiceSecret,
                ["InternalAccess:Secret"] = TestInternalServiceSecret,
                ["WorkerCallback:Secret"] = TestWorkerCallbackSecret,
                ["Jwt:Secret"] = TestJwtSecret,
                ["Jwt:Issuer"] = TestJwtIssuer,
                ["Jwt:Audience"] = TestJwtAudience,
                ["Places:Enabled"] = "false",
                ["Metrics:RequireScrapeSecret"] = "false",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            RemoveService<IMonolithAccessClient>(services);
            services.AddSingleton<IMonolithAccessClient>(sp =>
                new InMemoryMonolithAccessClient(sp.GetRequiredService<IHttpContextAccessor>()));
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<LocationDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<LocationDbContext>(options =>
                options.UseNpgsql(_connectionString, npgsql =>
                    npgsql.EnableRetryOnFailure(maxRetryCount: 5)));
        });

        builder.ConfigureTestServices(services =>
        {
            RemoveService<IConnectionMultiplexer>(services);
            RemoveService<IWorkQueue>(services);
            RemoveService<IDistributedCache>(services);

            services.AddSingleton<IDistributedCache, FakeDistributedCache>();
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(
                    RedisServiceCollectionExtensions.ParseRedisConfiguration(_redisConnectionString)));
            services.AddSingleton<IWorkQueue, FakeWorkQueue>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                MonolithAccess.Reset();
            }
            catch (InvalidOperationException)
            {
                // Host was not built.
            }

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
