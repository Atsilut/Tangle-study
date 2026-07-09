using Community.Client;
using Community.Db;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Community.Tests.Infrastructure;

public sealed class CommunityWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
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
        var additionalSettings = new Dictionary<string, string?>();
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "SocialClient", "http://social.test");
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "GroupClient", "http://group.test");
        IntegrationTestConfiguration.AddDownstreamClient(additionalSettings, "MediaClient", "http://media.test");
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
