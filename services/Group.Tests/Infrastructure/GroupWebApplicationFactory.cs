using Group.Client;
using Group.Db;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Group.Tests.Infrastructure;

public sealed class GroupWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "integration-test-jwt-secret-at-least-32-characters-long";
    public const string TestJwtIssuer = "Tangle";
    public const string TestJwtAudience = "TangleClient";
    public const string TestInternalServiceSecret = "test-internal-service-secret";

    private readonly string _connectionString = connectionString;
    private readonly FakeCommunityClient _fakeCommunityClient = new();
    private readonly FakeLocationClient _fakeLocationClient = new();

    public InMemoryUserClient InMemoryUser =>
        Services.GetRequiredService<InMemoryUserClient>();

    public FakeCommunityClient FakeCommunityClient => _fakeCommunityClient;
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
        builder.UseSetting("CommunityClient:BaseUrl", "http://community.test");
        builder.UseSetting("CommunityClient:InternalSecret", TestInternalServiceSecret);
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
                ["CommunityClient:BaseUrl"] = "http://community.test",
                ["CommunityClient:InternalSecret"] = TestInternalServiceSecret,
                ["LocationClient:BaseUrl"] = "http://location.test",
                ["LocationClient:InternalSecret"] = TestInternalServiceSecret,
                ["InternalAccess:Secret"] = TestInternalServiceSecret,
                ["Jwt:Secret"] = TestJwtSecret,
                ["Jwt:Issuer"] = TestJwtIssuer,
                ["Jwt:Audience"] = TestJwtAudience,
                ["Metrics:RequireScrapeSecret"] = "false",
                ["Database:ResetOnStartup"] = "false",
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

            services.RemoveService<ICommunityClient>();
            services.AddSingleton<ICommunityClient>(_fakeCommunityClient);

            services.RemoveService<ILocationClient>();
            services.AddSingleton<ILocationClient>(_fakeLocationClient);
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GroupDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<GroupDbContext>(options =>
                options.UseNpgsql(_connectionString, npgsql =>
                    npgsql.EnableRetryOnFailure(maxRetryCount: 5)));
        });
    }

    public async Task ClearAllGroupDataAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GroupDbContext>();
        await db.GroupBoards.ExecuteDeleteAsync();
        await db.GroupBlacklists.ExecuteDeleteAsync();
        await db.GroupApplications.ExecuteDeleteAsync();
        await db.GroupInvitations.ExecuteDeleteAsync();
        await db.GroupMembers.ExecuteDeleteAsync();
        await db.Groups.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
    }

}
