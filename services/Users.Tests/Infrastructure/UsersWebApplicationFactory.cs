using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;
using Users.Client;
using Users.Db;
using Users.Events;
using Users.Infrastructure;

namespace Users.Tests.Infrastructure;

public sealed class UsersWebApplicationFactory(
    string connectionString,
    string redisConnectionString,
    bool metricsRequireScrapeSecret = false,
    string? metricsScrapeSecret = null) : WebApplicationFactory<Program>
{
    public const string TestInternalServiceSecret = "test-internal-service-secret";
    public const string TestGatewaySecret = UsersTestAuthHelpers.TestGatewaySecret;
    public const string TestJwtSecret = "integration-test-jwt-secret-at-least-32-characters-long";

    private readonly string _connectionString = connectionString;
    private readonly string _redisConnectionString = redisConnectionString;
    private readonly bool _metricsRequireScrapeSecret = metricsRequireScrapeSecret;
    private readonly string? _metricsScrapeSecret = metricsScrapeSecret;
    private readonly FakeMediaClient _fakeMediaClient = new();
    private readonly FakeChatClient _fakeChatClient = new();
    private readonly FakeLocationClient _fakeLocationClient = new();
    private readonly FakeCommunityClient _fakeCommunityClient = new();
    private readonly FakeGroupClient _fakeGroupClient = new();
    private readonly FakeSocialClient _fakeSocialClient = new();

    public FakeMediaClient FakeMediaClient => _fakeMediaClient;
    public FakeChatClient FakeChatClient => _fakeChatClient;
    public FakeLocationClient FakeLocationClient => _fakeLocationClient;
    public FakeCommunityClient FakeCommunityClient => _fakeCommunityClient;
    public FakeGroupClient FakeGroupClient => _fakeGroupClient;
    public FakeSocialClient FakeSocialClient => _fakeSocialClient;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Redis:ConnectionString", _redisConnectionString);
        builder.UseSetting("InternalAccess:Secret", TestInternalServiceSecret);
        builder.UseSetting("GatewayIdentity:Secret", TestGatewaySecret);
        builder.UseSetting("Jwt:Secret", TestJwtSecret);
        builder.UseSetting("Jwt:Issuer", "Tangle");
        builder.UseSetting("Jwt:Audience", "TangleClient");
        builder.UseSetting("MediaClient:BaseUrl", "http://media.test");
        builder.UseSetting("MediaClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("ChatClient:BaseUrl", "http://chat.test");
        builder.UseSetting("ChatClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("LocationClient:BaseUrl", "http://location.test");
        builder.UseSetting("LocationClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("CommunityClient:BaseUrl", "http://community.test");
        builder.UseSetting("CommunityClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("GroupClient:BaseUrl", "http://group.test");
        builder.UseSetting("GroupClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("SocialClient:BaseUrl", "http://social.test");
        builder.UseSetting("SocialClient:InternalSecret", TestInternalServiceSecret);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["InternalAccess:Secret"] = TestInternalServiceSecret,
                ["GatewayIdentity:Secret"] = TestGatewaySecret,
                ["Jwt:Secret"] = TestJwtSecret,
                ["Jwt:Issuer"] = "Tangle",
                ["Jwt:Audience"] = "TangleClient",
                ["Redis:ConnectionString"] = _redisConnectionString,
                ["Redis:InstanceName"] = "tangle:",
                ["Redis:SignalRChannelPrefix"] = "tangle:signalr:",
                ["Redis:WorkQueueStreamPrefix"] = "tangle:queue:",
                ["Metrics:RequireScrapeSecret"] = _metricsRequireScrapeSecret ? "true" : "false",
                ["Metrics:ScrapeSecret"] = _metricsScrapeSecret ?? "",
                ["Database:ResetOnStartup"] = "false",
                ["MediaClient:BaseUrl"] = "http://media.test",
                ["MediaClient:InternalSecret"] = TestInternalServiceSecret,
                ["ChatClient:BaseUrl"] = "http://chat.test",
                ["ChatClient:InternalSecret"] = TestInternalServiceSecret,
                ["LocationClient:BaseUrl"] = "http://location.test",
                ["LocationClient:InternalSecret"] = TestInternalServiceSecret,
                ["CommunityClient:BaseUrl"] = "http://community.test",
                ["CommunityClient:InternalSecret"] = TestInternalServiceSecret,
                ["GroupClient:BaseUrl"] = "http://group.test",
                ["GroupClient:InternalSecret"] = TestInternalServiceSecret,
                ["SocialClient:BaseUrl"] = "http://social.test",
                ["SocialClient:InternalSecret"] = TestInternalServiceSecret,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            ReplaceClient<IMediaClient, FakeMediaClient>(services, _fakeMediaClient);
            ReplaceClient<IChatClient, FakeChatClient>(services, _fakeChatClient);
            ReplaceClient<ILocationClient, FakeLocationClient>(services, _fakeLocationClient);
            ReplaceClient<ICommunityClient, FakeCommunityClient>(services, _fakeCommunityClient);
            ReplaceClient<IGroupClient, FakeGroupClient>(services, _fakeGroupClient);
            ReplaceClient<ISocialClient, FakeSocialClient>(services, _fakeSocialClient);

            RemoveService<IConnectionMultiplexer>(services);
            RemoveService<IEventPublisher>(services);

            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(
                    RedisServiceCollectionExtensions.ParseRedisConfiguration(_redisConnectionString)));
            services.AddSingleton<IEventPublisher, RedisEventPublisher>();
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<UsersDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<UsersDbContext>(options =>
                options.UseNpgsql(_connectionString, npgsql =>
                    npgsql.EnableRetryOnFailure(maxRetryCount: 5)));
        });
    }

    public async Task ClearAllUsersAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        await db.Users.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
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

    private static void ReplaceClient<TInterface, TFake>(IServiceCollection services, TFake fake)
        where TInterface : class
        where TFake : class, TInterface
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(TInterface)).ToList();
        foreach (var descriptor in descriptors)
            services.Remove(descriptor);
        services.AddSingleton<TInterface>(fake);
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(T)).ToList())
            services.Remove(descriptor);
    }
}
