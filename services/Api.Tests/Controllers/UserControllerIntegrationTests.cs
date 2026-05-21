using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class UserControllerIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    private readonly string testUserPassword = "testtest123!";

    private async Task<UserGetResponseDto> CreateUserForTest(string testMethodName, string password = "testtest123!", long index = 1)
    {
        var email = testMethodName + index.ToString() + "@test.com";
        var nickname = $"{testMethodName}User" + index.ToString();
        var req = new UserCreateRequestDto
        {
            Email = email,
            Password = password,
            Nickname = nickname,
        };
        var create = await Client.PostAsJsonAsync("/api/join", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var getAll = await Client.GetAsync("/api/users");
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
        var login = await Client.PostAsJsonAsync("/api/login", req);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var loginRes = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginRes);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginRes.AccessToken);
    }


    // --- GET ---

    [Fact]
    public async Task GetUserById_Returns200_WhenFound()
    {
        // Arrange
        const string testMethodName = "GetUserById";
        var created = await CreateUserForTest(testMethodName, testUserPassword);

        // Act
        var res = await Client.GetAsync("/api/users/" + created.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task GetUserById_Returns404_WhenMissing()
    {
        // Arrange
        const long missingUserId = 123456;

        // Act
        var res = await Client.GetAsync("/api/users/" + missingUserId);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // --- PATCH ---

    [Fact]
    public async Task UpdateUser_Returns200_WhenValidRequest()
    {
        // Arrange
        const string testMethodName = "UserPatch";
        const string newNickname = "new";
        var created = await CreateUserForTest(testMethodName, testUserPassword);
        await LoginAs(created, testUserPassword);
        var updatedAtBefore = created.UpdatedAt;

        // Act
        var patch = await Client.PatchAsJsonAsync($"/api/users", new UserPatchRequestDto(created.Id, newNickname));

        // Assert
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var patched = await patch.Content.ReadFromJsonAsync<UserPatchResponseDto>();
        Assert.NotNull(patched);
        Assert.Equal(newNickname, patched.Nickname);
        Assert.True(updatedAtBefore < patched.UpdatedAt);
    }

    [Fact]
    public async Task UpdateUser_Returns404_WhenMissing()
    {
        // Arrange
        const string testMethodName = "UserPatchMissing";
        const string newNickname = "new";
        var created = await CreateUserForTest(testMethodName, testUserPassword);
        await LoginAs(created, testUserPassword);
        var delete = await Client.DeleteAsync($"/api/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // Act
        var patch = await Client.PatchAsJsonAsync($"/api/users", new UserPatchRequestDto(created.Id, newNickname));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_Returns401_WhenNotAuthenticated()
    {
        // Arrange
        const string testMethodName = "UserPatchUnauth";
        var created = await CreateUserForTest(testMethodName, testUserPassword);
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto(created.Id, "new"));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, patch.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_Returns200_WhenSameNickname()
    {
        // Arrange
        const string testMethodName = "UserPatchSameNickname";
        var created = await CreateUserForTest(testMethodName, testUserPassword);
        await LoginAs(created, testUserPassword);

        // Act
        var patch = await Client.PatchAsJsonAsync($"/api/users", new UserPatchRequestDto(created.Id, created.Nickname));

        // Assert
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var patched = await patch.Content.ReadFromJsonAsync<UserPatchResponseDto>();
        Assert.NotNull(patched);
        Assert.Equal(created.Nickname, patched.Nickname);
    }

    [Fact]
    public async Task UpdateUser_Returns409_WhenNicknameAlreadyExists()
    {
        // Arrange
        const string testMethodName = "UserPatchDuplicateNickname";
        var created = await CreateUserForTest(testMethodName, testUserPassword);
        var existingUser = await CreateUserForTest(testMethodName + "Existing", testUserPassword);
        await LoginAs(created, testUserPassword);

        // Act
        var patch = await Client.PatchAsJsonAsync($"/api/users", new UserPatchRequestDto(created.Id, existingUser.Nickname));

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, patch.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_Returns401_WhenUpdatingOtherUser()
    {
        // Arrange
        const string testMethodName = "UserPatchAuth";
        var owner = await CreateUserForTest(testMethodName + "Owner", testUserPassword);
        var attacker = await CreateUserForTest(testMethodName + "Attacker", testUserPassword);
        await LoginAs(attacker, testUserPassword);

        // Act
        var patch = await Client.PatchAsJsonAsync("/api/users", new UserPatchRequestDto(owner.Id, "hacked"));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, patch.StatusCode);
    }

    [Fact]
    public async Task UpdatePrivacy_Returns200_AndUpdatesFriendsListVisibility()
    {
        const string testMethodName = "UserPrivacy";
        var created = await CreateUserForTest(testMethodName, testUserPassword);
        await LoginAs(created, testUserPassword);

        var patch = await Client.PatchAsJsonAsync("/api/users/privacy",
            new UserPrivacySettingsUpdateRequestDto { FriendsListVisibility = FriendsListVisibility.Public });

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var body = await patch.Content.ReadFromJsonAsync<UserPrivacySettingsResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(FriendsListVisibility.Public, body.FriendsListVisibility);

        var get = await Client.GetAsync($"/api/users/{created.Id}");
        var user = await get.Content.ReadFromJsonAsync<UserGetResponseDto>();
        Assert.Equal(FriendsListVisibility.Public, user!.FriendsListVisibility);
    }

    [Fact]
    public async Task UpdatePrivacy_Returns401_WhenNotAuthenticated()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var patch = await Client.PatchAsJsonAsync("/api/users/privacy",
            new UserPrivacySettingsUpdateRequestDto { FriendsListVisibility = FriendsListVisibility.Private });

        Assert.Equal(HttpStatusCode.Unauthorized, patch.StatusCode);
    }

    // --- DELETE ---

    [Fact]
    public async Task DeleteUser_Returns204_WhenFound()
    {
        // Arrange
        const string testMethodName = "UserDelete";
        var created = await CreateUserForTest(testMethodName, testUserPassword);
        await LoginAs(created, testUserPassword);

        // Act
        var delete = await Client.DeleteAsync($"/api/users/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var found = await Client.GetAsync($"/api/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, found.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_Returns404_WhenMissing()
    {
        // Arrange
        const string testMethodName = "UserDeleteMissing";
        var created = await CreateUserForTest(testMethodName, testUserPassword);
        await LoginAs(created, testUserPassword);
        var delete = await Client.DeleteAsync($"/api/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // Act
        delete = await Client.DeleteAsync($"/api/users/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_Returns401_WhenDeletingOtherUser()
    {
        // Arrange
        const string testMethodName = "UserDeleteAuth";
        var owner = await CreateUserForTest(testMethodName + "Owner", testUserPassword);
        var attacker = await CreateUserForTest(testMethodName + "Attacker", testUserPassword);
        await LoginAs(attacker, testUserPassword);

        // Act
        var delete = await Client.DeleteAsync($"/api/users/{owner.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    }
}
