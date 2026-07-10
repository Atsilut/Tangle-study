using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Social.Client;
using Social.Db;

namespace Social.Tests.Infrastructure;

public sealed class SocialWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    private readonly string _connectionString = connectionString;

    public InMemoryUserClient InMemoryUser =>
        Services.GetRequiredService<InMemoryUserClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        IntegrationTestConfiguration.Apply(builder, new IntegrationTestOptions
        {
            ConnectionString = _connectionString,
            Environment = Environments.Production,
            AdditionalSettings = new Dictionary<string, string?>
            {
                ["Database:ResetOnStartup"] = "false",
            },
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveService<IUserClient>();
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
}
