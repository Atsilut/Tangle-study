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

    private async Task<UserGetResponseDto> CreateUserForTest(string email, string password, string nickname)
    {
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
        var p = "testpass123!";
        var user = await CreateUserForTest("postcreates@test.com", p, "postcreator2");

        await LoginAs(user, p);

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
}