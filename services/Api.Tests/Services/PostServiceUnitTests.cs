using Api.Domain.Posts.Dto;
using Api.Domain.Users.Domain;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public sealed class PostServiceUnitTests
{
    [Fact]
    public async Task CreatePostAsync_ValidRequest_CreatesPost()
    {
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var user = await CreateUserAsync(graph.UserRepository);
        http.HttpContext = ContextFor(user.Id);

        await graph.PostService.CreatePostAsync(new PostCreateRequestDto
        {
            Title = "Test title",
            Content = "Test content",
        });

        var posts = await graph.PostRepository.GetPostsByUserIdAsync(user.Id);
        Assert.Single(posts);
        Assert.Equal("Test content", posts[0].Content);
    }

    private static DefaultHttpContext ContextFor(long userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
    };

    private static async Task<User> CreateUserAsync(FakeUserRepository repo)
    {
        var user = new User("test@test.com", "Password123!", "test");
        await repo.CreateUserAsync(user);
        return user;
    }
}
