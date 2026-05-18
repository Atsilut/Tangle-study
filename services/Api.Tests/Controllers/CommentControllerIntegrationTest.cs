using Api.Domain.Comments.Dto;
using Api.Domain.Posts.Dto;
using Api.Domain.Users.Dto;
using Api.Global.Db;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class CommentControllerIntegrationTest : IDisposable
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private readonly string testPassword = "testpass123!";

    public CommentControllerIntegrationTest(PostgresTestcontainerFixture postgres)
    {
        _factory = new ApiWebApplicationFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<UserGetResponseDto> CreateUserForTest(string testMethodName, string password = "testpass123!", long index = 1)
    {
        var email = testMethodName + index.ToString() + "@test.com";
        var nickname = $"{testMethodName}User" + index.ToString();
        var req = new UserCreateRequestDto
        {
            Email = email,
            Password = password,
            Nickname = nickname,
        };
        var create = await _client.PostAsJsonAsync("/api/join", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var getAll = await _client.GetAsync("/api/users");
        var all = await getAll.Content.ReadFromJsonAsync<List<UserGetResponseDto>>();
        return all!.Single(u => u.Email == req.Email);
    }

    private async Task<PostGetResponseDto> CreatePostForTest(string testMethodName, long userId, long index = 1)
    {
        var req = new PostCreateRequestDto
        {
            Title = $"{testMethodName} Post Title " + index.ToString(),
            Content = $"{testMethodName} Post Content " + index.ToString()
        };
        var create = await _client.PostAsJsonAsync("/api/posts", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var getAll = await _client.GetAsync("/api/posts");
        var all = await getAll.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        return all!.Single(p => p.Title == req.Title && p.Content == req.Content && p.AuthorId == userId);
    }

    private async Task LoginAs(UserGetResponseDto user, string password)
    {
        var req = new LoginRequestDto
        {
            Email = user.Email,
            Password = password
        };
        var res = await _client.PostAsJsonAsync("/api/login", req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var loginRes = await res.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginRes);

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginRes.AccessToken);
    }

    [Fact]
    public async Task CreateComment_Returns201()
    {
        // Arrange
        var testMethodName = "CreateComment";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var post = await CreatePostForTest(testMethodName, user.Id);
        var req = new CommentCreateRequestDto
        {
            PostId = post.Id,
            Content = $"{testMethodName} Test"
        };

        // Act
        var res = await _client.PostAsJsonAsync("/api/comments", req);

        //Assert
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task CreateComment_Returns401_IfNotLoggedIn()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;
        var req = new CommentCreateRequestDto
        {
            PostId = 1,
            Content = "Unauthorized Comment"
        };
        // Act
        var res = await _client.PostAsJsonAsync("/api/comments", req);
        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task CreateComment_Return404_IfPostNotFound()
    {
        // Arrange
        var testMethodName = "CreateComment_PostNotFound";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        const long nonExistentPostId = 9999; // Assuming this post has been deleted while commenting
        var req = new CommentCreateRequestDto
        {
            PostId = nonExistentPostId,
            Content = $"{testMethodName} Test"
        };

        // Act
        var res = await _client.PostAsJsonAsync("/api/comments", req);

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
