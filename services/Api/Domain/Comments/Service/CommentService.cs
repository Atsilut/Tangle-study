using Api.Domain.Comments.Domain;
using Api.Domain.Comments.Dto;
using Api.Domain.Comments.Repository;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Service;
using Api.Global.Db;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Comments.Service
{
    [Service]
    public class CommentService
    {
        private readonly ICommentRepository _repo;
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly PostService _postService;
        private readonly UserService _userService;

        public CommentService(
            ICommentRepository repo,
            AppDbContext db,
            IHttpContextAccessor httpContextAccessor,
            PostService postService,
            UserService userService)
        {
            _repo = repo;
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _postService = postService;
            _userService = userService;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        private async Task<Comment> GetCommentOrThrowAsync(long id, string notFoundMessage = "Comment not found", int statusCode = StatusCodes.Status404NotFound)
        {
            var comment = await _repo.GetCommentByIdAsync(id);
            if (comment == null)
                throw new EntityNotFoundException(notFoundMessage, statusCode);
            return comment;
        }

        public async Task CreateCommentAsync(CommentCreateRequestDto request)
        {
            var userId = GetUserIdFromLogin();
            await _userService.EnsureUserExistsAsync(userId, "Authentication failed", StatusCodes.Status400BadRequest);
            await _postService.EnsurePostExistsAsync(request.PostId, statusCode: StatusCodes.Status400BadRequest);

            if (request.ParentId.HasValue)
            {
                var parentComment = await GetCommentOrThrowAsync(request.ParentId.Value, "Parent comment not found", StatusCodes.Status400BadRequest);
                if (parentComment.LogicalPostId != request.PostId)
                    throw new ArgumentException("Parent comment must belong to the same post");
            }

            var comment = new Comment(
                content: request.Content,
                userId: userId,
                postId: request.PostId,
                parentId: request.ParentId);
            await _repo.CreateCommentAsync(comment);
        }

        public async Task<CommentGetResponseDto?> GetCommentByIdAsync(long id)
        {
            var comment = await _repo.GetCommentByIdAsync(id);
            if (comment == null) return null;
            return MapToDto(comment);
        }

        public async Task<List<CommentGetResponseDto>?> GetCommentsByPostIdAsync(long postId)
        {
            await _postService.EnsurePostExistsAsync(postId);
            var comments = await _repo.GetCommentsByPostIdAsync(postId);
            if (comments.Count == 0) return null;
            return BuildCommentTree(comments);
        }

        public async Task<List<CommentGetResponseDto>?> GetCommentsByUserIdAsync(long userId)
        {
            var comments = await _repo.GetCommentsByUserIdAsync(userId);
            if (comments.Count == 0)
            {
                await _userService.EnsureUserExistsAsync(userId);
                return null;
            }

            return comments.Select(MapToDto).ToList();
        }

        private static CommentGetResponseDto MapToDto(Comment comment) => new()
        {
            Id = comment.Id,
            Content = comment.Content,
            UserId = comment.UserId,
            DeletedUserId = comment.DeletedUserId,
            PostId = comment.PostId,
            DeletedPostId = comment.DeletedPostId,
            ParentId = comment.ParentId,
            DeletedParentId = comment.DeletedParentId,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt
        };

        private List<CommentGetResponseDto> BuildCommentTree(IReadOnlyList<Comment> comments)
        {
            var byId = comments.ToDictionary(c => c.Id, MapToDto);
            var roots = new List<CommentGetResponseDto>();

            foreach (var comment in comments.OrderBy(c => c.CreatedAt))
            {
                var dto = byId[comment.Id];
                if (comment.ParentId is null)
                {
                    roots.Add(dto);
                    continue;
                }

                if (byId.TryGetValue(comment.ParentId.Value, out var parent))
                    parent.Replies.Add(dto);
                else if (comment.ParentId is null && comment.DeletedParentId is not null)
                    roots.Add(dto);
            }

            return roots;
        }

        public async Task<CommentPatchResponseDto> UpdateCommentAsync(CommentPatchRequestDto request)
        {
            var user = await _userService.GetUserByIdOrThrowAsync(GetUserIdFromLogin(), "Authentication failed");
            var comment = await GetCommentOrThrowAsync(request.Id);
            if (comment.PostId is null && comment.DeletedPostId is not null)
                throw new EntityNotFoundException("Post is not reachable. Comments are readonly.");
            if (comment.AuthorUserId != user.Id) throw new UnauthorizedAccessException("Unauthorized access");
            comment.UpdateContent(request.Content);
            await _repo.UpdateCommentAsync(comment);
            return new CommentPatchResponseDto
            {
                Content = comment.Content,
                PostId = comment.PostId,
                DeletedPostId = comment.DeletedPostId,
                UpdatedAt = comment.UpdatedAt
            };
        }

        public Task DetachCommentsFromDeletedPostAsync(long postId) =>
            _repo.DetachPostFromCommentsAsync(postId);

        public Task DetachAuthorFromDeletedUserAsync(long userId) =>
            _repo.DetachAuthorFromCommentsAsync(userId);

        public async Task DeleteCommentAsync(long id)
        {
            var user = await _userService.GetUserByIdOrThrowAsync(GetUserIdFromLogin(), "Authentication failed");
            var comment = await GetCommentOrThrowAsync(id);
            if (comment.AuthorUserId != user.Id) throw new UnauthorizedAccessException("Unauthorized access");
            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _repo.DetachParentFromRepliesAsync(id);
                await _repo.DeleteCommentAsync(comment);
            });
        }
    }
}
