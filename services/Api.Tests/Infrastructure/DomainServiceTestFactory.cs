using Api.Domain.Comments.Service;
using Api.Domain.Friendships.Service;
using Api.Domain.Groups.Service;
using Api.Domain.Posts.Service;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Service;
using Api.Global.Db;
using Api.Global.Events;
using Api.Tests.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Infrastructure;

internal static class DomainServiceTestFactory
{
    internal sealed record Graph(
        UserService UserService,
        PostService PostService,
        CommentService CommentService,
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
        FakePostRepository PostRepository,
        FakeCommentRepository CommentRepository,
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
        GroupBoardService GroupBoardService);

    public static Graph Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var userRepository = new FakeUserRepository();
        var postRepository = new FakePostRepository();
        var commentRepository = new FakeCommentRepository();
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
        var nicknameCacheService = new NicknameCacheService(userRepository, distributedCache);
        var eventPublisher = new NoOpEventPublisher();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        PostService postService = null!;
        CommentService commentService = null!;
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
            new Lazy<PostService>(() => postService),
            new Lazy<CommentService>(() => commentService),
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

        postService = new PostService(
            postRepository,
            db,
            new Lazy<CommentService>(() => commentService),
            http,
            userService,
            groupBoardAccessService);

        commentService = new CommentService(
            commentRepository,
            db,
            http,
            postService,
            groupBoardAccessService,
            userService);

        friendshipService = new FriendshipService(
            friendshipRepository,
            userService,
            http);

        var userBlockService = new UserBlockService(
            userBlockRepository,
            new Lazy<FriendRequestService>(() => friendRequestService),
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
            new Lazy<PostService>(() => postService));

        return new Graph(
            userService,
            postService,
            commentService,
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
            postRepository,
            commentRepository,
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
            groupBoardService);
    }
}
