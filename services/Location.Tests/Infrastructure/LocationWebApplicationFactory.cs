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

    public InMemoryUserClient InMemoryUser =>
        Services.GetRequiredService<InMemoryUserClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Docker");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Users:BaseUrl", "http://users.test");
        builder.UseSetting("Users:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("GatewayIdentity:Secret", LocationTestAuthHelpers.TestGatewaySecret);
        builder.UseSetting("SocialClient:BaseUrl", "http://social.test");
        builder.UseSetting("SocialClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("GroupClient:BaseUrl", "http://group.test");
        builder.UseSetting("GroupClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("CommunityClient:BaseUrl", "http://community.test");
        builder.UseSetting("CommunityClient:InternalSecret", TestInternalServiceSecret);
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
                ["Users:BaseUrl"] = "http://users.test",
                ["Users:InternalSecret"] = TestInternalServiceSecret,
                ["GatewayIdentity:Secret"] = LocationTestAuthHelpers.TestGatewaySecret,
                ["SocialClient:BaseUrl"] = "http://social.test",
                ["SocialClient:InternalSecret"] = TestInternalServiceSecret,
                ["GroupClient:BaseUrl"] = "http://group.test",
                ["GroupClient:InternalSecret"] = TestInternalServiceSecret,
                ["CommunityClient:BaseUrl"] = "http://community.test",
                ["CommunityClient:InternalSecret"] = TestInternalServiceSecret,
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
            RemoveService<IUserClient>(services);
            RemoveService<ISocialClient>(services);
            RemoveService<ICommunityAccessClient>(services);
            RemoveService<IGroupClient>(services);
            services.AddSingleton<InMemoryUserClient>(sp =>
                new InMemoryUserClient(sp.GetRequiredService<IHttpContextAccessor>()));
            services.AddSingleton<IUserClient>(sp =>
                sp.GetRequiredService<InMemoryUserClient>());
            services.AddSingleton<ISocialClient>(sp =>
                sp.GetRequiredService<InMemoryUserClient>());
            services.AddSingleton<ICommunityAccessClient>(sp =>
                sp.GetRequiredService<InMemoryUserClient>());
            services.AddSingleton<IGroupClient>(sp =>
                sp.GetRequiredService<InMemoryUserClient>());
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
                InMemoryUser.Reset();
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
