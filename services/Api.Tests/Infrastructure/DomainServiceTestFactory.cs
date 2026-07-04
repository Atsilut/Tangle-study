using Api.Client;
using Api.Domain.Friendships.Service;
using Api.Domain.Groups.Service;
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
        GroupService GroupService,
        GroupMembershipService GroupMembershipService,
        GroupApplicationService GroupApplicationService,
        GroupInvitationService GroupInvitationService,
        GroupJoinResolutionService GroupJoinResolutionService,
        GroupJoinService GroupJoinService,
        GroupBlacklistService GroupBlacklistService,
        FakeUserRepository UserRepository,
        FakeFriendshipRepository FriendshipRepository,
        FakeFriendRequestRepository FriendRequestRepository,
        FakeUserBlockRepository UserBlockRepository,
        FakeGroupRepository GroupRepository,
        FakeGroupMemberRepository GroupMemberRepository,
        FakeGroupApplicationRepository GroupApplicationRepository,
        FakeGroupInvitationRepository GroupInvitationRepository,
        FakeGroupBlacklistRepository GroupBlacklistRepository,
        FakeGroupBoardRepository GroupBoardRepository,
        GroupBoardAccessService GroupBoardAccessService,
        GroupBoardService GroupBoardService,
        FakeCommunityClient CommunityClient,
        FakeLocationClient LocationClient);

    public static Graph Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var userRepository = new FakeUserRepository();
        var friendshipRepository = new FakeFriendshipRepository();
        var friendRequestRepository = new FakeFriendRequestRepository();
        var userBlockRepository = new FakeUserBlockRepository();
        var groupRepository = new FakeGroupRepository();
        var groupMemberRepository = new FakeGroupMemberRepository();
        var groupApplicationRepository = new FakeGroupApplicationRepository();
        var groupInvitationRepository = new FakeGroupInvitationRepository();
        var groupBlacklistRepository = new FakeGroupBlacklistRepository();
        var groupBoardRepository = new FakeGroupBoardRepository();
        var http = httpContextAccessor ?? new FakeHttpContextAccessor("1");
        var distributedCache = new FakeDistributedCache();
        var nicknameCacheService = CreateNicknameCacheService(userRepository, distributedCache);
        var eventPublisher = new NoOpEventPublisher();
        var locationClient = new FakeLocationClient();
        var communityClient = new FakeCommunityClient();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var mediaClient = new FakeMediaClient();
        FriendshipService friendshipService = null!;
        FriendRequestService friendRequestService = null!;
        GroupMembershipService groupMembershipService = null!;
        GroupJoinResolutionService groupJoinResolutionService = null!;
        GroupJoinService groupJoinService = null!;
        GroupApplicationService groupApplicationService = null!;
        GroupInvitationService groupInvitationService = null!;
        GroupBlacklistService groupBlacklistService = null!;
        GroupService groupService = null!;
        GroupBoardService groupBoardService = null!;

        var userService = new UserService(
            userRepository,
            db,
            communityClient,
            mediaClient,
            new FakeChatClient(),
            locationClient,
            new Lazy<GroupMembershipService>(() => groupMembershipService),
            http,
            nicknameCacheService,
            eventPublisher);

        groupMembershipService = new GroupMembershipService(
            groupMemberRepository,
            new Lazy<GroupService>(() => groupService),
            userService,
            http);

        var groupBoardAccessService = new GroupBoardAccessService(
            groupBoardRepository,
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            http);

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

        groupBlacklistService = new GroupBlacklistService(
            groupBlacklistRepository,
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            new Lazy<GroupJoinResolutionService>(() => groupJoinResolutionService),
            userService,
            db,
            http);

        groupApplicationService = new GroupApplicationService(
            groupApplicationRepository,
            new Lazy<GroupInvitationService>(() => groupInvitationService),
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            new Lazy<GroupJoinResolutionService>(() => groupJoinResolutionService),
            groupBlacklistService,
            userService,
            db,
            http);

        groupInvitationService = new GroupInvitationService(
            groupInvitationRepository,
            new Lazy<GroupApplicationService>(() => groupApplicationService),
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            new Lazy<GroupJoinResolutionService>(() => groupJoinResolutionService),
            groupBlacklistService,
            userBlockService,
            userService,
            db,
            http);

        groupJoinResolutionService = new GroupJoinResolutionService(
            groupMembershipService,
            groupInvitationService,
            groupApplicationService,
            groupBlacklistService,
            db);

        groupJoinService = new GroupJoinService(
            new Lazy<GroupService>(() => groupService),
            groupInvitationService,
            groupMembershipService,
            groupJoinResolutionService,
            groupBlacklistService,
            http);

        groupBoardService = new GroupBoardService(
            groupBoardRepository,
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            groupBoardAccessService,
            http);

        groupService = new GroupService(
            groupRepository,
            groupMembershipService,
            userService,
            db,
            http,
            new Lazy<GroupInvitationService>(() => groupInvitationService),
            new Lazy<GroupApplicationService>(() => groupApplicationService),
            new Lazy<GroupBlacklistService>(() => groupBlacklistService),
            new Lazy<GroupBoardService>(() => groupBoardService),
            communityClient,
            locationClient);

        return new Graph(
            userService,
            friendshipService,
            friendRequestService,
            userBlockService,
            groupService,
            groupMembershipService,
            groupApplicationService,
            groupInvitationService,
            groupJoinResolutionService,
            groupJoinService,
            groupBlacklistService,
            userRepository,
            friendshipRepository,
            friendRequestRepository,
            userBlockRepository,
            groupRepository,
            groupMemberRepository,
            groupApplicationRepository,
            groupInvitationRepository,
            groupBlacklistRepository,
            groupBoardRepository,
            groupBoardAccessService,
            groupBoardService,
            communityClient,
            locationClient);
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
