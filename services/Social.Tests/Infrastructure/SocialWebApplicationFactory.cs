using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Social.Client;
using Social.Db;

namespace Social.Tests.Infrastructure;

public sealed class SocialWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "integration-test-jwt-secret-at-least-32-characters-long";
    public const string TestJwtIssuer = "Tangle";
    public const string TestJwtAudience = "TangleClient";
    public const string TestInternalServiceSecret = "test-internal-service-secret";

    private readonly string _connectionString = connectionString;

    public InMemoryMonolithAccessClient MonolithAccess =>
        Services.GetRequiredService<InMemoryMonolithAccessClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Monolith:BaseUrl", "http://monolith.test");
        builder.UseSetting("Monolith:InternalSecret", TestInternalServiceSecret);
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
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<SocialDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<SocialDbContext>(options =>
                options.UseNpgsql(_connectionString, npgsql =>
                    npgsql.EnableRetryOnFailure(maxRetryCount: 5)));
        });
    }

    public async Task ClearAllSocialDataAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SocialDbContext>();
        await db.FriendRequests.ExecuteDeleteAsync();
        await db.Friendships.ExecuteDeleteAsync();
        await db.UserBlocks.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var descriptor in descriptors)
            services.Remove(descriptor);
    }
}
