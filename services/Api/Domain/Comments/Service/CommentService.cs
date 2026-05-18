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

            var comment = new Comment(
                content: request.Content,
                postId: request.PostId,
                userId: userId);

            await _repo.CreateCommentAsync(comment);
        }

        public async Task<CommentGetResponseDto?> GetCommentByIdAsync(long id)
        {
            var comment = await _repo.GetCommentByIdAsync(id);
            if (comment == null) return null;
            var res = new CommentGetResponseDto
            {
                Content = comment.Content,
                UserId = comment.UserId,
                PostId = comment.PostId,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt
            };

            return res;
        }

        public async Task<List<CommentGetResponseDto>?> GetCommentsByPostIdAsync(long postId)
        {
            var post = await _postService.GetPostByIdAsync(postId);
            if (post == null) throw new EntityNotFoundException("Post not found");
            var comments = await _repo.GetCommentsByPostIdAsync(postId);
            if (comments == null || comments.Count == 0) return null;
            var res = comments.Select(comment => new CommentGetResponseDto
            {
                Content = comment.Content,
                UserId = comment.UserId,
                PostId = comment.PostId,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt
            }).ToList();
            return res;
        }

        public async Task<List<CommentGetResponseDto>?> GetCommentsByUserIdAsync(long userId)
        {
            var comments = await _repo.GetCommentsByUserIdAsync(userId);
            if (comments == null || comments.Count == 0) return null;
            var res = comments.Select(comment => new CommentGetResponseDto
            {
                Content = comment.Content,
                UserId = comment.UserId,
                PostId = comment.PostId,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt
            }).ToList();
            return res;
        }

        public async Task<CommentPatchResponseDto> UpdateCommentAsync(CommentPatchRequestDto request)
        {
            var user = await _userService.GetUserByIdAsync(GetUserIdFromLogin());
            var comment = await _repo.GetCommentByIdAsync(request.Id);
            if (user == null) throw new EntityNotFoundException("Authentication failed");
            if (comment == null) throw new EntityNotFoundException("Comment not found");
            if (comment.UserId != user.Id) throw new UnauthorizedAccessException("Unauthorized access");
            comment.Content = request.Content;
            comment.UpdatedAt = DateTime.UtcNow;
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
            var comment = await _repo.GetCommentByIdAsync(id);
            if (user == null) throw new EntityNotFoundException("Authentication failed");
            if (comment == null) throw new EntityNotFoundException("Comment not found");
            if (comment.UserId != user.Id) throw new UnauthorizedAccessException("Unauthorized access");
            await _repo.DeleteCommentAsync(comment);
        }
    }
}