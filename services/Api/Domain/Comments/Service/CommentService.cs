using Api.Domain.Comments.Domain;
using Api.Domain.Comments.Dto;
using Api.Domain.Comments.Repository;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Comments.Service
{
    [Service]
    public class CommentService
    {
        private readonly ICommentRepository _repo;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly PostService _postService;
        private readonly UserService _userService;

        public CommentService(ICommentRepository repo, IHttpContextAccessor httpContextAccessor, PostService postService, UserService userService)
        {
            _repo = repo;
            _httpContextAccessor = httpContextAccessor;
            _postService = postService;
            _userService = userService;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        public async Task CreateCommentAsync(CommentCreateRequestDto request)
        {
            var userId = GetUserIdFromLogin();
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null) throw new EntityNotFoundException("Authentication failed");
            var post = await _postService.GetPostByIdAsync(request.PostId);
            if (post == null) throw new EntityNotFoundException("Post not found");

            if (request.ParentId.HasValue)
            {
                var parentComment = await _repo.GetCommentByIdAsync(request.ParentId.Value);
                if (parentComment == null) throw new EntityNotFoundException("Parent comment not found");
                if (parentComment.PostId != request.PostId) throw new ArgumentException("Parent comment must belong to the same post");
            }

            var comment = new Comment(
                content: request.Content,
                postId: request.PostId,
                userId: userId,
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
            var post = await _postService.GetPostByIdAsync(postId);
            if (post == null) throw new EntityNotFoundException("Post not found");
            var comments = await _repo.GetCommentsByPostIdAsync(postId);
            if (comments.Count == 0) return null;
            return BuildCommentTree(comments);
        }

        public async Task<List<CommentGetResponseDto>?> GetCommentsByUserIdAsync(long userId)
        {
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null) throw new EntityNotFoundException("User not found");
            var comments = await _repo.GetCommentsByUserIdAsync(userId);
            if (comments.Count == 0) return null;
            return comments.Select(MapToDto).ToList();
        }

        private CommentGetResponseDto MapToDto(Comment comment) => new()
        {
            Id = comment.Id,
            Content = comment.Content,
            UserId = comment.UserId,
            PostId = comment.PostId,
            ParentId = comment.ParentId,
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
            }

            return roots;
        }

        public async Task<CommentPatchResponseDto> UpdateCommentAsync(CommentPatchRequestDto request)
        {
            var user = await _userService.GetUserByIdAsync(GetUserIdFromLogin());
            if (user == null) throw new EntityNotFoundException("Authentication failed");
            var comment = await _repo.GetCommentByIdAsync(request.Id);
            if (comment == null) throw new EntityNotFoundException("Comment not found");
            if (comment.UserId != user.Id) throw new UnauthorizedAccessException("Unauthorized access");
            comment.UpdateContent(request.Content);
            await _repo.UpdateCommentAsync(comment);
            var response = new CommentPatchResponseDto
            {
                Content = comment.Content,
                PostId = comment.PostId,
                UpdatedAt = comment.UpdatedAt
            };
            return response;
        }

        public async Task DeleteCommentAsync(long id)
        {
            var user = await _userService.GetUserByIdAsync(GetUserIdFromLogin());
            if (user == null) throw new EntityNotFoundException("Authentication failed");
            var comment = await _repo.GetCommentByIdAsync(id);
            if (comment == null) throw new EntityNotFoundException("Comment not found");
            if (comment.UserId != user.Id) throw new UnauthorizedAccessException("Unauthorized access");
            await _repo.DeleteCommentAsync(comment);
        }
    }
}