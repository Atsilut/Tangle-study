using Api.Domain.Posts.Dto;
using Api.Global.Exceptions;
using Api.Tests.Infrastructure;
using Api.Tests.Repositories;

namespace Api.Tests.Services;

public sealed class PostServiceUnitTests
{
    [Fact]
    public async Task CreatePostAsync_ValidRequest_CreatesPost()
    {
        // Arrange
        var http = new FakeHttpContextAccessor("1");
        var graph = DomainServiceTestFactory.Create(http);
        var user = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository);
        http.HttpContext = ServiceTestHelpers.ContextFor(user.Id);

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
        var owner = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "owner");
        var other = await ServiceTestHelpers.CreateUserAsync(graph.UserRepository, "other");
        http.HttpContext = ServiceTestHelpers.ContextFor(owner.Id);
        await graph.PostService.CreatePostAsync(new PostCreateRequestDto { Title = "t", Content = "c" });
        var post = (await graph.PostRepository.GetPostsByUserIdAsync(owner.Id)).Single();
        http.HttpContext = ServiceTestHelpers.ContextFor(other.Id);

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
}
