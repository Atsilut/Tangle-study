using System.Net;
using System.Net.Http.Json;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class LoginControllerIntegrationTest(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    private readonly string testUserPassword = "testtest123!";
    private readonly string testUserNickname = "old";

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
        var create = await Client.PostAsJsonAsync("/api/join", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var getAll = await Client.GetAsync("/api/users");
        var all = await getAll.Content.ReadFromJsonAsync<List<UserGetResponseDto>>();
        return all!.First(u => u.Email == req.Email);
    }

    // --- CREATE (POST /api/join) ---

    [Fact]
    public async Task CreateUser_Returns409_WhenEmailAlreadyExists()
    {
        // Arrange
        var created = await CreateAndGetUser();
        var duplicateReq = CreateUserRequest(email: created.Email);

        // Act
        var res = await Client.PostAsJsonAsync("/api/join", duplicateReq);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // --- LOGIN (POST /api/login) ---

    [Fact]
    public async Task Login_Returns200_WhenCredentialsValid()
    {
        // Arrange
        var created = await CreateAndGetUser();
        var req = new LoginRequestDto
        {
            Email = created.Email,
            Password = testUserPassword
        };

        // Act
        var res = await Client.PostAsJsonAsync("/api/login", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var loginResult = await res.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginResult);
    }

    [Fact]
    public async Task Login_Returns401_WhenWrongPassword()
    {
        // Arrange
        var created = await CreateAndGetUser();
        const string wrongPassword = "wrongpassword789!";
        var req = new LoginRequestDto
        {
            Email = created.Email,
            Password = wrongPassword
        };

        // Act
        var res = await Client.PostAsJsonAsync("/api/login", req);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_Returns401_WhenEmailNotFound()
    {
        // Arrange
        await CreateAndGetUser();
        const string unknownEmail = "neversignedupbefore@random.com";
        var req = new LoginRequestDto
        {
            Email = unknownEmail,
            Password = testUserPassword
        };

        // Act
        var res = await Client.PostAsJsonAsync("/api/login", req);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
