using Community.Client;
using Community.Db;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Community.Tests.Infrastructure;

public sealed class CommunityWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "integration-test-jwt-secret-at-least-32-characters-long";
    public const string TestJwtIssuer = "Tangle";
    public const string TestJwtAudience = "TangleClient";
    public const string TestInternalServiceSecret = "test-internal-service-secret";

    private readonly string _connectionString = connectionString;
    private readonly FakeMediaClient _fakeMediaClient = new();
    private readonly FakeLocationClient _fakeLocationClient = new();

    public InMemoryMonolithAccessClient MonolithAccess =>
        Services.GetRequiredService<InMemoryMonolithAccessClient>();

    public InMemoryGroupClient GroupAccess =>
        Services.GetRequiredService<InMemoryGroupClient>();

    public FakeMediaClient FakeMediaClient => _fakeMediaClient;
    public FakeLocationClient FakeLocationClient => _fakeLocationClient;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Monolith:BaseUrl", "http://monolith.test");
        builder.UseSetting("Monolith:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("SocialClient:BaseUrl", "http://social.test");
        builder.UseSetting("SocialClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("GroupClient:BaseUrl", "http://group.test");
        builder.UseSetting("GroupClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("MediaClient:BaseUrl", "http://media.test");
        builder.UseSetting("MediaClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("LocationClient:BaseUrl", "http://location.test");
        builder.UseSetting("LocationClient:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("InternalAccess:Secret", TestInternalServiceSecret);
        builder.UseSetting("Jwt:Secret", TestJwtSecret);
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtAudience);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Monolith:BaseUrl"] = "http://monolith.test",
                ["Monolith:InternalSecret"] = TestInternalServiceSecret,
                ["SocialClient:BaseUrl"] = "http://social.test",
                ["SocialClient:InternalSecret"] = TestInternalServiceSecret,
                ["GroupClient:BaseUrl"] = "http://group.test",
                ["GroupClient:InternalSecret"] = TestInternalServiceSecret,
                ["MediaClient:BaseUrl"] = "http://media.test",
                ["MediaClient:InternalSecret"] = TestInternalServiceSecret,
                ["LocationClient:BaseUrl"] = "http://location.test",
                ["LocationClient:InternalSecret"] = TestInternalServiceSecret,
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
            services.AddSingleton<InMemoryMonolithAccessClient>();
            services.AddSingleton<IMonolithAccessClient>(sp =>
                sp.GetRequiredService<InMemoryMonolithAccessClient>());
            services.AddSingleton<ISocialClient>(sp =>
                sp.GetRequiredService<InMemoryMonolithAccessClient>());

            RemoveService<IGroupClient>(services);
            services.AddSingleton<InMemoryGroupClient>();
            services.AddSingleton<IGroupClient>(sp =>
                sp.GetRequiredService<InMemoryGroupClient>());

            RemoveService<IMediaClient>(services);
            services.AddSingleton<IMediaClient>(_fakeMediaClient);

            RemoveService<ILocationClient>(services);
            services.AddSingleton<ILocationClient>(_fakeLocationClient);
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CommunityDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<CommunityDbContext>(options =>
                options.UseNpgsql(_connectionString, npgsql =>
                    npgsql.EnableRetryOnFailure(maxRetryCount: 5)));
        });
    }

    public async Task ClearAllCommunityDataAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommunityDbContext>();
        await db.Comments.ExecuteDeleteAsync();
        await db.Posts.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var descriptor in descriptors)
            services.Remove(descriptor);
    }
}
