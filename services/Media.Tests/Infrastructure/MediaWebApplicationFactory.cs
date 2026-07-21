using Media.Client;
using Media.Db;
using Media.Infrastructure;
using Media.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;
using Tangle.AspNetCore.Queue;

namespace Media.Tests.Infrastructure;

public sealed class MediaWebApplicationFactory(
    string connectionString,
    string redisConnectionString) : WebApplicationFactory<Program>
{
    private readonly string _connectionString = connectionString;
    private readonly string _redisConnectionString = redisConnectionString;
    private readonly FakeMediaStorage _fakeStorage = new();

    public FakeMediaStorage FakeStorage => _fakeStorage;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var additionalSettings = new Dictionary<string, string?>
        {
            ["Media:ConnectionString"] = IntegrationTestConstants.TestBlobConnectionString,
            ["Media:ContainerName"] = "tangle-media",
            ["Media:WorkerCallbackSecret"] = IntegrationTestConstants.TestWorkerCallbackSecret,
            ["CommunityClient:BaseUrl"] = "http://community.test",
            ["Outbox:PollIntervalMilliseconds"] = "200",
            ["Outbox:BatchSize"] = "50",
            ["Outbox:MaxAttempts"] = "10",
        };
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "ChatClient", "http://chat.test");

        IntegrationTestConfiguration.Apply(builder, new IntegrationTestOptions
        {
            ConnectionString = _connectionString,
            RedisConnectionString = _redisConnectionString,
            Environment = Environments.Production,
            AdditionalSettings = additionalSettings,
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveService<IMediaStorage>();
            services.AddSingleton<IMediaStorage>(_fakeStorage);

            services.RemoveService<IUserClient>();
            services.AddSingleton<IUserClient, AllowAllUserClient>();

            services.RemoveService<ICommunityAccessClient>();
            services.AddSingleton<ICommunityAccessClient, AllowAllCommunityAccessClient>();

            services.RemoveService<IChatAccessClient>();
            services.AddSingleton<IChatAccessClient, AllowAllChatAccessClient>();
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<MediaDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<MediaDbContext>(options =>
                options.UseNpgsql(_connectionString, npgsql =>
                    npgsql.EnableRetryOnFailure(maxRetryCount: 5)));
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveService<IConnectionMultiplexer>();
            services.RemoveService<IWorkQueue>();
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(
                    RedisServiceCollectionExtensions.ParseRedisConfiguration(_redisConnectionString)));
            services.AddSingleton<IWorkQueue, RedisStreamWorkQueue>();
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
}
