using Api.Domain.Comments.Service;
using Api.Domain.Posts.Domain;
using Api.Domain.Posts.Dto;
using Api.Domain.Posts.Repository;
using Api.Domain.Users.Service;
using Api.Global.Db;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Posts.Service
{
    [Service]
    public class PostService
    {
        private readonly IPostRepository _repo;
        private readonly AppDbContext _db;
        private readonly Lazy<CommentService> _commentService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserService _userService;

        public PostService(
            IPostRepository repo,
            AppDbContext db,
            Lazy<CommentService> commentService,
            IHttpContextAccessor httpContextAccessor,
            UserService userService)
        {
            _repo = repo;
            _db = db;
            _commentService = commentService;
            _httpContextAccessor = httpContextAccessor;
            _userService = userService;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        public async Task EnsurePostExistsAsync(long id, string notFoundMessage = "Post not found", int statusCode = StatusCodes.Status404NotFound)
        {
            if (!await _repo.ExistsPostByIdAsync(id))
                throw new EntityNotFoundException(notFoundMessage, statusCode);
        }

        private async Task<Post> GetPostOrThrowAsync(long id, string notFoundMessage = "Post not found")
        {
            var post = await _repo.GetPostByIdAsync(id);
            if (post == null)
                throw new EntityNotFoundException(notFoundMessage);
            return post;
        }

        public async Task CreatePostAsync(PostCreateRequestDto request)
        {
            var userId = GetUserIdFromLogin();
            await _userService.EnsureUserExistsAsync(userId, statusCode: StatusCodes.Status400BadRequest);

            var post = new Post(
                userId: userId,
                title: request.Title,
                content: request.Content
            );
            await _repo.CreatePostAsync(post);
        }

        public async Task<List<PostGetResponseDto>?> GetAllPostsAsync()
        {
            var posts = await _repo.GetAllPostsAsync();
            if (posts.Count == 0) return null;

            var nicknames = await _userService.GetNicknamesByUserIdsAsync(posts.Select(p => p.AuthorUserId));
            return posts
                .Select(post => MapToDto(post, nicknames.GetValueOrDefault(post.AuthorUserId, "Deleted User")))
                .ToList();
        }

        public async Task<PostGetResponseDto?> GetPostByIdAsync(long id)
        {
            var post = await _repo.GetPostByIdAsync(id);
            if (post == null) return null;

            var user = await _userService.GetUserByIdAsync(post.AuthorUserId);
            return MapToDto(post, user?.Nickname ?? "Deleted User");
        }

        public async Task<List<PostGetResponseDto>?> GetPostsByUserNicknameAsync(string nickname)
        {
            var user = await _userService.GetUserByNicknameAsync(nickname);
            if (user == null) return null;
            var posts = await _repo.GetPostsByUserIdAsync(user.Id);
            if (posts.Count == 0) return null;

            return posts.Select(post => MapToDto(post, user.Nickname)).ToList();
        }

        private static PostGetResponseDto MapToDto(Post post, string authorNickname) => new(
            Id: post.Id,
            Title: post.Title,
            Content: post.Content,
            CreatedAt: post.CreatedAt,
            UpdatedAt: post.UpdatedAt,
            AuthorId: post.AuthorUserId,
            AuthorNickname: authorNickname
        );

        public async Task<PostPatchResponseDto>? UpdatePostAsync(PostPatchRequestDto request)
        {
            var user = await _userService.GetUserByIdOrThrowAsync(GetUserIdFromLogin(), "Unauthorized user");
            var post = await GetPostOrThrowAsync(request.Id);
            if (post.AuthorUserId != user.Id) throw new UnauthorizedAccessException();
            post.Update(request.Title, request.Content);
            await _repo.UpdatePostAsync(post);
            return new PostPatchResponseDto(
                Title: post.Title,
                Content: post.Content,
                UpdatedAt: post.UpdatedAt
            );
        }

        public Task DetachAuthorFromDeletedUserAsync(long userId) =>
            _repo.DetachAuthorFromPostsAsync(userId);

        public async Task DeletePostAsync(long id)
        {
            var user = await _userService.GetUserByIdOrThrowAsync(GetUserIdFromLogin(), "Authentication failed");
            var post = await GetPostOrThrowAsync(id);
            if (post.AuthorUserId != user.Id) throw new UnauthorizedAccessException("Unauthorized access");

            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _commentService.Value.DetachCommentsFromDeletedPostAsync(id);
                await _repo.DeletePostAsync(post);
            });
        }
    }
}
