using Api.Domain.Comments.Service;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Service;
using Api.Tests.Repositories;

namespace Api.Tests.Infrastructure;

internal static class DomainServiceTestFactory
{
    public static (
        UserService UserService,
        PostService PostService,
        CommentService CommentService,
        FakeUserRepository UserRepository,
        FakePostRepository PostRepository,
        FakeCommentRepository CommentRepository) Create(FakeHttpContextAccessor? httpContextAccessor = null)
    {
        var userRepository = new FakeUserRepository();
        var postRepository = new FakePostRepository();
        var commentRepository = new FakeCommentRepository();
        var http = httpContextAccessor ?? new FakeHttpContextAccessor("1");

        PostService postService = null!;
        CommentService commentService = null!;

        var userService = new UserService(
            userRepository,
            new Lazy<PostService>(() => postService),
            new Lazy<CommentService>(() => commentService),
            http);

        postService = new PostService(
            postRepository,
            new Lazy<CommentService>(() => commentService),
            http,
            userService);

        commentService = new CommentService(
            commentRepository,
            http,
            postService,
            userService);

        return (userService, postService, commentService, userRepository, postRepository, commentRepository);
    }
}
