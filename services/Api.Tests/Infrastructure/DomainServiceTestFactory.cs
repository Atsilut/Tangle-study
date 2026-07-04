using Api.Domain.Friendships.Service;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Service;
using Api.Global.Config;
using Api.Global.Db;
using Api.Global.Events;
using Api.Tests.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests.Infrastructure;

internal static class DomainServiceTestFactory
{
    internal sealed record Graph(
        UserService UserService,
        FriendshipService FriendshipService,
        FriendRequestService FriendRequestService,
        UserBlockService UserBlockService,
        FakeUserRepository UserRepository,
        FakeFriendshipRepository FriendshipRepository,
        FakeFriendRequestRepository FriendRequestRepository,
        FakeUserBlockRepository UserBlockRepository,
        FakeCommunityClient CommunityClient,
        FakeLocationClient LocationClient,
        FakeGroupClient GroupClient);

    public static Graph Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var userRepository = new FakeUserRepository();
        var friendshipRepository = new FakeFriendshipRepository();
        var friendRequestRepository = new FakeFriendRequestRepository();
        var userBlockRepository = new FakeUserBlockRepository();
        var http = httpContextAccessor ?? new FakeHttpContextAccessor("1");
        var distributedCache = new FakeDistributedCache();
        var nicknameCacheService = CreateNicknameCacheService(userRepository, distributedCache);
        var eventPublisher = new NoOpEventPublisher();
        var locationClient = new FakeLocationClient();
        var communityClient = new FakeCommunityClient();
        var groupClient = new FakeGroupClient();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var mediaClient = new FakeMediaClient();
        FriendshipService friendshipService = null!;
        FriendRequestService friendRequestService = null!;

        var userService = new UserService(
            userRepository,
            db,
            communityClient,
            mediaClient,
            new FakeChatClient(),
            locationClient,
            groupClient,
            http,
            nicknameCacheService,
            eventPublisher);

        var userBlockService = new UserBlockService(
            userBlockRepository,
            new Lazy<FriendRequestService>(() => friendRequestService),
            userService,
            http);

        friendshipService = new FriendshipService(
            friendshipRepository,
            userService,
            http);

        friendRequestService = new FriendRequestService(
            friendRequestRepository,
            friendshipService,
            userService,
            userBlockService,
            db,
            http,
            NullLogger<FriendRequestService>.Instance);

        return new Graph(
            userService,
            friendshipService,
            friendRequestService,
            userBlockService,
            userRepository,
            friendshipRepository,
            friendRequestRepository,
            userBlockRepository,
            communityClient,
            locationClient,
            groupClient);
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
