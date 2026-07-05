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
    private readonly InMemoryUserClient _monolithAccess = new(new FakeHttpContextAccessor("1"));
    private readonly FakeMediaClient _fakeMediaClient = new();

    public InMemoryUserClient InMemoryUser => _monolithAccess;
    public FakeMediaClient FakeMediaClient => _fakeMediaClient;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Docker");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Users:BaseUrl", "http://users.test");
        builder.UseSetting("Users:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("GatewayIdentity:Secret", GatewayTestAuthHelpers.TestGatewaySecret);
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
                ["Users:BaseUrl"] = "http://users.test",
                ["Users:InternalSecret"] = TestInternalServiceSecret,
                ["GatewayIdentity:Secret"] = GatewayTestAuthHelpers.TestGatewaySecret,
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
            services.RemoveService<IUserClient>();
            services.RemoveService<ISocialClient>();
            services.RemoveService<IGroupClient>();
            services.AddSingleton<IUserClient>(_monolithAccess);
            services.AddSingleton<ISocialClient>(_monolithAccess);
            services.AddSingleton<IGroupClient>(_monolithAccess);

            services.RemoveService<IMediaClient>();
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
            services.RemoveService<IConnectionMultiplexer>();
            services.RemoveService<IWorkQueue>();
            services.RemoveService<IEventPublisher>();

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
}
