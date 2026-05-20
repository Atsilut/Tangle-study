using Api.Domain.Posts.Domain;
using Api.Domain.Posts.Dto;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Service;
using Api.Domain.Users.Domain;
using Api.Tests.Repositories;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Api.Global.Exceptions;

namespace Api.Tests.Services;

public sealed class PostServiceUnitTests
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
        const string testTitle = "Test title";
        const string testContent = "Test content";
        const long firstPostId = 1;
        var request = new PostCreateRequestDto
        {
            Title = testTitle,
            Content = testContent
        };
        var user = await CreateTestUserAsync();

        // Act
        await _postService.CreatePostAsync(request);

        // Assert
        var post = await _postRepository.GetPostByIdAsync(firstPostId); // Assuming the fake returns 1 for first
        Assert.NotNull(post);
        Assert.Equal(testContent, post.Content);
        Assert.Equal(user.Id, post.UserId);
    }

    [Fact]
    public async Task CreatePostAsync_MissingUser_ThrowsEntityNotFoundException()
    {
        // Arrange
        const string testTitle = "Test title";
        const string testContent = "Test content";
        const string invalidUserId = "999";
        var request = new PostCreateRequestDto
        {
            Title = testTitle,
            Content = testContent
        };

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", invalidUserId) }))
        };

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _postService.CreatePostAsync(request));
    }

    [Fact]
    public async Task CreatePostAsync_MissingLogin_ThrowsEntityNotFoundException()
    {
        // Arrange
        const string testTitle = "Test title";
        const string testContent = "Test content";
        const string unauthorizedAccessMessage = "Unauthorized Access";
        var request = new PostCreateRequestDto
        {
            Title = testTitle,
            Content = testContent
        };
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()) // Simulating no logged-in user
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() => _postService.CreatePostAsync(request));
        Assert.Equal(unauthorizedAccessMessage, exception.Message);
    }

    [Fact]
    public async Task GetPostByIdAsync_ExistingPost_ReturnsPost()
    {
        // Arrange
        const string email = "test1@test.com";
        const string username = "test1";
        const string testContent = "Test content";
        const long firstPostId = 1;
        var user = await CreateTestUserAsync(email, username);
        await CreateTestPostAsync(user.Id);
        // Act
        var result = await _postService.GetPostByIdAsync(firstPostId);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(testContent, result.Content);
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
        const string email = "test2@test.com";
        const string username = "test2";
        const string title1 = "Test title 1";
        const string content1 = "Test content 1";
        const string title2 = "Test title 2";
        const string content2 = "Test content 2";
        var user = await CreateTestUserAsync(email, username);
        await CreateTestPostAsync(user.Id, title1, content1);
        await CreateTestPostAsync(user.Id, title2, content2);
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

        const string editedTitle = "Edited title";
        const string editedContent = "Edited content";
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
        const string testTitle = "Test title";
        const string testContent = "Test content";
        const string invalidUserId = "999";
        const string unauthorizedUserMessage = "Unauthorized user";

        var request = new PostPatchRequestDto
        {
            Id = post.Id,
            Title = testTitle,
            Content = testContent
        };

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", invalidUserId) }))
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() => _postService.UpdatePostAsync(request));
        Assert.Equal(unauthorizedUserMessage, exception.Message);
    }

    [Fact]
    public async Task UpdatePostAsync_MissingPost_ThrowsEntityNotFoundException()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        const long nonExistentPostId = 999;
        const string testTitle = "Test title";
        const string testContent = "Test content";
        const string postNotFoundMessage = "Post not found";

        var request = new PostPatchRequestDto
        {
            Id = nonExistentPostId,
            Title = testTitle,
            Content = testContent
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() => _postService.UpdatePostAsync(request));
        Assert.Equal(postNotFoundMessage, exception.Message);
    }

    [Fact]
    public async Task UpdatePostAsync_MissingLogin_ThrowsEntityNotFoundException()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()) // Simulating non logged-in user
        };
        const string testTitle = "Test title";
        const string testContent = "Test content";
        const string unauthorizedAccessMessage = "Unauthorized Access";

        var request = new PostPatchRequestDto
        {
            Id = post.Id,
            Title = testTitle,
            Content = testContent
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() => _postService.UpdatePostAsync(request));
        Assert.Equal(unauthorizedAccessMessage, exception.Message);
    }

    [Fact]
    public async Task UpdatePostAsync_UserNotAuthor_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        const string ownerEmail = "owner@test.com";
        const string ownerUsername = "owner";
        const string hackerEmail = "hacker@test.com";
        const string hackerUsername = "hacker";
        const string hackedTitle = "Hacked title";
        const string hackedContent = "Hacked content";
        var postOwner = await CreateTestUserAsync(ownerEmail, ownerUsername);
        var post = await CreateTestPostAsync(postOwner.Id);
        var requestingUser = await CreateTestUserAsync(hackerEmail, hackerUsername);

        // Mock for the malicious/Unauthorized user token
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", requestingUser.Id.ToString()) }))
        };

        var request = new PostPatchRequestDto
        {
            Id = post.Id,
            Title = hackedTitle,
            Content = hackedContent
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

        const long firstPostId = 1;

        // Act
        await _postService.DeletePostAsync(firstPostId);

        // Assert
        var findPost = await _postRepository.GetPostByIdAsync(post.Id);
        Assert.Null(findPost);
    }

    [Fact]
    public async Task DeletePostAsync_UserNotAuthor_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        const string ownerEmail = "owner@test.com";
        const string ownerUsername = "owner";
        const string hackerEmail = "hacker@test.com";
        const string hackerUsername = "hacker";
        const string unauthorizedAccessMessage = "Unauthorized access";
        var postOwner = await CreateTestUserAsync(ownerEmail, ownerUsername);
        var post = await CreateTestPostAsync(postOwner.Id);
        var requestingUser = await CreateTestUserAsync(hackerEmail, hackerUsername);

        // Mock for the malicious/Unauthorized user token
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", requestingUser.Id.ToString()) }))
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _postService.DeletePostAsync(post.Id));
        Assert.Equal(unauthorizedAccessMessage, exception.Message);
    }
}