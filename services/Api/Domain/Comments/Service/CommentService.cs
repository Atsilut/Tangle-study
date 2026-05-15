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
            var post = await _postService.GetPostByIdAsync(request.PostId);
            if (post == null) throw new EntityNotFoundException("Post not found");
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null) throw new EntityNotFoundException("User not found");
            
            var comment = new Comment
            (
                content : request.Content,
                postId : request.PostId,
                userId : userId
            );

            await _repo.CreateCommentAsync(comment);
        }
    }
}
