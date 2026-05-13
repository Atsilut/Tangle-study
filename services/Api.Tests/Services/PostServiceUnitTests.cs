using Api.Domain.Posts.Domain;
using Api.Domain.Posts.Dto;
using Api.Domain.Posts.Repository;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Service;
using Api.Domain.Users.Domain;
using Api.Tests.Fakes;
using System.Threading.Tasks;
using Xunit;

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

    [Fact]
    public async Task CreatePostAsync_ValidRequest_CreatesPost()
    {
        // Arrange
        var request = new PostCreateRequestDto
        {
            Title = "Test title",
            Content = "Test content"
        };
        var user = new User("test@test.com", "Password123!", "test");
        await _userRepository.CreateUserAsync(user);

        // Act
        await _postService.CreatePostAsync(request);

        // Assert
        var post = await _postRepository.GetPostByIdAsync(1); // Assuming the fake returns 1 for first
        Assert.NotNull(post);
        Assert.Equal("Test content", post.Content);
        Assert.Equal(user.Id, post.UserId);
    }
}