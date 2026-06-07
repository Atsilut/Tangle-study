using Api.Domain.Comments.Service;
using Api.Domain.Groups.Service;
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
    public class PostService(
        IPostRepository repo,
        AppDbContext db,
        Lazy<CommentService> commentService,
        IHttpContextAccessor httpContextAccessor,
        UserService userService,
        GroupBoardAccessService groupBoardAccess)
    {
        private readonly IPostRepository _repo = repo;
        private readonly AppDbContext _db = db;
        private readonly Lazy<CommentService> _commentService = commentService;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly UserService _userService = userService;
        private readonly GroupBoardAccessService _groupBoardAccess = groupBoardAccess;

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));

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

            if (request.GroupId.HasValue || request.GroupBoardId.HasValue)
            {
                if (!request.GroupId.HasValue || !request.GroupBoardId.HasValue)
                    throw new ArgumentException("GroupId and GroupBoardId must be provided together for group posts.");
                await _groupBoardAccess.EnsureCanWritePostAsync(request.GroupId.Value, request.GroupBoardId.Value);
            }

            var post = new Post(
                userId: userId,
                title: request.Title,
                content: request.Content,
                groupId: request.GroupId,
                groupBoardId: request.GroupBoardId
            );
            await _repo.CreatePostAsync(post);
        }

        public async Task<List<PostGetResponseDto>?> GetAllPostsAsync()
        {
            var posts = await _repo.GetAllPostsAsync();
            if (posts.Count == 0) return null;

            var nicknames = await _userService.GetNicknamesByUserIdsAsync(posts.Select(p => p.AuthorUserId));
            return [.. posts
                .Select(post => MapToDto(post, nicknames.GetValueOrDefault(post.AuthorUserId, "Deleted User")))];
        }

        public async Task<PostGetResponseDto?> GetPostByIdAsync(long id)
        {
            var post = await _repo.GetPostByIdAsync(id);
            if (post == null) return null;

            if (post.GroupId is not null && post.GroupBoardId is not null)
                await _groupBoardAccess.EnsureCanViewBoardAsync(post.GroupId.Value, post.GroupBoardId.Value);

            var user = await _userService.GetUserByIdAsync(post.AuthorUserId);
            return MapToDto(post, user?.Nickname ?? "Deleted User");
        }

        public async Task CreateGroupBoardPostAsync(long groupId, long boardId, GroupBoardPostCreateRequestDto request)
        {
            var userId = GetUserIdFromLogin();
            await _userService.EnsureUserExistsAsync(userId, statusCode: StatusCodes.Status400BadRequest);
            await _groupBoardAccess.EnsureCanWritePostAsync(groupId, boardId);

            var post = new Post(userId, request.Title, request.Content, groupId, boardId);
            await _repo.CreatePostAsync(post);
        }

        public async Task<List<PostGetResponseDto>?> GetGroupBoardPostsAsync(long groupId, long boardId)
        {
            await _groupBoardAccess.EnsureCanViewBoardAsync(groupId, boardId);
            var posts = await _repo.GetPostsByGroupBoardAsync(groupId, boardId);
            if (posts.Count == 0) return null;

            var nicknames = await _userService.GetNicknamesByUserIdsAsync(posts.Select(p => p.AuthorUserId));
            return [.. posts
                .Select(post => MapToDto(post, nicknames.GetValueOrDefault(post.AuthorUserId, "Deleted User")))];
        }

        public async Task<PostGetResponseDto?> GetGroupBoardPostByIdAsync(long groupId, long boardId, long postId)
        {
            await _groupBoardAccess.EnsureCanViewBoardAsync(groupId, boardId);
            var post = await _repo.GetGroupBoardPostAsync(groupId, boardId, postId);
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

            return [.. posts.Select(post => MapToDto(post, user.Nickname))];
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

        public async Task<PostPatchResponseDto> UpdatePostAsync(PostPatchRequestDto request)
        {
            var user = await _userService.GetLoggedInUserEntityOrThrowAsync();
            var post = await GetPostOrThrowAsync(request.Id);
            if (post.GroupId is not null && post.GroupBoardId is not null)
                await _groupBoardAccess.EnsureCanViewBoardAsync(post.GroupId.Value, post.GroupBoardId.Value);
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

        public async Task<(long GroupId, long GroupBoardId)?> TryGetGroupBoardContextAsync(long postId)
        {
            var post = await _repo.GetPostByIdAsync(postId);
            if (post is null) return null;
            if (post.GroupId is null || post.GroupBoardId is null) return null;
            return (post.GroupId.Value, post.GroupBoardId.Value);
        }

        public async Task DeleteAllByGroupAsync(long groupId)
        {
            var postIds = await _repo.GetPostIdsByGroupAsync(groupId);
            if (postIds.Count > 0)
                await _commentService.Value.DeleteAllForPostIdsAsync(postIds);
            await _repo.DeleteAllByGroupAsync(groupId);
        }

        public async Task DeletePostAsync(long id)
        {
            var user = await _userService.GetLoggedInUserEntityOrThrowAsync();
            var post = await GetPostOrThrowAsync(id);
            if (post.GroupId is not null && post.GroupBoardId is not null)
                await _groupBoardAccess.EnsureCanViewBoardAsync(post.GroupId.Value, post.GroupBoardId.Value);
            if (post.AuthorUserId != user.Id) throw new UnauthorizedAccessException("Unauthorized access");

            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _commentService.Value.DetachCommentsFromDeletedPostAsync(id);
                await _repo.DeletePostAsync(post);
            });
        }
    }
}
