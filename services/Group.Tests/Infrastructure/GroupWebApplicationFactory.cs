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

    public InMemoryMonolithAccessClient MonolithAccess =>
        Services.GetRequiredService<InMemoryMonolithAccessClient>();

    public FakeCommunityClient FakeCommunityClient => _fakeCommunityClient;
    public FakeLocationClient FakeLocationClient => _fakeLocationClient;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Monolith:BaseUrl", "http://monolith.test");
        builder.UseSetting("Monolith:InternalSecret", TestInternalServiceSecret);
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
                ["Monolith:BaseUrl"] = "http://monolith.test",
                ["Monolith:InternalSecret"] = TestInternalServiceSecret,
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
            RemoveService<IMonolithAccessClient>(services);
            services.AddSingleton<InMemoryMonolithAccessClient>();
            services.AddSingleton<IMonolithAccessClient>(sp =>
                sp.GetRequiredService<InMemoryMonolithAccessClient>());

            RemoveService<ICommunityClient>(services);
            services.AddSingleton<ICommunityClient>(_fakeCommunityClient);

            RemoveService<ILocationClient>(services);
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

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var descriptor in descriptors)
            services.Remove(descriptor);
    }
}
