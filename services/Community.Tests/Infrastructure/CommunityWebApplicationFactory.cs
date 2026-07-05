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

    public InMemoryUserClient InMemoryUser =>
        Services.GetRequiredService<InMemoryUserClient>();

    public InMemoryGroupClient GroupAccess =>
        Services.GetRequiredService<InMemoryGroupClient>();

    public FakeMediaClient FakeMediaClient => _fakeMediaClient;
    public FakeLocationClient FakeLocationClient => _fakeLocationClient;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
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
                ["Users:BaseUrl"] = "http://users.test",
                ["Users:InternalSecret"] = TestInternalServiceSecret,
                ["GatewayIdentity:Secret"] = GatewayTestAuthHelpers.TestGatewaySecret,
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
            services.RemoveService<IUserClient>();
            services.RemoveService<ISocialClient>();
            services.AddSingleton<InMemoryUserClient>();
            services.AddSingleton<IUserClient>(sp =>
                sp.GetRequiredService<InMemoryUserClient>());
            services.AddSingleton<ISocialClient>(sp =>
                sp.GetRequiredService<InMemoryUserClient>());

            services.RemoveService<IGroupClient>();
            services.AddSingleton<InMemoryGroupClient>();
            services.AddSingleton<IGroupClient>(sp =>
                sp.GetRequiredService<InMemoryGroupClient>());

            services.RemoveService<IMediaClient>();
            services.AddSingleton<IMediaClient>(_fakeMediaClient);

            services.RemoveService<ILocationClient>();
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

}
