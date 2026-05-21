using Api.Domain.Comments.Service;
using Api.Domain.Friendships.Service;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Service;
using Api.Global.Db;
using Api.Tests.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Infrastructure;

internal static class DomainServiceTestFactory
{
    public static (
        UserService UserService,
        PostService PostService,
        CommentService CommentService,
        FriendshipService FriendshipService,
        FriendRequestService FriendRequestService,
        FakeUserRepository UserRepository,
        FakePostRepository PostRepository,
        FakeCommentRepository CommentRepository,
        FakeFriendshipRepository FriendshipRepository,
        FakeFriendRequestRepository FriendRequestRepository) Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var userRepository = new FakeUserRepository();
        var postRepository = new FakePostRepository();
        var commentRepository = new FakeCommentRepository();
        var friendshipRepository = new FakeFriendshipRepository();
        var friendRequestRepository = new FakeFriendRequestRepository();
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

        var friendRequestService = new FriendRequestService(
            friendRequestRepository,
            friendshipService,
            userService,
            db,
            http);

        return (userService, postService, commentService, friendshipService, friendRequestService, userRepository, postRepository, commentRepository, friendshipRepository, friendRequestRepository);
    }
}
