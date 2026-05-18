using Api.Domain.Comments.Domain;
using Api.Domain.Comments.Dto;
using Api.Domain.Comments.Service;
using Api.Domain.Posts.Domain;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
using Api.Tests.Repositories;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Api.Tests.Services;

public class CommentServiceUnitTests
{
    private readonly CommentService _commentService;
    private readonly FakeCommentRepository _commentRepository;
    private readonly FakePostRepository _postRepository;
    private readonly FakeUserRepository _userRepository;
    private readonly FakeHttpContextAccessor _httpContextAccessor;
    private readonly UserService _userService;
    private readonly PostService _postService;

    public CommentServiceUnitTests()
    {
        _commentRepository = new FakeCommentRepository();
        _postRepository = new FakePostRepository();
        _userRepository = new FakeUserRepository();
        _httpContextAccessor = new FakeHttpContextAccessor("1");
        _userService = new UserService(_userRepository);
        _postService = new PostService(_postRepository, _httpContextAccessor, _userService);
        _commentService = new CommentService(
            repo: _commentRepository,
            httpContextAccessor: _httpContextAccessor,
            userService: _userService,
            postService: _postService);
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

    private async Task<Comment> CreateTestCommentAsync(long userId, long postId, string content = "Test comment")
    {
        var comment = new Comment(content, postId, userId);
        await _commentRepository.CreateCommentAsync(comment);
        return comment;
    }

    [Fact]
    public async Task CreateCommentAsync_ValidRequest_CreatesComment()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);
        var request = new CommentCreateRequestDto
        {
            Content = "Test comment",
            PostId = post.Id
        };

        // Act
        await _commentService.CreateCommentAsync(request);

        // Assert
        var comments = await _commentRepository.GetAllCommentsAsync();
        Assert.Single(comments);
        Assert.Equal(request.Content, comments[0].Content);
        Assert.Equal(post.Id, comments[0].PostId);
        Assert.Equal(user.Id, comments[0].UserId);
    }

    [Fact]
    public async Task CreateCommentAsync_MissingPost_ThrowsEntityNotFoundException()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        const long nonExistentPostId = 999;
        var request = new CommentCreateRequestDto
        {
            Content = "Test comment",
            PostId = nonExistentPostId
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() => _commentService.CreateCommentAsync(request));
        Assert.Equal("Post not found", exception.Message);
    }

    [Fact]
    public async Task CreateCommentAsync_MissingUser_ThrowsEntityNotFoundException()
    {
        // Arrange
        var post = await CreateTestPostAsync(1); // Create an invalid post
        // User has never been created so default login user id of 1 will not exist in the user repository
        var request = new CommentCreateRequestDto
        {
            Content = "Test comment",
            PostId = post.Id,
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() => _commentService.CreateCommentAsync(request));
        Assert.Equal("User not found", exception.Message);
        // Assert
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreateCommentAsync_MissingLogin_ThrowsEntityNotFoundException()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);
        var request = new CommentCreateRequestDto
        {
            Content = "Test comment",
            PostId = post.Id
        };

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity()) // Simulating no logged-in user
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() => _commentService.CreateCommentAsync(request));
        Assert.Equal("Unauthorized access", exception.Message);
    }

    [Fact]
    public async Task GetCommentByIdAsync_ExistingComment_ReturnsComment()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);
        await CreateTestCommentAsync(user.Id, post.Id);

        // Act
        var result = await _commentService.GetCommentByIdAsync(1);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test comment", result.Content);
    }

    [Fact]
    public async Task GetCommentByIdAsync_NonExistingComment_ReturnsNull()
    {
        // Arrange
        const long nonExistentCommentId = 999;

        // Act
        var result = await _commentService.GetCommentByIdAsync(nonExistentCommentId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCommentsByPostIdAsync_ReturnsOnlyCommentsForThatPost()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);
        await CreateTestCommentAsync(user.Id, post.Id);
        await CreateTestCommentAsync(user.Id, post.Id);

        // Act
        var result = await _commentService.GetCommentsByPostIdAsync(post.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetCommentsByPostIdAsync_NoComments_ReturnsNull()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);

        // Act
        var result = await _commentService.GetCommentsByPostIdAsync(post.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCommentsByUserIdAsync_ReturnsOnlyCommentsForThatUser()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);
        const string comment1 = "Test Comment 1";
        const string comment2 = "Test Comment 2";
        await CreateTestCommentAsync(user.Id, post.Id, comment1);
        await CreateTestCommentAsync(user.Id, post.Id, comment2);

        // Act
        var result = await _commentService.GetCommentsByUserIdAsync(user.Id);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal(comment1, result[0].Content);
        Assert.Equal(comment2, result[1].Content);
    }

    [Fact]
    public async Task GetCommentsByUserIdAsync_NoComments_ReturnsNull()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);
        
        // Act
        var result = await _commentService.GetCommentsByUserIdAsync(user.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateCommentAsync_ValidRequest_UpdatesComment()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);
        var comment = await CreateTestCommentAsync(user.Id, post.Id);

        var newContent = "Updated comment";
        var request = new CommentPatchRequestDto
        {
            Id = comment.Id,
            Content = newContent
        };

        // Act
        await _commentService.UpdateCommentAsync(request);
        
        // Assert
        var findComment = await _commentRepository.GetCommentByIdAsync(comment.Id);
        Assert.NotNull(findComment);
        Assert.Equal(newContent, findComment.Content);
    }

    [Fact]
    public async Task UpdateCommentAsync_Missingcomment_ThrowsEntityNotFoundException()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);

        const long nonExistentCommentId = 999;
        var request = new CommentPatchRequestDto
        {
            Id = nonExistentCommentId,
            Content = "Updated comment"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() => _commentService.UpdateCommentAsync(request));
        Assert.Equal("Comment not found", exception.Message);
    }

    [Fact]
    public async Task UpdateCommentAsync_CorruptedUserData_ThrowsEntityNotFoundException()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);
        var comment = await CreateTestCommentAsync(user.Id, post.Id);

        var request = new CommentPatchRequestDto
        {
            Id = comment.Id,
            Content = "Test content"
        };

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "999") }))
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<EntityNotFoundException>(() => _commentService.UpdateCommentAsync(request));
        Assert.Equal("User not found", exception.Message);
    }

    [Fact]
    public async Task UpdateCommentAsync_UserNotAuthor_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var mainOwner = await CreateTestUserAsync("owner@test.com", "Owner");
        var post = await CreateTestPostAsync(mainOwner.Id);
        var requestingUser = await CreateTestUserAsync("hacker@test.com", "Hacker");
        var comment = await CreateTestCommentAsync(mainOwner.Id, post.Id);

        // Mock for the malicious/Unauthorized user token
        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", requestingUser.Id.ToString()) }))
        };

        var request = new CommentPatchRequestDto
        {
            Id = comment.Id,
            Content = "This comment is hacked"
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _commentService.UpdateCommentAsync(request));
    }

    [Fact]
    public async Task DeleteCommentAsync_ValidRequest_DeletesComment()
    {
        // Arrange
        var user = await CreateTestUserAsync();
        var post = await CreateTestPostAsync(user.Id);
        var comment = await CreateTestCommentAsync(user.Id, post.Id);

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", user.Id.ToString()) }))
        };

        // Act
        await _commentService.DeleteCommentAsync(comment.Id);

        // Assert
        var findComment = await _commentRepository.GetCommentByIdAsync(comment.Id);
        Assert.Null(findComment);
    }

    [Fact]
    public async Task DeleteCommentAsync_UserNotAuthor_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@test.com", "Owner");
        var post = await CreateTestPostAsync(owner.Id);
        var comment = await CreateTestCommentAsync(owner.Id, post.Id);
        var requestingUser = await CreateTestUserAsync("hacker@test.com", "Hacker");

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", requestingUser.Id.ToString()) }))
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _commentService.DeleteCommentAsync(comment.Id));
        Assert.Equal("Unauthorized access", exception.Message);
    }
}
