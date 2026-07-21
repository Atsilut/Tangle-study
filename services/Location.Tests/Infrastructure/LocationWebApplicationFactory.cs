using Location.Client;
using Location.Db;
using Location.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;
using Tangle.AspNetCore.Queue;

namespace Location.Tests.Infrastructure;

public sealed class LocationWebApplicationFactory(string connectionString, string redisConnectionString)
    : WebApplicationFactory<Program>
{
    public const string TestWorkerCallbackSecret = "test-location-worker-secret";

    private readonly string _connectionString = connectionString;
    private readonly string _redisConnectionString = redisConnectionString;
    private readonly FakeSocialClient _fakeSocialClient = new();

    public InMemoryUserClient InMemoryUser =>
        Services.GetRequiredService<InMemoryUserClient>();

    public FakeSocialClient FakeSocial => _fakeSocialClient;

    public FakeCommunityAccessClient FakeCommunity =>
        Services.GetRequiredService<FakeCommunityAccessClient>();

    public FakeGroupClient FakeGroup =>
        Services.GetRequiredService<FakeGroupClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var additionalSettings = new Dictionary<string, string?>
        {
            ["WorkerCallback:Secret"] = TestWorkerCallbackSecret,
            ["Places:Enabled"] = "false",
            ["Outbox:PollIntervalMilliseconds"] = "200",
            ["Outbox:BatchSize"] = "50",
            ["Outbox:MaxAttempts"] = "10",
        };
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "SocialClient", "http://social.test");
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "GroupClient", "http://group.test");
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "CommunityClient", "http://community.test");

        IntegrationTestConfiguration.Apply(builder, new IntegrationTestOptions
        {
            ConnectionString = _connectionString,
            RedisConnectionString = _redisConnectionString,
            Environment = Environments.Production,
            AdditionalSettings = additionalSettings,
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveService<IUserClient>();
            services.RemoveService<ISocialClient>();
            services.RemoveService<ICommunityAccessClient>();
            services.RemoveService<IGroupClient>();

            services.AddSingleton<InMemoryUserClient>();
            services.AddSingleton<IUserClient>(sp => sp.GetRequiredService<InMemoryUserClient>());
            services.AddSingleton(_fakeSocialClient);
            services.AddSingleton<ISocialClient>(_fakeSocialClient);
            services.AddSingleton<FakeCommunityAccessClient>(sp =>
                new FakeCommunityAccessClient(sp.GetRequiredService<IHttpContextAccessor>()));
            services.AddSingleton<ICommunityAccessClient>(sp =>
                sp.GetRequiredService<FakeCommunityAccessClient>());
            services.AddSingleton<FakeGroupClient>(sp =>
                new FakeGroupClient(sp.GetRequiredService<InMemoryUserClient>()));
            services.AddSingleton<IGroupClient>(sp => sp.GetRequiredService<FakeGroupClient>());
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
            services.RemoveService<IConnectionMultiplexer>();
            services.RemoveService<IWorkQueue>();
            services.RemoveService<IDistributedCache>();

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
                FakeSocial.Reset();
                FakeCommunity.Reset();
                FakeGroup.Reset();
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
}
