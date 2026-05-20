using System.Net;
using System.Net.Http.Json;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class LoginControllerIntegrationTest : IDisposable
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private readonly string testUserPassword = "testtest123!";
    private readonly string testUserNickname = "old";

    public LoginControllerIntegrationTest(PostgresTestcontainerFixture postgres)
    {
        _factory = new ApiWebApplicationFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    public UserCreateRequestDto CreateUserRequest(
        string? email = null,
        string? password = null,
        string? nickname = null
        ) => new()
        {
            Email = email ?? $"{Guid.NewGuid()}@test.com",
            Password = password ?? testUserPassword,
            Nickname = nickname ?? testUserNickname,
        };

    private async Task<UserGetResponseDto> CreateAndGetUser(UserCreateRequestDto? req = null)
    {
        req ??= CreateUserRequest();
        var create = await _client.PostAsJsonAsync("/api/join", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var getAll = await _client.GetAsync("/api/users");
        var all = await getAll.Content.ReadFromJsonAsync<List<UserGetResponseDto>>();
        return all!.First(u => u.Email == req.Email);
    }

    [Fact]
    public async Task CreateUser_Return400_WhenEmailAlreadyExists()
    {
        // Arrange
        var created = await CreateAndGetUser();
        var duplicateReq = CreateUserRequest(email: created.Email);

        // Act
        var res = await _client.PostAsJsonAsync("/api/join", duplicateReq);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Login_Return200_WhenCredentialsValid()
    {
        // Arrange
        var created = await CreateAndGetUser();
        var req = new LoginRequestDto
        {
            Email = created.Email,
            Password = testUserPassword
        };

        // Act
        var res = await _client.PostAsJsonAsync("/api/login", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var loginResult = await res.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginResult);
    }

    [Fact]
    public async Task Login_Return401_WhenWrongPassword()
    {
        // Arrange
        var created = await CreateAndGetUser();
        var req = new LoginRequestDto
        {
            Email = created.Email,
            Password = "wrongpassword789!"
        };

        // Act
        var res = await _client.PostAsJsonAsync("/api/login", req);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_Return401_WhenEmailNotFound()
    {
        // Arrange
        await CreateAndGetUser();
        var req = new LoginRequestDto
        {
            Email = "neversignedupbefore@random.com",
            Password = testUserPassword
        };

        // Act
        var res = await _client.PostAsJsonAsync("/api/login", req);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
