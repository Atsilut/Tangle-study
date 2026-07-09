using Group.Client;
using Group.Db;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Group.Tests.Infrastructure;

public sealed class GroupWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    private readonly string _connectionString = connectionString;
    private readonly FakeCommunityClient _fakeCommunityClient = new();
    private readonly FakeLocationClient _fakeLocationClient = new();

    public InMemoryUserClient InMemoryUser =>
        Services.GetRequiredService<InMemoryUserClient>();

    public FakeCommunityClient FakeCommunityClient => _fakeCommunityClient;
    public FakeLocationClient FakeLocationClient => _fakeLocationClient;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var additionalSettings = new Dictionary<string, string?>
        {
            ["Database:ResetOnStartup"] = "false",
        };
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "SocialClient", "http://social.test");
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "CommunityClient", "http://community.test");
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "LocationClient", "http://location.test");

        IntegrationTestConfiguration.Apply(builder, new IntegrationTestOptions
        {
            ConnectionString = _connectionString,
            Environment = Environments.Production,
            AdditionalSettings = additionalSettings,
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
