using Api.Domain.Comments.Service;
using Api.Domain.Friendships.Service;
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
    public static (
        UserService UserService,
        PostService PostService,
        CommentService CommentService,
        FriendshipService FriendshipService,
        FriendRequestService FriendRequestService,
        UserBlockService UserBlockService,
        FakeUserRepository UserRepository,
        FakePostRepository PostRepository,
        FakeCommentRepository CommentRepository,
        FakeFriendshipRepository FriendshipRepository,
        FakeFriendRequestRepository FriendRequestRepository,
        FakeUserBlockRepository UserBlockRepository) Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var userRepository = new FakeUserRepository();
        var postRepository = new FakePostRepository();
        var commentRepository = new FakeCommentRepository();
        var friendshipRepository = new FakeFriendshipRepository();
        var friendRequestRepository = new FakeFriendRequestRepository();
        var userBlockRepository = new FakeUserBlockRepository();
        var http = httpContextAccessor ?? new FakeHttpContextAccessor("1");
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        PostService postService = null!;
        CommentService commentService = null!;
        FriendshipService friendshipService = null!;

        var userService = new UserService(
            userRepository,
            db,
            new Lazy<PostService>(() => postService),
            new Lazy<CommentService>(() => commentService),
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

        FriendRequestService friendRequestService = null!;

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

        return (userService, postService, commentService, friendshipService, friendRequestService, userBlockService, userRepository, postRepository, commentRepository, friendshipRepository, friendRequestRepository, userBlockRepository);
    }
}
