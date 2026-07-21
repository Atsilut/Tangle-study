using Chat.Client;
using Chat.Db;
using Chat.Events;
using Chat.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;
using Tangle.AspNetCore.Queue;

namespace Chat.Tests.Infrastructure;

public sealed class ChatWebApplicationFactory(
    string connectionString,
    string redisConnectionString) : WebApplicationFactory<Program>
{
    private readonly string _connectionString = connectionString;
    private readonly string _redisConnectionString = redisConnectionString;
    private readonly FakeMediaClient _fakeMediaClient = new();

    public InMemoryUserClient InMemoryUser =>
        Services.GetRequiredService<InMemoryUserClient>();

    public FakeMediaClient FakeMediaClient => _fakeMediaClient;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var additionalSettings = new Dictionary<string, string?>
        {
            ["Outbox:PollIntervalMilliseconds"] = "200",
            ["Outbox:BatchSize"] = "50",
            ["Outbox:MaxAttempts"] = "10",
        };
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "SocialClient", "http://social.test");
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "GroupClient", "http://group.test");
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "MediaClient", "http://media.test");

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
            services.RemoveService<IGroupClient>();
            services.AddSingleton<InMemoryUserClient>(sp =>
                new InMemoryUserClient(sp.GetRequiredService<IHttpContextAccessor>()));
            services.AddSingleton<IUserClient>(sp => sp.GetRequiredService<InMemoryUserClient>());
            services.AddSingleton<ISocialClient>(sp => sp.GetRequiredService<InMemoryUserClient>());
            services.AddSingleton<IGroupClient>(sp => sp.GetRequiredService<InMemoryUserClient>());

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
            try
            {
                if (Server is not null)
                    InMemoryUser.Reset();
            }
            catch (InvalidOperationException)
            {
                // Factory was never started.
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
