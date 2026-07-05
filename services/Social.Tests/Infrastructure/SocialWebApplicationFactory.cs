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
    public const string TestInternalServiceSecret = "test-internal-service-secret";
    public const string TestGatewaySecret = SocialTestAuthHelpers.TestGatewaySecret;

    private readonly string _connectionString = connectionString;

    public InMemoryUserClient InMemoryUser =>
        Services.GetRequiredService<InMemoryUserClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Users:BaseUrl", "http://users.test");
        builder.UseSetting("Users:InternalSecret", TestInternalServiceSecret);
        builder.UseSetting("InternalAccess:Secret", TestInternalServiceSecret);
        builder.UseSetting("GatewayIdentity:Secret", TestGatewaySecret);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Users:BaseUrl"] = "http://users.test",
                ["Users:InternalSecret"] = TestInternalServiceSecret,
                ["InternalAccess:Secret"] = TestInternalServiceSecret,
                ["GatewayIdentity:Secret"] = TestGatewaySecret,
                ["Metrics:RequireScrapeSecret"] = "false",
                ["Database:ResetOnStartup"] = "false",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            RemoveService<IUserClient>(services);
            services.AddSingleton<InMemoryUserClient>();
            services.AddSingleton<IUserClient>(sp =>
                sp.GetRequiredService<InMemoryUserClient>());
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
