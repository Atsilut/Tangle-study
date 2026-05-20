using System.Net;
using System.Net.Http.Json;
using Api.Domain.Posts.Dto;
using Api.Domain.Users.Dto;
using Api.Global.Db;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class PostControllerIntegrationTests : IDisposable
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private readonly string testPassword = "testpass123!";

    public PostControllerIntegrationTests(PostgresTestcontainerFixture postgres)
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

    private async Task DeleteAllPostsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Posts.ExecuteDeleteAsync();
    }

    [Fact]
    public async Task CreatePost_Returns201()
    {
        // Arrange
        var testMethodName = "PostCreate";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var req = new PostCreateRequestDto { Title = "my title", Content = "my content" };

        // Act
        var res = await _client.PostAsJsonAsync("/api/posts", req);

        // Assert
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task CreatePost_Returns401_WhenNotAuthenticated()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;
        var req = new PostCreateRequestDto { Title = "title", Content = "content" };

        // Act
        var res = await _client.PostAsJsonAsync("/api/posts", req);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("", "valid content")]
    [InlineData("valid title", "")]
    public async Task CreatePost_Returns400_WhenInvalidRequest(string title, string content)
    {
        // Arrange
        var testMethodName = "PostCreateWithInvalidRequest";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var req = new PostCreateRequestDto { Title = title, Content = content };
        // Act
        var res = await _client.PostAsJsonAsync("/api/posts", req);
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task GetAllPosts_Returns200_WithPosts()
    {
        // Arrange
        await DeleteAllPostsAsync();
        var testMethodName = "GetAllPosts";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var title = "test title";
        var content = "test content";
        var createReq1 = new PostCreateRequestDto { Title = title + "1", Content = content + " 111" };
        var createReq2 = new PostCreateRequestDto { Title = title + "2", Content = content + " 222" };
        var createRes1 = await _client.PostAsJsonAsync("/api/posts", createReq1);
        Assert.Equal(HttpStatusCode.Created, createRes1.StatusCode);
        var createRes2 = await _client.PostAsJsonAsync("/api/posts", createReq2);
        Assert.Equal(HttpStatusCode.Created, createRes2.StatusCode);

        // Act
        var res = await _client.GetAsync("/api/posts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = await res.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, p => p.Title == title + "1" && p.Content == content + " 111");
        Assert.Contains(list, p => p.Title == title + "2" && p.Content == content + " 222");
    }

    [Fact]
    public async Task GetAllPosts_Returns204_WhenNoPosts()
    {
        // Arrange
        await DeleteAllPostsAsync();

        // Act
        var res = await _client.GetAsync("/api/posts");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task GetPostById_Returns200_WhenPostExists()
    {
        // Arrange
        var testMethodName = "GetPostById";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var title = "test title";
        var content = "test content";
        var createReq = new PostCreateRequestDto { Title = title, Content = content };
        var createRes = await _client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await _client.GetAsync("/api/posts");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(post => post.Title == title);

        // Act
        var getRes = await _client.GetAsync($"/api/posts/{created.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        Assert.NotNull(dto);
        Assert.Equal(created.Id, dto.Id);
        Assert.Equal(title, dto.Title);
        Assert.Equal(content, dto.Content);
        Assert.Equal(user.Id, dto.AuthorId);
        Assert.Equal(user.Nickname, dto.AuthorNickname);
    }

    [Fact]
    public async Task GetPostById_Returns404_WhenPostMissing()
    {
        // Arrange
        const long missingPostId = 999999999999;

        // Act
        var res = await _client.GetAsync($"/api/posts/{missingPostId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetPostsByNickname_Returns200_WithAuthorPosts()
    {
        // Arrange
        var testMethodName = "GetPostsByNick";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var title = "nickname lookup title";
        var content = "nickname lookup content";
        var createReq = new PostCreateRequestDto { Title = title, Content = content };
        var createRes = await _client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        _client.DefaultRequestHeaders.Authorization = null;

        var encodedNickname = Uri.EscapeDataString(user.Nickname);

        // Act
        var getRes = await _client.GetAsync($"/api/posts/nickname/{encodedNickname}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var list = await getRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        Assert.NotNull(list);
        var post = Assert.Single(list);
        Assert.Equal(title, post.Title);
        Assert.Equal(content, post.Content);
        Assert.Equal(user.Id, post.AuthorId);
        Assert.Equal(user.Nickname, post.AuthorNickname);
    }

    [Fact]
    public async Task GetPostsByNickname_Returns204_WhenUserHasNoPosts()
    {
        // Arrange
        var testMethodName = "GetPostsByNickEmpty";
        var user = await CreateUserForTest(testMethodName, testPassword);
        var encodedNickname = Uri.EscapeDataString(user.Nickname);

        // Act
        var getRes = await _client.GetAsync($"/api/posts/nickname/{encodedNickname}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, getRes.StatusCode);
    }

    [Fact]
    public async Task GetPostsByNickname_Returns204_WhenNicknameUnknown()
    {
        // Arrange
        const string unknownNicknamePath = "DefinitelyNoSuchUserNickname99999";

        // Act
        var getRes = await _client.GetAsync("/api/posts/nickname/" + unknownNicknamePath);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, getRes.StatusCode);
    }

    [Fact]
    public async Task UpdatePost_Returns200_WhenLoggedInAsOwner()
    {
        // Arrange
        var testMethodName = "PostPatch";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var createReq = new PostCreateRequestDto { Title = "original", Content = "original body" };
        var createRes = await _client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await _client.GetAsync("/api/posts");
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(p => p.Title == "original");
        var updatedAtBefore = created.UpdatedAt;

        var newTitle = "updated title";
        var newContent = "updated body";
        var patchReq = new PostPatchRequestDto
        {
            Id = created.Id,
            Title = newTitle,
            Content = newContent,
        };

        // Act
        var patchRes = await _client.PatchAsJsonAsync("/api/posts", patchReq);
        var patchDto = await patchRes.Content.ReadFromJsonAsync<PostPatchResponseDto>();
        var getRes = await _client.GetAsync($"/api/posts/{created.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);

        Assert.NotNull(patchDto);
        Assert.Equal(newTitle, patchDto.Title);
        Assert.Equal(newContent, patchDto.Content);
        Assert.True(updatedAtBefore < patchDto.UpdatedAt);

        Assert.NotNull(dto);
        Assert.Equal(newTitle, dto.Title);
        Assert.Equal(newContent, dto.Content);
        Assert.True(updatedAtBefore < dto.UpdatedAt);
    }

    [Fact]
    public async Task UpdatePost_Returns401_WhenLoggedInAsNonOwner()
    {
        // Arrange
        var testMethodName = "PostPatchAuth";
        var owner = await CreateUserForTest(testMethodName + "Owner", testPassword);
        var nonOwner = await CreateUserForTest(testMethodName + "NonOwner", testPassword);

        await LoginAs(owner, testPassword);
        var title = "owners post";
        var content = "hands off";
        var createReq = new PostCreateRequestDto { Title = title, Content = content };
        var createRes = await _client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await _client.GetAsync("/api/posts");
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(p => p.Title == "owners post");

        await LoginAs(nonOwner, testPassword);
        var newTitle = "hijacked title";
        var newContent = "hijacked body";
        var patchReq = new PostPatchRequestDto
        {
            Id = created.Id,
            Title = newTitle,
            Content = newContent,
        };

        // Act
        var patchRes = await _client.PatchAsJsonAsync("/api/posts", patchReq);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, patchRes.StatusCode);

        await LoginAs(owner, testPassword);
        var getRes = await _client.GetAsync($"/api/posts/{created.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(title, dto.Title);
        Assert.NotEqual(newTitle, dto.Title);
        Assert.Equal(content, dto.Content);
        Assert.NotEqual(newContent, dto.Content);
    }

    [Fact]
    public async Task UpdatePost_Returns404_WhenPostMissing()
    {
        // Arrange
        var user = await CreateUserForTest("PostPatchMissing", testPassword);
        await LoginAs(user, testPassword);
        var patchReq = new PostPatchRequestDto
        {
            Id = 999999999999,
            Title = "n/a",
            Content = "n/a",
        };

        // Act
        var patchRes = await _client.PatchAsJsonAsync("/api/posts", patchReq);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, patchRes.StatusCode);
    }

    [Fact]
    public async Task DeletePost_Returns204_WhenLoggedInAsOwner()
    {
        // Arrange
        var testMethodName = "PostDelete";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);
        var createReq = new PostCreateRequestDto { Title = "to delete", Content = "gone soon" };
        var createRes = await _client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await _client.GetAsync("/api/posts");
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(p => p.Title == "to delete");

        // Act
        var deleteRes = await _client.DeleteAsync($"/api/posts/{created.Id}");
        var getRes = await _client.GetAsync($"/api/posts/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    [Fact]
    public async Task DeletePost_Returns401_WhenLoggedInAsNonOwner()
    {
        // Arrange
        var testMethodName = "PostDeleteAuth";
        var owner = await CreateUserForTest(testMethodName + "Owner", testPassword);
        var other = await CreateUserForTest(testMethodName + "Other", testPassword);

        await LoginAs(owner, testPassword);
        var title = "do not delete me";
        var content = "still here";
        var createReq = new PostCreateRequestDto { Title = title, Content = content };
        var createRes = await _client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await _client.GetAsync("/api/posts");
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(p => p.Title == title);

        await LoginAs(other, testPassword);

        // Act
        var deleteRes = await _client.DeleteAsync($"/api/posts/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, deleteRes.StatusCode);

        await LoginAs(owner, testPassword);
        var getRes = await _client.GetAsync($"/api/posts/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var dto = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(title, dto.Title);
        Assert.Equal(content, dto.Content);
    }

    [Fact]
    public async Task DeletePost_Returns404_WhenPostMissing()
    {
        // Arrange
        var user = await CreateUserForTest("PostDeleteMissing", testPassword);
        await LoginAs(user, testPassword);

        // Act
        var deleteRes = await _client.DeleteAsync("/api/posts/999999999999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, deleteRes.StatusCode);
    }
}
