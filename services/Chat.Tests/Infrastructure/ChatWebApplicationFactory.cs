using Chat.Client;
using Chat.Config;
using Chat.Db;
using Chat.Events;
using Chat.Infrastructure;
using Chat.Queue;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;

namespace Chat.Tests.Infrastructure;

public sealed class ChatWebApplicationFactory(
    string connectionString,
    string redisConnectionString) : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "integration-test-jwt-secret-at-least-32-characters-long";
    public const string TestJwtIssuer = "Tangle";
    public const string TestJwtAudience = "TangleClient";
    public const string TestInternalServiceSecret = "test-internal-service-secret";

    private readonly string _connectionString = connectionString;
    private readonly string _redisConnectionString = redisConnectionString;
    private readonly InMemoryMonolithAccessClient _monolithAccess = new(new FakeHttpContextAccessor("1"));
    private readonly FakeMediaClient _fakeMediaClient = new();

    public InMemoryMonolithAccessClient MonolithAccess => _monolithAccess;
    public FakeMediaClient FakeMediaClient => _fakeMediaClient;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Docker");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Monolith:BaseUrl", "http://monolith.test");
        builder.UseSetting("Monolith:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("SocialClient:BaseUrl", "http://social.test");
        builder.UseSetting("SocialClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("GroupClient:BaseUrl", "http://group.test");
        builder.UseSetting("GroupClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("MediaClient:BaseUrl", "http://media.test");
        builder.UseSetting("MediaClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("InternalAccess:Secret", TestInternalServiceSecret);
        builder.UseSetting("Redis:ConnectionString", _redisConnectionString);
        builder.UseSetting("Jwt:Secret", TestJwtSecret);
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtAudience);

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
                ["SocialClient:BaseUrl"] = "http://social.test",
                ["SocialClient:InternalSecret"] = TestInternalServiceSecret,
                ["GroupClient:BaseUrl"] = "http://group.test",
                ["GroupClient:InternalSecret"] = TestInternalServiceSecret,
                ["MediaClient:BaseUrl"] = "http://media.test",
                ["MediaClient:InternalSecret"] = TestInternalServiceSecret,
                ["InternalAccess:Secret"] = TestInternalServiceSecret,
                ["Jwt:Secret"] = TestJwtSecret,
                ["Jwt:Issuer"] = TestJwtIssuer,
                ["Jwt:Audience"] = TestJwtAudience,
                ["Metrics:RequireScrapeSecret"] = "false",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            RemoveService<IMonolithAccessClient>(services);
            RemoveService<ISocialClient>(services);
            RemoveService<IGroupClient>(services);
            services.AddSingleton<IMonolithAccessClient>(_monolithAccess);
            services.AddSingleton<ISocialClient>(_monolithAccess);
            services.AddSingleton<IGroupClient>(_monolithAccess);

            RemoveService<IMediaClient>(services);
            services.AddSingleton<IMediaClient>(_fakeMediaClient);
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ChatDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<ChatDbContext>(options =>
                options.UseNpgsql(_connectionString, npgsql =>
                    npgsql.EnableRetryOnFailure(maxRetryCount: 5)));
        });

        builder.ConfigureTestServices(services =>
        {
            RemoveService<IConnectionMultiplexer>(services);
            RemoveService<IWorkQueue>(services);
            RemoveService<IEventPublisher>(services);

            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(
                    RedisServiceCollectionExtensions.ParseRedisConfiguration(_redisConnectionString)));
            services.AddSingleton<IWorkQueue, RedisStreamWorkQueue>();
            services.AddSingleton<IEventPublisher, RedisEventPublisher>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monolithAccess.Reset();
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
