using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Users.Dto;
using Users.Tests.Infrastructure;

namespace Users.Tests.Controllers;

[Collection(UsersIntegrationTestCollection.Name)]
public sealed class LoginControllerIntegrationTests(
    PostgresTestcontainerFixture postgres,
    RedisTestcontainerFixture redis)
    : IntegrationTestBase(postgres, redis)
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
        var profile = all!.First(u => u.Nickname == req.Nickname);
        IntegrationTestAuthHelpers.RegisterTestEmail(profile.Id, req.Email);
        return profile;
    }

    // --- CREATE (POST /api/join) ---

    [Fact]
    public async Task CreateUser_Returns201_WhenValid()
    {
        var req = CreateUserRequest(nickname: $"Signup{Guid.NewGuid():N}"[..20]);

        var res = await Client.PostAsJsonAsync("/api/join", req, TestContext.Current.CancellationToken);

        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateUser_Returns409_WhenEmailAlreadyExists()
    {
        // Arrange
        var created = await CreateAndGetUser();
        var duplicateReq = CreateUserRequest(email: IntegrationTestAuthHelpers.GetTestEmail(created.Id));

        // Act
        var res = await Client.PostAsJsonAsync("/api/join", duplicateReq, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateUser_Returns409_WhenNicknameAlreadyExists()
    {
        // Arrange
        const string nickname = "TakenNickname";
        var created = await CreateAndGetUser(CreateUserRequest(nickname: nickname));
        var duplicateReq = CreateUserRequest(nickname: created.Nickname);

        // Act
        var res = await Client.PostAsJsonAsync("/api/join", duplicateReq, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertProblemDetailAsync(
            res,
            HttpStatusCode.Conflict,
            $"A user with nickname '{nickname}' already exists.");
    }

    [Fact]
    public async Task CheckNicknameAvailable_ReturnsTrue_WhenUnused()
    {
        // Act
        var res = await Client.GetAsync("/api/join/nickname-available?nickname=UnusedNick", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<NicknameAvailabilityResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.True(body.Available);
    }

    [Fact]
    public async Task CheckNicknameAvailable_ReturnsFalse_WhenTaken()
    {
        // Arrange
        const string nickname = "TakenNick";
        await CreateAndGetUser(CreateUserRequest(nickname: nickname));

        // Act
        var res = await Client.GetAsync($"/api/join/nickname-available?nickname={nickname}", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<NicknameAvailabilityResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.False(body.Available);
    }

    [Fact]
    public async Task CheckNicknameAvailable_Returns400_WhenEmpty()
    {
        // Act — empty query value fails [ApiController] required binding, not service validation
        var res = await Client.GetAsync("/api/join/nickname-available?nickname=", TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.BadRequest);
    }

    // --- LOGIN (POST /api/login) ---

    [Fact]
    public async Task Login_Returns200_WhenCredentialsValid()
    {
        // Arrange
        var created = await CreateAndGetUser();
        var req = new LoginRequestDto
        {
            Email = IntegrationTestAuthHelpers.GetTestEmail(created.Id),
            Password = testUserPassword
        };

        // Act
        var res = await Client.PostAsJsonAsync("/api/login", req, TestContext.Current.CancellationToken);

        // Assert
        await IntegrationAssertions.AssertStatusAsync(res, HttpStatusCode.OK);

        var loginResult = await res.Content.ReadFromJsonAsync<LoginResponseDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(loginResult);
        Assert.False(string.IsNullOrWhiteSpace(loginResult.AccessToken));

        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(UsersWebApplicationFactory.TestJwtSecret));
        var principal = handler.ValidateToken(
            loginResult.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "Tangle",
                ValidateAudience = true,
                ValidAudience = "TangleClient",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
            },
            out _);
        var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Assert.Equal(created.Id.ToString(), sub);
    }

    [Fact]
    public async Task Login_Returns401_WhenWrongPassword()
    {
        // Arrange
        var created = await CreateAndGetUser();
        const string wrongPassword = "wrongpassword789!";
        var req = new LoginRequestDto
        {
            Email = IntegrationTestAuthHelpers.GetTestEmail(created.Id),
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
