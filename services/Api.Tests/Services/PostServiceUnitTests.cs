using Api.Domain.Posts.Dto;
using Api.Domain.Users.Domain;
using Api.Global.Exceptions;
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
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var user = await CreateUserAsync(graph.UserRepository);
        http.HttpContext = ContextFor(user.Id);

        // Act
        await graph.PostService.CreatePostAsync(new PostCreateRequestDto
        {
            Title = "Test title",
            Content = "Test content",
        });

        // Assert
        var posts = await graph.PostRepository.GetPostsByUserIdAsync(user.Id);
        Assert.Single(posts);
        Assert.Equal("Test content", posts[0].Content);
    }

    [Fact]
    public async Task UpdatePostAsync_NonOwner_ThrowsUnauthorized()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var owner = await CreateUserAsync(graph.UserRepository, "owner");
        var other = await CreateUserAsync(graph.UserRepository, "other");
        http.HttpContext = ContextFor(owner.Id);
        await graph.PostService.CreatePostAsync(new PostCreateRequestDto { Title = "t", Content = "c" });
        var post = (await graph.PostRepository.GetPostsByUserIdAsync(owner.Id)).Single();
        http.HttpContext = ContextFor(other.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            graph.PostService.UpdatePostAsync(new PostPatchRequestDto
            {
                Id = post.Id,
                Title = "hijacked",
                Content = "nope",
            }));
    }

    [Fact]
    public async Task GetPostByIdAsync_ReturnsNull_WhenMissing()
    {
        // Arrange
        var graph = DomainServiceTestFactory.Create();

        // Act
        var dto = await graph.PostService.GetPostByIdAsync(99999);

        // Assert
        Assert.Null(dto);
    }

    private static DefaultHttpContext ContextFor(long userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) })),
    };

    private static async Task<User> CreateUserAsync(FakeUserRepository repo, string nickname = "test")
    {
        var user = new User($"{nickname}@test.com", "Password123!", nickname);
        await repo.CreateUserAsync(user);
        return user;
    }
}
