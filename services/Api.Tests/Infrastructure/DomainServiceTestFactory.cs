using Api.Domain.Comments.Service;
using Api.Domain.Friendships.Service;
using Api.Domain.Groups.Service;
using Api.Domain.Posts.Service;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Service;
using Api.Global.Db;
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

        var userService = new UserService(
            userRepository,
            db,
            new Lazy<PostService>(() => postService),
            new Lazy<CommentService>(() => commentService),
            http);

        groupMembershipService = new GroupMembershipService(
            groupMemberRepository,
            groupRepository,
            userService,
            http);

        var groupBoardAccessService = new GroupBoardAccessService(
            groupRepository,
            groupBoardRepository,
            groupMemberRepository,
            http);

        groupJoinResolutionService = new GroupJoinResolutionService(
            groupInvitationRepository,
            groupApplicationRepository,
            groupBlacklistRepository,
            groupMembershipService,
            db);

        groupBlacklistService = new GroupBlacklistService(
            groupBlacklistRepository,
            groupRepository,
            groupMemberRepository,
            groupMembershipService,
            groupJoinResolutionService,
            userService,
            db,
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

        groupApplicationService = new GroupApplicationService(
            groupApplicationRepository,
            groupInvitationRepository,
            groupRepository,
            groupMembershipService,
            groupJoinResolutionService,
            groupBlacklistService,
            userService,
            db,
            http);

        groupJoinService = new GroupJoinService(
            groupRepository,
            groupInvitationRepository,
            groupMembershipService,
            groupJoinResolutionService,
            groupBlacklistService,
            http);

        groupInvitationService = new GroupInvitationService(
            groupInvitationRepository,
            groupApplicationRepository,
            groupRepository,
            groupMembershipService,
            groupJoinResolutionService,
            groupBlacklistService,
            userBlockService,
            userService,
            db,
            http);

        groupService = new GroupService(
            groupRepository,
            groupMemberRepository,
            groupBlacklistRepository,
            groupBoardRepository,
            postRepository,
            groupMembershipService,
            userService,
            db,
            http,
            new Lazy<GroupInvitationService>(() => groupInvitationService),
            new Lazy<GroupApplicationService>(() => groupApplicationService));

        var groupBoardService = new GroupBoardService(
            groupBoardRepository,
            groupRepository,
            groupMembershipService,
            groupBoardAccessService,
            http);

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
