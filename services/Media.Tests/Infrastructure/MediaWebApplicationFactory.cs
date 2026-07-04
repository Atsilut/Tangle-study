using Media.Client;
using Media.Db;
using Media.Infrastructure;
using Media.Queue;
using Media.Security;
using Media.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;

namespace Media.Tests.Infrastructure;

public sealed class MediaWebApplicationFactory(
    string connectionString,
    string redisConnectionString) : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "integration-test-jwt-secret-at-least-32-characters-long";
    public const string TestJwtIssuer = "Tangle";
    public const string TestJwtAudience = "TangleClient";
    public const string TestWorkerCallbackSecret = "test-media-worker-secret";
    public const string TestInternalServiceSecret = "test-internal-service-secret";

    private const string TestBlobConnectionString =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    private readonly string _connectionString = connectionString;
    private readonly string _redisConnectionString = redisConnectionString;
    private readonly FakeMediaStorage _fakeStorage = new();

    public FakeMediaStorage FakeStorage => _fakeStorage;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Media:ConnectionString", TestBlobConnectionString);
        builder.UseSetting("Media:WorkerCallbackSecret", TestWorkerCallbackSecret);
        builder.UseSetting("Media:InternalServiceSecret", TestInternalServiceSecret);
        builder.UseSetting("Monolith:BaseUrl", "http://monolith.test");
        builder.UseSetting("Monolith:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("Redis:ConnectionString", _redisConnectionString);
        builder.UseSetting("ChatClient:BaseUrl", "http://chat.test");
        builder.UseSetting("CommunityClient:BaseUrl", "http://community.test");
        builder.UseSetting("ChatClient:InternalSecret", TestInternalServiceSecret);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Redis:ConnectionString"] = _redisConnectionString,
                ["Redis:WorkQueueStreamPrefix"] = "tangle:queue:",
                ["Media:ConnectionString"] = TestBlobConnectionString,
                ["Media:ContainerName"] = "tangle-media",
                ["Media:WorkerCallbackSecret"] = TestWorkerCallbackSecret,
                ["Media:InternalServiceSecret"] = TestInternalServiceSecret,
                ["Monolith:BaseUrl"] = "http://monolith.test",
                ["Monolith:InternalSecret"] = TestInternalServiceSecret,
                ["ChatClient:BaseUrl"] = "http://chat.test",
                ["CommunityClient:BaseUrl"] = "http://community.test",
                ["ChatClient:InternalSecret"] = TestInternalServiceSecret,
                ["Jwt:Secret"] = TestJwtSecret,
                ["Jwt:Issuer"] = TestJwtIssuer,
                ["Jwt:Audience"] = TestJwtAudience,
                ["Metrics:RequireScrapeSecret"] = "false",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            RemoveService<IMediaStorage>(services);
            services.AddSingleton<IMediaStorage>(_fakeStorage);

            RemoveService<IMonolithAccessClient>(services);
            services.AddSingleton<IMonolithAccessClient, AllowAllMonolithAccessClient>();

            RemoveService<ICommunityAccessClient>(services);
            services.AddSingleton<ICommunityAccessClient, AllowAllCommunityAccessClient>();

            RemoveService<IChatAccessClient>(services);
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
            RemoveService<IConnectionMultiplexer>(services);
            RemoveService<IWorkQueue>(services);
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

    private static void RemoveService<T>(IServiceCollection services)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(T)).ToList())
            services.Remove(descriptor);
    }
}
