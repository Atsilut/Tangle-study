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
        FakeUserRepository UserRepository,
        FakePostRepository PostRepository,
        FakeCommentRepository CommentRepository,
        FakeFriendshipRepository FriendshipRepository,
        FakeFriendRequestRepository FriendRequestRepository,
        FakeUserBlockRepository UserBlockRepository,
        FakeGroupRepository GroupRepository,
        FakeGroupMemberRepository GroupMemberRepository);

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
        var http = httpContextAccessor ?? new FakeHttpContextAccessor("1");
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        PostService postService = null!;
        CommentService commentService = null!;
        FriendshipService friendshipService = null!;
        FriendRequestService friendRequestService = null!;
        GroupMembershipService groupMembershipService = null!;
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

        postService = new PostService(
            postRepository,
            db,
            new Lazy<CommentService>(() => commentService),
            http,
            userService);

        commentService = new CommentService(
            commentRepository,
            db,
            http,
            postService,
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

        groupService = new GroupService(
            groupRepository,
            groupMemberRepository,
            groupMembershipService,
            userService,
            db,
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
            userRepository,
            postRepository,
            commentRepository,
            friendshipRepository,
            friendRequestRepository,
            userBlockRepository,
            groupRepository,
            groupMemberRepository);
    }
}
