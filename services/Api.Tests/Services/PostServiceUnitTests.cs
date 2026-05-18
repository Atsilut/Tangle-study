using Api.Domain.Posts.Domain;
using Api.Domain.Posts.Dto;
using Api.Domain.Posts.Repository;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Service;
using Api.Domain.Users.Domain;
using Api.Tests.Repositories;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Xunit;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public class PostServiceUnitTests
{
    private readonly PostService _postService;
    private readonly FakePostRepository _postRepository;
    private readonly FakeUserRepository _userRepository;
    private readonly FakeHttpContextAccessor _httpContextAccessor;
    private readonly UserService _userService;

    public PostServiceUnitTests()
    {
        _postRepository = new FakePostRepository();
        _userRepository = new FakeUserRepository();
        _httpContextAccessor = new FakeHttpContextAccessor("1");
        _userService = new UserService(_userRepository);
        _postService = new PostService(_postRepository, _httpContextAccessor, _userService);
    }

    private async Task<User> CreateTestUserAsync(string email = "test@test.com", string username = "test")
    {
        var user = new User(email, "Password123!", username);
        await _userRepository.CreateUserAsync(user);
        return user;
    }
    private async Task<Post> CreateTestPostAsync(long userId, string title = "Test title", string content = "Test content")
    {
        var post = new Post(userId, title, content);
        await _postRepository.CreatePostAsync(post);
        return post;
    }

    [Fact]
    public async Task CreatePostAsync_ValidRequest_CreatesPost()
    {
        // Arrange
        var request = new PostCreateRequestDto
        {
            Title = "Test title",
            Content = "Test content"
        };
        var user = await CreateTestUserAsync();

        // Act
        await _postService.CreatePostAsync(request);

        // Assert
        var post = await _postRepository.GetPostByIdAsync(1); // Assuming the fake returns 1 for first
        Assert.NotNull(post);
        Assert.Equal("Test content", post.Content);
        Assert.Equal(user.Id, post.UserId);
    }

    [Fact]
    public async Task CreatePostAsync_MissingUser_ThrowsEntityNotFoundException()
    {
        // Arrange
        var request = new PostCreateRequestDto
        {
            Title = "Test title",
            Content = "Test content"
        };

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "999") }))
        };

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _postService.CreatePostAsync(request));
    }

    [Fact]
    public async Task GetPostByIdAsync_ExistingPost_ReturnsPost()
    {
        // Arrange
        var user = await CreateTestUserAsync("test1@test.com", "test1");
        await CreateTestPostAsync(user.Id);
        // Act
        var result = await _postService.GetPostByIdAsync(1);
        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test content", result.Content);
    }

    [Fact]
    public async Task GetPostByIdAsync_NonExistingPost_ReturnsNull()
    {
        // Arrange
        const long nonExistingPostId = 999;

        // Act
        var result = await _postService.GetPostByIdAsync(nonExistingPostId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllPostsAsync_ReturnsAllPosts()
    {
        // Arrange
        var user = await CreateTestUserAsync("test2@test.com", "test2");
        await CreateTestPostAsync(user.Id, "Test title 1", "Test content 1");
        await CreateTestPostAsync(user.Id, "Test title 2", "Test content 2");
        // Act
        var result = await _postService.GetAllPostsAsync();
        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task UpdatePostAsync_ValidRequest_UpdatesPost()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);
        var updatedAtBefore = post.UpdatedAt;

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", user.Id.ToString()) }))
        };

        var editedTitle = "Edited title";
        var editedContent = "Edited content";
        var updateRequest = new PostPatchRequestDto
        {
            Id = post.Id,
            Title = editedTitle,
            Content = editedContent
        };

        // Act
        await _postService.UpdatePostAsync(updateRequest);

        // Assert
        var findPost = await _postRepository.GetPostByIdAsync(post.Id);
        Assert.NotNull(findPost);
        Assert.Equal(findPost.Title, editedTitle);
        Assert.Equal(findPost.Content, editedContent);
        Assert.True(findPost.UpdatedAt > updatedAtBefore);
    }

    [Fact]
    public async Task UpdatePostAsync_CorruptedUserData_ThrowsEntityNotFoundException()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);

        var request = new PostPatchRequestDto
        {
            Id = post.Id,
            Title = "Test title",
            Content = "Test content"
        };

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "999") }))
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() => _postService.UpdatePostAsync(request));
        Assert.Equal("Unauthorized user", exception.Message);
    }

    [Fact]
    public async Task UpdatePostAsync_PostMissing_ThrowsEntityNotFoundException()
    {
        // Arrange
        var user = await CreateTestUserAsync();

        var request = new PostPatchRequestDto
        {
            Id = 999, // Non-existent post
            Title = "Test title",
            Content = "Test content"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() => _postService.UpdatePostAsync(request));
        Assert.Equal("Post not found", exception.Message);
    }

    [Fact]
    public async Task UpdatePostAsync_UserNotAuthor_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var postOwner = await CreateTestUserAsync("owner@test.com", "owner");
        var post = await CreateTestPostAsync(postOwner.Id);
        var requestingUser = await CreateTestUserAsync("hacker@test.com", "hacker");

        // Mock for the malicious/Unauthorized user token
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", requestingUser.Id.ToString()) }))
        };

        var request = new PostPatchRequestDto
        {
            Id = 1,
            Title = "Hacked title",
            Content = "Hacked content"
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _postService.UpdatePostAsync(request));
    }

    [Fact]
    public async Task DeletePostAsync_ValidRequest_DeletesPost()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", user.Id.ToString()) }))
        };

        // Act
        await _postService.DeletePostAsync(1);

        // Assert
        var findPost = await _postRepository.GetPostByIdAsync(post.Id);
        Assert.Null(findPost);
    }

    [Fact]
    public async Task DeletePostAsync_UserNotAuthor_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var postOwner = await CreateTestUserAsync("owner@test.com", "owner");
        var post = await CreateTestPostAsync(postOwner.Id);
        var requestingUser = await CreateTestUserAsync("hacker@test.com", "hacker");

        // Mock for the malicious/Unauthorized user token
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", requestingUser.Id.ToString()) }))
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _postService.DeletePostAsync(post.Id));
        Assert.Equal("Unauthorized access", exception.Message);
    }
}