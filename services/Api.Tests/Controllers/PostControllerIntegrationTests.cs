using System.Net;
using System.Net.Http.Json;
using Api.Domain.Posts.Dto;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;
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

    private async Task<UserGetResponseDto> CreateUserForTest(string testMethod, string password = "testpass123!", long index = 1)
    {
        var email = $"{testMethod}@test.com";
        var nickname = $"{testMethod}User" + index;
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
        return all!.First(u => u.Email == req.Email);
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
    public async Task CreatePost_ReturnsCreated()
    {
        var testMethodName = "PostCreate";
        var user = await CreateUserForTest(testMethodName, testPassword);

        await LoginAs(user, testPassword);

        var req = new PostCreateRequestDto { Title = "my title", Content = "my content" };
        var res = await _client.PostAsJsonAsync("/api/posts", req);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task CreatePost_Returns401_WhenNotAuthenticated()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var req = new PostCreateRequestDto { Title = "title", Content = "content" };
        var res = await _client.PostAsJsonAsync("/api/posts", req);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
    
    [Fact]
    public async Task GetAllPosts_ReturnsPosts()
    {
        var res = await _client.GetAsync("/api/posts");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        
        var list = await res.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task GetPostById_ReturnsOk_WhenPostExists()
    {
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

        var getRes = await _client.GetAsync($"/api/posts/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var dto = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(created.Id, dto.Id);
        Assert.Equal(title, dto.Title);
        Assert.Equal(content, dto.Content);
        Assert.Equal(user.Id, dto.UserId);
        Assert.Equal(user.Nickname, dto.AuthorNickname);
    }

    [Fact]
    public async Task GetPostById_ReturnsNotFound_WhenPostMissing()
    {
        var res = await _client.GetAsync("/api/posts/999999999999");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetPostsByNickname_ReturnsOk_WithAuthorPosts()
    {
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
        var getRes = await _client.GetAsync($"/api/posts/nickname/{encodedNickname}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var list = await getRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        Assert.NotNull(list);
        var post = Assert.Single(list);
        Assert.Equal(title, post.Title);
        Assert.Equal(content, post.Content);
        Assert.Equal(user.Id, post.UserId);
        Assert.Equal(user.Nickname, post.AuthorNickname);
    }

    [Fact]
    public async Task GetPostsByNickname_ReturnsOk_Empty_WhenUserHasNoPosts()
    {
        var testMethodName = "GetPostsByNickEmpty";
        var user = await CreateUserForTest(testMethodName, testPassword);

        var encodedNickname = Uri.EscapeDataString(user.Nickname);
        var getRes = await _client.GetAsync($"/api/posts/nickname/{encodedNickname}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var list = await getRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetPostsByNickname_ReturnsNoContent_WhenNicknameUnknown()
    {
        var getRes = await _client.GetAsync("/api/posts/nickname/DefinitelyNoSuchUserNickname99999");
        Assert.Equal(HttpStatusCode.NoContent, getRes.StatusCode);
    }
    
    [Fact]
    public async Task UpdatePost_ReturnsOk_WhenLoggedInAsOwner()
    {
        var testMethodName = "PostPatchOwner";
        var user = await CreateUserForTest(testMethodName, testPassword);
        await LoginAs(user, testPassword);

        var createReq = new PostCreateRequestDto { Title = "original", Content = "original body" };
        var createRes = await _client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await _client.GetAsync("/api/posts");
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(p => p.Title == "original");

        var newTitle = "updated title";
        var newContent = "updated body";
        var patchReq = new PostPatchRequestDto
        {
            Id = created.Id,
            Title = newTitle,
            Content = newContent,
        };
        var patchRes = await _client.PatchAsJsonAsync("/api/posts", patchReq);
        Assert.Equal(HttpStatusCode.OK, patchRes.StatusCode);

        var patchDto = await patchRes.Content.ReadFromJsonAsync<PostPatchResponseDto>();
        Assert.NotNull(patchDto);
        Assert.Equal(newTitle, patchDto.Title);
        Assert.Equal(newContent, patchDto.Content);

        var getRes = await _client.GetAsync($"/api/posts/{created.Id}");
        var dto = await getRes.Content.ReadFromJsonAsync<PostGetResponseDto>();
        Assert.NotNull(dto);
        Assert.Equal(newTitle, dto.Title);
        Assert.Equal(newContent, dto.Content);
    }

    [Fact]
    public async Task UpdatePost_ReturnsUnauthorized_WhenLoggedInAsNonOwner()
    {
        var owner = await CreateUserForTest("PostPatchNonOwnerA", testPassword);
        var other = await CreateUserForTest("PostPatchNonOwnerB", testPassword);

        await LoginAs(owner, testPassword);
        var title = "owners post";
        var content = "hands off";
        var createReq = new PostCreateRequestDto { Title = title, Content = content };
        var createRes = await _client.PostAsJsonAsync("/api/posts", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var listRes = await _client.GetAsync("/api/posts");
        var allPosts = await listRes.Content.ReadFromJsonAsync<List<PostGetResponseDto>>();
        var created = allPosts!.Single(p => p.Title == "owners post");

        await LoginAs(other, testPassword);
        var newTitle = "hijacked title";
        var newContent = "hijacked body";
        var patchReq = new PostPatchRequestDto
        {
            Id = created.Id,
            Title = newTitle,
            Content = newContent,
        };
        var patchRes = await _client.PatchAsJsonAsync("/api/posts", patchReq);
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
    public async Task UpdatePost_ReturnsNotFound_WhenPostMissing()
    {
        var user = await CreateUserForTest("PostPatchMissing", testPassword);
        await LoginAs(user, testPassword);

        var patchReq = new PostPatchRequestDto
        {
            Id = 999999999999,
            Title = "n/a",
            Content = "n/a",
        };
        var patchRes = await _client.PatchAsJsonAsync("/api/posts", patchReq);
        Assert.Equal(HttpStatusCode.NotFound, patchRes.StatusCode);
    }
}