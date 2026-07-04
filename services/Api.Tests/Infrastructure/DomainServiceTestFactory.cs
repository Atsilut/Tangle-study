using Api.Domain.Users.Service;
using Api.Global.Config;
using Api.Global.Db;
using Api.Global.Events;
using Api.Tests.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Tests.Infrastructure;

internal static class DomainServiceTestFactory
{
    internal sealed record Graph(
        UserService UserService,
        FakeUserRepository UserRepository,
        FakeCommunityClient CommunityClient,
        FakeLocationClient LocationClient,
        FakeGroupClient GroupClient,
        FakeSocialClient SocialClient);

    public static Graph Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var userRepository = new FakeUserRepository();
        var http = httpContextAccessor ?? new FakeHttpContextAccessor("1");
        var distributedCache = new FakeDistributedCache();
        var nicknameCacheService = CreateNicknameCacheService(userRepository, distributedCache);
        var eventPublisher = new NoOpEventPublisher();
        var locationClient = new FakeLocationClient();
        var communityClient = new FakeCommunityClient();
        var groupClient = new FakeGroupClient();
        var socialClient = new FakeSocialClient();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var mediaClient = new FakeMediaClient();

        var userService = new UserService(
            userRepository,
            db,
            communityClient,
            mediaClient,
            new FakeChatClient(),
            locationClient,
            groupClient,
            socialClient,
            http,
            nicknameCacheService,
            eventPublisher);

        return new Graph(
            userService,
            userRepository,
            communityClient,
            locationClient,
            groupClient,
            socialClient);
    }

    internal static NicknameCacheService CreateNicknameCacheService(
        FakeUserRepository userRepository,
        FakeDistributedCache cache)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<RedisOptions>(options => options.InstanceName = "tangle:");
        var serviceProvider = services.BuildServiceProvider();
        return new NicknameCacheService(
            userRepository,
            cache,
            serviceProvider,
            serviceProvider.GetRequiredService<IOptions<RedisOptions>>());
    }
}
