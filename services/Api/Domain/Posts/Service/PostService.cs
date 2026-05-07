using Api.Domain.Posts.Domain;
using Api.Domain.Posts.Dto;
using Api.Domain.Posts.Repository;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;
using Microsoft.AspNetCore.Mvc;

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
    }
}