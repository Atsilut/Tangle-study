using Api.Domain.Posts.Domain;
using Api.Domain.Posts.Dto;
using Api.Domain.Posts.Repository;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Posts.Service
{
    [Service]
    public class PostService
    {
        private readonly IPostRepository _repo;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserService _userService;

        public PostService(IPostRepository repo, IHttpContextAccessor httpContextAccessor)
        {
            _repo = repo;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserId() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        public async Task CreatePostAsync(PostCreateRequestDto request)
        {
            var post = new Post(
                userId: GetUserId(),
                title: request.Title,
                content: request.Content
            );
            await _repo.CreatePostAsync(post);
        }

        public async Task<List<PostGetResponseDto>?> GetAllPostsAsync()
        {
            var posts = await _repo.GetAllPostsAsync();
            if (posts == null) return null;

            var list = new List<PostGetResponseDto>();
            foreach (var post in posts)
            {
                list.Add(new PostGetResponseDto(
                        Id: post.Id,
                        Title: post.Title,
                        Content: post.Content,
                        UserId: post.UserId
                    ));
            }

            return list;
        }

        public async Task<PostGetResponseDto?> GetPostByIdAsync(long id)
        {
            var post = await _repo.GetPostByIdAsync(id);
            if (post == null) return null;

            var postResponse = new PostGetResponseDto(
                Id: post.Id,
                Title: post.Title,
                Content: post.Content,
                UserId: post.UserId
            );

            return postResponse;
        }

        public async Task<List<PostGetResponseDto>?> GetPostsByUserNickname(string nickname)
        {
            var user = await _userService.GetUserByNicknameAsync(nickname);
            if (user == null) return null;
            var posts = await _repo.GetPostsByUserIdAsync(user.Id);
            if (posts == null) return null;

            var list = new List<PostGetResponseDto>();
            foreach (var post in posts)
            {
                var postResponse = new PostGetResponseDto(
                    Id: post.Id,
                    Title: post.Title,
                    Content: post.Content,
                    UserId: post.UserId
                );
            }
            return list;
        }

        public async Task<PostPatchResponseDto>? UpdatePostAsync(PostPatchRequestDto request)
        {
            var user = await _userService.GetUserByIdAsync(GetUserId());
            var post = await _repo.GetPostByIdAsync(request.Id);
            if (user == null) throw new EntityNotFoundException("Unauthorized user");
            if (post == null) throw new EntityNotFoundException("Post not found");
            if (post.UserId != user.Id) throw new UnauthorizedAccessException();
            post.Title = request.Title;
            post.Content = request.Content;
            await _repo.UpdatePostAsync(post);
            var response = new PostPatchResponseDto(
                Title: post.Title,
                Content: post.Content
            );
            return response;
        }

        public async Task DeletePostAsync(long id)
        {
            var user = await _userService.GetUserByIdAsync(GetUserId());
            if (user == null) throw new EntityNotFoundException("Authentication failed");
            var post = await _repo.GetPostByIdAsync(id);
            if (post == null) throw new EntityNotFoundException("Post not found");
            if (post.UserId != user.Id) throw new UnauthorizedAccessException("Unauthorized access");

            await _repo.DeletePostAsync(post);
        }
    }
}