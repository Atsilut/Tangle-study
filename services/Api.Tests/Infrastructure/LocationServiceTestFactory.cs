using Api.Domain.Chat.Service;
using Api.Domain.Comments.Service;
using Api.Domain.Friendships.Service;
using Api.Domain.Groups.Service;
using Api.Domain.Location.Repository;
using Api.Domain.Location.Service;
using Api.Domain.Media;
using Api.Domain.Media.Repository;
using Api.Domain.Media.Service;
using Api.Domain.Media.Storage;
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
        LocationAccessService LocationAccessService,
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
        MediaService mediaService = null!;
        FriendshipService friendshipService = null!;
        FriendRequestService friendRequestService = null!;
        GroupMembershipService groupMembershipService = null!;
        GroupService groupService = null!;
        GroupBoardAccessService groupBoardAccessService = null!;

        var mediaOptions = Options.Create(new MediaOptions
        {
            Enabled = true,
            IngressMultiplier = 3,
            Post = new MediaContextLimitOptions
            {
                VideoPerFileBytes = 2L * 1024 * 1024 * 1024,
                VideoTotalBytes = 10L * 1024 * 1024 * 1024,
                ImagePerFileBytes = 150L * 1024 * 1024,
                ImageTotalBytes = 3L * 1024 * 1024 * 1024,
            },
            Comment = new MediaContextLimitOptions(),
            ChatMessage = new MediaContextLimitOptions(),
        });

        MapPinService mapPinService = null!;

        var userService = new UserService(
            userRepository,
            db,
            new Lazy<PostService>(() => postService),
            new Lazy<CommentService>(() => commentService),
            new Lazy<MediaService>(() => mediaService),
            new Lazy<ChatMessageService>(() => null!),
            new Lazy<ChatRoomService>(() => null!),
            new Lazy<GroupMembershipService>(() => groupMembershipService),
            new Lazy<MapPinService>(() => mapPinService),
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

        mediaService = new MediaService(
            new MediaAssetRepository(db),
            CreateMediaStorageProvider(new FakeMediaStorage()),
            new MediaLimitPolicy(mediaOptions),
            userService,
            new Lazy<ChatMessageService>(() => null!),
            new Lazy<PostService>(() => postService),
            new Lazy<CommentService>(() => commentService),
            groupBoardAccessService,
            new FakeWorkQueue(),
            mediaOptions,
            http);

        postService = new PostService(
            new FakePostRepository(),
            db,
            new Lazy<CommentService>(() => commentService),
            new Lazy<MediaService>(() => mediaService),
            new Lazy<MapPinService>(() => mapPinService),
            http,
            userService,
            groupBoardAccessService);

        commentService = new CommentService(
            new FakeCommentRepository(),
            db,
            http,
            postService,
            groupBoardAccessService,
            userService,
            new Lazy<MediaService>(() => mediaService));

        friendshipService = new FriendshipService(new FakeFriendshipRepository(), userService, http);

        var userBlockService = new UserBlockService(
            new FakeUserBlockRepository(),
            new Lazy<FriendRequestService>(() => friendRequestService),
            userService,
            http);

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

        mapPinService = new MapPinService(mapPinRepository, userService, locationAccessService, http);

        return new Graph(mapPinService, locationAccessService, userRepository, mapPinRepository);
    }

    private static ServiceProvider CreateMediaStorageProvider(FakeMediaStorage storage) =>
        new ServiceCollection()
            .AddSingleton<IMediaStorage>(storage)
            .BuildServiceProvider();
}
