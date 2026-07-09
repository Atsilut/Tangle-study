using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;
using Users.Client;
using Users.Db;
using Users.Infrastructure;

namespace Users.Tests.Infrastructure;

public sealed class UsersWebApplicationFactory(
    string connectionString,
    string redisConnectionString,
    bool metricsRequireScrapeSecret = false,
    string? metricsScrapeSecret = null) : WebApplicationFactory<UsersProgram>
{
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
        var additionalSettings = new Dictionary<string, string?>
        {
            ["Redis:InstanceName"] = "tangle:",
            ["Metrics:RequireScrapeSecret"] = _metricsRequireScrapeSecret ? "true" : "false",
            ["Metrics:ScrapeSecret"] = _metricsScrapeSecret ?? "",
            ["Database:ResetOnStartup"] = "false",
        };
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "MediaClient", "http://media.test");
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "ChatClient", "http://chat.test");
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "LocationClient", "http://location.test");
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "CommunityClient", "http://community.test");
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "GroupClient", "http://group.test");
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "SocialClient", "http://social.test");

        IntegrationTestConfiguration.Apply(builder, new IntegrationTestOptions
        {
            ConnectionString = _connectionString,
            RedisConnectionString = _redisConnectionString,
            Environment = Environments.Production,
            AdditionalSettings = additionalSettings,
        });

        builder.ConfigureTestServices(services =>
        {
            ReplaceClient<IMediaClient, FakeMediaClient>(services, _fakeMediaClient);
            ReplaceClient<IChatClient, FakeChatClient>(services, _fakeChatClient);
            ReplaceClient<ILocationClient, FakeLocationClient>(services, _fakeLocationClient);
            ReplaceClient<ICommunityClient, FakeCommunityClient>(services, _fakeCommunityClient);
            ReplaceClient<IGroupClient, FakeGroupClient>(services, _fakeGroupClient);
            ReplaceClient<ISocialClient, FakeSocialClient>(services, _fakeSocialClient);

            services.RemoveService<IConnectionMultiplexer>();
            services.RemoveService<IEventPublisher>();

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
}
