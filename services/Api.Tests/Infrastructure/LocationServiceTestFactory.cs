using Api.Domain.Comments.Service;
using Api.Domain.Friendships.Service;
using Api.Domain.Groups.Service;
using Api.Domain.Location.Repository;
using Api.Domain.Location.Service;
using Api.Client;
using Api.Domain.Posts.Service;
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

internal static class LocationServiceTestFactory
{
    internal sealed record Graph(
        MapPinService MapPinService,
        LocationClusterService LocationClusterService,
        LocationAccessService LocationAccessService,
        PostService PostService,
        FakeUserRepository UserRepository,
        IMapPinRepository MapPinRepository);

    public static Graph Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var userRepository = new FakeUserRepository();
        var http = httpContextAccessor ?? new FakeHttpContextAccessor("1");
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var mapPinRepository = new MapPinRepository(db);
        var distributedCache = new FakeDistributedCache();
        var nicknameCacheService = DomainServiceTestFactory.CreateNicknameCacheService(userRepository, distributedCache);
        var eventPublisher = new NoOpEventPublisher();

        PostService postService = null!;
        CommentService commentService = null!;
        var mediaClient = new FakeMediaClient();
        FriendshipService friendshipService = null!;
        FriendRequestService friendRequestService = null!;
        GroupMembershipService groupMembershipService = null!;
        GroupService groupService = null!;
        GroupBoardAccessService groupBoardAccessService = null!;

        MapPinService mapPinService = null!;

        var userService = new UserService(
            userRepository,
            db,
            new Lazy<PostService>(() => postService),
            new Lazy<CommentService>(() => commentService),
            mediaClient,
            new FakeChatClient(),
            new Lazy<GroupMembershipService>(() => groupMembershipService),
            new Lazy<MapPinService>(() => mapPinService),
            new Lazy<LocationSessionService>(() => null!),
            http,
            nicknameCacheService,
            eventPublisher);

        groupMembershipService = new GroupMembershipService(
            new FakeGroupMemberRepository(),
            new Lazy<GroupService>(() => groupService),
            userService,
            http);

        groupBoardAccessService = new GroupBoardAccessService(
            new FakeGroupBoardRepository(),
            new Lazy<GroupService>(() => groupService),
            groupMembershipService,
            http);

        var userBlockService = new UserBlockService(
            new FakeUserBlockRepository(),
            new Lazy<FriendRequestService>(() => friendRequestService),
            userService,
            http);

        postService = new PostService(
            new FakePostRepository(),
            db,
            new Lazy<CommentService>(() => commentService),
            mediaClient,
            new Lazy<MapPinService>(() => mapPinService),
            http,
            userService,
            userBlockService,
            groupBoardAccessService);

        commentService = new CommentService(
            new FakeCommentRepository(),
            db,
            http,
            postService,
            groupBoardAccessService,
            userService,
            userBlockService,
            mediaClient);

        friendshipService = new FriendshipService(new FakeFriendshipRepository(), userService, http);

        friendRequestService = new FriendRequestService(
            new FakeFriendRequestRepository(),
            friendshipService,
            userService,
            userBlockService,
            db,
            http,
            NullLogger<FriendRequestService>.Instance);

        groupService = new GroupService(
            new FakeGroupRepository(),
            groupMembershipService,
            userService,
            db,
            http,
            new Lazy<GroupInvitationService>(() => null!),
            new Lazy<GroupApplicationService>(() => null!),
            new Lazy<GroupBlacklistService>(() => null!),
            new Lazy<GroupBoardService>(() => null!),
            new Lazy<PostService>(() => postService));

        var locationAccessService = new LocationAccessService(postService, userBlockService, http);

        var locationClusterService = new LocationClusterService(
            mapPinRepository,
            locationAccessService,
            distributedCache,
            new FakeWorkQueue());

        mapPinService = new MapPinService(
            mapPinRepository,
            userService,
            locationAccessService,
            new Lazy<LocationClusterService>(() => locationClusterService),
            http);

        return new Graph(
            mapPinService,
            locationClusterService,
            locationAccessService,
            postService,
            userRepository,
            mapPinRepository);
    }
}
