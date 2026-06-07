using System.Net;
using System.Net.Http.Json;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class LoginControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    private readonly string testUserPassword = IntegrationTestAuthHelpers.DefaultPassword;
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
        var create = await Client.PostAsJsonAsync("/api/join", req, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(create, HttpStatusCode.Created);

        var getAll = await Client.GetAsync("/api/users", TestContext.Current.CancellationToken);
        var all = await getAll.Content.ReadFromJsonAsync<List<UserGetResponseDto>>(TestContext.Current.CancellationToken);
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
        var res = await Client.PostAsJsonAsync("/api/join", duplicateReq, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Conflict);
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
        var res = await Client.PostAsJsonAsync("/api/login", req, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);

        var loginResult = await res.Content.ReadFromJsonAsync<LoginResponseDto>(TestContext.Current.CancellationToken);
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
        var res = await Client.PostAsJsonAsync("/api/login", req, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
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
        var res = await Client.PostAsJsonAsync("/api/login", req, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Unauthorized);
    }
}
